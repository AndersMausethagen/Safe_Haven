using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Landmarks seen from afar, set apart from trails, clearings and each other; landmarks at the nodes where trails meet, for a top-down
/// camera that sees little of the land (Docs/WorldDesignReview_2026-11-17.md G4); and short flags along the two ways to the exit
/// ahead. Each from its own random stream, after everything else, so none of them moves anything placed before. Part of
/// RaidLayoutPlanner, in its own file.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float LandmarkFromTrail = 9f;

    private const float LandmarkFromRoom = 8f;

    private const float LandmarkFromOther = 30f;

    private const int LandmarkTries = 60;

    // Hill tops first, as a landmark is for seeing from afar; then open wild ground, well off the trails.
    private static void PlaceLandmarks(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        // Landmarks for nodes have their own step (PlaceNodeLandmarks); with none of the others there is nothing to place here.
        bool any = false;
        foreach (RaidLandmarkRule rule in rules.Landmarks)
        {
            any |= rule != null && !rule.atNodes;
        }

        if (rules.LandmarkCount.y <= 0 || !any)
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

    // Landmarks at nodes: kept from trails (beyond a trail's edge), sites, water, cliffs, boulders and each other.
    private const float NodeFromTrail = 2.5f;
    private const float NodeFromSite = 6f;
    private const float NodeFromOther = 40f;
    private const float NodeFromBoulder = 5f;
    private const int NodeAngleTries = 7;
    private const float NodeAngleStep = 0.4f;
    // Inside the clearing's edge (a giant at the edge of the open ground, like a lookout), then just beyond it.
    private static readonly float[] NodeDistances = { 0.8f, 1.25f };

    /// <summary>
    /// Landmarks at the map's nodes (RaidLandmarkRule.atNodes): the clearings three or more trails meet, the hub and the apex, the
    /// busiest first. One in each district first, then the rest anywhere; each kind once while another is left. Each stands in the
    /// widest gap between the trails leaving its clearing, so it never stands in a way. Own stream: rules with none at nodes draw nothing.
    /// </summary>
    private static void PlaceNodeLandmarks(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        var kinds = new List<int>();
        for (int i = 0; i < rules.Landmarks.Count; i++)
        {
            RaidLandmarkRule rule = rules.Landmarks[i];
            // Reference checks only: the plan can be laid out off the main thread.
            if (rule != null && rule.atNodes && !(rule.prefab is null) && rule.weight > 0f)
            {
                kinds.Add(i);
            }
        }

        if (kinds.Count == 0 || rules.NodeLandmarkCount.y <= 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Node Landmarks " + attempt));
        int wanted = random.Next(Mathf.Max(0, rules.NodeLandmarkCount.x), rules.NodeLandmarkCount.y + 1);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;

        // The nodes, busiest first; shuffled first so equal ones fall by the seed (rank keeps the sort stable).
        var nodes = new List<int>();
        foreach (int index in RaidLayout.PickIndices(random, plan.Rooms.Count, plan.Rooms.Count))
        {
            RaidLayoutRoom room = plan.Rooms[index];
            if (room.Role == RaidRoomRole.Hub || room.Role == RaidRoomRole.Apex || (room.Role == RaidRoomRole.Plain && room.Links >= 3))
            {
                nodes.Add(index);
            }
        }

        var rank = new Dictionary<int, int>();
        for (int i = 0; i < nodes.Count; i++)
        {
            rank[nodes[i]] = i;
        }

        nodes.Sort((a, b) =>
        {
            int busy = NodeScore(plan.Rooms[b]).CompareTo(NodeScore(plan.Rooms[a]));
            return busy != 0 ? busy : rank[a].CompareTo(rank[b]);
        });

        var used = new List<int>();
        var taken = new HashSet<int>();
        int placed = 0;
        int districts = Mathf.Max(1, plan.Regions.Count);
        for (int district = 0; district < districts && placed < wanted; district++)
        {
            foreach (int node in nodes)
            {
                if (!taken.Contains(node) && plan.RegionIndexAt(plan.Rooms[node].Position) == district
                    && TryNodeLandmark(plan, rules, node, kinds, used, halfWidth, random))
                {
                    taken.Add(node);
                    placed++;
                    break;
                }
            }
        }

        foreach (int node in nodes)
        {
            if (placed >= wanted)
            {
                break;
            }

            if (!taken.Contains(node) && TryNodeLandmark(plan, rules, node, kinds, used, halfWidth, random))
            {
                taken.Add(node);
                placed++;
            }
        }
    }

    private static int NodeScore(RaidLayoutRoom room) => room.Links + (room.Role == RaidRoomRole.Hub || room.Role == RaidRoomRole.Apex ? 2 : 0);

    private static bool TryNodeLandmark(RaidLayoutPlan plan, RaidLayoutRules rules, int node, List<int> kinds, List<int> used, float halfWidth,
        System.Random random)
    {
        int kind = PickNodeKind(rules, kinds, used, random);
        RaidLandmarkRule rule = rules.Landmarks[kind];
        RaidLayoutRoom room = plan.Rooms[node];
        float gap = WidestGap(plan, node, random);
        for (int k = 0; k < NodeAngleTries; k++)
        {
            // The gap's middle first, then a step either way, then two.
            float angle = gap + ((k + 1) / 2) * NodeAngleStep * (k % 2 == 0 ? 1f : -1f);
            foreach (float share in NodeDistances)
            {
                Vector2 spot = room.Position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (room.Radius * share);
                if (!NodeLandmarkFits(plan, rules, spot, halfWidth, rule.clearing))
                {
                    continue;
                }

                used.Add(kind);
                plan.Landmarks.Add(new RaidLayoutLandmark
                {
                    Position = spot, Turn = Range(random, 0f, 360f), Pick = 0f, Scale = (float)random.NextDouble(), Rule = kind,
                });
                if (rule.clearing > 0f)
                {
                    plan.Pockets.Add((spot, rule.clearing));
                }

                return true;
            }
        }

        return false;
    }

    // By weight, among the kinds not yet on this map while there are any (each landmark a different kind).
    private static int PickNodeKind(RaidLayoutRules rules, List<int> kinds, List<int> used, System.Random random)
    {
        bool fresh = false;
        foreach (int kind in kinds)
        {
            fresh |= !used.Contains(kind);
        }

        float total = 0f;
        foreach (int kind in kinds)
        {
            total += fresh && used.Contains(kind) ? 0f : rules.Landmarks[kind].weight;
        }

        float roll = (float)random.NextDouble() * total;
        int last = kinds[0];
        foreach (int kind in kinds)
        {
            if (fresh && used.Contains(kind))
            {
                continue;
            }

            last = kind;
            roll -= rules.Landmarks[kind].weight;
            if (roll <= 0f)
            {
                return kind;
            }
        }

        return last;
    }

    // The middle of the widest gap, in radians, between the trails leaving a clearing (each by its first bend), so what stands there
    // stands beside the ways in, never across one.
    private static float WidestGap(RaidLayoutPlan plan, int node, System.Random random)
    {
        Vector2 centre = plan.Rooms[node].Position;
        var angles = new List<float>();
        for (int link = 0; link < plan.Links.Count && link < plan.Trails.Count; link++)
        {
            (int a, int b) = plan.Links[link];
            List<Vector2> trail = plan.Trails[link];
            if (trail.Count < 2 || (a != node && b != node))
            {
                continue;
            }

            Vector2 toward = (a == node ? trail[1] : trail[trail.Count - 2]) - centre;
            if (toward.sqrMagnitude > 0.01f)
            {
                angles.Add(Mathf.Atan2(toward.y, toward.x));
            }
        }

        if (angles.Count == 0)
        {
            return Range(random, 0f, Mathf.PI * 2f);
        }

        angles.Sort();
        float best = -1f, middle = 0f;
        for (int i = 0; i < angles.Count; i++)
        {
            float from = angles[i];
            float to = i + 1 < angles.Count ? angles[i + 1] : angles[0] + Mathf.PI * 2f;
            if (to - from > best)
            {
                best = to - from;
                middle = (from + to) * 0.5f;
            }
        }

        return middle;
    }

    private static bool NodeLandmarkFits(RaidLayoutPlan plan, RaidLayoutRules rules, Vector2 spot, float halfWidth, float clearing)
    {
        float margin = rules.EdgeMargin;
        float keep = Mathf.Max(2f, clearing * 0.5f);
        if (spot.x < margin || spot.y < margin || spot.x > plan.Size - margin || spot.y > plan.Size - margin
            || LineWithin(plan.Trails, spot, halfWidth + NodeFromTrail + keep) || LineWithin(plan.Spurs, spot, halfWidth + NodeFromTrail + keep)
            || plan.InRiver(spot, RiverBank + keep) || plan.InWater(spot) || !PocketFits(plan, rules, spot, keep))
        {
            return false;
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < NodeFromSite + keep)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(spot) < 4f + keep)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(spot, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + RaidLayoutRavine.RagReach + 4f + keep)
            {
                return false;
            }
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if (spire.Edge(spot) < 6f)
            {
                return false;
            }
        }

        foreach (RaidLayoutBoulder boulder in plan.Boulders)
        {
            if (Vector2.Distance(boulder.Position, spot) < NodeFromBoulder + keep)
            {
                return false;
            }
        }

        foreach (RaidLayoutPiece piece in plan.Pieces)
        {
            if (Vector2.Distance(piece.Position, spot) < piece.Definition.Radius + keep)
            {
                return false;
            }
        }

        foreach (RaidLayoutLandmark other in plan.Landmarks)
        {
            if (Vector2.Distance(other.Position, spot) < NodeFromOther)
            {
                return false;
            }
        }

        return true;
    }

    // Short flags: never in a clearing (the clearing is the place), clear of other trails, sites, water and each other; a spur is
    // narrower than a trail (RaidLayoutField's SpurWidth).
    private const float FlagFromSite = 4f;
    private const float FlagFromThing = 3f;
    private const float SpurWidthShare = 0.6f;
    private static readonly float[] FlagShifts = { 0f, 6f, -6f, 12f };

    /// <summary>
    /// Short flags beside the short and the long way to the exit ahead (RaidLayoutRules › Short Flags), about every Short Flag Spacing
    /// metres walked from the start, on either side, facing the trail: the minimap shows the way, a flag in view says you are still on
    /// it. Own stream; rules without flags draw nothing.
    /// </summary>
    private static void PlaceShortFlags(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        if (rules.ShortFlags.Count == 0 || plan.ShortRoute.Count == 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Short Flags " + attempt));
        float least = Mathf.Max(10f, rules.ShortFlagSpacing.x);
        float most = Mathf.Max(least, rules.ShortFlagSpacing.y);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails * rules.RoadWidth;
        foreach (HashSet<int> route in new[] { plan.ShortRoute, plan.LongRoute })
        {
            List<Vector2> line = RouteLine(plan, route);
            if (line.Count < 2)
            {
                continue;
            }

            float length = Length(line);
            for (float next = Range(random, least, most) * 0.5f; next < length; next += Range(random, least, most))
            {
                float side = random.Next(2) == 0 ? -1f : 1f;
                float pick = (float)random.NextDouble();
                foreach (float shift in FlagShifts)
                {
                    if (TryFlag(plan, rules, line, next + shift, side, halfWidth, least, pick)
                        || TryFlag(plan, rules, line, next + shift, -side, halfWidth, least, pick))
                    {
                        break;
                    }
                }
            }
        }
    }

    private static bool TryFlag(RaidLayoutPlan plan, RaidLayoutRules rules, List<Vector2> line, float at, float side, float halfWidth, float least,
        float pick)
    {
        if (!PointAlong(line, at, out Vector2 point, out Vector2 along))
        {
            return false;
        }

        Vector2 normal = Vector2.Perpendicular(along) * side;
        Vector2 spot = point + normal * (halfWidth + rules.ShortFlagOffset);
        float margin = rules.EdgeMargin * 0.5f;
        if (spot.x < margin || spot.y < margin || spot.x > plan.Size - margin || spot.y > plan.Size - margin
            || LineWithin(plan.Trails, spot, halfWidth + 0.5f) || LineWithin(plan.Spurs, spot, halfWidth * SpurWidthShare + 0.5f)
            || plan.InRiver(spot, 1.5f) || plan.InWater(spot) || !PocketFits(plan, rules, spot, 1f))
        {
            return false;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, spot) < room.Radius + 1f)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < FlagFromSite)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(spot) < 1.5f)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(spot, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + RaidLayoutRavine.RagReach + 2f)
            {
                return false;
            }
        }

        foreach (RaidLayoutBoulder boulder in plan.Boulders)
        {
            if (Vector2.Distance(boulder.Position, spot) < FlagFromThing + 2f)
            {
                return false;
            }
        }

        foreach (RaidLayoutLandmark landmark in plan.Landmarks)
        {
            if (Vector2.Distance(landmark.Position, spot) < FlagFromThing + 3f)
            {
                return false;
            }
        }

        foreach (RaidLayoutFlag other in plan.Flags)
        {
            if (Vector2.Distance(other.Position, spot) < least * 0.5f)
            {
                return false;
            }
        }

        // Facing the trail: a flag's front (local Z) toward the way it marks.
        Vector2 facing = -normal;
        plan.Flags.Add(new RaidLayoutFlag { Position = spot, Turn = Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg, Pick = pick });
        return true;
    }

    /// <summary>
    /// A route's trails as one line walked from the start (each link of the set once, turned to run onward), up to where it ends or the
    /// set no longer joins on. Empty when no link of the set leaves the start.
    /// </summary>
    public static List<Vector2> RouteLine(RaidLayoutPlan plan, HashSet<int> route)
    {
        var line = new List<Vector2>();
        var left = new List<int>(route);
        left.Sort();
        int at = 0;
        while (left.Count > 0)
        {
            int found = -1;
            bool backward = false;
            foreach (int link in left)
            {
                if (link < plan.Links.Count && link < plan.Trails.Count && (plan.Links[link].a == at || plan.Links[link].b == at))
                {
                    found = link;
                    backward = plan.Links[link].b == at;
                    break;
                }
            }

            if (found < 0)
            {
                break;
            }

            left.Remove(found);
            List<Vector2> trail = plan.Trails[found];
            for (int i = 0; i < trail.Count; i++)
            {
                if (line.Count == 0 || i > 0)
                {
                    line.Add(trail[backward ? trail.Count - 1 - i : i]);
                }
            }

            at = backward ? plan.Links[found].a : plan.Links[found].b;
        }

        return line;
    }

    // The point this many metres along a line and the way it runs there; false beyond its end.
    private static bool PointAlong(List<Vector2> line, float distance, out Vector2 point, out Vector2 along)
    {
        point = line[0];
        along = Vector2.up;
        if (distance < 0f)
        {
            return false;
        }

        float walked = 0f;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            float length = Vector2.Distance(line[i], line[i + 1]);
            if (length > 0.0001f && walked + length >= distance)
            {
                along = (line[i + 1] - line[i]) / length;
                point = line[i] + along * (distance - walked);
                return true;
            }

            walked += length;
        }

        return false;
    }
}
