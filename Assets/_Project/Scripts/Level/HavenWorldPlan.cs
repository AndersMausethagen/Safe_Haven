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
/// </summary>
public sealed class HavenWorldPlan
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
    private const float ValleyFloor = 5f;
    private const float ValleyMax = 40f;
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
    private const float PathStart = 32f;
    private const float PathFromRim = 25f;
    private const float RiverFromHome = 95f;
    private const float LakeBank = 25f;
    private const float LakeSlope = 18f;
    private const float PondBank = 4f;
    private const float RavineStep = 6f;
    private const float RavineTurn = 10f;

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

    // ----- The ground -----

    /// <summary>Direction of a bearing (degrees, 0 north, clockwise) on the ground (x east, y north).</summary>
    public static Vector2 Dir(float degrees) => new Vector2(Mathf.Sin(degrees * Mathf.Deg2Rad), Mathf.Cos(degrees * Mathf.Deg2Rad));

    public static float BearingOf(Vector2 point) => Mathf.Repeat(Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg, 360f);

    private float Perlin(Vector2 point, float scale, float salt) => Mathf.PerlinNoise((point.x + noise.x + salt) / scale, (point.y + noise.y - salt) / scale);

    /// <summary>0 in the Haven's ring, 1 in the Misty Woods, with a wandering border.</summary>
    public float WoodsWeight(Vector2 point)
    {
        float wander = (Perlin(point, 70f, 41f) - 0.5f) * 60f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.haven, Recipe.forest, point.magnitude + wander));
    }

    /// <summary>How much of the mountain stands at a point, 0 to 1.</summary>
    public float MountainWeight(Vector2 point)
    {
        float half = Recipe.mountainAt.y * 0.5f;
        float off = Mathf.Abs(Mathf.DeltaAngle(BearingOf(point), Recipe.mountainAt.x));
        float across = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.55f, half, off));
        float along = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.mountainFrom, Recipe.mountainFrom + 170f, point.magnitude));
        return across * along;
    }

    /// <summary>The ground before the lake and ponds are dug, in metres above the portal's ground.</summary>
    public float Ground(Vector2 point)
    {
        float r = point.magnitude;
        float far = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.buildable, Half, r));
        float hills = (Perlin(point, Recipe.hillScale, 0f) - 0.5f) * 2f * Mathf.Lerp(Recipe.havenHills, Recipe.outerHills, far);
        // Rounded crests (a parabola, not the V of ridged noise) on a wide ramp: ridges you see in the light and walk over, whose
        // sides stay under the trees' 36 degrees. Sharp ones left a sixth of the woods bare, pale ribbons that read as paths.
        float fold = Perlin(point, Recipe.hillScale * 0.6f, 97f) * 2f - 1f;
        float ridged = 1f - fold * fold;
        float ridge = Recipe.ridges * WoodsWeight(point) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, ridged));
        // Gentle and open where the Haven is built: the ward reaches about 50 m.
        float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.buildable, Recipe.buildable + 70f, r));
        float near = (Perlin(point, 25f, 13f) - 0.5f) * 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 40f, r));
        float height = (hills + ridge) * open + near * (1f - open);
        height += Recipe.mountainHeight * MountainWeight(point) * (0.7f + 0.3f * ridged);
        // The edge of the world rises into a broken, rocky ridge (steep enough for the terrain's rock), not a smooth wall.
        float edge = Half - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y));
        float rim = Mathf.Clamp01(1f - edge / Recipe.rim.y);
        float crest = 1f - Mathf.Abs(Perlin(point, 38f, 211f) * 2f - 1f);
        float rocky = Mathf.Lerp(0.55f, 1.35f, crest) + (Perlin(point, 11f, 223f) - 0.5f) * 0.3f;
        return height + Recipe.rim.x * Mathf.Pow(rim, 1.6f) * rocky;
    }

    /// <summary>The ground with the lake, its islands and the ponds dug, in metres above the portal's ground.</summary>
    public float Height(Vector2 point)
    {
        float ground = Ground(point);
        if (Lake != null && (point - Lake.Position).sqrMagnitude < (Lake.Reach + LakeBank) * (Lake.Reach + LakeBank))
        {
            float edge = Lake.Edge(point);
            if (edge < LakeBank)
            {
                ground = edge >= 0f
                    ? Mathf.Lerp(Lake.Level, ground, Mathf.SmoothStep(0f, 1f, edge / LakeBank))
                    : Lake.Level - Lake.Depth * Mathf.SmoothStep(0f, 1f, -edge / LakeSlope);
                foreach (Island island in Islands)
                {
                    float d = Vector2.Distance(island.Centre, point);
                    if (d < island.Radius * 1.6f)
                    {
                        float dome = Mathf.SmoothStep(0f, 1f, 1f - d / (island.Radius * 1.6f));
                        ground = Mathf.Max(ground, Mathf.Lerp(Lake.Level - Lake.Depth, Lake.Level + island.Height, dome));
                    }
                }
            }
        }

        ground = RiverValley(point, ground);
        foreach (RaidLayoutPond pond in Ponds)
        {
            if ((point - pond.Position).sqrMagnitude >= (pond.Reach + PondBank) * (pond.Reach + PondBank))
            {
                continue;
            }

            float edge = pond.Edge(point);
            if (edge >= PondBank)
            {
                continue;
            }

            ground = edge >= 0f
                ? Mathf.Lerp(pond.Level, ground, Mathf.SmoothStep(0f, 1f, edge / PondBank))
                : pond.Level - pond.Depth * Mathf.SmoothStep(0f, 1f, -edge / Mathf.Min(3f, Mathf.Min(pond.Radii.x, pond.Radii.y) * 0.8f));
        }

        return ground;
    }

    // The water lies just under the lowest ground around it, so it never stands above its own banks.
    private float LevelAround(RaidLayoutPond water, float beyond, float below)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < 32; i++)
        {
            lowest = Mathf.Min(lowest, Ground(water.Rim(i * Mathf.PI * 2f / 32f, beyond)));
        }

        return lowest - below;
    }

    // ----- Placing -----

    private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

    private Vector2 Around(float from, float to) => Dir(Range(0f, 360f)) * Range(from, to);

    private float MaxReach => Half - Recipe.rim.y - PathFromRim;

    private void PlaceLake()
    {
        Lake = new RaidLayoutPond
        {
            Position = Dir(Recipe.lakeAt.x) * Recipe.lakeAt.y,
            Radii = Recipe.lakeSize,
            Turn = Range(0f, Mathf.PI * 2f),
            Depth = Recipe.lakeDepth,
        };
        Lake.Level = LevelAround(Lake, 6f, 0.5f);
        int count = random.Next(Recipe.islands.x, Recipe.islands.y + 1);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                float radius = Range(Recipe.islandRadius.x, Recipe.islandRadius.y);
                Vector2 at = Lake.Position + new Vector2(Range(-1f, 1f) * Lake.Radii.x * 0.6f, Range(-1f, 1f) * Lake.Radii.y * 0.6f);
                if (Lake.Edge(at) > -(radius * 1.6f + 10f) || Islands.Exists(other => Vector2.Distance(other.Centre, at) < other.Radius + radius + 14f))
                {
                    continue;
                }

                Islands.Add(new Island { Centre = at, Radius = radius, Height = Range(2f, 4.5f) });
                break;
            }
        }
    }

    // Out from the Haven's edge in every direction: one to the lake shore (where a boat is put in), one up into the mountain, the rest to
    // the edge of the woods. A ring joins them, except across the lake and the mountain.
    private void PlacePaths()
    {
        int count = Mathf.Max(0, Recipe.radialPaths);
        float turn = Range(0f, 360f / Mathf.Max(1, count));
        float lakeBearing = BearingOf(Lake.Position);
        var bearings = new List<float>();
        for (int i = 0; i < count; i++)
        {
            bearings.Add(turn + i * 360f / count);
        }

        Snap(bearings, lakeBearing);
        Snap(bearings, MountainPathAngle);
        foreach (float bearing in bearings)
        {
            float end = MaxReach;
            if (Mathf.Abs(Mathf.DeltaAngle(bearing, lakeBearing)) < 1f)
            {
                end = Vector2.Distance(Vector2.zero, Lake.Rim(Mathf.Atan2(-Lake.Position.y, -Lake.Position.x) - Lake.Turn, 3f));
            }

            Vector2 from = Dir(bearing) * PathStart, to = Dir(bearing) * end;
            AddPath(Wander(from, to, 0.08f), PathKind.Road);
        }

        radialBearings.AddRange(bearings);

        if (Recipe.ringPath <= 0f)
        {
            return;
        }

        bearings.Sort();
        for (int i = 0; i < bearings.Count; i++)
        {
            float a = bearings[i], b = bearings[(i + 1) % bearings.Count] + (i + 1 == bearings.Count ? 360f : 0f);
            var arc = new List<Vector2>();
            bool open = true;
            for (float t = a; t <= b + 0.01f; t += 8f)
            {
                Vector2 point = Dir(t) * (Recipe.ringPath + (Perlin(Dir(t) * 50f, 30f, 7f) - 0.5f) * 30f);
                if (Lake.Edge(point) < 12f || MountainWeight(point) > 0.25f)
                {
                    open = false;
                    break;
                }

                arc.Add(point);
            }

            if (open && arc.Count >= 3)
            {
                AddPath(arc, PathKind.Ring);
            }
        }
    }

    // The radial closest to a bearing turns to it.
    private static void Snap(List<float> bearings, float to)
    {
        int best = 0;
        for (int i = 1; i < bearings.Count; i++)
        {
            if (Mathf.Abs(Mathf.DeltaAngle(bearings[i], to)) < Mathf.Abs(Mathf.DeltaAngle(bearings[best], to)))
            {
                best = i;
            }
        }

        if (bearings.Count > 0)
        {
            bearings[best] = to;
        }
    }

    // Midpoint displacement: each pass splits every stretch and pushes the middle aside, less each time.
    private List<Vector2> Wander(Vector2 from, Vector2 to, float share)
    {
        var line = new List<Vector2> { from, to };
        float reach = Vector2.Distance(from, to) * share;
        for (int level = 0; level < 4; level++)
        {
            var next = new List<Vector2>(line.Count * 2) { line[0] };
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 side = Vector2.Perpendicular((line[i + 1] - line[i]).normalized);
                next.Add((line[i] + line[i + 1]) * 0.5f + side * Range(-reach, reach));
                next.Add(line[i + 1]);
            }

            line = next;
            reach *= 0.5f;
        }

        return line;
    }

    // From the mountain down to the lake, and from the far edge of the woods past the Haven (water near home) to the lake.
    private void PlaceRivers()
    {
        int count = random.Next(Recipe.rivers.x, Recipe.rivers.y + 1);
        Vector2 mouth(Vector2 from) => Lake.Rim(Mathf.Atan2(from.y - Lake.Position.y, from.x - Lake.Position.x) - Lake.Turn, -6f);
        for (int i = 0; i < count; i++)
        {
            List<Vector2> line;
            if (i == 0)
            {
                Vector2 source = Dir(Recipe.mountainAt.x + Range(-15f, 15f)) * (MaxReach - 10f);
                line = Wander(source, mouth(source), 0.12f);
            }
            else
            {
                float side = Recipe.lakeAt.x + 180f + Range(-50f, 50f);
                Vector2 source = Dir(side) * MaxReach;
                Vector2 past = Dir(side + Range(50f, 80f) * (i % 2 == 0 ? 1f : -1f)) * (Recipe.buildable + RiverFromHome);
                line = Wander(source, past, 0.1f);
                List<Vector2> rest = Wander(past, mouth(past), 0.1f);
                rest.RemoveAt(0);
                line.AddRange(rest);
            }

            // Never through the buildable ground.
            for (int k = 0; k < line.Count; k++)
            {
                if (line[k].magnitude < Recipe.buildable + RiverFromHome * 0.7f)
                {
                    line[k] = line[k].normalized * (Recipe.buildable + RiverFromHome * 0.7f);
                }
            }

            Rivers.Add(line);
        }
    }

    private void PlacePonds()
    {
        int count = random.Next(Recipe.ponds.x, Recipe.ponds.y + 1);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                float radius = Range(Recipe.pondRadius.x, Recipe.pondRadius.y);
                var pond = new RaidLayoutPond
                {
                    Position = Around(Recipe.buildable + 25f, Recipe.haven + 40f),
                    Radii = new Vector2(radius, radius / Range(1f, 1.6f)),
                    Turn = Range(0f, Mathf.PI * 2f),
                    Depth = Mathf.Lerp(0.8f, 1.8f, Mathf.InverseLerp(Recipe.pondRadius.x, Recipe.pondRadius.y, radius)),
                };
                if (Near(Paths, pond.Position, pond.Reach + 6f) || Near(Rivers, pond.Position, pond.Reach + 12f)
                    || Ponds.Exists(other => Vector2.Distance(other.Position, pond.Position) < other.Reach + pond.Reach + 25f))
                {
                    continue;
                }

                pond.Level = LevelAround(pond, 2f, 0.15f);
                Ponds.Add(pond);
                break;
            }
        }
    }

    // Where the lake path meets the water, running out from the shore; and on the island holding a place, facing the Haven's shore.
    private void PlaceLandings()
    {
        Vector2 toLake = Lake.Position.normalized;
        float local = Mathf.Atan2(-Lake.Position.y, -Lake.Position.x) - Lake.Turn;
        Vector2 shore = Lake.Rim(local, 1.5f);
        Landings.Add((shore, BearingOf(toLake)));
        Island held = Islands.Find(island => Pois.Exists(poi => Vector2.Distance(poi.At, island.Centre) < 1f));
        if (held != null)
        {
            Vector2 facing = (shore - held.Centre).normalized;
            Landings.Add((held.Centre + facing * (held.Radius * 1.05f), BearingOf(facing)));
        }
    }

    private bool InWater(Vector2 point, float beyond) =>
        Lake.Edge(point) < beyond || Ponds.Exists(pond => pond.Edge(point) < beyond);

    private void PlacePois()
    {
        foreach (HavenPointOfInterest rule in Recipe.pointsOfInterest)
        {
            if (rule == null || rule.prefab == null)
            {
                continue;
            }

            for (int i = 0; i < rule.count; i++)
            {
                if (rule.onIsland)
                {
                    Island island = Islands.Find(one => !Pois.Exists(poi => Vector2.Distance(poi.At, one.Centre) < 1f));
                    if (island != null)
                    {
                        Pois.Add(new Poi { Rule = rule, At = island.Centre, Turn = Range(0f, 360f) });
                    }

                    continue;
                }

                Vector2 best = default;
                float bestHeight = float.MinValue;
                int found = 0;
                for (int tries = 0; tries < Tries && found < (rule.high ? 12 : 1); tries++)
                {
                    Vector2 at = Around(rule.distance.x, Mathf.Min(rule.distance.y, MaxReach));
                    if (!PoiFits(rule, at))
                    {
                        continue;
                    }

                    found++;
                    float height = Ground(at);
                    if (height > bestHeight)
                    {
                        bestHeight = height;
                        best = at;
                    }
                }

                if (found == 0)
                {
                    continue;
                }

                Pois.Add(new Poi { Rule = rule, At = best, Turn = Range(0f, 360f) });
                if (rule.path)
                {
                    AddSpur(best, rule.clearing);
                }
            }
        }
    }

    private bool PoiFits(HavenPointOfInterest rule, Vector2 at)
    {
        return at.magnitude > Recipe.buildable + rule.clearing + 30f && !InWater(at, rule.clearing + 8f)
            && !Near(Paths, at, rule.clearing + 3f) && Near(Paths, at, rule.clearing + 70f) && !Near(Rivers, at, rule.clearing + 8f)
            && !Pois.Exists(poi => Vector2.Distance(poi.At, at) < 50f);
    }

    // A short path from the nearest path to the edge of a place.
    private void AddSpur(Vector2 to, float clearing)
    {
        Vector2 from = NearestOn(Paths, to);
        Vector2 end = to + (from - to).normalized * clearing * 0.5f;
        if (Vector2.Distance(from, end) > 4f)
        {
            AddPath(Wander(from, end, 0.1f), PathKind.Spur);
        }
    }

    private void AddPath(List<Vector2> line, PathKind kind)
    {
        Paths.Add(line);
        PathKinds.Add(kind);
    }

    /// <summary>The open ground of the glades at a point, 0 to 1.</summary>
    public float GladeOpen(Vector2 point)
    {
        float open = 0f;
        foreach (Glade glade in Glades)
        {
            open = Mathf.Max(open, glade.Open(point));
        }

        return open;
    }

    // ----- Woodland trails and glades -----

    // A trail can go where it is dry, off the ravines' lips, round rock and places, and not up the mountain's steeps.
    private bool TrailClear(List<Vector2> line)
    {
        // Every few metres, not just at the knots: an arc's knots lie 20 m apart, room enough to step over a river unseen.
        var points = new List<Vector2>();
        for (int i = 0; i < line.Count; i++)
        {
            points.Add(line[i]);
            if (i + 1 < line.Count)
            {
                int steps = Mathf.FloorToInt(Vector2.Distance(line[i], line[i + 1]) / 4f);
                for (int k = 1; k < steps; k++)
                {
                    points.Add(Vector2.Lerp(line[i], line[i + 1], k / (float)steps));
                }
            }
        }

        foreach (Vector2 point in points)
        {
            float edge = Half - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y));
            if (edge < Recipe.rim.y + 8f || point.magnitude < Recipe.haven || InWater(point, 3f) || Near(Rivers, point, 5f) || MountainWeight(point) > 0.6f
                || Ravines.Exists(ravine => NearLine(ravine.Line, point, ravine.FloorWidth + 8f))
                || Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, point) < outcrop.Radius + 3f)
                || Pois.Exists(poi => Vector2.Distance(poi.At, point) < poi.Rule.clearing * 0.5f))
            {
                return false;
            }
        }

        return true;
    }

    // Deep in the woods, an arc from each main path to the next, so a walk out can come back another way.
    private void PlaceOuterLoops()
    {
        if (!Recipe.outerLoops || radialBearings.Count < 2)
        {
            return;
        }

        var sorted = new List<float>(radialBearings);
        sorted.Sort();
        for (int i = 0; i < sorted.Count; i++)
        {
            float a = sorted[i], b = sorted[(i + 1) % sorted.Count] + (i + 1 == sorted.Count ? 360f : 0f);
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float radius = Range(Recipe.forest - 10f, MaxReach - 30f);
                var arc = new List<Vector2>();
                for (float t = a; t <= b + 0.01f; t += 4f)
                {
                    arc.Add(Dir(t) * (radius + (Perlin(Dir(t) * 60f, 35f, 19f + attempt) - 0.5f) * 40f));
                }

                // Its ends sit on the main paths it joins.
                arc[0] = NearestOn(Paths, arc[0]);
                arc[arc.Count - 1] = NearestOn(Paths, arc[arc.Count - 1]);
                if (arc.Count >= 4 && TrailClear(arc.GetRange(1, arc.Count - 2)))
                {
                    AddPath(arc, PathKind.Loop);
                    break;
                }
            }
        }
    }

    private void PlaceGlades()
    {
        int count = random.Next(Recipe.glades.x, Recipe.glades.y + 1);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                // Square root leans them outward, where the woods are thickest and the work is.
                float distance = Mathf.Lerp(Recipe.haven + 50f, MaxReach - 15f, Mathf.Sqrt((float)random.NextDouble()));
                Vector2 at = Dir(Range(0f, 360f)) * distance;
                float radius = Range(Recipe.gladeRadius.x, Recipe.gladeRadius.y);
                if (MountainWeight(at) > 0.45f || InWater(at, radius + 10f) || Near(Rivers, at, radius + 6f)
                    || Ravines.Exists(ravine => NearLine(ravine.Line, at, radius + 10f))
                    || Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, at) < outcrop.Radius + radius + 8f)
                    || Pois.Exists(poi => Vector2.Distance(poi.At, at) < poi.Rule.clearing + radius + 15f)
                    || Glades.Exists(other => Vector2.Distance(other.Centre, at) < other.Radius + radius + 25f))
                {
                    continue;
                }

                var glade = new Glade { Centre = at, Radius = radius, Wobble = new float[10] };
                for (int k = 0; k < glade.Wobble.Length; k++)
                {
                    glade.Wobble[k] = Range(0.78f, 1.22f);
                }

                // A path already through it, or a trail to the nearest one that can get there.
                List<Vector2> trail = Near(Paths, at, radius) ? null : TrailTo(glade, NearestOn(Paths, at), 170f);
                if (!Near(Paths, at, radius) && trail == null)
                {
                    continue;
                }

                if (trail != null)
                {
                    AddPath(trail, PathKind.Trail);
                }

                float outward = Mathf.InverseLerp(Recipe.haven, MaxReach, distance);
                float roll = Range(0f, 1f);
                glade.Kind = roll < 0.12f ? GladeKind.Wet : roll < 0.12f + 0.25f + outward * 0.15f ? GladeKind.Lair : roll < 0.7f ? GladeKind.Rocky : GladeKind.Meadow;
                if (glade.Kind == GladeKind.Wet)
                {
                    AddGladePool(glade);
                }

                glade.Landmark = Recipe.gladeLandmarks.Count > 0 && glade.Kind != GladeKind.Wet && Range(0f, 1f) < Recipe.gladeLandmarkChance;
                glade.LandmarkKind = Recipe.gladeLandmarks.Count > 0 ? random.Next(Recipe.gladeLandmarks.Count) : 0;
                Glades.Add(glade);
                break;
            }
        }

        // Some glades get a second way out: to another glade or path, never the one they came by.
        foreach (Glade glade in Glades)
        {
            if (Range(0f, 1f) >= Recipe.gladeLinks)
            {
                continue;
            }

            Vector2 target = default;
            float best = float.MaxValue;
            foreach (Glade other in Glades)
            {
                float d = Vector2.Distance(other.Centre, glade.Centre);
                if (other != glade && d > 45f && d < 150f && d < best)
                {
                    best = d;
                    target = other.Centre + (glade.Centre - other.Centre).normalized * other.Radius * 0.6f;
                }
            }

            if (best == float.MaxValue)
            {
                continue;
            }

            List<Vector2> link = TrailTo(glade, target, 160f);
            if (link != null && !SameAs(link))
            {
                AddPath(link, PathKind.Trail);
            }
        }
    }

    // From inside a glade (so the trail visibly enters it) to a point, if the way is clear.
    private List<Vector2> TrailTo(Glade glade, Vector2 target, float longest)
    {
        Vector2 toward = (target - glade.Centre).normalized;
        Vector2 start = glade.Centre + toward * glade.Radius * 0.55f;
        if (Vector2.Distance(start, target) > longest || Vector2.Distance(start, target) < 6f)
        {
            return null;
        }

        for (int attempt = 0; attempt < 3; attempt++)
        {
            List<Vector2> line = Wander(start, target, 0.14f);
            // The last stretch may meet the path it joins; only the way there must be clear.
            int keep = Mathf.Max(1, line.Count - 2);
            if (TrailClear(line.GetRange(1, keep - 1).FindAll(point => glade.Open(point) < 0.5f)))
            {
                return line;
            }
        }

        return null;
    }

    // A trail that would only double one already there (both ends near another's).
    private bool SameAs(List<Vector2> line)
    {
        foreach (List<Vector2> other in Paths)
        {
            if (NearLine(other, line[0], 12f) && NearLine(other, line[line.Count - 1], 12f) && NearLine(other, line[line.Count / 2], 12f))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A river's water level at one of its knots (metres above the portal's ground): it only runs downhill.</summary>
    public float RiverLevel(int river, int knot) => riverLevels[river][knot];

    // A river runs only downhill: from its source each knot's level is the lowest ground met so far, never under the lake.
    private void LevelRivers()
    {
        riverLevels.Clear();
        foreach (List<Vector2> river in Rivers)
        {
            var levels = new float[river.Count];
            float level = float.MaxValue;
            for (int i = 0; i < river.Count; i++)
            {
                level = Mathf.Max(Mathf.Min(level, Ground(river[i])), Lake.Level + 0.2f);
                levels[i] = level;
            }

            riverLevels.Add(levels);
        }
    }

    // Brings the ground down to a river's level along it, with sides that widen the deeper the cut (about 18 degrees), so the river
    // cuts only its own channel instead of a trench where the ground rises along its way.
    private float RiverValley(Vector2 point, float ground)
    {
        for (int r = 0; r < riverLevels.Count; r++)
        {
            List<Vector2> river = Rivers[r];
            float[] levels = riverLevels[r];
            float best = float.MaxValue, level = 0f;
            for (int i = 0; i + 1 < river.Count; i++)
            {
                Vector2 a = river[i], along = river[i + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, along) / Mathf.Max(0.0001f, along.sqrMagnitude));
                float squared = (a + along * t - point).sqrMagnitude;
                if (squared < best)
                {
                    best = squared;
                    level = Mathf.Lerp(levels[i], levels[i + 1], t);
                }
            }

            float distance = Mathf.Sqrt(best);
            float cut = ground - (level + 0.2f);
            if (cut <= 0f || distance > ValleyMax)
            {
                continue;
            }

            float reach = ValleyFloor + Mathf.Min(cut * 3f, ValleyMax - ValleyFloor);
            if (distance < reach)
            {
                ground -= cut * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ValleyFloor * 0.6f, reach, distance)));
            }
        }

        return ground;
    }

    // Every crossing of a path and a river gets a knot of its own, so the river can be made shallow just there.
    private void PlaceFords()
    {
        for (int r = 0; r < Rivers.Count; r++)
        {
            List<Vector2> river = Rivers[r];
            for (int i = 0; i + 1 < river.Count; i++)
            {
                if (!Crossing(river[i], river[i + 1], out Vector2 at))
                {
                    continue;
                }

                // Close to a knot already there: that knot is the ford.
                if (Vector2.Distance(at, river[i]) < 2f)
                {
                    Fords.Add((r, i));
                }
                else if (Vector2.Distance(at, river[i + 1]) < 2f)
                {
                    Fords.Add((r, i + 1));
                    i++;
                }
                else
                {
                    river.Insert(i + 1, at);
                    Fords.Add((r, i + 1));
                    i++;
                }
            }
        }
    }

    private bool Crossing(Vector2 a, Vector2 b, out Vector2 at)
    {
        foreach (List<Vector2> path in Paths)
        {
            for (int k = 0; k + 1 < path.Count; k++)
            {
                if (Intersect(a, b, path[k], path[k + 1], out at))
                {
                    return true;
                }
            }
        }

        at = default;
        return false;
    }

    private static bool Intersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 at)
    {
        Vector2 r = b - a, s = d - c;
        float cross = r.x * s.y - r.y * s.x;
        at = default;
        if (Mathf.Abs(cross) < 1e-6f)
        {
            return false;
        }

        Vector2 ca = c - a;
        float t = (ca.x * s.y - ca.y * s.x) / cross;
        float u = (ca.x * r.y - ca.y * r.x) / cross;
        if (t < 0f || t > 1f || u < 0f || u > 1f)
        {
            return false;
        }

        at = a + r * t;
        return true;
    }

    // A wet glade's pool, off its middle so there is dry ground to stand on.
    private void AddGladePool(Glade glade)
    {
        float radius = Mathf.Min(glade.Radius * 0.35f, Recipe.pondRadius.y);
        var pond = new RaidLayoutPond
        {
            Position = glade.Centre + Dir(Range(0f, 360f)) * glade.Radius * 0.2f,
            Radii = new Vector2(radius, radius / Range(1f, 1.5f)),
            Turn = Range(0f, Mathf.PI * 2f),
            Depth = 1.2f,
        };
        pond.Level = LevelAround(pond, 2f, 0.15f);
        Ponds.Add(pond);
    }

    private void PlaceOutcrops()
    {
        int count = random.Next(Recipe.outcrops.x, Recipe.outcrops.y + 1);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                // A third in the Haven's ring (smaller), the rest in the woods.
                bool haven = i < count / 3;
                Vector2 at = haven ? Around(Recipe.buildable + 40f, Recipe.haven + 30f) : Around(Recipe.haven + 30f, MaxReach - 20f);
                float radius = haven ? Range(7f, 12f) : Range(10f, 22f);
                if (Near(Paths, at, radius + 8f) || Near(Rivers, at, radius + 10f) || InWater(at, radius + 14f)
                    || Pois.Exists(poi => Vector2.Distance(poi.At, at) < radius + poi.Rule.clearing + 8f)
                    || Outcrops.Exists(other => Vector2.Distance(other.Centre, at) < other.Radius + radius + 15f) || MountainWeight(at) > 0.3f)
                {
                    continue;
                }

                Outcrops.Add(new Outcrop { Centre = at, Radius = radius, Height = haven ? Range(3f, 5f) : Range(5f, 9f), Loop = Loop(at, radius) });
                break;
            }
        }
    }

    private List<Vector2> Loop(Vector2 centre, float radius)
    {
        var loop = new List<Vector2>();
        float turn = Range(0f, 360f);
        int knots = 10;
        for (int k = 0; k < knots; k++)
        {
            float angle = turn + k * 360f / knots;
            loop.Add(centre + Dir(angle) * radius * Range(0.8f, 1.15f));
        }

        return loop;
    }

    // Terrace edges across the mountain's flanks, high side up the mountain, broken where the mountain path climbs.
    private void PlaceMountainCliffs()
    {
        int count = random.Next(Recipe.mountainCliffs.x, Recipe.mountainCliffs.y + 1);
        float half = Recipe.mountainAt.y * 0.5f;
        for (int i = 0; i < count; i++)
        {
            float radius = Mathf.Lerp(Recipe.mountainFrom + 50f, MaxReach - 10f, (i + Range(0.2f, 0.8f)) / count);
            float from = Recipe.mountainAt.x - half * Range(0.7f, 0.95f), to = Recipe.mountainAt.x + half * Range(0.7f, 0.95f);
            var line = new List<Vector2>();
            for (float bearing = from; bearing <= to; bearing += 3f)
            {
                bool gap = Mathf.Abs(Mathf.DeltaAngle(bearing, MountainPathAngle)) < 6f || Near(Rivers, Dir(bearing) * radius, 8f);
                if (gap)
                {
                    AddCliff(line);
                    line = new List<Vector2>();
                    continue;
                }

                line.Add(Dir(bearing) * (radius + (Perlin(Dir(bearing) * 40f, 25f, 3f) - 0.5f) * 20f));
            }

            AddCliff(line);
        }
    }

    private void AddCliff(List<Vector2> line)
    {
        if (line.Count < 3)
        {
            return;
        }

        // Drawn clockwise, the outside (up the mountain) is on the left.
        Vector2 along = line[line.Count - 1] - line[0];
        Vector2 left = new Vector2(-along.y, along.x);
        bool highOnLeft = Vector2.Dot(left, line[line.Count / 2]) > 0f;
        Cliffs.Add(new Cliff { Line = line, Height = Range(5f, 9f), HighOnLeft = highOnLeft });
    }

    // In the woods, more of them further out; never across a path (a path has no neck over one), a river or the lake.
    private void PlaceRavines()
    {
        int count = random.Next(Recipe.ravines.x, Recipe.ravines.y + 1);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                float r = Mathf.Lerp(Recipe.forest * 0.85f, MaxReach - 10f, Mathf.Sqrt((float)random.NextDouble()));
                Vector2 start = Dir(Range(0f, 360f)) * r;
                float length = Range(Recipe.ravineLength.x, Recipe.ravineLength.y);
                List<Vector2> line = GrowRavine(start, length);
                if (line == null)
                {
                    continue;
                }

                Ravines.Add(new Ravine { Line = line, Depth = Range(Recipe.ravineDepth.x, Recipe.ravineDepth.y), FloorWidth = Range(3.5f, 6f) });
                break;
            }
        }
    }

    private List<Vector2> GrowRavine(Vector2 start, float length)
    {
        if (!RavineCanPass(start))
        {
            return null;
        }

        var line = new List<Vector2> { start };
        float heading = Range(0f, 360f);
        float turn = 0f;
        float grown = 0f;
        foreach (int side in new[] { 1, -1 })
        {
            Vector2 tip = start;
            float facing = heading + (side < 0 ? 180f : 0f);
            while (grown < length)
            {
                turn = Mathf.Clamp(turn + Range(-RavineTurn, RavineTurn) * 0.5f, -RavineTurn, RavineTurn);
                facing += turn;
                Vector2 next = tip + Dir(facing) * RavineStep;
                if (!RavineCanPass(next))
                {
                    break;
                }

                if (side > 0)
                {
                    line.Add(next);
                }
                else
                {
                    line.Insert(0, next);
                }

                tip = next;
                grown += RavineStep;
            }
        }

        return grown >= length * 0.55f ? line : null;
    }

    private bool RavineCanPass(Vector2 point)
    {
        float edge = Half - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y));
        return edge > Recipe.rim.y + 10f && point.magnitude > Recipe.forest * 0.8f && !Near(Paths, point, 16f) && !Near(Rivers, point, 18f)
            && !InWater(point, 20f) && !Pois.Exists(poi => Vector2.Distance(poi.At, point) < poi.Rule.clearing + 14f)
            && !Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, point) < outcrop.Radius + 12f)
            && !Ravines.Exists(other => NearLine(other.Line, point, 28f));
    }

    private void PlaceGroves()
    {
        int count = random.Next(Recipe.groves.x, Recipe.groves.y + 1);
        float turn = Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                float bearing = turn + i * 360f / count + Range(-30f, 30f);
                float radius = Range(30f, 50f);
                Vector2 at = Dir(bearing) * Range(Recipe.buildable + radius * 0.6f, Recipe.haven + 25f);
                if (!InWater(at, radius * 0.5f))
                {
                    Groves.Add(Loop(at, radius));
                    break;
                }
            }
        }
    }

    private void PlaceZones()
    {
        foreach (HavenCreatureBand band in Recipe.creatures)
        {
            if (band == null)
            {
                continue;
            }

            for (int i = 0; i < band.zones; i++)
            {
                // Lairs first, then any dry glade still free: open ground to fight in, reached by a trail.
                Glade den = Glades.Find(glade => !glade.HasZone && glade.Kind == GladeKind.Lair && InBand(glade.Centre, band.distance))
                            ?? Glades.Find(glade => !glade.HasZone && glade.Kind != GladeKind.Wet && InBand(glade.Centre, band.distance));
                if (den != null)
                {
                    den.HasZone = true;
                    Zones.Add(new Zone { Band = band, At = den.Centre, Radius = Mathf.Max(9f, den.Radius * 0.55f) });
                    continue;
                }

                for (int tries = 0; tries < Tries; tries++)
                {
                    Vector2 at = Around(band.distance.x, Mathf.Min(band.distance.y, MaxReach));
                    float radius = Range(9f, 15f);
                    if (at.magnitude < Recipe.haven || Near(Paths, at, radius + 4f) || Near(Rivers, at, radius + 6f) || InWater(at, radius + 6f)
                        || Ravines.Exists(ravine => NearLine(ravine.Line, at, radius + 10f))
                        || Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, at) < outcrop.Radius + radius + 4f)
                        || Zones.Exists(zone => Vector2.Distance(zone.At, at) < 40f))
                    {
                        continue;
                    }

                    Zones.Add(new Zone { Band = band, At = at, Radius = radius });
                    break;
                }
            }
        }
    }

    private void PlaceNodes()
    {
        foreach (HavenResourceBand band in Recipe.resources)
        {
            if (band == null)
            {
                continue;
            }

            for (int i = 0; i < band.clusters; i++)
            {
                if (!band.mountainOnly && Range(0f, 1f) < 0.5f && TryGladeCluster(band))
                {
                    continue;
                }

                for (int tries = 0; tries < Tries; tries++)
                {
                    Vector2 at = Around(band.distance.x, Mathf.Min(band.distance.y, MaxReach));
                    if (!NodeFits(at, band) || (band.mountainOnly && MountainWeight(at) < 0.35f))
                    {
                        continue;
                    }

                    int count = random.Next(band.perCluster.x, band.perCluster.y + 1);
                    for (int k = 0; k < count; k++)
                    {
                        Vector2 spot = k == 0 ? at : at + Dir(Range(0f, 360f)) * Range(1.5f, 6f);
                        if (k == 0 || NodeFits(spot, band))
                        {
                            Nodes.Add(new Node { Band = band, At = spot });
                        }
                    }

                    break;
                }
            }
        }
    }

    private void PlaceMountainRocks()
    {
        int kinds = Recipe.mountainRocks.Count;
        if (kinds == 0)
        {
            return;
        }

        int count = random.Next(Recipe.mountainRockCount.x, Recipe.mountainRockCount.y + 1);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                Vector2 at = Around(Recipe.mountainFrom, MaxReach + PathFromRim);
                // More of them higher up, where the mountain is barest; none on its paths, places, nodes or each other.
                float weight = MountainWeight(at);
                if (weight < 0.3f || Range(0f, 1f) > weight || Near(Paths, at, 5f) || Near(Rivers, at, 6f) || InWater(at, 3f)
                    || Pois.Exists(poi => Vector2.Distance(poi.At, at) < poi.Rule.clearing + 4f)
                    || Nodes.Exists(node => (node.At - at).sqrMagnitude < 16f)
                    || Rocks.Exists(rock => (rock.At - at).sqrMagnitude < 81f))
                {
                    continue;
                }

                Rocks.Add(new Rock
                {
                    Kind = random.Next(kinds),
                    At = at,
                    Scale = Range(Recipe.mountainRockScale.x, Recipe.mountainRockScale.y),
                    Turn = Range(-35f, 35f),
                });
                break;
            }
        }
    }

    private bool InBand(Vector2 point, Vector2 band) => point.magnitude >= band.x && point.magnitude <= band.y;

    // A cluster round the edge of a glade that suits it: stone in the rocky ones, forage and plants in meadows and by pools.
    private bool TryGladeCluster(HavenResourceBand band)
    {
        var suited = Glades.FindAll(glade => InBand(glade.Centre, band.distance) && Suits(glade.Kind, band.kind));
        if (suited.Count == 0)
        {
            return false;
        }

        Glade glade = suited[random.Next(suited.Count)];
        int count = random.Next(band.perCluster.x, band.perCluster.y + 1);
        int placed = 0;
        for (int k = 0; k < count * 3 && placed < count; k++)
        {
            Vector2 spot = glade.Centre + Dir(Range(0f, 360f)) * glade.Radius * Range(0.35f, 0.85f);
            if (NodeFits(spot, band) && !Nodes.Exists(node => (node.At - spot).sqrMagnitude < 4f))
            {
                Nodes.Add(new Node { Band = band, At = spot });
                placed++;
            }
        }

        return placed > 0;
    }

    private static bool Suits(GladeKind glade, ResourceNodeKind kind) =>
        kind == ResourceNodeKind.Rock ? glade == GladeKind.Rocky
        : kind == ResourceNodeKind.Forage || kind == ResourceNodeKind.Plant ? glade == GladeKind.Meadow || glade == GladeKind.Wet
        : glade != GladeKind.Lair;

    // ----- Along the way -----

    // Where a trail, loop or spur meets another path, a marker stands beside it, out of the tread.
    private void PlaceJunctions()
    {
        if (Recipe.junctionMarker == null)
        {
            return;
        }

        for (int p = 0; p < Paths.Count; p++)
        {
            if (PathKinds[p] == PathKind.Road || PathKinds[p] == PathKind.Ring)
            {
                continue;
            }

            List<Vector2> line = Paths[p];
            foreach (bool start in new[] { true, false })
            {
                Vector2 end = start ? line[0] : line[line.Count - 1];
                Vector2 inward = (start ? line[Mathf.Min(2, line.Count - 1)] : line[Mathf.Max(0, line.Count - 3)]) - end;
                if (GladeOpen(end) > 0.3f || !MeetsAnother(p, end) || inward.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                // A few metres up the trail and to its side: seen from the path it leaves, never in the way.
                Vector2 along = inward.normalized;
                Vector2 at = end + along * 4f + Vector2.Perpendicular(along) * (Range(0f, 1f) < 0.5f ? 2.6f : -2.6f);
                if (!InWater(at, 1f) && !Junctions.Exists(j => (j.At - at).sqrMagnitude < 100f))
                {
                    Junctions.Add(new Placed { At = at, Turn = Range(0f, 360f) });
                }
            }
        }
    }

    private bool MeetsAnother(int path, Vector2 point)
    {
        for (int other = 0; other < Paths.Count; other++)
        {
            if (other != path && NearLine(Paths[other], point, 3f))
            {
                return true;
            }
        }

        return false;
    }

    // Along every path in the woods, a scene every so many metres beside the tread, sides taking turns: something to come
    // across between the glades. Not in glades, places, water, ravines or on resource points.
    private void PlaceTrailScenes()
    {
        int kinds = Recipe.trailScenes.Count;
        if (kinds == 0)
        {
            return;
        }

        for (int p = 0; p < Paths.Count; p++)
        {
            List<Vector2> line = Paths[p];
            float next = Range(Recipe.trailSceneSpacing.x, Recipe.trailSceneSpacing.y) * 0.5f;
            float walked = 0f;
            float side = Range(0f, 1f) < 0.5f ? 1f : -1f;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                float length = Vector2.Distance(line[i], line[i + 1]);
                while (walked + length >= next)
                {
                    float t = (next - walked) / Mathf.Max(length, 0.001f);
                    Vector2 on = Vector2.Lerp(line[i], line[i + 1], t);
                    Vector2 along = (line[i + 1] - line[i]).normalized;
                    Vector2 at = on + Vector2.Perpendicular(along) * side * Range(5f, 7f);
                    next += Range(Recipe.trailSceneSpacing.x, Recipe.trailSceneSpacing.y);
                    if (WoodsWeight(at) < 0.5f || !SceneFits(at))
                    {
                        continue;
                    }

                    // Turned to face the path it sits beside.
                    TrailScenes.Add(new Placed { Kind = random.Next(kinds), At = at, Turn = BearingOf(on - at) + Range(-25f, 25f) });
                    side = -side;
                }

                walked += length;
            }
        }
    }

    private bool SceneFits(Vector2 at) =>
        !Near(Paths, at, 3.5f) && !InWater(at, 3f) && !Near(Rivers, at, 6f) && GladeOpen(at) < 0.05f
        && !Ravines.Exists(ravine => NearLine(ravine.Line, at, ravine.FloorWidth + 12f))
        && !Pois.Exists(poi => Vector2.Distance(poi.At, at) < poi.Rule.clearing + 8f)
        && !Nodes.Exists(node => (node.At - at).sqrMagnitude < 25f)
        && !TrailScenes.Exists(scene => (scene.At - at).sqrMagnitude < 900f)
        && !Junctions.Exists(marker => (marker.At - at).sqrMagnitude < 36f);

    // Each glade dressed for its kind: scattered over its open ground, more towards its rim, none on its landmark or pool.
    private void PlaceGladeDressing()
    {
        foreach (Glade glade in Glades)
        {
            List<GameObject> kit = DressingFor(glade.Kind);
            if (kit.Count == 0)
            {
                continue;
            }

            int count = random.Next(Recipe.dressingPerGlade.x, Recipe.dressingPerGlade.y + 1);
            for (int k = 0; k < count * 3 && GladeProps.Count(g => g.glade == glade) < count; k++)
            {
                Vector2 at = glade.Centre + Dir(Range(0f, 360f)) * glade.Radius * Mathf.Sqrt(Range(0.15f, 0.8f));
                if (glade.Open(at) < 0.6f || InWater(at, 2f) || Near(Paths, at, 2.5f) || (glade.Landmark && Vector2.Distance(at, glade.Centre) < 5f)
                    || Nodes.Exists(node => (node.At - at).sqrMagnitude < 9f) || GladeProps.Exists(g => (g.prop.At - at).sqrMagnitude < 9f))
                {
                    continue;
                }

                GladeProps.Add((glade, new Placed { Kind = random.Next(kit.Count), At = at, Turn = Range(0f, 360f), Scale = Range(0.85f, 1.2f) }));
            }
        }
    }

    public List<GameObject> DressingFor(GladeKind kind) => kind switch
    {
        GladeKind.Lair => Recipe.lairDressing,
        GladeKind.Rocky => Recipe.rockyDressing,
        GladeKind.Wet => Recipe.wetDressing,
        _ => Recipe.meadowDressing,
    };

    private bool NodeFits(Vector2 at, HavenResourceBand band) =>
        at.magnitude > Recipe.buildable && !Near(Paths, at, 3f) && !Near(Rivers, at, 6f) && !InWater(at, 2f)
        && !Ravines.Exists(ravine => NearLine(ravine.Line, at, 10f))
        && !Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, at) < outcrop.Radius + 2f)
        && !Pois.Exists(poi => Vector2.Distance(poi.At, at) < poi.Rule.clearing);

    // ----- Lines -----

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
