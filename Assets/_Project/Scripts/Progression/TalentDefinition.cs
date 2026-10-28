using System.Collections.Generic;
using UnityEngine;

/// <summary>The four talent trees. Points can go in any of them: they are disciplines to mix, not locked classes.</summary>
public enum TalentTree
{
    /// <summary>Bows, speed, stealth and foraging.</summary>
    Ranger = 0,
    /// <summary>Health, armour and the weapon in hand.</summary>
    Soldier = 1,
    /// <summary>Spells, wards and resistances.</summary>
    Runemaster = 2,
    /// <summary>Tools, gathering and stamina for work.</summary>
    Tinkerer = 3,
}

/// <summary>
/// One talent: a small passive bonus bought with talent points (from levelling up), up to Max Rank times. It sits in a
/// tree, on a row: row 0 is open from the start, each later row once Points Per Row (LevelingSettings) have gone into
/// that tree. Each rank adds the Per Rank bonuses to the player's stats (PlayerTalents).
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Progression/Talent", fileName = "Talent")]
public sealed class TalentDefinition : ScriptableObject
{
    [Tooltip("Saved in the player's progress. Never change it once players have it.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private Sprite icon;
    [SerializeField] private TalentTree tree;
    [Tooltip("0 is open from the start; row N needs N × Points Per Row spent in this tree.")]
    [SerializeField, Range(0, 4)] private int row;
    [SerializeField, Range(1, 5)] private int maxRank = 3;
    [Tooltip("Added to the player's stats for every rank.")]
    [SerializeField] private List<StatBonus> perRank = new List<StatBonus>();

    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public TalentTree Tree => tree;
    public int Row => row;
    public int MaxRank => maxRank;
    public IReadOnlyList<StatBonus> PerRank => perRank;

    /// <summary>Sets the talent up (editor tools and tests).</summary>
    public void Configure(string talentId, string label, string text, TalentTree inTree, int onRow, int ranks, params StatBonus[] bonuses)
    {
        id = talentId;
        displayName = label;
        description = text;
        tree = inTree;
        row = onRow;
        maxRank = Mathf.Clamp(ranks, 1, 5);
        perRank = new List<StatBonus>(bonuses);
    }

    /// <summary>"+5% Movement Speed" for each bonus, at this many ranks.</summary>
    public string DescribeBonuses(int ranks)
    {
        var parts = new List<string>();
        foreach (StatBonus bonus in perRank)
        {
            parts.Add(new StatBonus(bonus.stat, bonus.value * ranks, bonus.op).Describe());
        }

        return string.Join(", ", parts);
    }
}
