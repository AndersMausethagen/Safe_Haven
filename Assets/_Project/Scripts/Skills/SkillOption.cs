using System;
using UnityEngine;

/// <summary>
/// One choice for an ability at the altar (Docs/SkillsImprovementResearch_2026-11-24.md 3, 4: Lost Ark's tripods, V Rising's jewels). Each
/// rank opens a set: rank I small tweaks, rank II ones that change how it plays, rank III two that redefine it. One per rank is chosen,
/// free to change at the altar. Authored on the SkillDefinition; which is chosen is the player's (CharacterProgress, saved). Every field
/// left at its default does nothing, so an option names only what it changes.
/// </summary>
[Serializable]
public sealed class SkillOption
{
    [Tooltip("Stable id, saved with the choice: never rename once shipped.")]
    public string id;
    public string displayName;
    [TextArea(1, 3)] public string description;
    [Tooltip("The altar rank it opens at (1-3); one option per rank is chosen.")]
    [Range(1, 3)] public int rank = 1;

    [Header("Numbers")]
    [Min(0.1f)] public float damageScale = 1f;
    [Min(0.1f)] public float cooldownScale = 1f;
    [Tooltip("Its stamina and Focus.")]
    [Min(0f)] public float costScale = 1f;
    [Tooltip("Its timed buffs' values (a war cry's fury).")]
    [Min(0f)] public float buffScale = 1f;
    [Tooltip("Health back to the caster as it is cast.")]
    [Min(0f)] public float heal;
    [Tooltip("More patches of ground left behind (Skill › Leaves).")]
    [Range(0, 4)] public int extraLeaves;
    [Tooltip("How far a war cry carries.")]
    [Min(0.5f)] public float foesRadiusScale = 1f;

    [Header("On the foes it reaches")]
    [Tooltip("Laid on every foe it strikes (or that hears a war cry).")]
    public StatusEffectDefinition addsStatus;
    [Tooltip("A status on a foe it strikes, used up for the payoffs below (V Rising's jewels: biting a bleeding foe heals you).")]
    public StatusEffectDefinition consumes;
    [Tooltip("Health back to the caster for each foe whose status it used up.")]
    [Min(0f)] public float consumeHeal;
    [Tooltip("Extra damage to that foe, as a share of what the skill dealt it.")]
    [Min(0f)] public float consumeDamage;
    [Tooltip("Laid on that foe in the used-up status's place.")]
    public StatusEffectDefinition consumeInto;
}
