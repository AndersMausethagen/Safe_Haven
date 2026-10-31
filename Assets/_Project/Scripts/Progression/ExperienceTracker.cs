using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Turns what the player does into experience for GameSession.Progress, with amounts from LevelingSettings: monsters
/// the player kills, quests completed, raids finished (more for getting out), the first craft of each item, nodes
/// gathered, new ground explored, rune books read and experience stones touched (Grant). Announces level-ups and, in
/// each scene with a player, adds the experience bar and the talent window. A game system (GameSystems).
/// </summary>
public sealed class ExperienceTracker : MonoBehaviour, IGameSystem
{
    private LevelingSettings settings;
    private int exploredCells;

    public static ExperienceTracker Instance { get; private set; }

    /// <summary>Raised with each amount of experience gained (the experience bar shows it).</summary>
    public static event Action<int> Gained;

    public void Initialise(LevelingSettings levelingSettings)
    {
        settings = levelingSettings;
        Instance = this;
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
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OnSceneEntered(Scene scene)
    {
        exploredCells = 0;
        if (Player.Current != null)
        {
            ExperienceHud.Create();
            TalentWindow.Create();
            MinimapHud.Create();
        }
    }

    /// <summary>Gives experience now (an experience stone, a story). Announces any level reached.</summary>
    public static void Grant(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        LevelingSettings rules = Instance != null ? Instance.settings : LevelingSettings.Current;
        CharacterProgress progress = GameSession.Progress;
        int levels = progress.AddExperience(amount, rules);
        Gained?.Invoke(amount);
        if (levels > 0)
        {
            int points = progress.UnspentPoints(rules);
            GameMessages.Post($"Level {progress.Level}! {points} talent point{(points == 1 ? "" : "s")} to spend (N).", MessageKind.Gain);
            if (Player.Current != null)
            {
                AudioManager.Play(rules.LevelUpSound, Player.Current.transform.position);
            }
        }
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        switch (gameEvent.Kind)
        {
            case GameEventKind.Killed:
                if (gameEvent.Subject is MonsterDefinition monster && gameEvent.ForPlayerSide)
                {
                    Grant(KillExperience(monster, gameEvent.Origin));
                }

                break;
            case GameEventKind.QuestCompleted:
                if (gameEvent.Subject is QuestDefinition quest)
                {
                    Grant(quest.Experience > 0 ? quest.Experience : settings.QuestExperience);
                }

                break;
            case GameEventKind.RaidFinished:
                Grant(gameEvent.Outcome == RaidOutcome.Extracted ? settings.RaidExtractedExperience : settings.RaidFailedExperience);
                break;
            case GameEventKind.Crafted:
                if (gameEvent.Subject != null && GameSession.Progress.MarkCrafted(gameEvent.Subject.name))
                {
                    Grant(settings.FirstCraftExperience);
                }

                break;
            case GameEventKind.Consumed:
                if (gameEvent.Subject is ItemDefinition book && book.Experience > 0)
                {
                    GameMessages.Post($"You read the {book.DisplayName}.", MessageKind.Gain);
                    Grant(book.Experience);
                }

                break;
        }
    }

    /// <summary>The experience for killing a monster: its own, or worked out from its health.</summary>
    public int KillExperience(MonsterDefinition monster, GameObject body)
    {
        if (monster.Experience > 0)
        {
            return monster.Experience;
        }

        float health = body != null && body.TryGetComponent(out Damageable damageable) ? damageable.MaxHealth : 0f;
        return Mathf.Max(settings.MinimumKillExperience, Mathf.RoundToInt(health * settings.KillExperiencePerHealth));
    }

    private void OnConstructionFinished(Construction construction)
    {
        if (Application.isPlaying)
        {
            Grant(construction.Level <= 1 ? settings.BuildExperience : settings.UpgradeExperience);
        }
    }

    private void OnHarvested(GameObject gatherer)
    {
        if (Player.IsPlayer(gatherer))
        {
            Grant(settings.GatherExperience);
        }
    }

    private void OnExplored(int cells)
    {
        exploredCells += cells;
        int experience = exploredCells / settings.ExploredCellsPerExperience;
        if (experience > 0)
        {
            exploredCells -= experience * settings.ExploredCellsPerExperience;
            Grant(experience);
        }
    }
}
