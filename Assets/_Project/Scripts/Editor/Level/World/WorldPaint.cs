using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Paints a whole terrain from <see cref="TerrainPaintRules"/> (the World Terrain Inspector's Auto-Paint): each
/// rule's layer goes where the ground's steepness, height and hollowness match, broken into patches. Undoable; the
/// features paint over the result in the build that follows.</summary>
public static class WorldPaint
{
    public const float HollowRadius = 4f;

    /// <summary>How much a point sits in a hollow (+1) or on a ridge or hump (-1), from the ground around it.</summary>
    public static float Hollowness(float centre, float around, float radius) =>
        Mathf.Clamp((around - centre) / Mathf.Max(radius, 0.01f) * 3f, -1f, 1f);

    /// <summary>How strongly a rule paints a cell (0-1).</summary>
    public static float RuleWeight(TerrainPaintRules.Rule rule, float slope, float height, float hollow, float noise)
    {
        float soft = Mathf.Max(rule.softness, 0.01f);
        float w = rule.strength;
        if (rule.slope.x > 0f)
        {
            w *= WorldNoise2D.Smooth((slope - rule.slope.x + soft) / soft);
        }

        if (rule.slope.y < 90f)
        {
            w *= 1f - WorldNoise2D.Smooth((slope - rule.slope.y) / soft);
        }

        if (rule.limitHeight)
        {
            w *= WorldNoise2D.Smooth((height - rule.height.x + soft) / soft) * (1f - WorldNoise2D.Smooth((height - rule.height.y) / soft));
        }

        if (Mathf.Abs(rule.hollows) > 0.001f)
        {
            float wanted = WorldNoise2D.Smooth(hollow * Mathf.Sign(rule.hollows) / 0.35f);
            w *= Mathf.Lerp(1f, wanted, Mathf.Abs(rule.hollows));
        }

        if (rule.patchiness > 0f)
        {
            w *= Mathf.Lerp(1f, WorldNoise2D.Smooth((noise - 0.45f) / 0.15f), rule.patchiness);
        }

        return Mathf.Clamp01(w);
    }

    public static void Apply(Terrain terrain, TerrainPaintRules rules)
    {
        TerrainData data = terrain.terrainData;
        var layers = new List<TerrainLayer>(data.terrainLayers);
        foreach (TerrainPaintRules.Rule rule in rules.Rules)
        {
            if (rule.layer != null && !layers.Contains(rule.layer))
            {
                layers.Add(rule.layer);
            }
        }

        var undo = new List<Object> { data };
        undo.AddRange(data.alphamapTextures);
        Undo.RegisterCompleteObjectUndo(undo.ToArray(), "Auto-Paint Terrain");
        if (layers.Count != data.terrainLayers.Length)
        {
            data.terrainLayers = layers.ToArray();
        }

        int resolution = data.alphamapResolution;
        int count = data.alphamapLayers;
        var index = new int[rules.Rules.Count];
        for (int r = 0; r < index.Length; r++)
        {
            index[r] = layers.IndexOf(rules.Rules[r].layer);
        }

        float[,,] maps = data.GetAlphamaps(0, 0, resolution, resolution);
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        float du = HollowRadius / size.x, dv = HollowRadius / size.z;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float u = (x + 0.5f) / resolution, v = (y + 0.5f) / resolution;
                float slope = data.GetSteepness(u, v);
                float h = data.GetInterpolatedHeight(u, v);
                float around = (data.GetInterpolatedHeight(Mathf.Clamp01(u - du), v) + data.GetInterpolatedHeight(Mathf.Clamp01(u + du), v) +
                                data.GetInterpolatedHeight(u, Mathf.Clamp01(v - dv)) + data.GetInterpolatedHeight(u, Mathf.Clamp01(v + dv))) * 0.25f;
                float hollow = Hollowness(h, around, HollowRadius);
                float worldX = origin.x + u * size.x, worldZ = origin.z + v * size.z;
                for (int r = 0; r < index.Length; r++)
                {
                    TerrainPaintRules.Rule rule = rules.Rules[r];
                    if (index[r] < 0)
                    {
                        continue;
                    }

                    float noise = WorldNoise2D.Fbm(worldX / rule.patchSize, worldZ / rule.patchSize, rules.Seed + r * 17);
                    float w = RuleWeight(rule, slope, origin.y + h, hollow, noise);
                    if (w <= 0f)
                    {
                        continue;
                    }

                    for (int l = 0; l < count; l++)
                    {
                        maps[y, x, l] = maps[y, x, l] * (1f - w) + (l == index[r] ? w : 0f);
                    }
                }
            }
        }

        data.SetAlphamaps(0, 0, maps);
        EditorUtility.SetDirty(data);
    }
}
