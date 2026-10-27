using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>A spline sampled into a polyline on the ground plane: points, flat directions, distance along it (metres,
/// on the ground) and knot-space position (for per-knot values). Closed splines repeat the first point at the end.</summary>
public sealed class SplineLine
{
    public readonly Vector3[] Points;
    public readonly Vector2[] Directions; // flat (x, z), normalised
    public readonly float[] Along;
    public readonly float[] KnotT;
    public readonly bool Closed;
    public readonly int KnotCount;

    public SplineLine(Vector3[] points, float[] knotT, bool closed, int knotCount)
    {
        Points = points;
        KnotT = knotT;
        Closed = closed;
        KnotCount = knotCount;
        Directions = new Vector2[points.Length];
        Along = new float[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            if (i > 0)
            {
                Along[i] = Along[i - 1] + Flat(points[i] - points[i - 1]).magnitude;
            }

            Vector3 delta = i + 1 < points.Length ? points[i + 1] - points[i] : points[i] - points[Mathf.Max(0, i - 1)];
            Vector2 flat = Flat(delta);
            Directions[i] = flat.sqrMagnitude > 1e-8f ? flat.normalized : (i > 0 ? Directions[i - 1] : Vector2.up);
        }
    }

    public int Count => Points.Length;
    public float Length => Along.Length > 0 ? Along[Along.Length - 1] : 0f;

    /// <summary>Samples the container's first spline every <paramref name="step"/> metres (world space).</summary>
    public static SplineLine Sample(SplineContainer container, float step)
    {
        Spline spline = container.Spline;
        float length = container.CalculateLength();
        int count = Mathf.Max(2, Mathf.CeilToInt(length / Mathf.Max(step, 0.05f)) + 1);
        var points = new Vector3[count];
        var knotT = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)(count - 1);
            container.Evaluate(t, out float3 position, out float3 _, out float3 _);
            points[i] = position;
            knotT[i] = spline.ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Knot);
        }

        if (spline.Closed)
        {
            knotT[count - 1] = spline.Count; // wraps to the first knot
        }

        return new SplineLine(points, knotT, spline.Closed, spline.Count);
    }

    public static Vector2 Flat(Vector3 v) => new Vector2(v.x, v.z);

    /// <summary>Linear interpolation of a per-sample value at segment <paramref name="segment"/>, fraction <paramref name="f"/>.</summary>
    public float Lerp(float[] values, int segment, float f) =>
        segment + 1 < values.Length ? Mathf.Lerp(values[segment], values[segment + 1], f) : values[values.Length - 1];

    public Vector3 PointAt(int segment, float f) =>
        segment + 1 < Points.Length ? Vector3.Lerp(Points[segment], Points[segment + 1], f) : Points[Points.Length - 1];

    /// <summary>The per-knot value at a sample position.</summary>
    public float KnotValue(KnotValues values, int segment, float f) =>
        values.Evaluate(Lerp(KnotT, segment, f), KnotCount, Closed);

    /// <summary>Distance on the ground to the nearest point of the line, with the segment and fraction there.</summary>
    public float Nearest(Vector2 xz, out int segment, out float fraction)
    {
        float best = float.MaxValue;
        segment = 0;
        fraction = 0f;
        for (int i = 0; i + 1 < Points.Length; i++)
        {
            float d = SegmentDistance(xz, Flat(Points[i]), Flat(Points[i + 1]), out float f);
            if (d < best)
            {
                best = d;
                segment = i;
                fraction = f;
            }
        }

        return best;
    }

    /// <summary>Whether the point is inside the closed line (even-odd rule on the ground plane).</summary>
    public bool Inside(Vector2 xz)
    {
        bool inside = false;
        for (int i = 0, j = Points.Length - 1; i < Points.Length; j = i++)
        {
            Vector2 a = Flat(Points[i]);
            Vector2 b = Flat(Points[j]);
            if ((a.y > xz.y) != (b.y > xz.y) && xz.x < (b.x - a.x) * (xz.y - a.y) / (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    public Rect FlatBounds(float margin)
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (Vector3 p in Points)
        {
            minX = Mathf.Min(minX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxX = Mathf.Max(maxX, p.x);
            maxZ = Mathf.Max(maxZ, p.z);
        }

        return Rect.MinMaxRect(minX - margin, minZ - margin, maxX + margin, maxZ + margin);
    }

    public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b, out float fraction)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        fraction = lengthSq > 1e-10f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
        return Vector2.Distance(p, a + ab * fraction);
    }
}

/// <summary>A terrain's sample grid in world space: heightmap samples sit on the corners (resolution - 1 cells across),
/// alphamap and detail samples in the middle of their cells.</summary>
public readonly struct TerrainGrid
{
    public readonly Vector2 Origin;
    public readonly Vector2 Spacing;
    public readonly int Resolution;
    public readonly bool Corners;

    public TerrainGrid(Vector2 origin, Vector2 size, int resolution, bool corners)
    {
        Origin = origin;
        Resolution = resolution;
        Corners = corners;
        Spacing = corners ? size / Mathf.Max(1, resolution - 1) : size / Mathf.Max(1, resolution);
    }

    public static TerrainGrid Heights(Terrain terrain) =>
        new TerrainGrid(SplineLine.Flat(terrain.transform.position), SplineLine.Flat(terrain.terrainData.size),
                        terrain.terrainData.heightmapResolution, true);

    public static TerrainGrid Alphas(Terrain terrain) =>
        new TerrainGrid(SplineLine.Flat(terrain.transform.position), SplineLine.Flat(terrain.terrainData.size),
                        terrain.terrainData.alphamapResolution, false);

    public static TerrainGrid Details(Terrain terrain) =>
        new TerrainGrid(SplineLine.Flat(terrain.transform.position), SplineLine.Flat(terrain.terrainData.size),
                        terrain.terrainData.detailResolution, false);

    public Vector2 World(int x, int y) => Corners
        ? Origin + new Vector2(x * Spacing.x, y * Spacing.y)
        : Origin + new Vector2((x + 0.5f) * Spacing.x, (y + 0.5f) * Spacing.y);

    /// <summary>The cells whose samples fall inside the world rectangle (clamped to the grid; may be empty).</summary>
    public RectInt Cells(Rect world)
    {
        float offset = Corners ? 0f : 0.5f;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((world.xMin - Origin.x) / Spacing.x - offset), 0, Resolution);
        int y0 = Mathf.Clamp(Mathf.FloorToInt((world.yMin - Origin.y) / Spacing.y - offset), 0, Resolution);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((world.xMax - Origin.x) / Spacing.x - offset) + 1, 0, Resolution);
        int y1 = Mathf.Clamp(Mathf.CeilToInt((world.yMax - Origin.y) / Spacing.y - offset) + 1, 0, Resolution);
        return new RectInt(x0, y0, Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
    }

    /// <summary>Continuous grid coordinates of a world point (for bilinear sampling).</summary>
    public Vector2 GridPosition(Vector2 world)
    {
        Vector2 g = new Vector2((world.x - Origin.x) / Spacing.x, (world.y - Origin.y) / Spacing.y);
        return Corners ? g : g - new Vector2(0.5f, 0.5f);
    }
}

