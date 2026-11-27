using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Stands the wild ground's band (Docs/WorldDesignReview_2026-11-17.md G2, RaidLayoutPlan.Barriers) as solid walls: a chain of tall box
/// colliders along every walled run, so neither the player nor a creature (the navmesh is built round them) crosses it but at its gaps.
/// The colliders draw nothing; what the band looks like grows on it as terrain trees (RaidMapSurface's Barrier Dressing). Under their
/// own holder, made anew each attempt at the map, which ClearBlockedTrails can clear as it clears boulders. Part of RaidMapGenerator,
/// in its own file.
/// </summary>
public static partial class RaidMapGenerator
{
    public const string BarriersName = "Barriers";

    /// <summary>Metres a piece of the wall spans at most along its run.</summary>
    public const float BarrierPiece = 4f;
    /// <summary>Metres across a piece of the wall.</summary>
    public const float BarrierThickness = 1.2f;
    /// <summary>Metres a piece rises above the higher of its two ends (and goes below the lower), so nothing steps over or under it.</summary>
    public const float BarrierRise = 3.5f;
    private const float BarrierBelow = 1f;
    // Metres each piece runs past its ends, so a bend in the run leaves no slit between two pieces.
    private const float BarrierOverlap = 0.35f;

    /// <summary>Metres a windthrow log is sunk into the ground below where its line meets it.</summary>
    private const float LogSink = 0.08f;
    public const string LogPrefix = "Log ";

    /// <summary>
    /// Stands the plan's walled runs as colliders, and lays the band's windthrow <paramref name="logs"/> (RaidMapSurface.Scatter's) along
    /// them as objects, each tilted to the ground under it. Nothing (no holder) when the plan has none.
    /// </summary>
    public static void PlaceBarriers(RaidMap map, RaidLayoutPlan plan, IReadOnlyList<RaidMapSurface.LogObject> logs = null)
    {
        RemoveOld(map.transform, BarriersName);

        if (plan.Barriers.Count == 0)
        {
            return;
        }

        var holder = new GameObject(BarriersName).transform;
        holder.SetParent(map.transform, false);
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        int pieces = 0;
        foreach (RaidLayoutBarrier run in plan.Barriers)
        {
            // Pieces of up to BarrierPiece metres: from a point, as far along the run as stays within that.
            int from = 0;
            while (from < run.Points.Count - 1)
            {
                int to = from + 1;
                while (to < run.Points.Count - 1 && Vector2.Distance(run.Points[from], run.Points[to + 1]) <= BarrierPiece)
                {
                    to++;
                }

                Vector3 a = RaidMapSurface.Ground(terrain, origin + new Vector3(run.Points[from].x, 0f, run.Points[from].y));
                Vector3 b = RaidMapSurface.Ground(terrain, origin + new Vector3(run.Points[to].x, 0f, run.Points[to].y));
                Piece(holder, a, b, pieces++);
                from = to;
            }
        }

        if (logs == null)
        {
            return;
        }

        int laid = 0;
        foreach (RaidMapSurface.LogObject log in logs)
        {
            if (log.Prefab != null)
            {
                LayLog(holder, terrain, origin, log, laid++);
            }
        }
    }

    // A log along its axis from one end's ground to the other's, rolled to the slope across it (a crest under it shows through, within
    // RaidMapSurface's LogCrest, rather than lifting its ends off the ground).
    private static void LayLog(Transform holder, Terrain terrain, Vector3 origin, RaidMapSurface.LogObject log, int index)
    {
        Vector3 centre = RaidMapSurface.Ground(terrain, origin + new Vector3(log.Position.x, 0f, log.Position.y));
        Vector3 a = RaidMapSurface.Ground(terrain, origin + new Vector3(log.Position.x - log.Axis.x * log.Half, 0f, log.Position.y - log.Axis.y * log.Half));
        Vector3 b = RaidMapSurface.Ground(terrain, origin + new Vector3(log.Position.x + log.Axis.x * log.Half, 0f, log.Position.y + log.Axis.y * log.Half));
        Vector3 along = (b - a).normalized;
        TerrainData data = terrain.terrainData;
        Vector3 normal = data.GetInterpolatedNormal((centre.x - origin.x) / data.size.x, (centre.z - origin.z) / data.size.z);
        Vector3 up = Vector3.ProjectOnPlane(normal, along);
        if (up.sqrMagnitude < 1e-4f)
        {
            up = Vector3.up;
        }

        // LookRotation's right is up x forward, so this forward puts the prefab's X along the log.
        Quaternion turn = Quaternion.LookRotation(Vector3.Cross(along, up.normalized), up.normalized);
        Vector3 at = (a + b) * 0.5f;
        at.y -= LogSink;
        GameObject made = Object.Instantiate(log.Prefab, at, turn, holder);
        made.transform.localScale = log.Prefab.transform.localScale * log.Scale;
        made.name = LogPrefix + index + " " + log.Prefab.name;
    }

