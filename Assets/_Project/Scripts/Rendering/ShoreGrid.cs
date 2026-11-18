using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the water is on a painted map (the Haven), for the ground light field's wet shores (GroundLightMap.Build's shore lookup): the
/// scene's still water (ponds, tarns, a lake, a bog: a flat surface over its footprint) and rivers (their centrelines) laid on a grid
/// over the terrain where the ground lies under their surface, then each cell within <see cref="Reach"/> metres of water given the
/// nearest water's surface and how far it is (a two-pass distance sweep). A generated raid knows its water from its plan; this is the
/// same answer from what a scene holds. Plain arithmetic once built: safe for the field's worker thread.
/// </summary>
public sealed class ShoreGrid
{
    /// <summary>A still water's footprint (world x, z) and surface height.</summary>
    public readonly struct Still
    {
        public readonly Rect Area;
        public readonly float Level;

        public Still(Rect area, float level)
        {
            Area = area;
            Level = level;
        }
    }

    private readonly int size;
    private readonly float cell;
    private readonly Vector2 corner;
    private readonly float baseHeight;
    private readonly float[] level;
    private readonly float[] distance;

    /// <summary>Metres from the water within which a cell has a shore.</summary>
    public float Reach { get; }

    private ShoreGrid(int size, float cell, Vector2 corner, float baseHeight, float reach)
    {
        this.size = size;
        this.cell = cell;
        this.corner = corner;
        this.baseHeight = baseHeight;
        Reach = reach;
        level = new float[size * size];
        distance = new float[size * size];
    }

    /// <summary>
    /// Lays the water on a grid of <paramref name="size"/>² cells over a terrain (<paramref name="heights"/> its normalised heights, [z, x],
    /// <paramref name="terrainSize"/> its size, <paramref name="origin"/> its corner): a cell is water where the ground is under a still
    /// water's surface inside its footprint, or within a river's half width of its centreline and under its surface there.
    /// </summary>
    public static ShoreGrid Build(float[,] heights, Vector3 terrainSize, Vector3 origin, int size, IReadOnlyList<Still> still,
        IReadOnlyList<Vector4[]> rivers, float reach)
    {
        float side = Mathf.Max(terrainSize.x, terrainSize.z);
        var grid = new ShoreGrid(size, side / size, new Vector2(origin.x, origin.z), origin.y, reach);
        int resolution = heights.GetLength(0);
        for (int i = 0; i < grid.level.Length; i++)
        {
            grid.level[i] = float.NaN;
            grid.distance[i] = float.PositiveInfinity;
        }

        float Ground(float x, float z)
        {
            float u = Mathf.Clamp01((x - origin.x) / terrainSize.x) * (resolution - 1);
            float v = Mathf.Clamp01((z - origin.z) / terrainSize.z) * (resolution - 1);
            int x0 = Mathf.Min((int)u, resolution - 2), z0 = Mathf.Min((int)v, resolution - 2);
            float tx = u - x0, tz = v - z0;
            float bottom = Mathf.Lerp(heights[z0, x0], heights[z0, x0 + 1], tx);
            float top = Mathf.Lerp(heights[z0 + 1, x0], heights[z0 + 1, x0 + 1], tx);
            return origin.y + Mathf.Lerp(bottom, top, tz) * terrainSize.y;
        }

        void Wet(int cx, int cz, float surface)
        {
            if (cx < 0 || cz < 0 || cx >= size || cz >= size)
            {
                return;
            }

            float x = grid.corner.x + (cx + 0.5f) * grid.cell, z = grid.corner.y + (cz + 0.5f) * grid.cell;
            if (Ground(x, z) >= surface - 0.02f)
            {
                return;
            }

            int index = cz * size + cx;
            // Where two waters meet, the higher surface (a river into a pond).
            if (float.IsNaN(grid.level[index]) || surface > grid.level[index])
            {
                grid.level[index] = surface;
            }

            grid.distance[index] = 0f;
        }

        foreach (Still water in still)
        {
            int x0 = Mathf.FloorToInt((water.Area.xMin - grid.corner.x) / grid.cell), x1 = Mathf.CeilToInt((water.Area.xMax - grid.corner.x) / grid.cell);
            int z0 = Mathf.FloorToInt((water.Area.yMin - grid.corner.y) / grid.cell), z1 = Mathf.CeilToInt((water.Area.yMax - grid.corner.y) / grid.cell);
            for (int cz = z0; cz <= z1; cz++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    Wet(cx, cz, water.Level);
                }
            }
        }

