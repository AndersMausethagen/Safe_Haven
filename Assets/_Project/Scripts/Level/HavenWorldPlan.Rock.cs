using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Rock: outcrops, ravines grown through the woods, and the mountain's scattered rocks. (The cliffs between the terraces are
/// traced from the land: HavenWorldPlan.Terraces.)
/// Part of HavenWorldPlan, in its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
    private const float RavineStep = 6f;

    private const float RavineTurn = 10f;

    private void PlaceOutcrops()
    {
        int count = random.Next(Recipe.outcrops.x, Recipe.outcrops.y + 1);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                // A third in the Haven's ring (smaller), the rest in the woods.
                bool haven = i < count / 3;
                Vector2 at = haven ? Around(Recipe.buildable + 40f, Recipe.haven + 30f) : Around(Recipe.haven + 30f, MaxReach);
                float radius = haven ? Range(7f, 12f) : Range(10f, 22f);
                if (Near(Paths, at, radius + 8f) || Near(Rivers, at, radius + 10f) || InWater(at, radius + 14f)
                    || Pois.Exists(poi => Vector2.Distance(poi.At, at) < radius + poi.Rule.clearing + 8f)
                    || Outcrops.Exists(other => Vector2.Distance(other.Centre, at) < other.Radius + radius + 15f) || MountainWeight(at) > 0.3f
                    || BogWeight(at) > 0f || !OneBench(at, radius + 6f))
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

    // In the woods, more of them further out; never across a path (a path has no neck over one), a river or the lake.
    private void PlaceRavines()
    {
        int count = random.Next(Recipe.ravines.x, Recipe.ravines.y + 1);
        for (int i = 0; i < count; i++)
        {
            for (int tries = 0; tries < Tries; tries++)
            {
                Vector2 start = Around(Recipe.forest * 0.85f, MaxReach);
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
            && BogWeight(point) <= 0f
            && !InWater(point, 20f) && !Pois.Exists(poi => Vector2.Distance(poi.At, point) < poi.Rule.clearing + 14f)
            && !Outcrops.Exists(outcrop => Vector2.Distance(outcrop.Centre, point) < outcrop.Radius + 12f)
            && !Ravines.Exists(other => NearLine(other.Line, point, 28f));
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
}
