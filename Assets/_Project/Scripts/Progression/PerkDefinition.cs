using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// One perk: a passive bonus chosen at a skill's milestone (ProficiencyDefinition › Milestones), one of two on offer.
/// Its bonuses become stat modifiers on the player while it is chosen (PlayerProficiencies). Perks can be chosen again
/// for free beside the portal. Once the talents of the four trees; the script keeps their GUID.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Progression/Perk", fileName = "Perk")]
public sealed class PerkDefinition : ScriptableObject
{
    [Tooltip("Saved in the player's progress. Never change it once players have it.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private Sprite icon;
    [Tooltip("Added to the player's stats while the perk is chosen.")]
    [FormerlySerializedAs("perRank")]
    [SerializeField] private List<StatBonus> bonuses = new List<StatBonus>();

    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public IReadOnlyList<StatBonus> Bonuses => bonuses;

    /// <summary>Sets the perk up (editor tools and tests).</summary>
    public void Configure(string perkId, string label, string text, params StatBonus[] perkBonuses)
    {
        id = perkId;
        displayName = label;
        description = text;
        bonuses = new List<StatBonus>(perkBonuses);
    }

    /// <summary>"+5% Movement Speed, +10 Health".</summary>
    public string DescribeBonuses()
    {
        var parts = new List<string>();
        foreach (StatBonus bonus in bonuses)
        {
            parts.Add(bonus.Describe());
        }

        return string.Join(", ", parts);
    }
}
