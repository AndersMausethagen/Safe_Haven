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
    /// <summary>Which ground level it stands on, 0 the lowest, on a mood with terraces (RaidMapMood.terraces).</summary>
    public int Level;
}

/// <summary>
/// A ravine on a generated map, in metres from the map's south-west corner: a deep cut with sheer walls along a line. Where a trail
/// crosses it, a neck of solid ground carries the trail over. Its floor only falls along the line, so a river on it runs from the
/// first point to the last.
/// </summary>
public sealed class RaidLayoutRavine
{
    /// <summary>The middle of the cut.</summary>
    public readonly List<Vector2> Line = new List<Vector2>();
    /// <summary>Half the width from rim to rim at each point of the line.</summary>
    public readonly List<float> HalfWidths = new List<float>();
    /// <summary>The least depth below the rim.</summary>
    public float Depth;
    public bool River;
    /// <summary>The ground's height at each point before the cut, above the map's lowest point. RaidLayoutField works it out.</summary>
    public float[] Rim = System.Array.Empty<float>();
    /// <summary>The floor's height at each point. RaidLayoutField works it out.</summary>
    public float[] Floor = System.Array.Empty<float>();
    /// <summary>The segments (from point i to i + 1) where the floor drops by a cliff and a river falls. RaidLayoutField works them out.</summary>
    public readonly List<int> Cascades = new List<int>();
    /// <summary>The mood of the region it runs through: its width, depth, mist and river.</summary>
    public RaidMapMood Mood;
    /// <summary>Its first point is an open end whose floor slopes up to the rim: the one way down (RaidMapMood.ravineDescents).</summary>
    public bool Descent;
    /// <summary>How much it narrows at each point towards its closed ends (1 in its middle).</summary>
    public readonly List<float> Tapers = new List<float>();

    private const float MinWall = 1.3f;
    private const float WallShare = 0.22f;

    /// <summary>How far a wall's ragged edge wanders out past the half width, in metres.</summary>
    public const float RagReach = 1.8f;

    /// <summary>Metres over which a wall climbs from the floor to the rim: sheer.</summary>
    public static float WallRun(float halfWidth) => Mathf.Max(MinWall, halfWidth * WallShare);

    public float WidestHalf
    {
        get
        {
            float widest = 0f;
            foreach (float half in HalfWidths)
            {
                widest = Mathf.Max(widest, half);
            }

            return widest;
        }
    }

    /// <summary>The nearest point of the line: its segment, how far along it (0-1) and how far away, in metres.</summary>
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

    /// <summary>A value kept per point of the line (the widths, rim or floor), at a place along a segment.</summary>
    public static float At(IReadOnlyList<float> values, int segment, float along) =>
        values.Count == 0 ? 0f : Mathf.Lerp(values[Mathf.Min(segment, values.Count - 1)], values[Mathf.Min(segment + 1, values.Count - 1)], along);
}

/// <summary>A great hill on a generated map, in metres from the map's south-west corner: a broad rise with gentle flanks.</summary>
public sealed class RaidLayoutHill
{
    public Vector2 Position;
    public float Radius;
    public float Height;

    /// <summary>How much it raises the ground at a point: full at the top, easing out to nothing at its radius.</summary>
    public float RiseAt(Vector2 point)
    {
        float squared = (point - Position).sqrMagnitude;
        if (squared >= Radius * Radius)
        {
            return 0f;
        }

        return Height * Mathf.SmoothStep(0f, 1f, 1f - Mathf.Sqrt(squared) / Radius);
    }
}

/// <summary>A great boulder on a generated map, in metres from the map's south-west corner.</summary>
public sealed class RaidLayoutBoulder
{
    public Vector2 Position;
    public float Scale;
    /// <summary>Degrees about up.</summary>
    public float Turn;
    /// <summary>0-1: which piece of the kit, by weight.</summary>
    public float Pick;
}

/// <summary>A sheer rock spire on a generated map, in metres from the map's south-west corner: too steep to climb, so nothing goes on it.</summary>
public sealed class RaidLayoutSpire
{
    public Vector2 Position;
    public float Radius;
    public float Height;
    /// <summary>Which way its uneven outline lies, in radians.</summary>
    public float Turn;
    /// <summary>The height of its top above the map's lowest point. RaidLayoutField works it out.</summary>
    public float Top;

    private const float MaxWobble = 1.22f;

    public float Reach => Radius * MaxWobble;

    /// <summary>Metres from the point to the spire's edge: below zero it is on the spire.</summary>
    public float Edge(Vector2 point)
    {
        Vector2 offset = point - Position;
        float angle = Mathf.Atan2(offset.y, offset.x);
        float wobble = 1f + 0.14f * Mathf.Sin(angle * 3f + Turn) + 0.08f * Mathf.Sin(angle * 5f - Turn * 2f);
        return offset.magnitude - Radius * wobble;
    }
}

/// <summary>A pond on a generated map, in metres from the map's south-west corner: a slightly uneven oval.</summary>
public sealed class RaidLayoutPond
{
    public Vector2 Position;
    /// <summary>Half its length and half its width.</summary>
    public Vector2 Radii;
    /// <summary>Which way it lies, in radians.</summary>
    public float Turn;
    /// <summary>How deep it is at its middle, in metres.</summary>
    public float Depth;
    /// <summary>The height of its surface above the map's lowest point. RaidLayoutField works it out from the ground around it.</summary>
    public float Level;

    /// <summary>The longest way from its middle to its shore.</summary>
    public float Reach => Mathf.Max(Radii.x, Radii.y) * MaxWobble;

    private const float MaxWobble = 1.11f;

    private float Wobble(float angle) => 1f + 0.07f * Mathf.Sin(angle * 3f + Turn) + 0.04f * Mathf.Sin(angle * 7f - Turn);

    /// <summary>Metres from the point to the shore: below zero it is in the water.</summary>
    public float Edge(Vector2 point)
    {
        Vector2 offset = point - Position;
        float cos = Mathf.Cos(Turn), sin = Mathf.Sin(Turn);
        float along = (offset.x * cos + offset.y * sin) / Radii.x;
        float across = (offset.y * cos - offset.x * sin) / Radii.y;
        return (Mathf.Sqrt(along * along + across * across) / Wobble(Mathf.Atan2(across, along)) - 1f) * Mathf.Min(Radii.x, Radii.y);
    }

