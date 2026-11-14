using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Original, periodic PBR source art for the preview. No downloaded images or baked lighting.</summary>
public static class ForestGroundTextures
{
    public const string Root = "Assets/_Project/Art/Moss/Ground";
    public const int Resolution = 1024;

    [MenuItem("Safe Haven/Setup/Art Pipeline/Regenerate Forest Ground Textures", false, 200)]
    private static void Regenerate()
    {
        if (RetiredBuilders.Refuse("Regenerate Forest Ground Textures")) return;
        if (EditorUtility.DisplayDialog("Regenerate ground textures",
            "Overwrite the generated Moss, Soil and Stone texture maps? Materials and scene settings are kept.", "Regenerate", "Cancel"))
            Generate();
    }

    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/_Project/Art/Moss", "Ground");
        foreach (string kind in new[] { "Moss", "Soil", "Stone" }) GenerateSet(kind);
        AssetDatabase.SaveAssets();
    }

    private static void GenerateSet(string kind)
    {
        int size = Resolution;
        var albedo = new Color[size * size];
        var heights = new float[size * size];
        var roughness = new float[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                Sample(kind, new Vector2((float)x / size, (float)y / size), out albedo[i], out heights[i], out roughness[i]);
            }
        }

        var normal = new Color[albedo.Length];
        var packed = new Color[albedo.Length];
        var terrainMask = new Color[albedo.Length];
        float relief = kind == "Moss" ? 0.012f : kind == "Soil" ? 0.008f : 0.018f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float dx = heights[y * size + (x + 1) % size] - heights[y * size + (x + size - 1) % size];
                float dy = heights[((y + 1) % size) * size + x] - heights[((y + size - 1) % size) * size + x];
                Vector3 n = new Vector3(-dx * relief * size * 0.5f, -dy * relief * size * 0.5f, 1f).normalized;
                normal[i] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
                float ao = Mathf.Lerp(0.62f, 1f, Mathf.SmoothStep(0f, 1f, heights[i]));
                packed[i] = new Color(ao, roughness[i], heights[i], 1f);
                terrainMask[i] = new Color(0f, ao, heights[i], 1f - roughness[i]);
            }
        }

        Write(kind + "_BaseColor", albedo, false, true);
        Write(kind + "_Normal", normal, true, false);
        Write(kind + "_Packed", packed, false, false);
        Write(kind + "_TerrainMask", terrainMask, false, false);
    }

    public static void Sample(string kind, Vector2 uv, out Color color, out float height, out float roughness)
    {
        float broad = Noise(uv, 8, 31);
        float medium = Noise(uv, 32, 71);
        float grain = Noise(uv, 256, 13);
        if (kind == "Moss")
        {
            Cell(uv, 20, 17, out float radius, out float edge, out float id, out Vector2 delta);
            float cushion = 1f - Smooth(0.05f, 0.8f, radius);
            Cell(uv, 140, 43, out float leafRadius, out _, out float leafId, out Vector2 leaf);
            float angle = Mathf.Atan2(leaf.y, leaf.x);
            float needles = Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 3f + leafId * 6f)), 10f);
            float frond = (1f - Smooth(0.1f, 0.65f, leafRadius)) * (0.25f + 0.75f * needles);
            height = Mathf.Clamp01(0.16f + cushion * 0.34f + frond * 0.35f + grain * 0.1f);
            Color dark = new Color(0.105f, 0.145f, 0.055f);
            Color green = new Color(0.31f, 0.37f, 0.13f);
            color = Color.Lerp(dark, green, cushion * 0.5f + medium * 0.25f + frond * 0.25f);
            color *= Mathf.Lerp(0.8f, 1.18f, broad);
            color = Color.Lerp(color, new Color(0.42f, 0.39f, 0.17f), Mathf.Max(0f, id - 0.7f) * 0.7f * cushion);
            roughness = Mathf.Lerp(0.78f, 0.95f, grain);
        }
        else if (kind == "Soil")
        {
            Cell(uv, 48, 19, out float radius, out _, out float id, out Vector2 delta);
            float pebble = id > 0.67f ? 1f - Smooth(0.1f, 0.3f, radius) : 0f;
            Cell(uv, 17, 61, out _, out _, out float leafId, out Vector2 leaf);
            float angle = leafId * Mathf.PI * 2f;
            Vector2 rotated = new Vector2(leaf.x * Mathf.Cos(angle) - leaf.y * Mathf.Sin(angle),
                leaf.x * Mathf.Sin(angle) + leaf.y * Mathf.Cos(angle));
            float ellipse = new Vector2(rotated.x * 6f, rotated.y * 2.5f).magnitude;
            float litter = leafId > 0.4f ? 1f - Smooth(0.82f, 1f, ellipse) : 0f;
            float vein = Mathf.Exp(-Mathf.Abs(rotated.x) * 160f) * litter;
            height = Mathf.Clamp01(0.15f + medium * 0.25f + grain * 0.2f + pebble * 0.3f + litter * 0.12f + vein * 0.06f);
            color = Color.Lerp(new Color(0.105f, 0.075f, 0.049f), new Color(0.25f, 0.19f, 0.12f), medium * 0.6f + grain * 0.4f);
            color = Color.Lerp(color, new Color(0.34f, 0.32f, 0.27f) * Mathf.Lerp(0.7f, 1.2f, id), pebble);
            color = Color.Lerp(color, Color.Lerp(new Color(0.21f, 0.13f, 0.055f), new Color(0.38f, 0.26f, 0.12f), leafId), litter * 0.85f);
            color *= Mathf.Lerp(0.8f, 1.2f, broad);
            roughness = Mathf.Lerp(0.73f, 0.96f, grain) - litter * 0.08f;
        }
        else
        {
            Cell(uv, 7, 89, out _, out float edge, out float id, out _);
            float crack = 1f - Smooth(0.01f, 0.055f, edge);
            float ridge = 1f - Mathf.Abs(Noise(uv, 64, 41) * 2f - 1f);
            height = Mathf.Clamp01(0.25f + broad * 0.25f + medium * 0.22f + ridge * 0.12f - crack * 0.22f);
            color = Color.Lerp(new Color(0.17f, 0.19f, 0.19f), new Color(0.43f, 0.44f, 0.41f), broad * 0.5f + medium * 0.3f + grain * 0.2f);
            color *= 1f - crack * 0.3f;
            color = Color.Lerp(color, new Color(0.37f, 0.32f, 0.25f), Mathf.Max(0f, id - 0.6f) * 0.4f);
            roughness = Mathf.Lerp(0.68f, 0.92f, grain);
        }
        color.a = 1f;
    }

    private static float Hash(int x, int y, int period, int seed)
    {
        x = (x % period + period) % period;
        y = (y % period + period) % period;
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1274126177);
            h = (h ^ (h >> 13)) * 1274126177u;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
    }

    private static float Smooth(float min, float max, float value)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(min, max, value));
    }

    private static float Noise(Vector2 uv, int frequency, int seed)
    {
        float x = uv.x * frequency, y = uv.y * frequency;
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        return Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, frequency, seed), Hash(ix + 1, iy, frequency, seed), fx),
            Mathf.Lerp(Hash(ix, iy + 1, frequency, seed), Hash(ix + 1, iy + 1, frequency, seed), fx), fy);
    }

    private static void Cell(Vector2 uv, int frequency, int seed, out float nearest, out float edge, out float id, out Vector2 delta)
    {
        Vector2 p = uv * frequency;
        int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
        float first = 10f, second = 10f;
        id = 0f;
        delta = Vector2.zero;
        for (int j = -1; j <= 1; j++)
            for (int i = -1; i <= 1; i++)
            {
                Vector2 d = p - new Vector2(x + i + Hash(x + i, y + j, frequency, seed),
                    y + j + Hash(x + i, y + j, frequency, seed + 1));
                float distance = d.sqrMagnitude;
                if (distance < first)
                {
                    second = first; first = distance;
                    delta = d;
                    id = Hash(x + i, y + j, frequency, seed + 2);
                }
                else second = Mathf.Min(second, distance);
            }
        nearest = Mathf.Sqrt(first);
        edge = Mathf.Sqrt(second) - nearest;
    }

    private static void Write(string name, Color[] pixels, bool normal, bool srgb)
    {
        string path = Root + "/" + name + ".png";
        var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, !srgb);
        try
        {
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.anisoLevel = 8;
        importer.maxTextureSize = Resolution;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
