using UnityEngine;

/// <summary>
/// The ground the field gives the terrain: the height at a point (levels, ponds sunk into basins, ravines, rivers and
/// spires on top), the height map in metres or scaled, what is kept clear of trees and rocks, and the path, forest and
/// clearing zones. Part of RaidLayoutField, in its own file.
/// </summary>
public sealed partial class RaidLayoutField
{
    private const float SoilEdge = 1.5f;

    private const float WobbleScale = 11f;

    private const float RoughFrom = 9f;

    private const float RidgeVariety = 0.55f;

    private const float RidgeVarietyScale = 46f;

    private const float PondBank = 3.5f;

    private const float PondBelowRim = 0.12f;

    private const float PondRimBeyond = 2f;

    private const int PondRimSamples = 24;

    private const float PondSoil = 2.4f;

    private const float RiverIntoBank = 0.5f;

    private const float RavineSoil = 1f;

    private const float KeepClearBeyond = 1.5f;

    /// <summary>
    /// True where nothing may stand, in metres from the map's south-west corner: in a ravine or at its very edge, and on a spire. What grows
    /// or lies there could never be reached, so trees, rocks and resources keep off.
    /// </summary>
    public bool KeepsClear(Vector2 point)
    {
        if (ravineEdge != null && Sample(ravineEdge, point.x / plan.Size, point.y / plan.Size) < KeepClearBeyond + RaidLayoutRavine.RagReach)
        {
            return true;
        }

        if (riverEdge != null && Sample(riverEdge, point.x / plan.Size, point.y / plan.Size) < RiverIntoBank)
        {
            return true;
        }

        foreach (RaidLayoutSpire spire in plan.Spires)
        {
            if ((point - spire.Position).sqrMagnitude < (spire.Reach + KeepClearBeyond) * (spire.Reach + KeepClearBeyond)
                && spire.Edge(point) < KeepClearBeyond)
            {
                return true;
            }
        }

        return false;
    }