    /// <summary>The shore straight out from its middle towards the point, or the point itself when it is in the water.</summary>
    public Vector2 Shore(Vector2 point)
    {
        if (Edge(point) <= 0f)
        {
            return point;
        }

        Vector2 offset = point - Position;
        float cos = Mathf.Cos(Turn), sin = Mathf.Sin(Turn);
        float along = (offset.x * cos + offset.y * sin) / Radii.x;
        float across = (offset.y * cos - offset.x * sin) / Radii.y;
        return Rim(Mathf.Atan2(across, along), 0f);
    }

    /// <summary>The point <paramref name="beyond"/> metres out from the shore, in a direction from its middle.</summary>
    public Vector2 Rim(float angle, float beyond)
    {
        float wobble = Wobble(angle);
        float along = Mathf.Cos(angle) * (Radii.x * wobble + beyond);
        float across = Mathf.Sin(angle) * (Radii.y * wobble + beyond);
        float cos = Mathf.Cos(Turn), sin = Mathf.Sin(Turn);
        return Position + new Vector2(along * cos - across * sin, along * sin + across * cos);
    }
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
    /// <summary>The ponds, placed once everything else has its spot.</summary>
    public readonly List<RaidLayoutPond> Ponds = new List<RaidLayoutPond>();
    /// <summary>The authored places on this map.</summary>
    public readonly List<RaidLayoutPiece> Pieces = new List<RaidLayoutPiece>();
    /// <summary>The ravines, placed after the ponds, so they never move a site.</summary>
    public readonly List<RaidLayoutRavine> Ravines = new List<RaidLayoutRavine>();
    /// <summary>The rock spires nobody can climb, placed last.</summary>
    public readonly List<RaidLayoutSpire> Spires = new List<RaidLayoutSpire>();
    /// <summary>The great hills in the wild ground.</summary>
    public readonly List<RaidLayoutHill> Hills = new List<RaidLayoutHill>();
    /// <summary>The great boulders lying about.</summary>
    public readonly List<RaidLayoutBoulder> Boulders = new List<RaidLayoutBoulder>();
    /// <summary>How hilly this run is (Rules › Hill Variance): multiplies the rolling of the ground and the hills' height.</summary>
    public float Hilliness = 1f;
    /// <summary>The regions and their moods; one on a map of the normal size (the run's mood everywhere).</summary>
    public readonly List<RaidLayoutRegion> Regions = new List<RaidLayoutRegion>();
    /// <summary>Metres over which one region's look gives way to the next.</summary>
    public float RegionBlend = 30f;
    /// <summary>True when any region's clearings stand on levels (terraces).</summary>
    public bool HasTerraces;
    /// <summary>Metres between two levels.</summary>
    public float TerraceHeight = 7f;
    /// <summary>The clearing of the exit straight ahead (-1 when there is none), and the links of the short and the long way to it.</summary>
    public int MainExit = -1;
    public readonly HashSet<int> ShortRoute = new HashSet<int>();
    public readonly HashSet<int> LongRoute = new HashSet<int>();
    /// <summary>The rivers across the open land.</summary>
    public readonly List<RaidLayoutRiver> Rivers = new List<RaidLayoutRiver>();
    /// <summary>Tall things to find the way by.</summary>
    public readonly List<RaidLayoutLandmark> Landmarks = new List<RaidLayoutLandmark>();

    private const float RegionWarp = 45f;

    /// <summary>The mood of the region a point lies in, in metres from the map's corner: the run's mood on a map of one region.</summary>
    public RaidMapMood MoodAt(Vector2 point)
    {
        if (Regions.Count <= 1)
        {
            return Mood;
        }

        Nearest(point, out RaidLayoutRegion first, out _, out _, out _);
        return first.Mood;
    }

    /// <summary>A mood's value at a point, eased from one region's to the next's across their border.</summary>
    public float Blend(Vector2 point, System.Func<RaidMapMood, float> value)
    {
        if (Regions.Count <= 1)
        {
            return value(Mood);
        }

        Nearest(point, out RaidLayoutRegion first, out float near, out RaidLayoutRegion second, out float far);
        float share = Mathf.SmoothStep(0f, 1f, 0.5f + (far - near) / Mathf.Max(1f, RegionBlend));
        return Mathf.Lerp(value(second.Mood), value(first.Mood), share);
    }

    // The two nearest region centres, by distance over reach, from a point shifted by broad noise so borders wander.
    private void Nearest(Vector2 point, out RaidLayoutRegion first, out float near, out RaidLayoutRegion second, out float far)
    {
        var warped = new Vector2(
            point.x + (Mathf.PerlinNoise((point.x + NoiseOffset.x) / 90f, (point.y + NoiseOffset.y + 333f) / 90f) - 0.5f) * 2f * RegionWarp,
            point.y + (Mathf.PerlinNoise((point.x + NoiseOffset.x - 333f) / 90f, (point.y + NoiseOffset.y) / 90f) - 0.5f) * 2f * RegionWarp);
        first = second = Regions[0];
        near = far = float.MaxValue;
        foreach (RaidLayoutRegion region in Regions)
        {
            float distance = Vector2.Distance(region.Centre, warped) / region.Reach;
            if (distance < near)
            {
                far = near;
                second = first;
                near = distance;
                first = region;
            }
            else if (distance < far)
            {
                far = distance;
                second = region;
            }
        }
    }

    /// <summary>True when the point lies within a river's water, or this many metres of its edge.</summary>
    public bool InRiver(Vector2 point, float beyond = 0f)
    {
        foreach (RaidLayoutRiver river in Rivers)
        {
            river.Nearest(point, out int segment, out float along, out float distance);
            if (distance < RaidLayoutRavine.At(river.HalfWidths, segment, along) + beyond)
            {
                return true;
            }
        }

        return false;
    }

    public RaidLayoutRoom Start => Rooms[0];

    /// <summary>The sites of one kind, in the order they were placed (the best spots first).</summary>
    public List<RaidSite> SitesOf(RaidSiteKind kind) => Sites.FindAll(site => site.Kind == kind);

    private const float DampPatchSize = 34f;

