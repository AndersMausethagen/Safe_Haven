using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Turns what the player does into experience for GameSession.Progress, with the amounts in LevelingSettings:
/// <list type="bullet">
/// <item>Practice trains a skill (<see cref="Practise"/>): nodes gathered train the skill of their tool, and the player's
/// own PlayerTraining reports blows, parries, ambushes and the like. An action is worth its tier's experience, less
/// below the skill's band and more on a raid.</item>
/// <item>What the player makes trains the crafting skill of its goods (<see cref="PractiseCraft"/>), and buildings
/// hammered up train Construction.</item>
/// <item>Everything else is Insight (<see cref="GrantInsight"/>), put into a skill of the player's choice in the skills
/// window: quests, raids, first crafts, catches, new ground, rune books, experience stones, blows no skill trains yet.</item>
/// </list>
/// Announces levels and milestones and, in each scene with a player, adds the experience bar and the skills window. A
/// game system (GameSystems).
/// </summary>
public sealed class ExperienceTracker : MonoBehaviour, IGameSystem
{
    // What each skill learned on the raid under way: its level when it started, the experience kept, and the raid's bonus,
    // held until the raid ends (SettleRaid): it comes home only with an extraction.
    private sealed class Lesson
    {
        public int LevelBefore;
        public float Kept;
        public float Held;
    }

    private readonly Dictionary<string, float> carried = new Dictionary<string, float>();
    private readonly Dictionary<ProficiencyDefinition, Lesson> lessons = new Dictionary<ProficiencyDefinition, Lesson>();
    private RaidSession lessonsFor;

    // The stretch at home since the last raid: each skill's level when it began, the perks then, and the good work since.
    private readonly Dictionary<ProficiencyDefinition, int> homeStart = new Dictionary<ProficiencyDefinition, int>();
    private CharacterProgress homeProgress;
    private int homePerks;
    private int homeFine;
    private int homeMasterwork;
    private LevelingSettings settings;
    private int exploredCells;
    private float carriedInsight;

