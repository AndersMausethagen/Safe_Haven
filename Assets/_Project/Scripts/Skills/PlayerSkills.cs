using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Why a cast did or didn't happen.</summary>
public enum SkillCastResult
{
    Cast,
    EmptySlot,
    CoolingDown,
    /// <summary>Dead, interacting, dodging or still casting the previous skill.</summary>
    Busy,
    NoStamina,
    NoFocus,
    /// <summary>An item's slot, and none of it is carried.</summary>
    NoneCarried,
    /// <summary>A weapon's own technique, without one of its weapons in hand.</summary>
    WrongWeapon,
}

/// <summary>
/// The player's skills, V Rising style: a book of known skills, a five-slot bar (the loadout) arranged in the skill
/// window, and casting from the bar by hotkey. A cast pays stamina (an empty pool refuses it), starts the skill's own
/// cooldown and then carries out the definition: lunge, hit through AttackResolver, heal and timed buffs.
/// <para>
/// Where it belongs: on the PlayerArmature prefab, reached through Player.Current.Skills. Input arrives through
/// PlayerStateMachine.SkillRequested. Known skills and the loadout travel between scenes in PlayerSnapshot; cooldowns
/// and buffs are runtime-only.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerStateMachine), typeof(PlayerMovement), typeof(Combatant))]
[RequireComponent(typeof(Damageable))]
public sealed class PlayerSkills : MonoBehaviour, IPlayerActivity
{
    [SerializeField] private PlayerSkillSettings settings;
    [Tooltip("Layers skill hits can reach. Targets still have to be damageable and hostile.")]
    [SerializeField] private LayerMask hitLayers = ~0;

    private readonly List<SkillDefinition> known = new List<SkillDefinition>();
    private readonly SkillLoadout loadout = new SkillLoadout();
    private readonly SkillCooldowns cooldowns = new SkillCooldowns();
    private readonly Dictionary<SkillDefinition, float> activeBuffs = new Dictionary<SkillDefinition, float>();
    private readonly List<SkillDefinition> expired = new List<SkillDefinition>();

    private PlayerStateMachine stateMachine;
    private PlayerMovement movement;
    private Combatant combatant;
    private Damageable health;
    private PlayerStamina stamina;
    private PlayerCombat combat;
    private PlayerDodge dodge;
    private bool initialised;

    private float busyUntil;
    private float lungeEndsAt = float.NegativeInfinity;
    // Standing in the cast's rune circle until it is gone (HoldsStillFor); negative while not held.
    private float heldStillUntil = float.NegativeInfinity;
    // The creatures a skill's blow struck as it landed (Damageable.AnyDamaged during its hit), for its look's struck effects.
    private readonly List<Vector3> struck = new List<Vector3>();
    // The creatures the landing skill struck and what each took, for its altar options' statuses and payoffs.
    private readonly List<(Damageable foe, float took)> struckFoes = new List<(Damageable foe, float took)>();
    /// <summary>This player's skill left ground behind (Skill › Leaves), at these points: CoopGround shows it to the others.</summary>
    public static event System.Action<SkillDefinition, IReadOnlyList<Vector3>> GroundLeft;

    // Entering Play doesn't reload scripts in this project: statics carry over from the last session unless cleared.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetGroundLeft() => GroundLeft = null;

    // The dodge's readied blow (Docs/SkillsImprovementResearch_2026-11-24.md 5): the next swing within this long lays the status of the
    // ability cast last; every creature that one swing strikes takes it (a cleave), then it is spent.
    private const float ReadiedSeconds = 2.5f;
    private const float ReadiedSwingSeconds = 0.25f;
    // Only a dodge soon after the ability readies its blow (the review, 2026-11-24: one cast early in a raid made every dodge after it
    // lay the status, a free passive); once readied, a later dodge needs another cast.
    private const float ReadiedAfterCastSeconds = 8f;
    private SkillDefinition lastCast;
    private float lastCastAt = float.NegativeInfinity;
    private bool toldReadied;
    private StatusEffectDefinition readiedStatus;
    private float readiedUntil;
    private float readiedLanded = -1f;

    /// <summary>The status the next swing will lay, readied by a dodge after an ability; null when none (ArtBladeView glows in its colour).</summary>
    public StatusEffectDefinition ReadiedStatus => readiedStatus != null && Time.time < readiedUntil
        && (readiedLanded < 0f || Time.time - readiedLanded <= ReadiedSwingSeconds) ? readiedStatus : null;

    // What an ability lays: its blow's status, or what a war cry lays on those who hear it.
    private static StatusEffectDefinition SchoolStatusOf(SkillDefinition skill) =>
        skill.Attack != null && skill.Attack.AppliesStatus != null ? skill.Attack.AppliesStatus : skill.FoesStatus;

    // A skill pressed while busy, cast when it can be within this long (Docs/SkillsImprovementResearch_2026-11-24.md 2).
    private const float BufferSeconds = 0.25f;
    private int bufferedSlot = -1;
    private float bufferedUntil;

    // A channel under way (Skill › Channel Seconds): which, from which slot, until when, and its next strike.
    private SkillDefinition channeling;
    private int channelSlot = -1;
    private float channelStartedAt;
    private float channelEndsAt;
    private float nextChannelTick = float.MaxValue;

    /// <summary>A channel ended (let go, run out, dodged or staggered): the body leaves its loop (PlayerAnimationController).</summary>
    public event Action ChannelEnded;

    public bool IsChanneling => channeling != null;

    /// <summary>The channel under way, or null (the cast bar, CastBarView).</summary>
    public SkillDefinition Channeling => channeling;

    /// <summary>Seconds the channel under way has run, from the cast.</summary>
    public float ChannelElapsed => channeling != null ? Mathf.Max(0f, Time.time - channelStartedAt) : 0f;

    /// <summary>The longest the channel under way can run, from the cast to its last strike.</summary>
    public float ChannelLength => channeling != null ? Mathf.Max(0f, channelEndsAt - channelStartedAt) : 0f;

    /// <summary>The skills' buffs on the player now (a war cry, a blessing), each with the Time.time it wears off (PlayerConditionsView).</summary>
    public IReadOnlyDictionary<SkillDefinition, float> ActiveBuffs => activeBuffs;

    /// <summary>How much faster blows taken fill Focus now: the most any lasting buff gives (Bear-Hide), 1 with none.</summary>
    public float FocusFromBlowsScale
    {
        get
        {
            float scale = 1f;
            foreach (KeyValuePair<SkillDefinition, float> buff in activeBuffs)
            {
                if (buff.Key != null && Time.time < buff.Value)
                {
                    scale = Mathf.Max(scale, buff.Key.FocusFromBlows);
                }
            }

            return scale;
        }
    }

    // Which skill keys are down (pressed through the input, let go on release): a channel lasts only while its key is held, so one
    // cast from a buffered press or after an aimed release, with the key already up, ends after its first blow.
    private readonly bool[] held = new bool[SkillLoadout.SlotCount];

