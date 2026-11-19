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
        // What a return home does is SettlementState.CameHome's to say. Co-op: a raid followed with the host counts for this
        // player's own Haven (its breaches, the ward's strain), not the copy of the host's they stand in, which the host's next
        // word would undo.
        if (gameEvent.Kind == GameEventKind.RaidFinished)
        {
            RaidOutcome outcome = gameEvent.Outcome;
            GameSession.Home.CameHome(outcome);
        }
    }

    private void Update()
    {
        // A friend's Haven runs in their game; this copy only shows it (co-op).
        if (HavenVisit.IsVisiting)
        {
            return;
        }

        // Only off a visit (above), where one's own Haven is the one here.
        SettlementState settlement = GameSession.Home;
        settlement.Workshops.Tick(Time.deltaTime, GameSession.Stash.Resources, OnCrafted);
        // Away from home, the Carpenter and Mason get on with the scaffolds left standing there (ResidentBuilders): those at
        // home, not one who went along on the raid.
        if (HomePortal.Current == null && settlement.ScaffoldsWaiting)
        {
            ResidentBuilders.WorkAway(settlement, Time.deltaTime, GameSession.ActiveRaid?.Companion);
        }

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
            // Made at the altar, as a station makes its work: a quest to purify counts it (Craft Item, the Purified Essence).
            GameEvents.Raise(new GameEvent(GameEventKind.Crafted, settings.PurifiedEssence, made));
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
            WorkerLearned(station, recipe);
            if (recipe.OutputDefinition is IStackDefinition made)
            {
                GameSession.Home.NoteMadeByWorker(made, recipe.OutputAmount);
            }
        }
        else
        {
            PlayerOrderFinished(station, recipe);
            if (station.IsIdle)
            {
                GameMessages.Post($"The {station.Type.DisplayName()} finished {recipe.DisplayName}.", MessageKind.Gain);
            }
        }
    }

    // A worker gets better at their job with every craft: a new rank speeds their station, and a practised hand may get
    // one more out of what they make.
    private static void WorkerLearned(StationQueue station, RecipeDefinition recipe)
    {
        // This station's own worker (two fishers at two docks: each learns at their own).
        ResidentRecord worker = GameSession.Here.WorkerAt(station.StationId, station.Type);
        if (worker == null)
        {
            return;
        }

        SettlementSettings settings = SettlementSettings.Current;
        int before = settings.RankIndex(worker.WorkDone);
        worker.WorkDone++;
        if (settings.RankIndex(worker.WorkDone) > before && worker.Job != null)
        {
            GameMessages.Post($"{worker.Resident.DisplayName} is now a {settings.RankFor(worker.WorkDone).name} {worker.Job.DisplayName}.", MessageKind.Gain);
        }

        if (recipe.Output != null && !recipe.Output.HasDurability && UnityEngine.Random.value < settings.RankFor(worker.WorkDone).extraChance + station.ExtraChance)
        {
            station.AddExtra(recipe.Output, 1);
        }
    }

    // The player's own order: it trains their crafting skill, and a practised hand may get one more out of it (an extra
    // portion, draught or plank, into the output slot while there is room). Gear gets its quality when it is collected.
    private static void PlayerOrderFinished(StationQueue station, RecipeDefinition recipe)
    {
        if (recipe.Output == null)
        {
            return;
        }

        ExperienceTracker.PractiseCraft(recipe);
        StatSheet maker = Player.Current != null && Player.Current.Combatant != null ? Player.Current.Combatant.Stats : null;
        if (!recipe.Output.HasDurability && UnityEngine.Random.value < CraftedWork.ExtraChance(CraftedWork.Of(recipe), maker))
        {
            station.AddExtra(recipe.Output, 1);
        }
    }

    /// <summary>Warns once when a station stops because a finished craft can't go in its output slot.</summary>
    private void LateUpdate()
    {
        // A friend's stations are theirs to hear about (co-op), as Update leaves them alone.
        if (HavenVisit.IsVisiting)
        {
            return;
        }

        foreach (StationQueue station in GameSession.Here.Workshops.Queues)
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
