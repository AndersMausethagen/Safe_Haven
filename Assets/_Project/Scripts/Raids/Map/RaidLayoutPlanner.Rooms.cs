using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Laying out the clearings and the trails: the start on the edge and the exits across from it, clearings in a ring and
/// along the spine, then a spanning tree of trails with a few loops, each trail wandering a little. Part of
/// RaidLayoutPlanner, in its own file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float ExitSpread = 62f;

    private const float ExitJitter = 14f;

    private const float ExitArc = 250f;

    private const float RingShare = 0.34f;

    private const float SpineShare = 0.12f;

    private const int RoomTries = 28;

    private const int WanderLevels = 3;

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

        // The exits lie across the map from the start, one ahead and one to either side, so there is always a choice. More of them share
        // a wide arc, which keeps clear of the start.
        int exits = rules.ExitRooms;
        float step = exits <= 3 ? ExitSpread : ExitArc / (exits - 1);
        for (int i = 0; i < exits; i++)
        {
            float angle = startAngle + 180f + (i - (exits - 1) * 0.5f) * step + Range(random, -ExitJitter, ExitJitter);
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
                Vector2 to = plan.Rooms[1 + rules.ExitRooms / 2].Position;
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

                // Ends at the exit it heads for (the one straight ahead, EnsureRoutes' too): a fixed 2 was that exit only with two or
                // three exits, and a large raid's six ran its last trail round the rim to another.
                spine.Add(1 + rules.ExitRooms / 2);
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
}