        foreach (Vector4[] line in rivers)
        {
            for (int s = 0; s + 1 < line.Length; s++)
            {
                Vector4 a = line[s], b = line[s + 1];
                float widest = Mathf.Max(a.w, b.w);
                int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - widest - grid.corner.x) / grid.cell);
                int x1 = Mathf.CeilToInt((Mathf.Max(a.x, b.x) + widest - grid.corner.x) / grid.cell);
                int z0 = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - widest - grid.corner.y) / grid.cell);
                int z1 = Mathf.CeilToInt((Mathf.Max(a.z, b.z) + widest - grid.corner.y) / grid.cell);
                var ab = new Vector2(b.x - a.x, b.z - a.z);
                float length = ab.sqrMagnitude;
                for (int cz = z0; cz <= z1; cz++)
                {
                    for (int cx = x0; cx <= x1; cx++)
                    {
                        var p = new Vector2(grid.corner.x + (cx + 0.5f) * grid.cell - a.x, grid.corner.y + (cz + 0.5f) * grid.cell - a.z);
                        float t = length > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p, ab) / length) : 0f;
                        if ((p - ab * t).magnitude <= Mathf.Lerp(a.w, b.w, t))
                        {
                            Wet(cx, cz, Mathf.Lerp(a.y, b.y, t));
                        }
                    }
                }
            }
        }

        grid.Spread();
        return grid;
    }

    // Two sweeps (forward, then back) carry the nearest water's surface and its distance out from the water, an eight-way chamfer.
    private void Spread()
    {
        float straight = cell, diagonal = cell * 1.41421356f;
        for (int pass = 0; pass < 2; pass++)
        {
            bool forward = pass == 0;
            for (int k = 0; k < size; k++)
            {
                int z = forward ? k : size - 1 - k;
                for (int j = 0; j < size; j++)
                {
                    int x = forward ? j : size - 1 - j;
                    int index = z * size + x;
                    int step = forward ? -1 : 1;
                    Offer(index, x + step, z, straight);
                    Offer(index, x, z + step, straight);
                    Offer(index, x + step, z + step, diagonal);
                    Offer(index, x - step, z + step, diagonal);
                }
            }
        }

        for (int i = 0; i < distance.Length; i++)
        {
            if (distance[i] > Reach)
            {
                level[i] = float.NaN;
            }
        }
    }

    private void Offer(int index, int x, int z, float add)
    {
        if (x < 0 || z < 0 || x >= size || z >= size)
        {
            return;
        }

        int from = z * size + x;
        float through = distance[from] + add;
        if (through < distance[index] && through <= Reach + cell)
        {
            distance[index] = through;
            level[index] = level[from];
        }
    }

    /// <summary>
    /// The ground light field's shore lookup: at a point (metres from the terrain's corner), the nearest water's surface in metres above
    /// the terrain's base, and the metres to its edge (0 in it); x NaN where no water is within <see cref="Reach"/>.
    /// </summary>
    public Vector2 At(Vector2 metresFromCorner)
    {
        int x = Mathf.Clamp((int)(metresFromCorner.x / cell), 0, size - 1);
        int z = Mathf.Clamp((int)(metresFromCorner.y / cell), 0, size - 1);
        int index = z * size + x;
        float surface = level[index];
        return float.IsNaN(surface) ? new Vector2(float.NaN, float.NaN) : new Vector2(surface - baseHeight, Mathf.Max(0f, distance[index] - cell * 0.5f));
    }
}
