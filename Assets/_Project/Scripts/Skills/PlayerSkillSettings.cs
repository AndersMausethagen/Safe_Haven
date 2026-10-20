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

    public IReadOnlyList<SkillDefinition> StartingSkills => startingSkills;
    public IReadOnlyList<SkillDefinition> StartingLoadout => startingLoadout;

    private void OnValidate()
    {
        if (startingLoadout == null || startingLoadout.Length != SkillLoadout.SlotCount)
        {
            System.Array.Resize(ref startingLoadout, SkillLoadout.SlotCount);
        }
    }
}
