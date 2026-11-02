using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Turns a RaidLayoutPlan into ground: how far every spot lies from open ground, and from that the heights and the painted
/// zones a hand-made map would have. The trails run along the valley floors and the wild ground rises between them, so
/// what the plan joined is what the player can walk. Plain arithmetic over grids, free of the terrain, so it can be tested.
/// </summary>
public sealed class RaidLayoutField
{
    private const float SoilEdge = 1.5f;
    private const float ForestEdge = 7f;
    private const float EdgeWobble = 2.5f;
    private const float WobbleScale = 11f;
    private const float RoughnessScale = 9f;
    private const float RoughFrom = 9f;
    private const float RidgeVariety = 0.55f;
    private const float RidgeVarietyScale = 46f;
    private const float SpurWidth = 0.6f;
    private const float PondShore = 5f;
    private const float PondBank = 3.5f;
    private const float PondBelowRim = 0.12f;
    private const float PondRimBeyond = 2f;
    private const int PondRimSamples = 24;
    private const float PondSoil = 2.4f;

    private readonly RaidLayoutPlan plan;
    private readonly RaidLayoutRules rules;
    private readonly int resolution;
    private readonly float cell;
    private readonly float reach;
    private readonly float[] openDistance;
    private readonly float[] trailDistance;
    private readonly float[] clearDistance;

    private RaidLayoutField(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        this.plan = plan;
        this.rules = rules;
        resolution = rules.FieldResolution;
        cell = plan.Size / (resolution - 1);
        reach = rules.RidgeWidth + EdgeWobble * 2f + ForestEdge;
        openDistance = Filled(reach);
        trailDistance = Filled(reach);
        clearDistance = Filled(reach);
    }

    public int Resolution => resolution;

    public static RaidLayoutField Build(RaidLayoutPlan plan, RaidLayoutRules rules)
    {
        var field = new RaidLayoutField(plan, rules);
        float halfWidth = rules.TrailHalfWidth * plan.Mood.trails;
        foreach (List<Vector2> trail in plan.Trails)
        {
            field.StampLine(trail, halfWidth, field.trailDistance);
        }

        foreach (List<Vector2> spur in plan.Spurs)
        {
            field.StampLine(spur, halfWidth * SpurWidth, field.trailDistance);
        }

        foreach (RaidLayoutRoom room in plan.Rooms)
        {
            field.Stamp(room.Position, room.Position, room.Radius, field.clearDistance);
        }

        foreach ((Vector2 centre, float radius) in plan.Pockets)
        {
            field.Stamp(centre, centre, radius, field.clearDistance);
        }

        for (int i = 0; i < field.openDistance.Length; i++)
        {
            field.openDistance[i] = Mathf.Min(field.trailDistance[i], field.clearDistance[i]);
        }

        // Ponds lie on open ground: the ridges keep back and the forest thins, though only trails and clearings are soil.
        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            field.Stamp(pond.Position, pond.Position, pond.Reach + PondShore, field.openDistance);
        }

        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            pond.Level = field.PondLevel(pond);
        }

        return field;
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

    private float[] Filled(float value)
    {
        var grid = new float[resolution * resolution];
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = value;
        }

        return grid;
    }

    private void StampLine(List<Vector2> line, float radius, float[] grid)
    {
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Stamp(line[i], line[i + 1], radius, grid);
        }
    }

    // Writes the distance to a capsule into the cells near it, keeping whatever is nearer.
    private void Stamp(Vector2 from, Vector2 to, float radius, float[] grid)
    {
        float extent = radius + reach;
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
                float distance = Vector2.Distance(point, from + along * t) - radius;
                int index = y * resolution + x;
                if (distance < grid[index])
                {
                    grid[index] = distance;
                }
            }
        }
    }

    private float Sample(float[] grid, float u, float v)
    {
        float x = Mathf.Clamp01(u) * (resolution - 1);
        float y = Mathf.Clamp01(v) * (resolution - 1);
        int x0 = Mathf.Min((int)x, resolution - 2);
        int y0 = Mathf.Min((int)y, resolution - 2);
        float tx = x - x0;
        float ty = y - y0;
        float bottom = Mathf.Lerp(grid[y0 * resolution + x0], grid[y0 * resolution + x0 + 1], tx);
        float top = Mathf.Lerp(grid[(y0 + 1) * resolution + x0], grid[(y0 + 1) * resolution + x0 + 1], tx);
        return Mathf.Lerp(bottom, top, ty);
    }

    private float Noise(float x, float y, float scale, float salt) =>
        Mathf.PerlinNoise((x + plan.NoiseOffset.x + salt) / scale, (y + plan.NoiseOffset.y - salt) / scale);

    /// <summary>The metres from (u, v) to the nearest trail or clearing: 0 or less is open ground.</summary>
    public float OpenDistance(float u, float v) => Sample(openDistance, u, v);

    /// <summary>The ground's height at (u, v), in metres above the map's lowest point.</summary>
    public float HeightAt(float u, float v)
    {
        float height = GroundAt(u, v);
        if (plan.Ponds.Count == 0)
        {
            return height;
        }

        var point = new Vector2(u * plan.Size, v * plan.Size);
        foreach (RaidLayoutPond pond in plan.Ponds)
        {
            height = Basin(pond, point, height);
        }

        return height;
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
        float ground = rules.GroundHeight + (Noise(x, y, rules.HillScale, 0f) - 0.5f) * 2f * rules.Hills;
        float variety = 1f + (Noise(x, y, RidgeVarietyScale, 31f) - 0.5f) * 2f * RidgeVariety;
        float ridge = Mathf.SmoothStep(0f, 1f, open / rules.RidgeWidth) * rules.RidgeHeight * plan.Mood.ridges * variety;
        float rough = (Noise(x, y, RoughnessScale, 67f) - 0.5f) * 2f * rules.Roughness * Mathf.Clamp01(open / RoughFrom);
        float edge = Mathf.Min(x, Mathf.Min(y, Mathf.Min(plan.Size - x, plan.Size - y)));
        float rim = Mathf.SmoothStep(0f, 1f, 1f - edge / rules.RimWidth) * rules.RimHeight;
        return ground + ridge + rough + rim;
    }

    /// <summary>The heights as a terrain takes them (rows from the south, 0–1 of the map's height).</summary>
    public float[,] Heights(int size, float mapHeight)
    {
        var heights = new float[size, size];
        float last = size - 1f;
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                heights[row, column] = Mathf.Clamp01(HeightAt(column / last, row / last) / mapHeight);
            }
        }

        return heights;
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
                forest[index] = Byte(density * patches * Mathf.SmoothStep(0f, 1f, (openDistance[index] + wobble) / ForestEdge));
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