    // Opening walled pockets: every PocketStep metres along each run, a spot either side at each of PocketOffsets metres off it is tried;
    // unreached spots along one run within PocketMerge metres of each other make one stretch, which needs PocketLeast of them (a lone
    // one is a hole between two pieces of dressing, not ground the wall cut off). At most PocketMost openings a map.
    private const float PocketStep = 8f;
    private static readonly float[] PocketOffsets = { 5f, 11f };
    private const float PocketMerge = 16f;
    private const int PocketLeast = 2;
    private const int PocketMost = 8;
    /// <summary>Metres either side of an opening's middle that its wall and dressing are taken away.</summary>
    public const float OpeningHalf = 4f;

    /// <summary>
    /// After the navmesh is built: the walls can cut off ground that was only reached through the deep ground they ring (a strip between a
    /// ring and the map's rim or a steep slope; seen in a launched Misty Woods, 2026-11-19: 6% of the map). Tries spots beside every run;
    /// where a stretch of a run has two or more on the navmesh that can't be walked to from the nearest clearing, opens the run in that
    /// stretch's middle (its colliders off, its dressing taken away) and records the opening as a gap. A few hundred path queries at most,
    /// as only spots by the walls are tried. Returns how many it opened; the caller rebuilds the trunks and the navmesh when it opened any.
    /// </summary>
    public static int OpenWalledPockets(RaidMap map, RaidLayoutPlan plan)
    {
        if (plan.Barriers.Count == 0)
        {
            return 0;
        }

        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        // From the nearest clearing, not the start: clearings stand on the trails, which the walls never touch, and one long search over
        // a large map runs out of nodes and reports a partial path where the way is open (as ExitsReachable found).
        var rooms = new List<(Vector2 at, Vector3 onMesh)>();
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            Vector3 at = RaidMapSurface.Ground(terrain, origin + new Vector3(room.Position.x, 0f, room.Position.y));
            if (NavMesh.SamplePosition(at, out NavMeshHit onMesh, ReachSample, NavMesh.AllAreas))
            {
                rooms.Add((room.Position, onMesh.position));
            }
        }

        if (rooms.Count == 0)
        {
            return 0;
        }