    // Just below the lowest ground around it, so the water never stands above its own banks.
    private float PondLevel(RaidLayoutPond pond)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < PondRimSamples; i++)
        {
            Vector2 rim = pond.Rim(i * Mathf.PI * 2f / PondRimSamples, PondRimBeyond);
            lowest = Mathf.Min(lowest, GroundAt(rim.x / plan.Size, rim.y / plan.Size));
        }

        return lowest - PondBelowRim;
    }

    /// <summary>The ground's height at (u, v), in metres above the map's lowest point.</summary>
    public float HeightAt(float u, float v)
    {
        float height = GroundAt(u, v);
        if (plan.Ponds.Count == 0 && plan.Spires.Count == 0 && ravineEdge == null && riverEdge == null)
        {
            return height;
        }

        var point = new Vector2(u * plan.Size, v * plan.Size);
        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            height = Basin(pond, point, height);
        }

        if (riverEdge != null)
        {
            height = RiverCut(u, v, height);
        }

        if (plan.Spires.Count > 0)
        {
            height = Spires(point, height);
        }

        return ravineEdge != null ? Cut(u, v, height) : height;
    }

    // The bed sinks from the shore to the pond's depth; the bank leads back up to the ground around it.
    private static float Basin(RaidLayoutPond pond, Vector2 point, float ground)
    {
        float far = pond.Reach + PondBank;
        if ((point - pond.Position).sqrMagnitude >= far * far)
        {
            return ground;
        }

        float edge = pond.Edge(point);
        if (edge >= PondBank)
        {
            return ground;
        }

        if (edge >= 0f)
        {
            return Mathf.Lerp(pond.Level, ground, Mathf.SmoothStep(0f, 1f, edge / PondBank));
        }

        float slope = Mathf.Min(Mathf.Min(pond.Radii.x, pond.Radii.y) * 0.8f, 3.5f);
        return pond.Level - pond.Depth * Mathf.SmoothStep(0f, 1f, -edge / slope);
    }

    private float GroundAt(float u, float v)
    {
        float x = u * plan.Size;
        float y = v * plan.Size;
        float open = Mathf.Max(0f, OpenDistance(u, v));
        float ground = rules.GroundHeight + (Noise(x, y, rules.HillScale, 0f) - 0.5f) * 2f * rules.Hills * plan.Hilliness;
        if (plan.Hills.Count > 0)
        {
            var point = new Vector2(x, y);
            foreach (RaidLayoutHill hill in plan.Hills)
            {
                ground += hill.RiseAt(point);
            }
        }

        float variety = 1f + (Noise(x, y, RidgeVarietyScale, 31f) - 0.5f) * 2f * RidgeVariety;
        float ridges = plan.Regions.Count > 1 ? plan.Blend(new Vector2(x, y), mood => mood.ridges) : plan.Mood.ridges;
        float ridge = Mathf.SmoothStep(0f, 1f, open / rules.RidgeWidth) * rules.RidgeHeight * ridges * variety;
        float rough = (Noise(x, y, RoughnessScale, 67f) - 0.5f) * 2f * rules.Roughness * Mathf.Clamp01(open / RoughFrom);
        float edge = Mathf.Min(x, Mathf.Min(y, Mathf.Min(plan.Size - x, plan.Size - y)));
        float rim = Mathf.SmoothStep(0f, 1f, 1f - edge / rules.RimWidth) * rules.RimHeight;
        float level = levelHeight != null ? Sample(levelHeight, u, v) : 0f;
        return ground + ridge + rough + rim + level + lift;
    }

    /// <summary>The heights as a terrain takes them (rows from the south, 0–1 of the map's height).</summary>
    public float[,] Heights(int size, float mapHeight) => Scaled(Metres(size, out _), mapHeight);

    /// <summary>The heights in metres above the map's lowest point (rows from the south), and the highest of them.</summary>
    public float[,] Metres(int size, out float highest)
    {
        var heights = new float[size, size];
        var rowHighest = new float[size];
        float last = size - 1f;
        // Row by row on every core: HeightAt only reads what Build worked out.
        System.Threading.Tasks.Parallel.For(0, size, row =>
        {
            float top = 0f;
            for (int column = 0; column < size; column++)
            {
                float height = HeightAt(column / last, row / last);
                heights[row, column] = height;
                top = Mathf.Max(top, height);
            }

            rowHighest[row] = top;
        });
        highest = 0f;
        foreach (float top in rowHighest)
        {
            highest = Mathf.Max(highest, top);
        }

        return heights;
    }

    /// <summary>Heights in metres as 0-1 of a terrain this many metres high, in place.</summary>
    public static float[,] Scaled(float[,] metres, float mapHeight)
    {
        int rows = metres.GetLength(0), columns = metres.GetLength(1);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                metres[row, column] = Mathf.Clamp01(metres[row, column] / mapHeight);
            }
        }

        return metres;
    }

    /// <summary>The painted zones a map needs, each resolution × resolution with row 0 at the south.</summary>
    public void Zones(out byte[] path, out byte[] forest, out byte[] clearing)
    {
        path = new byte[resolution * resolution];
        forest = new byte[resolution * resolution];
        clearing = new byte[resolution * resolution];
        float density = rules.ForestDensity * plan.Mood.forest;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = y * resolution + x;
                float worldX = x * cell;
                float worldY = y * cell;
                // A little noise on the edges, so a clearing is never a perfect circle.
                float wobble = (Noise(worldX, worldY, WobbleScale, 11f) - 0.5f) * 2f * EdgeWobble;
                float patches = 0.55f + 0.9f * Noise(worldX, worldY, rules.ForestPatches, 113f);
                path[index] = Byte(1f - Mathf.SmoothStep(0f, 1f, (trailDistance[index] + SoilEdge) / (SoilEdge * 2f)));
                clearing[index] = Byte(1f - Mathf.SmoothStep(0f, 1f, (clearDistance[index] + wobble + SoilEdge) / (SoilEdge * 2f)));
                float here = plan.Regions.Count > 1 ? rules.ForestDensity * plan.Blend(new Vector2(worldX, worldY), mood => mood.forest) : density;
                forest[index] = Byte(here * patches * Mathf.SmoothStep(0f, 1f, (openDistance[index] + wobble) / ForestEdge));
                if (ravineEdge != null && ravineEdge[index] < 0f)
                {
                    // Gravel on a ravine's floor, which the walls' rock gives way to.
                    float floorEdge = -RaidLayoutRavine.WallRun(ravineHalf[index]);
                    float gravel = 1f - Mathf.SmoothStep(0f, 1f, (ravineEdge[index] - floorEdge + RavineSoil) / RavineSoil);
                    path[index] = (byte)Mathf.Max(path[index], Byte(gravel));
                }
            }
        }

        // A bed of bare soil under the water and along the shore: trees, rocks and plants keep off it as they do off a trail.
        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            float far = pond.Reach + PondSoil;
            int xMin = Mathf.Max(0, Mathf.FloorToInt((pond.Position.x - far) / cell));
            int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((pond.Position.x + far) / cell));
            int yMin = Mathf.Max(0, Mathf.FloorToInt((pond.Position.y - far) / cell));
            int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt((pond.Position.y + far) / cell));
            for (int y = yMin; y <= yMax; y++)
            {
                for (int x = xMin; x <= xMax; x++)
                {
                    float soil = 1f - Mathf.SmoothStep(0f, 1f, pond.Edge(new Vector2(x * cell, y * cell)) / PondSoil);
                    path[y * resolution + x] = (byte)Mathf.Max(path[y * resolution + x], Byte(soil));
                }
            }
        }
    }

    private static byte Byte(float strength) => (byte)Mathf.RoundToInt(Mathf.Clamp01(strength) * 255f);
}
