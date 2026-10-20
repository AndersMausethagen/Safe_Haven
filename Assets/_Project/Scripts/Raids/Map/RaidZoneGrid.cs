using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The arithmetic of painted raid-map zones, kept free of the editor so it can be tested: reading a strength between
/// cells, a soft round brush, resizing a grid and scattering candidate points over a zone. Grids are square byte arrays,
/// row 0 = south, cell i at i / resolution (the same as Texture2D.GetPixelBilinear with Clamp, so maps converted from
/// the old mask image bake exactly as before).
/// </summary>
public static class RaidZoneGrid
{
    /// <summary>Strength 0–1 at (u, v), bilinear between cells and clamped at the edges.</summary>
    public static float Sample(byte[] grid, int resolution, float u, float v)
    {
        if (grid == null || resolution <= 0 || grid.Length < resolution * resolution)
        {
            return 0f;
        }

        float x = Mathf.Clamp(u * resolution, 0f, resolution - 1f);
        float y = Mathf.Clamp(v * resolution, 0f, resolution - 1f);
        int x0 = (int)x;
        int y0 = (int)y;
        int x1 = Mathf.Min(x0 + 1, resolution - 1);
        int y1 = Mathf.Min(y0 + 1, resolution - 1);
        float tx = x - x0;
        float ty = y - y0;
        float bottom = Mathf.Lerp(grid[y0 * resolution + x0], grid[y0 * resolution + x1], tx);
        float top = Mathf.Lerp(grid[y1 * resolution + x0], grid[y1 * resolution + x1], tx);
        return Mathf.Lerp(bottom, top, ty) / 255f;
    }

    /// <summary>
    /// One dab of a round brush: every cell inside the radius moves toward target (0–1) by strength, fading to nothing at
    /// the rim. Hardness 1 is a hard edge, 0 fades from the centre. Returns the cells touched (empty when none).
    /// </summary>
    public static RectInt Paint(byte[] grid, int resolution, Vector2 centre, float radius, float strength, float target, float hardness)
    {
        if (grid == null || radius <= 0f || strength <= 0f)
        {
            return new RectInt();
        }

        float cx = centre.x * resolution;
        float cy = centre.y * resolution;
        float r = radius * resolution;
        int xMin = Mathf.Max(0, Mathf.FloorToInt(cx - r));
        int yMin = Mathf.Max(0, Mathf.FloorToInt(cy - r));
        int xMax = Mathf.Min(resolution - 1, Mathf.CeilToInt(cx + r));
        int yMax = Mathf.Min(resolution - 1, Mathf.CeilToInt(cy + r));
        if (xMin > xMax || yMin > yMax)
        {
            return new RectInt();
        }

        int goal = Mathf.RoundToInt(Mathf.Clamp01(target) * 255f);
        float solid = Mathf.Clamp01(hardness);
        for (int y = yMin; y <= yMax; y++)
        {
            for (int x = xMin; x <= xMax; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / r;
                if (distance >= 1f)
                {
                    continue;
                }

                float falloff = distance <= solid ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (distance - solid) / Mathf.Max(0.0001f, 1f - solid));
                float amount = Mathf.Clamp01(strength * falloff);
                int index = y * resolution + x;
                int current = grid[index];
                if (current == goal || amount <= 0f)
                {
                    continue;
                }

                int next = Mathf.RoundToInt(Mathf.Lerp(current, goal, amount));
                if (next == current)
                {
                    // Small dabs still creep toward the goal instead of stalling on rounding.
                    next += goal > current ? 1 : -1;
                }

                grid[index] = (byte)next;
            }
        }

        return new RectInt(xMin, yMin, xMax - xMin + 1, yMax - yMin + 1);
    }

    /// <summary>A copy of a grid at another resolution, read bilinearly.</summary>
    public static byte[] Resample(byte[] grid, int from, int to)
    {
        var result = new byte[to * to];
        for (int y = 0; y < to; y++)
        {
            for (int x = 0; x < to; x++)
            {
                result[y * to + x] = (byte)Mathf.RoundToInt(Sample(grid, from, x / (float)to, y / (float)to) * 255f);
            }
        }

        return result;
    }

    /// <summary>
    /// Candidate points over a painted zone (0–1 map positions): one try per cell of a jittered grid spacing apart (in
    /// metres on a map of mapSize), kept with a chance equal to the strength there and when accept agrees. When more than
    /// maxPoints are kept, an even random share of them stays. The same inputs always give the same points.
    /// </summary>
    public static List<Vector2> Scatter(Func<Vector2, float> strengthAt, float mapSize, float spacing, int maxPoints, int seed,
        Func<Vector2, bool> accept = null)
    {
        var points = new List<Vector2>();
        if (maxPoints <= 0 || spacing <= 0f || mapSize <= 0f)
        {
            return points;
        }

        var random = new System.Random(seed);
        int cells = Mathf.CeilToInt(mapSize / spacing);
        for (int gy = 0; gy < cells; gy++)
        {
            for (int gx = 0; gx < cells; gx++)
            {
                var spot = new Vector2((gx + (float)random.NextDouble()) * spacing / mapSize, (gy + (float)random.NextDouble()) * spacing / mapSize);
                double roll = random.NextDouble();
                if (spot.x >= 1f || spot.y >= 1f)
                {
                    continue;
                }

                float strength = strengthAt(spot);
                if (strength <= 0f || roll >= strength || (accept != null && !accept(spot)))
                {
                    continue;
                }

                points.Add(spot);
            }
        }

        if (points.Count > maxPoints)
        {
            for (int i = points.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (points[i], points[j]) = (points[j], points[i]);
            }

            points.RemoveRange(maxPoints, points.Count - maxPoints);
            points.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        }

        return points;
    }

    /// <summary>A stable seed from a name, for scattering each zone differently but the same on every bake.</summary>
    public static int SeedFor(int seed, string name)
    {
        unchecked
        {
            int hash = seed * 31 + 17;
            foreach (char c in name ?? string.Empty)
            {
                hash = hash * 31 + c;
            }

            return hash;
        }
    }
}
