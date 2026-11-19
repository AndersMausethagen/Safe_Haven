using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// That the made map can be walked: trails blocked by what was placed are cleared, and every exit (and clearing) is
/// checked to be reachable from the start. Part of RaidMapGenerator, in its own file.
/// </summary>
public static partial class RaidMapGenerator
{
    private const float ReachSample = 8f;

    /// <summary>
    /// After a failed reachability check: switches off the colliders of the boulders and rocks standing near the trails into the clearings
    /// that couldn't be reached, so the navmesh can be built again without laying out the whole map anew. Returns how many it cleared.
    /// </summary>
    public static int ClearBlockedTrails(RaidMap map, RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        Vector3 World(Vector2 position) => RaidMapSurface.Ground(terrain, origin + new Vector3(position.x * plan.Size, 0f, position.y * plan.Size));
        var path = new NavMeshPath();
        bool Walkable(Vector2 from, Vector2 to) =>
            NavMesh.SamplePosition(World(from / plan.Size), out NavMeshHit a, ReachSample, NavMesh.AllAreas)
            && NavMesh.SamplePosition(World(to / plan.Size), out NavMeshHit b, ReachSample, NavMesh.AllAreas)
            && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
            && path.status == NavMeshPathStatus.PathComplete;
        bool[] reached = RoomsReached(plan, Walkable);
        var blocked = new List<List<Vector2>>();
        for (int i = 0; i < plan.Links.Count && i < plan.Trails.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            if (reached[a] != reached[b])
            {
                blocked.Add(plan.Trails[i]);
            }
        }

        float reach = rules.TrailHalfWidth * plan.Mood.trails + BoulderWidest * 0.5f + 1f;
        int cleared = 0;
        foreach (string holderName in new[] { BouldersName, RavinesName, DressingName, BarriersName })
        {
            Transform holder = map.transform.Find(holderName);
            if (holder == null)
            {
                continue;
            }

            foreach (Transform piece in holder)
            {
                var flat = new Vector2(piece.position.x - origin.x, piece.position.z - origin.z);
                if (!NearAny(blocked, flat, reach))
                {
                    continue;
                }

                foreach (Collider collider in piece.GetComponentsInChildren<Collider>())
                {
                    if (collider.enabled)
                    {
                        collider.enabled = false;
                        cleared++;
                    }
                }
            }
        }

        return cleared;
    }

    /// <summary>True when the navmesh leads from the start to every exit (GDD §7.6: a reachable extraction must always exist).</summary>
    public static bool ExitsReachable(RaidMap map, RaidLayoutPlan plan)
    {
        Terrain terrain = map.Terrain;
        Vector3 origin = terrain.transform.position;
        Vector3 World(Vector2 position) => RaidMapSurface.Ground(terrain, origin + new Vector3(position.x * plan.Size, 0f, position.y * plan.Size));
        var path = new NavMeshPath();
        bool Walkable(Vector2 from, Vector2 to) =>
            NavMesh.SamplePosition(World(from / plan.Size), out NavMeshHit a, ReachSample, NavMesh.AllAreas)
            && NavMesh.SamplePosition(World(to / plan.Size), out NavMeshHit b, ReachSample, NavMesh.AllAreas)
            && NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)
            && path.status == NavMeshPathStatus.PathComplete;

        // Clearing to clearing along the trails, as the player goes. One long search runs out of nodes on a large map and reports a partial path even where the way is open.
        bool[] reached = RoomsReached(plan, Walkable);
        for (int i = 0; i < plan.Rooms.Count; i++)
        {
            if (plan.Rooms[i].Role == RaidRoomRole.Exit && !reached[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Which clearings can be walked to from the start, given whether the way between two is open.</summary>
    public static bool[] RoomsReached(RaidLayoutPlan plan, System.Func<Vector2, Vector2, bool> walkable)
    {
        var reached = new bool[plan.Rooms.Count];
        var open = new Queue<int>();
        int start = plan.Rooms.IndexOf(plan.Start);
        reached[start] = true;
        open.Enqueue(start);
        while (open.Count > 0)
        {
            int room = open.Dequeue();
            foreach ((int a, int b) in plan.Links)
            {
                int other = a == room ? b : b == room ? a : -1;
                if (other >= 0 && !reached[other] && walkable(plan.Rooms[room].Position, plan.Rooms[other].Position))
                {
                    reached[other] = true;
                    open.Enqueue(other);
                }
            }
        }

        return reached;
    }
}
