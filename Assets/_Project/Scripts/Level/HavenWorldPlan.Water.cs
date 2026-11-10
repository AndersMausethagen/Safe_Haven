using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Water: the lake and its islands, the meres (smaller lakes) and theirs, the bog, rivers down to the lake and streams down to
/// the nearest water, all levelled along their way and cut into valleys, falls where they cross a cliff, ponds and spring
/// pools, the lake's landings, and fords where paths cross. Part of HavenWorldPlan, in its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
    private const float ValleyMax = 40f;

    private const float RiverFromHome = 95f;

    // A mere's bench stands this far over its water, and the lake's over its own.
    private const float MereShore = 0.5f;

    private const float LakeShore = 0.6f;

    private void PlaceLake()
    {
        LakeBench = -Recipe.terraceStep;
        Lake = new RaidLayoutPond
        {
            Position = Dir(Recipe.lakeAt.x) * Recipe.lakeAt.y,
            Radii = Recipe.lakeSize,
            Turn = Range(0f, Mathf.PI * 2f),
            Depth = Recipe.lakeDepth,
            // In its own valley, one terrace under the Haven: every river and stream has somewhere lower to run to.
            Level = LakeBench - LakeShore,
        };
        PlaceIslands(Lake, random.Next(Recipe.islands.x, Recipe.islands.y + 1), Recipe.islandRadius, 0.6f);

        if (HasBog)
        {
            BogCentre = Dir(Recipe.bogAt.x) * Recipe.bogAt.y;
            // A basin: one terrace under the lowest land anywhere round it, so its water lies below every shore it has. (Set from
            // the land at its middle, it hung over the lower ground on the Haven's side.)
            float lowest = float.MaxValue;
            for (int i = 0; i < 24; i++)
            {
                lowest = Mathf.Min(lowest, OpenLand(BogCentre + Dir(i * 15f) * Recipe.bogRadius * 1.25f));
            }

            BogBench = (Mathf.RoundToInt(lowest / Recipe.terraceStep) - 1) * Recipe.terraceStep;
        }
    }

    private void PlaceIslands(RaidLayoutPond water, int count, Vector2 radii, float spread)
    {
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                float radius = Range(radii.x, radii.y);
                Vector2 at = water.Position + Rotate(new Vector2(Range(-1f, 1f) * water.Radii.x * spread, Range(-1f, 1f) * water.Radii.y * spread), water.Turn);
                if (water.Edge(at) > -(radius * 1.6f + (water == Lake ? 10f : 5f))
                    || Islands.Exists(other => other.Water == water && Vector2.Distance(other.Centre, at) < other.Radius + radius + (water == Lake ? 14f : 8f)))
                {
                    continue;
                }

                Islands.Add(new Island { Centre = at, Radius = radius, Height = water == Lake ? Range(2f, 4.5f) : Range(1.2f, 2.4f), Water = water });
                break;
            }
        }
    }

    private static Vector2 Rotate(Vector2 offset, float turn)
    {
        float cos = Mathf.Cos(turn), sin = Mathf.Sin(turn);
        return new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
    }

    // Smaller lakes with islands of their own, each on a bench of its own: near a path (a spur can reach its shore), never on one.
    private void PlaceMeres()
    {
        foreach (HavenMere rule in Recipe.meres)
        {
            if (rule == null)
            {
                continue;
            }

            for (int tries = 0; tries < Tries; tries++)
            {
                float stray = 10f + tries * 0.9f;
                Vector2 at = Dir(rule.at.x + Range(-stray, stray) * 0.25f) * Mathf.Min(rule.at.y + Range(-stray, stray), MaxReach - Mathf.Max(rule.size.x, rule.size.y));
                var mere = new RaidLayoutPond { Position = at, Radii = rule.size, Turn = Range(0f, Mathf.PI * 2f), Depth = rule.depth };
                float reach = mere.Reach + MereBasin;
                if (at.magnitude < Recipe.buildable + reach + 30f || Lake.Edge(at) < reach + LakeBasin * 0.5f || BogWeight(at) > 0f
                    || Vector2.Distance(at, BogCentre) < Recipe.bogRadius * 1.3f + reach || Near(Paths, at, mere.Reach + 10f)
                    || Meres.Exists(other => Vector2.Distance(other.Position, at) < other.Reach + reach + 40f))
                {
                    continue;
                }

                // On the bench its middle stands on, so its shore is level all round.
                mere.Level = Mathf.RoundToInt(OpenLand(at) / Recipe.terraceStep) * Recipe.terraceStep - MereShore;
                Meres.Add(mere);
                MereNames.Add(rule.name);
                float small = Mathf.Min(rule.size.x, rule.size.y);
                PlaceIslands(mere, random.Next(rule.islands.x, rule.islands.y + 1), new Vector2(small * 0.16f, small * 0.3f), 0.5f);
                break;
            }
        }
    }

    // Rivers: from the mountain down to the lake, and from the far edge of the woods past the Haven (water near home) to the lake.
    // Then streams: out of each mere and the bog, and from springs in the hills, down to the nearest river or the lake.
    private void PlaceRivers()
    {
        int count = random.Next(Recipe.rivers.x, Recipe.rivers.y + 1);
        Vector2 mouth(Vector2 from) => Lake.Rim(Mathf.Atan2(from.y - Lake.Position.y, from.x - Lake.Position.x) - Lake.Turn, -6f);
        for (int i = 0; i < count; i++)
        {
            List<Vector2> line = null;
            for (int tries = 0; tries < 12; tries++)
            {
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

                line = Meander(line, Range(9f, 14f), Range(50f, 70f));
                // Never through the buildable ground.
                for (int k = 0; k < line.Count; k++)
                {
                    if (line[k].magnitude < Recipe.buildable + RiverFromHome * 0.7f)
                    {
                        line[k] = line[k].normalized * (Recipe.buildable + RiverFromHome * 0.7f);
                    }
                }

                // Round the meres and the bog, not through them (the last try stands whatever it meets).
                if (!ThroughStillWater(line))
                {
                    break;
                }
            }

            AddRiver(line, Mathf.Lerp(Recipe.riverWidth.y, Recipe.riverWidth.x, count > 1 ? i / (count - 1f) : 0f), 1.3f, -1, float.MaxValue);
        }

        int streams = random.Next(Recipe.streams.x, Recipe.streams.y + 1);
        foreach (RaidLayoutPond mere in Meres)
        {
            if (streams > 0 && StreamFrom(mere.Position, mere, mere.Level))
            {
                streams--;
            }
        }

        if (HasBog && streams > 0 && StreamFrom(BogCentre, null, BogLevel))
        {
            streams--;
        }

        // Springs: a small pool where the water rises, at the foot of the hills.
        for (int tries = 0; tries < Tries * 2 && streams > 0; tries++)
        {
            Vector2 at = Around(Recipe.haven + 40f, MaxReach);
            if (Tier(at) < 1 || MountainWeight(at) > 0.7f || BogWeight(at) > 0f || InWater(at, 14f) || Near(Paths, at, 12f) || Near(Rivers, at, 50f)
                || !OneBench(at, 8f))
            {
                continue;
            }

            float radius = Range(3f, 4.5f);
            var spring = new RaidLayoutPond { Position = at, Radii = new Vector2(radius, radius / Range(1f, 1.3f)), Turn = Range(0f, Mathf.PI * 2f), Depth = 0.7f };
            spring.Level = LevelAround(spring, 2f, 0.15f);
            if (StreamFrom(at, spring, spring.Level))
            {
                Ponds.Add(spring);
                Springs.Add(at);
                streams--;
            }
        }
    }

    // Water does not run straight: the line swings from side to side along its way, its ends staying where they are.
    private List<Vector2> Meander(List<Vector2> line, float swing, float wavelength)
    {
        float total = Length(line), spacing = wavelength / 4f;
        if (total < wavelength * 1.5f)
        {
            return line;
        }

        // A knot every quarter wave.
        var even = new List<Vector2> { line[0] };
        float carried = 0f;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            float length = Vector2.Distance(line[i], line[i + 1]);
            for (float d = spacing - carried; d < length; d += spacing)
            {
                even.Add(Vector2.Lerp(line[i], line[i + 1], d / length));
            }

            carried = (carried + length) % spacing;
        }

        if (Vector2.Distance(even[even.Count - 1], line[line.Count - 1]) < spacing * 0.5f)
        {
            even.RemoveAt(even.Count - 1);
        }

        even.Add(line[line.Count - 1]);
        float phase = Range(0f, Mathf.PI * 2f), salt = Range(0f, 500f);
        var bent = new List<Vector2>(even);
        float along = 0f;
        for (int i = 1; i + 1 < even.Count; i++)
        {
            along += Vector2.Distance(even[i - 1], even[i]);
            Vector2 side = Vector2.Perpendicular((even[i + 1] - even[i - 1]).normalized);
            float taper = Mathf.Clamp01(Mathf.Min(along, total - along) / wavelength);
            float wave = Mathf.Sin(along / wavelength * Mathf.PI * 2f + phase) * 0.6f + (Mathf.PerlinNoise(along / (wavelength * 1.7f), salt) - 0.5f) * 1.6f;
            bent[i] = even[i] + side * wave * swing * taper;
        }

        return bent;
    }

    private void AddRiver(List<Vector2> line, float width, float depth, int joins, float startLevel)
    {
        Rivers.Add(line);
        RiverWidths.Add(width);
        RiverDepths.Add(depth);
        riverJoins.Add(joins);
        riverStarts.Add(startLevel);
    }

    // True when a river's line runs through a mere or the bog.
    private bool ThroughStillWater(List<Vector2> line)
    {
        for (int i = 0; i + 1 < line.Count; i++)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(line[i], line[i + 1]) / 6f));
            for (int k = 0; k <= steps; k++)
            {
                Vector2 point = Vector2.Lerp(line[i], line[i + 1], k / (float)steps);
                if (BogWeight(point) > 0.05f || Meres.Exists(mere => mere.Edge(point) < 12f))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // A stream from a point (in a mere, a spring's pool or the bog) down to the nearest river or the lake.
    private bool StreamFrom(Vector2 from, RaidLayoutPond water, float level)
    {
        const float longest = 320f;
        Vector2 target = default;
        int joins = -1;
        float best = longest;
        // The lake's shore, if it is near; else the nearest river that lies lower.
        Vector2 shore = Lake.Shore(from);
        if (Vector2.Distance(shore, from) < best)
        {
            best = Vector2.Distance(shore, from);
            target = Lake.Rim(Mathf.Atan2(from.y - Lake.Position.y, from.x - Lake.Position.x) - Lake.Turn, -5f);
        }

        for (int r = 0; r < Rivers.Count; r++)
        {
            Vector2 on = NearestOn(new List<List<Vector2>> { Rivers[r] }, from);
            float distance = Vector2.Distance(on, from);
            if (distance < best && Ground(on) < level - 1f)
            {
                best = distance;
                target = on;
                joins = r;
            }
        }

        if (best >= longest || best < 40f)
        {
            return false;
        }

        bool fromBog = water == null;
        Vector2 toward = (target - from).normalized;
        Vector2 start = fromBog ? from + toward * Recipe.bogRadius * 0.45f : water.Rim(Mathf.Atan2(toward.y, toward.x) - water.Turn, -1.5f);
        for (int tries = 0; tries < 6; tries++)
        {
            List<Vector2> line = Meander(Wander(start, target, 0.13f), Range(4f, 7f), Range(28f, 40f));
            bool clear = true;
            for (int k = 1; k + 1 < line.Count && clear; k++)
            {
                Vector2 point = line[k];
                clear = point.magnitude > Recipe.buildable + RiverFromHome * 0.6f && (k + 3 >= line.Count || !Near(Rivers, point, 14f))
                        && !Meres.Exists(mere => mere != water && mere.Edge(point) < 10f) && !Ponds.Exists(pond => pond != water && pond.Edge(point) < 6f)
                        && (fromBog || BogWeight(point) <= 0.05f) && (joins < 0 || Lake.Edge(point) > 8f);
            }

            if (clear)
            {
                AddRiver(line, Range(Recipe.streamWidth.x, Recipe.streamWidth.y), 0.55f, joins, level);
                return true;
            }
        }

        return false;
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
                if (Near(Paths, pond.Position, pond.Reach + 6f) || Near(Rivers, pond.Position, pond.Reach + 12f) || Lake.Edge(pond.Position) < pond.Reach + 12f
                    || !OneBench(pond.Position, pond.Reach + 5f)
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
        Island held = Islands.Find(island => island.Water == Lake && Pois.Exists(poi => Vector2.Distance(poi.At, island.Centre) < 1f));
        if (held != null)
        {
            Vector2 facing = (shore - held.Centre).normalized;
            Landings.Add((held.Centre + facing * (held.Radius * 1.05f), BearingOf(facing)));
        }
    }

    /// <summary>True on the dry ground of an island: in the lake's or a mere's outline, but standing out of the water.</summary>
    public bool OnIsland(Vector2 point)
    {
        foreach (Island island in Islands)
        {
            if ((island.Centre - point).sqrMagnitude < island.Radius * island.Radius && Height(point) > island.Water.Level + 0.2f)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True in the lake, a mere, a pond or one of the bog's pools, or within <paramref name="beyond"/> metres of their shores.</summary>
    public bool InWater(Vector2 point, float beyond) =>
        Lake.Edge(point) < beyond || Ponds.Exists(pond => pond.Edge(point) < beyond) || Meres.Exists(mere => mere.Edge(point) < beyond) || BogWet(point, beyond);

    /// <summary>A river's water level at one of its knots (metres above the portal's ground): it only runs downhill.</summary>
    public float RiverLevel(int river, int knot) => riverLevels[river][knot];

    // A river's level at a point along it.
    private float RiverLevelNear(int river, Vector2 point)
    {
        List<Vector2> line = Rivers[river];
        float[] levels = riverLevels[river];
        float best = float.MaxValue, level = levels[0];
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 a = line[i], along = line[i + 1] - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, along) / Mathf.Max(0.0001f, along.sqrMagnitude));
            float squared = (a + along * t - point).sqrMagnitude;
            if (squared < best)
            {
                best = squared;
                level = Mathf.Lerp(levels[i], levels[i + 1], t);
            }
        }

        return level;
    }

    // Where a river comes down to a lower bench it falls: knots just above and just below the cliff's face, so the water keeps its
    // level to the lip and drops in a few metres instead of sliding down the whole way between two knots.
    private void PlaceFalls()
    {
        const float back = 2.6f, further = 7f;
        foreach (List<Vector2> river in Rivers)
        {
            var laid = new List<Vector2> { river[0] };
            Vector2 source = river[0], mouth = river[river.Count - 1];
            var last = new Vector2(float.MaxValue, float.MaxValue);
            int lowest = Tier(source);
            for (int i = 0; i + 1 < river.Count; i++)
            {
                Vector2 a = river[i], b = river[i + 1];
                float length = Vector2.Distance(a, b);
                Vector2 along = (b - a) / Mathf.Max(length, 0.001f);
                for (float d = 1f; d < length; d += 1f)
                {
                    Vector2 at = a + along * d;
                    int tier = Tier(at);
                    // Only where it comes lower than it has yet been: over a rise it has cut its way through, and falls nowhere.
                    if (tier >= lowest)
                    {
                        continue;
                    }

                    lowest = tier;
                    // On the mountain's steeps, in the lake's bank, hard on its ends or on the last fall: rapids, not a fall.
                    if (Vector2.Distance(at, source) < further * 2f || Vector2.Distance(at, mouth) < further * 2f || MountainWeight(at) > 0.75f
                        || Lake.Edge(at) < LakeBank || Vector2.Distance(at, last) < further * 2.5f)
                    {
                        continue;
                    }

                    while (laid.Count > 1 && Vector2.Distance(laid[laid.Count - 1], at) < further + 1f)
                    {
                        laid.RemoveAt(laid.Count - 1);
                    }

                    laid.AddRange(new[] { at - along * further, at - along * back, at + along * back, at + along * further });
                    Falls.Add(at);
                    last = at;
                }

                // The knot itself, unless the fall just laid has taken its place.
                if (i + 2 == river.Count || Vector2.Distance(b, last) > further + 1f)
                {
                    laid.Add(b);
                }
            }

            river.Clear();
            river.AddRange(laid);
        }
    }

    // A river runs only downhill: from its source each knot's level is the lowest ground met so far, never under the water it runs to.
    private void LevelRivers()
    {
        riverLevels.Clear();
        for (int r = 0; r < Rivers.Count; r++)
        {
            List<Vector2> river = Rivers[r];
            float floor = riverJoins[r] >= 0 ? RiverLevelNear(riverJoins[r], river[river.Count - 1]) : Lake.Level + 0.2f;
            var levels = new float[river.Count];
            float level = riverStarts[r];
            for (int i = 0; i < river.Count; i++)
            {
                level = Mathf.Max(Mathf.Min(level, Ground(river[i])), floor);
                levels[i] = level;
            }

            // It meets the water it joins at that water's level.
            levels[river.Count - 1] = floor;
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

            // A stream's valley is narrower than a river's.
            float floor = RiverWidths[r] + 0.5f;
            float reach = floor + Mathf.Min(cut * 3f, ValleyMax - floor);
            if (distance < reach)
            {
                ground -= cut * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(floor * 0.6f, reach, distance)));
            }
        }

        return ground;
    }

    // A river running into still water scours a fan into its bed and spreads its gravel there: without one its channel ends
    // against the shallows like a ditch against a wall.
    private void PlaceMouths()
    {
        for (int r = 0; r < Rivers.Count; r++)
        {
            Vector2 end = Rivers[r][Rivers[r].Count - 1];
            RaidLayoutPond water = Lake.Edge(end) < 0f ? Lake : Meres.Find(mere => mere.Edge(end) < 0f);
            if (water != null)
            {
                Mouths.Add(new Mouth { Water = water, At = end, Width = RiverWidths[r] });
            }
        }
    }

    /// <summary>How much of a river's fan lies at a point in or by the water it runs into, 0 to 1.</summary>
    public float MouthFan(Vector2 point)
    {
        float fan = 0f;
        foreach (Mouth mouth in Mouths)
        {
            float squared = (mouth.At - point).sqrMagnitude;
            if (squared < mouth.Reach * mouth.Reach && mouth.Water.Edge(point) < 2f)
            {
                fan = Mathf.Max(fan, (1f - Ease(mouth.Reach * 0.25f, mouth.Reach, Mathf.Sqrt(squared))) * Ease(2f, -1f, mouth.Water.Edge(point)));
            }
        }

        return fan;
    }

    // Every crossing of a path and a river gets a knot of its own, so the river can be made shallow just there. A stretch may be
    // crossed more than once (a road and the trail that leaves it): each crossing gets its ford.
    private void PlaceFords()
    {
        for (int r = 0; r < Rivers.Count; r++)
        {
            List<Vector2> river = Rivers[r];
            for (int i = 0; i + 1 < river.Count; i++)
            {
                Vector2 from = river[i];
                List<Vector2> crossings = Crossings(from, river[i + 1]);
                crossings.Sort((x, y) => (x - from).sqrMagnitude.CompareTo((y - from).sqrMagnitude));
                foreach (Vector2 at in crossings)
                {
                    // Close to a knot already there (the stretch's start, or the ford just laid): that knot is the ford.
                    if (Vector2.Distance(at, river[i]) < 2f)
                    {
                        AddFord(r, i);
                    }
                    else if (Vector2.Distance(at, river[i + 1]) < 2f)
                    {
                        AddFord(r, i + 1);
                    }
                    else
                    {
                        river.Insert(i + 1, at);
                        AddFord(r, i + 1);
                        i++;
                    }
                }
            }
        }
    }

    private void AddFord(int river, int knot)
    {
        if (!Fords.Contains((river, knot)))
        {
            Fords.Add((river, knot));
        }
    }

    private List<Vector2> Crossings(Vector2 a, Vector2 b)
    {
        var found = new List<Vector2>();
        foreach (List<Vector2> path in Paths)
        {
            for (int k = 0; k + 1 < path.Count; k++)
            {
                if (Intersect(a, b, path[k], path[k + 1], out Vector2 at))
                {
                    found.Add(at);
                }
            }
        }

        return found;
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
}
