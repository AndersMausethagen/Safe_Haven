using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Paths: roads out from the Haven and the ring joining them, wandering a little, and the points of interest along them
/// with a spur each. Part of HavenWorldPlan, in its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
    private const float PathStart = 32f;

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
}
