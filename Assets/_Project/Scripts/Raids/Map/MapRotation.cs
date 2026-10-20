using UnityEngine;

/// <summary>
/// Turns a square raid map by quarter turns, so one hand-made layout can be entered from four directions. A Unity Terrain
/// can't be rotated, so its heights, paint and trees are turned in their arrays instead, and everything standing on the
/// map is turned around the map's centre to match. Every function here turns the same way as
/// Quaternion.Euler(0, 90 * quarterTurns, 0) (clockwise seen from above), so arrays and objects always agree.
/// </summary>
public static class MapRotation
{
    /// <summary>Degrees around the up axis for this many quarter turns.</summary>
    public static float Yaw(int quarterTurns) => Normalize(quarterTurns) * 90f;

    /// <summary>0 to 3 quarter turns from the raid's seed; the same seed always faces the map the same way.</summary>
    public static int Pick(int raidSeed)
    {
        return new System.Random(RaidLayout.SeedFor(raidSeed, "Map Rotation")).Next(4);
    }

    public static int Normalize(int quarterTurns) => ((quarterTurns % 4) + 4) % 4;

    /// <summary>Turns an offset from the map's centre (x, z on the ground).</summary>
    public static Vector2 RotateOffset(Vector2 offset, int quarterTurns)
    {
        switch (Normalize(quarterTurns))
        {
            case 1: return new Vector2(offset.y, -offset.x);
            case 2: return new Vector2(-offset.x, -offset.y);
            case 3: return new Vector2(-offset.y, offset.x);
            default: return offset;
        }
    }

    /// <summary>Turns a position given as 0–1 across the map (like a TreeInstance's x and z).</summary>
    public static Vector2 RotateNormalized(Vector2 uv, int quarterTurns)
    {
        return RotateOffset(uv - new Vector2(0.5f, 0.5f), quarterTurns) + new Vector2(0.5f, 0.5f);
    }

    /// <summary>
    /// Where a cell of a square grid indexed [row (z), column (x)] lands after turning, the way TerrainData stores heights
    /// and paint. The grid turns around its centre.
    /// </summary>
    public static void RotateIndex(int row, int column, int size, int quarterTurns, out int newRow, out int newColumn)
    {
        int last = size - 1;
        switch (Normalize(quarterTurns))
        {
            case 1: newRow = last - column; newColumn = row; break;
            case 2: newRow = last - row; newColumn = last - column; break;
            case 3: newRow = column; newColumn = last - row; break;
            default: newRow = row; newColumn = column; break;
        }
    }

    /// <summary>A turned copy of a square grid such as TerrainData.GetHeights.</summary>
    public static float[,] RotateGrid(float[,] source, int quarterTurns)
    {
        int size = RequireSquare(source.GetLength(0), source.GetLength(1));
        var result = new float[size, size];
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                RotateIndex(row, column, size, quarterTurns, out int newRow, out int newColumn);
                result[newRow, newColumn] = source[row, column];
            }
        }

        return result;
    }

    /// <summary>A turned copy of square paint weights [row, column, layer] such as TerrainData.GetAlphamaps.</summary>
    public static float[,,] RotateLayers(float[,,] source, int quarterTurns)
    {
        int size = RequireSquare(source.GetLength(0), source.GetLength(1));
        int layers = source.GetLength(2);
        var result = new float[size, size, layers];
        for (int row = 0; row < size; row++)
        {
            for (int column = 0; column < size; column++)
            {
                RotateIndex(row, column, size, quarterTurns, out int newRow, out int newColumn);
                for (int layer = 0; layer < layers; layer++)
                {
                    result[newRow, newColumn, layer] = source[row, column, layer];
                }
            }
        }

        return result;
    }

    /// <summary>Turns terrain trees in place: their spot on the map and the way each one faces.</summary>
    public static void RotateTrees(TreeInstance[] trees, int quarterTurns)
    {
        float turn = Yaw(quarterTurns) * Mathf.Deg2Rad;
        for (int i = 0; i < trees.Length; i++)
        {
            Vector3 position = trees[i].position;
            Vector2 turned = RotateNormalized(new Vector2(position.x, position.z), quarterTurns);
            trees[i].position = new Vector3(turned.x, position.y, turned.y);
            trees[i].rotation = Mathf.Repeat(trees[i].rotation + turn, Mathf.PI * 2f);
        }
    }

    private static int RequireSquare(int rows, int columns)
    {
        if (rows != columns)
        {
            throw new System.ArgumentException($"Only square maps can be turned; this one is {columns} by {rows}.");
        }

        return rows;
    }
}
