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

    [Header("Cost")]
    [Tooltip("Seconds before the skill can be cast again.")]
    [SerializeField, Min(0f)] private float cooldown = 8f;
    [Tooltip("Stamina paid when cast. An empty stamina pool refuses the skill.")]
    [SerializeField, Min(0f)] private float staminaCost = 20f;
    [Tooltip("Focus it needs and spends. Focus is earned by fighting well (hits, parries, perfect dodges); 0 = none.")]
    [SerializeField, Min(0f)] private float focusCost;
    [Tooltip("Seconds after casting before another skill can be cast.")]
    [SerializeField, Min(0f)] private float castTime = 0.45f;

    [Header("Lunge")]
    [Tooltip("Metres travelled toward the cursor before the hit. 0 stays in place.")]
    [SerializeField, Min(0f)] private float lungeDistance;
    [SerializeField, Min(0.05f)] private float lungeDuration = 0.2f;

    [Header("Around the caster (a war cry)")]
    [Tooltip("Laid on every foe within Foes Radius when it rings out (Cowed: they deal less). Empty: none.")]
    [SerializeField] private StatusEffectDefinition foesStatus;
    [SerializeField, Min(0f)] private float foesRadius = 7f;
    [Tooltip("Seconds after the cast that it rings out: the roar's loudest moment in its animation.")]
    [SerializeField, Min(0f)] private float foesDelay = 0.5f;

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
    [SerializeField] private List<SkillStatBuff> buffs = new List<SkillStatBuff>();
    [Tooltip("Seconds the buffs last.")]
    [SerializeField, Min(0f)] private float buffDuration = 6f;

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
    public float Cooldown => cooldown;
    public float StaminaCost => staminaCost;
    public float FocusCost => focusCost;
    public float CastTime => castTime;
    public float LungeDistance => lungeDistance;
    public float LungeDuration => lungeDuration;
    public GameObject Leaves => leaves;
    public StatusEffectDefinition FoesStatus => foesStatus;
    public float FoesRadius => foesRadius;
    public float FoesDelay => foesDelay;
    public int LeavesCount => leavesCount;
    public AttackDefinition Attack => attack;
    public float Heal => heal;
    public IReadOnlyList<SkillStatBuff> Buffs => buffs;
    public float BuffDuration => buffDuration;
    public bool PlayAttackAnimation => playAttackAnimation;
    public string AnimationState => animationState;
    public float AnimationSpeed => animationSpeed;
    public SkillLook Look => look;
    public WeaponArtSlot WeaponArt => weaponArt;
    public bool HasBuffs => buffs.Count > 0 && buffDuration > 0f;
}
