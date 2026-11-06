using System.Collections.Generic;
using UnityEngine;

/// <summary>A region of a large generated map: the land nearest its centre has its mood (forest, ridges, cliffs, ravines, hills).</summary>
public sealed class RaidLayoutRegion
{
    public Vector2 Centre;
    public RaidMapMood Mood;
    /// <summary>How far its land reaches against the others' (distances are divided by it): the start's region reaches furthest.</summary>
    public float Reach = 1f;
}

/// <summary>A river across the open land of a generated map, in metres from the map's south-west corner: deep enough to swim, with fords
/// where trails cross it. Its surface only falls along the line, so it runs from the first point to the last.</summary>
public sealed class RaidLayoutRiver
{
    public readonly List<Vector2> Line = new List<Vector2>();
    /// <summary>Half the width of the water at each point.</summary>
    public readonly List<float> HalfWidths = new List<float>();
    /// <summary>How deep it is in its middle, away from fords.</summary>
    public float Depth;
    /// <summary>The height of its surface at each point, above the map's lowest point. RaidLayoutField works it out.</summary>
    public float[] Level = System.Array.Empty<float>();

    public void Nearest(Vector2 point, out int segment, out float along, out float distance)
    {
        segment = 0;
        along = 0f;
        float best = float.MaxValue;
        for (int i = 0; i + 1 < Line.Count; i++)
        {
            Vector2 step = Line[i + 1] - Line[i];
            float t = Mathf.Clamp01(Vector2.Dot(point - Line[i], step) / Mathf.Max(0.0001f, step.sqrMagnitude));
            float squared = (Line[i] + step * t - point).sqrMagnitude;
            if (squared < best)
            {
                best = squared;
                segment = i;
                along = t;
            }
        }

        distance = Mathf.Sqrt(best);
    }
}

/// <summary>A landmark on a generated map: something tall to find the way by.</summary>
public sealed class RaidLayoutLandmark
{
    public Vector2 Position;
    public float Scale;
    public float Turn;
    /// <summary>0-1: which of the rules' landmarks, by weight.</summary>
    public float Pick;
}

