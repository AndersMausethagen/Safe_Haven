using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lays out a raid map from a seed and rules, the way roguelikes lay out a level: the start goes on the edge and the exits
/// across from it, clearings fill the land between, trails join them all (a spanning tree, so everything can be reached,
/// plus a few loops), and sites go where the kind of site belongs. Plain C#, so it can be tested; the same seed always
/// gives the same map. The shape and the mood are rolled apart from the rest, so a briefing can name them cheaply.
/// <para>
/// Here: rolling the shape and mood, the order of the steps (Plan) and the geometry they share. The steps are parts, each
/// with its own tuning: .Rooms (clearings and trails), .Sites (sites and set pieces), .Ground (hills, boulders, ponds),
/// .Cliffs (levels, ramps, ravines, spires), .Regions (a large map's regions), .Routes (a short way and a long one),
/// .Height (heights, the apex, sites over ramps, ways down into ravines), .Rivers and .Landmarks. The map itself is
/// a <see cref="RaidLayoutPlan"/>. Each step draws from the random streams in a fixed order, so moving code between the
/// parts changes nothing, but reordering draws inside a step changes every map for a seed.
/// </para>
/// </summary>
public static partial class RaidLayoutPlanner
{
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

        if (plan.Shape.shape == RaidLayoutShape.Valley)
        {
            PlaceValley(plan, rules, attempt);
        }

        PlaceRooms(plan, rules, random, out List<int> spine);
        JoinRooms(plan, rules, random, spine);
        PlaceRegions(plan, rules, attempt);
        EnsureRoutes(plan, rules, attempt);
        ShapeCrossings(plan, rules, attempt);
        AssignLevels(plan, rules);
        PlaceHills(plan, rules, attempt);
        ApexByHeight(plan, rules);
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
        PlaceSetPieces(plan, rules, seed, attempt);
        PlacePonds(plan, rules, attempt);
        PlaceRavines(plan, rules, attempt);
        PlaceDescents(plan, rules);
        PlaceSpires(plan, rules, attempt);
        PlaceRivers(plan, rules, attempt);
        PlaceBoulders(plan, rules, attempt);
        PlaceLandmarks(plan, rules, attempt);
        // After every step above, each from its own stream, so turning them on never moves what those placed.
        PlaceNodeLandmarks(plan, rules, attempt);
        PlaceShortFlags(plan, rules, attempt);
        PlaceBarriers(plan, rules, attempt);
        return plan;
    }

    /// <summary>How hilly a run is: fixed by its seed, whichever attempt at the map is used.</summary>
    public static float Hilliness(RaidLayoutRules rules, int seed)
    {
        var random = new System.Random(RaidLayout.SeedFor(seed, "Hilliness"));
        return Range(random, rules.HillVariance.x, Mathf.Max(rules.HillVariance.x, rules.HillVariance.y));
    }

    /// <summary>
    /// A seed whose run passes <paramref name="wanted"/> (its shape and mood), tried from <paramref name="from"/> on: cheap, as only Choose
    /// runs. Null when none of <paramref name="tries"/> does, as when the rules have no such mood. The cheat menu starts a raid of a chosen
    /// kind this way, so the run is an ordinary one and its briefing, map and spawns all agree.
    /// </summary>
    public static int? FindSeed(RaidLayoutRules rules, System.Func<RaidShapeRule, RaidMapMood, bool> wanted, int from, int tries = 4000)
    {
        for (int i = 0; i < tries; i++)
        {
            int seed = unchecked(from + i * 7919);
            (RaidShapeRule shape, RaidMapMood mood) = Choose(rules, seed);
            if (wanted(shape, mood))
            {
                return seed;
            }
        }

        return null;
    }

    private static readonly float[] RavineSteers = { 0f, 15f, -15f, 30f, -30f, 45f, -45f };
    private const float RiverBank = 3f;

    private static bool NearLine(List<Vector2> line, Vector2 point, float distance) => DistanceToLine(line, point) < distance;

    private static float DistanceToLine(List<Vector2> line, Vector2 point)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 along = line[i + 1] - line[i];
            float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
            best = Mathf.Min(best, (line[i] + along * t - point).sqrMagnitude);
        }

        return Mathf.Sqrt(best);
    }

    private static float Length(List<Vector2> line)
    {
        float length = 0f;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            length += Vector2.Distance(line[i], line[i + 1]);
        }

        return length;
    }

    // The regions, or the run's mood as one region before regions are placed (tests that plan by parts).
    private static List<RaidLayoutRegion> RegionsOrMood(RaidLayoutPlan plan) =>
        plan.Regions.Count > 0 ? plan.Regions : new List<RaidLayoutRegion> { new RaidLayoutRegion { Mood = plan.Mood } };

    // A region's share of a count meant for the whole map, by how much of the map it covers (the start's reaches furthest): rounded by
    // chance, so small counts still come up.
    private static int Share(System.Random random, int count, RaidLayoutPlan plan, RaidLayoutRegion region)
    {
        if (plan.Regions.Count <= 1)
        {
            return count;
        }

        float total = 0f;
        foreach (RaidLayoutRegion each in plan.Regions)
        {
            total += each.Reach * each.Reach;
        }

        float share = count * region.Reach * region.Reach / Mathf.Max(0.01f, total);
        return Mathf.FloorToInt(share) + (random.NextDouble() < share - Mathf.Floor(share) ? 1 : 0);
    }

    // The most any region's mood gives of something, so a count can be thinned by chance in the others.
    private static float MostOf(RaidLayoutPlan plan, System.Func<RaidMapMood, float> value)
    {
        float most = 0f;
        foreach (RaidLayoutRegion region in RegionsOrMood(plan))
        {
            most = Mathf.Max(most, value(region.Mood));
        }

        return most;
    }

    private static bool LineWithin(List<List<Vector2>> lines, Vector2 point, float distance)
    {
        float squared = distance * distance;
        foreach (List<Vector2> line in lines)
        {
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 along = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
                if ((line[i] + along * t - point).sqrMagnitude < squared)
                {
                    return true;
                }
            }
        }

        return false;
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

    private static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        float Side(Vector2 from, Vector2 to, Vector2 point) => (to.x - from.x) * (point.y - from.y) - (to.y - from.y) * (point.x - from.x);
        return Side(a, b, c) * Side(a, b, d) < 0f && Side(c, d, a) * Side(c, d, b) < 0f;
    }
}