/// <summary>For every cell of a grid rectangle near a line: the distance to it, which side (+1 left of the direction
/// of travel, -1 right; closed lines: +1 inside), and where along it (segment, fraction). Cells farther than the reach
/// have distance = infinity.</summary>
public sealed class LineField
{
    public readonly RectInt Rect;
    public readonly float[] Distance;
    public readonly sbyte[] Side;
    public readonly int[] Segment;
    public readonly float[] Fraction;

    private LineField(RectInt rect)
    {
        Rect = rect;
        int count = rect.width * rect.height;
        Distance = new float[count];
        Side = new sbyte[count];
        Segment = new int[count];
        Fraction = new float[count];
        for (int i = 0; i < count; i++)
        {
            Distance[i] = float.PositiveInfinity;
        }
    }

    public int IndexOf(int x, int y) => (y - Rect.y) * Rect.width + (x - Rect.x);

    public bool Contains(int x, int y) => x >= Rect.xMin && x < Rect.xMax && y >= Rect.yMin && y < Rect.yMax;

    public static LineField Build(SplineLine line, TerrainGrid grid, float reach)
    {
        RectInt rect = grid.Cells(line.FlatBounds(reach + 1f));
        var field = new LineField(rect);
        if (rect.width == 0 || rect.height == 0)
        {
            return field;
        }

        for (int s = 0; s + 1 < line.Count; s++)
        {
            Vector2 a = SplineLine.Flat(line.Points[s]);
            Vector2 b = SplineLine.Flat(line.Points[s + 1]);
            var segmentBounds = UnityEngine.Rect.MinMaxRect(Mathf.Min(a.x, b.x) - reach, Mathf.Min(a.y, b.y) - reach,
                                                Mathf.Max(a.x, b.x) + reach, Mathf.Max(a.y, b.y) + reach);
            RectInt cells = grid.Cells(segmentBounds);
            int xMax = Mathf.Min(cells.xMax, rect.xMax), yMax = Mathf.Min(cells.yMax, rect.yMax);
            for (int y = Mathf.Max(cells.y, rect.y); y < yMax; y++)
            {
                for (int x = Mathf.Max(cells.x, rect.x); x < xMax; x++)
                {
                    Vector2 p = grid.World(x, y);
                    float d = SplineLine.SegmentDistance(p, a, b, out float f);
                    int i = field.IndexOf(x, y);
                    if (d > reach || d >= field.Distance[i])
                    {
                        continue;
                    }

                    field.Distance[i] = d;
                    field.Segment[i] = s;
                    field.Fraction[i] = f;
                    Vector2 direction = b - a;
                    Vector2 offset = p - (a + direction * f);
                    float cross = direction.x * offset.y - direction.y * offset.x;
                    field.Side[i] = (sbyte)(cross >= 0f ? 1 : -1);
                }
            }
        }

        if (line.Closed)
        {
            for (int y = rect.y; y < rect.yMax; y++)
            {
                for (int x = rect.x; x < rect.xMax; x++)
                {
                    int i = field.IndexOf(x, y);
                    if (!float.IsPositiveInfinity(field.Distance[i]))
                    {
                        field.Side[i] = (sbyte)(line.Inside(grid.World(x, y)) ? 1 : -1);
                    }
                }
            }
        }

        return field;
    }
}

/// <summary>Cheap deterministic value noise for frayed edges and patches (0..1).</summary>
public static class WorldNoise2D
{
    public static float Value(float x, float y, int seed)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float fx = x - xi, fy = y - yi;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed), c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// <summary>Two octaves of value noise (0..1).</summary>
    public static float Fbm(float x, float y, int seed) => Value(x, y, seed) * 0.65f + Value(x * 2.1f + 13.7f, y * 2.1f - 7.3f, seed + 1) * 0.35f;

    public static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777215f;
        }
    }

    public static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
