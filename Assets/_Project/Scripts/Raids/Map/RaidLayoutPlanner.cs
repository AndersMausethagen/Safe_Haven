using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a clearing on a generated map is for.</summary>
public enum RaidRoomRole
{
    Plain = 0,
    Start = 1,
    Exit = 2,
    /// <summary>Where the raid's greatest danger lives: the farthest clearing from the start.</summary>
    Apex = 3,
    /// <summary>The great clearing of a Hub map.</summary>
    Hub = 4,
}

/// <summary>A clearing on a generated map, in metres from the map's south-west corner.</summary>
public sealed class RaidLayoutRoom
{
    public Vector2 Position;
    public float Radius;
    public RaidRoomRole Role;
    /// <summary>How many trails lead here. One makes it a dead end.</summary>
    public int Links;
}

/// <summary>
/// One generated map, as data: its clearings, the trails between them and the sites along them. Made by RaidLayoutPlanner
/// and turned into ground by RaidLayoutField and RaidMapGenerator.
/// </summary>
public sealed class RaidLayoutPlan
{
    public int Seed;
    public float Size;
    public RaidShapeRule Shape;
    public RaidMapMood Mood;
    /// <summary>Added to noise lookups, so every map's hills are its own.</summary>
    public Vector2 NoiseOffset;
    public readonly List<RaidLayoutRoom> Rooms = new List<RaidLayoutRoom>();
    /// <summary>Which rooms each trail joins, by index.</summary>
    public readonly List<(int a, int b)> Links = new List<(int, int)>();
    /// <summary>The trails, as lines in metres. Index for index with Links.</summary>
    public readonly List<List<Vector2>> Trails = new List<List<Vector2>>();
    /// <summary>The short side trails leading to pockets, narrower than the rest.</summary>
    public readonly List<List<Vector2>> Spurs = new List<List<Vector2>>();
    /// <summary>Open ground around sites, in metres.</summary>
    public readonly List<(Vector2 centre, float radius)> Pockets = new List<(Vector2, float)>();
    /// <summary>Sites, 0–1 across the map, like a painted map's markers.</summary>
    public readonly List<RaidSite> Sites = new List<RaidSite>();

    public RaidLayoutRoom Start => Rooms[0];

    /// <summary>The sites of one kind, in the order they were placed (the best spots first).</summary>
    public List<RaidSite> SitesOf(RaidSiteKind kind) => Sites.FindAll(site => site.Kind == kind);
}

/// <summary>
/// Lays out a raid map from a seed and rules, the way roguelikes lay out a level: the start goes on the edge and the exits
/// across from it, clearings fill the land between, trails join them all (a spanning tree, so everything can be reached,
/// plus a few loops), and sites go where the kind of site belongs. Plain C#, so it can be tested; the same seed always
/// gives the same map. The shape and the mood are rolled apart from the rest, so a briefing can name them cheaply.
/// </summary>
public static class RaidLayoutPlanner
{
    private const float ExitSpread = 62f;
    private const float ExitJitter = 14f;
    private const float RingShare = 0.34f;
    private const float SpineShare = 0.12f;
    private const float MinimumFromOtherSites = 7f;
    private const int RoomTries = 28;
    private const int SiteTries = 60;
    private const int WanderLevels = 3;

    /// <summary>The shape and mood of a run: fixed by its seed, whichever attempt at the map is used.</summary>
    public static (RaidShapeRule shape, RaidMapMood mood) Choose(RaidLayoutRules rules, int seed)
    {
        var random = new System.Random(RaidLayout.SeedFor(seed, "Layout Mood"));
        var shapeWeights = new List<float>();
        foreach (RaidShapeRule rule in rules.Shapes)
        {
            shapeWeights.Add(rule.weight);
        }

        var moodWeights = new List<float>();
        foreach (RaidMapMood mood in rules.Moods)
        {
            moodWeights.Add(mood.weight);
        }

        int shape = RaidLayout.PickWeighted(random, shapeWeights);
        int chosen = RaidLayout.PickWeighted(random, moodWeights);
        return (shape >= 0 ? rules.Shapes[shape] : new RaidShapeRule { shape = RaidLayoutShape.Thicket }, chosen >= 0 ? rules.Moods[chosen] : new RaidMapMood());
    }

