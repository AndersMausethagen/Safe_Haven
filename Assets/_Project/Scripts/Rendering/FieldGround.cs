using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ground a light field is worked out over (GroundLightMap): one terrain's heights as they are, or several terrains laid on one
/// grid over the square that holds them all (the prologue's meadow and forest). Heights are 0-1 over <see cref="Size"/>.y from
/// <see cref="Origin"/>.y, [z, x], with a sample on each corner as a terrain's own, so GroundLightMap and ShoreGrid read either the
/// same way.
/// </summary>
public sealed class FieldGround
{
    public float[,] Heights;
    public int Resolution;
    /// <summary>x and z the ground's extent in metres (a square for several terrains), y its height span.</summary>
    public Vector3 Size;
    /// <summary>The corner (x, z) and the height that 0 stands for.</summary>
    public Vector3 Origin;
    /// <summary>The lowest and highest ground in world metres.</summary>
    public float Lowest, Highest;

    /// <summary>One terrain's own heights (main thread).</summary>
    public static FieldGround Of(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        int resolution = data.heightmapResolution;
        Vector3 position = terrain.GetPosition();
        Bounds bounds = data.bounds;
        return new FieldGround
        {
            Heights = data.GetHeights(0, 0, resolution, resolution),
            Resolution = resolution,
            Size = data.size,
            Origin = position,
            Lowest = position.y + bounds.min.y,
            Highest = position.y + bounds.max.y,
        };
    }

    /// <summary>What <see cref="Across"/> needs from each terrain, read on the main thread.</summary>
    public struct Piece
    {
        public float[,] Heights;
        public int Resolution;
        public Vector3 Position;
        public Vector3 Size;

        public static Piece Of(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            return new Piece { Heights = data.GetHeights(0, 0, resolution, resolution), Resolution = resolution, Position = terrain.GetPosition(), Size = data.size };
        }

        public bool Contains(float x, float z) => x >= Position.x && x <= Position.x + Size.x && z >= Position.z && z <= Position.z + Size.z;

        /// <summary>The ground's world height at a point, between the samples as the terrain's surface lies; clamped to its edge outside.</summary>
        public float HeightAt(float x, float z)
        {
            float u = Mathf.Clamp01((x - Position.x) / Size.x) * (Resolution - 1);
            float v = Mathf.Clamp01((z - Position.z) / Size.z) * (Resolution - 1);
            int u0 = (int)u, v0 = (int)v;
            int u1 = Math.Min(u0 + 1, Resolution - 1), v1 = Math.Min(v0 + 1, Resolution - 1);
            float fu = u - u0, fv = v - v0;
            float south = Heights[v0, u0] + (Heights[v0, u1] - Heights[v0, u0]) * fu;
            float north = Heights[v1, u0] + (Heights[v1, u1] - Heights[v1, u0]) * fu;
            return Position.y + (south + (north - south) * fv) * Size.y;
        }

        /// <summary>How far a point lies outside this terrain (0 inside).</summary>
        public float Outside(float x, float z)
        {
            float dx = Mathf.Max(Position.x - x, 0f, x - (Position.x + Size.x));
            float dz = Mathf.Max(Position.z - z, 0f, z - (Position.z + Size.z));
            return dx * dx + dz * dz;
        }
    }

    /// <summary>
    /// Several terrains on one grid over the square that holds them (plain arithmetic, for a worker thread). A point on no terrain
    /// takes the nearest one's edge, so a gap between them reads as the ground beside it rather than a pit.
    /// </summary>
    public static FieldGround Across(IReadOnlyList<Piece> pieces, int resolution)
    {
        float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue;
        foreach (Piece piece in pieces)
        {
            minX = Mathf.Min(minX, piece.Position.x);
            minZ = Mathf.Min(minZ, piece.Position.z);
            maxX = Mathf.Max(maxX, piece.Position.x + piece.Size.x);
            maxZ = Mathf.Max(maxZ, piece.Position.z + piece.Size.z);
        }

        float side = Mathf.Max(maxX - minX, maxZ - minZ);
        var world = new float[resolution, resolution];
        float lowest = float.MaxValue, highest = float.MinValue;
        for (int z = 0; z < resolution; z++)
        {
            float atZ = minZ + side * z / (resolution - 1);
            for (int x = 0; x < resolution; x++)
            {
                float atX = minX + side * x / (resolution - 1);
                int best = 0;
                float nearest = float.MaxValue;
                for (int p = 0; p < pieces.Count; p++)
                {
                    float outside = pieces[p].Outside(atX, atZ);
                    if (outside < nearest)
                    {
                        nearest = outside;
                        best = p;
                        if (outside <= 0f)
                        {
                            break;
                        }
                    }
                }

                float height = pieces[best].HeightAt(atX, atZ);
                world[z, x] = height;
                lowest = Mathf.Min(lowest, height);
                highest = Mathf.Max(highest, height);
            }
        }

        float span = Mathf.Max(highest - lowest, 0.01f);
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                world[z, x] = (world[z, x] - lowest) / span;
            }
        }

        return new FieldGround
        {
            Heights = world,
            Resolution = resolution,
            Size = new Vector3(side, span, side),
            Origin = new Vector3(minX, lowest, minZ),
            Lowest = lowest,
            Highest = highest,
        };
    }
}
