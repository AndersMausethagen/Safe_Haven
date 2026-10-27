using System;
using UnityEngine;

/// <summary>
/// Which ground has been seen: a top-down grid of bytes (0 unseen, 255 seen) over a world rectangle, filled in soft-
/// edged circles. Plain C# (ExplorationMap owns one; tested in ExplorationGridTests). Saved as one bit per cell.
/// </summary>
public sealed class ExplorationGrid
{
    public ExplorationGrid(Rect area, float cellSize)
    {
        Area = area;
        CellSize = Mathf.Max(0.1f, cellSize);
        Width = Mathf.Max(1, Mathf.CeilToInt(area.width / CellSize));
        Height = Mathf.Max(1, Mathf.CeilToInt(area.height / CellSize));
        Cells = new byte[Width * Height];
    }

    /// <summary>The world XZ rectangle covered (x, y = world x, z).</summary>
    public Rect Area { get; }
    public float CellSize { get; }
    public int Width { get; }
    public int Height { get; }
    /// <summary>Row 0 is the south edge (lowest z), like a texture's first row.</summary>
    public byte[] Cells { get; }

    /// <summary>How explored the cell under a world position is (0–1); outside the area, 0.</summary>
    public float At(Vector2 worldXZ)
    {
        if (!TryCell(worldXZ, out int x, out int y))
        {
            return 0f;
        }

        return Cells[y * Width + x] / 255f;
    }

    /// <summary>
    /// Explores a circle: fully inside radius × (1 − softness), fading to nothing at the radius. Never makes a cell less
    /// explored. Returns true when anything changed.
    /// </summary>
    public bool Reveal(Vector2 centerXZ, float radius, float softness)
    {
        if (radius <= 0f)
        {
            return false;
        }

        float inner = radius * (1f - Mathf.Clamp01(softness));
        int minX = Mathf.Max(0, Mathf.FloorToInt((centerXZ.x - radius - Area.xMin) / CellSize));
        int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt((centerXZ.x + radius - Area.xMin) / CellSize));
        int minY = Mathf.Max(0, Mathf.FloorToInt((centerXZ.y - radius - Area.yMin) / CellSize));
        int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt((centerXZ.y + radius - Area.yMin) / CellSize));
        bool changed = false;
        for (int y = minY; y <= maxY; y++)
        {
            float cz = Area.yMin + (y + 0.5f) * CellSize;
            for (int x = minX; x <= maxX; x++)
            {
                float cx = Area.xMin + (x + 0.5f) * CellSize;
                float distance = Vector2.Distance(new Vector2(cx, cz), centerXZ);
                if (distance >= radius)
                {
                    continue;
                }

                float amount = distance <= inner ? 1f : 1f - (distance - inner) / Mathf.Max(0.0001f, radius - inner);
                byte value = (byte)Mathf.RoundToInt(Mathf.Clamp01(amount) * 255f);
                int index = y * Width + x;
                if (value > Cells[index])
                {
                    Cells[index] = value;
                    changed = true;
                }
            }
        }

        return changed;
    }

    public void Clear() => Array.Clear(Cells, 0, Cells.Length);

    /// <summary>One bit per cell (set = mostly explored), as base64, for the save file.</summary>
    public string ToBits()
    {
        var bits = new byte[(Cells.Length + 7) / 8];
        for (int i = 0; i < Cells.Length; i++)
        {
            if (Cells[i] >= 128)
            {
                bits[i >> 3] |= (byte)(1 << (i & 7));
            }
        }

        return Convert.ToBase64String(bits);
    }

    /// <summary>Restores what <see cref="ToBits"/> wrote. A save from a grid of another size is ignored (returns false).</summary>
    public bool FromBits(string saved)
    {
        if (string.IsNullOrEmpty(saved))
        {
            return false;
        }

        byte[] bits;
        try
        {
            bits = Convert.FromBase64String(saved);
        }
        catch (FormatException)
        {
            return false;
        }

        if (bits.Length != (Cells.Length + 7) / 8)
        {
            return false;
        }

        for (int i = 0; i < Cells.Length; i++)
        {
            Cells[i] = (bits[i >> 3] & (1 << (i & 7))) != 0 ? (byte)255 : (byte)0;
        }

        return true;
    }

    private bool TryCell(Vector2 worldXZ, out int x, out int y)
    {
        x = Mathf.FloorToInt((worldXZ.x - Area.xMin) / CellSize);
        y = Mathf.FloorToInt((worldXZ.y - Area.yMin) / CellSize);
        return x >= 0 && y >= 0 && x < Width && y < Height;
    }
}
