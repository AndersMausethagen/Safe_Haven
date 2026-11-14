using System;
using System.IO;
using System.Linq;
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
        // The shader displaces the first sixteen layers (four control maps); any past them are painted but stay flat. Refusing a
        // terrain with more left its displacement stale: the Haven's still had the tiling of its old 400 m terrain at 1 km.
        TerrainLayer[] layers = data.terrainLayers.Take(16).ToArray();
        if (layers.Length == 0)
            throw new NotSupportedException("Displacement needs at least one painted terrain layer.");
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
            TerrainSurfaceProfile.Layer known = KnownSettings(layer);
            var entry = new TerrainSurfaceProfile.Layer { terrainLayer = layer, rock = TerrainSurfaceProfile.RockOf(layer) };
            if (known != null)
            {
                entry.footprintResponse = known.footprintResponse;
                entry.amplitude = known.amplitude;
                entry.midpoint = known.midpoint;
                entry.contrast = known.contrast;
                entry.heightMap = known.heightMap;
                entry.channel = known.channel;
            }

            string layerPathOnDisk = AssetDatabase.GetAssetPath(layer);
            string sourcePath = Path.GetDirectoryName(layerPathOnDisk).Replace('\\', '/') + "/" + layer.name + "_Height.png";
            Texture2D sourceHeight = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
            if (sourceHeight != null && entry.heightMap == null)
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
                // A mask's height is read from its file, at full precision, not from the imported texture: the masks are
                // block-compressed for the game (BC7), and compression steps the height, which tessellation shows.
                Texture2D fromFile = entry.heightMap == null ? SourcePixels(source) : null;
                Graphics.Blit(fromFile != null ? fromFile : source != null ? source : Texture2D.grayTexture, target);
                if (fromFile != null) UnityEngine.Object.DestroyImmediate(fromFile);
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
                layerData.SetPixel(i, 2, new Color(entry.footprintResponse, (float)entry.rock, 0, 0));
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
        if (material.HasProperty("_CliffHeightMidpoint"))
            material.SetFloat("_CliffHeightMidpoint", MeanHeight(material.GetTexture("_CliffMask")));
        if (material.HasProperty("_SteepHeightMidpoint"))
            material.SetFloat("_SteepHeightMidpoint", MeanHeight(material.GetTexture("_SteepMask")));
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
        { "_DisplacementStrength", "_DisplacementNear", "_DisplacementFar", "_TessellationFactor", "_TessellationEdgeLength", "_TessellationPixels",
          "_DetailFocusNear", "_DetailFocusFar" };

    private static TerrainSurfaceProfile StandardProfile => AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>(StandardProfilePath);

    /// <summary>How a ground is already set up on another terrain (the Haven's first), so a layer added to a terrain
    /// displaces and takes footprints as it does where it was tuned.</summary>
    private static TerrainSurfaceProfile.Layer KnownSettings(TerrainLayer layer)
    {
        TerrainSurfaceProfile.Layer known = StandardProfile != null ? StandardProfile.Find(layer) : null;
        if (known != null) return known;
        foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(TerrainSurfaceProfile)))
        {
            var other = AssetDatabase.LoadAssetAtPath<TerrainSurfaceProfile>(AssetDatabase.GUIDToAssetPath(guid));
            known = other != null ? other.Find(layer) : null;
            if (known != null) return known;
        }

        return null;
    }

    /// <summary>The average of a mask's blue channel (its height): the level the rock's relief is pushed out and in from.</summary>
    public static float MeanHeight(Texture texture)
    {
        if (texture == null) return 0.5f;
        RenderTexture previous = RenderTexture.active;
        RenderTexture small = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var read = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
        try
        {
            Graphics.Blit(texture, small);
            RenderTexture.active = small;
            read.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
            float sum = 0;
            foreach (Color32 pixel in read.GetPixels32()) sum += pixel.b;
            return sum / (64 * 64 * 255f);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(small);
            UnityEngine.Object.DestroyImmediate(read);
        }
    }

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

    // The texture's own file, decoded as it is on disk (linear, uncompressed), or null when Unity can't decode it here
    // (only PNG and JPG can be): the imported texture is used then.
    internal static Texture2D SourcePixels(Texture2D texture)
    {
        if (texture == null) return null;
        string path = AssetDatabase.GetAssetPath(texture);
        string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") return null;
        var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, true, true) { hideFlags = HideFlags.HideAndDontSave };
        if (decoded.LoadImage(System.IO.File.ReadAllBytes(path), false)) return decoded;
        UnityEngine.Object.DestroyImmediate(decoded);
        return null;
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
