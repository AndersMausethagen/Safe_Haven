using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Follow a trail to a hidden place (Docs/RaidEventsPlan_2026-11-26.md E3 Track): signs appear one by one along the way (a snare, a
/// print, a broken branch), each as the player reaches the one before, and an animal may walk on ahead (TrackLeader: the White Hart).
/// The place lies 60-120 m off, out of sight; reaching it carries out On Found there. Made by TrackOutcome on an object of its own.
/// </summary>
public sealed class TrackTask : RaidEventTask
{
    // How close the player must come to a sign for the next to show, and to the end for it to count.
    public const float ReachWithin = 9f;
    public const float FoundWithin = 4f;
    // Signs are left this long after the trail ends, then cleared.
    private const float SignsLinger = 40f;

    private RaidEventContext context;
    private string label;
    private List<RaidEventOutcome> onFound;
    private GameObject endPrefab;
    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<GameObject> signs = new List<GameObject>();
    private TrackLeader leader;
    private GameObject leaderObject;
    private int next;

    public bool IsDone { get; private set; }

    /// <summary>The trail's points, the last being the hidden place. For tests.</summary>
    public IReadOnlyList<Vector3> Points => points;

    /// <summary>Which point is to be reached next.</summary>
    public int Next => next;

    public override string HudLine
    {
        get
        {
            if (points.Count == 0)
            {
                return "";
            }

            Vector3 from = context?.Player != null ? context.Player.transform.position : transform.position;
            int metres = Mathf.RoundToInt(FlatDistance(points[Mathf.Min(next, points.Count - 1)], from));
            return $"<color={UIStyle.Current.AccentHex}>{label}</color>: {metres} m";
        }
    }

    /// <summary>
    /// Lays the trail from the site to a place between the two distances off, puts the first sign out and sets the leader going.
    /// False when no place was found (the trail goes cold): the caller pays at the site instead.
    /// </summary>
    public bool Begin(RaidEventContext eventContext, string what, Vector2 distance, float signEvery, GameObject sign, GameObject leaderPrefab,
        GameObject endAt, List<RaidEventOutcome> found)
    {
        context = eventContext;
        label = what;
        onFound = found ?? new List<RaidEventOutcome>();
        endPrefab = endAt;
        var random = context?.Random ?? new System.Random();
        var path = new NavMeshPath();
        Vector3 start = transform.position;
        if (!RaidSpots.TryFindSpot(start, distance.x, distance.y, random, path, out Vector3 end, 24)
            || !NavMesh.SamplePosition(start, out NavMeshHit from, 4f, NavMesh.AllAreas)
            || !NavMesh.CalculatePath(from.position, end, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        // Between the path's corners the line runs straight through hills: each sign is put back on the ground.
        foreach (Vector3 point in Spaced(path.corners, signEvery))
        {
            points.Add(NavMesh.SamplePosition(point, out NavMeshHit onGround, 3f, NavMesh.AllAreas) ? onGround.position : RaidSpots.Grounded(point));
        }

        foreach (Vector3 point in points)
        {
            GameObject made = sign != null ? Instantiate(sign, point, Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f)) : null;
            if (made != null)
            {
                made.SetActive(false);
            }

            signs.Add(made);
        }

        if (leaderPrefab != null)
        {
            leaderObject = Instantiate(leaderPrefab, from.position, Quaternion.identity);
            leader = leaderObject.GetComponent<TrackLeader>();
        }

        Show(0);
        return true;
    }

#if UNITY_EDITOR
    /// <summary>A trail of these points with no NavMesh, signs or leader (tests).</summary>
    public void LayForTests(IEnumerable<Vector3> trail, List<RaidEventOutcome> found)
    {
        points.Clear();
        points.AddRange(trail);
        onFound = found ?? new List<RaidEventOutcome>();
        label = "Trail";
        next = 0;
        context = new RaidEventContext();
    }
#endif

    /// <summary>
    /// Points along a path every <paramref name="spacing"/> metres, ending on its last corner (the hidden place). Pure, for tests.
    /// </summary>
    public static List<Vector3> Spaced(IReadOnlyList<Vector3> corners, float spacing)
    {
        var result = new List<Vector3>();
        if (corners == null || corners.Count == 0)
        {
            return result;
        }

        spacing = Mathf.Max(1f, spacing);
        float carried = 0f;
        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 a = corners[i - 1];
            Vector3 b = corners[i];
            float length = Vector3.Distance(a, b);
            float at = spacing - carried;
            while (at <= length)
            {
                result.Add(Vector3.Lerp(a, b, at / length));
                at += spacing;
            }

            carried = length - (at - spacing);
        }

        Vector3 last = corners[corners.Count - 1];
        // The end is a sign of its own, unless the last one already stands nearly on it.
        if (result.Count == 0 || Vector3.Distance(result[result.Count - 1], last) > spacing * 0.4f)
        {
            result.Add(last);
        }
        else
        {
            result[result.Count - 1] = last;
        }

        return result;
    }

    // Shows sign i and sends the leader on to it.
    private void Show(int index)
    {
        next = index;
        if (index < signs.Count && signs[index] != null)
        {
            signs[index].SetActive(true);
        }

        if (leader != null && index < points.Count && context?.Player != null)
        {
            leader.GoTo(points[index], context.Player.transform.position);
        }
    }

    /// <summary>The player stands at <paramref name="player"/>: the next sign, or the end. Public for tests.</summary>
    public void Step(Vector3 player)
    {
        if (IsDone || points.Count == 0)
        {
            return;
        }

        // The farthest sign along that the player stands at (one who cuts a corner, or walks straight to the place, is credited).
        int reached = -1;
        for (int i = points.Count - 1; i >= next; i--)
        {
            bool atEnd = i == points.Count - 1;
            if (FlatDistance(points[i], player) <= (atEnd ? FoundWithin : ReachWithin))
            {
                reached = i;
                break;
            }
        }

        if (reached < 0)
        {
            return;
        }

        if (reached < points.Count - 1)
        {
            Show(reached + 1);
            return;
        }

        IsDone = true;
        Vector3 end = points[points.Count - 1];
        if (endPrefab != null)
        {
            Instantiate(endPrefab, end, Quaternion.identity);
        }

        if (leaderObject != null)
        {
            GameMessages.Post("It stands a moment, looks back at you, and is gone between the trees.", MessageKind.Info);
            Destroy(leaderObject, 0.5f);
        }

        Apply(onFound, context, end);
        context?.Session?.RecordEvent($"{label}: found");
        Finish();
    }

    private void Update()
    {
        if (IsDone || context?.Player == null)
        {
            return;
        }

        if (context.Session != null && context.Session.IsOver)
        {
            Finish();
            return;
        }

        Step(context.Player.transform.position);
    }

    private void Finish()
    {
        IsDone = true;
        foreach (GameObject sign in signs)
        {
            if (sign != null)
            {
                if (Application.isPlaying) Destroy(sign, SignsLinger);
                else DestroyImmediate(sign);
            }
        }

        if (Application.isPlaying)
        {
            if (leaderObject != null && context?.Session != null && context.Session.IsOver)
            {
                Destroy(leaderObject);
            }

            Destroy(gameObject);
        }
        else
        {
            enabled = false;
        }
    }
}
