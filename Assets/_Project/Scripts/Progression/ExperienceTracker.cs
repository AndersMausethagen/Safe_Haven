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
/// <item>Everything else is Insight (<see cref="GrantInsight"/>), put into a skill of the player's choice in the skills
/// window: quests, raids, first crafts, buildings, catches, new ground, rune books, experience stones, blows no skill
/// trains yet.</item>
/// </list>
/// Announces levels and milestones and, in each scene with a player, adds the experience bar and the skills window. A
/// game system (GameSystems).
/// </summary>
public sealed class ExperienceTracker : MonoBehaviour, IGameSystem
{
    private readonly Dictionary<string, float> carried = new Dictionary<string, float>();
    private LevelingSettings settings;
    private int exploredCells;
    private float carriedInsight;

    public static ExperienceTracker Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

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
    public static void Practise(ProficiencyDefinition skill, int tier, float baseAmount)
    {
        if (skill == null || baseAmount <= 0f)
        {
            return;
        }

        LevelingSettings rules = Rules;
        CharacterProgress progress = GameSession.Progress;
        float amount = baseAmount * rules.Multiplier(tier, progress.LevelOf(skill, rules), OnRaid);
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

    /// <summary>Puts Insight into a skill (the skills window), announcing any level it reaches.</summary>
    public static int SpendInsight(ProficiencyDefinition skill, int amount)
    {
        LevelingSettings rules = Rules;
        CharacterProgress progress = GameSession.Progress;
        int before = progress.LevelOf(skill, rules);
        int spent = progress.SpendInsight(skill, amount, rules);
        Announce(skill, before, progress.LevelOf(skill, rules));
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

    private void OnConstructionFinished(Construction construction)
    {
        if (Application.isPlaying)
        {
            GrantInsight(construction.Level <= 1 ? settings.BuildExperience : settings.UpgradeExperience);
        }
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
