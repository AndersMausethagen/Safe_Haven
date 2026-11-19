using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Edges in the wild ground (Docs/WorldDesignReview_2026-11-17.md G2): the band of thicket or rock that walls off the wild ground deep
/// between the trails, crossed only at its gaps. Part of RaidLayoutPlanner, in its own file.
/// <para>
/// How: the walkable ground (trails at their width, roads wider, spurs, clearings, pockets, sites with their room, ponds, rivers and the
/// map's margin, so the band only rings deep ground inside the map) is
/// stamped onto a grid of <see cref="BarrierCell"/> metres, a distance transform gives every cell its metres from it, and the line where
/// that distance is Edge From is traced (marching squares) and joined into lines. Each line long enough, in a district whose mood walls
/// it (Mood › Barrier), is cut into runs by gaps every Gap Every metres (at least one on every line), and the points that would stand
/// in a ravine, on a spire or off a valley's floor are left out. As the band only runs where the ground is that far from anything
/// walkable, it never crosses a trail, a clearing or a site, so whatever was joined stays joined; the deep ground beyond is reached at
/// the gaps. From its own stream, after everything else: turning it on moves nothing placed before it.
/// </para>
/// </summary>
public static partial class RaidLayoutPlanner
{
    /// <summary>Metres across a cell of the band's distance grid.</summary>
    public const float BarrierCell = 2f;

    // Chamfer weights of the distance transform, in cells: straight and diagonal.
    private const float ChamferStraight = 1f;
    private const float ChamferDiagonal = 1.41421356f;
    // Runs shorter than this many metres (what is left between two gaps or exclusions) are dropped as scraps.
    private const float ShortestRun = 4f;

    private static void PlaceBarriers(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        RaidBarrierRule rule = rules.Barriers;
        if (rule == null || !AnyMoodWalls(plan))
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Barriers " + attempt));
        int cells = Mathf.Max(2, Mathf.CeilToInt(plan.Size / BarrierCell));
        float[] distance = WalkableDistance(plan, rules, rule, cells);
        foreach (List<Vector2> line in Contours(distance, cells, rule.edgeFrom))
        {
            float length = Length(line);
            if (length < rule.shortest)
            {
                continue;
            }

            // Draws are made for every line, walled or not, so one district's share never moves another's gaps.
            double wallRoll = random.NextDouble();
            float firstGap = Range(random, 0.2f, 0.8f) * Mathf.Min(length, Mathf.Max(1f, rule.gapEvery.x));
            int gapSeed = random.Next();
            RaidMapMood mood = plan.MoodAt(line[line.Count / 2]);
            if (mood == null || wallRoll >= mood.barrier)
            {
                continue;
            }

            CutIntoRuns(plan, rule, line, firstGap, new System.Random(gapSeed));
        }
    }

    private static bool AnyMoodWalls(RaidLayoutPlan plan)
    {
        if (plan.Mood != null && plan.Mood.barrier > 0f)
        {
            return true;
        }

        foreach (RaidLayoutRegion region in plan.Regions)
        {
            if (region.Mood != null && region.Mood.barrier > 0f)
            {
                return true;
            }
        }

        return false;
    }

    // ----- The distance from walkable ground -----

