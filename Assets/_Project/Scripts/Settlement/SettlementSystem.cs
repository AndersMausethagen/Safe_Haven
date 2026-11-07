using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps Safe Haven running wherever the player is: it counts successful extractions (the first ward breach waits for
/// one, GDD §6.6), works every crafting station's queue (results to storage or the station's output slot, see
/// StationQueue) and works through the Purification Altar's queue while a resident holds the purifier job (GDD §13.5),
/// putting the Purified Essence straight into storage. A persistent system created by GameSystems, so all of this keeps
/// going during raids.
/// </summary>
public sealed class SettlementSystem : MonoBehaviour, IGameSystem
{
    private const float WorkerLookSeconds = 1f;

    private readonly HashSet<string> blockedStations = new HashSet<string>();
    private float nextWorkerLook;
    private SettlementSettings settings;

    public void Initialise(SettlementSettings settlementSettings)
    {
        settings = settlementSettings;
        GameEvents.Raised += OnGameEvent;
    }

    private void OnDestroy()
    {
        GameEvents.Raised -= OnGameEvent;
    }

    public void OnSceneEntered(Scene scene)
    {
    }

    private static void OnGameEvent(GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.RaidFinished && gameEvent.Outcome == RaidOutcome.Extracted)
        {
            GameSession.Settlement.RecordExtraction();
        }
    }

    private void Update()
    {
        // A friend's Haven runs in their game; this copy only shows it (co-op).
        if (HavenVisit.IsVisiting)
        {
            return;
        }

        SettlementState settlement = GameSession.Settlement;
        settlement.Workshops.Tick(Time.deltaTime, GameSession.Stash.Resources, OnCrafted);
        if (Time.unscaledTime >= nextWorkerLook)
        {
            nextWorkerLook = Time.unscaledTime + WorkerLookSeconds;
            WorkerProduction.Work(settlement, GameSession.Stash);
        }

        if (settings == null || settlement.PurificationQueued <= 0 || !settlement.HasWorker(settings.PurifierJob))
        {
            return;
        }

        int finished = settlement.TickPurification(Time.deltaTime * (1f + settlement.PurificationBonus), settings.SecondsPerBatch);
        if (finished > 0 && settings.PurifiedEssence != null)
        {
            int made = GameSession.Stash.Resources.Add(settings.PurifiedEssence, settlement.PurifiedFrom(finished * settings.PurifiedPerBatch));
            GameMessages.Post($"The altar purified {made} {settings.PurifiedEssence.DisplayName} into storage.", MessageKind.Gain);
        }
    }

    // Quests count crafts when they finish, not when they are queued.
    private static void OnCrafted(StationQueue station, RecipeDefinition recipe)
    {
        GameEvents.Raise(new GameEvent(GameEventKind.Crafted, recipe.OutputDefinition, recipe.OutputAmount));
        if (station.WorkerCrafts > 0)
        {
            station.WorkerCrafts--;
            if (recipe.OutputDefinition is IStackDefinition made)
            {
                GameSession.Settlement.NoteMadeByWorker(made, recipe.OutputAmount);
            }
        }
        else if (station.IsIdle)
        {
            GameMessages.Post($"The {station.Type.DisplayName()} finished {recipe.DisplayName}.", MessageKind.Gain);
        }
    }

    /// <summary>Warns once when a station stops because a finished craft can't go in its output slot.</summary>
    private void LateUpdate()
    {
        foreach (StationQueue station in GameSession.Settlement.Workshops.Queues)
        {
            bool blocked = station.Blocked;
            if (blocked && !blockedStations.Contains(station.StationId))
            {
                GameMessages.Post($"The {station.Type.DisplayName()} has stopped: {station.WaitingFor}.", MessageKind.Warning);
            }

            if (blocked)
            {
                blockedStations.Add(station.StationId);
            }
            else
            {
                blockedStations.Remove(station.StationId);
            }
        }
    }
}