/// <summary>
/// What makes runs differ beyond their shape and mood: regions of their own mood on large maps, two ways to the exit, content placed by
/// height, a way down into a ravine, rivers across the open land and landmarks. Each draws from its own random stream, so turning one on
/// or off moves nothing else.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float StartRegionReach = 1.4f;
    private const float RegionFromEdge = 0.2f;
    private const int RegionTries = 40;
    private const float RoutePenalty = 4f;
    private const float RouteShared = 0.6f;
    private const float BypassReach = 1.5f;
    private const float DescentSlope = 2.6f;
    private const float DescentFromTrail = 45f;
    private const float DescentEntry = 3f;
    private const float DescentAfterRamp = 10f;
    private const float DescentSiteGap = 12f;
    private const float DescentShortest = 80f;
    private const float RiverStep = 5f;
    private const float RiverTurn = 7f;
    private const int RiverTries = 60;
    private const float RiverCrossing = 0.57f;
    private const float RiverFromRoom = 3f;
    private const float RiverFromSite = 5f;
    private const float RiverFromRavine = 6f;
    private const float RiverFromRiver = 25f;
    private const float RiverBank = 3f;
    private const float LandmarkFromTrail = 9f;
    private const float LandmarkFromRoom = 8f;
    private const float LandmarkFromOther = 30f;
    private const int LandmarkTries = 60;

    // ----- Regions -----

    /// <summary>
    /// The moods of a run's regions, the run's own first: fixed by the seed, so the briefing can name them cheaply. A map of the normal size
    /// (or rules without regions) has one.
    /// </summary>
    public static List<RaidMapMood> RegionMoods(RaidLayoutRules rules, int seed) => RegionMoods(rules, seed, rules.Regions);

    /// <summary>The moods of a run's regions for this many regions, least and most (a briefing asks with the large map's range).</summary>
    public static List<RaidMapMood> RegionMoods(RaidLayoutRules rules, int seed, Vector2Int range)
    {
        (RaidShapeRule _, RaidMapMood first) = Choose(rules, seed);
        var moods = new List<RaidMapMood> { first };
        var random = new System.Random(RaidLayout.SeedFor(seed, "Region Moods"));
        int count = random.Next(Mathf.Max(1, range.x), Mathf.Max(1, range.y) + 1);
        while (moods.Count < count)
        {
            var weights = new List<float>();
            foreach (RaidMapMood mood in rules.Moods)
            {
                weights.Add(moods.Exists(one => one.name == mood.name) ? 0f : mood.weight);
            }

            int pick = RaidLayout.PickWeighted(random, weights);
            if (pick < 0)
            {
                break;
            }

            moods.Add(rules.Moods[pick]);
        }

        return moods;
    }

    // The start's region around the start, the others as far from it and from each other as the map allows.
    private static void PlaceRegions(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        List<RaidMapMood> moods = RegionMoods(rules, plan.Seed);
        // The moods come from the same list (copies, on a larger map); the run's own stays the plan's Mood.
        moods[0] = plan.Mood;
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Regions " + attempt));
        var centre = new Vector2(plan.Size * 0.5f, plan.Size * 0.5f);
        plan.Regions.Add(new RaidLayoutRegion { Centre = Vector2.Lerp(plan.Start.Position, centre, 0.3f), Mood = moods[0], Reach = StartRegionReach });
        float margin = plan.Size * RegionFromEdge;
        for (int i = 1; i < moods.Count; i++)
        {
            Vector2 best = centre;
            float bestDistance = -1f;
            for (int tries = 0; tries < RegionTries; tries++)
            {
                var spot = new Vector2(Range(random, margin, plan.Size - margin), Range(random, margin, plan.Size - margin));
                float nearest = float.MaxValue;
                foreach (RaidLayoutRegion region in plan.Regions)
                {
                    nearest = Mathf.Min(nearest, Vector2.Distance(region.Centre, spot));
                }

                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = spot;
                }
            }

            plan.Regions.Add(new RaidLayoutRegion { Centre = best, Mood = moods[i] });
        }

        plan.RegionBlend = rules.RegionBlend;
    }

    // ----- Routes -----

    // The exit straight ahead gets two ways: the shortest and, avoiding it as far as it can, the next. If the trails give only one, a new
    // trail joins two clearings that hang off the way at least two clearings apart, which makes a loop around part of it.
    private static void EnsureRoutes(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        int exit = 1 + rules.ExitRooms / 2;
        if (exit >= plan.Rooms.Count || plan.Rooms[exit].Role != RaidRoomRole.Exit)
        {
            return;
        }

        plan.MainExit = exit;
        List<int> shortest = ShortestPath(plan, 0, exit, null);
        if (shortest == null)
        {
            return;
        }

        List<int> other = AlternativePath(plan, exit, shortest);
        if (rules.TwoRoutes && other == null)
        {
            var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Routes " + attempt));
            if (AddBypass(plan, rules, shortest, random))
            {
                shortest = ShortestPath(plan, 0, exit, null);
                other = shortest != null ? AlternativePath(plan, exit, shortest) : null;
            }
        }

        if (shortest != null)
        {
            plan.ShortRoute.UnionWith(shortest);
        }

        if (other != null)
        {
            plan.LongRoute.UnionWith(other);
        }
    }

    private static List<int> AlternativePath(RaidLayoutPlan plan, int exit, List<int> shortest)
    {
        var avoid = new HashSet<int>(shortest);
        List<int> other = ShortestPath(plan, 0, exit, avoid);
        if (other == null)
        {
            return null;
        }

        int shared = 0;
        foreach (int link in other)
        {
            shared += avoid.Contains(link) ? 1 : 0;
        }

        return shared <= other.Count * RouteShared && shared < other.Count ? other : null;
    }

    /// <summary>The links of the shortest walk between two clearings along the trails; links in <paramref name="avoid"/> cost more.</summary>
    public static List<int> ShortestPath(RaidLayoutPlan plan, int from, int to, HashSet<int> avoid)
    {
        int count = plan.Rooms.Count;
        var distance = new float[count];
        var via = new int[count];
        var done = new bool[count];
        for (int i = 0; i < count; i++)
        {
            distance[i] = float.MaxValue;
            via[i] = -1;
        }

        distance[from] = 0f;
        for (int step = 0; step < count; step++)
        {
            int room = -1;
            for (int i = 0; i < count; i++)
            {
                if (!done[i] && distance[i] < float.MaxValue && (room < 0 || distance[i] < distance[room]))
                {
                    room = i;
                }
            }

            if (room < 0 || room == to)
            {
                break;
            }

            done[room] = true;
            for (int link = 0; link < plan.Links.Count && link < plan.Trails.Count; link++)
            {
                (int a, int b) = plan.Links[link];
                int other = a == room ? b : b == room ? a : -1;
                if (other < 0 || done[other])
                {
                    continue;
                }

                float cost = Length(plan.Trails[link]) * (avoid != null && avoid.Contains(link) ? RoutePenalty : 1f);
                if (distance[room] + cost < distance[other])
                {
                    distance[other] = distance[room] + cost;
                    via[other] = link;
                }
            }
        }

        if (distance[to] == float.MaxValue)
        {
            return null;
        }

        var path = new List<int>();
        for (int room = to; room != from;)
        {
            int link = via[room];
            path.Add(link);
            room = plan.Links[link].a == room ? plan.Links[link].b : plan.Links[link].a;
        }

        path.Reverse();
        return path;
    }

    private static bool AddBypass(RaidLayoutPlan plan, RaidLayoutRules rules, List<int> route, System.Random random)
    {
        // Each clearing, by which clearing of the way it hangs from (the way's own clearings by their place along it).
        var onRoute = new List<int> { 0 };
        foreach (int link in route)
        {
            int last = onRoute[onRoute.Count - 1];
            onRoute.Add(plan.Links[link].a == last ? plan.Links[link].b : plan.Links[link].a);
        }

        var hangs = new int[plan.Rooms.Count];
        for (int i = 0; i < hangs.Length; i++)
        {
            hangs[i] = -1;
        }

        var routeLinks = new HashSet<int>(route);
        var open = new Queue<int>();
        for (int i = 0; i < onRoute.Count; i++)
        {
            hangs[onRoute[i]] = i;
            open.Enqueue(onRoute[i]);
        }

        while (open.Count > 0)
        {
            int room = open.Dequeue();
            for (int link = 0; link < plan.Links.Count; link++)
            {
                if (routeLinks.Contains(link))
                {
                    continue;
                }

                (int a, int b) = plan.Links[link];
                int other = a == room ? b : b == room ? a : -1;
                if (other >= 0 && hangs[other] < 0)
                {
                    hangs[other] = hangs[room];
                    open.Enqueue(other);
                }
            }
        }

        int bestA = -1, bestB = -1;
        float best = rules.LoopReach * BypassReach;
        for (int a = 0; a < plan.Rooms.Count; a++)
        {
            for (int b = a + 1; b < plan.Rooms.Count; b++)
            {
                if (hangs[a] < 0 || hangs[b] < 0 || Mathf.Abs(hangs[a] - hangs[b]) < 2 || plan.Links.Contains((a, b)) || plan.Links.Contains((b, a)))
                {
                    continue;
                }

                float distance = Vector2.Distance(plan.Rooms[a].Position, plan.Rooms[b].Position);
                if (distance < best && !Crosses(plan, a, b))
                {
                    best = distance;
                    bestA = a;
                    bestB = b;
                }
            }
        }

        if (bestA < 0)
        {
            return false;
        }

        plan.Links.Add((bestA, bestB));
        plan.Rooms[bestA].Links++;
        plan.Rooms[bestB].Links++;
        plan.Trails.Add(Wander(plan.Rooms[bestA].Position, plan.Rooms[bestB].Position, rules, plan.Size, random));
        return true;
    }

    /// <summary>The clearings the short way to the exit passes through (its ends included).</summary>
    public static HashSet<int> RoomsOf(RaidLayoutPlan plan, HashSet<int> links)
    {
        var rooms = new HashSet<int>();
        foreach (int link in links)
        {
            rooms.Add(plan.Links[link].a);
            rooms.Add(plan.Links[link].b);
        }

        return rooms;
    }

    // ----- Height -----

    /// <summary>Roughly how high the ground stands at a point before the ridges and roughness: its terrace and the great hills.</summary>
    public static float HeightOf(RaidLayoutPlan plan, Vector2 point)
    {
        float height = 0f;
        foreach (RaidLayoutHill hill in plan.Hills)
        {
            height += hill.RiseAt(point);
        }

        if (plan.HasTerraces)
        {
            RaidLayoutRoom nearest = null;
            float closest = float.MaxValue;
            foreach (RaidLayoutRoom room in plan.Rooms)
            {
                float distance = Vector2.Distance(room.Position, point) - room.Radius;
                if (distance < closest)
                {
                    closest = distance;
                    nearest = room;
                }
            }

            height += nearest != null ? nearest.Level * plan.TerraceHeight : 0f;
        }

        return height;
    }

    // The greatest danger waits on the highest ground of the far third of the map, which a terrace gives a hill fort's single way up.
    private static void ApexByHeight(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        if (!plan.HasTerraces && plan.Hills.Count == 0)
        {
            return;
        }

        RaidLayoutRoom apex = plan.Rooms.Find(room => room.Role == RaidRoomRole.Apex);
        if (apex == null)
        {
            return;
        }

        float far = Vector2.Distance(apex.Position, plan.Start.Position) * 0.67f;
        RaidLayoutRoom best = apex;
        float highest = HeightOf(plan, apex.Position);
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (room.Role == RaidRoomRole.Plain && Vector2.Distance(room.Position, plan.Start.Position) >= far)
            {
                float height = HeightOf(plan, room.Position);
                if (height > highest + 0.5f)
                {
                    highest = height;
                    best = room;
                }
            }
        }

        if (best != apex)
        {
            apex.Role = RaidRoomRole.Plain;
            best.Role = RaidRoomRole.Apex;
            best.Radius = Mathf.Max(best.Radius, rules.RoomRadius.y * 1.2f * plan.Mood.rooms);
        }
    }

    // At the top of each ramp, at the edge of the higher clearing, looking down the way up.
    private static void PlaceOverRamps(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSiteQuota quota, System.Random random)
    {
        if (!quota.overlooksRamps || !plan.HasTerraces)
        {
            return;
        }

        foreach (int index in RaidLayout.PickIndices(random, plan.Links.Count, plan.Links.Count))
        {
            (int a, int b) = plan.Links[index];
            RaidLayoutRoom first = plan.Rooms[a], second = plan.Rooms[b];
            if (first.Level == second.Level || CountOf(plan, quota.kind) >= quota.count)
            {
                continue;
            }

            RaidLayoutRoom upper = first.Level > second.Level ? first : second;
            RaidLayoutRoom lower = upper == first ? second : first;
            if (upper.Role != RaidRoomRole.Plain && upper.Role != RaidRoomRole.Hub)
            {
                continue;
            }

            Vector2 spot = upper.Position + (lower.Position - upper.Position).normalized * upper.Radius * 0.7f;
            spot = Inside(spot, plan.Size, rules.EdgeMargin);
            if (CanStand(plan, quota, spot) && PocketFits(plan, rules, spot, quota.clearing))
            {
                AddSite(plan, quota, spot);
            }
        }
    }

    // ----- A way down into a ravine -----

    // A long ravine near a trail gets one end sloping down to its floor from a short spur, and a find or two waiting at the bottom.
    private static void PlaceDescents(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        var candidates = plan.Ravines.FindAll(one => one.Mood != null && one.Mood.ravineDescents > 0 && Length(one.Line) >= DescentShortest);
        if (candidates.Count == 0)
        {
            return;
        }

        int wanted = 0;
        foreach (RaidLayoutRavine ravine in candidates)
        {
            wanted = Mathf.Max(wanted, ravine.Mood.ravineDescents);
        }

        candidates.Sort((a, b) => Length(b.Line).CompareTo(Length(a.Line)));
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        int made = 0;
        foreach (RaidLayoutRavine ravine in candidates)
        {
            if (made >= wanted)
            {
                break;
            }

            if (TryDescent(plan, rules, ravine, true, halfWidth) || TryDescent(plan, rules, ravine, false, halfWidth))
            {
                made++;
            }
        }

        if (made > 0)
        {
            RaidMapMarkers.Sort(plan.Sites);
        }
    }

    private static bool TryDescent(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutRavine ravine, bool atStart, float halfWidth)
    {
        int count = ravine.Line.Count;
        Vector2 end = atStart ? ravine.Line[0] : ravine.Line[count - 1];
        Vector2 inward = atStart ? ravine.Line[1] : ravine.Line[count - 2];
        Vector2 outward = (end - inward).normalized;
        float half = (atStart ? ravine.HalfWidths[0] : ravine.HalfWidths[count - 1]) / Mathf.Max(0.01f, atStart ? ravine.Tapers[0] : ravine.Tapers[count - 1]);
        Vector2 entry = end + outward * (half + DescentEntry);
        Vector2 nearest = default;
        float closest = DescentFromTrail;
        foreach (List<Vector2> trail in plan.Trails)
        {
            for (int i = 0; i + 1 < trail.Count; i++)
            {
                Vector2 along = trail[i + 1] - trail[i];
                float t = Mathf.Clamp01(Vector2.Dot(entry - trail[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
                Vector2 point = trail[i] + along * t;
                float distance = Vector2.Distance(point, entry);
                if (distance < closest)
                {
                    closest = distance;
                    nearest = point;
                }
            }
        }

        if (closest >= DescentFromTrail)
        {
            return false;
        }

        if (!atStart)
        {
            ravine.Line.Reverse();
            ravine.HalfWidths.Reverse();
            ravine.Tapers.Reverse();
        }

        // Full width at the way in: a ravine narrows towards its closed ends, not its open one.
        for (int i = 0; i < count / 2; i++)
        {
            ravine.HalfWidths[i] /= Mathf.Max(0.01f, ravine.Tapers[i]);
            ravine.Tapers[i] = 1f;
        }

        ravine.Descent = true;
        ravine.River = false;
        plan.Spurs.Add(new List<Vector2> { nearest, entry });

        // The finds stand past the foot of the slope, on the floor.
        float ramp = ravine.Depth * DescentSlope;
        float run = 0f;
        int placed = 0;
        for (int i = 1; i < count && placed < rules.DescentSites.Count; i++)
        {
            run += Vector2.Distance(ravine.Line[i - 1], ravine.Line[i]);
            if (run >= ramp + DescentAfterRamp + placed * DescentSiteGap)
            {
                plan.Sites.Add(new RaidSite(rules.DescentSites[placed], ravine.Line[i] / plan.Size));
                placed++;
            }
        }

        return true;
    }

    // ----- Rivers across the open land -----

    private static void PlaceRivers(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        if (rules.SurfaceRivers.y <= 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Rivers " + attempt));
        float share = 0f;
        foreach (RaidLayoutRegion region in plan.Regions)
        {
            share = Mathf.Max(share, region.Mood.rivers);
        }

        int wanted = Mathf.RoundToInt(random.Next(rules.SurfaceRivers.x, rules.SurfaceRivers.y + 1) * Mathf.Min(1f, share));
        for (int i = 0; i < wanted; i++)
        {
            for (int tries = 0; tries < RiverTries; tries++)
            {
                RaidLayoutRiver river = GrowRiver(plan, rules, random, share);
                if (river != null)
                {
                    plan.Rivers.Add(river);
                    break;
                }
            }
        }
    }

    private static RaidLayoutRiver GrowRiver(RaidLayoutPlan plan, RaidLayoutRules rules, System.Random random, float most)
    {
        float half = Range(random, rules.SurfaceRiverWidth.x, rules.SurfaceRiverWidth.y) * 0.5f;
        float length = Range(random, rules.SurfaceRiverLength.x, rules.SurfaceRiverLength.y);
        var start = new Vector2(Range(random, rules.EdgeMargin, plan.Size - rules.EdgeMargin), Range(random, rules.EdgeMargin, plan.Size - rules.EdgeMargin));
        // A river comes up in a region as often as its mood says.
        if (random.NextDouble() * most > plan.MoodAt(start).rivers)
        {
            return null;
        }

        float heading = Range(random, 0f, 360f);
        if (!RiverCanPass(plan, rules, start, Direction(heading), half, out _))
        {
            return null;
        }

        var ahead = new List<Vector2>();
        var behind = new List<Vector2>();
        float grown = 0f;
        float headingAhead = heading, headingBehind = heading + 180f;
        float turnAhead = Range(random, -RiverTurn, RiverTurn), turnBehind = Range(random, -RiverTurn, RiverTurn);
        bool openAhead = true, openBehind = true;
        Vector2 tipAhead = start, tipBehind = start;
        while (grown < length && (openAhead || openBehind))
        {
            if (openAhead)
            {
                openAhead = Step(ref tipAhead, ref headingAhead, ref turnAhead, ahead);
            }

            if (openBehind && grown < length)
            {
                openBehind = Step(ref tipBehind, ref headingBehind, ref turnBehind, behind);
            }
        }

        if (grown < rules.SurfaceRiverLength.x * 0.5f)
        {
            return null;
        }

        var river = new RaidLayoutRiver { Depth = Range(random, rules.SurfaceRiverDepth.x, rules.SurfaceRiverDepth.y) };
        for (int i = behind.Count - 1; i >= 0; i--)
        {
            river.Line.Add(behind[i]);
        }

        river.Line.Add(start);
        river.Line.AddRange(ahead);
        float noiseAt = Range(random, 0f, 500f);
        float run = 0f;
        for (int i = 0; i < river.Line.Count; i++)
        {
            run += i > 0 ? Vector2.Distance(river.Line[i - 1], river.Line[i]) : 0f;
            river.HalfWidths.Add(half * (1f + (Mathf.PerlinNoise(noiseAt + run / 31f, noiseAt) - 0.5f) * 0.5f));
        }

        return river;

        bool Step(ref Vector2 tip, ref float facing, ref float turn, List<Vector2> into)
        {
            turn = Mathf.Clamp(turn + Range(random, -RiverTurn, RiverTurn) * 0.5f, -RiverTurn * 1.5f, RiverTurn * 1.5f);
            float side = random.Next(2) == 0 ? 1f : -1f;
            foreach (float steer in RavineSteers)
            {
                float facingNow = facing + turn + steer * side;
                Vector2 next = tip + Direction(facingNow) * RiverStep;
                if (RiverCanPass(plan, rules, next, Direction(facingNow), half, out bool atEdge))
                {
                    facing = facingNow;
                    into.Add(next);
                    tip = next;
                    grown += RiverStep;
                    return true;
                }

                if (atEdge)
                {
                    // Off the edge of the map: it runs on out of sight, the best end a river can have.
                    return false;
                }
            }

            return false;
        }
    }

    private static bool RiverCanPass(RaidLayoutPlan plan, RaidLayoutRules rules, Vector2 point, Vector2 heading, float half, out bool atEdge)
    {
        float margin = rules.EdgeMargin * 0.5f;
        atEdge = point.x < margin || point.y < margin || point.x > plan.Size - margin || point.y > plan.Size - margin;
        if (atEdge)
        {
            return false;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, point) < room.Radius + half + RiverBank + RiverFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, point) < half + RiverBank + RiverFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, point) < radius + half + RiverBank)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(point) < half + RiverBank + 2f)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(point, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + RaidLayoutRavine.RagReach + half + RiverFromRavine)
            {
                return false;
            }
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if (spire.Edge(point) < half + RiverBank + 2f)
            {
                return false;
            }
        }

        foreach (RaidLayoutRiver other in plan.Rivers)
        {
            other.Nearest(point, out _, out _, out float distance);
            if (distance < half + RiverFromRiver)
            {
                return false;
            }
        }

        // A trail meets it at a ford, so it crosses trails at a fair angle and never runs along one.
        float keep = rules.TrailHalfWidth * plan.Mood.trails + half + 2f;
        foreach (List<Vector2> line in plan.Trails)
        {
            if (!CrossesAtLeast(line, point, heading, keep, RiverCrossing))
            {
                return false;
            }
        }

        foreach (List<Vector2> line in plan.Spurs)
        {
            if (!CrossesAtLeast(line, point, heading, keep, RiverCrossing))
            {
                return false;
            }
        }

        return true;
    }

    private static bool CrossesAtLeast(List<Vector2> line, Vector2 point, Vector2 heading, float keep, float sine)
    {
        float squared = keep * keep;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 along = line[i + 1] - line[i];
            float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
            if ((line[i] + along * t - point).sqrMagnitude >= squared)
            {
                continue;
            }

            Vector2 direction = along.normalized;
            if (Mathf.Abs(heading.x * direction.y - heading.y * direction.x) < sine)
            {
                return false;
            }
        }

        return true;
    }

    // ----- Landmarks -----

    // Hill tops first, as a landmark is for seeing from afar; then open wild ground, well off the trails.
    private static void PlaceLandmarks(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        if (rules.LandmarkCount.y <= 0 || rules.Landmarks.Count == 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Landmarks " + attempt));
        int wanted = random.Next(rules.LandmarkCount.x, rules.LandmarkCount.y + 1);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        var hills = new List<RaidLayoutHill>(plan.Hills);
        hills.Sort((a, b) => b.Height.CompareTo(a.Height));
        foreach (RaidLayoutHill hill in hills)
        {
            if (plan.Landmarks.Count >= wanted)
            {
                return;
            }

            TryLandmark(plan, hill.Position, halfWidth, random);
        }

        for (int tries = 0; tries < LandmarkTries && plan.Landmarks.Count < wanted; tries++)
        {
            var spot = new Vector2(Range(random, rules.EdgeMargin + rules.RimWidth, plan.Size - rules.EdgeMargin - rules.RimWidth),
                Range(random, rules.EdgeMargin + rules.RimWidth, plan.Size - rules.EdgeMargin - rules.RimWidth));
            TryLandmark(plan, spot, halfWidth, random);
        }
    }

    private static void TryLandmark(RaidLayoutPlan plan, Vector2 spot, float halfWidth, System.Random random)
    {
        if (LineWithin(plan.Trails, spot, halfWidth + LandmarkFromTrail) || LineWithin(plan.Spurs, spot, halfWidth + LandmarkFromTrail) || plan.InRiver(spot, RiverBank + 4f))
        {
            return;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, spot) < room.Radius + LandmarkFromRoom)
            {
                return;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < LandmarkFromRoom)
            {
                return;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(spot, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + RaidLayoutRavine.RagReach + 5f)
            {
                return;
            }
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if (spire.Edge(spot) < 6f)
            {
                return;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(spot) < 5f)
            {
                return;
            }
        }

        foreach (RaidLayoutLandmark other in plan.Landmarks)
        {
            if (Vector2.Distance(other.Position, spot) < LandmarkFromOther)
            {
                return;
            }
        }

        plan.Landmarks.Add(new RaidLayoutLandmark { Position = spot, Turn = Range(random, 0f, 360f), Pick = (float)random.NextDouble(), Scale = (float)random.NextDouble() });
    }
}
