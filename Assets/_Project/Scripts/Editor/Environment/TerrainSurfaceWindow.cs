using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class TerrainSurfaceWindow : EditorWindow
{
    [SerializeField] private Terrain terrain;
    private Vector2 scroll;

    [MenuItem("Safe Haven/World/Terrain Surface")]
    public static void Open()
    {
        var window = GetWindow<TerrainSurfaceWindow>("Terrain Surface");
        window.minSize = new Vector2(420, 500);
        window.UseSelection();
    }

    private void OnSelectionChange()
    {
        UseSelection();
        Repaint();
    }

    private void UseSelection()
    {
        if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent(out Terrain selected))
            terrain = selected;
        if (terrain == null) terrain = Terrain.activeTerrain;
    }

    private void OnGUI()
    {
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        if (terrain == null)
        {
            EditorGUILayout.HelpBox("Choose a terrain. This tool preserves its painted layers; mesh materials are not supported.", MessageType.Info);
            return;
        }
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.HelpBox("Real displaced geometry with height-derived lighting. Collider and NavMesh are unchanged. " +
            "High amplitudes can cause floating props. Maximum quality is GPU intensive.", MessageType.Info);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Set Up / Rebuild Height Resources"))
            {
                TerrainDisplacementEditor.Apply(terrain);
                GUIUtility.ExitGUI();
            }
        }
        TerrainSurface surface = terrain.GetComponent<TerrainSurface>();
        Material material = terrain.materialTemplate;
        if (surface == null || surface.Profile == null || material == null || material.shader.name != TerrainDisplacementEditor.ShaderName)
        {
            EditorGUILayout.HelpBox("Set up the terrain to assign the world shader, a dedicated material and a height profile.", MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }
        EditorGUILayout.ObjectField("Assigned Material", material, typeof(Material), false);
        var quality = (TerrainSurface.DetailQuality)EditorGUILayout.EnumPopup("Quality Preview / Runtime", surface.Quality);
        if (quality != surface.Quality)
        {
            Undo.RecordObject(surface, "Change Terrain Surface Quality");
            surface.SetQuality(quality);
            EditorUtility.SetDirty(surface);
        }
        EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
        EditorGUILayout.LabelField("Geometry", EditorStyles.boldLabel);
        Slider(material, "_DisplacementStrength", "Height Range (metres)", 0, 1);
        Slider(material, "_DisplacementNear", "Full Detail Distance", 0, 100);
        Slider(material, "_DisplacementFar", "Fade Distance", material.GetFloat("_DisplacementNear") + 0.1f, 200);
        Slider(material, "_TessellationFactor", "Maximum Subdivision", 1, 64);
        Slider(material, "_TessellationEdgeLength", "Target Edge Length (metres)", 0.005f, 0.2f);
        Slider(material, "_TessellationPixels", "Screen Edge Limit (pixels, 0 off)", 0, 32);
        Slider(material, "_DisplacementMip", "Height Smoothing", 0, 5);
        Slider(material, "_HeightBlendSharpness", "Height Blend Sharpness", 0, 8);
        EditorGUILayout.HelpBox("Height blend sharpness affects geometry across every painted layer. It does not replace URP's colour blending. " +
            "A 64 subdivision cap does not guarantee 64 subdivisions: target edge size and quality also limit density.", MessageType.None);

        EditorGUILayout.LabelField("Recovering Footprints", EditorStyles.boldLabel);
        var footprintProfile = new SerializedObject(surface.Profile);
        footprintProfile.Update();
        foreach (string field in new[] { "footprints", "footprintLifetime", "footprintDepth", "footprintFlattening",
            "footprintNormalFlattening", "footprintDarkening", "footprintGrassFlattening", "bootWidth", "bootLength", "bootMask", "footprintArea", "footprintResolution" })
            EditorGUILayout.PropertyField(footprintProfile.FindProperty(field));
        if (footprintProfile.ApplyModifiedProperties()) surface.Apply();
        EditorGUILayout.HelpBox("30 seconds by default, using game time (pause stops recovery). White in the optional sole mask presses down. " +
            "Per-layer Footprint Response below requires Rebuild: 0 = hard ground, 1 = soft moss/mud. " +
            "The local rolling field discards distant tracks. Grass bends visually without editing TerrainData, instances, colliders or save games.", MessageType.None);

        EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
        Slider(material, "_DisplacementNormalStrength", "Height Normal Strength", 0, 2);
        Slider(material, "_NormalMapStrength", "Normal Map Multiplier", 0, 3);
        Slider(material, "_SurfaceOcclusion", "Occlusion Strength", 0, 2);
        Slider(material, "_SurfaceSmoothness", "Smoothness Multiplier", 0, 2);
        Slider(material, "_SurfaceDebug", "Preview: 0 Lit / 1 Height / 2 Normals / 3 Tracks", 0, 3, true);
        EditorGUILayout.HelpBox("Diagnostic colour previews require the Forward rendering path. Height-normal lighting works in Forward and Deferred.", MessageType.None);
        EditorGUILayout.LabelField("Quality Presets (keep height and layer settings)", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Balanced")) Preset(material, 16, 0.04f, 12, 35);
        if (GUILayout.Button("Maximum")) Preset(material, 64, 0.015f, 20, 50);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Height Sources and Per-Layer Shape", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            var profile = new SerializedObject(surface.Profile);
            profile.Update();
            EditorGUILayout.PropertyField(profile.FindProperty("resolution"));
            EditorGUILayout.PropertyField(profile.FindProperty("layers"), true);
            if (profile.ApplyModifiedProperties())
                ShowNotification(new GUIContent("Rebuild Height Resources to apply height-source changes"));
        }
        EditorGUILayout.HelpBox("An empty height override uses the layer mask's BLUE channel. Overrides use the selected channel and must be linear (sRGB off). " +
            "Height source, contrast, amplitude, midpoint, resolution and tiling changes require Rebuild.", MessageType.None);

        EditorGUILayout.LabelField("Painted Layer Textures and Normals", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("These are shared Terrain Layer assets: changes also affect other terrains using the same layer. " +
            "Normal strength changes are immediate; use Rebuild after changing tile size or offset.", MessageType.Warning);
        foreach (TerrainLayer layer in terrain.terrainData.terrainLayers)
        {
            if (layer == null) continue;
            EditorGUILayout.ObjectField(layer.name, layer, typeof(TerrainLayer), false);
            EditorGUI.BeginChangeCheck();
            var diffuse = (Texture2D)EditorGUILayout.ObjectField("Albedo", layer.diffuseTexture, typeof(Texture2D), false);
            var normalMap = (Texture2D)EditorGUILayout.ObjectField("Normal Map", layer.normalMapTexture, typeof(Texture2D), false);
            var mask = (Texture2D)EditorGUILayout.ObjectField("Mask (Metal/AO/Height/Smooth)", layer.maskMapTexture, typeof(Texture2D), false);
            float normal = EditorGUILayout.Slider("Normal Strength", layer.normalScale, 0, 5);
            Vector2 tile = EditorGUILayout.Vector2Field("Tile Size (metres)", layer.tileSize);
            Vector2 offset = EditorGUILayout.Vector2Field("Tile Offset (metres)", layer.tileOffset);
            Vector4 remapMin = EditorGUILayout.Vector4Field("Mask Remap Minimum", layer.maskMapRemapMin);
            Vector4 remapMax = EditorGUILayout.Vector4Field("Mask Remap Maximum", layer.maskMapRemapMax);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(layer, "Tune Terrain Layer");
                layer.normalScale = normal;
                layer.diffuseTexture = diffuse;
                layer.normalMapTexture = normalMap;
                layer.maskMapTexture = mask;
                layer.tileSize = new Vector2(Mathf.Max(0.01f, tile.x), Mathf.Max(0.01f, tile.y));
                layer.tileOffset = offset;
                layer.maskMapRemapMin = remapMin;
                layer.maskMapRemapMax = remapMax;
                EditorUtility.SetDirty(layer);
            }
        }
        EditorGUILayout.LabelField("Cliffs", EditorStyles.boldLabel);
        Slider(material, "_CliffStrength", "Cliff Coverage", 0, 1);
        Slider(material, "_CliffSlopeStart", "Starts At (degrees)", 0, 90);
        Slider(material, "_CliffSlopeEnd", "Fully Rock At (degrees)", 0, 90);
        Slider(material, "_CliffBreakup", "Ragged Edge", 0, 1);
        Slider(material, "_CliffTiling", "Rock Size (metres)", 0.1f, 30);
        Slider(material, "_CliffNormalStrength", "Rock Normal Strength", 0, 2);
        Texture(material, "_CliffAlbedo", "Rock Colour");
        Texture(material, "_CliffNormal", "Rock Normal");
        Texture(material, "_CliffMask", "Rock Mask");
        EditorGUI.BeginChangeCheck();
        Color tint = EditorGUILayout.ColorField("Rock Tint", material.GetColor("_CliffTint"));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(material, "Change Cliff Tint");
            material.SetColor("_CliffTint", tint);
            EditorUtility.SetDirty(material);
        }
        if (GUILayout.Button("Save Surface Assets"))
        {
            AssetDatabase.SaveAssetIfDirty(material);
            if (surface.Profile != null) AssetDatabase.SaveAssetIfDirty(surface.Profile);
            foreach (TerrainLayer layer in terrain.terrainData.terrainLayers)
                if (layer != null) AssetDatabase.SaveAssetIfDirty(layer);
        }
        EditorGUILayout.HelpBox("Component/material assignment and quality selection require saving the scene. The button below also saves other unsaved work in this scene.", MessageType.None);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
            if (GUILayout.Button("Save This Scene (including other scene changes)"))
                EditorSceneManager.SaveScene(terrain.gameObject.scene);
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndScrollView();
    }

    private void Slider(Material material, string property, string label, float min, float max, bool integer = false)
    {
        EditorGUI.BeginChangeCheck();
        float current = material.GetFloat(property);
        float value = integer ? EditorGUILayout.IntSlider(label, Mathf.RoundToInt(current), (int)min, (int)max)
            : EditorGUILayout.Slider(label, current, min, max);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(material, "Tune Terrain Surface");
        material.SetFloat(property, value);
        EditorUtility.SetDirty(material);
        terrain.GetComponent<TerrainSurface>().Apply();
        SceneView.RepaintAll();
    }

    private static void Texture(Material material, string property, string label)
    {
        EditorGUI.BeginChangeCheck();
        var value = (Texture2D)EditorGUILayout.ObjectField(label, material.GetTexture(property), typeof(Texture2D), false);
        if (!EditorGUI.EndChangeCheck()) return;
        Undo.RecordObject(material, "Change Cliff Texture");
        material.SetTexture(property, value);
        EditorUtility.SetDirty(material);
    }

    private void Preset(Material material, float factor, float edge, float near, float far)
    {
        Undo.RecordObject(material, "Apply Terrain Quality Preset");
        material.SetFloat("_TessellationFactor", factor);
        material.SetFloat("_TessellationEdgeLength", edge);
        material.SetFloat("_DisplacementNear", near);
        material.SetFloat("_DisplacementFar", far);
        // Triangles smaller than this many pixels show no more detail, only cost: about 9 ms of GPU at 1080p was spent
        // on 1-pixel triangles round the Haven with this at 0 (see LevelDesign.md); 6 rather than 4 since the 1 km Haven.
        material.SetFloat("_TessellationPixels", 6);
        EditorUtility.SetDirty(material);
        terrain.GetComponent<TerrainSurface>().SetQuality(TerrainSurface.DetailQuality.Ultra);
        SceneView.RepaintAll();
    }
}