    /// <summary>True when the point, in metres from the map's corner, lies in a pond.</summary>
    public bool InWater(Vector2 point)
    {
        foreach (RaidLayoutPond pond in Ponds)
        {
            if ((point - pond.Position).sqrMagnitude < pond.Reach * pond.Reach && pond.Edge(point) < 0f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The ravine whose cut the point lies over, in metres from the map's corner, with the height of its rim there. A neck that carries a
    /// trail across lies within it too, as solid ground: the caller compares heights.
    /// </summary>
    public bool OverRavine(Vector2 point, out RaidLayoutRavine over, out float rim, float beyond = 0f)
    {
        foreach (RaidLayoutRavine ravine in Ravines)
        {
            ravine.Nearest(point, out int segment, out float along, out float distance);
            if (distance < RaidLayoutRavine.At(ravine.HalfWidths, segment, along) + beyond)
            {
                over = ravine;
                rim = RaidLayoutRavine.At(ravine.Rim, segment, along);
                return true;
            }
        }

        over = null;
        rim = 0f;
        return false;
    }

    /// <summary>How wet the ground is at a point, 0 to 1: full at the water's edge, fading with distance, with damp patches in the forest.</summary>
    public float Wetness(Vector2 point, float reach, float damp)
    {
        float wet = 0f;
        if (damp > 0f)
        {
            float patch = Mathf.PerlinNoise((point.x + NoiseOffset.x + 211f) / DampPatchSize, (point.y + NoiseOffset.y - 211f) / DampPatchSize);
            wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 0.8f, patch)) * damp;
        }

        foreach (RaidLayoutPond pond in Ponds)
        {
            float far = pond.Reach + reach;
            if (reach > 0f && (point - pond.Position).sqrMagnitude < far * far)
            {
                wet = Mathf.Max(wet, 1f - Mathf.Clamp01(pond.Edge(point) / reach));
            }
        }

        return wet;
    }
}

/// <summary>
/// Lays out a raid map from a seed and rules, the way roguelikes lay out a level: the start goes on the edge and the exits
/// across from it, clearings fill the land between, trails join them all (a spanning tree, so everything can be reached,
/// plus a few loops), and sites go where the kind of site belongs. Plain C#, so it can be tested; the same seed always
/// gives the same map. The shape and the mood are rolled apart from the rest, so a briefing can name them cheaply.
/// </summary>
public static partial class RaidLayoutPlanner
{
    private const float ExitSpread = 62f;
    private const float ExitJitter = 14f;
    private const float ExitArc = 250f;
    private const float RingShare = 0.34f;
    private const float SpineShare = 0.12f;
    private const float MinimumFromOtherSites = 7f;
    private const int RoomTries = 28;
    private const int SiteTries = 60;
    private const int WanderLevels = 3;
    private const int PondTries = 90;
    private const float PondShrink = 0.86f;
    private const float PondFromTrail = 1.5f;
    private const float PondFromSite = 5f;
    private const float PondFromStart = 18f;
    private const float PondFromPond = 10f;

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
        PlaceRegions(plan, rules, attempt);
        EnsureRoutes(plan, rules, attempt);
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
        return plan;
    }

    /// <summary>How hilly a run is: fixed by its seed, whichever attempt at the map is used.</summary>
    public static float Hilliness(RaidLayoutRules rules, int seed)
    {
        var random = new System.Random(RaidLayout.SeedFor(seed, "Hilliness"));
        return Range(random, rules.HillVariance.x, Mathf.Max(rules.HillVariance.x, rules.HillVariance.y));
    }

    private const float HillFromRoom = 0.55f;
    private const float HillSteepest = 0.35f;
    private const float HillFromRamp = 0.85f;
    private const float HillFromTrail = 0.3f;
    private const int HillTries = 40;
    // From a boulder's middle: the largest is about 9 m across (RaidMapGenerator.BoulderWidest).
    private const float BoulderFromTrail = 6.5f;
    private const float BoulderFromRoom = 5.5f;
    private const float BoulderFromSite = 9f;
    private const float BoulderSpread = 6f;
    private const int BoulderTries = 30;

    // Their tops in the wild ground and off the clearings; trails may climb their outer flanks. From their own stream, so nothing else moves.
    private static void PlaceHills(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        plan.Hilliness = Hilliness(rules, plan.Seed);
        float share = MostOf(plan, mood => mood.hills);
        if (rules.GreatHills.y <= 0 || share <= 0f)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Hills " + attempt));
        int wanted = Mathf.RoundToInt(random.Next(rules.GreatHills.x, rules.GreatHills.y + 1) * share);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        for (int i = 0; i < wanted; i++)
        {
            for (int tries = 0; tries < HillTries; tries++)
            {
                var hill = new RaidLayoutHill
                {
                    Radius = Range(random, rules.GreatHillRadius.x, rules.GreatHillRadius.y),
                    Height = Range(random, rules.GreatHillHeight.x, rules.GreatHillHeight.y) * plan.Hilliness,
                };
                float margin = rules.EdgeMargin;
                hill.Position = new Vector2(Range(random, margin, plan.Size - margin), Range(random, margin, plan.Size - margin));
                // As many as the region's mood wants, as high as it wants, and no steeper than a trail can climb over a flank.
                float here = plan.MoodAt(hill.Position).hills;
                if (random.NextDouble() * share > here)
                {
                    continue;
                }

                hill.Height = Mathf.Min(hill.Height * Mathf.Max(1f, here), hill.Radius * HillSteepest);
                if (HillFits(plan, hill, halfWidth))
                {
                    plan.Hills.Add(hill);
                    break;
                }
            }
        }
    }

