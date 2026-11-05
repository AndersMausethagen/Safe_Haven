using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ground relief in real units (Safe Haven › World › Terrain › Calibrate Ground Relief). Height maps come normalised to
/// 0-1 over whatever range their scan had, so the same amplitude gives one ground centimetres and another a spiky
/// decimetre. For each ground layer of every terrain profile this sets the midpoint to the height map's own average
/// (the ground stays level on average) and the amplitude so the height between its 1st and 99th percentile spans
/// <see cref="Relief"/>: 2 cm for moss, grass and litter, 3 cm for soil, paths and banks, 6 cm for stones and rock. The
/// steep-ground rock (both kinds) is set to span 8 cm. Then every terrain's displacement data is rebuilt.
/// </summary>
public static class GroundRelief
{
    public const float SoftRelief = 0.02f, SoilRelief = 0.03f, RockRelief = 0.06f, CliffRelief = 0.08f;

    /// <summary>The displacement strength the amplitudes are calibrated against: at this strength (Relief Scale ×1) each
    /// ground spans exactly its <see cref="Relief"/>. The material's strength is then the one live dial for all grounds.</summary>
    public const float ReferenceStrength = 0.1f;

    private static readonly Dictionary<Texture, float> rockSpread = new Dictionary<Texture, float>();

    /// <summary>How much deeper than the calibrated relief this terrain material draws every ground and its rock (×1 = 2/3/6 cm).</summary>
    public static float Scale(Material material) => material.GetFloat("_DisplacementStrength") / ReferenceStrength;

    /// <summary>Sets the relief of every ground and the steep rock at once, keeping their proportions. Live: no rebuild.</summary>
    public static void SetScale(Material material, float scale)
    {
        scale = Mathf.Clamp(scale, 0f, 1f / ReferenceStrength);
        material.SetFloat("_DisplacementStrength", ReferenceStrength * scale);
        if (material.HasProperty("_CliffDisplacement")) material.SetFloat("_CliffDisplacement", RockBase(material) * scale);
        EditorUtility.SetDirty(material);
    }

    // The rock relief at ×1: CliffRelief over the wider of its two masks' height spread.
    private static float RockBase(Material material)
    {
        float spread = 0.01f;
        foreach (string mask in new[] { "_CliffMask", "_SteepMask" })
        {
            Texture texture = material.HasProperty(mask) ? material.GetTexture(mask) : null;
            if (texture == null) continue;
            if (!rockSpread.TryGetValue(texture, out float s))
            {
                (float low, float high, float _) = Spread(texture, 2);
                rockSpread[texture] = s = high - low;
            }

            spread = Mathf.Max(spread, s);
        }

        return CliffRelief / spread;
    }

    /// <summary>The relief (metres, 1st to 99th percentile) a ground should have, by its name.</summary>
    public static float Relief(TerrainLayer layer)
    {
        string name = layer != null ? layer.name.ToLowerInvariant() : "";
        if (new[] { "rock", "gravel", "stone", "cliff", "scree" }.Any(name.Contains)) return RockRelief;
        if (new[] { "path", "soil", "bank", "dirt", "humus", "earth", "mud" }.Any(name.Contains)) return SoilRelief;
        return SoftRelief;
    }

    [MenuItem("Safe Haven/World/Terrain/Calibrate Ground Relief")]
    private static void CalibrateMenu() => Debug.Log("Ground relief: " + CalibrateAll());

    /// <summary>Calibrates every terrain profile and rock material, and rebuilds their displacement. Returns a summary.</summary>
    public static string CalibrateAll()
    {
        var report = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(TerrainSurfaceProfile)))
        {
            string profilePath = AssetDatabase.GUIDToAssetPath(guid);
            string root = profilePath.Replace(" Surface Profile.asset", "");
            var profile = AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>(profilePath);
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(root + ".asset");
            var material = AssetDatabase.LoadAssetAtPath<Material>(root + " Displacement.mat");
            if (profile == null || data == null || material == null)
            {
                report.Add(Path.GetFileName(root) + ": skipped (no terrain data or material beside it)");
                continue;
            }

            float scale = Scale(material);
            Calibrate(profile, ReferenceStrength);
            SetScale(material, scale);
            Rebuild(data, material);
            report.Add($"{Path.GetFileName(root)}: {profile.layers.Count} grounds, relief ×{scale:0.0}");
        }

        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    /// <summary>Sets each ground's midpoint and amplitude from its own heights (see the class summary).</summary>
    public static void Calibrate(TerrainSurfaceProfile profile, float strength)
    {
        foreach (TerrainSurfaceProfile.Layer entry in profile.layers.Where(e => e != null && e.terrainLayer != null))
        {
            TerrainLayer layer = entry.terrainLayer;
            bool own = entry.heightMap != null;
            Texture source = own ? entry.heightMap : layer.maskMapTexture;
            if (source == null) continue;
            float scale = own ? 1f : layer.maskMapRemapMax.z - layer.maskMapRemapMin.z;
            float offset = own ? 0f : layer.maskMapRemapMin.z;
            (float low, float high, float mean) = Spread(source, own ? (int)entry.channel : 2);
            float spread = Mathf.Max((high - low) * scale, 0.01f);
            entry.contrast = 1f;
            entry.midpoint = Mathf.Clamp01(mean * scale + offset);
            entry.amplitude = Mathf.Clamp(Relief(layer) / (spread * Mathf.Max(strength, 0.001f)), 0f, 4f);
        }

        EditorUtility.SetDirty(profile);
    }

    /// <summary>The steep-ground rock's relief at the material's current scale.</summary>
    public static void CalibrateRock(Material material)
    {
        if (material.HasProperty("_CliffDisplacement")) SetScale(material, Scale(material));
    }

    /// <summary>A texture channel's 1st and 99th percentile and mean, read as the displacement bake reads it.</summary>
    public static (float low, float high, float mean) Spread(Texture texture, int channel)
    {
        const int size = 256;
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var read = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
        try
        {
            Graphics.Blit(texture, target);
            RenderTexture.active = target;
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            float[] values = read.GetPixels().Select(c => c[channel]).OrderBy(v => v).ToArray();
            return (values[values.Length / 100], values[values.Length * 99 / 100], values.Average());
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            Object.DestroyImmediate(read);
        }
    }

    // Rebuilt on a stand-in terrain in a preview scene, so no open scene is touched: the scenes' terrains share these assets.
    private static void Rebuild(TerrainData data, Material material)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject go = Terrain.CreateTerrainGameObject(data);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            var terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = material;
            TerrainDisplacementEditor.Apply(terrain);
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
