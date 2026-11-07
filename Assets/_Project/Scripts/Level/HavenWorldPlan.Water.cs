using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Water: the lake and its islands, rivers down to it levelled along their way and cut into valleys, ponds, the lake's
/// landings, and fords where paths cross rivers. Part of HavenWorldPlan, in its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
    private const float ValleyFloor = 5f;

    private const float ValleyMax = 40f;

    private const float RiverFromHome = 95f;

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
}
