using System;
using System.Collections;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// Paints a scene's map once, from what the scene is made of: the colour of its terrain layers, shaded by the slope, water,
/// and everything nobody can walk on (trees, rocks, cliffs) darker, read off the NavMesh. Paint rather than a camera because
/// the map must look the same at noon and midnight, with no fog or night veil, and because nothing renders after that. Spread
/// over a few frames, so arriving in a scene doesn't stall.
/// </summary>
public static class MinimapMap
{
    private const int SampleSize = 32;
    private const float SlopeShade = 0.9f;
    private const float WaterAbove = 3f;
    private const float WaterReach = 6f;
    private const float WaterTint = 0.75f;
    private const long SliceMilliseconds = 4;

    /// <summary>The world rectangle (x, z) all the terrain covers; false in a scene with none.</summary>
    public static bool TryGetArea(out Rect area)
    {
        area = default;
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains.Length == 0)
        {
            return false;
        }

        float xMin = float.MaxValue, zMin = float.MaxValue, xMax = float.MinValue, zMax = float.MinValue;
        foreach (Terrain terrain in terrains)
        {
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            xMin = Mathf.Min(xMin, origin.x);
            zMin = Mathf.Min(zMin, origin.z);
            xMax = Mathf.Max(xMax, origin.x + size.x);
            zMax = Mathf.Max(zMax, origin.z + size.z);
        }

