using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Along the way: junctions where paths meet, small scenes beside the paths wherever a walk would otherwise pass nothing, and
/// the measure of it: the longest walk with nothing to come across, and any path that ends at nothing. Part of
/// HavenWorldPlan, in its own file.
/// <para>
/// The rule: a path always leads somewhere. Every stretch has something on it or in sight within a short walk (a glade, a
/// place, water, a cliff's ramp, a fork, a scene), and every path ends at a place, a glade, the water or another path.
/// </para>
/// </summary>
public sealed partial class HavenWorldPlan
{
    private const float WaySample = 4f;

    /// <summary>The longest walk along any path with nothing to come across, in metres.</summary>
    public float LongestDullWalk { get; private set; }

    /// <summary>Each walk longer than the recipe allows (Longest Dull Walk) with nothing on it: where it starts and ends.</summary>
    public readonly List<(Vector2 from, Vector2 to, float length)> DullWalks = new List<(Vector2, Vector2, float)>();

    /// <summary>Path ends that lead nowhere: not on another path, in a glade, at a place or at the water.</summary>
    public readonly List<Vector2> DeadEnds = new List<Vector2>();

    /// <summary>Metres of path in the world.</summary>
    public float PathMetres { get; private set; }

    /// <summary>Ground with nothing within <see cref="EmptyBeyond"/> metres of it (no place, glade, water, cliff, scene, creature or
    /// resource), as points on a grid; and its share of the land that can be walked to, 0 to 1.</summary>
    public readonly List<Vector2> EmptyGround = new List<Vector2>();
    public float EmptyShare { get; private set; }

    public const float EmptyBeyond = 45f;

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

    /// <summary>
    /// True when a walker on a path here has something to come across: a glade, a place, water, a fall, a cliff's ramp, a fork,
    /// a scene, something to gather or something to fight or hunt.
    /// </summary>
    public bool Interesting(Vector2 point, int path = -1)
    {
        if (GladeOpen(point) > 0.15f || Lake.Edge(point) < 16f || BogWeight(point) > 0.3f || Near(Rivers, point, 9f) || !OneBench(point, 5f))
        {
            return true;
        }

        if (Pois.Exists(poi => (poi.At - point).sqrMagnitude < (poi.Rule.clearing + 14f) * (poi.Rule.clearing + 14f))
            || Meres.Exists(mere => mere.Edge(point) < 16f) || Ponds.Exists(pond => pond.Edge(point) < 10f)
            || Falls.Exists(fall => (fall - point).sqrMagnitude < 28f * 28f)
            || TrailScenes.Exists(scene => (scene.At - point).sqrMagnitude < 13f * 13f)
            || Zones.Exists(zone => (zone.At - point).sqrMagnitude < (zone.Radius + 10f) * (zone.Radius + 10f))
            || Nodes.Exists(node => (node.At - point).sqrMagnitude < 9f * 9f)
            || Outcrops.Exists(outcrop => (outcrop.Centre - point).sqrMagnitude < (outcrop.Radius + 10f) * (outcrop.Radius + 10f))
            || Ravines.Exists(ravine => NearLine(ravine.Line, point, ravine.FloorWidth + 12f))
            || Rocks.Exists(rock => (rock.At - point).sqrMagnitude < 10f * 10f))
        {
            return true;
        }

        // A fork: another path leaves or joins within a few metres.
        for (int other = 0; other < Paths.Count; other++)
        {
            if (other == path)
            {
                continue;
            }

            List<Vector2> line = Paths[other];
            if ((line[0] - point).sqrMagnitude < 49f || (line[line.Count - 1] - point).sqrMagnitude < 49f)
            {
                return true;
            }
        }

        return false;
    }

