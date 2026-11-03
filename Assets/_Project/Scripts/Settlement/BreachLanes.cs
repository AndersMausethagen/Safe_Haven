using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>One way into the clearing: where attackers set out from, the road they take to the portal and its direction in words.</summary>
public readonly struct BreachLane
{
    public readonly Vector3 Start;
    public readonly Vector3[] Road;
    public readonly string Direction;

    public BreachLane(Vector3 start, Vector3[] road, string direction)
    {
        Start = start;
        Road = road;
        Direction = direction;
    }
}

/// <summary>
/// Finds the ways a breach comes in (GDD §6.9: clearly marked lanes, predictable pathing): spread around the portal beyond
/// the ward, each with a road that really reaches the portal. A lane is found again from its seed after a load, and it moves
/// out as the ward grows, so a bigger home is attacked from further off.
/// </summary>
public static class BreachLanes
{
    private const float SearchReach = 8f;
    private const float StopShortOfTree = 4f;
    private const float TurnStep = 20f;
    private const int Turns = 12;

    private static readonly string[] Winds = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };

    public static List<BreachLane> Find(Vector3 centre, float distance, int count, int seed)
    {
        var lanes = new List<BreachLane>(count);
        var path = new NavMeshPath();
        float first = (float)new System.Random(seed).NextDouble() * 360f;
        for (int i = 0; i < count; i++)
        {
            float heading = first + i * 360f / count;
            for (int turn = 0; turn < Turns; turn++)
            {
                // Out from the even spread, either side in turn, until a road is found.
                float angle = heading + (turn + 1) / 2 * TurnStep * (turn % 2 == 0 ? 1f : -1f);
                if (TryRoad(centre, angle, distance, path, out BreachLane lane))
                {
                    lanes.Add(lane);
                    break;
                }
            }
        }

        return lanes;
    }

    /// <summary>The compass point of one place from another, in words. North is world +Z.</summary>
    public static string Compass(Vector3 from, Vector3 to)
    {
        float bearing = Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;
        int wind = Mathf.RoundToInt(Mathf.Repeat(bearing, 360f) / 45f) % Winds.Length;
        return Winds[wind];
    }

    /// <summary>Several directions as one phrase: "the north and the south-east".</summary>
    public static string Describe(IReadOnlyList<BreachLane> lanes)
    {
        var names = new List<string>(lanes.Count);
        foreach (BreachLane lane in lanes)
        {
            if (!names.Contains(lane.Direction))
            {
                names.Add(lane.Direction);
            }
        }

        if (names.Count == 0)
        {
            return "";
        }

        string last = "the " + names[names.Count - 1];
        if (names.Count == 1)
        {
            return last;
        }

        names.RemoveAt(names.Count - 1);
        return "the " + string.Join(", the ", names) + " and " + last;
    }

    private static bool TryRoad(Vector3 centre, float angle, float distance, NavMeshPath path, out BreachLane lane)
    {
        lane = default;
        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        if (!NavMesh.SamplePosition(centre + direction * distance, out NavMeshHit start, SearchReach, NavMesh.AllAreas)
            || !NavMesh.SamplePosition(centre + direction * StopShortOfTree, out NavMeshHit end, SearchReach, NavMesh.AllAreas)
            || !NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
        {
            return false;
        }

        lane = new BreachLane(start.position, path.corners, Compass(centre, start.position));
        return true;
    }
}
