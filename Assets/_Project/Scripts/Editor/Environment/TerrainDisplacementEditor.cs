using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Builds persistent height-mask resources shared by every terrain rendering pass.</summary>
public static class TerrainDisplacementEditor
{
    public const string ShaderName = "Safe Haven/Terrain (World)";

    [MenuItem("Safe Haven/World/Terrain/Enable or Refresh Displacement")]
    private static void EnableSelected()
    {
        Terrain terrain = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Terrain>() : null;
        if (terrain == null)
            throw new InvalidOperationException("Select the terrain to enable or refresh its displacement.");
        Apply(terrain);
        Debug.Log("Terrain displacement configured. Save the scene to keep its material assignment.", terrain);
    }

    public static Material Apply(Terrain terrain)
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Configure terrain displacement outside Play Mode.");
        if (!SystemInfo.supportsTessellationShaders)
            throw new NotSupportedException("Terrain displacement requires hardware tessellation support.");
        if (terrain == null || terrain.terrainData == null)
            throw new ArgumentException("Expected a terrain with saved TerrainData.");
        if (terrain.materialTemplate != null && terrain.materialTemplate.shader.name != ShaderName &&
            terrain.materialTemplate.shader.name != "Universal Render Pipeline/Terrain/Lit")
            throw new ArgumentException("Only Safe Haven world terrain and URP Terrain Lit materials can be converted safely.");
        Shader shader = Shader.Find(ShaderName);
        if (shader == null || !shader.isSupported)
            throw new InvalidOperationException("The Safe Haven terrain shader must compile before setup.");