    public static ExperienceTracker Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        SpendingInsight = false;
    }

    /// <summary>Raised with a skill and the level it reached, and whether that level is a milestone (the HUD's banner).</summary>
    public static event Action<ProficiencyDefinition, int, bool> LevelReached;

    /// <summary>Raised with the experience each practice earned (what friends nearby learn a share of: CoopLessons).</summary>
    public static event Action<ProficiencyDefinition, float> Practised;

    /// <summary>Raised with the skill and the Insight put into it (the playtest log).</summary>
    public static event Action<ProficiencyDefinition, int> InsightSpent;

    /// <summary>True while Insight is raising a skill, so a level reached then can be told from practice.</summary>
    public static bool SpendingInsight { get; private set; }

    /// <summary>Raised with each amount gained: the skill, or null for Insight (the experience bar shows it).</summary>
    public static event Action<ProficiencyDefinition, int> Gained;

    /// <summary>The tier of where the player is: the raid's, or I at home and in the prologue.</summary>
    public static int PlaceTier => GameSession.ActiveRaid != null ? Mathf.Max(1, GameSession.ActiveRaid.Tier) : 1;

    public static bool OnRaid => GameSession.ActiveRaid != null;

    private static LevelingSettings Rules => Current != null && Current.settings != null ? Current.settings : LevelingSettings.Current;

    public void Initialise(LevelingSettings levelingSettings)
    {
        settings = levelingSettings;
        Current = this;
        GameEvents.Raised += OnGameEvent;
        ResourceSource.Harvested += OnHarvested;
        ExplorationMap.Explored += OnExplored;
        Construction.Finished += OnConstructionFinished;
        CraftedWork.Made += OnMade;
    }

    private void OnDestroy()
    {
        GameEvents.Raised -= OnGameEvent;
        ResourceSource.Harvested -= OnHarvested;
        ExplorationMap.Explored -= OnExplored;
        Construction.Finished -= OnConstructionFinished;
        CraftedWork.Made -= OnMade;
        if (Current == this)
        {
            Current = null;
        }
    }

    public void OnSceneEntered(Scene scene)
    {
        exploredCells = 0;
        EnsureHomeStretch(); // a new or loaded game starts its stretch at home here
        if (Player.Current != null)
        {
            ExperienceHud.Create();
            CraftFeedback.Create();
            SkillsWindow.Create();
            JournalWindow.Create();
            TutorialWindow.Create();
            TutorialNoticeHud.Create();
            PlaceDiscovery.Begin(scene.name);
            TerritorySpawner.Begin(scene.name);
            HavenPlaceVerbs.Begin(scene.name);
            RegionTitleHud.Create(scene.name);
            JournalNotices.CheckPages();
            // The main menu's Cinematics lists what this computer has seen: a save that has reached one counts.
            CinematicArchive.RememberReached(JournalSettings.Current, GameSession.Quests, GameSession.Progress);
            MinimapHud.Create();
        }
    }

    /// <summary>Gives Insight now (an experience stone, a story, a quest): experience the player puts into any skill. The
    /// listed amount is scaled by LevelingSettings › Insight Scale; fractions are kept until they add up.</summary>
    public static void GrantInsight(int amount)
    {
        if (amount > 0)
        {
            GrantInsightShare(amount);
        }
    }

    /// <summary>Insight exactly as given, unscaled (the cheat menu).</summary>
    public static void GrantInsightExactly(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        GameSession.Progress.AddInsight(amount);
        Gained?.Invoke(null, amount);
    }

    /// <summary>What a listed Insight reward is really worth, for text that names the amount.</summary>
    public static int InsightWorth(int listed) => Mathf.Max(listed > 0 ? 1 : 0, Mathf.RoundToInt(listed * InsightScale));

    private static float InsightScale => Rules != null ? Rules.InsightScale : 1f;

    /// <summary>
    /// Practice of a skill at a place or node of <paramref name="tier"/>: <paramref name="baseAmount"/> experience before the
    /// skill's band and the raid bonus. Fractions are kept and paid once they add up to a whole point.
    /// </summary>
    public static void Practise(ProficiencyDefinition skill, int tier, float baseAmount) => Practise(skill, tier, baseAmount, OnRaid);

    /// <summary>Practice with the raid bonus decided by the caller: a station order that finishes while the player is
    /// away on a raid was still made at home.</summary>
    public static void Practise(ProficiencyDefinition skill, int tier, float baseAmount, bool onRaid)
    {
        if (skill == null || baseAmount <= 0f)
        {
            return;
        }

        LevelingSettings rules = Rules;
        CharacterProgress progress = GameSession.Progress;
        float amount = baseAmount * rules.Multiplier(tier, progress.LevelOf(skill, rules), false) * Boost(skill)
                       / Mathf.Max(0.01f, rules.PracticeTimeScale);
        Practised?.Invoke(skill, amount);
        // On a raid the bonus share waits for the way home: kept on extraction, lost otherwise.
        float bonus = onRaid ? amount * (rules.RaidMultiplier - 1f) : 0f;
        if (bonus > 0f && Current != null && GameSession.ActiveRaid != null)
        {
            Current.Note(skill, amount, bonus);
        }
        else
        {
            amount += bonus;
        }

        if (Current != null)
        {
            Current.carried.TryGetValue(skill.Id, out float before);
            amount += before;
        }

        int whole = Mathf.FloorToInt(amount);
        if (Current != null)
        {
            Current.carried[skill.Id] = amount - whole;
        }

        Award(skill, whole);
    }

    /// <summary>
    /// What a skill's experience is multiplied by beyond the action itself: its mentor building's tiers at home
    /// (SettlementState remembers them on raids too) and the player's Experience Gain (rested, the sauna). Home is one's own
    /// Haven (GameSession.Home): on a raid followed with the host (co-op), the Haven one stands in is still the host's.
    /// </summary>
    public static float Boost(ProficiencyDefinition skill)
    {
        float mentor = skill != null && skill.Mentor != null ? Rules.MentorBonus(GameSession.Home.BuiltTier(skill.Mentor)) : 0f;
        float gain = Player.Current != null && Player.Current.Combatant != null ? Player.Current.Combatant.Stats.Get(StatType.ExperienceGain) : 0f;
        return Mathf.Max(0f, 1f + mentor + gain);
    }

    /// <summary>The tier a resident teaches at: their rank in the job, or their workplace's tier if that is higher.</summary>
    public static int LessonTier(ResidentRecord teacher)
    {
        if (teacher == null || teacher.Job == null)
        {
            return 1;
        }

        int rank = 1 + SettlementSettings.Current.RankIndex(teacher.WorkDone);
        int workplace = teacher.Job.Workplace != null ? GameSession.Here.BuiltTier(teacher.Job.Workplace) : 0;
        return Mathf.Clamp(Mathf.Max(rank, workplace), 1, Tiers.Max);
    }

    /// <summary>
    /// A resident's lesson in their job's skill: the tier experience of what they know times the lesson's work, falling off
    /// like practice when the player already knows more than they can teach. Once between raids each. Returns what it gave.
    /// </summary>
    public static int TakeLesson(ResidentRecord teacher)
    {
        ProficiencyDefinition skill = teacher?.Job?.Teaches;
        if (skill == null || teacher.TaughtSinceRaid)
        {
            return 0;
        }

        int before = GameSession.Progress.ExperienceOf(skill);
        int tier = LessonTier(teacher);
        Practise(skill, tier, Rules.TierExperience(tier) * SettlementSettings.Current.LessonWork, false);
        teacher.TaughtSinceRaid = true;
        return GameSession.Progress.ExperienceOf(skill) - before;
    }

    /// <summary>A share of a friend's practice, learned by watching (CoopLessons). Kept at once, and never passed on again.</summary>
    public static void LearnByWatching(ProficiencyDefinition skill, float amount) => Teach(skill, amount);

    /// <summary>Experience given in full to a skill, without fall-off or the raid bonus: a lesson (a crafting order's, a friend's).</summary>
    public static void Teach(ProficiencyDefinition skill, float amount)
    {
        if (skill == null || amount <= 0f)
        {
            return;
        }

        float total = amount;
        if (Current != null)
        {
            Current.carried.TryGetValue(skill.Id, out float before);
            total += before;
        }

        int whole = Mathf.FloorToInt(total);
        if (Current != null)
        {
            Current.carried[skill.Id] = total - whole;
        }

        Award(skill, whole);
    }

    /// <summary>Practice of whatever skill this kind of training trains; Insight while no skill does.</summary>
    public static void Practise(ProficiencyTraining training, int tier, float baseAmount)
    {
        ProficiencyDefinition skill = Rules.SkillTrainedBy(training);
        if (skill != null)
        {
            Practise(skill, tier, baseAmount);
        }
        else
        {
            GrantInsightShare(baseAmount * (OnRaid ? Rules.RaidMultiplier : 1f));
        }
    }

    /// <summary>Insight from something worth a fraction of a point (a blow no skill trains): kept until it adds up.</summary>
    public static void GrantInsightShare(float amount)
    {
        amount *= InsightScale;
        if (Current == null)
        {
            GrantInsightExactly(Mathf.RoundToInt(amount));
            return;
        }

        Current.carriedInsight += Mathf.Max(0f, amount);
        int whole = Mathf.FloorToInt(Current.carriedInsight);
        Current.carriedInsight -= whole;
        GrantInsightExactly(whole);
    }

    /// <summary>
    /// The player made something: <paramref name="batches"/> crafts of the recipe train the crafting skill for its kind of
    /// goods (CraftedWork.Of), each worth its tier's experience times the goods' work (LevelingSettings › Crafting). Never
    /// the raid bonus: crafting is home work. Workers' orders train nobody; callers leave them out.
    /// </summary>
    public static void PractiseCraft(RecipeDefinition recipe, int batches = 1)
    {
        LevelingSettings rules = Rules;
        CraftedGoods goods = CraftedWork.Of(recipe);
        ProficiencyDefinition skill = rules.SkillCrafting(goods);
        if (skill != null && batches > 0)
        {
            int tier = Mathf.Max(1, recipe.Output.Tier);
            Practise(skill, tier, rules.TierExperience(tier) * rules.CraftWork(goods) * batches, false);
        }
    }

    private void Note(ProficiencyDefinition skill, float kept, float held)
    {
        if (lessonsFor != GameSession.ActiveRaid)
        {
            lessons.Clear();
            lessonsFor = GameSession.ActiveRaid;
        }

        if (!lessons.TryGetValue(skill, out Lesson lesson))
        {
            lesson = new Lesson { LevelBefore = GameSession.Progress.LevelOf(skill, settings) };
            lessons[skill] = lesson;
        }

        lesson.Kept += kept;
        lesson.Held += held;
    }

    /// <summary>
    /// The raid is over: its held bonus experience comes home with an extraction and is lost otherwise, and what each skill
    /// learned goes into the report. RaidDirector calls it before the raid ends.
    /// </summary>
    public static void SettleRaid(RaidReport report, bool extracted)
    {
        if (Current == null || Current.lessonsFor != GameSession.ActiveRaid || report == null)
        {
            return;
        }

        LevelingSettings rules = Rules;
        int lost = 0;
        foreach (KeyValuePair<ProficiencyDefinition, Lesson> entry in Current.lessons)
        {
            int bonus = Mathf.FloorToInt(entry.Value.Held);
            if (extracted)
            {
                Award(entry.Key, bonus);
            }
            else
            {
                lost += bonus;
            }

            int levelAfter = GameSession.Progress.LevelOf(entry.Key, rules);
            report.Lessons.Add(new SkillLesson(entry.Key, entry.Value.LevelBefore, levelAfter, Mathf.RoundToInt(entry.Value.Kept), bonus, extracted));
        }

        report.Lessons.Sort((a, b) => (b.Experience + b.Bonus).CompareTo(a.Experience + a.Bonus));
        if (lost > 0)
        {
            GameMessages.Post($"The raid's extra lessons were lost with it (-{lost} experience). Get out to bring them home.", MessageKind.Warning);
        }

        Current.lessons.Clear();
        Current.lessonsFor = null;
    }

    /// <summary>The raid bonus held so far on the raid under way, all skills together (the HUD shows it).</summary>
    public static int HeldBonus
    {
        get
        {
            if (Current == null || Current.lessonsFor == null || Current.lessonsFor != GameSession.ActiveRaid)
            {
                return 0;
            }

            // Each skill's whole points, as SettleRaid pays them: the fractions summed showed "4 held" for five skills at 0.9
            // that pay nothing.
            int held = 0;
            foreach (Lesson lesson in Current.lessons.Values)
            {
                held += Mathf.FloorToInt(lesson.Held);
            }

            return held;
        }
    }

    /// <summary>Puts Insight into a skill (the skills window), announcing any level it reaches.</summary>
    public static int SpendInsight(ProficiencyDefinition skill, int amount)
    {
        LevelingSettings rules = Rules;
        CharacterProgress progress = GameSession.Progress;
        int before = progress.LevelOf(skill, rules);
        int spent = progress.SpendInsight(skill, amount, rules);
        SpendingInsight = true;
        try
        {
            Announce(skill, before, progress.LevelOf(skill, rules));
        }
        finally
        {
            SpendingInsight = false;
        }

        if (spent > 0)
        {
            InsightSpent?.Invoke(skill, spent);
        }

        return spent;
    }

    /// <summary>A monster's worth in experience: its own Experience, or worked out from its health.</summary>
    public static float MonsterWorth(MonsterDefinition monster, float maxHealth)
    {
        LevelingSettings rules = Rules;
        if (monster != null && monster.Experience > 0)
        {
            return monster.Experience;
        }

        return Mathf.Max(rules.MinimumKillExperience, maxHealth * rules.KillExperiencePerHealth);
    }

    private static void Award(ProficiencyDefinition skill, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        LevelingSettings rules = Rules;
        CharacterProgress progress = GameSession.Progress;
        int before = progress.LevelOf(skill, rules);
        int had = progress.ExperienceOf(skill);
        progress.AddExperience(skill, amount, rules);
        int gained = progress.ExperienceOf(skill) - had;
        if (gained > 0)
        {
            Gained?.Invoke(skill, gained);
        }

        Announce(skill, before, progress.LevelOf(skill, rules));
    }

    private static void Announce(ProficiencyDefinition skill, int before, int after)
    {
        if (skill == null || after <= before)
        {
            return;
        }

        bool milestone = false;
        foreach (ProficiencyDefinition.Milestone reached in skill.Milestones)
        {
            milestone |= reached.level > before && reached.level <= after;
        }

        GameMessages.Post(milestone
            ? GameInput.WithRebinds($"{skill.DisplayName} {after}! A perk to choose ({{key:Talents}}).")
            : $"{skill.DisplayName} {after}!", MessageKind.Gain);
        LevelReached?.Invoke(skill, after, milestone);
        int flourish = TechniqueSettings.Current.FlourishLevel;
        if (skill.Training == ProficiencyTraining.Melee && before < flourish && after >= flourish)
        {
            GameMessages.Post("Weapon flourishes: one-handed arts strike harder and a kill gives back Focus, two-handed arts sunder, polearm arts pin.", MessageKind.Gain);
        }
        AudioSettingsDefinition sounds = AudioManager.Sounds;
        UIMotion.Sound(milestone && sounds != null && sounds.Milestone != null ? sounds.Milestone : Rules.LevelUpSound);
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        switch (gameEvent.Kind)
        {
            case GameEventKind.QuestCompleted:
                if (gameEvent.Subject is QuestDefinition quest)
                {
                    GrantInsight(quest.Experience > 0 ? quest.Experience : settings.QuestExperience);
                }

                break;
            case GameEventKind.RaidFinished:
                StartHomeStretch();
                GrantInsight(gameEvent.Outcome == RaidOutcome.Extracted ? settings.RaidExtractedExperience : settings.RaidFailedExperience);
                break;
            case GameEventKind.Crafted:
                if (gameEvent.Subject != null && GameSession.Progress.MarkCrafted(gameEvent.Subject.name))
                {
                    GrantInsight(settings.FirstCraftExperience);
                }

                break;
            case GameEventKind.Consumed:
                if (gameEvent.Subject is ItemDefinition book && book.Experience > 0)
                {
                    GameMessages.Post($"You read the {book.DisplayName}.", MessageKind.Gain);
                    GrantInsight(book.Experience);
                }

                break;
        }
    }

    // A building hammered up or upgraded trains Construction, by the level it reached (as its tier).
    private void OnMade(ItemDefinition item, int count, ItemQuality quality, string name)
    {
        EnsureHomeStretch();
        homeFine += quality == ItemQuality.Fine ? count : 0;
        homeMasterwork += quality == ItemQuality.Masterwork ? count : 0;
    }

    private void EnsureHomeStretch()
    {
        if (homeProgress != GameSession.Progress)
        {
            StartHomeStretch();
        }
    }

    private void StartHomeStretch()
    {
        homeProgress = GameSession.Progress;
        homeStart.Clear();
        foreach (ProficiencyDefinition skill in settings.Skills)
        {
            if (skill != null)
            {
                homeStart[skill] = homeProgress.LevelOf(skill, settings);
            }
        }

        homePerks = homeProgress.PerkCount;
        homeFine = homeMasterwork = 0;
    }

    /// <summary>
    /// What the stretch at home since the last raid came to, for the portal's planner: skills that grew, perks taken,
    /// good work made. Null when nothing grew.
    /// </summary>
    public static string SinceLastRaid()
    {
        if (Current == null)
        {
            return null;
        }

        Current.EnsureHomeStretch();
        LevelingSettings rules = Rules;
        var parts = new List<string>();
        foreach (KeyValuePair<ProficiencyDefinition, int> start in Current.homeStart)
        {
            int now = GameSession.Progress.LevelOf(start.Key, rules);
            if (now > start.Value)
            {
                parts.Add($"{start.Key.DisplayName} {start.Value} → {now}");
            }
        }

        int perks = GameSession.Progress.PerkCount - Current.homePerks;
        if (perks > 0)
        {
            parts.Add($"{perks} perk{(perks == 1 ? "" : "s")} taken");
        }

        if (Current.homeFine + Current.homeMasterwork > 0)
        {
            parts.Add(Current.homeMasterwork > 0
                ? $"{Current.homeMasterwork} Masterwork and {Current.homeFine} Fine made"
                : $"{Current.homeFine} Fine piece{(Current.homeFine == 1 ? "" : "s")} made");
        }

        return parts.Count > 0 ? string.Join(",  ", parts) : null;
    }

    private void OnConstructionFinished(Construction construction)
    {
        if (!Application.isPlaying || construction.FinishedByOthers)
        {
            return;
        }

        int tier = Mathf.Max(1, construction.Level);
        float work = construction.Level <= 1 ? settings.BuildWork : settings.UpgradeWork;
        Practise(ProficiencyTraining.Construction, tier, settings.TierExperience(tier) * work);
    }

    // A node trains the skill of the tool it is gathered with, at the node's tier; one no skill trains gives Insight.
    private void OnHarvested(GameObject gatherer, ResourceSource source)
    {
        if (!Player.IsPlayer(gatherer))
        {
            return;
        }

        GatheringNodeDefinition node = source != null ? source.Node : null;
        ProficiencyDefinition skill = node != null ? settings.SkillFor(node.GatherTool) : null;
        if (skill != null)
        {
            // The source's tier: a felled tree trains as the tree it was (ResourceSource.TakeOnTree).
            int tier = source.Tier;
            Practise(skill, tier, settings.TierExperience(tier));
        }
        else
        {
            GrantInsight(settings.HandGatherExperience);
        }
    }

    private void OnExplored(int cells)
    {
        exploredCells += cells;
        int insight = exploredCells / settings.ExploredCellsPerExperience;
        if (insight > 0)
        {
            exploredCells -= insight * settings.ExploredCellsPerExperience;
            GrantInsight(insight);
        }
    }
}
