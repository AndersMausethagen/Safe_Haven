using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How long the way to each quest target really takes, for the playtest log (the user, 2026-11-27: "walking for several minutes
/// is not fun"; until now only straight-line estimates said how long). A clock starts when a quest's waypoint is made (its objective
/// open) and, once the objective is done, a <see cref="PlaytestLog.Trip"/> row says the played seconds it took, how far off it was
/// then, the share of that time in the saddle and how many times the old ways or the journal's "Go there" were taken meanwhile.
/// Trips left unfinished (a raid, a load) leave no row. Kept by QuestTargetWaypoints, one per scene.
/// </summary>
public sealed class PlaytestTrips
{
    private sealed class Trip
    {
        public string Label;
        public float Seconds;
        public float Ridden;
        public int Distance;
        public int Jumps;
    }

    private readonly Dictionary<QuestWaypoint, Trip> open = new Dictionary<QuestWaypoint, Trip>();

    public int Open => open.Count;

    /// <summary>A waypoint was made: its trip's clock starts.</summary>
    public void Begin(QuestWaypoint waypoint, string label)
    {
        if (waypoint == null || open.ContainsKey(waypoint))
        {
            return;
        }

        Player player = Player.Current;
        float distance = player != null ? Flat(player.transform.position, waypoint.transform.position) : 0f;
        open.Add(waypoint, new Trip { Label = label, Distance = Mathf.RoundToInt(distance) });
    }

    /// <summary>Played time passes (not paused, the game in front), in the saddle or not.</summary>
    public void Tick(float seconds, bool riding)
    {
        if (open.Count == 0 || seconds <= 0f)
        {
            return;
        }

        foreach (Trip trip in open.Values)
        {
            trip.Seconds += seconds;
            trip.Ridden += riding ? seconds : 0f;
        }
    }

    /// <summary>The old ways or "Go there" taken: counted on every trip under way.</summary>
    public void Jumped()
    {
        foreach (Trip trip in open.Values)
        {
            trip.Jumps++;
        }
    }

    /// <summary>The waypoint is gone: with its objective done, the trip's row is written; otherwise it is dropped.</summary>
    public void End(QuestWaypoint waypoint, QuestDefinition quest, int objective, bool done)
    {
        if (waypoint is null || !open.TryGetValue(waypoint, out Trip trip))
        {
            return;
        }

        open.Remove(waypoint);
        if (done && quest != null)
        {
            PlaytestLogger.Note(PlaytestLog.Trip, $"{quest.Id}#{objective}", Mathf.RoundToInt(trip.Seconds), Detail(trip));
        }
    }

    /// <summary>The trip to <paramref name="waypoint"/> so far: its played seconds and the row's detail. False with none under way.</summary>
    public bool TryRead(QuestWaypoint waypoint, out int seconds, out string detail)
    {
        Trip trip = null;
        bool any = waypoint is object && open.TryGetValue(waypoint, out trip);
        seconds = any ? Mathf.RoundToInt(trip.Seconds) : 0;
        detail = any ? Detail(trip) : null;
        return any;
    }

    /// <summary>The row's detail: what it was, how far off at the start, the share ridden, the jumps taken.</summary>
    public static string Detail(string label, int metres, float seconds, float ridden, int jumps) =>
        $"{label}; {metres} m; rode {(seconds > 0f ? Mathf.RoundToInt(100f * ridden / seconds) : 0)}%; jumps {jumps}";

    private static string Detail(Trip trip) => Detail(trip.Label, trip.Distance, trip.Seconds, trip.Ridden, trip.Jumps);

    private static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;
}
