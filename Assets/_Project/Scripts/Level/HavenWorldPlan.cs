using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Where everything in the Haven world goes, worked out from a HavenWorldRecipe and its seed before anything touches the scene:
/// the lake and its islands, the mountain, paths running out from the Haven and the ring joining them, rivers down to the lake, ponds,
/// points of interest, rock outcrops, cliff lines up the mountain, ravines in the woods, maple groves, spawn zones and resource
/// clusters, and the ground's shape under them (Height, in metres above the portal's ground, world x and z). Plain C#, so it can be
/// tested and so a game could one day roll a Haven of its own (a seed per new game); the same recipe and seed give the same world.
/// HavenWorldGenerator (editor) turns it into terrain and World Builder features.
/// <para>
/// What a world holds, Make (the order of the steps, each drawing from the seed's stream in turn) and the shared helpers
/// are here; the steps are in parts: .Ground, .Water, .Paths, .Woodland, .Rock, .Resources and .AlongTheWay. Moving
/// code between them changes nothing; reordering draws within a step changes the world a seed makes.
/// </para>
/// </summary>
public sealed partial class HavenWorldPlan
{
    public sealed class Outcrop
    {
        public List<Vector2> Loop;
        public Vector2 Centre;
        public float Radius;
        public float Height;
    }

    public sealed class Ravine
    {
        public List<Vector2> Line;
        public float Depth;
        public float FloorWidth;
    }

    public sealed class Cliff
    {
        public List<Vector2> Line;
        public float Height;
        public bool HighOnLeft;
    }

    public sealed class Island
    {
        public Vector2 Centre;
        public float Radius;
        public float Height;
    }

    public sealed class Poi
    {
        public HavenPointOfInterest Rule;
        public Vector2 At;
        public float Turn;
    }

    public sealed class Zone
    {
        public HavenCreatureBand Band;
        public Vector2 At;
        public float Radius;
    }

    /// <summary>What a path is: a main path out from the Haven, the ring, a spur to a place, a loop between main paths, a woodland trail.</summary>
    public enum PathKind { Road, Ring, Spur, Loop, Trail }

    /// <summary>What a glade is for: open meadow (forage, plants), rocky (stone), a lair (creatures), wet (a pool in it).</summary>
    public enum GladeKind { Meadow, Rocky, Lair, Wet }

    /// <summary>An open glade in the woods with a ragged rim.</summary>
    public sealed class Glade
    {
        public Vector2 Centre;
        public float Radius;
        public GladeKind Kind;
        public float[] Wobble;
        public bool Landmark;
        public int LandmarkKind;
        public bool HasZone;