    /// <summary>
    /// Metres from every cell's middle to the nearest walkable ground, by a two-pass chamfer transform over the stamped grid (within about
    /// 8% of the straight-line distance). Row-major, <paramref name="cells"/> a side; walkable cells hold 0.
    /// </summary>
    internal static float[] WalkableDistance(RaidLayoutPlan plan, RaidLayoutRules rules, RaidBarrierRule rule, int cells)
    {
        var distance = new float[cells * cells];
        for (int i = 0; i < distance.Length; i++)
        {
            distance[i] = float.MaxValue;
        }

        float halfWidth = rules.TrailHalfWidth * (plan.Mood != null ? plan.Mood.trails : 1f);
        for (int link = 0; link < plan.Trails.Count; link++)
        {
            StampLine(distance, cells, plan.Trails[link], plan.IsRoad(link) ? halfWidth * rules.RoadWidth : halfWidth);
        }

        foreach (List<Vector2> spur in plan.Spurs)
        {
            StampLine(distance, cells, spur, halfWidth * SpurWidthShare);
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            StampDisc(distance, cells, room.Position, room.Radius);
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            StampDisc(distance, cells, centre, radius);
        }

        foreach (RaidSite site in plan.Sites)
        {
            StampDisc(distance, cells, site.Position * plan.Size, rule.siteRoom);
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            StampWhere(distance, cells, pond.Position, pond.Reach + 1f, point => pond.Edge(point) < 1f);
        }

        foreach (RaidLayoutRiver river in plan.Rivers)
        {
            float widest = 0f;
            foreach (float half in river.HalfWidths)
            {
                widest = Mathf.Max(widest, half);
            }

            StampLine(distance, cells, river.Line, widest + 1f);
        }

        // The map's margin counts as walkable too: the band then only rings deep ground inside the map. Without it, deep ground running
        // out to the edge was traced as a long thin strip between the wall and the rising rim, reached at one far gap or none (seen in a
        // launched Moss Marsh, 2026-11-19).
        int margin = Mathf.Clamp(Mathf.CeilToInt(rules.EdgeMargin / BarrierCell), 1, cells / 2);
        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                if (x < margin || z < margin || x >= cells - margin || z >= cells - margin)
                {
                    distance[z * cells + x] = 0f;
                }
            }
        }

        // Forward then backward, each cell taking the least of its neighbours already passed plus the step.
        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                int i = z * cells + x;
                float best = distance[i];
                if (x > 0) best = Mathf.Min(best, distance[i - 1] + ChamferStraight);
                if (z > 0)
                {
                    best = Mathf.Min(best, distance[i - cells] + ChamferStraight);
                    if (x > 0) best = Mathf.Min(best, distance[i - cells - 1] + ChamferDiagonal);
                    if (x < cells - 1) best = Mathf.Min(best, distance[i - cells + 1] + ChamferDiagonal);
                }

                distance[i] = best;
            }
        }

        for (int z = cells - 1; z >= 0; z--)
        {
            for (int x = cells - 1; x >= 0; x--)
            {
                int i = z * cells + x;
                float best = distance[i];
                if (x < cells - 1) best = Mathf.Min(best, distance[i + 1] + ChamferStraight);
                if (z < cells - 1)
                {
                    best = Mathf.Min(best, distance[i + cells] + ChamferStraight);
                    if (x < cells - 1) best = Mathf.Min(best, distance[i + cells + 1] + ChamferDiagonal);
                    if (x > 0) best = Mathf.Min(best, distance[i + cells - 1] + ChamferDiagonal);
                }

                distance[i] = best;
            }
        }

        // Cells to metres. A map with nothing walkable at all (never generated) stays far everywhere.
        for (int i = 0; i < distance.Length; i++)
        {
            distance[i] = distance[i] >= float.MaxValue * 0.5f ? float.MaxValue : distance[i] * BarrierCell;
        }

        return distance;
    }

    private static Vector2 CellMiddle(int x, int z) => new Vector2((x + 0.5f) * BarrierCell, (z + 0.5f) * BarrierCell);

    private static void StampWhere(float[] distance, int cells, Vector2 centre, float reach, System.Func<Vector2, bool> walkable)
    {
        int x0 = Mathf.Max(0, Mathf.FloorToInt((centre.x - reach) / BarrierCell)), x1 = Mathf.Min(cells - 1, Mathf.CeilToInt((centre.x + reach) / BarrierCell));
        int z0 = Mathf.Max(0, Mathf.FloorToInt((centre.y - reach) / BarrierCell)), z1 = Mathf.Min(cells - 1, Mathf.CeilToInt((centre.y + reach) / BarrierCell));
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (walkable(CellMiddle(x, z)))
                {
                    distance[z * cells + x] = 0f;
                }
            }
        }
    }

    private static void StampDisc(float[] distance, int cells, Vector2 centre, float radius) =>
        StampWhere(distance, cells, centre, radius, point => (point - centre).sqrMagnitude <= radius * radius);

    private static void StampLine(float[] distance, int cells, List<Vector2> line, float halfWidth)
    {
        for (int i = 1; i < line.Count; i++)
        {
            Vector2 a = line[i - 1], b = line[i];
            Vector2 middle = (a + b) * 0.5f;
            float reach = Vector2.Distance(a, b) * 0.5f + halfWidth;
            StampWhere(distance, cells, middle, reach, point => DistanceToSegment(point, a, b) <= halfWidth);
        }
    }

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(point, a + ab * t);
    }

    // ----- Tracing the band -----

    /// <summary>
    /// The lines where the distance grid crosses <paramref name="level"/>, by marching squares over the cells' middles, joined end to end:
    /// closed rings (first point repeated last) and lines that end at the grid's edge. In metres from the map's corner.
    /// </summary>
    internal static List<List<Vector2>> Contours(float[] distance, int cells, float level)
    {
        // Each crossing sits on a grid edge between two cell middles: horizontal edges (x,z)-(x+1,z) are 2i, vertical (x,z)-(x,z+1) 2i+1.
        var points = new Dictionary<int, Vector2>();
        var links = new Dictionary<int, List<int>>();

        float At(int x, int z) => Mathf.Min(distance[z * cells + x], 1e6f);

        int EdgeId(int x, int z, bool vertical) => (z * cells + x) * 2 + (vertical ? 1 : 0);

        Vector2 Crossing(int x0, int z0, int x1, int z1)
        {
            float a = At(x0, z0), b = At(x1, z1);
            float t = Mathf.Abs(b - a) > 1e-6f ? Mathf.Clamp01((level - a) / (b - a)) : 0.5f;
            return Vector2.Lerp(CellMiddle(x0, z0), CellMiddle(x1, z1), t);
        }

        int Edge(int x, int z, int side)
        {
            // Sides of the square with corner (x,z): 0 bottom, 1 right, 2 top, 3 left.
            int id;
            switch (side)
            {
                case 0: id = EdgeId(x, z, false); if (!points.ContainsKey(id)) points[id] = Crossing(x, z, x + 1, z); break;
                case 1: id = EdgeId(x + 1, z, true); if (!points.ContainsKey(id)) points[id] = Crossing(x + 1, z, x + 1, z + 1); break;
                case 2: id = EdgeId(x, z + 1, false); if (!points.ContainsKey(id)) points[id] = Crossing(x, z + 1, x + 1, z + 1); break;
                default: id = EdgeId(x, z, true); if (!points.ContainsKey(id)) points[id] = Crossing(x, z, x, z + 1); break;
            }

            return id;
        }

        void Link(int a, int b)
        {
            if (!links.TryGetValue(a, out List<int> fromA)) links[a] = fromA = new List<int>(2);
            if (!links.TryGetValue(b, out List<int> fromB)) links[b] = fromB = new List<int>(2);
            fromA.Add(b);
            fromB.Add(a);
        }

        for (int z = 0; z < cells - 1; z++)
        {
            for (int x = 0; x < cells - 1; x++)
            {
                // Corners above the level: 1 bottom-left, 2 bottom-right, 4 top-right, 8 top-left.
                int index = (At(x, z) > level ? 1 : 0) | (At(x + 1, z) > level ? 2 : 0) | (At(x + 1, z + 1) > level ? 4 : 0) | (At(x, z + 1) > level ? 8 : 0);
                switch (index)
                {
                    case 0: case 15: break;
                    case 1: case 14: Link(Edge(x, z, 3), Edge(x, z, 0)); break;
                    case 2: case 13: Link(Edge(x, z, 0), Edge(x, z, 1)); break;
                    case 3: case 12: Link(Edge(x, z, 3), Edge(x, z, 1)); break;
                    case 4: case 11: Link(Edge(x, z, 1), Edge(x, z, 2)); break;
                    case 6: case 9: Link(Edge(x, z, 0), Edge(x, z, 2)); break;
                    case 7: case 8: Link(Edge(x, z, 3), Edge(x, z, 2)); break;
                    default:
                        // A saddle (5 or 10): the middle's value says which corners join.
                        float middle = (At(x, z) + At(x + 1, z) + At(x + 1, z + 1) + At(x, z + 1)) * 0.25f;
                        bool joined = (middle > level) == (index == 5);
                        if (joined)
                        {
                            Link(Edge(x, z, 3), Edge(x, z, 2));
                            Link(Edge(x, z, 0), Edge(x, z, 1));
                        }
                        else
                        {
                            Link(Edge(x, z, 3), Edge(x, z, 0));
                            Link(Edge(x, z, 1), Edge(x, z, 2));
                        }

                        break;
                }
            }
        }

        // Join: open lines from their ends first (an edge crossed once), then the rings that are left. In edge-id order, so the same
        // grid always gives the same lines.
        var lines = new List<List<Vector2>>();
        var used = new HashSet<int>();
        var ids = new List<int>(links.Keys);
        ids.Sort();
        foreach (bool ends in new[] { true, false })
        {
            foreach (int id in ids)
            {
                if (used.Contains(id) || (ends && links[id].Count != 1))
                {
                    continue;
                }

                var line = new List<Vector2>();
                int previous = -1, current = id;
                while (current >= 0 && used.Add(current))
                {
                    line.Add(points[current]);
                    int next = -1;
                    foreach (int other in links[current])
                    {
                        if (other != previous && !used.Contains(other))
                        {
                            next = other;
                            break;
                        }
                    }

                    if (next < 0 && !ends)
                    {
                        // A ring: back to its first point.
                        foreach (int other in links[current])
                        {
                            if (other == id && other != previous)
                            {
                                line.Add(points[id]);
                                break;
                            }
                        }
                    }

                    previous = current;
                    current = next;
                }

                if (line.Count >= 2)
                {
                    lines.Add(line);
                }
            }
        }

        return lines;
    }

    // ----- Runs and gaps -----

    // Walks a line of the band: wall, then a gap, then wall, from a first gap partway in (so every line has one), every Gap Every metres;
    // the points that can't carry a wall (in a ravine, on a spire, off a valley's floor) break a run without adding a gap. A ring is turned
    // first so that it starts and ends in the middle of its first gap: its closing point is then always open (before, the first and last
    // runs met there as one wall, and the dressing kept back from both ends as if it were a gap: a hidden wall; review 2026-11-19).
    // A gap whose middle can't carry a wall (on a ravine's lip, say) or lies by a great boulder or a landmark is moved on until it is
    // clear, so a short ring is never left sealed.
    private static void CutIntoRuns(RaidLayoutPlan plan, RaidBarrierRule rule, List<Vector2> line, float firstGap, System.Random random)
    {
        float least = Mathf.Max(5f, rule.gapEvery.x), most = Mathf.Max(least, rule.gapEvery.y);
        float gapLeast = Mathf.Max(2f, rule.gapWidth.x), gapMost = Mathf.Max(gapLeast, rule.gapWidth.y);
        float length = Length(line);
        bool ring = line.Count > 3 && Vector2.Distance(line[0], line[line.Count - 1]) < 0.01f;
        float width = Range(random, gapLeast, gapMost);
        float gapStart, gapEnd, tail = float.MaxValue;
        if (ring)
        {
            float middle = FitGap(plan, line, firstGap + width * 0.5f, width, length);
            line = TurnRing(line, middle);
            gapStart = -width * 0.5f;
            gapEnd = width * 0.5f;
            tail = length - width * 0.5f;
        }
        else
        {
            float middle = FitGap(plan, line, firstGap + width * 0.5f, width, length);
            gapStart = middle - width * 0.5f;
            gapEnd = middle + width * 0.5f;
        }

        RaidLayoutBarrier run = null;
        float along = 0f;
        for (int i = 0; i < line.Count; i++)
        {
            if (i > 0)
            {
                along += Vector2.Distance(line[i - 1], line[i]);
            }

            while (along > gapEnd)
            {
                AddGap(plan, PointAt(line, Mathf.Max(0f, (gapStart + gapEnd) * 0.5f)));
                width = Range(random, gapLeast, gapMost);
                float middle = FitGap(plan, line, gapEnd + Range(random, least, most) + width * 0.5f, width, length);
                gapStart = middle - width * 0.5f;
                gapEnd = middle + width * 0.5f;
            }

            bool inGap = (along >= gapStart && along <= gapEnd) || along >= tail;
            if (inGap || !CanWall(plan, line[i]))
            {
                Finish(plan, run);
                run = null;
                continue;
            }

            run ??= new RaidLayoutBarrier();
            run.Points.Add(line[i]);
        }

        Finish(plan, run);
        // An open line's gap that runs past its end is still a way through.
        if (!ring && gapStart < along && gapEnd > along)
        {
            AddGap(plan, PointAt(line, Mathf.Min(along, (gapStart + gapEnd) * 0.5f)));
        }
    }

    // The middle of a gap at or after <paramref name="middle"/> metres along the line where a wall could have stood (so the gap is a way
    // through, not a ravine's lip): moved on by its width up to a few times. Beyond the line's end it is left where it falls.
    private static float FitGap(RaidLayoutPlan plan, List<Vector2> line, float middle, float width, float length)
    {
        for (int tries = 0; tries < 6 && middle < length; tries++)
        {
            Vector2 point = PointAt(line, middle);
            if (CanWall(plan, point) && GapClear(plan, point))
            {
                return middle;
            }

            middle += width;
        }

        return middle;
    }

    // Metres a gap keeps from a great boulder or a landmark, which are solid and could close it.
    private const float GapFromSolid = 5f;

    private static bool GapClear(RaidLayoutPlan plan, Vector2 point)
    {
        foreach (RaidLayoutBoulder boulder in plan.Boulders)
        {
            if (Vector2.Distance(boulder.Position, point) < GapFromSolid + boulder.Scale)
            {
                return false;
            }
        }

        foreach (RaidLayoutLandmark landmark in plan.Landmarks)
        {
            if (Vector2.Distance(landmark.Position, point) < GapFromSolid + landmark.Scale)
            {
                return false;
            }
        }

        return true;
    }

    // The ring started again at <paramref name="at"/> metres along it (a new first point there), closed on itself.
    private static List<Vector2> TurnRing(List<Vector2> ring, float at)
    {
        Vector2 first = PointAt(ring, at);
        var turned = new List<Vector2> { first };
        float along = 0f;
        int count = ring.Count - 1; // the last point repeats the first
        int startIndex = 0;
        for (int i = 1; i <= count; i++)
        {
            along += Vector2.Distance(ring[i - 1], ring[i]);
            if (along > at)
            {
                startIndex = i;
                break;
            }
        }

        for (int k = 0; k < count; k++)
        {
            turned.Add(ring[(startIndex + k) % count]);
        }

        turned.Add(first);
        return turned;
    }

    // A gap only counts where a wall could have stood: one up a valley's wall or in a ravine is no way through anything.
    private static void AddGap(RaidLayoutPlan plan, Vector2 middle)
    {
        if (CanWall(plan, middle))
        {
            plan.BarrierGaps.Add(middle);
        }
    }

    private static void Finish(RaidLayoutPlan plan, RaidLayoutBarrier run)
    {
        if (run != null && run.Points.Count >= 2 && run.Length >= ShortestRun)
        {
            plan.Barriers.Add(run);
        }
    }

    private static Vector2 PointAt(List<Vector2> line, float distance)
    {
        for (int i = 1; i < line.Count; i++)
        {
            float step = Vector2.Distance(line[i - 1], line[i]);
            if (distance <= step)
            {
                return Vector2.Lerp(line[i - 1], line[i], step > 1e-5f ? distance / step : 0f);
            }

            distance -= step;
        }

        return line[line.Count - 1];
    }

    // Where a wall may stand: inside the map's margin, never in a ravine, on a spire or up a valley's walls (they are edges already).
    private static bool CanWall(RaidLayoutPlan plan, Vector2 point)
    {
        if (point.x < 1f || point.y < 1f || point.x > plan.Size - 1f || point.y > plan.Size - 1f)
        {
            return false;
        }

        if (plan.Valley != null && plan.Valley.FloorEdge(point) > 0f)
        {
            return false;
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(point, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + RaidLayoutRavine.RagReach + 1f)
            {
                return false;
            }
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if (spire.Edge(point) < 1.5f)
            {
                return false;
            }
        }

        return true;
    }
}