    // A war cry ringing out (Skill › Foes Status): which, and when.
    private SkillDefinition pendingCry;
    private float pendingCryAt;
    private readonly List<HitDetector.Hit> heard = new List<HitDetector.Hit>();
    private bool collectingStruck;

    /// <summary>True while a skill is being cast or its lunge and hit are under way: a swing pressed now waits for it (PlayerCombat).</summary>
    public bool IsBusy => Time.time < busyUntil;

    /// <summary>Seconds until the skill under way is done.</summary>
    public float BusyLeft => Mathf.Max(0f, busyUntil - Time.time);

    /// <summary>When the last lunging skill with a blow (Wolf's Lunge) is done: a swing right after it goes on with the chain.</summary>
    public float LungeEndsAt => lungeEndsAt;

    /// <summary>True from the end of the last lunging skill for <paramref name="seconds"/>.</summary>
    public bool LungedWithin(float seconds) => Time.time >= lungeEndsAt && Time.time - lungeEndsAt <= seconds;
    private SkillDefinition pendingHit;
    private float pendingHitAt;
    private Vector3 pendingDirection;

    public SkillLoadout Loadout => loadout;
    public IReadOnlyList<SkillDefinition> KnownSkills => known;

    /// <summary>Raised when a skill is cast, with its slot. Animation and the skill bar listen.</summary>
    public event Action<int, SkillDefinition> SkillCast;

    /// <summary>Raised when a cast from a slot was refused, and why.</summary>
    public event Action<int, SkillCastResult> CastRefused;

    /// <summary>Raised when the item in a slot was used, with the slot. The skill bar listens.</summary>
    public event Action<int, ItemDefinition> BarItemUsed;

    /// <summary>Raised after the set of known skills changes.</summary>
    public event Action KnownSkillsChanged;

    private void Awake()
    {
        stateMachine = GetComponent<PlayerStateMachine>();
        movement = GetComponent<PlayerMovement>();
        combatant = GetComponent<Combatant>();
        health = GetComponent<Damageable>();
        stamina = GetComponent<PlayerStamina>();
        combat = GetComponent<PlayerCombat>();
        dodge = GetComponent<PlayerDodge>();
        guard = GetComponent<PlayerGuard>();
        EnsureInitialised();
    }

    private PlayerGuard guard;

    private void OnEnable()
    {
        stateMachine.SkillRequested += OnSkillRequested;
        stateMachine.SkillAimed += OnSkillAimed;
        stateMachine.SkillReleased += OnSkillReleased;
        Damageable.AnyDamaged += OnAnyDamaged;
        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }

        PlayerInterrupts.For(gameObject).Add(this);

        if (TryGetComponent(out bagItems))
        {
            bagItems.ConsumableArrived += OnConsumableArrived;
            bagItems.Received += OnBagReceived;
        }

