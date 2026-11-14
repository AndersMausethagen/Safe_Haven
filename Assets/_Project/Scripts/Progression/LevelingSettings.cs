using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Skills and how they grow: the experience each level takes, the baseline level the game was tuned for (level bonuses
/// are 0 there), what an action is worth by its tier, the raid bonus, Insight (experience from quests, raids and
/// discoveries that the player puts into a skill of their choice) and every skill. Lives at Resources/LevelingSettings;
/// read by CharacterProgress, ExperienceTracker, PlayerProficiencies and the skills window. Built-in defaults apply when
/// the asset is missing (then with no skills). See Docs/Skills.md for how the numbers were chosen.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Leveling Settings", fileName = "LevelingSettings")]
public sealed class LevelingSettings : GameSettings<LevelingSettings>
{
    [Header("Levels")]
    [SerializeField, Min(2)] private int maxLevel = 50;
    [Tooltip("Experience from level 1 to level 2 of a skill.")]
    [SerializeField, Min(1)] private int firstLevelExperience = 50;
    [Tooltip("Each level takes this many times the experience of the one before.")]
    [SerializeField, Range(1f, 2f)] private float levelGrowth = 1.105f;
    [Tooltip("The first levels take this many times their experience (level 1 to 2), easing back to 1 over Early Levels: " +
             "at the start every skill levelled within minutes, level sounds and banners piling up with the quests'.")]
    [SerializeField, Range(1f, 3f)] private float earlySlowdown = 1.6f;
    [SerializeField, Min(1)] private int earlyLevels = 5;
    [Tooltip("The level the game's numbers were tuned for. Skill level bonuses are 0 here: below it the player is a " +
             "beginner (At First Level), above it better than the game assumed (At Last Level).")]
    [SerializeField, Min(1)] private int baselineLevel = 10;
    [Tooltip("How quickly a beginner's penalties fade: 1 evenly to the baseline, higher sheds most of them in the first few " +
             "levels (2.5: two thirds gone by level 4, three quarters by 5).")]
    [SerializeField, Range(1f, 4f)] private float beginnerEase = 2.5f;
    [SerializeField] private SoundDefinition levelUpSound;

    [Header("Experience per action")]
    [Tooltip("Experience for an action of tier I, II, III... (a node of that tier, or anything done in a raid of that tier).")]
    [SerializeField] private int[] tierExperience = { 10, 16, 25, 40, 60 };
    [Tooltip("Levels per tier band: with 10, levels 1-10 are at home with tier I, 11-20 with tier II...")]
    [SerializeField, Min(1)] private int levelsPerTier = 10;
    [Tooltip("Share of the experience for an action one tier below the skill's band.")]
    [SerializeField, Range(0f, 1f)] private float oneTierBelow = 0.6f;
    [Tooltip("Share for an action two or more tiers below: farming the easy trees stops paying.")]
    [SerializeField, Range(0f, 1f)] private float farBelow = 0.25f;
    [Tooltip("Everything learned on a raid counts this many times: danger teaches faster.")]
    [SerializeField, Min(1f)] private float raidMultiplier = 1.5f;
    [Tooltip("Share more experience for each tier of a skill's mentor building above I (a Smithy III: two tiers).")]
    [SerializeField, Range(0f, 0.5f)] private float mentorBonusPerTier = 0.05f;

    [Header("Combat")]
    [Tooltip("A monster's worth in experience: its Experience, or this much per point of its health. Each blow gives the " +
             "share of that worth it took off the monster's health, to the skill that dealt it.")]
    [SerializeField, Min(0f)] private float killExperiencePerHealth = 0.25f;
    [SerializeField, Min(0)] private int minimumKillExperience = 3;
    [Tooltip("A parry or a perfect dodge, in tier I experience (scaled by the place's tier). Trains Melee.")]
    [SerializeField, Min(0)] private int guardExperience = 3;
    [Tooltip("Parries and perfect dodges that pay against any one monster: dancing with a wolf all day teaches nothing more.")]
    [SerializeField, Min(0)] private int guardRewardsPerFoe = 5;

    [Header("Sneaking (tier I experience, scaled by the place's tier)")]
    [Tooltip("The first blow on an unaware monster.")]
    [SerializeField, Min(0)] private int ambushExperience = 8;
    [Tooltip("Sneaking for Slip Seconds within sight of a monster that hasn't spotted you.")]
    [SerializeField, Min(0)] private int slipExperience = 4;
    [SerializeField, Min(1f)] private float slipSeconds = 6f;
    [Tooltip("How often slipping past one monster pays.")]
    [SerializeField, Min(0)] private int slipsPerFoe = 3;
    [Tooltip("A monster that hunted you lost track of you.")]
    [SerializeField, Min(0)] private int lostTrackExperience = 10;
    [Tooltip("A lock pin set in the silent zone.")]
    [SerializeField, Min(0)] private int silentPinExperience = 2;

    [Header("Crafting")]
    [Tooltip("Experience for one craft, in its item tier's experience: a weapon or a piece of armour is a day's work, a batch " +
             "of arrows or planks a moment's.")]
    [SerializeField, Min(0f)] private float weaponWork = 3f;
    [SerializeField, Min(0f)] private float armorWork = 3f;
    [SerializeField, Min(0f)] private float toolWork = 2f;
    [SerializeField, Min(0f)] private float arrowWork = 0.5f;
    [SerializeField, Min(0f)] private float mealWork = 1f;
    [SerializeField, Min(0f)] private float tonicWork = 1.5f;
    [SerializeField, Min(0f)] private float materialWork = 0.5f;
    [Tooltip("A catch from the Dock's nets, lines or traps: less than a fish landed by hand.")]
    [SerializeField, Min(0f)] private float dockWork = 0.75f;
    [Tooltip("A building hammered up, in the tier experience of its level (a ruin repaired counts as one).")]
    [SerializeField, Min(0f)] private float buildWork = 4f;
    [Tooltip("A building upgraded, in the tier experience of the level it reached.")]
    [SerializeField, Min(0f)] private float upgradeWork = 6f;
    [Tooltip("What a Fine piece is worth, in levels of empowering at the altar (its stats, weapon damage and gear score).")]
    [SerializeField, Range(0f, 2f)] private float fineStrength = 0.5f;
    [Tooltip("What a Masterwork piece is worth, in levels of empowering.")]
    [SerializeField, Range(0f, 3f)] private float masterworkStrength = 1f;
    [Tooltip("Share of a maker's quality chance that comes out Masterwork rather than Fine.")]
    [SerializeField, Range(0f, 1f)] private float masterworkShare = 0.25f;

    [Header("Fishing and spells")]
    [Tooltip("A fish landed, in its tier's experience.")]
    [SerializeField, Min(0f)] private float catchWork = 2f;
    [Tooltip("Any spell cast, in tier I experience (scaled by the place's tier): healing and warding train Runecraft too.")]
    [SerializeField, Min(0)] private int castExperience = 3;

    [Header("Co-op")]
    [Tooltip("Share of a friend's practice the player learns by watching it nearby.")]
    [SerializeField, Range(0f, 1f)] private float watchShare = 0.25f;
    [Tooltip("How near, in metres, a friend's practice teaches.")]
    [SerializeField, Min(1f)] private float watchReach = 20f;

    [Header("Insight (put into any skill)")]
    [Tooltip("For a completed quest without its own Experience.")]
    [SerializeField, Min(0)] private int questExperience = 50;
    [SerializeField, Min(0)] private int raidExtractedExperience = 150;
    [Tooltip("A raid that went wrong still teaches something.")]
    [SerializeField, Min(0)] private int raidFailedExperience = 40;
    [Tooltip("The first time each item is crafted.")]
    [SerializeField, Min(0)] private int firstCraftExperience = 10;
    [Tooltip("A node no skill trains.")]
    [SerializeField, Min(0)] private int handGatherExperience = 2;
    [Tooltip("1 Insight for this many square metres of new ground explored.")]
    [SerializeField, Min(1)] private int exploredCellsPerExperience = 150;
    [Tooltip("Insight goes into a skill in full below this level, so it can catch a neglected skill up...")]
    [SerializeField, Min(1)] private int insightFullBelow = 20;
    [Tooltip("...and at this share from there on, so practice always leads.")]
    [SerializeField, Range(0.05f, 1f)] private float insightShareAbove = 0.5f;
    [Tooltip("Insight the skills window's button puts into a skill at a time.")]
    [SerializeField, Min(1)] private int insightStep = 100;
    [Tooltip("Every Insight reward, times this (quests, raids, exploring, stones, books...). 0.2 = a fifth of the listed " +
             "amounts. Fractions add up across rewards. Cheats are not scaled.")]
    [SerializeField, Range(0f, 2f)] private float insightScale = 1f;
    [Tooltip("How much longer practice takes to raise a skill, at every level: 1.3 = 30% longer. Divides practice " +
             "experience (gathering, fighting, crafting...), so saved levels stay as they are.")]
    [SerializeField, Range(0.25f, 4f)] private float practiceTimeScale = 1f;

    [Header("Total level")]
    [Tooltip("Titles for every skill's level added up, lowest first: the highest reached is the player's.")]
    [SerializeField] private List<Title> titles = new List<Title>();
    [Tooltip("Total levels above the start (one per skill) for each point of Haven renown: the Haven is known by its people.")]
    [SerializeField, Min(1)] private int totalLevelsPerRenown = 20;

    [Header("Skills")]
    [Tooltip("Every skill, in the order the skills window shows them within their group.")]
    [SerializeField] private List<ProficiencyDefinition> skills = new List<ProficiencyDefinition>();

    /// <summary>A name for how far the player has come, from a total level up.</summary>
    [System.Serializable]
    public struct Title
    {
        [Min(0)] public int totalLevel;
        public string name;

        public Title(int totalLevel, string name)
        {
            this.totalLevel = totalLevel;
            this.name = name;
        }
    }

    // Total experience needed to reach each level, worked out once.
    private int[] totals;

    public int MaxLevel => maxLevel;
    public int BaselineLevel => Mathf.Clamp(baselineLevel, 1, maxLevel);
    public SoundDefinition LevelUpSound => levelUpSound;
    public float KillExperiencePerHealth => killExperiencePerHealth;
    public int MinimumKillExperience => minimumKillExperience;
    public int GuardExperience => guardExperience;
    public int GuardRewardsPerFoe => guardRewardsPerFoe;
    public int AmbushExperience => ambushExperience;
    public int SlipExperience => slipExperience;
    public float SlipSeconds => slipSeconds;
    public int SlipsPerFoe => slipsPerFoe;
    public int LostTrackExperience => lostTrackExperience;
    public int SilentPinExperience => silentPinExperience;
    public float RaidMultiplier => raidMultiplier;

    /// <summary>The share more experience a skill gets from its mentor building at this built tier (0 when unbuilt or I).</summary>
    public float MentorBonus(int builtTier) => Mathf.Max(0, builtTier - 1) * mentorBonusPerTier;
    public int QuestExperience => questExperience;
    public int RaidExtractedExperience => raidExtractedExperience;
    public int RaidFailedExperience => raidFailedExperience;
    public int FirstCraftExperience => firstCraftExperience;
    public int HandGatherExperience => handGatherExperience;
    public float BuildWork => buildWork;
    public float UpgradeWork => upgradeWork;
    public float MasterworkShare => masterworkShare;
    public float CatchWork => catchWork;
    public int CastExperience => castExperience;
    public int ExploredCellsPerExperience => exploredCellsPerExperience;
    public int InsightStep => insightStep;
    public float WatchShare => watchShare;

    /// <summary>The title for a total level, or empty when none is reached yet.</summary>
    public string TitleFor(int totalLevel)
    {
        string title = "";
        foreach (Title entry in titles)
        {
            if (totalLevel >= entry.totalLevel)
            {
                title = entry.name;
            }
        }

        return title;
    }

    /// <summary>The Haven renown the player's total level brings.</summary>
    public int RenownFor(int totalLevel) => Mathf.Max(0, totalLevel - skills.Count) / Mathf.Max(1, totalLevelsPerRenown);
    public float WatchReach => watchReach;
    public int InsightFullBelow => insightFullBelow;
    public float InsightScale => insightScale;
    public float PracticeTimeScale => practiceTimeScale;

    /// <summary>Experience one point of Insight is worth to a skill at this level.</summary>
    public float InsightRate(int level) => level < insightFullBelow ? 1f : insightShareAbove;
    public IReadOnlyList<ProficiencyDefinition> Skills => skills;

    /// <summary>Experience needed to go from <paramref name="level"/> to the next one (rounded to 5).</summary>
    public int ExperienceToNext(int level)
    {
        // Eased off geometrically, no faster than the levels grow, so no level is ever cheaper than the one before.
        float early = level <= earlyLevels ? Mathf.Pow(earlySlowdown, (earlyLevels - (level - 1)) / (float)earlyLevels) : 1f;
        float needed = firstLevelExperience * Mathf.Pow(levelGrowth, Mathf.Max(0, level - 1)) * early;
        return Mathf.Max(1, Mathf.RoundToInt(needed / 5f) * 5);
    }

    /// <summary>Total experience to reach <paramref name="level"/> from nothing.</summary>
    public int TotalExperienceFor(int level)
    {
        int[] table = Totals();
        return table[Mathf.Clamp(level, 1, maxLevel)];
    }

    /// <summary>The level a skill with this much total experience is at.</summary>
    public int LevelFor(int totalExperience)
    {
        int[] table = Totals();
        int level = 1;
        while (level < maxLevel && totalExperience >= table[level + 1])
        {
            level++;
        }

        return level;
    }

    /// <summary>Where a level sits on the bonus curve: -1 at level 1, 0 at the baseline (eased: most of the way there within a
    /// few levels), 1 at the top (evenly from the baseline).</summary>
    public float CurvePosition(int level)
    {
        int baseline = BaselineLevel;
        level = Mathf.Clamp(level, 1, maxLevel);
        if (level <= baseline)
        {
            // Eased: the first levels take away most of a beginner's penalties, so the first hour feels like quick growth.
            return baseline > 1 ? -Mathf.Pow((baseline - level) / (float)(baseline - 1), beginnerEase) : 0f;
        }

        return maxLevel > baseline ? (level - baseline) / (float)(maxLevel - baseline) : 0f;
    }

    /// <summary>The skill level a player is expected to have reached by a raid tier: the baseline at tier I, a band more
    /// each tier after (20, 30, 40 with 10 levels per tier). Raid tiers are tuned to it (GearBalance).</summary>
    public int ExpectedLevel(int tier) => Mathf.Clamp(BaselineLevel + (tier - 1) * Mathf.Max(1, levelsPerTier), 1, maxLevel);

    /// <summary>What a skill's level bonuses to one stat come to at a level (0 when it has none).</summary>
    public static float BonusAt(ProficiencyDefinition skill, StatType stat, int level, LevelingSettings settings)
    {
        float total = 0f;
        if (skill != null)
        {
            foreach (ProficiencyDefinition.LevelBonus bonus in skill.LevelBonuses)
            {
                if (bonus.stat == stat)
                {
                    total += ProficiencyDefinition.ValueAt(bonus, level, settings);
                }
            }
        }

        return total;
    }

    /// <summary>The tier band a skill level is at home in: 1 for levels 1-10, 2 for 11-20... (with 10 levels per tier).</summary>
    public int TierBand(int level) => 1 + (Mathf.Max(1, level) - 1) / Mathf.Max(1, levelsPerTier);

    /// <summary>
    /// An action of <paramref name="tier"/>'s experience before the skill's level and place count: the tier's amount, or
    /// for actions measured in tier I experience (a parry, a silent pin) <paramref name="tierOneAmount"/> grown the same way.
    /// </summary>
    public float TierExperience(int tier, int tierOneAmount = 0)
    {
        if (tierExperience == null || tierExperience.Length == 0)
        {
            return 0f;
        }

        float amount = tierExperience[Mathf.Clamp(tier, 1, tierExperience.Length) - 1];
        return tierOneAmount > 0 ? amount * tierOneAmount / Mathf.Max(1, tierExperience[0]) : amount;
    }

    /// <summary>What experience from an action of <paramref name="tier"/> is multiplied by for a skill at
    /// <paramref name="skillLevel"/>: less when the action is below the skill's band, more on a raid.</summary>
    public float Multiplier(int tier, int skillLevel, bool onRaid)
    {
        int below = TierBand(skillLevel) - Mathf.Max(1, tier);
        float share = below <= 0 ? 1f : below == 1 ? oneTierBelow : farBelow;
        return share * (onRaid ? raidMultiplier : 1f);
    }

    /// <summary>What an action of <paramref name="tier"/> is worth to a skill at <paramref name="skillLevel"/>, rounded.</summary>
    public int ActionExperience(int tier, int skillLevel, bool onRaid, int tierOneAmount = 0) =>
        Mathf.Max(1, Mathf.RoundToInt(TierExperience(tier, tierOneAmount) * Multiplier(tier, skillLevel, onRaid)));

    /// <summary>The skill this kind of practice trains (a gathering skill goes by its tool: SkillFor), or null.</summary>
    public ProficiencyDefinition SkillTrainedBy(ProficiencyTraining training)
    {
        foreach (ProficiencyDefinition skill in skills)
        {
            if (skill != null && skill.Training == training)
            {
                return skill;
            }
        }

        return null;
    }

    public ProficiencyDefinition FindSkill(string id)
    {
        foreach (ProficiencyDefinition skill in skills)
        {
            if (skill != null && skill.Id == id)
            {
                return skill;
            }
        }

        return null;
    }

    /// <summary>A craft's experience, in its item tier's experience, by the kind of goods.</summary>
    public float CraftWork(CraftedGoods goods)
    {
        switch (goods)
        {
            case CraftedGoods.Weapons: return weaponWork;
            case CraftedGoods.Armor: return armorWork;
            case CraftedGoods.Tools: return toolWork;
            case CraftedGoods.Arrows: return arrowWork;
            case CraftedGoods.Meals: return mealWork;
            case CraftedGoods.Tonics: return tonicWork;
            case CraftedGoods.Materials: return materialWork;
            case CraftedGoods.Catch: return dockWork;
            default: return 0f;
        }
    }

    /// <summary>What a better make is worth, in levels of empowering.</summary>
    public float QualityStrength(ItemQuality quality) =>
        quality == ItemQuality.Masterwork ? masterworkStrength : quality == ItemQuality.Fine ? fineStrength : 0f;

    /// <summary>The crafting skill that making these goods trains, or null.</summary>
    public ProficiencyDefinition SkillCrafting(CraftedGoods goods)
    {
        if (goods == CraftedGoods.None)
        {
            return null;
        }

        foreach (ProficiencyDefinition skill in skills)
        {
            if (skill != null && (skill.Crafts & goods) != 0)
            {
                return skill;
            }
        }

        return null;
    }

    /// <summary>The gathering skill nodes of this tool train (for no tool, the one that gathers by hand), or null.</summary>
    public ProficiencyDefinition SkillFor(ToolType tool)
    {
        foreach (ProficiencyDefinition skill in skills)
        {
            if (skill != null && skill.Training == ProficiencyTraining.Gathering
                && (tool == ToolType.None ? skill.GathersByHand : skill.GatherTool == tool))
            {
                return skill;
            }
        }

        return null;
    }

    /// <summary>The perk with this id and the skill whose milestone offers it.</summary>
    public PerkDefinition FindPerk(string id, out ProficiencyDefinition offeredBy)
    {
        foreach (ProficiencyDefinition skill in skills)
        {
            if (skill == null)
            {
                continue;
            }

            foreach (ProficiencyDefinition.Milestone milestone in skill.Milestones)
            {
                foreach (PerkDefinition perk in new[] { milestone.first, milestone.second })
                {
                    if (perk != null && perk.Id == id)
                    {
                        offeredBy = skill;
                        return perk;
                    }
                }
            }
        }

        offeredBy = null;
        return null;
    }

    /// <summary>Sets the curve and skills up (tests and editor tools).</summary>
    public void Configure(int levels, int firstLevel, float growth, int baseline, IEnumerable<ProficiencyDefinition> allSkills, float slowStart = 1f)
    {
        maxLevel = Mathf.Max(2, levels);
        firstLevelExperience = Mathf.Max(1, firstLevel);
        levelGrowth = Mathf.Clamp(growth, 1f, 2f);
        earlySlowdown = Mathf.Clamp(slowStart, 1f, 3f);
        baselineLevel = Mathf.Max(1, baseline);
        skills = new List<ProficiencyDefinition>(allSkills);
        totals = null;
    }

    private void OnValidate() => totals = null;

    private int[] Totals()
    {
        if (totals != null && totals.Length == maxLevel + 1)
        {
            return totals;
        }

        totals = new int[maxLevel + 1];
        for (int level = 2; level <= maxLevel; level++)
        {
            totals[level] = totals[level - 1] + ExperienceToNext(level - 1);
        }

        return totals;
    }
}
