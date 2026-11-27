using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>One stat change a skill grants for a while (see SkillDefinition.Buffs).</summary>
[Serializable]
public struct SkillStatBuff
{
    public StatType stat;
    public StatModifierOp op;
    public float value;
}

/// <summary>
/// One skill the player can learn and put on the skill bar, V Rising style: a cooldown, a stamina cost and what it does
/// when cast. What it does is composed from optional parts, applied in this order: a lunge along the aim direction, a
/// hit (an AttackDefinition handed to AttackResolver, so skills share the weapon damage pipeline), a heal and timed stat
/// buffs. Authored data only; cooldowns and the loadout are runtime state kept by PlayerSkills.
/// </summary>
[CreateAssetMenu(fileName = "Skill", menuName = "Safe Haven/Combat/Skill")]
public sealed class SkillDefinition : ScriptableObject
{
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 5)] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private SkillSchool school;
    [Tooltip("What it is for, and so which bar slot takes it (SkillLoadout): a Technique (no Focus, on its cooldown), a Power (Focus), " +
             "an Ultimate (a full Focus bar). A weapon art's entry is always an Art, whatever this says.")]
    [SerializeField] private SkillKind kind = SkillKind.Power;

    [Header("Cost")]
    [Tooltip("Seconds before the skill can be cast again.")]
    [SerializeField, Min(0f)] private float cooldown = 8f;
    [Tooltip("Stamina paid when cast. An empty stamina pool refuses the skill. 0 for every ability since 2026-11-25: Focus is their " +
             "price, and stamina is kept for the dodge (SkillPayoffTests).")]
    [SerializeField, Min(0f)] private float staminaCost;
    [Tooltip("Full Focus circles it needs and spends. Focus is earned by fighting (hits, parries, perfect dodges, blocks, blows taken; " +
             "faster in a crowd) and kept in circles above the skill bar. By kind since 2026-11-27 (SkillPriceTests): a Technique 0, a Power " +
             "1 (2 for the strongest), an Ultimate 3. 0 also for a weapon art's entry, which the art pays for.")]
    [SerializeField, Range(0, 3)] private int focusCircles = 1;
    [Tooltip("Seconds after casting before another skill can be cast.")]
    [SerializeField, Min(0f)] private float castTime = 0.45f;

    [Header("Lunge")]
    [Tooltip("Metres travelled toward the cursor before the hit. 0 stays in place.")]
    [SerializeField, Min(0f)] private float lungeDistance;
    [SerializeField, Min(0.05f)] private float lungeDuration = 0.2f;

    [Header("Held (Docs/SkillsImprovementResearch_2026-11-24.md 10: a channel)")]
    [Tooltip("Above 0: a channel. It strikes again every Channel Tick while its key is held, for at most this long; letting go, a dodge or " +
             "a stagger ends it. Its first strike lands at the attack's Hit Delay. 0: one blow, as most skills.")]
    [SerializeField, Min(0f)] private float channelSeconds;
    [SerializeField, Min(0.1f)] private float channelTick = 0.3f;

    [Header("Around the caster (a war cry)")]
    [Tooltip("Laid on every foe within Foes Radius when it rings out (Cowed: they deal less). Empty: none.")]
    [SerializeField] private StatusEffectDefinition foesStatus;
    [SerializeField, Min(0f)] private float foesRadius = 7f;
    [Tooltip("Seconds after the cast that it rings out: the roar's loudest moment in its animation.")]
    [SerializeField, Min(0f)] private float foesDelay = 0.5f;

    [Header("Weapon (Docs/SkillChoiceResearch_2026-11-27.md 3.3)")]
    [Tooltip("The kinds of weapon it belongs to, by their signature (Counter Chain: swords, Assassin: daggers, Guard Breaker: one-handed " +
             "axes and clubs, Momentum: greataxes, Hyper Armour: greatswords, Crush: hammers, Anti Charge: spears, Flurry: fists, Channel: " +
             "staves, Hunter's Mark: bows). Empty: no weapon in particular.")]
    [SerializeField] private List<WeaponSignature> weapons = new List<WeaponSignature>();
    [Tooltip("On: a weapon's own technique, cast only with one of those in hand, and learned by wielding one of Learn At Tier or better " +
             "(PlayerSkillSettings). Off: any weapon will do, and one of those strikes Favoured Damage harder.")]
    [SerializeField] private bool needsWeapon;
    [SerializeField, Range(1f, 1.5f)] private float favouredDamage = 1.25f;

    [Header("Set up and pay off (Docs/SkillChoiceResearch_2026-11-27.md 3.4)")]
    [Tooltip("What it does to foes bearing a status, built in (as the altar's options do, and on top of them): one skill lays a status " +
             "(its attack's Applies Status), another uses it up for a payoff (Consumes: a burst of Consume Damage times the blow, health " +
             "back, another status). Uses Consumes, Consume Damage, Consume Heal, Consume Into and Adds Status, and Display Name for the " +
             "call (\"Open the Vein!\").")]
    [SerializeField] private List<SkillOption> combos = new List<SkillOption>();

    [Header("Choices at the altar (Docs/SkillsImprovementResearch_2026-11-24.md 3)")]
    [Tooltip("What the altar offers it, one choice per rank: rank I small tweaks, rank II ones that change how it plays, rank III two that redefine it.")]
    [SerializeField] private List<SkillOption> options = new List<SkillOption>();

    [Header("Left behind (Docs/CombatSkillsResearch_2026-11-24.md 6: lingering)")]
    [Tooltip("A patch of ground (a GroundHazard prefab) left along its reach where it lands: smouldering ground, a rime patch. It hurts " +
             "only the caster's foes. Empty: nothing is left.")]
    [SerializeField] private GameObject leaves;
    [SerializeField, Range(1, 8)] private int leavesCount = 3;

    [Header("Hit")]
    [Tooltip("Optional. The damage dealt, its area and when it lands (Hit Delay, counted after the lunge).")]
    [SerializeField] private AttackDefinition attack;

    [Header("Support")]
    [Tooltip("Health restored when cast.")]
    [SerializeField, Min(0f)] private float heal;
    [Tooltip("What casting it grants the caster for Buff Duration: each ability its own (the user, 2026-11-25: \"a secondary beneficial " +
             "effect for a short duration that's unique to the skill\").")]
    [SerializeField] private List<SkillStatBuff> buffs = new List<SkillStatBuff>();
    [Tooltip("Seconds the buffs last.")]
    [SerializeField, Min(0f)] private float buffDuration = 6f;
    [Tooltip("The buff's own name, on the card and beside the health bar while it lasts (Steel Wall). Empty: the skill's name.")]
    [SerializeField] private string buffName;
    [Tooltip("While the buff lasts, blows taken fill Focus this many times faster (Bear-Hide: built around taking hits). 1: as ever.")]
    [SerializeField, Range(1f, 4f)] private float focusFromBlows = 1f;

    [Header("Presentation")]
    [SerializeField] private bool playAttackAnimation = true;
    [Tooltip("The player's animator state the skill plays with the whole body (a cast, a dash, a spin), by name. Empty: the weapon " +
             "stance's old attack, as before.")]
    [SerializeField] private string animationState;
    [Tooltip("Playback speed of that state.")]
    [SerializeField, Range(0.25f, 3f)] private float animationSpeed = 1f;
    [Tooltip("How it looks: effects as it is cast, as it is let go (over the attack's reach and arc) and on what it strikes; its area on " +
             "the ground while it winds up; a flash and a shake (Docs/CombatSkillsResearch_2026-11-24.md §6).")]
    [SerializeField] private SkillLook look = new SkillLook();
    [Tooltip("A bar entry for the weapon in hand's art instead of a skill of its own (the user, 2026-11-24: weapon skills stay on the 1-5 " +
             "bar and can be placed anywhere): Own strikes the weapon's art (the middle button's), Rune the attuned rune's (attacking out of " +
             "the guard). Its cost and cooldown are the art's; the fields above are not used.")]
    [SerializeField] private WeaponArtSlot weaponArt;

    /// <summary>Stable identifier, used for saves: the asset's name.</summary>
    public string Id => name;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public SkillSchool School => school;
    /// <summary>What it is for, and which bar slot takes it: a weapon art's entry is an Art.</summary>
    public SkillKind Kind => weaponArt != WeaponArtSlot.None ? SkillKind.Art : kind;
    public float Cooldown => cooldown;
    public float StaminaCost => staminaCost;
    public int FocusCircles => focusCircles;
    public float CastTime => castTime;
    public float LungeDistance => lungeDistance;
    public float LungeDuration => lungeDuration;

    /// <summary>Steel and the beasts' skills are blows, not spells (Docs/SkillsImprovementResearch_2026-11-24.md 1): they land as heavy
    /// melee blows (Melee trains, a frozen foe shatters), scale with the weapon side, and Runecraft has no say in them.</summary>
    public bool IsPhysical => school == SkillSchool.Steel || school == SkillSchool.Beast;
    public GameObject Leaves => leaves;
    public float ChannelSeconds => channelSeconds;
    public float ChannelTick => channelTick;
    /// <summary>A thrown skill (its attack has a Cast Projectile: a boulder, a spear): it flies to what it hits, not a blow round the caster.</summary>
    public bool Throws => attack != null && attack.CastProjectile != null;
    public IReadOnlyList<SkillOption> Options => options;
    /// <summary>The kinds of weapon it belongs to, by signature; empty for none in particular.</summary>
    public IReadOnlyList<WeaponSignature> Weapons => weapons;
    /// <summary>A weapon's own technique: cast only with one of <see cref="Weapons"/> in hand.</summary>
    public bool NeedsWeapon => needsWeapon && weapons.Count > 0;
    /// <summary>Its built-in set-ups and payoffs on what it strikes.</summary>
    public IReadOnlyList<SkillOption> Combos => combos;

    /// <summary>True when it can be cast with <paramref name="weapon"/> in hand (null: none).</summary>
    public bool CastsWith(WeaponDefinition weapon) => !NeedsWeapon || weapon != null && weapons.Contains(weapon.Signature);

    /// <summary>How much harder it strikes with <paramref name="weapon"/>: Favoured Damage with one it belongs to, 1 otherwise.</summary>
    public float WeaponScale(WeaponDefinition weapon) =>
        !needsWeapon && weapons.Count > 0 && weapon != null && weapons.Contains(weapon.Signature) ? favouredDamage : 1f;
    public StatusEffectDefinition FoesStatus => foesStatus;
    public float FoesRadius => foesRadius;
    public float FoesDelay => foesDelay;
    public int LeavesCount => leavesCount;
    public AttackDefinition Attack => attack;
    public float Heal => heal;
    public IReadOnlyList<SkillStatBuff> Buffs => buffs;
    public float BuffDuration => buffDuration;
    public string BuffName => string.IsNullOrWhiteSpace(buffName) ? DisplayName : buffName;
    /// <summary>How much faster blows taken fill Focus while its buff lasts.</summary>
    public float FocusFromBlows => focusFromBlows;
    public bool PlayAttackAnimation => playAttackAnimation;
    public string AnimationState => animationState;
    public float AnimationSpeed => animationSpeed;
    public SkillLook Look => look;
    public WeaponArtSlot WeaponArt => weaponArt;
    public bool HasBuffs => buffs.Count > 0 && buffDuration > 0f;

    /// <summary>
    /// Held to aim, cast on release (GameOptions.AimSkillsFirst): anything with a blow that goes somewhere, a cone, a lunge, a leap or a
    /// throw. Not a ring round the caster that goes nowhere (Whirling Steel, Trollsbane's Wrath), nothing without a blow (a blessing, a war
    /// cry), not the weapon's art, and not a channel, which is held to keep going and follows the aim as it strikes.
    /// </summary>
    public bool IsAimed => attack != null && weaponArt == WeaponArtSlot.None && channelSeconds <= 0f
        && (attack.Arc < 360f || lungeDistance > 0f || Throws);
}