        if (combat != null)
        {
            combat.WeaponChanged += OnWeaponChanged;
        }
    }

    // A weapon of copper or better teaches its kind's own technique the first time it is wielded (PlayerSkillSettings › Weapon Techniques).
    private void OnWeaponChanged(WeaponDefinition weapon) => LearnWeaponTechnique(weapon);

    /// <summary>Learns the technique <paramref name="weapon"/>'s kind teaches, if it is of the tier that teaches it and the technique
    /// isn't known yet. True when one was learned.</summary>
    public bool LearnWeaponTechnique(WeaponDefinition weapon)
    {
        EnsureInitialised();
        SkillDefinition technique = weapon != null && settings != null ? settings.TechniqueOf(weapon.Signature) : null;
        if (technique == null || IsKnown(technique) || TierOf(weapon) < settings.LearnAtTier)
        {
            return false;
        }

        return Learn(technique, true, $"Your hands learn the way of {WeaponFamilies.Name(weapon.Signature)}: {technique.DisplayName}.");
    }

    // The item tier of a weapon: the one in hand, or the first item that is it. Bare fists are no item: always one's own, so they teach
    // theirs the first time the player fights barehanded.
    private int TierOf(WeaponDefinition weapon)
    {
        if (TryGetComponent(out PlayerEquipment equipment) && equipment.MainHand.Item != null && equipment.MainHand.Item.Weapon == weapon)
        {
            return equipment.MainHand.Item.Tier;
        }

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (content != null)
        {
            foreach (ItemDefinition item in content.Items)
            {
                if (item != null && item.Weapon == weapon)
                {
                    return item.Tier;
                }
            }
        }

        // Bare fists are no item: always one's own, but they teach on a blow landed barehanded, not by being wielded (OnAnyDamaged).
        return weapon.Signature == WeaponSignature.Flurry && fistsLanded ? int.MaxValue : 0;
    }

    private ItemInventory bagItems;

    // Lore come into the bag, whichever way: the find is announced (SkillLore).
    private void OnBagReceived(ItemDefinition item, int count)
    {
        if (item != null && item.IsSkillLore)
        {
            SkillLore.Announce(gameObject, item);
        }
    }

    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        // The readied blow: the first swing after a dodge lays the readied status on all it strikes. A swing of the weapon in hand only:
        // a thrown skill's weapon (Troll's Hurl) strikes as a heavy melee blow but from its flight, after the cast.
        if (!collectingStruck && readiedStatus != null && victim != null && damage.Source == combatant && result.Damage > 0f && !result.Blocked
            && (damage.Technique & HitTechnique.Melee) != 0 && Time.time < readiedUntil
            && combat != null && damage.Attack != null && damage.Attack == combat.ActiveAttack)
        {
            if (readiedLanded < 0f)
            {
                readiedLanded = Time.time;
            }

            if (Time.time - readiedLanded <= ReadiedSwingSeconds)
            {
                StatusEffects.Apply(victim, readiedStatus, combatant);
            }
        }

        if (readiedLanded >= 0f && Time.time - readiedLanded > ReadiedSwingSeconds)
        {
            readiedStatus = null;
            readiedLanded = -1f;
        }

        if (collectingStruck && victim != null && damage.Source == combatant && struck.Count < 16 && result.Damage > 0f && !result.Blocked && !result.Parried)
        {
            struck.Add(victim.transform.position);
            struckFoes.Add((victim, result.Damage));
            NoteStatusesBefore(victim);
        }

        // Bare fists teach theirs on a blow landed barehanded: sheathing a weapon swaps fists in, and that taught it at every load
        // (bug hunt 2026-11-27). A swing of the hands themselves: Troll's Hurl's boulder lands as a melee blow too (review 2026-11-27).
        if (!collectingStruck && damage.Source == combatant && result.Damage > 0f && combat != null && combat.Weapon != null
            && combat.Weapon.Signature == WeaponSignature.Flurry && (damage.Technique & HitTechnique.Melee) != 0
            && (damage.Technique & HitTechnique.Ability) == 0 && damage.Attack != null && damage.Attack == combat.ActiveAttack)
        {
            fistsLanded = true;
            LearnWeaponTechnique(combat.Weapon);
            fistsLanded = false;
        }
    }

    // What the skill being resolved can use up, on a foe it struck, as the foe had it before this blow: AfterHit lays the blow's own
    // status after this, and Surtr's Brand used up the Burning it had just laid on every foe (bug hunt 2026-11-27).
    private void NoteStatusesBefore(Damageable victim)
    {
        if (collectingFor == null || !victim.TryGetComponent(out StatusEffects effects))
        {
            return;
        }

        foreach (SkillOption option in Both(SkillChoices.For(collectingFor).Chosen, collectingFor.Combos))
        {
            if (option.consumes != null && effects.Has(option.consumes))
            {
                hadBefore.Add((victim, option.consumes));
            }
        }
    }

    // The skill whose blow is being resolved now (while Collecting Struck), and the statuses its foes had before it.
    private SkillDefinition collectingFor;
    private readonly List<(Damageable foe, StatusEffectDefinition status)> hadBefore = new List<(Damageable, StatusEffectDefinition)>();
    // True while a barehanded blow is teaching the fists' technique (TierOf lets fists, which are no item, through only then).
    private bool fistsLanded;
    // The weapon a pending skill blow was begun with.
    private WeaponDefinition pendingWeapon;

    // A consumable new to the bag goes on the first empty slot of the bar, 1 to 9 (the user, 2026-11-15: with 1-5 full they
    // went nowhere). One already on the bar stays where the player put it.
    private void OnConsumableArrived(ItemDefinition item)
    {
        if (loadout.IndexOf(item) >= 0)
        {
            return;
        }

        // Slots 6-9 first: 1-5 are where abilities go as they are learned (the user, 2026-11-24: skills stay 1-5); then any free one.
        int free = -1;
        for (int slot = 5; slot < SkillLoadout.SlotCount && free < 0; slot++)
        {
            if (loadout.Get(slot) == null && loadout.GetItem(slot) == null)
            {
                free = slot;
            }
        }

        if (free < 0)
        {
            free = loadout.FirstEmptySlot();
        }

        if (free >= 0)
        {
            loadout.EquipItem(free, item);
        }
    }

    private void OnDisable()
    {
        stateMachine.SkillRequested -= OnSkillRequested;
        stateMachine.SkillAimed -= OnSkillAimed;
        stateMachine.SkillReleased -= OnSkillReleased;
        Damageable.AnyDamaged -= OnAnyDamaged;
        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        if (TryGetComponent(out PlayerInterrupts interrupts))
        {
            interrupts.Remove(this);
        }

        if (bagItems != null)
        {
            bagItems.ConsumableArrived -= OnConsumableArrived;
            bagItems.Received -= OnBagReceived;
        }

        if (combat != null)
        {
            combat.WeaponChanged -= OnWeaponChanged;
        }

        pendingHit = null;
        pendingCry = null;
        LetGoOfFeet();
        foreach (SkillDefinition skill in activeBuffs.Keys)
        {
            combatant.Stats.RemoveModifiersFrom(skill);
        }

        activeBuffs.Clear();
    }

    private void Update()
    {
        // Death cancels a hit that has not landed yet, just as a dodge does.
        if (!health.IsAlive && pendingHit != null)
        {
            pendingHit = null;
            SkillAreaMark.Hide();
        }

        // Held by something else (a cinematic, the story, an interaction): the key's release never arrives (the input handler is off),
        // so what was being aimed or channelled is let go here, not left drawn on the ground until the key is pressed again.
        if ((aimingSlot >= 0 || channeling != null) && PlayerControl.HeldOn(gameObject))
        {
            LetGoOfKeys();
        }

        if (aimingSlot >= 0)
        {
            ShowAim();
        }

        if (bufferedSlot >= 0)
        {
            int slot = bufferedSlot;
            // Kept as long as a heavy's wind-up lasts (however early in it, or however long it is held charging), then as long again.
            if (combat != null && combat.IsWindingUp)
            {
                bufferedUntil = Mathf.Max(bufferedUntil, Time.time + BufferSeconds);
            }

            if (Time.time > bufferedUntil || !health.IsAlive)
            {
                bufferedSlot = -1;
                // Never lost without a word: the bar shows it was refused as busy.
                if (health.IsAlive)
                {
                    CastRefused?.Invoke(slot, SkillCastResult.Busy);
                }
            }
            else if (CheckCast(loadout.Get(slot)) != SkillCastResult.Busy)
            {
                bufferedSlot = -1;
                TryCast(slot);
            }
        }

        KeepFeet(Time.time);

        if (pendingCry != null && (!health.IsAlive || Time.time >= pendingCryAt))
        {
            SkillDefinition cry = pendingCry;
            pendingCry = null;
            if (health.IsAlive)
            {
                RingOut(cry);
            }
        }

        if (pendingHit != null && Time.time >= pendingHitAt)
        {
            SkillDefinition skill = pendingHit;
            pendingHit = null;
            WeaponDefinition weapon = pendingWeapon;
            float weaponDamage = weapon != null ? weapon.Damage : 0f;
            // The Runecraft skill scales every spell's damage; its hits carry the Spell technique, which trains it. A physical skill
            // (Steel, Beast) is a heavy melee blow instead: Melee trains, and a frozen foe shatters under it.
            float runecraft = skill.IsPhysical ? 1f : Mathf.Max(0f, 1f + combatant.Stats.Get(StatType.SpellDamage));
            HitTechnique kind = AbilityTechnique(skill);
            // What it strikes, for the effects that land on each creature hit (SkillLook.struck).
            struck.Clear();
            struckFoes.Clear();
            hadBefore.Clear();
            collectingFor = skill;
            collectingStruck = true;
            try
            {
                float multiplier = Power(skill) * runecraft * SkillChoices.For(skill).Damage * skill.WeaponScale(weapon);
                if (skill.Throws)
                {
                    Throw(skill, weaponDamage, multiplier, kind);
                }
                else
                {
                    AttackResolver.Execute(combatant, skill.Attack, weaponDamage, transform.position, pendingDirection, hitLayers, multiplier, 1f, kind);
                }
            }
            finally
            {
                collectingStruck = false;
            }

            SkillFx.Release(skill, transform.position, pendingDirection, struck);
            AfterOptions(skill, struckFoes);
            LeaveGround(skill, pendingDirection);
            if (skill.HasBuffs)
            {
                ApplyBuffs(skill, Time.time);
            }
            if (skill.ChannelSeconds > 0f && channeling == skill)
            {
                nextChannelTick = Time.time + skill.ChannelTick;
            }
        }

        if (channeling != null && (channelSlot < 0 || channelSlot >= held.Length || !held[channelSlot]))
        {
            EndChannel();
        }

        if (channeling != null)
        {
            Channel();
        }

        if (activeBuffs.Count > 0)
        {
            ExpireBuffs();
        }
    }

    public bool IsKnown(SkillDefinition skill) => skill != null && known.Contains(skill);

    public float CooldownRemaining(SkillDefinition skill) => cooldowns.Remaining(skill, Time.time);

    /// <summary>Share of the skill's cooldown still to go, from 1 (just cast) to 0 (ready).</summary>
    public float CooldownFraction(SkillDefinition skill) =>
        skill != null && skill.WeaponArt != WeaponArtSlot.None ? (combat != null ? combat.ArtCooldownFraction : 0f) : cooldowns.Fraction(skill, Time.time);

    /// <summary>Learns a skill. Returns false when it was already known.</summary>
    public bool Learn(SkillDefinition skill, bool announce = true) => Learn(skill, announce, null);

    /// <summary>Learns a skill, announced in <paramref name="words"/> (null: "New skill: ..."). Returns false when it was already known.</summary>
    public bool Learn(SkillDefinition skill, bool announce, string words)
    {
        EnsureInitialised();
        if (!AddKnown(skill))
        {
            return false;
        }

        if (announce)
        {
            GameMessages.Post(words ?? "New skill: " + skill.DisplayName, MessageKind.Gain);
            // Seen at once: into the first free slot of 1-5 that takes its kind (the user, 2026-11-24: "the skills should be visible";
            // 2026-11-27: each slot a kind); moved after to any other that takes it.
            int free = loadout.IndexOf(skill) < 0 ? loadout.FirstFittingEmpty(skill) : -1;
            if (free >= 0)
            {
                loadout.Equip(free, skill);
            }
        }

        KnownSkillsChanged?.Invoke();
        return true;
    }

    /// <summary>Puts a known skill in a bar slot (null clears it). Returns false when nothing changed.</summary>
    public bool Equip(int slot, SkillDefinition skill)
    {
        if (skill != null && !IsKnown(skill))
        {
            return false;
        }

        return loadout.Equip(slot, skill);
    }

    /// <summary>
    /// Makes the book and bar exactly the given ones, for a scene handover. Called by PlayerSnapshot, possibly before
    /// this component's Awake; it replaces the starting setup.
    /// </summary>
    public void Restore(IEnumerable<SkillDefinition> knownSkills, SkillDefinition[] slots, ItemDefinition[] slotItems = null)
    {
        EnsureInitialised();
        known.Clear();
        if (knownSkills != null)
        {
            foreach (SkillDefinition skill in knownSkills)
            {
                AddKnown(skill);
            }
        }

        // A save from before the weapon-art entries (2026-11-24) knows them too: they come with every game, and are put on no slot here.
        if (settings != null)
        {
            foreach (SkillDefinition starting in settings.StartingSkills)
            {
                if (starting != null && starting.WeaponArt != WeaponArtSlot.None)
                {
                    AddKnown(starting);
                }
            }
        }

        var allowed = new SkillDefinition[SkillLoadout.SlotCount];
        for (int i = 0; i < allowed.Length && slots != null && i < slots.Length; i++)
        {
            allowed[i] = IsKnown(slots[i]) ? slots[i] : null;
        }

        loadout.SetAll(allowed, slotItems);
        loadout.SetItems(slotItems);
        // Slot 1 takes only an art (2026-11-27): a bar saved without the weapon's art there (before 2026-11-24, or with something else
        // on 1) gets it back, or the key would do nothing.
        if (loadout.IsEmpty(0) && settings != null)
        {
            foreach (SkillDefinition starting in settings.StartingSkills)
            {
                if (starting != null && starting.WeaponArt == WeaponArtSlot.Own && IsKnown(starting) && loadout.IndexOf(starting) < 0)
                {
                    loadout.Equip(0, starting);
                    break;
                }
            }
        }

        // The weapon's own technique is looked at once the gear is back (PlayerSnapshot, after PlayerEquipment.Restore): here the hand
        // still held the prefab's fists, and every load taught Rib-Breaker (bug hunt 2026-11-27).
        KnownSkillsChanged?.Invoke();
    }

    /// <summary>Casts the skill in a bar slot. Everything that can refuse it is checked before anything is paid.</summary>
    public SkillCastResult TryCast(int slot)
    {
        if (loadout.GetItem(slot) != null)
        {
            return UseBarItem(slot);
        }

        SkillDefinition skill = loadout.Get(slot);
        // The weapon's art from the bar: what the weapon in hand strikes, at the art's own cost and cooldown.
        if (skill != null && skill.WeaponArt != WeaponArtSlot.None)
        {
            if (combat == null || !combat.HasArt(skill.WeaponArt == WeaponArtSlot.Rune))
            {
                GameMessages.Post(skill.WeaponArt == WeaponArtSlot.Rune ? "No rune is attuned to the weapon in hand." : "Nothing in hand has an art.",
                    MessageKind.Warning);
                return SkillCastResult.EmptySlot;
            }

            if (combat.TryArt(skill.WeaponArt == WeaponArtSlot.Rune))
            {
                return SkillCastResult.Cast;
            }

            SkillCastResult refused = combat.ArtCooldownFraction > 0f ? SkillCastResult.CoolingDown : SkillCastResult.Busy;
            CastRefused?.Invoke(slot, refused);
            return refused;
        }

        SkillCastResult result = CheckCast(skill);
        // Pressed a moment before the swing or cast under way ends: kept, and cast as soon as it can be (as a swing's press is).
        if (result == SkillCastResult.Busy && health.IsAlive)
        {
            bufferedSlot = slot;
            bufferedUntil = Time.time + BufferSeconds;
            return result;
        }

        // A weapon's own technique wants one of its weapons in hand (2026-11-27).
        if (result == SkillCastResult.Cast && !skill.CastsWith(combat != null ? combat.Weapon : null))
        {
            GameMessages.Post($"{skill.DisplayName} needs {WeaponFamilies.Names(skill.Weapons)} in hand.", MessageKind.Warning);
            result = SkillCastResult.WrongWeapon;
        }

        // Only a skill that may be cast is paid for. (An empty slot came on to here and read the missing skill's cost: every
        // press of an empty key threw, and the HUD never said the slot was empty: the user's build, 2026-11-25.)
        if (result == SkillCastResult.Cast)
        {
            result = Pay(skill);
        }

        if (result != SkillCastResult.Cast)
        {
            CastRefused?.Invoke(slot, result);
            return result;
        }

        Perform(skill);
        SkillCast?.Invoke(slot, skill);
        return result;
    }

    // Focus circles, then stamina; nothing is taken unless both are there.
    private SkillCastResult Pay(SkillDefinition skill)
    {
        PlayerFocus focus = GetComponent<PlayerFocus>();
        int circles = CirclesFor(skill);
        if (circles > 0 && focus != null && focus.Circles < circles)
        {
            focus.TrySpendCircles(circles); // refused: lets the HUD say so
            return SkillCastResult.NoFocus;
        }

        if (skill.StaminaCost > 0f && stamina != null && !stamina.TrySpend(SpellCost(skill)))
        {
            return SkillCastResult.NoStamina;
        }

        if (circles > 0 && focus != null)
        {
            // An option that makes it cheaper (Cost under 1) gives that share of what it took back, into the circle filling.
            float spent = focus.TrySpendCircles(circles);
            focus.Gain(spent * Mathf.Clamp01(1f - SkillChoices.For(skill).Cost));
        }

        return SkillCastResult.Cast;
    }

    // A consumable on the bar: its key uses one from the bag (or the safe pocket), as the quickbar does. Eating and
    // drinking keep their own commitment (PlayerEating); only being dead or mid-dodge refuses it here.
    private SkillCastResult UseBarItem(int slot)
    {
        ItemDefinition item = loadout.GetItem(slot);
        ItemInventory inventory = GetComponent<ItemInventory>();
        SkillCastResult result = !health.IsAlive || (dodge != null && dodge.IsDodging) || stateMachine.CurrentState == PlayerState.Interacting
            ? SkillCastResult.Busy
            : inventory == null || inventory.CountCarried(item) <= 0 ? SkillCastResult.NoneCarried
            : inventory.UseFirst(item) ? SkillCastResult.Cast : SkillCastResult.Busy;
        if (result == SkillCastResult.NoneCarried)
        {
            GameMessages.Post($"No {item.DisplayName} left.", MessageKind.Warning);
        }

        if (result != SkillCastResult.Cast)
        {
            CastRefused?.Invoke(slot, result);
            return result;
        }

        BarItemUsed?.Invoke(slot, item);
        return result;
    }

    private SkillCastResult CheckCast(SkillDefinition skill)
    {
        if (skill == null)
        {
            return SkillCastResult.EmptySlot;
        }

        if (!health.IsAlive
            || stateMachine.CurrentState == PlayerState.Interacting
            || (dodge != null && dodge.IsDodging)
            || movement.IsDashing
            || (TryGetComponent(out PlayerEating eating) && eating.IsEating)
            // Rocked back, floored, stunned by a broken guard or locked in a finishing blow: no more a skill than a swing.
            || (combat != null && (combat.IsFlinching || combat.IsFinishing))
            // A heavy's blow still on its way: the skill waits for it (kept, and cast once it lands). A normal strike gives way to it
            // instead (Perform ends it), never both blows landing. Waiting on every wind-up, War Cry pressed while attacking waited on
            // swing after swing and was lost (the user, 2026-11-25: "often when pressing War Cry it doesnt activate").
            || (combat != null && !combat.GivesWayToSkill)
            || (guard != null && guard.IsStunned)
            || Time.time < busyUntil)
        {
            return SkillCastResult.Busy;
        }

        return cooldowns.IsReady(skill, Time.time) ? SkillCastResult.Cast : SkillCastResult.CoolingDown;
    }

    private void Perform(SkillDefinition skill)
    {
        float now = Time.time;
        // Spell Echo: now and then the words leave no weariness behind.
        if (Techniques.Has(PerkTechnique.SpellEcho) && UnityEngine.Random.value < TechniqueSettings.Current.SpellEchoChance)
        {
            GameMessages.Post($"Spell Echo: {skill.DisplayName} is ready again.", MessageKind.Gain);
            Techniques.Used(PerkTechnique.SpellEcho);
        }
        else
        {
            cooldowns.Start(skill, now, CooldownOf(skill));
        }
        busyUntil = now + skill.CastTime;
        // A normal strike under way gives way to the skill (CheckCast let it through only where it does).
        if (combat != null)
        {
            combat.GiveWayToSkill();
        }

        // Like the weapon swing, the direction is locked in toward the cursor as the cast begins.
        Vector3 direction = movement.AimDirection;
        // How it looks as it is cast, and its area on the ground until it lands (a skill without a blow still shows its cast).
        float windUp = skill.Attack != null ? (skill.LungeDistance > 0f ? skill.LungeDuration : 0f) + skill.Attack.HitDelay : 0f;
        SkillFx.Cast(skill, transform, direction, windUp, skill.LungeDistance > 0f ? LungeReach(skill) : -1f);
        if (skill.Attack == null)
        {
            SkillFx.Release(skill, transform.position, direction, null);
        }

        if (skill.FoesStatus != null)
        {
            pendingCry = skill;
            pendingCryAt = now + skill.FoesDelay;
        }

        // Feet planted in its rune circle until the circle is gone (the user, 2026-11-27: "keep the player centered in the runic circle
        // until after the effect is done"): walking off mid-cast left the circle, and the blow, behind. A dodge still breaks free
        // (a dash isn't scaled by speed, and it ends the cast: CancelCast).
        PlantFeet(skill, now);

        float hitAfter = 0f;
        if (skill.LungeDistance > 0f)
        {
            movement.Dash(direction * (LungeReach(skill) / skill.LungeDuration), skill.LungeDuration);
            hitAfter = skill.LungeDuration;
        }

        if (skill.Attack != null && skill.ChannelSeconds > 0f)
        {
            channeling = skill;
            channelSlot = loadout.IndexOf(skill);
            channelStartedAt = now;
            channelEndsAt = now + skill.Attack.HitDelay + skill.ChannelSeconds;
            nextChannelTick = float.MaxValue;
        }

        if (skill.Attack != null)
        {
            pendingHit = skill;
            // The weapon the blow was begun with: swapped mid-cast, a dagger's technique landed with a greatsword's damage (bug hunt).
            pendingWeapon = combat != null ? combat.Weapon : null;
            pendingHitAt = now + hitAfter + skill.Attack.HitDelay;
            busyUntil = Mathf.Max(busyUntil, skill.ChannelSeconds > 0f ? channelEndsAt : pendingHitAt);
            pendingDirection = direction;
            if (skill.LungeDistance > 0f)
            {
                lungeEndsAt = busyUntil;
            }

            GameStateManager gameState = GameStateManager.Current;
            if (gameState != null)
            {
                gameState.TrySetState(GameState.Combat);
            }
        }

        GameSession.Progress.CountUse(skill);
        TellPractised(skill);
        lastCast = skill;
        lastCastAt = now;
        float optionHeal = SkillChoices.For(skill).Heal;
        if (optionHeal > 0f)
        {
            health.Heal(optionHeal);
        }

        if (skill.Heal > 0f)
        {
            health.Heal(skill.Heal * Power(skill));
        }

        // A skill with a blow grants its buff once the blow has landed (Release), not before: Troll's Blood drained life from the very
        // blow that granted it, ten foes at once, and Cold Clarity sharpened its own lance (the review, 2026-11-25).
        if (skill.HasBuffs && skill.Attack == null)
        {
            ApplyBuffs(skill, now);
        }
    }

    /// <summary>How long casting <paramref name="skill"/> plants the caster's feet: as long as the marks its cast draws on the caster's
    /// own ground last (SkillLook.CasterGroundSeconds), and never for a lunge or a leap, whose whole point is the move.</summary>
    public static float HoldsStillFor(SkillDefinition skill) =>
        skill == null || skill.Look == null || skill.LungeDistance > 0f ? 0f : Mathf.Min(skill.Look.CasterGroundSeconds, LongestHold);

    // The longest the feet stay planted: as long as our own circles last (Huldra's 1.6 s). The VFX pack's circles dissolve for seconds
    // more, round a player already walking: held all that while, a quick blessing stood the player still for six (2026-11-27).
    private const float LongestHold = 1.6f;

    // Held from the cast until its circle is gone; never shortened by a quicker circle cast inside it. A lunge or a leap lets go at once:
    // carried out of the circle, the player was left frozen where it landed until the old circle's time ran out (review 2026-11-27).
    private void PlantFeet(SkillDefinition skill, float now)
    {
        if (skill != null && skill.LungeDistance > 0f)
        {
            LetGoOfFeet();
            return;
        }

        float still = HoldsStillFor(skill);
        if (still <= 0f)
        {
            return;
        }

        heldStillUntil = Mathf.Max(heldStillUntil, now + still);
        movement.SetSpeedScale(this, 0f);
    }

    // Let go once the circle is gone, or the caster has fallen.
    private void KeepFeet(float now)
    {
        if (heldStillUntil >= 0f && (now >= heldStillUntil || !health.IsAlive))
        {
            LetGoOfFeet();
        }
    }

    private void LetGoOfFeet()
    {
        heldStillUntil = float.NegativeInfinity;
        if (movement != null)
        {
            movement.SetSpeedScale(this, 1f);
        }
    }

    /// <summary>How an ability's blow lands: a physical one as a heavy melee blow, a spell as a spell; either marked as an ability.</summary>
    public static HitTechnique AbilityTechnique(SkillDefinition skill) =>
        (skill.IsPhysical ? HitTechnique.Melee | HitTechnique.Heavy : HitTechnique.Spell) | HitTechnique.Ability;

    /// <summary>What deepening a skill at the altar multiplies its damage, healing and buffs by.</summary>
    public static float Power(SkillDefinition skill) => 1f + EssenceSettings.Current.PowerAt(GameSession.Progress.RankOf(skill));

    /// <summary>The skill's cooldown at its rank.</summary>
    // A spell's stamina after the Runecraft skill: a beginner's words cost more breath.
    private float SpellCost(SkillDefinition skill) =>
        SkillChoices.For(skill).Cost * (skill.IsPhysical || combatant == null ? skill.StaminaCost
            : skill.StaminaCost * Mathf.Max(0.1f, 1f + combatant.Stats.Get(StatType.SpellStaminaCost)));

    /// <summary>The Focus circles a cast spends: its own price, never more than the bar holds (CombatTechniqueSettings › Focus Circles),
    /// so an ultimate's full bar stays reachable if the bar is ever made shorter.</summary>
    public static int CirclesFor(SkillDefinition skill) =>
        skill == null ? 0 : Mathf.Min(skill.FocusCircles, Mathf.Max(1, CombatTechniqueSettings.Current.FocusCircles));

    public static float CooldownOf(SkillDefinition skill) =>
        skill.Cooldown * (1f - EssenceSettings.Current.CooldownCutAt(GameSession.Progress.RankOf(skill))) * SkillChoices.For(skill).Cooldown;

    private void ApplyBuffs(SkillDefinition skill, float now)
    {
        // Recasting refreshes the buff rather than stacking it.
        combatant.Stats.RemoveModifiersFrom(skill);
        foreach (SkillStatBuff buff in skill.Buffs)
        {
            float value = buff.value > 0f ? buff.value * Power(skill) * SkillChoices.For(skill).Buff : buff.value;
            combatant.Stats.AddModifier(new StatModifier(buff.stat, buff.op, value, skill));
        }

        activeBuffs[skill] = now + skill.BuffDuration;
    }

    // A war cry rings out: every foe in earshot (not behind a wall) is laid under its status.
    private void RingOut(SkillDefinition skill)
    {
        SkillChoices.Totals options = SkillChoices.For(skill);
        HitDetector.FindAround(transform.position, skill.FoesRadius * options.FoesRadius, combatant.Faction, hitLayers, heard);
        foreach (HitDetector.Hit hit in heard)
        {
            if (hit.Target is Damageable foe)
            {
                StatusEffects.Apply(foe, skill.FoesStatus, combatant);
                foreach (SkillOption option in options.Chosen)
                {
                    StatusEffects.Apply(foe, option.addsStatus, combatant);
                }
            }
        }

        heard.Clear();
    }

    // A thrown skill: its attack's projectile(s) flung along the aim, as a staff's cast is (PlayerCombat.Cast). Each strikes as the
    // skill's blow would: its technique, its power, its options' scale.
    private void Throw(SkillDefinition skill, float weaponDamage, float multiplier, HitTechnique kind)
    {
        AttackDefinition attack = skill.Attack;
        var volley = new CastVolley { Multiplier = multiplier, Poise = 1f, Technique = kind };
        var flight = new MonsterAttack
        {
            attack = attack, damage = weaponDamage, projectileSpeed = attack.CastSpeed, projectileRadius = attack.CastRadius,
            maximumRange = attack.CastRange, hazardPrefab = attack.CastHazard,
        };
        Vector3 from = transform.position + Vector3.up * 1.4f + pendingDirection * 0.6f;
        int count = Mathf.Max(1, attack.CastCount);
        for (int i = 0; i < count; i++)
        {
            float turn = (i - (count - 1) * 0.5f) * attack.CastSpread;
            Vector3 aim = Quaternion.Euler(0f, turn, 0f) * pendingDirection;
            GameObject thrown = PrefabPool.Spawn(attack.CastProjectile, from, Quaternion.LookRotation(aim));
            if (thrown.TryGetComponent(out MonsterProjectile projectile))
            {
                projectile.Initialize(combatant, flight, aim, volley);
            }
        }
    }

    // A channel's strikes while its key is held: each another blow round the caster, with its look; it ends when let go or run out.
    private void Channel()
    {
        if (!health.IsAlive || Time.time >= channelEndsAt)
        {
            EndChannel();
            return;
        }

        if (Time.time < nextChannelTick)
        {
            return;
        }

        nextChannelTick += channeling.ChannelTick;
        // The weapon the channel began with, as its first blow (review 2026-11-27: a swap mid-channel changed its damage).
        WeaponDefinition weapon = pendingWeapon;
        float weaponDamage = weapon != null ? weapon.Damage : 0f;
        float runecraft = channeling.IsPhysical ? 1f : Mathf.Max(0f, 1f + combatant.Stats.Get(StatType.SpellDamage));
        HitTechnique kind = AbilityTechnique(channeling);
        Vector3 aim = movement.AimDirection;
        aim.y = 0f;
        if (aim.sqrMagnitude > 0.0001f)
        {
            pendingDirection = aim.normalized;
            transform.rotation = Quaternion.LookRotation(pendingDirection, Vector3.up);
        }

        struck.Clear();
        struckFoes.Clear();
        hadBefore.Clear();
        collectingFor = channeling;
        collectingStruck = true;
        try
        {
            AttackResolver.Execute(combatant, channeling.Attack, weaponDamage, transform.position, pendingDirection, hitLayers,
                Power(channeling) * runecraft * SkillChoices.For(channeling).Damage * channeling.WeaponScale(weapon), 1f, kind);
        }
        finally
        {
            collectingStruck = false;
        }

        SkillFx.Release(channeling, transform.position, pendingDirection, struck);
        AfterOptions(channeling, struckFoes);
    }

    private void EndChannel()
    {
        if (channeling == null)
        {
            return;
        }

        channeling = null;
        channelSlot = -1;
        nextChannelTick = float.MaxValue;
        // Let go early: free again at once, but not before its first blow (paid for) has landed.
        busyUntil = Mathf.Min(busyUntil, pendingHit != null ? Mathf.Max(Time.time, pendingHitAt) : Time.time);
        ChannelEnded?.Invoke();
    }

    // The chosen altar options on what the skill struck (Docs/SkillsImprovementResearch_2026-11-24.md 4): a status laid, or one used
    // up for a payoff (health back, a burst of extra damage, another status in its place).
    // And the skill's own built-in combos (SkillDefinition.Combos, Docs/SkillChoiceResearch_2026-11-27.md 3.4), with the same rules.
    private const float PayoffHitStop = 0.07f;
    private const float PayoffShake = 0.35f;
    private void AfterOptions(SkillDefinition skill, List<(Damageable foe, float took)> foes)
    {
        SkillChoices.Totals options = SkillChoices.For(skill);
        if (options.Chosen.Count == 0 && skill.Combos.Count == 0 || foes.Count == 0)
        {
            return;
        }

        var called = new HashSet<string>();
        foreach ((Damageable foe, float took) in foes)
        {
            if (foe == null || !foe.IsAlive)
            {
                continue;
            }

            foreach (SkillOption option in Both(options.Chosen, skill.Combos))
            {
                // Not on a friend's copy of the host's creature: the host keeps its statuses, so it would be used up here and tick on there.
                if (option.consumes != null && foe.TryGetComponent(out StatusEffects effects) && !effects.IsCopy && effects.Has(option.consumes)
                    && hadBefore.Contains((foe, option.consumes)))
                {
                    if (option.consumeHeal > 0f)
                    {
                        health.Heal(option.consumeHeal);
                    }

                    if (option.consumeDamage > 0f && foe.IsAlive)
                    {
                        // The payoff staggers as it hurts (a Headsman's Drop on a Sundered foe topples it), and counts as the skill's
                        // own damage in the playtest log (its attack and the Ability technique).
                        var burst = new DamageInfo(combatant, skill.Attack, took * option.consumeDamage, skill.Attack.DamageType, false,
                            foe.transform.position + Vector3.up, pendingDirection, skill.Attack.PoiseDamage * option.consumeDamage, HitTechnique.Ability);
                        combatant.ReportDamageDealt(foe, burst, foe.ApplyDamage(burst));
                    }

                    // Used up after the burst, so the burst still has what the status gave (Sundered: takes more). Seen and heard as a
                    // burst (its Consume Look and Sound), with a beat of hit stop and a jolt: the set-up paying off has to feel it.
                    effects.End(option.consumes);
                    StatusEffects.NoteConsumed(foe, option.consumes, foe.transform.position + Vector3.up);
                    if (called.Count == 0)
                    {
                        if (CombatFeedback.Current != null) CombatFeedback.Current.HitStop(PayoffHitStop);
                        CameraShake.Add(PayoffShake);
                    }

                    if (option.consumeInto != null && foe.IsAlive)
                    {
                        StatusEffects.Apply(foe, option.consumeInto, combatant);
                    }

                    // Said once a cast, however many it struck.
                    if (called.Add(option.displayName))
                    {
                        GameMessages.Post(option.displayName + "!", MessageKind.Gain);
                    }
                }

                if (option.addsStatus != null && foe.IsAlive)
                {
                    StatusEffects.Apply(foe, option.addsStatus, combatant);
                }
            }
        }
    }

    private static IEnumerable<SkillOption> Both(IReadOnlyList<SkillOption> chosen, IReadOnlyList<SkillOption> combos)
    {
        foreach (SkillOption option in chosen)
        {
            yield return option;
        }

        foreach (SkillOption option in combos)
        {
            if (option != null)
            {
                yield return option;
            }
        }
    }

    // What the skill leaves on the ground where it lands (Skill › Leaves): patches spread out along its reach, nearest first.
    private void LeaveGround(SkillDefinition skill, Vector3 direction)
    {
        if (skill.Leaves == null || skill.Attack == null)
        {
            return;
        }

        var along = new SkillFxCue { place = SkillFxPlace.AlongReach, count = Mathf.Min(8, skill.LeavesCount + SkillChoices.For(skill).ExtraLeaves) };
        var points = SkillFx.Points(along, transform.position, direction, skill.Attack.Range, skill.Attack.Arc, null);
        foreach (Vector3 point in points)
        {
            GroundHazard.Spawn(skill.Leaves, point, combatant);
        }

        GroundLeft?.Invoke(skill, points);
    }

    private void ExpireBuffs()
    {
        expired.Clear();
        foreach (KeyValuePair<SkillDefinition, float> buff in activeBuffs)
        {
            if (Time.time >= buff.Value)
            {
                expired.Add(buff.Key);
            }
        }

        foreach (SkillDefinition skill in expired)
        {
            activeBuffs.Remove(skill);
            combatant.Stats.RemoveModifiersFrom(skill);
        }
    }

    // The cast that makes an ability practised enough for its next rank says so once (the altar was the only place to find out).
    private static void TellPractised(SkillDefinition skill)
    {
        int rank = GameSession.Progress.RankOf(skill);
        IReadOnlyList<EssenceSettings.SkillRank> ranks = EssenceSettings.Current.SkillRanks;
        if (rank < ranks.Count && ranks[rank].practisedUses > 0 && GameSession.Progress.UsesOf(skill) == ranks[rank].practisedUses)
        {
            GameMessages.Post($"{skill.DisplayName} is practised: rank {AltarWork.Roman(rank + 1)} is free at the altar.", MessageKind.Gain);
        }
    }

    // A dodge is the way out of trouble: like a weapon swing, a skill hit that hadn't landed yet is lost.
    private void OnDodged(Vector3 direction)
    {
        // A dodge readies the next swing with the status of the ability cast last (V Rising's veils).
        StatusEffectDefinition school = lastCast != null && Time.time - lastCastAt <= ReadiedAfterCastSeconds ? SchoolStatusOf(lastCast) : null;
        if (school != null)
        {
            readiedStatus = school;
            readiedUntil = Time.time + ReadiedSeconds;
            readiedLanded = -1f;
            lastCastAt = float.NegativeInfinity;
            // Said once a scene (the blade's tint was the only sign, and small from the high camera); the journal's Abilities page
            // tells it too.
            if (!toldReadied)
            {
                toldReadied = true;
                GameMessages.Post($"Your next swing lays {school.DisplayName}: a dodge after an ability readies its mark.", MessageKind.Info);
            }
        }

        CancelCast(dodged: true);
    }

    /// <summary>A blow that rocks the player (and a death) loses a skill hit not yet landed, as a weapon swing's is. A pause or a
    /// window only lets go of the keys: what was being aimed is not cast, a channel ends (the user's build, 2026-11-25: Esc while
    /// aiming cast the skill, as the paused keys came up).</summary>
    public PlayerInterrupt EndsOn => PlayerInterrupt.Staggered | PlayerInterrupt.Paused | PlayerInterrupt.WindowOpened;

    public void Interrupt(PlayerInterrupt reason)
    {
        bufferedSlot = -1;
        if (reason != PlayerInterrupt.Staggered)
        {
            LetGoOfKeys(); // a stagger loses the cast, not the aim: the key is still down, and its release casts as ever
        }

        if (reason == PlayerInterrupt.Paused || reason == PlayerInterrupt.WindowOpened)
        {
            return;
        }

        CancelCast(dodged: false);
    }

    // The cast under way is over, all of it: the blow and the cry not yet out are lost, a channel ends, and its moment no longer
    // keeps swings and skills waiting. A lunge stops where it is (a stagger or a death no longer slides on; a dodge has already
    // taken the dash over) and is no longer drawn as one. Before this, a dodge out of War Cry left swings queued for the rest of its
    // 1.35 s cast, and a staggered lunge slid on in its lunge pose (bug hunt, 2026-11-25).
    private void CancelCast(bool dodged)
    {
        pendingCry = null; // a cry cut short never rings out
        EndChannel();
        if (pendingHit != null)
        {
            SkillAreaMark.Hide();
        }

        pendingHit = null;
        busyUntil = Mathf.Min(busyUntil, Time.time);
        LetGoOfFeet();
        if (Time.time < lungeEndsAt)
        {
            if (!dodged && movement != null)
            {
                movement.EndDash();
            }

            lungeEndsAt = float.NegativeInfinity;
        }
    }

    private void OnSkillRequested(int slot)
    {
        SetHeld(slot, true);
        TryCast(slot);
    }

    // Every skill key counts as up: nothing is being aimed (its mark goes), and a channel held by its key ends on the next frame.
    private void LetGoOfKeys()
    {
        if (aimingSlot >= 0)
        {
            aimingSlot = -1;
            SkillAreaMark.Hide();
        }

        Array.Clear(held, 0, held.Length);
    }

    private void SetHeld(int slot, bool down)
    {
        if (slot >= 0 && slot < held.Length)
        {
            held[slot] = down;
        }
    }

    // Aim Skills First (GameOptions.AimSkillsFirst): the held skill's area follows the aim until the key is let go, then it casts.
    private int aimingSlot = -1;
    private PlayerFocus aimFocus;

    private void OnSkillAimed(int slot)
    {
        SetHeld(slot, true);
        SkillDefinition skill = loadout.Get(slot);
        // Nothing to aim (a blessing, a war cry, a ring round the player, an item, the weapon's art), or a channel: cast on the press.
        if (skill == null || !skill.IsAimed)
        {
            TryCast(slot);
            return;
        }

        aimingSlot = slot;
    }

    private void OnSkillReleased(int slot)
    {
        SetHeld(slot, false);
        if (channeling != null && channelSlot == slot)
        {
            EndChannel();
            return;
        }

        if (aimingSlot != slot)
        {
            return;
        }

        aimingSlot = -1;
        SkillAreaMark.Hide();
        // Aimed, then the hands were taken (a cart's handles, the oars, deep water) or an interaction began: let go without a cast.
        // So is a release that is the game pausing or the controls being taken (the Player map switched off lets its keys up), the
        // world map or a name being typed among them (gameplay suspended: review 2026-11-27, it cast behind the map).
        if ((stateMachine == null || stateMachine.CanUseSkills) && !GamePause.IsPaused && !PlayerControl.HeldOn(gameObject)
            && !GameInput.KeysGoing && !GameInput.IsGameplaySuspended)
        {
            TryCast(slot);
        }
    }

    private void ShowAim()
    {
        SkillDefinition skill = aimingSlot >= 0 ? loadout.Get(aimingSlot) : null;
        if (skill == null || skill.Attack == null || !health.IsAlive)
        {
            aimingSlot = -1;
            SkillAreaMark.Hide();
            return;
        }

        Vector3 aim = movement.AimDirection;
        Vector3 flat = new Vector3(aim.x, 0f, aim.z);
        Vector3 lands = transform.position + (flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero) * LungeReach(skill);
        // Grey while letting go would be refused (cooling down, no Focus circle full): it looked castable until the refusal on release.
        if (aimFocus == null)
        {
            aimFocus = GetComponent<PlayerFocus>();
        }

        bool refused = CheckCast(skill) == SkillCastResult.CoolingDown || (aimFocus != null && aimFocus.Circles < CirclesFor(skill))
            || !skill.CastsWith(combat != null ? combat.Weapon : null);
        Color colour = refused ? new Color(0.55f, 0.55f, 0.55f, 0.6f) : SkillSchools.ColorOf(skill.School);
        // A throw is marked along its flight, out to where it can reach, as wide as its volley spreads; a blow by its own reach and arc.
        if (skill.Throws)
        {
            SkillAreaMark.Show(transform.position, aim, skill.Attack.CastRange, Mathf.Max(8f, skill.Attack.CastSpread * 2f), colour, 10f);
        }
        else
        {
            SkillAreaMark.Show(lands, aim, skill.Attack.Range, skill.Attack.Arc, colour, 10f);
        }
    }

    // A lunge or a leap goes as far as the player aims, never past its own Lunge Distance (the user, 2026-11-25: "shorter jumps and
    // lunges if the player wants it, but not longer than maximum"): to the enemy in focus, stopping short of it, else to the cursor's
    // point. A stick has no point on the ground: the whole distance.
    private const float ShortestLunge = 1f;
    private const float FocusStandOff = 1.2f;
    private PlayerFocusTarget focusTarget;

    private float LungeReach(SkillDefinition skill)
    {
        float most = skill.LungeDistance;
        float wanted = movement != null ? movement.AimReach : most;
        if (focusTarget == null)
        {
            focusTarget = GetComponent<PlayerFocusTarget>();
        }

        if (focusTarget != null && focusTarget.HasFocus && focusTarget.Target != null)
        {
            Vector3 to = focusTarget.Target.transform.position - transform.position;
            to.y = 0f;
            wanted = to.magnitude - FocusStandOff;
        }

        return LungeReach(most, wanted);
    }

    /// <summary>How far a lunge of <paramref name="most"/> metres goes when the player aims <paramref name="wanted"/> metres away: as far
    /// as aimed, at least a step, never past its own distance (infinity, no point aimed at: all of it).</summary>
    public static float LungeReach(float most, float wanted) => Mathf.Clamp(wanted, Mathf.Min(ShortestLunge, most), most);

    private void EnsureInitialised()
    {
        if (initialised)
        {
            return;
        }

        initialised = true;
        if (settings == null)
        {
            Debug.LogWarning("PlayerSkills has no settings; the player starts with no skills.", this);
            return;
        }

        foreach (SkillDefinition skill in settings.StartingSkills)
        {
            AddKnown(skill);
        }

        for (int i = 0; i < settings.StartingLoadout.Count && i < SkillLoadout.SlotCount; i++)
        {
            SkillDefinition skill = settings.StartingLoadout[i];
            if (skill != null)
            {
                AddKnown(skill);
                loadout.Equip(i, skill);
            }
        }
    }

    private bool AddKnown(SkillDefinition skill)
    {
        if (skill == null || known.Contains(skill))
        {
            return false;
        }

        known.Add(skill);
        return true;
    }
}