        area = Rect.MinMaxRect(xMin, zMin, xMax, zMax);
        return true;
    }

    /// <summary>Paints the map a slice at a time; run it as a coroutine. Hands the finished map to <paramref name="done"/>.</summary>
    public static IEnumerator Paint(Rect area, MinimapSettings settings, Action<Texture2D> done)
    {
        int size = settings.MapSize;
        var pixels = new Color32[size * size];
        bool[] walkable = WalkableCells(area, size);
        bool[] wet = WaterCells(area, size);
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            IEnumerator rows = PaintTerrain(terrain, area, size, pixels, walkable, wet, settings);
            while (rows.MoveNext())
            {
                yield return null;
            }
        }

        var map = new Texture2D(size, size, TextureFormat.RGBA32, false, false)
        {
            name = "Minimap",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        map.SetPixels32(pixels);
        map.Apply(false, true);
        done(map);
    }

    /// <summary>
    /// Which cells of a grid over the area a triangle list covers, row 0 at the south edge. A cell is covered when its centre is
    /// inside a triangle, or a corner of one lies in it, so small triangles aren't lost between centres.
    /// </summary>
    public static bool[] Fill(Rect area, int size, Vector3[] vertices, int[] indices)
    {
        var cells = new bool[size * size];
        FillInto(cells, area, size, vertices, indices);
        return cells;
    }

    private static void FillInto(bool[] cells, Rect area, int size, Vector3[] vertices, int[] indices)
    {
        float perMetreX = size / area.width;
        float perMetreY = size / area.height;
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            Vector2 a = Cell(vertices[indices[i]], area, perMetreX, perMetreY);
            Vector2 b = Cell(vertices[indices[i + 1]], area, perMetreX, perMetreY);
            Vector2 c = Cell(vertices[indices[i + 2]], area, perMetreX, perMetreY);
            FillTriangle(cells, size, a, b, c);
        }
    }

    // Where water might be, from its meshes. WaterSurface.TryFind is exact but walks a river's whole centre line, so asking it
    // of every pixel takes seconds; asked only of these it takes a moment.
    private static bool[] WaterCells(Rect area, int size)
    {
        var cells = new bool[size * size];
        foreach (WaterSurface surface in WaterSurface.Surfaces)
        {
            Mesh mesh = surface.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
            if (mesh == null || !mesh.isReadable)
            {
                continue;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = surface.transform.TransformPoint(vertices[i]);
            }

            FillInto(cells, area, size, vertices, mesh.triangles);
        }

        return cells;
    }

    // Null when the scene has no NavMesh: then nothing is painted as blocked.
    private static bool[] WalkableCells(Rect area, int size)
    {
        NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
        return mesh.indices.Length == 0 ? null : Fill(area, size, mesh.vertices, mesh.indices);
    }

    private static Vector2 Cell(Vector3 point, Rect area, float perMetreX, float perMetreY) =>
        new Vector2((point.x - area.xMin) * perMetreX, (point.z - area.yMin) * perMetreY);

    private static void FillTriangle(bool[] cells, int size, Vector2 a, Vector2 b, Vector2 c)
    {
        Mark(cells, size, a);
        Mark(cells, size, b);
        Mark(cells, size, c);
        float area = Edge(a, b, c);
        if (Mathf.Abs(area) < 0.0001f)
        {
            return;
        }

        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
        int maxX = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
        int maxY = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                var centre = new Vector2(x + 0.5f, y + 0.5f);
                float ab = Edge(a, b, centre) / area;
                float bc = Edge(b, c, centre) / area;
                float ca = Edge(c, a, centre) / area;
                if (ab >= 0f && bc >= 0f && ca >= 0f)
                {
                    cells[y * size + x] = true;
                }
            }
        }
    }

    private static float Edge(Vector2 from, Vector2 to, Vector2 point) => (to.x - from.x) * (point.y - from.y) - (to.y - from.y) * (point.x - from.x);

    private static void Mark(bool[] cells, int size, Vector2 point)
    {
        int x = Mathf.FloorToInt(point.x);
        int y = Mathf.FloorToInt(point.y);
        if (x >= 0 && y >= 0 && x < size && y < size)
        {
            cells[y * size + x] = true;
        }
    }

    private static IEnumerator PaintTerrain(Terrain terrain, Rect area, int size, Color32[] pixels, bool[] walkable, bool[] wet, MinimapSettings settings)
    {
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 extent = data.size;
        Color[] layers = LayerColours(data);
        int alphaWidth = data.alphamapWidth;
        int alphaHeight = data.alphamapHeight;
        int heightSize = data.heightmapResolution;
        float[,,] alphas = data.GetAlphamaps(0, 0, alphaWidth, alphaHeight);
        float[,] heights = data.GetHeights(0, 0, heightSize, heightSize);
        float slopeScale = extent.y / (2f * extent.x / (heightSize - 1));
        Stopwatch slice = Stopwatch.StartNew();
        for (int y = 0; y < size; y++)
        {
            if (slice.ElapsedMilliseconds >= SliceMilliseconds)
            {
                yield return null;
                slice.Restart();
            }

            float worldZ = area.yMin + (y + 0.5f) * area.height / size;
            float v = (worldZ - origin.z) / extent.z;
            if (v < 0f || v > 1f)
            {
                continue;
            }

            int alphaY = Mathf.Min(alphaHeight - 1, (int)(v * alphaHeight));
            int heightY = Mathf.Clamp((int)(v * (heightSize - 1)), 1, heightSize - 2);
            for (int x = 0; x < size; x++)
            {
                float worldX = area.xMin + (x + 0.5f) * area.width / size;
                float u = (worldX - origin.x) / extent.x;
                if (u < 0f || u > 1f)
                {
                    continue;
                }

                int alphaX = Mathf.Min(alphaWidth - 1, (int)(u * alphaWidth));
                int heightX = Mathf.Clamp((int)(u * (heightSize - 1)), 1, heightSize - 2);
                float red = 0f, green = 0f, blue = 0f;
                for (int layer = 0; layer < layers.Length; layer++)
                {
                    float weight = alphas[alphaY, alphaX, layer];
                    if (weight > 0f)
                    {
                        Color tint = layers[layer];
                        red += tint.r * weight;
                        green += tint.g * weight;
                        blue += tint.b * weight;
                    }
                }

                // Light from the north-west, as on old maps.
                float eastward = (heights[heightY, heightX + 1] - heights[heightY, heightX - 1]) * slopeScale;
                float northward = (heights[heightY + 1, heightX] - heights[heightY - 1, heightX]) * slopeScale;
                float light = Mathf.Clamp(1f + (northward - eastward) * SlopeShade, 0.6f, 1.35f);
                var colour = new Color(red * light, green * light, blue * light, 1f);
                float ground = origin.y + heights[heightY, heightX] * extent.y;
                var point = new Vector3(worldX, ground + WaterAbove, worldZ);
                if (wet[y * size + x] && WaterSurface.TryFind(point, WaterReach, out float surface) && surface > ground)
                {
                    colour = Color.Lerp(colour, settings.Water, WaterTint);
                }
                else if (walkable != null && !walkable[y * size + x])
                {
                    colour *= settings.BlockedShade;
                }

                colour.a = 1f;
                pixels[y * size + x] = colour;
            }
        }
    }

    // Terrain textures aren't readable from the CPU, so each is drawn small and read back.
    private static Color[] LayerColours(TerrainData data)
    {
        TerrainLayer[] layers = data.terrainLayers;
        var colours = new Color[layers.Length];
        RenderTexture target = RenderTexture.GetTemporary(SampleSize, SampleSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var reader = new Texture2D(SampleSize, SampleSize, TextureFormat.RGBA32, false, false);
        RenderTexture previous = RenderTexture.active;
        for (int i = 0; i < layers.Length; i++)
        {
            colours[i] = layers[i] != null && layers[i].diffuseTexture != null ? Average(layers[i].diffuseTexture, target, reader) : Color.gray;
        }

        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Object.Destroy(reader);
        return colours;
    }

    private static Color Average(Texture texture, RenderTexture target, Texture2D reader)
    {
        Graphics.Blit(texture, target);
        RenderTexture.active = target;
        reader.ReadPixels(new Rect(0, 0, SampleSize, SampleSize), 0, 0, false);
        Color32[] texels = reader.GetPixels32();
        long red = 0, green = 0, blue = 0;
        foreach (Color32 texel in texels)
        {
            red += texel.r;
            green += texel.g;
            blue += texel.b;
        }

        float scale = 1f / (texels.Length * 255f);
        return new Color(red * scale, green * scale, blue * scale, 1f);
    }
}
