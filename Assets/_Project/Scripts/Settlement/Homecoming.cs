using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fuller homecoming (Docs/SystemsGapReview_2026-11-19.md §2; TODO "A fuller homecoming"): what the Haven has for the player the moment
/// they are back from a raid, on one card (HomecomingCard), about two seconds after arriving: orders ready to collect at the stations,
/// what their people made while they were gone, what is now within reach (the portal's next tier or a tracked level that everything
/// home now covers) and asks they can answer. The raid pays off the moment you are home. Only after a raid's return, never in a
/// friend's Haven; when there is nothing to say, nothing shows, and the workers' line is told as before.
/// </summary>
public static class Homecoming
{
    public const int MostLines = 8;

    /// <summary>True from a raid's end until the Haven has told its homecoming.</summary>
    public static bool Pending { get; private set; }

    // The raid just ended: who went along (no gift from them), how long it lasted and how it ended.
    private static ResidentDefinition along;
    private static float awaySeconds;
    private static RaidOutcome outcome = RaidOutcome.InProgress;

    /// <summary>How the raid the player last came home from ended; In Progress when none has since the game was loaded.</summary>
    public static RaidOutcome LastOutcome => outcome;

    /// <summary>Who went along on that raid, or null.</summary>
    public static ResidentDefinition LastAlong => along;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => ForgetGame();

    // After GameEvents clears its listeners (SubsystemRegistration), listen again.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Listen()
    {
        GameEvents.Raised -= OnRaised;
        GameEvents.Raised += OnRaised;
    }

    /// <summary>Another game loaded (GameSession.Reset): a raid's end in the last one is no homecoming of this one.</summary>
    public static void ForgetGame()
    {
        Pending = false;
        along = null;
        awaySeconds = 0f;
        outcome = RaidOutcome.InProgress;
    }

    private static void OnRaised(GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.RaidFinished)
        {
            Pending = true;
            awaySeconds = gameEvent.Seconds;
            outcome = gameEvent.Outcome;
            RaidReport report = GameSession.PendingReport;
            along = report != null ? report.Companion : null;
        }
    }

    /// <summary>Called as the Haven starts (HavenProgress): the card, when coming home from a raid and there is something to say.
    /// False when nothing was shown (the caller tells the workers' line itself).</summary>
    public static bool Tell()
    {
        if (!Pending)
        {
            return false;
        }

        Pending = false;
        if (HavenVisit.IsVisiting || Player.Current == null)
        {
            return false;
        }

        List<string> lines = Compose(Player.Current, GameSession.Home);
        // One good thing that happened while they were away (HomecomingSettings), heading the card.
        HomecomingSettings gifts = HomecomingSettings.Current;
        string gift = gifts != null && awaySeconds >= gifts.MinAwaySeconds
            ? AwayGifts.Draw(gifts, GameSession.Home, GameSession.Stash, UnityEngine.Random.value, UnityEngine.Random.value, along)
            : null;
        if (gift != null)
        {
            lines.Insert(0, gift);
            if (lines.Count > MostLines)
            {
                lines.RemoveAt(lines.Count - 1);
            }
        }

        if (lines.Count == 0)
        {
            return false;
        }

        GameSession.Home.MadeByWorkers.Clear();
        HomecomingCard.Show(lines);
        return true;
    }

    /// <summary>The card's lines, in order: ready to collect, made by your people, now within reach, asks you can answer.</summary>
    public static List<string> Compose(Player player, SettlementState home)
    {
        var lines = new List<string>();
        if (home == null)
        {
            return lines;
        }

        foreach (StationQueue queue in home.Workshops.Queues)
        {
            if (queue.Output is IDisplayName made && queue.OutputAmount > 0)
            {
                lines.Add($"Ready at the {StationName(queue.StationId)}: {queue.OutputAmount} {made.DisplayName}.");
            }
        }

        if (home.MadeByWorkers.Count > 0)
        {
            var made = new List<string>();
            foreach (KeyValuePair<IStackDefinition, int> entry in home.MadeByWorkers)
            {
                made.Add($"{entry.Value} {entry.Key.DisplayName}");
            }

            lines.Add($"Your people made {string.Join(", ", made)}.");
        }

        // Within reach: a target everything home now covers (WantedAtHome's targets with nothing wanted).
        List<WantedAtHome.Target> targets = WantedAtHome.Targets(home, GameSession.Progress, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName));
        MaterialPool pool = MaterialPool.For(player.gameObject);
        foreach (WantedAtHome.Target target in targets)
        {
            if (WantedAtHome.Compute(new[] { target }, pool).Count == 0)
            {
                lines.Add(GameInput.WithRebinds(target.Name.StartsWith("the portal")
                    ? $"Within reach: {target.Name}. Everything it takes is home: strengthen it at the portal."
                    // A plan is paid from the chests by itself (BuildPlans); with it in the bag, the player hammers it.
                    : target.Name.StartsWith("the planned")
                    ? $"Within reach: {target.Name}. Everything it takes is home: in the chests the builders start on it, or hammer it yourself."
                    : $"Within reach: {target.Name}. Everything it takes is home ({{key:Build}})."));
            }
        }

        ItemInventory bag = player.GetComponent<ItemInventory>();
        foreach (HavenRequestDefinition request in HavenRequestBoard.Standing())
        {
            if (request.Wanted != null && HavenRequestBoard.AtHome(request, bag) >= request.Count)
            {
                string who = request.Asker != null ? request.Asker.DisplayName : "Someone";
                lines.Add($"{who} asked for {request.Count} {request.Wanted.DisplayName}: you have them.");
            }
        }

        if (lines.Count > MostLines)
        {
            lines.RemoveRange(MostLines, lines.Count - MostLines);
        }

        return lines;
    }

    private static string StationName(string stationId)
    {
        foreach (CraftingStation station in CraftingStation.Active)
        {
            if (station != null && station.Id == stationId && station.TryGetComponent(out BuildingObject building) && building.Definition != null)
            {
                return building.Definition.DisplayName;
            }
        }

        return "workshop";
    }
}
