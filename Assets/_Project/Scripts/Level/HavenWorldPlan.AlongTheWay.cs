using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Along the way: junctions where paths meet, and small scenes beside the trails. Part of HavenWorldPlan, in its own
/// file.
/// </summary>
public sealed partial class HavenWorldPlan
{
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
}