    private static bool HillFits(RaidLayoutPlan plan, RaidLayoutHill hill, float halfWidth)
    {
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, hill.Position) < room.Radius + hill.Radius * HillFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, hill.Position) < hill.Radius * HillFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutHill other in plan.Hills)
        {
            if (Vector2.Distance(other.Position, hill.Position) < (other.Radius + hill.Radius) * 0.6f)
            {
                return false;
            }
        }

        // A ramp already climbs: a hill's flank on it as well would make it too steep, so only its gentle outer edge may reach one.
        for (int i = 0; i < plan.Links.Count && i < plan.Trails.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            if (plan.Rooms[a].Level != plan.Rooms[b].Level && NearLine(plan.Trails[i], hill.Position, halfWidth + hill.Radius * HillFromRamp))
            {
                return false;
            }
        }

        return !LineWithin(plan.Trails, hill.Position, halfWidth + hill.Radius * HillFromTrail);
    }

    // Clusters of a few in the wild ground, never on a trail, clearing, site, pond, ravine or spire.
    private static void PlaceBoulders(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        float share = MostOf(plan, mood => mood.boulders);
        if (rules.BoulderClusters.y <= 0 || share <= 0f || rules.BoulderKit is null)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Boulders " + attempt));
        int clusters = Mathf.RoundToInt(random.Next(rules.BoulderClusters.x, rules.BoulderClusters.y + 1) * share);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        for (int i = 0; i < clusters; i++)
        {
            for (int tries = 0; tries < BoulderTries; tries++)
            {
                var centre = new Vector2(Range(random, rules.EdgeMargin, plan.Size - rules.EdgeMargin), Range(random, rules.EdgeMargin, plan.Size - rules.EdgeMargin));
                if (random.NextDouble() * share > plan.MoodAt(centre).boulders || !BoulderFits(plan, centre, halfWidth))
                {
                    continue;
                }

                int count = random.Next(rules.BoulderClusterSize.x, rules.BoulderClusterSize.y + 1);
                for (int k = 0; k < count; k++)
                {
                    Vector2 spot = k == 0 ? centre : centre + Direction(Range(random, 0f, 360f)) * Range(random, 2f, BoulderSpread);
                    if (k == 0 || BoulderFits(plan, spot, halfWidth))
                    {
                        plan.Boulders.Add(new RaidLayoutBoulder
                        {
                            Position = spot,
                            Scale = Range(random, rules.BoulderScale.x, rules.BoulderScale.y) * (k == 0 ? 1f : 0.75f),
                            Turn = Range(random, 0f, 360f),
                            Pick = (float)random.NextDouble(),
                        });
                    }
                }

                break;
            }
        }
    }

    private static bool BoulderFits(RaidLayoutPlan plan, Vector2 spot, float halfWidth)
    {
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, spot) < room.Radius + BoulderFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < BoulderFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, spot) < radius + BoulderFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(spot) < BoulderFromRoom + 1f)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(spot, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + RaidLayoutRavine.RagReach + BoulderFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if (spire.Edge(spot) < BoulderFromRoom + 2f)
            {
                return false;
            }
        }

        return !LineWithin(plan.Trails, spot, halfWidth + BoulderFromTrail) && !LineWithin(plan.Spurs, spot, halfWidth + BoulderFromTrail)
            && !plan.InRiver(spot, BoulderFromRoom + RiverBank);
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

    // ----- Cliffs and ravines: only moods with terraces, ravines or spires run any of this -----

    // A ramp eases in and out (smoothstep), so its steepest stretch is 1.5 times its mean slope; 0.53 is about 28 degrees.
    private const float RampSteepest = 0.53f;
    private const float RampSmoothSteepness = 1.5f;
    private const float RavineStep = 4f;
    private const float RavineTurn = 9f;
    private const int RavineTries = 150;
    private const float RavineFromRoom = 2.5f;
    private const float RavineFromSite = 6f;
    private const float RavineFromPocket = 2f;
    private const float RavineFromPond = 4f;
    private const float RavineFromRavine = 12f;
    private const float RavineFromTrail = 1.5f;
    // How squarely a ravine must cross a trail (the sine of the angle between them), so the neck over it stays short.
    private const float RavineCrossing = 0.7f;
    private const float RavineShortest = 0.5f;
    private const float RavineWobble = 0.14f;
    private static readonly float[] RavineSteers = { 0f, 15f, -15f, 30f, -30f, 45f, -45f };
    private const float RavineTaper = 18f;
    private const float RavineEndWidth = 0.45f;
    private const float SpireFromTrail = 7f;
    private const float SpireFromRoom = 6f;
    private const float SpireFromSite = 8f;
    private const float SpireFromRavine = 5f;
    private const float SpireFromSpire = 10f;
    private const int SpireTries = 120;
    // Two trails this close (beyond their own sides), or a trail this close to a clearing it doesn't end at, share one level.
    private const float LevelsApart = 4f;
    private const float LevelSample = 2f;

    // Each clearing's level comes from broad noise, so high and low ground lie in patches. Then a trail never climbs more than one level,
    // nor more steeply than a walkable ramp, and what stands too close together to have a cliff between (a trail passing a clearing, two
    // trails side by side) shares one level: each time, the higher end comes down. It only ever lowers, so it always settles.
    private static void AssignLevels(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        RaidMapMood terraced = null;
        foreach (RaidLayoutRegion region in plan.Regions)
        {
            if (region.Mood.terraces > 1 && terraced == null)
            {
                terraced = region.Mood;
            }
        }

        if (plan.Regions.Count == 0 && plan.Mood != null && plan.Mood.terraces > 1)
        {
            terraced = plan.Mood;
        }

        if (terraced == null)
        {
            return;
        }

        plan.HasTerraces = true;
        plan.TerraceHeight = terraced.terraceHeight;
        RaidMapMood mood = terraced;
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            // Each clearing on its region's levels; the ways out lie low.
            RaidMapMood here = plan.MoodAt(room.Position);
            if (here.terraces <= 1 || room.Role == RaidRoomRole.Exit)
            {
                room.Level = 0;
                continue;
            }

            float noise = Mathf.PerlinNoise((room.Position.x + plan.NoiseOffset.x + 517f) / here.terraceScale, (room.Position.y + plan.NoiseOffset.y - 517f) / here.terraceScale);
            room.Level = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(0.25f, 0.75f, noise) * here.terraces), 0, here.terraces - 1);
        }

        List<List<int>> together = SharedLevels(plan, rules.TrailHalfWidth * plan.Mood.trails);
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < plan.Links.Count; i++)
            {
                (int a, int b) = plan.Links[i];
                RaidLayoutRoom first = plan.Rooms[a], second = plan.Rooms[b];
                int climb = Mathf.Abs(first.Level - second.Level);
                if (climb == 0)
                {
                    continue;
                }

                float walk = Length(plan.Trails[i]) - first.Radius - second.Radius;
                if (climb > 1 || walk <= 0f || RampSmoothSteepness * mood.terraceHeight / walk > RampSteepest)
                {
                    (first.Level > second.Level ? first : second).Level--;
                    changed = true;
                }
            }

            foreach (List<int> group in together)
            {
                int lowest = int.MaxValue;
                foreach (int room in group)
                {
                    lowest = Mathf.Min(lowest, plan.Rooms[room].Level);
                }

                foreach (int room in group)
                {
                    if (plan.Rooms[room].Level > lowest)
                    {
                        plan.Rooms[room].Level = lowest;
                        changed = true;
                    }
                }
            }
        }
    }

    // The clearings that must stand on one level: a trail's two ends with any clearing it passes close by, and the ends of two trails
    // that run close beside each other away from the clearings they start from.
    private static List<List<int>> SharedLevels(RaidLayoutPlan plan, float halfWidth)
    {
        var groups = new List<List<int>>();
        for (int i = 0; i < plan.Links.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            var trail = new List<List<Vector2>> { plan.Trails[i] };
            for (int r = 0; r < plan.Rooms.Count; r++)
            {
                RaidLayoutRoom room = plan.Rooms[r];
                if (r != a && r != b && LineWithin(trail, room.Position, room.Radius + halfWidth + LevelsApart))
                {
                    groups.Add(new List<int> { a, b, r });
                }
            }
        }

        float close = halfWidth * 2f + LevelsApart;
        for (int i = 0; i < plan.Links.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            List<Vector2> trail = plan.Trails[i];
            for (int j = i + 1; j < plan.Links.Count; j++)
            {
                (int c, int d) = plan.Links[j];
                var other = new List<List<Vector2>> { plan.Trails[j] };
                if (AnyClose(plan, trail, a, b, c, d, other, close))
                {
                    groups.Add(new List<int> { a, b, c, d });
                }
            }
        }

        return groups;
    }

    // Along the trail, outside every clearing either trail starts or ends at (where trails meet by design).
    private static bool AnyClose(RaidLayoutPlan plan, List<Vector2> trail, int a, int b, int c, int d, List<List<Vector2>> other, float close)
    {
        for (int k = 0; k + 1 < trail.Count; k++)
        {
            float length = Vector2.Distance(trail[k], trail[k + 1]);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / LevelSample));
            for (int n = 0; n <= steps; n++)
            {
                Vector2 point = Vector2.Lerp(trail[k], trail[k + 1], n / (float)steps);
                if (InRoom(plan, point, a) || InRoom(plan, point, b) || InRoom(plan, point, c) || InRoom(plan, point, d))
                {
                    continue;
                }

                if (LineWithin(other, point, close))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool InRoom(RaidLayoutPlan plan, Vector2 point, int room) =>
        Vector2.Distance(point, plan.Rooms[room].Position) < plan.Rooms[room].Radius + LevelsApart;

    // On a map with terraces, a site's level pocket keeps clear of ramps and of what stands on another level, or a cliff would cut it.
    private static bool PocketFits(RaidLayoutPlan plan, RaidLayoutRules rules, Vector2 spot, float radius)
    {
        if (!plan.HasTerraces || radius <= 0f)
        {
            return true;
        }

        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        int level = LevelAt(plan, spot);
        for (int i = 0; i < plan.Trails.Count && i < plan.Links.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            if ((plan.Rooms[a].Level != level || plan.Rooms[b].Level != level) && NearLine(plan.Trails[i], spot, radius + halfWidth + LevelsApart))
            {
                return false;
            }
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (room.Level != level && Vector2.Distance(room.Position, spot) < room.Radius + radius + LevelsApart)
            {
                return false;
            }
        }

        return true;
    }

    // The level of the nearest clearing or level trail.
    private static int LevelAt(RaidLayoutPlan plan, Vector2 spot)
    {
        int level = 0;
        float nearest = float.MaxValue;
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            float distance = Vector2.Distance(room.Position, spot) - room.Radius;
            if (distance < nearest)
            {
                nearest = distance;
                level = room.Level;
            }
        }

        for (int i = 0; i < plan.Trails.Count && i < plan.Links.Count; i++)
        {
            (int a, int b) = plan.Links[i];
            if (plan.Rooms[a].Level == plan.Rooms[b].Level && NearLine(plan.Trails[i], spot, nearest))
            {
                nearest = DistanceToLine(plan.Trails[i], spot);
                level = plan.Rooms[a].Level;
            }
        }

        return level;
    }

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

    // On a map with terraces, a trail between two levels is all ramp: a site and its level pocket stand on level trails only.
    private static bool OnRamp(RaidLayoutPlan plan, List<Vector2> trail)
    {
        if (!plan.HasTerraces)
        {
            return false;
        }

        int index = plan.Trails.IndexOf(trail);
        return index >= 0 && plan.Rooms[plan.Links[index].a].Level != plan.Rooms[plan.Links[index].b].Level;
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

    // From their own stream, after the ponds, so ravines never move a site. A map gets fewer when they can't find room.
    // Each region's ravines, as many as its mood wants for its share of the map, staying within it.
    private static void PlaceRavines(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Ravines " + attempt));
        foreach (RaidLayoutRegion region in RegionsOrMood(plan))
        {
            RaidMapMood mood = region.Mood;
            if (mood == null || mood.ravines.y <= 0)
            {
                continue;
            }

            int wanted = Share(random, random.Next(Mathf.Max(0, mood.ravines.x), mood.ravines.y + 1), plan, region);
            for (int i = 0; i < wanted; i++)
            {
                for (int tries = 0; tries < RavineTries; tries++)
                {
                    RaidLayoutRavine ravine = GrowRavine(plan, rules, mood, random);
                    if (ravine != null)
                    {
                        plan.Ravines.Add(ravine);
                        break;
                    }
                }
            }
        }
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

    // From a spot in the wild ground both ways, turning a little at each step, until it is long enough or would cut what it must not: a
    // clearing, a site, a pond, another ravine, the map's edge, or a trail it doesn't cross squarely (a crossing becomes a neck).
    private static RaidLayoutRavine GrowRavine(RaidLayoutPlan plan, RaidLayoutRules rules, RaidMapMood mood, System.Random random)
    {
        float half = Range(random, mood.ravineWidth.x, mood.ravineWidth.y) * 0.5f;
        float widest = half * (1f + RavineWobble);
        float length = Range(random, mood.ravineLength.x, mood.ravineLength.y);
        float margin = rules.EdgeMargin + widest;
        var start = new Vector2(Range(random, margin, plan.Size - margin), Range(random, margin, plan.Size - margin));
        float heading = Range(random, 0f, 360f);
        if (!RavineCanPass(plan, rules, start, Direction(heading), widest) || (plan.Regions.Count > 1 && plan.MoodAt(start) != mood))
        {
            return null;
        }

        var ahead = new List<Vector2>();
        var behind = new List<Vector2>();
        float grown = 0f;
        float headingAhead = heading, headingBehind = heading + 180f;
        float turnAhead = Range(random, -RavineTurn, RavineTurn), turnBehind = Range(random, -RavineTurn, RavineTurn);
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

        if (grown < length * RavineShortest)
        {
            return null;
        }

        var ravine = new RaidLayoutRavine
        {
            Depth = Range(random, mood.ravineDepth.x, mood.ravineDepth.y),
            River = random.NextDouble() < mood.riverChance,
            Mood = mood,
        };
        for (int i = behind.Count - 1; i >= 0; i--)
        {
            ravine.Line.Add(behind[i]);
        }

        ravine.Line.Add(start);
        ravine.Line.AddRange(ahead);
        float noiseAt = Range(random, 0f, 500f);
        float run = 0f;
        float total = (ravine.Line.Count - 1) * RavineStep;
        for (int i = 0; i < ravine.Line.Count; i++)
        {
            run += i > 0 ? Vector2.Distance(ravine.Line[i - 1], ravine.Line[i]) : 0f;
            // Narrowing towards both ends, so a ravine closes up rather than stopping in a round pit.
            float taper = Mathf.Lerp(RavineEndWidth, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Min(run, total - run) / RavineTaper));
            ravine.Tapers.Add(taper);
            ravine.HalfWidths.Add(half * taper * (1f + (Mathf.PerlinNoise(noiseAt + run / 23f, noiseAt) - 0.5f) * 2f * RavineWobble));
        }

        return ravine;

        // The turn itself drifts, so the ravine bends in long curves rather than zigzags; something in the way bends it further, up to
        // a steer each way, before it stops.
        bool Step(ref Vector2 tip, ref float facing, ref float turn, List<Vector2> into)
        {
            turn = Mathf.Clamp(turn + Range(random, -RavineTurn, RavineTurn) * 0.5f, -RavineTurn, RavineTurn);
            float side = random.Next(2) == 0 ? 1f : -1f;
            foreach (float steer in RavineSteers)
            {
                float heading = facing + turn + steer * side;
                Vector2 next = tip + Direction(heading) * RavineStep;
                // Begun in its region, a ravine may run on into the next, as a gorge does.
                if (RavineCanPass(plan, rules, next, Direction(heading), widest) && !NearOwnLine(next, into))
                {
                    facing = heading;
                    into.Add(next);
                    tip = next;
                    grown += RavineStep;
                    return true;
                }
            }

            return false;
        }

        // A ravine bending back on itself would cut through its own walls.
        bool NearOwnLine(Vector2 point, List<Vector2> line)
        {
            for (int i = 0; i < line.Count - 3; i++)
            {
                if ((line[i] - point).sqrMagnitude < widest * widest * 4f)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static bool RavineCanPass(RaidLayoutPlan plan, RaidLayoutRules rules, Vector2 point, Vector2 heading, float half)
    {
        float margin = rules.EdgeMargin + half;
        if (point.x < margin || point.y < margin || point.x > plan.Size - margin || point.y > plan.Size - margin)
        {
            return false;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, point) < room.Radius + half + RavineFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, point) < half + RavineFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, point) < radius + half + RavineFromPocket)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(point) < half + RavineFromPond)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine other in plan.Ravines)
        {
            other.Nearest(point, out _, out _, out float distance);
            if (distance < half + other.WidestHalf + RavineFromRavine)
            {
                return false;
            }
        }

        // Near a trail only while crossing it squarely, so a neck stays short and no trail runs along an edge.
        float keep = rules.TrailHalfWidth * plan.Mood.trails + half + RavineFromTrail;
        return CrossesSquarely(plan.Trails, point, heading, keep) && CrossesSquarely(plan.Spurs, point, heading, keep);
    }

    private static bool CrossesSquarely(List<List<Vector2>> lines, Vector2 point, Vector2 heading, float keep)
    {
        float squared = keep * keep;
        foreach (List<Vector2> line in lines)
        {
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 along = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
                if ((line[i] + along * t - point).sqrMagnitude >= squared)
                {
                    continue;
                }

                Vector2 direction = along.normalized;
                if (Mathf.Abs(heading.x * direction.y - heading.y * direction.x) < RavineCrossing)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void PlaceSpires(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Spires " + attempt));
        foreach (RaidLayoutRegion region in RegionsOrMood(plan))
        {
            PlaceSpires(plan, rules, region, random);
        }
    }

    private static void PlaceSpires(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutRegion region, System.Random random)
    {
        RaidMapMood mood = region.Mood;
        if (mood == null || mood.spires.y <= 0)
        {
            return;
        }

        int wanted = Share(random, random.Next(Mathf.Max(0, mood.spires.x), mood.spires.y + 1), plan, region);
        for (int i = 0; i < wanted; i++)
        {
            for (int tries = 0; tries < SpireTries; tries++)
            {
                var spire = new RaidLayoutSpire
                {
                    Radius = Range(random, mood.spireRadius.x, mood.spireRadius.y),
                    Height = Range(random, mood.spireHeight.x, mood.spireHeight.y),
                    Turn = Range(random, 0f, Mathf.PI * 2f),
                };
                float margin = rules.EdgeMargin + rules.RimWidth + spire.Reach;
                spire.Position = new Vector2(Range(random, margin, plan.Size - margin), Range(random, margin, plan.Size - margin));
                if ((plan.Regions.Count <= 1 || plan.MoodAt(spire.Position) == mood) && SpireFits(plan, rules, spire))
                {
                    plan.Spires.Add(spire);
                    break;
                }
            }
        }
    }

    private static bool SpireFits(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutSpire spire)
    {
        Vector2 spot = spire.Position;
        float reach = spire.Reach;
        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            if (Vector2.Distance(room.Position, spot) < room.Radius + reach + SpireFromRoom)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < reach + SpireFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, spot) < radius + reach + SpireFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            if (pond.Edge(spot) < reach + SpireFromRoom)
            {
                return false;
            }
        }

        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            ravine.Nearest(spot, out _, out _, out float distance);
            if (distance < ravine.WidestHalf + reach + SpireFromRavine)
            {
                return false;
            }
        }

        foreach (RaidLayoutSpire other in plan.Spires)
        {
            if (Vector2.Distance(other.Position, spot) < other.Reach + reach + SpireFromSpire)
            {
                return false;
            }
        }

        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        return !LineWithin(plan.Trails, spot, reach + halfWidth + SpireFromTrail)
            && !LineWithin(plan.Spurs, spot, reach + halfWidth + SpireFromTrail);
    }


    // From their own stream, so adding water never moves a site. They go last and give way to everything already on the map:
    // beside a trail or at the edge of a clearing, so the player comes across them, and never where a trail would run through.
    // From their own stream, so a set-piece added to the rules never moves a site. Rumours first: a reward should stand somewhere.
    private static void PlaceSetPieces(RaidLayoutPlan plan, RaidLayoutRules rules, int seed, int attempt)
    {
        if (rules.SetPieces.Count == 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(seed, "Set Pieces " + attempt));
        var used = new List<RaidSetPieceDefinition>();
        // What is always there goes farthest from the apex: a boss's hollow shouldn't be on the great troll's round.
        List<RaidSite> rumours = plan.SitesOf(RaidSiteKind.Rumour);
        List<RaidSite> apex = plan.SitesOf(RaidSiteKind.Apex);
        if (apex.Count > 0)
        {
            Vector2 from = apex[0].Position;
            rumours.Sort((a, b) => Vector2.Distance(b.Position, from).CompareTo(Vector2.Distance(a.Position, from)));
        }

        foreach (RaidSite site in rumours)
        {
            AddPiece(plan, rules, site, true, used, random);
        }

        List<RaidSite> camps = plan.SitesOf(RaidSiteKind.EnemyCamp);
        int wanted = random.Next(rules.CampPieces.x, rules.CampPieces.y + 1);
        foreach (int index in RaidLayout.PickIndices(random, camps.Count, Mathf.Min(wanted, camps.Count)))
        {
            AddPiece(plan, rules, camps[index], false, used, random);
        }
    }

    private static void AddPiece(RaidLayoutPlan plan, RaidLayoutRules rules, RaidSite site, bool forRumour, List<RaidSetPieceDefinition> used, System.Random random)
    {
        RaidSetPieceDefinition piece = PickPiece(rules, plan.Mood != null ? plan.Mood.name : "", forRumour, used, random);
        if (piece is null)
        {
            return;
        }

        Vector2 spot = site.Position * plan.Size;
        if (!PocketFits(plan, rules, spot, piece.Radius))
        {
            return;
        }

        used.Add(piece);
        plan.Pieces.Add(new RaidLayoutPiece { Definition = piece, Position = spot, Turn = Range(random, 0f, 360f) });
        plan.Pockets.Add((spot, piece.Radius));
    }

    // By weight, and one not seen on this map yet while there is one.
    private static RaidSetPieceDefinition PickPiece(RaidLayoutRules rules, string mood, bool forRumour, List<RaidSetPieceDefinition> used, System.Random random)
    {
        var fresh = new List<RaidSetPieceDefinition>();
        var any = new List<RaidSetPieceDefinition>();
        foreach (RaidSetPieceDefinition piece in rules.SetPieces)
        {
            // Reference checks only: a run can be laid out off the main thread, where Unity's own null check can't run.
            if (piece is null || piece.Prefab is null || piece.Weight <= 0f || !piece.Suits(mood) || (forRumour && !piece.HoldsRumours))
            {
                continue;
            }

            if (forRumour && piece.AlwaysThere && !used.Contains(piece))
            {
                return piece;
            }

            any.Add(piece);
            if (!used.Contains(piece))
            {
                fresh.Add(piece);
            }
        }

        List<RaidSetPieceDefinition> from = fresh.Count > 0 ? fresh : any;
        float total = 0f;
        foreach (RaidSetPieceDefinition piece in from)
        {
            total += piece.Weight;
        }

        float roll = (float)random.NextDouble() * total;
        foreach (RaidSetPieceDefinition piece in from)
        {
            roll -= piece.Weight;
            if (roll <= 0f)
            {
                return piece;
            }
        }

        return from.Count > 0 ? from[from.Count - 1] : null;
    }

    private static void PlacePonds(RaidLayoutPlan plan, RaidLayoutRules rules, int attempt)
    {
        if (rules.Ponds.y <= 0)
        {
            return;
        }

        var random = new System.Random(RaidLayout.SeedFor(plan.Seed, "Ponds " + attempt));
        int wanted = random.Next(rules.Ponds.x, rules.Ponds.y + 1);

        // One draw from each slice of the size range, in random order, so a run's ponds never all come out alike.
        List<int> slices = RaidLayout.PickIndices(random, wanted, wanted);
        foreach (int slice in slices)
        {
            float share = (slice + (float)random.NextDouble()) / wanted;
            float radius = Mathf.Lerp(rules.PondRadius.x, rules.PondRadius.y, share);
            float stretch = Range(random, 1f, rules.PondStretch);
            float turn = Range(random, 0f, Mathf.PI * 2f);
            for (int tries = 0; tries < PondTries; tries++)
            {
                // A large pond that finds no room comes out smaller, down to the smallest size.
                if (tries > 0 && tries % 15 == 0)
                {
                    radius = Mathf.Max(rules.PondRadius.x, radius * PondShrink);
                }

                float depth = Mathf.Lerp(rules.PondDepth.x, rules.PondDepth.y, Mathf.InverseLerp(rules.PondRadius.x, rules.PondRadius.y, radius));
                var pond = new RaidLayoutPond { Radii = new Vector2(radius, radius / stretch), Turn = turn, Depth = depth };
                pond.Position = PondSpot(plan, rules, pond, random);
                if (PondFits(plan, rules, pond))
                {
                    plan.Ponds.Add(pond);
                    break;
                }
            }
        }
    }

    private static Vector2 PondSpot(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutPond pond, System.Random random)
    {
        if (plan.Trails.Count > 0 && random.Next(2) == 0)
        {
            List<Vector2> trail = plan.Trails[random.Next(plan.Trails.Count)];
            int at = random.Next(trail.Count - 1);
            Vector2 point = Vector2.Lerp(trail[at], trail[at + 1], (float)random.NextDouble());
            Vector2 side = Vector2.Perpendicular((trail[at + 1] - trail[at]).normalized) * (random.Next(2) == 0 ? -1f : 1f);
            return point + side * (rules.TrailHalfWidth * plan.Mood.trails + pond.Reach + Range(random, PondFromTrail, PondFromTrail + 5f));
        }

        RaidLayoutRoom room = plan.Rooms[random.Next(plan.Rooms.Count)];
        return room.Position + Direction(Range(random, 0f, 360f)) * (room.Radius + pond.Reach * Range(random, 0.2f, 0.9f));
    }

    private static bool PondFits(RaidLayoutPlan plan, RaidLayoutRules rules, RaidLayoutPond pond)
    {
        Vector2 spot = pond.Position;
        float reach = pond.Reach;
        float margin = rules.EdgeMargin + reach;
        if (spot.x < margin || spot.y < margin || spot.x > plan.Size - margin || spot.y > plan.Size - margin
            || Vector2.Distance(spot, plan.Start.Position) < reach + PondFromStart)
        {
            return false;
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            // It may lie at the edge of an ordinary clearing, but leaves the ones with a purpose whole.
            float keep = room.Role == RaidRoomRole.Plain ? room.Radius * 0.35f : room.Radius + 1f;
            if (Vector2.Distance(spot, room.Position) < reach + keep)
            {
                return false;
            }
        }

        foreach (RaidSite site in plan.Sites)
        {
            if (Vector2.Distance(site.Position * plan.Size, spot) < reach + PondFromSite)
            {
                return false;
            }
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            if (Vector2.Distance(centre, spot) < reach + radius)
            {
                return false;
            }
        }

        foreach (RaidLayoutPond other in plan.Ponds)
        {
            if (Vector2.Distance(other.Position, spot) < reach + other.Reach + PondFromPond)
            {
                return false;
            }
        }

        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        return !LineWithin(plan.Trails, spot, reach + halfWidth + PondFromTrail)
            && !LineWithin(plan.Spurs, spot, reach + halfWidth + PondFromTrail);
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
        PlaceOverRamps(plan, rules, quota, random);
        // Dead ends first, or only them; what stays unplaced finds a pocket along a trail. Camps take the short way to the exit first and
        // the long way last; what prefers height takes the highest clearings first.
        List<int> order = RaidLayout.PickIndices(random, plan.Rooms.Count, plan.Rooms.Count);
        if (quota.kind == RaidSiteKind.EnemyCamp && rules.CampsOnShortRoute && plan.ShortRoute.Count > 0)
        {
            HashSet<int> watched = RoomsOf(plan, plan.ShortRoute);
            HashSet<int> quiet = RoomsOf(plan, plan.LongRoute);
            quiet.ExceptWith(watched);
            order.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            int Rank(int room) => watched.Contains(room) ? 0 : quiet.Contains(room) ? 2 : 1;
        }
        else if (quota.prefersHeight)
        {
            var heights = new float[plan.Rooms.Count];
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = HeightOf(plan, plan.Rooms[i].Position);
            }

            order.Sort((a, b) => heights[b].CompareTo(heights[a]));
        }

        foreach (int index in order)
        {
            RaidLayoutRoom room = plan.Rooms[index];
            bool fits = room.Role == RaidRoomRole.Plain || room.Role == RaidRoomRole.Hub;
            if (!fits || (quota.placement == RaidSitePlacement.DeadEnd && room.Links > 1) || CountOf(plan, quota.kind) >= quota.count)
            {
                continue;
            }

            Vector2 spot = room.Position + Direction(Range(random, 0f, 360f)) * (room.Radius * Range(random, 0f, 0.5f));
            spot = Inside(spot, plan.Size, rules.EdgeMargin);
            if (CanStand(plan, quota, spot) && PocketFits(plan, rules, spot, quota.clearing))
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
        // Camps beside the short way to the exit, half the time; what prefers height takes the highest of a few tries.
        if (quota.kind == RaidSiteKind.EnemyCamp && rules.CampsOnShortRoute && plan.ShortRoute.Count > 0 && random.Next(2) == 0)
        {
            var watched = new List<int>(plan.ShortRoute);
            trail = plan.Trails[watched[random.Next(watched.Count)]];
            at = random.Next(trail.Count - 1);
            point = Vector2.Lerp(trail[at], trail[at + 1], (float)random.NextDouble());
        }
        else if (quota.prefersHeight)
        {
            for (int tries = 0; tries < 3; tries++)
            {
                List<Vector2> other = plan.Trails[random.Next(plan.Trails.Count)];
                int otherAt = random.Next(other.Count - 1);
                Vector2 candidate = Vector2.Lerp(other[otherAt], other[otherAt + 1], (float)random.NextDouble());
                if (HeightOf(plan, candidate) > HeightOf(plan, point))
                {
                    trail = other;
                    at = otherAt;
                    point = candidate;
                }
            }
        }

        if (OnRamp(plan, trail))
        {
            return;
        }

        if (quota.placement == RaidSitePlacement.OnPath)
        {
            if (CanStand(plan, quota, point) && PocketFits(plan, rules, point, quota.clearing))
            {
                AddSite(plan, quota, point);
            }

            return;
        }

        Vector2 side = Vector2.Perpendicular((trail[at + 1] - trail[at]).normalized) * (random.Next(2) == 0 ? -1f : 1f);
        Vector2 spot = point + side * Range(random, quota.offPath.x, quota.offPath.y);
        if (spot.x < rules.EdgeMargin || spot.y < rules.EdgeMargin || spot.x > plan.Size - rules.EdgeMargin || spot.y > plan.Size - rules.EdgeMargin
            || !CanStand(plan, quota, spot) || !PocketFits(plan, rules, spot, quota.clearing))
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
