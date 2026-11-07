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
    private LevelingSettings settings;
    private int exploredCells;
    private float carriedInsight;

    public static ExperienceTracker Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Raised with a skill and the level it reached, and whether that level is a milestone (the HUD's banner).</summary>
    public static event Action<ProficiencyDefinition, int, bool> LevelReached;

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
    }

    private void OnDestroy()
    {
        GameEvents.Raised -= OnGameEvent;
        ResourceSource.Harvested -= OnHarvested;
        ExplorationMap.Explored -= OnExplored;
        Construction.Finished -= OnConstructionFinished;
        if (Current == this)
        {
            Current = null;
        }
    }

    public void OnSceneEntered(Scene scene)
    {
        exploredCells = 0;
        if (Player.Current != null)
        {
            ExperienceHud.Create();
            SkillsWindow.Create();
            JournalWindow.Create();
            JournalNotices.CheckPages();
            MinimapHud.Create();
        }
    }

    /// <summary>Gives Insight now (an experience stone, a story, a quest): experience the player puts into any skill.</summary>
    public static void GrantInsight(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        GameSession.Progress.AddInsight(amount);
        Gained?.Invoke(null, amount);
    }

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
        float amount = baseAmount * rules.Multiplier(tier, progress.LevelOf(skill, rules), false) * Boost(skill);
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
    /// (SettlementState remembers them on raids too) and the player's Experience Gain (rested, the sauna).
    /// </summary>
    public static float Boost(ProficiencyDefinition skill)
    {
        float mentor = skill != null && skill.Mentor != null ? Rules.MentorBonus(GameSession.Settlement.BuiltTier(skill.Mentor)) : 0f;
        float gain = Player.Current != null && Player.Current.Combatant != null ? Player.Current.Combatant.Stats.Get(StatType.ExperienceGain) : 0f;
        return Mathf.Max(0f, 1f + mentor + gain);
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
        if (Current == null)
        {
            GrantInsight(Mathf.RoundToInt(amount));
            return;
        }

        Current.carriedInsight += Mathf.Max(0f, amount);
        int whole = Mathf.FloorToInt(Current.carriedInsight);
        Current.carriedInsight -= whole;
        GrantInsight(whole);
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

            float held = 0f;
            foreach (Lesson lesson in Current.lessons.Values)
            {
                held += lesson.Held;
            }

            return Mathf.FloorToInt(held);
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
            ? GameInput.WithRebinds($"{skill.DisplayName} {after}! A perk to choose (N).")
            : $"{skill.DisplayName} {after}!", MessageKind.Gain);
        LevelReached?.Invoke(skill, after, milestone);
        if (Player.Current != null)
        {
            AudioManager.Play(Rules.LevelUpSound, Player.Current.transform.position);
        }
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
    private void OnConstructionFinished(Construction construction)
    {
        if (!Application.isPlaying)
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
            Practise(skill, node.Tier, settings.TierExperience(node.Tier));
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