        /// <summary>How open the glade is at a point: 1 well inside, fading to 0 at its ragged rim.</summary>
        public float Open(Vector2 point)
        {
            Vector2 offset = point - Centre;
            float distance = offset.magnitude;
            if (distance > Radius * 1.35f)
            {
                return 0f;
            }

            float turns = Mathf.Repeat(Mathf.Atan2(offset.y, offset.x) / (Mathf.PI * 2f), 1f) * Wobble.Length;
            int i = Mathf.FloorToInt(turns) % Wobble.Length;
            float rim = Radius * Mathf.Lerp(Wobble[i], Wobble[(i + 1) % Wobble.Length], Mathf.SmoothStep(0f, 1f, turns - Mathf.Floor(turns)));
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(rim * 0.7f, rim, distance));
        }
    }

    public sealed class Node
    {
        public HavenResourceBand Band;
        public Vector2 At;
    }

    /// <summary>Something placed by the plan: which of a recipe list (Kind), where, and its turn (degrees).</summary>
    public sealed class Placed
    {
        public int Kind;
        public Vector2 At;
        public float Turn;
        public float Scale = 1f;
    }

    public sealed class Rock
    {
        public int Kind;
        public Vector2 At;
        public float Scale;
        public float Turn;
    }

    public readonly HavenWorldRecipe Recipe;
    public readonly float Half;
    public RaidLayoutPond Lake;
    public readonly List<Island> Islands = new List<Island>();
    public readonly List<RaidLayoutPond> Ponds = new List<RaidLayoutPond>();
    public readonly List<List<Vector2>> Paths = new List<List<Vector2>>();
    /// <summary>What each of <see cref="Paths"/> is, by index.</summary>
    public readonly List<PathKind> PathKinds = new List<PathKind>();
    public readonly List<Glade> Glades = new List<Glade>();
    /// <summary>Where a path crosses a river: the river's knot there (river index, knot index), made a broad, shallow ford.</summary>
    public readonly List<(int river, int knot)> Fords = new List<(int, int)>();
    // Each river's water level at each knot, running only downhill to the lake: the valley Height carves for it.
    private readonly List<float[]> riverLevels = new List<float[]>();
    public readonly List<List<Vector2>> Rivers = new List<List<Vector2>>();
    public readonly List<Poi> Pois = new List<Poi>();
    public readonly List<Outcrop> Outcrops = new List<Outcrop>();
    public readonly List<Cliff> Cliffs = new List<Cliff>();
    public readonly List<Ravine> Ravines = new List<Ravine>();
    public readonly List<List<Vector2>> Groves = new List<List<Vector2>>();
    public readonly List<Zone> Zones = new List<Zone>();
    public readonly List<Node> Nodes = new List<Node>();
    public float MountainPathAngle;
    private readonly List<float> radialBearings = new List<float>();
    /// <summary>Boat landings: where they stand on the shore and which way they run out over the water (bearing, degrees).</summary>
    public readonly List<(Vector2 at, float bearing)> Landings = new List<(Vector2, float)>();
    /// <summary>Rocks strewn over the mountain's flanks: which of the recipe's mountain rocks, its size and a turn added to facing downhill.</summary>
    public readonly List<Rock> Rocks = new List<Rock>();
    /// <summary>Small scenes beside the woods' paths (recipe.trailScenes).</summary>
    public readonly List<Placed> TrailScenes = new List<Placed>();
    /// <summary>Where a trail leaves another path: a marker stands there (recipe.junctionMarker).</summary>
    public readonly List<Placed> Junctions = new List<Placed>();
    /// <summary>What lies about each glade: Kind indexes that glade kind's dressing list.</summary>
    public readonly List<(Glade glade, Placed prop)> GladeProps = new List<(Glade, Placed)>();

    private readonly System.Random random;
    private readonly Vector2 noise;

    private const int Tries = 120;
    private const float PathFromRim = 25f;

    /// <summary>The seed this world was planned from.</summary>
    public readonly int Seed;

    private HavenWorldPlan(HavenWorldRecipe recipe, int seed)
    {
        Recipe = recipe;
        Seed = seed;
        Half = recipe.size * 0.5f;
        random = new System.Random(seed);
        noise = new Vector2(Range(0f, 1000f), Range(0f, 1000f));
    }

    /// <summary>Plans the world from the recipe, with its own seed or another (a new game's).</summary>
    public static HavenWorldPlan Make(HavenWorldRecipe recipe, int? seed = null)
    {
        var plan = new HavenWorldPlan(recipe, seed ?? recipe.seed);
        plan.PlaceLake();
        plan.MountainPathAngle = recipe.mountainAt.x + plan.Range(-8f, 8f);
        plan.PlacePaths();
        plan.PlaceRivers();
        plan.PlacePonds();
        plan.PlacePois();
        plan.PlaceOutcrops();
        plan.PlaceMountainCliffs();
        plan.PlaceRavines();
        plan.PlaceGroves();
        plan.PlaceOuterLoops();
        plan.PlaceGlades();
        plan.PlaceFords();
        plan.LevelRivers();
        plan.PlaceZones();
        plan.PlaceNodes();
        plan.PlaceLandings();
        // Last, so the rocks and the dressing never move what was placed before them.
        plan.PlaceMountainRocks();
        plan.PlaceJunctions();
        plan.PlaceTrailScenes();
        plan.PlaceGladeDressing();
        return plan;
    }

    /// <summary>Direction of a bearing (degrees, 0 north, clockwise) on the ground (x east, y north).</summary>
    public static Vector2 Dir(float degrees) => new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));

    public static float BearingOf(Vector2 point) => Mathf.Repeat(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg, 360f);

    private float Perlin(Vector2 point, float scale, float salt) => Mathf.PerlinNoise((point.x + noise.x + salt) / scale, (point.y + noise.y - salt) / scale);

    private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

    private Vector2 Around(float from, float to) => Dir(Range(0f, 360f)) * Range(from, to);

    private float MaxReach => Half - Recipe.rim.y - PathFromRim;

    public static bool Near(List<List<Vector2>> lines, Vector2 point, float distance)
    {
        foreach (List<Vector2> line in lines)
        {
            if (NearLine(line, point, distance))
            {
                return true;
            }
        }

        return false;
    }

    public static bool NearLine(List<Vector2> line, Vector2 point, float distance)
    {
        float squared = distance * distance;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 along = line[i + 1] - line[i];
            float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
            if ((line[i] + along * t - point).sqrMagnitude < squared)
            {
                return true;
            }
        }

        return false;
    }

    public static Vector2 NearestOn(List<List<Vector2>> lines, Vector2 point)
    {
        Vector2 best = point;
        float closest = float.MaxValue;
        foreach (List<Vector2> line in lines)
        {
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 along = line[i + 1] - line[i];
                float t = Mathf.Clamp01(Vector2.Dot(point - line[i], along) / Mathf.Max(0.0001f, along.sqrMagnitude));
                Vector2 on = line[i] + along * t;
                float squared = (on - point).sqrMagnitude;
                if (squared < closest)
                {
                    closest = squared;
                    best = on;
                }
            }
        }

        return best;
    }
}
