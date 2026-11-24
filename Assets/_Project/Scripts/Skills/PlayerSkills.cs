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
    private SkillDefinition lastCast;
    private StatusEffectDefinition readiedStatus;
    private float readiedUntil;
    private float readiedLanded = -1f;

    /// <summary>The status the next swing will lay, readied by a dodge after an ability; null when none (ArtBladeView glows in its colour).</summary>
    public StatusEffectDefinition ReadiedStatus => readiedStatus != null && Time.time < readiedUntil ? readiedStatus : null;

    // What an ability lays: its blow's status, or what a war cry lays on those who hear it.
    private static StatusEffectDefinition SchoolStatusOf(SkillDefinition skill) =>
        skill.Attack != null && skill.Attack.AppliesStatus != null ? skill.Attack.AppliesStatus : skill.FoesStatus;

    // A skill pressed while busy, cast when it can be within this long (Docs/SkillsImprovementResearch_2026-11-24.md 2).
    private const float BufferSeconds = 0.25f;
    private int bufferedSlot = -1;
    private float bufferedUntil;

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
        }
    }

    private ItemInventory bagItems;

    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        // The readied blow: the first swing after a dodge lays the readied status on all it strikes.
        if (!collectingStruck && readiedStatus != null && victim != null && damage.Source == combatant && result.Damage > 0f && !result.Blocked
            && (damage.Technique & HitTechnique.Melee) != 0 && Time.time < readiedUntil)
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
        }
    }

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
        }

        pendingHit = null;
        pendingCry = null;
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

        if (aimingSlot >= 0)
        {
            ShowAim();
        }

        if (bufferedSlot >= 0)
        {
            int slot = bufferedSlot;
            if (Time.time > bufferedUntil || !health.IsAlive)
            {
                bufferedSlot = -1;
            }
            else if (CheckCast(loadout.Get(slot)) != SkillCastResult.Busy)
            {
                bufferedSlot = -1;
                TryCast(slot);
            }
        }

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
            float weaponDamage = combat != null && combat.Weapon != null ? combat.Weapon.Damage : 0f;
            // The Runecraft skill scales every spell's damage; its hits carry the Spell technique, which trains it. A physical skill
            // (Steel, Beast) is a heavy melee blow instead: Melee trains, and a frozen foe shatters under it.
            float runecraft = skill.IsPhysical ? 1f : Mathf.Max(0f, 1f + combatant.Stats.Get(StatType.SpellDamage));
            HitTechnique kind = skill.IsPhysical ? HitTechnique.Melee | HitTechnique.Heavy : HitTechnique.Spell;
            // What it strikes, for the effects that land on each creature hit (SkillLook.struck).
            struck.Clear();
            struckFoes.Clear();
            collectingStruck = true;
            try
            {
                AttackResolver.Execute(combatant, skill.Attack, weaponDamage, transform.position, pendingDirection, hitLayers,
                    Power(skill) * runecraft * SkillChoices.For(skill).Damage, 1f, kind);
            }
            finally
            {
                collectingStruck = false;
            }

            SkillFx.Release(skill, transform.position, pendingDirection, struck);
            AfterOptions(skill, struckFoes);
            LeaveGround(skill, pendingDirection);
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
    public bool Learn(SkillDefinition skill, bool announce = true)
    {
        EnsureInitialised();
        if (!AddKnown(skill))
        {
            return false;
        }

        if (announce)
        {
            GameMessages.Post("New skill: " + skill.DisplayName, MessageKind.Gain);
            // Seen at once: into the first free slot of 1-5 (the user, 2026-11-24: "the skills should be visible"); moved anywhere after.
            for (int slot = 0; slot < 5; slot++)
            {
                if (loadout.Get(slot) == null && loadout.GetItem(slot) == null && loadout.IndexOf(skill) < 0)
                {
                    loadout.Equip(slot, skill);
                    break;
                }
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

        loadout.SetAll(allowed);
        loadout.SetItems(slotItems);
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

        PlayerFocus focus = GetComponent<PlayerFocus>();
        float focusCost = skill.FocusCost * SkillChoices.For(skill).Cost;
        if (result == SkillCastResult.Cast && focusCost > 0f && focus != null && !focus.CanSpend(focusCost))
        {
            focus.TrySpend(focusCost); // refused: lets the HUD say so
            result = SkillCastResult.NoFocus;
        }

        if (result == SkillCastResult.Cast && skill.StaminaCost > 0f && stamina != null && !stamina.TrySpend(SpellCost(skill)))
        {
            result = SkillCastResult.NoStamina;
        }

        if (result == SkillCastResult.Cast && focusCost > 0f && focus != null)
        {
            focus.TrySpend(focusCost);
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

        // Like the weapon swing, the direction is locked in toward the cursor as the cast begins.
        Vector3 direction = movement.AimDirection;
        // How it looks as it is cast, and its area on the ground until it lands (a skill without a blow still shows its cast).
        float windUp = skill.Attack != null ? (skill.LungeDistance > 0f ? skill.LungeDuration : 0f) + skill.Attack.HitDelay : 0f;
        SkillFx.Cast(skill, transform, direction, windUp);
        if (skill.Attack == null)
        {
            SkillFx.Release(skill, transform.position, direction, null);
        }

        if (skill.FoesStatus != null)
        {
            pendingCry = skill;
            pendingCryAt = now + skill.FoesDelay;
        }

        float hitAfter = 0f;
        if (skill.LungeDistance > 0f)
        {
            movement.Dash(direction * (skill.LungeDistance / skill.LungeDuration), skill.LungeDuration);
            hitAfter = skill.LungeDuration;
        }

        if (skill.Attack != null)
        {
            pendingHit = skill;
            pendingHitAt = now + hitAfter + skill.Attack.HitDelay;
            busyUntil = Mathf.Max(busyUntil, pendingHitAt);
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
        lastCast = skill;
        float optionHeal = SkillChoices.For(skill).Heal;
        if (optionHeal > 0f)
        {
            health.Heal(optionHeal);
        }

        if (skill.Heal > 0f)
        {
            health.Heal(skill.Heal * Power(skill));
        }

        if (skill.HasBuffs)
        {
            ApplyBuffs(skill, now);
        }
    }

    /// <summary>What deepening a skill at the altar multiplies its damage, healing and buffs by.</summary>
    public static float Power(SkillDefinition skill) => 1f + EssenceSettings.Current.PowerAt(GameSession.Progress.RankOf(skill));

    /// <summary>The skill's cooldown at its rank.</summary>
    // A spell's stamina after the Runecraft skill: a beginner's words cost more breath.
    private float SpellCost(SkillDefinition skill) =>
        SkillChoices.For(skill).Cost * (skill.IsPhysical || combatant == null ? skill.StaminaCost
            : skill.StaminaCost * Mathf.Max(0.1f, 1f + combatant.Stats.Get(StatType.SpellStaminaCost)));

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

    // The chosen altar options on what the skill struck (Docs/SkillsImprovementResearch_2026-11-24.md 4): a status laid, or one used
    // up for a payoff (health back, a burst of extra damage, another status in its place).
    private void AfterOptions(SkillDefinition skill, List<(Damageable foe, float took)> foes)
    {
        SkillChoices.Totals options = SkillChoices.For(skill);
        if (options.Chosen.Count == 0 || foes.Count == 0)
        {
            return;
        }

        foreach ((Damageable foe, float took) in foes)
        {
            if (foe == null || !foe.IsAlive)
            {
                continue;
            }

            foreach (SkillOption option in options.Chosen)
            {
                if (option.consumes != null && foe.TryGetComponent(out StatusEffects effects) && effects.Has(option.consumes))
                {
                    effects.End(option.consumes);
                    if (option.consumeHeal > 0f)
                    {
                        health.Heal(option.consumeHeal);
                    }

                    if (option.consumeDamage > 0f && foe.IsAlive)
                    {
                        var burst = new DamageInfo(combatant, null, took * option.consumeDamage, skill.Attack.DamageType, false,
                            foe.transform.position + Vector3.up, pendingDirection, 0f, HitTechnique.None);
                        combatant.ReportDamageDealt(foe, burst, foe.ApplyDamage(burst));
                    }

                    if (option.consumeInto != null && foe.IsAlive)
                    {
                        StatusEffects.Apply(foe, option.consumeInto, combatant);
                    }

                    GameMessages.Post(option.displayName + "!", MessageKind.Gain);
                }

                if (option.addsStatus != null && foe.IsAlive)
                {
                    StatusEffects.Apply(foe, option.addsStatus, combatant);
                }
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

    // A dodge is the way out of trouble: like a weapon swing, a skill hit that hadn't landed yet is lost.
    private void OnDodged(Vector3 direction)
    {
        // A dodge readies the next swing with the status of the ability cast last (V Rising's veils).
        StatusEffectDefinition school = lastCast != null ? SchoolStatusOf(lastCast) : null;
        if (school != null)
        {
            readiedStatus = school;
            readiedUntil = Time.time + ReadiedSeconds;
            readiedLanded = -1f;
        }

        pendingCry = null; // a cry cut short never rings out
        if (pendingHit != null)
        {
            SkillAreaMark.Hide();
        }

        pendingHit = null;
    }

    /// <summary>A blow that rocks the player (and a death) loses a skill hit not yet landed, as a weapon swing's is.</summary>
    public PlayerInterrupt EndsOn => PlayerInterrupt.Staggered;

    public void Interrupt(PlayerInterrupt reason)
    {
        pendingCry = null; // a cry cut short never rings out
        bufferedSlot = -1;
        if (pendingHit != null)
        {
            SkillAreaMark.Hide();
        }

        pendingHit = null;
    }

    private void OnSkillRequested(int slot)
    {
        TryCast(slot);
    }

    // Aim Skills First (GameOptions.AimSkillsFirst): the held skill's area follows the aim until the key is let go, then it casts.
    private int aimingSlot = -1;

    private void OnSkillAimed(int slot)
    {
        SkillDefinition skill = loadout.Get(slot);
        if (skill == null || skill.Attack == null || skill.WeaponArt != WeaponArtSlot.None)
        {
            TryCast(slot); // nothing to aim (a blessing, a war cry, an item, the weapon's art): as before
            return;
        }

        aimingSlot = slot;
    }

    private void OnSkillReleased(int slot)
    {
        if (aimingSlot != slot)
        {
            return;
        }

        aimingSlot = -1;
        SkillAreaMark.Hide();
        TryCast(slot);
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
        Vector3 lands = transform.position + (flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero) * skill.LungeDistance;
        SkillAreaMark.Show(lands, aim, skill.Attack.Range, skill.Attack.Arc, SkillSchools.ColorOf(skill.School), 10f);
    }

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
