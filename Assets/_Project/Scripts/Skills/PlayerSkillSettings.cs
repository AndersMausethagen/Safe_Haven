using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which skills a new player starts with and how their skill bar is arranged. Read-only at runtime; the live book and
/// loadout are kept by PlayerSkills.
/// </summary>
[CreateAssetMenu(fileName = "PlayerSkillSettings", menuName = "Safe Haven/Player Skill Settings")]
public sealed class PlayerSkillSettings : ScriptableObject
{
    [Tooltip("Skills known from the start. They appear in the skill window (K).")]
    [SerializeField] private List<SkillDefinition> startingSkills = new List<SkillDefinition>();
    [Tooltip("What sits in each of the five bar slots at the start. Skills here are learned automatically.")]
    [SerializeField] private SkillDefinition[] startingLoadout = new SkillDefinition[SkillLoadout.SlotCount];

    [Tooltip("Each kind of weapon's own technique (Needs Weapon on, its Weapons that kind): learned the first time the player wields one " +
             "of that kind at Learn At Tier or better, as V Rising's weapons teach their skills by tier (Docs/SkillChoiceResearch_2026-11-27.md).")]
    [SerializeField] private List<SkillDefinition> weaponTechniques = new List<SkillDefinition>();
    [Tooltip("The weapon's item tier that teaches its technique: 2 is copper.")]
    [SerializeField, Range(1, 5)] private int learnAtTier = 2;

    public IReadOnlyList<SkillDefinition> StartingSkills => startingSkills;
    public IReadOnlyList<SkillDefinition> StartingLoadout => startingLoadout;
    public IReadOnlyList<SkillDefinition> WeaponTechniques => weaponTechniques;
    public int LearnAtTier => learnAtTier;

    /// <summary>The technique a weapon of this kind teaches, or null.</summary>
    public SkillDefinition TechniqueOf(WeaponSignature signature)
    {
        foreach (SkillDefinition skill in weaponTechniques)
        {
            if (skill != null && skill.NeedsWeapon && System.Linq.Enumerable.Contains(skill.Weapons, signature))
            {
                return skill;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        if (startingLoadout == null || startingLoadout.Length != SkillLoadout.SlotCount)
        {
            System.Array.Resize(ref startingLoadout, SkillLoadout.SlotCount);
        }
    }
}