    // Along every path, a scene beside the tread wherever a walk has passed nothing for a while, sides taking turns: something
    // to come across between the glades. The Haven's meadows have their own kit (recipe.meadowScenes). Not in glades, places,
    // water, ravines or on resource points.
    private void PlaceTrailScenes()
    {
        if (Recipe.trailScenes.Count == 0 && Recipe.meadowScenes.Count == 0)
        {
            return;
        }

        for (int p = 0; p < Paths.Count; p++)
        {
            List<Vector2> line = Paths[p];
            float next = Range(Recipe.trailSceneSpacing.x, Recipe.trailSceneSpacing.y) * 0.6f;
            float since = 0f;
            float side = Range(0f, 1f) < 0.5f ? 1f : -1f;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                float length = Vector2.Distance(line[i], line[i + 1]);
                Vector2 along = (line[i + 1] - line[i]) / Mathf.Max(length, 0.001f);
                for (float d = 0f; d < length; d += WaySample)
                {
                    Vector2 on = line[i] + along * d;
                    if (Interesting(on, p))
                    {
                        since = 0f;
                        continue;
                    }

                    since += WaySample;
                    if (since < next)
                    {
                        continue;
                    }

                    bool meadow = WoodsWeight(on) < 0.5f && Recipe.meadowScenes.Count > 0;
                    int kinds = meadow ? Recipe.meadowScenes.Count : Recipe.trailScenes.Count;
                    if (kinds == 0)
                    {
                        continue;
                    }

                    // This side, else the other: whichever has room.
                    foreach (float turn in new[] { side, -side })
                    {
                        Vector2 at = on + Vector2.Perpendicular(along) * turn * Range(5f, 7f);
                        if (!SceneFits(at))
                        {
                            continue;
                        }

                        // Turned to face the path it sits beside.
                        TrailScenes.Add(new Placed { Kind = random.Next(kinds), Set = meadow ? 1 : 0, At = at, Turn = BearingOf(on - at) + Range(-25f, 25f) });
                        side = -turn;
                        since = 0f;
                        next = Range(Recipe.trailSceneSpacing.x, Recipe.trailSceneSpacing.y);
                        break;
                    }
                }
            }
        }
    }

    private bool SceneFits(Vector2 at) =>
        !Near(Paths, at, 3.5f) && !InWater(at, 3f) && !Near(Rivers, at, 6f) && GladeOpen(at) < 0.05f && OneBench(at, 4f) && Reachable(at)
        && at.magnitude > Recipe.buildable + 20f
        && !Ravines.Exists(ravine => NearLine(ravine.Line, at, ravine.FloorWidth + 12f))
        && !Pois.Exists(poi => Vector2.Distance(poi.At, at) < poi.Rule.clearing + 8f)
        && !Nodes.Exists(node => (node.At - at).sqrMagnitude < 25f)
        // 20 m from the last one as the crow flies: a winding trail walks far between two spots that lie close.
        && !TrailScenes.Exists(scene => (scene.At - at).sqrMagnitude < 400f)
        && !Junctions.Exists(marker => (marker.At - at).sqrMagnitude < 36f);

    // The measure of the rule: the longest walk on any path that passes nothing, and every path end that leads to nothing.
    private void MeasureWays()
    {
        for (int p = 0; p < Paths.Count; p++)
        {
            List<Vector2> line = Paths[p];
            float dull = 0f;
            Vector2 from = line[0];
            for (int i = 0; i + 1 < line.Count; i++)
            {
                float length = Vector2.Distance(line[i], line[i + 1]);
                PathMetres += length;
                Vector2 along = (line[i + 1] - line[i]) / Mathf.Max(length, 0.001f);
                for (float d = 0f; d < length; d += WaySample)
                {
                    Vector2 on = line[i] + along * d;
                    // The Haven's own ground is home, not a walk.
                    if (on.magnitude < Recipe.buildable || Interesting(on, p))
                    {
                        Close(from, on, dull);
                        dull = 0f;
                        from = on;
                        continue;
                    }

                    dull += WaySample;
                }
            }

            Close(from, line[line.Count - 1], dull);
            foreach (Vector2 end in new[] { line[0], line[line.Count - 1] })
            {
                if (!LeadsSomewhere(p, end))
                {
                    DeadEnds.Add(end);
                }
            }
        }

        MeasureEmptiness();
    }

    // Empty ground: walked over a coarse grid, any point with nothing within EmptyBeyond metres of it.
    private void MeasureEmptiness()
    {
        const float cell = 16f;
        int walkable = 0;
        for (float y = -Half + cell * 0.5f; y < Half; y += cell)
        {
            for (float x = -Half + cell * 0.5f; x < Half; x += cell)
            {
                var point = new Vector2(x, y);
                if (point.magnitude < Recipe.buildable || InTheRim(point) || !Reachable(point) || InWater(point, 0f))
                {
                    continue;
                }

                walkable++;
                if (!SomethingNear(point, EmptyBeyond))
                {
                    EmptyGround.Add(point);
                }
            }
        }

        EmptyShare = walkable > 0 ? EmptyGround.Count / (float)walkable : 0f;
    }

    private bool SomethingNear(Vector2 point, float within) =>
        Lake.Edge(point) < within || BogWeight(point) > 0f || Near(Rivers, point, within) || !OneBench(point, within) || !OneBench(point, within * 0.5f)
        || Glades.Exists(glade => Vector2.Distance(glade.Centre, point) < glade.Radius + within)
        || Pois.Exists(poi => Vector2.Distance(poi.At, point) < poi.Rule.clearing + within)
        || Meres.Exists(mere => mere.Edge(point) < within) || Ponds.Exists(pond => pond.Edge(point) < within)
        || TrailScenes.Exists(scene => (scene.At - point).sqrMagnitude < within * within)
        || Zones.Exists(zone => Vector2.Distance(zone.At, point) < zone.Radius + within)
        || Nodes.Exists(node => (node.At - point).sqrMagnitude < within * within)
        || Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, point) < outcrop.Radius + within)
        || Ravines.Exists(ravine => NearLine(ravine.Line, point, within))
        || Rocks.Exists(rock => (rock.At - point).sqrMagnitude < within * within);

    private void Close(Vector2 from, Vector2 to, float length)
    {
        LongestDullWalk = Mathf.Max(LongestDullWalk, length);
        if (length > Recipe.longestDullWalk)
        {
            DullWalks.Add((from, to, length));
        }
    }

    private bool LeadsSomewhere(int path, Vector2 end) =>
        end.magnitude < PathStart + 6f || MeetsAnother(path, end) || GladeOpen(end) > 0.15f || Lake.Edge(end) < 10f || BogWeight(end) > 0.5f
        || Meres.Exists(mere => mere.Edge(end) < 10f)
        || Pois.Exists(poi => Vector2.Distance(poi.At, end) < poi.Rule.clearing + 10f);
}
