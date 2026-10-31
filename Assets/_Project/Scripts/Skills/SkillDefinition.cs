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
    public AttackDefinition Attack => attack;
    public float Heal => heal;
    public IReadOnlyList<SkillStatBuff> Buffs => buffs;
    public float BuffDuration => buffDuration;
    public bool PlayAttackAnimation => playAttackAnimation;
    public bool HasBuffs => buffs.Count > 0 && buffDuration > 0f;
}