        TerrainData data = terrain.terrainData;
        TerrainLayer[] layers = data.terrainLayers;
        if (layers.Length == 0 || layers.Length > 16)
            throw new NotSupportedException("Displacement supports between 1 and 16 painted terrain layers.");
        foreach (TerrainLayer layer in layers)
            if (layer == null || layer.tileSize.x <= 0 || layer.tileSize.y <= 0)
                throw new InvalidOperationException("Every terrain layer must exist and have positive tile dimensions.");
        string dataPath = AssetDatabase.GetAssetPath(data);
        if (string.IsNullOrEmpty(dataPath))
            throw new InvalidOperationException("Save the TerrainData asset before configuring displacement.");
        string root = Path.ChangeExtension(dataPath, null);
        string maskPath = root + " Displacement Masks.asset";
        string layerPath = root + " Displacement Layers.asset";
        string materialPath = root + " Displacement.mat";
        TerrainSurface surface = terrain.GetComponent<TerrainSurface>();
        if (surface == null) surface = Undo.AddComponent<TerrainSurface>(terrain.gameObject);
        if (terrain.GetComponent<TerrainFootprints>() == null)
            Undo.AddComponent<TerrainFootprints>(terrain.gameObject);
        var footprints = new SerializedObject(terrain.GetComponent<TerrainFootprints>());
        footprints.FindProperty("stampShader").objectReferenceValue = Shader.Find("Hidden/Safe Haven/Terrain/Footprint Stamp");
        footprints.ApplyModifiedProperties();
        TerrainSurfaceProfile profile = surface.Profile;
        if (profile == null)
        {
            string profilePath = root + " Surface Profile.asset";
            profile = AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<TerrainSurfaceProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }
            Undo.RecordObject(surface, "Assign Terrain Surface Profile");
            surface.Profile = profile;
        }
        foreach (TerrainLayer layer in layers)
        {
            if (profile.Find(layer) != null) continue;
            TerrainSurfaceProfile.Layer known = StandardProfile != null ? StandardProfile.Find(layer) : null;
            var entry = new TerrainSurfaceProfile.Layer { terrainLayer = layer };
            if (known != null)
            {
                entry.footprintResponse = known.footprintResponse;
                entry.amplitude = known.amplitude;
            }

            string layerPathOnDisk = AssetDatabase.GetAssetPath(layer);
            string sourcePath = Path.GetDirectoryName(layerPathOnDisk).Replace('\\', '/') + "/" + layer.name + "_Height.png";
            Texture2D sourceHeight = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            if (sourceHeight != null)
                entry.heightMap = sourceHeight;
            profile.layers.Add(entry);
        }
        EditorUtility.SetDirty(profile);
        int resolution = (int)profile.resolution;
        if (resolution != 512 && resolution != 1024 && resolution != 2048)
            throw new InvalidOperationException("Height resolution must be 512, 1024 or 2048.");
        foreach (TerrainLayer layer in layers)
        {
            TerrainSurfaceProfile.Layer entry = profile.Find(layer);
            if (entry.heightMap != null && AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(entry.heightMap)) is TextureImporter importer && importer.sRGBTexture)
                throw new InvalidOperationException("Height maps must be imported with sRGB disabled: " + AssetDatabase.GetAssetPath(entry.heightMap));
        }

        var masks = new Texture2DArray(resolution, resolution, layers.Length, TextureFormat.RHalf, true, true)
        {
            name = data.name + " Displacement Masks",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };
        var layerData = new Texture2D(16, 3, TextureFormat.RGBAFloat, false, true)
        {
            name = data.name + " Displacement Layers",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        RenderTexture previous = RenderTexture.active;
        RenderTexture target = RenderTexture.GetTemporary(resolution, resolution, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var readback = new Texture2D(resolution, resolution, TextureFormat.RGBAFloat, false, true);
        var heights = new ushort[resolution * resolution];
        try
        {
            for (int i = 0; i < layers.Length; i++)
            {
                TerrainLayer layer = layers[i];
                TerrainSurfaceProfile.Layer entry = profile.Find(layer);
                Texture2D source = entry.heightMap != null ? entry.heightMap : layer.maskMapTexture;
                Graphics.Blit(source != null ? source : Texture2D.grayTexture, target);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0);
                readback.Apply(false);
                Color[] pixels = readback.GetPixels();
                int channel = entry.heightMap != null ? (int)entry.channel : 2;
                for (int p = 0; p < pixels.Length; p++)
                    heights[p] = Mathf.FloatToHalf(Mathf.Clamp01((pixels[p][channel] - 0.5f) * entry.contrast + 0.5f));
                masks.SetPixelData(heights, 0, i);
                layerData.SetPixel(i, 0, new Color(data.size.x / layer.tileSize.x, data.size.z / layer.tileSize.y,
                    layer.tileOffset.x / layer.tileSize.x, layer.tileOffset.y / layer.tileSize.y));
                layerData.SetPixel(i, 1, source != null
                    ? new Color(entry.heightMap != null ? 1 : layer.maskMapRemapMax.z - layer.maskMapRemapMin.z,
                        entry.heightMap != null ? 0 : layer.maskMapRemapMin.z, entry.amplitude, entry.midpoint)
                    : new Color(0, entry.midpoint, 0, entry.midpoint));
                layerData.SetPixel(i, 2, new Color(entry.footprintResponse, 0, 0, 0));
            }
            masks.Apply(true, false);
            layerData.Apply(false, false);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(readback);
        }

        Texture2DArray savedMasks = SaveResource(masks, maskPath);
        Texture2D savedLayers = SaveResource(layerData, layerPath);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = terrain.materialTemplate != null ? new Material(terrain.materialTemplate) : new Material(shader);
            material.name = data.name + " Displacement";
            material.shader = shader;
            UseStandardDisplacement(material);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else if (terrain.materialTemplate != null && terrain.materialTemplate != material)
        {
            Undo.RecordObject(material, "Refresh Terrain Displacement Material");
            material.CopyPropertiesFromMaterial(terrain.materialTemplate);
            material.shader = shader;
            UseStandardDisplacement(material);
        }
        material.SetTexture("_DisplacementMasks", savedMasks);
        material.SetTexture("_DisplacementLayers", savedLayers);
        material.SetFloat("_DisplacementLayerCount", layers.Length);
        material.SetFloat("_DisplacementDataVersion", 2);
        material.SetVector("_DisplacementTerrainSize", new Vector4(data.size.x, data.size.z, 0.01f, 0));
        Texture2D[] controls = data.alphamapTextures;
        for (int i = 0; i < 4; i++)
            material.SetTexture("_DisplacementControl" + i, i < controls.Length ? controls[i] : Texture2D.blackTexture);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        AssetDatabase.SaveAssetIfDirty(savedMasks);
        AssetDatabase.SaveAssetIfDirty(savedLayers);
        AssetDatabase.SaveAssetIfDirty(profile);
        Undo.RecordObject(terrain, "Enable Terrain Displacement");
        // The domain passes consume expanded terrain vertices, not instanced heightmap patches.
        terrain.drawInstanced = false;
        terrain.materialTemplate = material;
        surface.Apply();
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        terrain.Flush();
        return material;
    }

    // The Haven's terrain is the standard: footprints read the same on every terrain only if the ground around them is shaped alike.
    private const string StandardProfilePath = "Assets/_Project/Art/Terrain/SafeHaven/Haven World Terrain Surface Profile.asset";
    private const string StandardMaterialPath = "Assets/_Project/Art/Terrain/SafeHaven/Haven World Terrain Displacement.mat";
    private static readonly string[] DisplacementShape =
        { "_DisplacementStrength", "_DisplacementNear", "_DisplacementFar", "_TessellationFactor", "_TessellationEdgeLength", "_TessellationPixels" };

    private static TerrainSurfaceProfile StandardProfile => AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>(StandardProfilePath);

    /// <summary>Gives a material the standard's displacement and tessellation (the Haven's own is left as it is).</summary>
    public static void UseStandardDisplacement(Material material)
    {
        Material standard = AssetDatabase.LoadAssetAtPath<Material>(StandardMaterialPath);
        if (standard == null || standard == material)
        {
            return;
        }

        foreach (string property in DisplacementShape)
        {
            if (material.HasProperty(property) && standard.HasProperty(property))
            {
                material.SetFloat(property, standard.GetFloat(property));
            }
        }
    }

    /// <summary>Makes a terrain's footprints the standard's, and rebuilds its surface data with them.</summary>
    public static void UseStandardFootprints(Terrain terrain)
    {
        TerrainSurfaceProfile profile = terrain.GetComponent<TerrainSurface>().Profile;
        if (profile != StandardProfile)
        {
            profile.UseFootprintsOf(StandardProfile);
            EditorUtility.SetDirty(profile);
        }

        Apply(terrain);
        UseStandardDisplacement(terrain.materialTemplate);
        EditorUtility.SetDirty(terrain.materialTemplate);
        AssetDatabase.SaveAssetIfDirty(terrain.materialTemplate);
        terrain.GetComponent<TerrainSurface>().Apply();
    }

    private static T SaveResource<T>(T resource, string path) where T : UnityEngine.Object
    {
        T existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(resource, path);
            return resource;
        }
        EditorUtility.CopySerialized(resource, existing);
        UnityEngine.Object.DestroyImmediate(resource);
        EditorUtility.SetDirty(existing);
        return existing;
    }
}
