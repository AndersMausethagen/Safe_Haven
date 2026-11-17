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
    /// <summary>A smaller rock hugging its cluster's first one (RaidLayoutRules › Boulder Outcrops): set deeper into the ground.</summary>
    public bool Outcrop;
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
    /// <summary>On a map of the Valley shape, the valley (its river is the first of <see cref="Rivers"/>); null on any other.</summary>
    public RaidLayoutValley Valley;
    /// <summary>Where trails cross the valley's river, at fords and bridges.</summary>
    public readonly List<RaidLayoutCrossing> Crossings = new List<RaidLayoutCrossing>();
    /// <summary>Tall things to find the way by.</summary>
    public readonly List<RaidLayoutLandmark> Landmarks = new List<RaidLayoutLandmark>();
    /// <summary>Short flags beside the two ways to the exit ahead (RaidLayoutRules › Short Flags), placed last.</summary>
    public readonly List<RaidLayoutFlag> Flags = new List<RaidLayoutFlag>();

    /// <summary>True when the trail of this link is one of the two ways to the exit ahead: a road (RaidLayoutRules › Road Width).</summary>
    public bool IsRoad(int link) => ShortRoute.Contains(link) || LongRoute.Contains(link);

    /// <summary>The index of the region a point lies in (as <see cref="MoodAt"/> finds it); 0 on a map of one region.</summary>
    public int RegionIndexAt(Vector2 point)
    {
        if (Regions.Count <= 1)
        {
            return 0;
        }

        Nearest(point, out RaidLayoutRegion first, out _, out _, out _);
        return Regions.IndexOf(first);
    }

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

/// <summary>A region of a large generated map: the land nearest its centre has its mood (forest, ridges, cliffs, ravines, hills).</summary>
public sealed class RaidLayoutRegion
{
    public Vector2 Centre;
    public RaidMapMood Mood;
    /// <summary>How far its land reaches against the others' (distances are divided by it): the start's region reaches furthest.</summary>
    public float Reach = 1f;
    /// <summary>What the district is for (G1): the start's the Approach, the one round the apex the Lair, unless its mood says otherwise.</summary>
    public RaidDistrictRole Role = RaidDistrictRole.Approach;
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
    /// <summary>The rules' landmark it is, by index, for one placed at a node (RaidLandmarkRule.atNodes); -1: by <see cref="Pick"/>.</summary>
    public int Rule = -1;
}

/// <summary>A short flag beside a road (RaidLayoutRules › Short Flags), in metres from the map's south-west corner.</summary>
public sealed class RaidLayoutFlag
{
    public Vector2 Position;
    /// <summary>Degrees about up: it faces the trail.</summary>
    public float Turn;
    /// <summary>0-1: which of the rules' flags.</summary>
    public float Pick;
}
