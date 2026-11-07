using UnityEngine;

/// <summary>
/// Ravines and spires cut into the ground: each ravine's rim, half width and floor stamped along it and sloped in at
/// its ends, the cut with ragged walls, strata and rubble, and spires rising from scree. Part of RaidLayoutField, in
/// its own file.
/// </summary>
public sealed partial class RaidLayoutField
{
    private const float DescentLip = 0.3f;

    private const float DescentSlope = 2.6f;

    private const float DescentShortest = 25f;

    private const float NeckShoulder = 1.5f;

    private const float NeckRamp = 1.2f;

    private const float RavineBed = 0.8f;

    private const float RavineFall = 0.12f;

    private const float CascadeFrom = 2.5f;

    private const float RavineReachBeyond = 3f;

    private const float SpireWall = 1.6f;

    private const float SpireScree = 3f;

    private const float SpireScreeHeight = 1.4f;

    private const float SpireRoughness = 1.2f;

    private const float RagScale = 7f;

    private const float RagFineScale = 2.6f;

    private const float RagFine = 0.45f;

    private const float WallVarietyScale = 11f;

    private const float StrataMaskScale = 17f;

    private const int StrataBands = 3;

    private const float StrataRiser = 0.18f;

    private const float RubbleScale = 3.2f;

    private const float Rubble = 0.9f;

    private const float Talus = 0.7f;

    // A ravine's rim is the ground along its line before the cut. Its floor lies at least its depth below that and only ever falls, gently,
    // from the higher end, so a river runs the whole way down it; where the land rises over it, the ravine is deeper.
    private void BuildRavines()
    {
        if (plan.Ravines.Count == 0)
        {
            return;
        }

        ravineEdge = Filled(Far);
        ravineHalf = new float[resolution * resolution];
        ravineFloor = new float[resolution * resolution];
        foreach (RaidLayoutRavine ravine in plan.Ravines)
        {
            int count = ravine.Line.Count;
            var rim = new float[count];
            for (int i = 0; i < count; i++)
            {
                rim[i] = GroundAt(ravine.Line[i].x / plan.Size, ravine.Line[i].y / plan.Size);
            }

            // A river runs from the higher end; a ravine with a way down keeps its open end first, whatever the land does.
            if (!ravine.Descent && rim[0] < rim[count - 1])
            {
                ravine.Line.Reverse();
                ravine.HalfWidths.Reverse();
                ravine.Tapers.Reverse();
                System.Array.Reverse(rim);
            }

            var floor = new float[count];
            for (int i = 0; i < count; i++)
            {
                floor[i] = rim[i] - ravine.Depth;
                if (i > 0)
                {
                    floor[i] = Mathf.Min(floor[i], floor[i - 1]);
                }
            }

            // No steeper than a fast stream, except where the land drops by a cliff: there the river falls (a waterfall), and the gentler
            // drops ahead lower the floor before them.
            ravine.Cascades.Clear();
            for (int i = count - 2; i >= 0; i--)
            {
                if (floor[i] - floor[i + 1] >= CascadeFrom)
                {
                    ravine.Cascades.Add(i);
                    continue;
                }

                floor[i] = Mathf.Min(floor[i], floor[i + 1] + RavineFall * Vector2.Distance(ravine.Line[i], ravine.Line[i + 1]));
            }

            for (int i = 0; i < count; i++)
            {
                floor[i] = Mathf.Max(RavineFloorLowest, floor[i]);
            }

            if (ravine.Descent)
            {
                SlopeIn(ravine, rim, floor);
            }

            ravine.Rim = rim;
            ravine.Floor = floor;
            for (int i = 0; i + 1 < count; i++)
            {
                StampRavine(ravine.Line[i], ravine.Line[i + 1], ravine.HalfWidths[i], ravine.HalfWidths[i + 1], floor[i], floor[i + 1]);
            }
        }
    }