        var path = new NavMeshPath();
        bool Unreached(Vector2 spot)
        {
            Vector3 at = RaidMapSurface.Ground(terrain, origin + new Vector3(spot.x, 0f, spot.y));
            if (!NavMesh.SamplePosition(at, out NavMeshHit hit, 1f, NavMesh.AllAreas))
            {
                return false;
            }

            Vector3 from = rooms[0].onMesh;
            float nearest = float.MaxValue;
            foreach ((Vector2 roomAt, Vector3 onMesh) in rooms)
            {
                float d = (roomAt - spot).sqrMagnitude;
                if (d < nearest)
                {
                    nearest = d;
                    from = onMesh;
                }
            }

            return !NavMesh.CalculatePath(from, hit.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete;
        }

        var openings = new List<Vector2>();
        foreach (RaidLayoutBarrier run in plan.Barriers)
        {
            float stretchStart = -1f, stretchEnd = -1f;
            int samples = 0;
            float along = 0f;
            for (int i = 1; i < run.Points.Count && openings.Count < PocketMost; i++)
            {
                Vector2 a = run.Points[i - 1], b = run.Points[i];
                float step = Vector2.Distance(a, b);
                if (step < 1e-4f)
                {
                    continue;
                }

                Vector2 across = Vector2.Perpendicular((b - a) / step);
                for (float at = Mathf.Ceil(along / PocketStep) * PocketStep - along; at < step; at += PocketStep)
                {
                    Vector2 point = a + (b - a) * (at / step);
                    bool unreached = false;
                    foreach (float offset in PocketOffsets)
                    {
                        unreached |= Unreached(point + across * offset) || Unreached(point - across * offset);
                    }

                    float here = along + at;
                    if (!unreached)
                    {
                        continue;
                    }

                    if (stretchStart >= 0f && here - stretchEnd > PocketMerge)
                    {
                        Open(run, stretchStart, stretchEnd, samples, openings);
                        stretchStart = -1f;
                        samples = 0;
                    }

                    if (stretchStart < 0f)
                    {
                        stretchStart = here;
                    }

                    stretchEnd = here;
                    samples++;
                }

                along += step;
            }

            if (stretchStart >= 0f && openings.Count < PocketMost)
            {
                Open(run, stretchStart, stretchEnd, samples, openings);
            }
        }

        Transform holder = map.transform.Find(BarriersName);
        foreach (Vector2 wall in openings)
        {
            plan.BarrierGaps.Add(wall);
            RaidMapSurface.ClearBarrierDressing(terrain.terrainData, map.Definition, wall, OpeningHalf);
            if (holder == null)
            {
                continue;
            }

            foreach (Transform piece in holder)
            {
                var flat = new Vector2(piece.position.x - origin.x, piece.position.z - origin.z);
                if (piece.name.StartsWith(LogPrefix))
                {
                    // A log reaching into the opening goes (its whole length, as it may lie along the run).
                    if (piece.gameObject.activeSelf && LogReaches(piece, origin, wall, OpeningHalf))
                    {
                        piece.gameObject.SetActive(false);
                        Object.Destroy(piece.gameObject);
                    }

                    continue;
                }

                if (Vector2.Distance(flat, wall) < OpeningHalf + BarrierPiece * 0.5f + BarrierOverlap && piece.TryGetComponent(out Collider collider))
                {
                    collider.enabled = false;
                }
            }
        }

        return openings.Count;
    }

    // Whether a laid log comes within radius metres of a spot: its drawn extent along its own X, as a segment, against the spot.
    private static bool LogReaches(Transform log, Vector3 origin, Vector2 spot, float radius)
    {
        Bounds bounds = RaidMapSurface.PrefabBounds(log.gameObject);
        float half = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)) * log.lossyScale.x;
        float width = Mathf.Max(Mathf.Abs(bounds.min.z), Mathf.Abs(bounds.max.z)) * log.lossyScale.x;
        var centre = new Vector2(log.position.x - origin.x, log.position.z - origin.z);
        var axis = new Vector2(log.right.x, log.right.z);
        axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector2.right;
        float t = Mathf.Clamp(Vector2.Dot(spot - centre, axis), -half, half);
        return Vector2.Distance(spot, centre + axis * t) < radius + width;
    }

    // One opening in the middle of a stretch of a run that had enough unreached spots beside it.
    private static void Open(RaidLayoutBarrier run, float from, float to, int samples, List<Vector2> openings)
    {
        if (samples < PocketLeast)
        {
            return;
        }

        float middle = (from + to) * 0.5f;
        for (int i = 1; i < run.Points.Count; i++)
        {
            float step = Vector2.Distance(run.Points[i - 1], run.Points[i]);
            if (middle <= step)
            {
                openings.Add(Vector2.Lerp(run.Points[i - 1], run.Points[i], step > 1e-5f ? middle / step : 0f));
                return;
            }

            middle -= step;
        }

        openings.Add(run.Points[run.Points.Count - 1]);
    }

    private static void Piece(Transform holder, Vector3 a, Vector3 b, int index)
    {
        Vector3 flat = new Vector3(b.x - a.x, 0f, b.z - a.z);
        float length = flat.magnitude;
        if (length < 0.05f)
        {
            return;
        }

        float bottom = Mathf.Min(a.y, b.y) - BarrierBelow;
        float top = Mathf.Max(a.y, b.y) + BarrierRise;
        var piece = new GameObject("Wall " + index);
        piece.transform.SetParent(holder, false);
        piece.transform.SetPositionAndRotation(new Vector3((a.x + b.x) * 0.5f, (bottom + top) * 0.5f, (a.z + b.z) * 0.5f),
            Quaternion.LookRotation(flat / length, Vector3.up));
        var box = piece.AddComponent<BoxCollider>();
        box.size = new Vector3(BarrierThickness, top - bottom, length + BarrierOverlap * 2f);
    }
}
