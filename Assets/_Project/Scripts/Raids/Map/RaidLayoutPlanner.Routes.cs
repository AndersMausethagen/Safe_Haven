using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Two ways to the beacon ahead where the map allows: the shortest route, and a longer quieter one that shares little
/// of it, joined by a bypass when none exists. Part of RaidLayoutPlanner, in its own file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float RoutePenalty = 4f;

    private const float RouteShared = 0.6f;

    private const float BypassReach = 1.5f;

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
}