    public static RaidLayoutPlan Plan(RaidLayoutRules rules, float size, int seed, int attempt = 0)
    {
        var random = new System.Random(RaidLayout.SeedFor(seed, "Layout " + attempt));
        (RaidShapeRule shape, RaidMapMood mood) = Choose(rules, seed);
        var plan = new RaidLayoutPlan
        {
            Seed = seed,
            Size = size,
            Shape = shape,
            Mood = mood,
            NoiseOffset = new Vector2((float)random.NextDouble() * 1000f, (float)random.NextDouble() * 1000f),
        };

        PlaceRooms(plan, rules, random, out List<int> spine);
        JoinRooms(plan, rules, random, spine);
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            RaidSiteKind kind = room.Role == RaidRoomRole.Start ? RaidSiteKind.PlayerStart
                : room.Role == RaidRoomRole.Exit ? RaidSiteKind.Extraction
                : room.Role == RaidRoomRole.Apex ? RaidSiteKind.Apex : RaidSiteKind.None;
            if (kind != RaidSiteKind.None)
            {
                plan.Sites.Add(new RaidSite(kind, room.Position / size));
            }
        }

        foreach (RaidSiteQuota quota in rules.Sites)
        {
            if (quota != null)
            {
                PlaceSites(plan, rules, quota, random);
            }
        }