    // The open end's floor rises to just under the rim, steady enough to walk (about 21 degrees), and the cascades there are gone.
    private static void SlopeIn(RaidLayoutRavine ravine, float[] rim, float[] floor)
    {
        int count = floor.Length;
        float total = 0f;
        for (int i = 0; i + 1 < count; i++)
        {
            total += Vector2.Distance(ravine.Line[i], ravine.Line[i + 1]);
        }

        float top = rim[0] - DescentLip;
        float run = 0f;
        float length = Mathf.Clamp((top - floor[count / 2]) * DescentSlope, DescentShortest, total * 0.6f);
        int foot = 0;
        for (int i = 0; i < count && run < length; i++)
        {
            foot = i;
            if (i > 0)
            {
                run += Vector2.Distance(ravine.Line[i - 1], ravine.Line[i]);
            }
        }

        float bottom = floor[foot];
        run = 0f;
        for (int i = 0; i <= foot; i++)
        {
            if (i > 0)
            {
                run += Vector2.Distance(ravine.Line[i - 1], ravine.Line[i]);
            }

            floor[i] = Mathf.Max(floor[i], Mathf.Lerp(top, bottom, Mathf.SmoothStep(0f, 1f, run / Mathf.Max(0.01f, length))));
        }

        ravine.Cascades.RemoveAll(at => at < foot);
    }

    private void StampRavine(Vector2 from, Vector2 to, float halfFrom, float halfTo, float floorFrom, float floorTo)
    {
        float extent = Mathf.Max(halfFrom, halfTo) + RavineReachBeyond;
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
                if (edge < ravineEdge[index])
                {
                    ravineEdge[index] = edge;
                    ravineHalf[index] = half;
                    ravineFloor[index] = Mathf.Lerp(floorFrom, floorTo, t);
                }
            }
        }
    }

    // Rock walls from the floor's edge to the rim, ragged in plan, wider or narrower and here and there stepped in shelves (strata); rubble
    // along their foot and boulders in the bed, some breaking the water. A trail crossing keeps a neck of solid ground.
    private float Cut(float u, float v, float ground)
    {
        float edge = Sample(ravineEdge, u, v);
        if (edge >= RaidLayoutRavine.RagReach)
        {
            return ground;
        }

        float x = u * plan.Size, y = v * plan.Size;
        edge -= (Noise(x, y, RagScale, 401f) - 0.5f) * 2f * (RaidLayoutRavine.RagReach - RagFine) + (Noise(x, y, RagFineScale, 433f) - 0.5f) * 2f * RagFine;
        if (edge >= 0f)
        {
            return ground;
        }

        float half = Sample(ravineHalf, u, v);
        float wall = RaidLayoutRavine.WallRun(half) * Mathf.Lerp(0.75f, 2.3f, Noise(x, y, WallVarietyScale, 419f));
        float up = Mathf.Clamp01(1f + edge / wall);
        float rise = Mathf.SmoothStep(0f, 1f, up);
        float strata = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.7f, Noise(x, y, StrataMaskScale, 409f)));
        if (strata > 0f)
        {
            float band = up * StrataBands + (Noise(x, y, RagScale, 421f) - 0.5f) * 0.6f;
            float step = Mathf.Floor(band);
            float stepped = Mathf.Clamp01((step + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f - StrataRiser, 0.5f + StrataRiser, band - step))) / StrataBands);
            rise = Mathf.Lerp(rise, stepped, strata);
        }

        float floorHalf = Mathf.Max(0.5f, half - wall);
        float across = Mathf.Clamp01((edge + half) / floorHalf);
        float rubble = Mathf.Max(0f, Noise(x, y, RubbleScale, 457f) - 0.5f) * 2f * Rubble + across * across * across * Talus;
        float floor = Sample(ravineFloor, u, v) - RavineBed * (1f - across * across) + rubble;
        float cut = Mathf.Min(ground, Mathf.Lerp(floor, ground, rise));
        float neck = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Sample(trailDistance, u, v) - NeckShoulder) / NeckRamp));
        return Mathf.Lerp(ground, cut, neck);
    }

    // A flat, rough top on sheer sides, with a little scree around the foot.
    private float Spires(Vector2 point, float ground)
    {
        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            float far = spire.Reach + SpireScree;
            if ((point - spire.Position).sqrMagnitude > far * far)
            {
                continue;
            }

            float edge = spire.Edge(point);
            float scree = Mathf.Clamp01(1f - edge / SpireScree);
            float body = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge + SpireWall) / SpireWall));
            float top = spire.Top + (Noise(point.x, point.y, RoughnessScale * 0.5f, 191f) - 0.5f) * 2f * SpireRoughness;
            ground = Mathf.Max(ground + scree * scree * SpireScreeHeight, Mathf.Lerp(ground, top, body));
        }

        return ground;
    }
}
