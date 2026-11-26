using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Finding places in a running raid: on the ground, walkable, out of the water, out of the player's sight and with a way to the
/// player. Shared by what appears during a raid (RoamingSpawner, RaidEventDirector, SpawnCreaturesOutcome).
/// </summary>
public static class RaidSpots
{
    private const float GroundProbeHeight = 60f;
    private const float NavMeshSnap = 4f;

    /// <summary>Metres of water over the ground that still count as dry enough to put something there: a puddle, a fen's wet edge.</summary>
    public const float DeepestWading = 0.3f;

    // The water is looked for from this far above the ground, and as far again above that: a surface only counts what lies within its
    // own Depth below it (2 m), and a fen's pools and the valley's river run deeper. On a bridge's deck the water lies below: dry.
    private const float WaterProbeLift = 2f;

    /// <summary>
    /// False when the ground at <paramref name="point"/> lies under more than <see cref="DeepestWading"/> of open water. The Moss
    /// Marsh's water is one sheet with no collider and the valley's river has none either: the NavMesh runs on the sunken ground
    /// beneath them, so sites and creatures were put in the pools (review 2026-11-17, R5). Cheap (a lookup over the water surfaces):
    /// for spawning, not for every frame. Frozen water is ground.
    /// </summary>
    public static bool IsDryEnough(Vector3 point)
    {
        return !WaterSurface.TryFind(point + Vector3.up * WaterProbeLift, WaterProbeLift * 2f, out float surface) || surface - point.y <= DeepestWading;
    }

    private static readonly RaycastHit[] SightHits = new RaycastHit[16];

    /// <summary>In front of the camera, inside its view and with nothing solid in between.</summary>
    public static bool IsVisible(Vector3 point) => IsVisible(point, null);

