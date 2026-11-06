using System.Collections.Generic;
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

    public sealed class Node
    {
        public HavenResourceBand Band;
        public Vector2 At;
    }

    public readonly HavenWorldRecipe Recipe;
    public readonly float Half;
    public RaidLayoutPond Lake;
    public readonly List<Island> Islands = new List<Island>();
    public readonly List<RaidLayoutPond> Ponds = new List<RaidLayoutPond>();
    public readonly List<List<Vector2>> Paths = new List<List<Vector2>>();
    public readonly List<List<Vector2>> Rivers = new List<List<Vector2>>();
    public readonly List<Poi> Pois = new List<Poi>();
    public readonly List<Outcrop> Outcrops = new List<Outcrop>();
    public readonly List<Cliff> Cliffs = new List<Cliff>();
    public readonly List<Ravine> Ravines = new List<Ravine>();
    public readonly List<List<Vector2>> Groves = new List<List<Vector2>>();
    public readonly List<Zone> Zones = new List<Zone>();
    public readonly List<Node> Nodes = new List<Node>();
    public float MountainPathAngle;

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
        plan.PlaceZones();
        plan.PlaceNodes();
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
        float ridged = 1f - Mathf.Abs(Perlin(point, Recipe.hillScale * 0.6f, 97f) * 2f - 1f);
        float ridge = Recipe.ridges * WoodsWeight(point) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, ridged));
        // Gentle and open where the Haven is built: the ward reaches about 50 m.
        float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.buildable, Recipe.buildable + 70f, r));
        float near = (Perlin(point, 25f, 13f) - 0.5f) * 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 40f, r));
        float height = (hills + ridge) * open + near * (1f - open);
        height += Recipe.mountainHeight * MountainWeight(point) * (0.7f + 0.3f * ridged);
        float edge = Half - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y));
        float rim = Mathf.Clamp01(1f - edge / Recipe.rim.y);
        return height + Recipe.rim.x * rim * rim;
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
            Paths.Add(Wander(from, to, 0.08f));
        }

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
                Paths.Add(arc);
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
            Paths.Add(Wander(from, end, 0.1f));
        }
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
