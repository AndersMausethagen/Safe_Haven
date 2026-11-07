using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where a skill sits in the skills window (N).</summary>
public enum ProficiencyGroup
{
    Gathering = 0,
    Crafting = 1,
    Combat = 2,
    Stealth = 3,
}

/// <summary>What the player does to train a skill. ExperienceTracker hands each kind of practice to the skill trained by it.</summary>
public enum ProficiencyTraining
{
    /// <summary>Nodes gathered with the skill's Gather Tool.</summary>
    Gathering = 0,
    /// <summary>Weapon swings that hurt monsters, parries and perfect dodges.</summary>
    Melee = 1,
    /// <summary>Ambushes, slipping past monsters unseen, monsters that lose you, silent lock pins.</summary>
    Sneaking = 2,
    Archery = 3,
    Spells = 4,
    Fishing = 5,
    Construction = 6,
    Crafting = 7,
}

/// <summary>
/// One skill the player gets better at by doing it (woodcutting, mining, melee, sneaking...), from level 1 to
/// LevelingSettings › Max Level. Called a proficiency in code because SkillDefinition is a spell; the game calls them
/// skills. Two kinds of reward, both read by PlayerProficiencies:
/// <list type="bullet">
/// <item>Level Bonuses grow a little with every level. Each is 0 at LevelingSettings › Baseline Level, the player the
/// game was tuned for, At First Level at level 1 (a beginner: usually worse) and At Last Level at the top.</item>
/// <item>Milestones offer a choice of two perks at set levels; the player keeps one, and can choose again at home.</item>
/// </list>
/// Experience comes from ExperienceTracker: a gathering skill from nodes gathered with its Gather Tool, the others from
/// what their systems report. The asset only says what the skill gives; how fast it grows is LevelingSettings'.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Progression/Skill", fileName = "Skill")]
public sealed class ProficiencyDefinition : ScriptableObject
{
    /// <summary>A stat that grows with the skill's level.</summary>
    [Serializable]
    public struct LevelBonus
    {
        public StatType stat;
        [Tooltip("The bonus at level 1. Usually a penalty: a beginner swings slower, louder or more tiringly.")]
        public float atFirstLevel;
        [Tooltip("The bonus at the highest level. In between it grows evenly from 0 at the baseline level.")]
        public float atLastLevel;

        public LevelBonus(StatType stat, float atFirstLevel, float atLastLevel)
        {
            this.stat = stat;
            this.atFirstLevel = atFirstLevel;
            this.atLastLevel = atLastLevel;
        }
    }

    /// <summary>A level at which the player picks one of two perks.</summary>
    [Serializable]
    public struct Milestone
    {
        [Min(1)] public int level;
        public PerkDefinition first;
        public PerkDefinition second;

        public Milestone(int level, PerkDefinition first, PerkDefinition second)
        {
            this.level = level;
            this.first = first;
            this.second = second;
        }

        public bool Offers(PerkDefinition perk) => perk != null && (perk == first || perk == second);

        public PerkDefinition Other(PerkDefinition perk) => perk == first ? second : perk == second ? first : null;
    }

    [Tooltip("Saved in the player's progress. Never change it once players have it.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 4)] private string description;
    [Tooltip("What trains it, for the skills window: \"Felling and splitting trees\".")]
    [SerializeField] private string trainedBy;
    [SerializeField] private Sprite icon;
    [SerializeField] private ProficiencyGroup group;
    [Tooltip("What trains it.")]
    [SerializeField] private ProficiencyTraining training;
    [Tooltip("A crafting skill: making these goods trains it (by hand or ordered at a station; never workers' orders).")]
    [SerializeField] private CraftedGoods crafts;
    [Tooltip("A gathering skill: nodes gathered with this tool train it. None for the rest.")]
    [SerializeField] private ToolType gatherTool;
    [Tooltip("A gathering skill that nodes picked by hand (no tool) train as well.")]
    [SerializeField] private bool gathersByHand;
    [Tooltip("The Haven building that teaches it: each of its tiers above I adds LevelingSettings › Mentor Bonus Per Tier to " +
             "the experience. Empty for none.")]
    [SerializeField] private BuildingDefinition mentor;
    [SerializeField] private List<LevelBonus> levelBonuses = new List<LevelBonus>();
    [Tooltip("In order of level. Each offers two perks; the player keeps one.")]
    [SerializeField] private List<Milestone> milestones = new List<Milestone>();

    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public string TrainedBy => trainedBy;
    public Sprite Icon => icon;
    public ProficiencyGroup Group => group;
    public ProficiencyTraining Training => training;
    public CraftedGoods Crafts => crafts;
    public ToolType GatherTool => gatherTool;
    public bool GathersByHand => gathersByHand;
    public BuildingDefinition Mentor => mentor;
    public IReadOnlyList<LevelBonus> LevelBonuses => levelBonuses;
    public IReadOnlyList<Milestone> Milestones => milestones;

    /// <summary>A level bonus's value at <paramref name="level"/>: At First Level at 1, 0 at the baseline, At Last Level
    /// at the top, and evenly in between.</summary>
    public static float ValueAt(LevelBonus bonus, int level, LevelingSettings settings)
    {
        float t = settings.CurvePosition(level);
        return t < 0f ? -t * bonus.atFirstLevel : t * bonus.atLastLevel;
    }

    /// <summary>The milestone that offers this perk, if any.</summary>
    public bool TryFindMilestone(PerkDefinition perk, out Milestone found)
    {
        foreach (Milestone milestone in milestones)
        {
            if (milestone.Offers(perk))
            {
                found = milestone;
                return true;
            }
        }

        found = default;
        return false;
    }

    /// <summary>Sets the skill up (editor tools and tests).</summary>
    public void Configure(string skillId, string label, string text, string trained, ProficiencyGroup inGroup,
        ProficiencyTraining trainedThrough, ToolType tool, IEnumerable<LevelBonus> bonuses, IEnumerable<Milestone> levels,
        CraftedGoods madeGoods = CraftedGoods.None, bool byHand = false)
    {
        crafts = madeGoods;
        gathersByHand = byHand;
        id = skillId;
        displayName = label;
        description = text;
        trainedBy = trained;
        group = inGroup;
        training = trainedThrough;
        gatherTool = tool;
        levelBonuses = new List<LevelBonus>(bonuses);
        milestones = new List<Milestone>(levels);
        milestones.Sort((a, b) => a.level.CompareTo(b.level));
    }
}