    /// <summary>
    /// The same for something standing at <paramref name="point"/>: its own colliders (under <paramref name="self"/>) don't hide
    /// it. A troll's capsule reaches past chest height, so the plain check always found it hidden by its own body, and roamers
    /// were taken away in plain sight.
    /// </summary>
    public static bool IsVisible(Vector3 point, Transform self)
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return false;
        }

        Vector3 chest = point + Vector3.up * 1.2f;
        Vector3 viewport = view.WorldToViewportPoint(chest);
        if (viewport.z <= 0f || viewport.x < -0.1f || viewport.x > 1.1f || viewport.y < -0.1f || viewport.y > 1.1f)
        {
            return false;
        }

        Vector3 from = view.transform.position;
        if (self == null)
        {
            return !Physics.Linecast(from, chest, ~0, QueryTriggerInteraction.Ignore);
        }

        Vector3 way = chest - from;
        int count = Physics.RaycastNonAlloc(from, way.normalized, SightHits, way.magnitude, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!SightHits[i].transform.IsChildOf(self))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Points are picked flat at the player's height; this drops one onto the ground below or above it so the
    /// NavMesh lookup works on hilly terrain.</summary>
    public static Vector3 Grounded(Vector3 point)
    {
        return Physics.Raycast(point + Vector3.up * GroundProbeHeight, Vector3.down, out RaycastHit ground, GroundProbeHeight * 2f, ~0, QueryTriggerInteraction.Ignore)
            ? ground.point
            : point;
    }

    /// <summary>
    /// The map's event rooms (Docs/RaidLayoutAnalysis_2026-11-26.md R3) between the two distances from <paramref name="from"/>, flat,
    /// and not yet taken: indices into <paramref name="places"/>, nearest first. Pure, for tests.
    /// </summary>
    public static System.Collections.Generic.List<int> RoomsWithin(System.Collections.Generic.IReadOnlyList<RaidPlace> places, Vector3 from,
        float minDistance, float maxDistance, System.Collections.Generic.ICollection<int> taken)
    {
        var rooms = new System.Collections.Generic.List<int>();
        for (int i = 0; i < places.Count; i++)
        {
            Vector3 flat = places[i].Position - from;
            flat.y = 0f;
            float distance = flat.magnitude;
            if (places[i].Kind == RaidSiteKind.EventSite && !taken.Contains(i) && distance >= minDistance && distance <= maxDistance)
            {
                rooms.Add(i);
            }
        }

        rooms.Sort((a, b) => (places[a].Position - from).sqrMagnitude.CompareTo((places[b].Position - from).sqrMagnitude));
        return rooms;
    }

    /// <summary>
    /// An event room of the generated map for an event to stand in: one of <see cref="RoomsWithin"/> picked at random, out of sight, dry
    /// and with a complete path back to <paramref name="from"/>. False when none will do (a painted map has none), and the caller looks
    /// for any spot instead.
    /// </summary>
    public static bool TryTakeRoom(System.Collections.Generic.IReadOnlyList<RaidPlace> places, Vector3 from, float minDistance, float maxDistance,
        System.Collections.Generic.ICollection<int> taken, System.Random random, NavMeshPath path, out Vector3 spot)
    {
        spot = from;
        if (places == null || places.Count == 0 || !NavMesh.SamplePosition(from, out NavMeshHit start, NavMeshSnap, NavMesh.AllAreas))
        {
            return false;
        }

        System.Collections.Generic.List<int> rooms = RoomsWithin(places, from, minDistance, maxDistance, taken);
        while (rooms.Count > 0)
        {
            int pick = random.Next(rooms.Count);
            int index = rooms[pick];
            rooms.RemoveAt(pick);
            if (!NavMesh.SamplePosition(Grounded(places[index].Position), out NavMeshHit hit, NavMeshSnap, NavMesh.AllAreas)
                || !IsDryEnough(hit.position) || IsVisible(hit.position)
                || !NavMesh.CalculatePath(hit.position, start.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            taken.Add(index);
            spot = hit.position;
            return true;
        }

        return false;
    }

    /// <summary>
    /// A walkable spot between the two distances from <paramref name="from"/>, out of the water (<see cref="IsDryEnough"/>), out of
    /// sight and with a complete path back to it. False when none turned up in <paramref name="attempts"/> tries.
    /// </summary>
    public static bool TryFindSpot(Vector3 from, float minDistance, float maxDistance, System.Random random, NavMeshPath path, out Vector3 spot, int attempts = 16)
    {
        spot = from;
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, NavMeshSnap, NavMesh.AllAreas))
        {
            return false;
        }

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(minDistance, maxDistance, (float)random.NextDouble());
            Vector3 candidate = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (!NavMesh.SamplePosition(Grounded(candidate), out NavMeshHit hit, NavMeshSnap, NavMesh.AllAreas))
            {
                continue;
            }

            Vector3 flat = hit.position - from;
            flat.y = 0f;
            if (flat.magnitude < minDistance || !IsDryEnough(hit.position) || IsVisible(hit.position)
                || !NavMesh.CalculatePath(hit.position, start.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            spot = hit.position;
            return true;
        }

        return false;
    }

    // How far from a shore spot the water is looked for, and in how many directions.
    private static readonly float[] ShoreReach = { 3f, 6.5f };
    private const int ShoreDirections = 8;

    /// <summary>
    /// Like <see cref="TryFindSpot"/>, but on a shore: dry where it stands, with open water a few steps off (a ferryman's raft, a wisp
    /// over the pools). <paramref name="toWater"/> is the flat direction to the nearest water, for the site to face. More tries: shores
    /// are rarer than ground.
    /// </summary>
    public static bool TryFindShore(Vector3 from, float minDistance, float maxDistance, System.Random random, NavMeshPath path, out Vector3 spot,
        out Vector3 toWater, int attempts = 64)
    {
        spot = from;
        toWater = Vector3.forward;
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, NavMeshSnap, NavMesh.AllAreas))
        {
            return false;
        }

        // The cheap tests first (on the NavMesh, dry, water near), the dear ones (out of sight, a path back) only for a shore.
        for (int attempt = 0; attempt < attempts; attempt++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(minDistance, maxDistance, (float)random.NextDouble());
            Vector3 candidate = from + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            if (!NavMesh.SamplePosition(Grounded(candidate), out NavMeshHit hit, NavMeshSnap, NavMesh.AllAreas) || !IsDryEnough(hit.position)
                || !WaterNear(hit.position, out Vector3 way) || IsVisible(hit.position)
                || !NavMesh.CalculatePath(hit.position, start.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            spot = hit.position;
            toWater = way;
            return true;
        }

        return false;
    }

    /// <summary>True when open water lies within a few metres of <paramref name="point"/>; <paramref name="direction"/> points to the nearest.</summary>
    public static bool WaterNear(Vector3 point, out Vector3 direction)
    {
        direction = Vector3.forward;
        foreach (float reach in ShoreReach)
        {
            for (int i = 0; i < ShoreDirections; i++)
            {
                float angle = i * Mathf.PI * 2f / ShoreDirections;
                var way = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (!IsDryEnough(Grounded(point + way * reach)))
                {
                    direction = way;
                    return true;
                }
            }
        }

        return false;
    }
}