        RaidMapMarkers.Sort(plan.Sites);
        return plan;
    }

    private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

    private static Vector2 Direction(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

    // The edge of the square map, inside the margin, in a direction from its centre.
    private static Vector2 OnRim(float degrees, float size, float margin)
    {
        Vector2 direction = Direction(degrees);
        float half = size * 0.5f - margin;
        float scale = half / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
        return new Vector2(size * 0.5f, size * 0.5f) + direction * scale;
    }

    private static Vector2 Inside(Vector2 point, float size, float margin) =>
        new Vector2(Mathf.Clamp(point.x, margin, size - margin), Mathf.Clamp(point.y, margin, size - margin));

    private static float RoomSize(RaidLayoutRules rules, RaidMapMood mood, System.Random random) =>
        Range(random, rules.RoomRadius.x, rules.RoomRadius.y) * mood.rooms;

    private static bool IsFree(RaidLayoutPlan plan, Vector2 point, float separation)
    {
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if ((room.Position - point).sqrMagnitude < separation * separation)
            {
                return false;
            }
        }

        return true;
    }

    private static RaidLayoutRoom AddRoom(RaidLayoutPlan plan, Vector2 position, float radius, RaidRoomRole role)
    {
        var room = new RaidLayoutRoom { Position = position, Radius = radius, Role = role };
        plan.Rooms.Add(room);
        return room;
    }

    private static void PlaceRooms(RaidLayoutPlan plan, RaidLayoutRules rules, System.Random random, out List<int> spine)
    {
        float size = plan.Size;
        float margin = rules.EdgeMargin + rules.RoomRadius.y * 0.5f;
        var centre = new Vector2(size * 0.5f, size * 0.5f);
        float startAngle = Range(random, 0f, 360f);
        AddRoom(plan, OnRim(startAngle, size, margin), rules.RoomRadius.x, RaidRoomRole.Start);

        // The exits lie across the map from the start, one ahead and one to either side, so there is always a choice.
        for (int i = -1; i <= 1; i++)
        {
            float angle = startAngle + 180f + i * ExitSpread + Range(random, -ExitJitter, ExitJitter);
            AddRoom(plan, OnRim(angle, size, margin), rules.RoomRadius.x, RaidRoomRole.Exit);
        }

        int wanted = random.Next(rules.Rooms.x, rules.Rooms.y + 1);
        spine = new List<int>();
        switch (plan.Shape.shape)
        {
            case RaidLayoutShape.Hub:
                Vector2 middle = centre + Direction(Range(random, 0f, 360f)) * Range(random, 0f, size * 0.08f);
                AddRoom(plan, middle, rules.RoomRadius.y * 1.5f * plan.Mood.rooms, RaidRoomRole.Hub);
                break;
            case RaidLayoutShape.Ring:
                int around = Mathf.Min(wanted, Mathf.FloorToInt(2f * Mathf.PI * size * RingShare / rules.RoomSeparation));
                float turn = Range(random, 0f, 360f);
                for (int i = 0; i < around; i++)
                {
                    Vector2 spot = centre + Direction(turn + i * 360f / around + Range(random, -6f, 6f)) * (size * RingShare * Range(random, 0.92f, 1.08f));
                    spot = Inside(spot, size, margin);
                    if (IsFree(plan, spot, rules.RoomSeparation * 0.8f))
                    {
                        spine.Add(plan.Rooms.Count);
                        AddRoom(plan, spot, RoomSize(rules, plan.Mood, random), RaidRoomRole.Plain);
                    }
                }

                break;
            case RaidLayoutShape.Spine:
                // A zigzag from the start to the exit straight ahead.
                Vector2 from = plan.Rooms[0].Position;
                Vector2 to = plan.Rooms[2].Position;
                var side = Vector2.Perpendicular((to - from).normalized);
                int stops = Mathf.Max(3, wanted / 2);
                spine.Add(0);
                for (int i = 1; i <= stops; i++)
                {
                    Vector2 stop = Vector2.Lerp(from, to, i / (stops + 1f)) + side * ((i % 2 == 0 ? 1f : -1f) * size * SpineShare * Range(random, 0.6f, 1.2f));
                    stop = Inside(stop, size, margin);
                    if (IsFree(plan, stop, rules.RoomSeparation * 0.8f))
                    {
                        spine.Add(plan.Rooms.Count);
                        AddRoom(plan, stop, RoomSize(rules, plan.Mood, random), RaidRoomRole.Plain);
                    }
                }

                spine.Add(2);
                break;
        }

        // The rest by best candidate: of a few tries, the one farthest from every other clearing, so they spread evenly.
        int total = wanted + 4;
        while (plan.Rooms.Count < total)
        {
            Vector2 best = default;
            float bestDistance = 0f;
            for (int i = 0; i < RoomTries; i++)
            {
                var spot = new Vector2(Range(random, margin, size - margin), Range(random, margin, size - margin));
                float nearest = float.MaxValue;
                foreach (RaidLayoutRoom room in plan.Rooms)
                {
                    nearest = Mathf.Min(nearest, Vector2.Distance(room.Position, spot));
                }

                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    best = spot;
                }
            }

            if (bestDistance < rules.RoomSeparation)
            {
                break;
            }

            AddRoom(plan, best, RoomSize(rules, plan.Mood, random), RaidRoomRole.Plain);
        }

        // The greatest danger waits at the far end.
        RaidLayoutRoom farthest = null;
        float far = -1f;
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            float distance = Vector2.Distance(room.Position, plan.Rooms[0].Position);
            if (room.Role == RaidRoomRole.Plain && distance > far)
            {
                far = distance;
                farthest = room;
            }
        }

        if (farthest != null)
        {
            farthest.Role = RaidRoomRole.Apex;
            farthest.Radius = Mathf.Max(farthest.Radius, rules.RoomRadius.y * 1.2f * plan.Mood.rooms);
        }
    }

    private static void JoinRooms(RaidLayoutPlan plan, RaidLayoutRules rules, System.Random random, List<int> spine)
    {
        int count = plan.Rooms.Count;
        var groups = new int[count];
        for (int i = 0; i < count; i++)
        {
            groups[i] = i;
        }

        int Root(int room)
        {
            while (groups[room] != room)
            {
                groups[room] = groups[groups[room]];
                room = groups[room];
            }

            return room;
        }

        void Join(int a, int b)
        {
            plan.Links.Add((a, b));
            plan.Rooms[a].Links++;
            plan.Rooms[b].Links++;
            groups[Root(a)] = Root(b);
        }

        // What the shape insists on first.
        if (plan.Shape.shape == RaidLayoutShape.Hub)
        {
            int hub = plan.Rooms.FindIndex(room => room.Role == RaidRoomRole.Hub);
            for (int i = 0; hub >= 0 && i < count; i++)
            {
                if (i != hub && Vector2.Distance(plan.Rooms[i].Position, plan.Rooms[hub].Position) < plan.Size * 0.42f)
                {
                    Join(hub, i);
                }
            }
        }
        else if (plan.Shape.shape == RaidLayoutShape.Ring)
        {
            for (int i = 0; i < spine.Count; i++)
            {
                Join(spine[i], spine[(i + 1) % spine.Count]);
            }
        }
        else if (plan.Shape.shape == RaidLayoutShape.Spine)
        {
            for (int i = 0; i + 1 < spine.Count; i++)
            {
                Join(spine[i], spine[i + 1]);
            }
        }

        var pairs = new List<(int a, int b, float length)>();
        for (int a = 0; a < count; a++)
        {
            for (int b = a + 1; b < count; b++)
            {
                pairs.Add((a, b, Vector2.Distance(plan.Rooms[a].Position, plan.Rooms[b].Position)));
            }
        }

        pairs.Sort((x, y) => x.length.CompareTo(y.length));

        // Shortest first joins everything without crossing where it can (Kruskal's spanning tree), then whatever is
        // still cut off joins by any means: being reachable matters more than a tidy map.
        for (int pass = 0; pass < 2; pass++)
        {
            foreach ((int a, int b, float _) in pairs)
            {
                if (Root(a) != Root(b) && (pass == 1 || !Crosses(plan, a, b)))
                {
                    Join(a, b);
                }
            }
        }

        foreach ((int a, int b, float length) in pairs)
        {
            bool joined = plan.Links.Contains((a, b)) || plan.Links.Contains((b, a));
            if (!joined && length <= rules.LoopReach && random.NextDouble() < plan.Shape.loopChance && !Crosses(plan, a, b))
            {
                Join(a, b);
            }
        }

        foreach ((int a, int b) in plan.Links)
        {
            plan.Trails.Add(Wander(plan.Rooms[a].Position, plan.Rooms[b].Position, rules, plan.Size, random));
        }
    }

    private static bool Crosses(RaidLayoutPlan plan, int a, int b)
    {
        Vector2 p1 = plan.Rooms[a].Position;
        Vector2 p2 = plan.Rooms[b].Position;
        foreach ((int c, int d) in plan.Links)
        {
            if (c == a || c == b || d == a || d == b)
            {
                continue;
            }

            if (SegmentsCross(p1, p2, plan.Rooms[c].Position, plan.Rooms[d].Position))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float Side(Vector2 from, Vector2 to, Vector2 point) => (to.x - from.x) * (point.y - from.y) - (to.y - from.y) * (point.x - from.x);
        return Side(a, b, c) * Side(a, b, d) < 0f && Side(c, d, a) * Side(c, d, b) < 0f;
    }

    // Midpoint displacement: each pass splits every stretch and pushes the middle aside, less each time.
    private static List<Vector2> Wander(Vector2 from, Vector2 to, RaidLayoutRules rules, float size, System.Random random)
    {
        var line = new List<Vector2> { from, to };
        float reach = Vector2.Distance(from, to) * rules.TrailWander;
        for (int level = 0; level < WanderLevels; level++)
        {
            var next = new List<Vector2>(line.Count * 2) { line[0] };
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 side = Vector2.Perpendicular((line[i + 1] - line[i]).normalized);
                Vector2 middle = (line[i] + line[i + 1]) * 0.5f + side * Range(random, -reach, reach);
                next.Add(Inside(middle, size, rules.EdgeMargin));
                next.Add(line[i + 1]);
            }

            line = next;
            reach *= 0.5f;
        }

        return line;
    }

    private static void PlaceSites(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSiteQuota quota, System.Random random)
    {
        if (quota.placement == RaidSitePlacement.Room || quota.placement == RaidSitePlacement.DeadEnd)
        {
            PlaceInRooms(plan, rules, quota, random);
        }

        for (int tries = 0; tries < SiteTries * quota.count && CountOf(plan, quota.kind) < quota.count; tries++)
        {
            TryAlongTrail(plan, rules, quota, random);
        }
    }

    private static int CountOf(RaidLayoutPlan plan, RaidSiteKind kind)
    {
        int count = 0;
        foreach (RaidSite site in plan.Sites)
        {
            count += site.Kind == kind ? 1 : 0;
        }

        return count;
    }

    private static void PlaceInRooms(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSiteQuota quota, System.Random random)
    {
        // Dead ends first, or only them; what stays unplaced finds a pocket along a trail.
        foreach (int index in RaidLayout.PickIndices(random, plan.Rooms.Count, plan.Rooms.Count))
        {
            RaidLayoutRoom room = plan.Rooms[index];
            bool fits = room.Role == RaidRoomRole.Plain || room.Role == RaidRoomRole.Hub;
            if (!fits || (quota.placement == RaidSitePlacement.DeadEnd && room.Links > 1) || CountOf(plan, quota.kind) >= quota.count)
            {
                continue;
            }

            Vector2 spot = room.Position + Direction(Range(random, 0f, 360f)) * (room.Radius * Range(random, 0f, 0.5f));
            spot = Inside(spot, plan.Size, rules.EdgeMargin);
            if (CanStand(plan, quota, spot))
            {
                AddSite(plan, quota, spot);
            }
        }
    }

    private static void TryAlongTrail(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSiteQuota quota, System.Random random)
    {
        if (plan.Trails.Count == 0)
        {
            return;
        }

        List<Vector2> trail = plan.Trails[random.Next(plan.Trails.Count)];
        int at = random.Next(trail.Count - 1);
        Vector2 point = Vector2.Lerp(trail[at], trail[at + 1], (float)random.NextDouble());
        if (quota.placement == RaidSitePlacement.OnPath)
        {
            if (CanStand(plan, quota, point))
            {
                AddSite(plan, quota, point);
            }

            return;
        }

        Vector2 side = Vector2.Perpendicular((trail[at + 1] - trail[at]).normalized) * (random.Next(2) == 0 ? -1f : 1f);
        Vector2 spot = point + side * Range(random, quota.offPath.x, quota.offPath.y);
        if (spot.x < rules.EdgeMargin || spot.y < rules.EdgeMargin || spot.x > plan.Size - rules.EdgeMargin || spot.y > plan.Size - rules.EdgeMargin
            || !CanStand(plan, quota, spot))
        {
            return;
        }

        plan.Spurs.Add(new List<Vector2> { point, spot });
        AddSite(plan, quota, spot);
    }

    private static bool CanStand(RaidLayoutPlan plan, RaidSiteQuota quota, Vector2 spot)
    {
        if (Vector2.Distance(spot, plan.Start.Position) < quota.fromStart)
        {
            return false;
        }

        foreach (RaidSite site in plan.Sites)
        {
            float keep = site.Kind == quota.kind ? quota.separation : MinimumFromOtherSites;
            if ((site.Position * plan.Size - spot).sqrMagnitude < keep * keep)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddSite(RaidLayoutPlan plan, RaidSiteQuota quota, Vector2 spot)
    {
        plan.Sites.Add(new RaidSite(quota.kind, spot / plan.Size));
        if (quota.clearing > 0f)
        {
            plan.Pockets.Add((spot, quota.clearing));
        }
    }
}
