using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The woods' own trails and glades: loops further out, glades of each kind with a trail to them and a pool in the wet
/// ones, how open the ground is at a point, and what dresses each glade. Part of HavenWorldPlan, in its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
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
}
