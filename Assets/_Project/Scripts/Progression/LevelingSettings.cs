using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Levels, experience and talents: how much experience each level takes, what gives experience, how many talent points
/// a level brings and every talent in the four trees. Lives at Resources/LevelingSettings; read by CharacterProgress,
/// ExperienceTracker, PlayerTalents and the talent window. Built-in defaults apply when the asset is missing (then with
/// no talents).
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Leveling Settings", fileName = "LevelingSettings")]
public sealed class LevelingSettings : GameSettings<LevelingSettings>
{
    [Header("Levels")]
    [SerializeField, Min(2)] private int maxLevel = 30;
    [Tooltip("Experience from level 1 to level 2.")]
    [SerializeField, Min(1)] private int firstLevelExperience = 100;
    [Tooltip("Each level takes this many times the experience of the one before.")]
    [SerializeField, Range(1f, 2f)] private float levelGrowth = 1.18f;
    [SerializeField, Min(1)] private int talentPointsPerLevel = 1;
    [SerializeField] private SoundDefinition levelUpSound;

    [Header("Experience")]
    [Tooltip("A monster killed by the player gives its Experience, or this much per point of its health when it has none.")]
    [SerializeField, Min(0f)] private float killExperiencePerHealth = 0.25f;
    [SerializeField, Min(0)] private int minimumKillExperience = 3;
    [Tooltip("For a completed quest without its own Experience.")]
    [SerializeField, Min(0)] private int questExperience = 50;
    [SerializeField, Min(0)] private int raidExtractedExperience = 150;
    [Tooltip("A raid that went wrong still teaches something.")]
    [SerializeField, Min(0)] private int raidFailedExperience = 40;
    [Tooltip("The first time each item is crafted.")]
    [SerializeField, Min(0)] private int firstCraftExperience = 10;
    [Tooltip("A building hammered up (or a ruin repaired) at home.")]
    [SerializeField, Min(0)] private int buildExperience = 15;
    [Tooltip("A building upgraded at home.")]
    [SerializeField, Min(0)] private int upgradeExperience = 25;
    [Tooltip("Every node gathered (a bush picked, a rock mined, a tree felled).")]
    [SerializeField, Min(0)] private int gatherExperience = 2;
    [Tooltip("1 experience for this many square metres of new ground explored.")]
    [SerializeField, Min(1)] private int exploredCellsPerExperience = 150;

    [Header("Talents")]
    [Tooltip("Points spent in a tree to open each next row of it.")]
    [SerializeField, Min(1)] private int pointsPerRow = 3;
    [Tooltip("Every talent of the four trees.")]
    [SerializeField] private List<TalentDefinition> talents = new List<TalentDefinition>();

    public int MaxLevel => maxLevel;
    public int TalentPointsPerLevel => talentPointsPerLevel;
    public SoundDefinition LevelUpSound => levelUpSound;
    public float KillExperiencePerHealth => killExperiencePerHealth;
    public int MinimumKillExperience => minimumKillExperience;
    public int QuestExperience => questExperience;
    public int RaidExtractedExperience => raidExtractedExperience;
    public int RaidFailedExperience => raidFailedExperience;
    public int FirstCraftExperience => firstCraftExperience;
    public int GatherExperience => gatherExperience;
    public int BuildExperience => buildExperience;
    public int UpgradeExperience => upgradeExperience;
    public int ExploredCellsPerExperience => exploredCellsPerExperience;
    public int PointsPerRow => pointsPerRow;
    public IReadOnlyList<TalentDefinition> Talents => talents;

    /// <summary>Experience needed to go from <paramref name="level"/> to the next one (rounded to 5).</summary>
    public int ExperienceToNext(int level)
    {
        float needed = firstLevelExperience * Mathf.Pow(levelGrowth, Mathf.Max(0, level - 1));
        return Mathf.Max(1, Mathf.RoundToInt(needed / 5f) * 5);
    }

    public TalentDefinition FindTalent(string id)
    {
        foreach (TalentDefinition talent in talents)
        {
            if (talent != null && talent.Id == id)
            {
                return talent;
            }
        }

        return null;
    }

    /// <summary>Sets the levels up (tests and editor tools).</summary>
    public void Configure(int levels, int firstLevel, float growth, int pointsPerLevel, int rowPoints, IEnumerable<TalentDefinition> allTalents)
    {
        maxLevel = Mathf.Max(2, levels);
        firstLevelExperience = Mathf.Max(1, firstLevel);
        levelGrowth = Mathf.Clamp(growth, 1f, 2f);
        talentPointsPerLevel = Mathf.Max(1, pointsPerLevel);
        pointsPerRow = Mathf.Max(1, rowPoints);
        talents = new List<TalentDefinition>(allTalents);
    }
}
