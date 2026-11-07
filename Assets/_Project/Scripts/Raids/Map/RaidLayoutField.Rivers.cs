using UnityEngine;

/// <summary>
/// Rivers cut through the open land: their banks, water level and depth stamped along them, shallower at fords, and how
/// wet the ground near them is. Part of RaidLayoutField, in its own file.
/// </summary>
public sealed partial class RaidLayoutField
{
    private const float RiverBank = 3f;

    private const float RiverFordFade = 4f;

    private const float RiverFordBank = 3f;

    private const float RiverFreeboard = 0.35f;

    private const float RiverWaterline = 0.2f;

    private const float RiverReachBeyond = 10f;

    // A river's surface lies just under the ground along it and both its banks, and only ever falls, from the higher end; where the land
    // rises across its way it cuts a deeper bed through. Its channel is cut down from that surface (RiverCut).
    private void BuildRivers()
    {
        if (plan.Rivers.Count == 0)
        {
            return;
        }

        riverEdge = Filled(Far);
        riverHalf = new float[resolution * resolution];
        riverLevel = new float[resolution * resolution];
        riverDepth = new float[resolution * resolution];
        foreach (RaidLayoutRiver river in plan.Rivers)
        {
            int count = river.Line.Count;
            var surface = new float[count];
            for (int i = 0; i < count; i++)
            {
                Vector2 point = river.Line[i];
                Vector2 along = (river.Line[Mathf.Min(i + 1, count - 1)] - river.Line[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 side = new Vector2(-along.y, along.x) * (river.HalfWidths[i] + RiverBank * 0.5f);
                float low = Mathf.Min(GroundAt(point.x / plan.Size, point.y / plan.Size),
                    Mathf.Min(GroundAt((point.x + side.x) / plan.Size, (point.y + side.y) / plan.Size), GroundAt((point.x - side.x) / plan.Size, (point.y - side.y) / plan.Size)));
                surface[i] = low - RiverFreeboard;
            }

            if (surface[0] < surface[count - 1])
            {
                river.Line.Reverse();
                river.HalfWidths.Reverse();
                System.Array.Reverse(surface);
            }

            for (int i = 1; i < count; i++)
            {
                surface[i] = Mathf.Max(RavineFloorLowest + river.Depth, Mathf.Min(surface[i], surface[i - 1]));
            }

            river.Level = surface;
            for (int i = 0; i + 1 < count; i++)
            {
                StampRiver(river.Line[i], river.Line[i + 1], river.HalfWidths[i], river.HalfWidths[i + 1], surface[i], surface[i + 1], river.Depth);
            }
        }
    }

    private void StampRiver(Vector2 from, Vector2 to, float halfFrom, float halfTo, float levelFrom, float levelTo, float depth)
    {
        float extent = Mathf.Max(halfFrom, halfTo) + RiverReachBeyond;
        int xMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.x, to.x) - extent) / cell));
        int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.x, to.x) + extent) / cell));
        int yMin = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(from.y, to.y) - extent) / cell));
        int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(from.y, to.y) + extent) / cell));
        Vector2 along = to - from;
        float lengthSquared = Mathf.Max(0.0001f, along.sqrMagnitude);
        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                var point = new Vector2(x * cell, y * cell);
                float t = Mathf.Clamp01(Vector2.Dot(point - from, along) / lengthSquared);
                float half = Mathf.Lerp(halfFrom, halfTo, t);
                float edge = Vector2.Distance(point, from + along * t) - half;
                int index = y * resolution + x;
                if (edge < riverEdge[index])
                {
                    riverEdge[index] = edge;
                    riverHalf[index] = half;
                    riverLevel[index] = Mathf.Lerp(levelFrom, levelTo, t);
                    riverDepth[index] = depth;
                }
            }
        }
    }

    // A bed deepest in the middle, shallow at a ford where a trail crosses, and banks from the waterline up to the ground: gentle at a
    // ford, so the trail walks down into the water.
    private float RiverCut(float u, float v, float ground)
    {
        float edge = Sample(riverEdge, u, v);
        if (edge >= RiverBank + RiverFordBank)
        {
            return ground;
        }

        float half = Mathf.Max(0.5f, Sample(riverHalf, u, v));
        float level = Sample(riverLevel, u, v);
        float ford = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Sample(trailDistance, u, v) / RiverFordFade));
        float bank = RiverBank + RiverFordBank * ford;
        if (edge >= bank)
        {
            return ground;
        }

        float height;
        if (edge < 0f)
        {
            float across = Mathf.Clamp01((edge + half) / half);
            float deep = Mathf.Lerp(Sample(riverDepth, u, v), rules.FordDepth, ford);
            height = Mathf.Lerp(level - deep, level - RiverWaterline, across * across);
        }
        else
        {
            height = Mathf.Lerp(level - RiverWaterline, ground, Mathf.SmoothStep(0f, 1f, edge / bank));
        }

        return Mathf.Min(ground, height);
    }

    /// <summary>How wet the ground is by the rivers at a point (metres from the map's corner): 1 at the water, fading over <paramref name="reach"/>.</summary>
    public float RiverWetness(Vector2 point, float reach)
    {
        if (riverEdge == null || reach <= 0f)
        {
            return 0f;
        }

        return 1f - Mathf.Clamp01(Sample(riverEdge, point.x / plan.Size, point.y / plan.Size) / reach);
    }
}
