using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(TerrainMoss))]
public sealed class TerrainMossEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Paint the selected Moss Layer using Terrain > Paint Texture. The surface follows the alphamap. " +
            "After painting or sculpting, rebuild to redistribute tufts and fit the ground. Nothing here changes collision or gameplay saves.", MessageType.Info);
        DrawDefaultInspector();
        var moss = (TerrainMoss)target;
        if (!string.IsNullOrEmpty(moss.LastError))
        {
            EditorGUILayout.HelpBox(moss.LastError, MessageType.Warning);
        }

        EditorGUILayout.LabelField("Generated tufts", moss.TuftCount.ToString());
        if (GUILayout.Button("Rebuild Moss"))
        {
            moss.Rebuild();
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Select Scene Wind"))
        {
            MossWind wind = Object.FindAnyObjectByType<MossWind>();
            if (wind != null) Selection.activeGameObject = wind.gameObject;
            else EditorUtility.DisplayDialog("Moss wind", "Add a wind source using Safe Haven > World > Sky and Weather > Add Shared Moss Wind.", "OK");
        }
    }
}

[CustomEditor(typeof(MossWind))]
public sealed class MossWindEditor : Editor
{
    private bool animate;

    public override bool RequiresConstantRepaint() => animate;

    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("One wind source drives all Terrain Moss and Tree Wind trees in the loaded scenes. " +
            "With a Directional WindZone on this object, its arrow is the direction, Main scales moss Strength and tree sway, " +
            "and Pulse Magnitude is the moss gusts. Strength is moss tip movement in metres. No physics or terrain movement.",
            MessageType.Info);
        DrawDefaultInspector();
        animate = EditorGUILayout.Toggle("Preview wind in Scene view", animate);
        if (animate)
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
        }
    }
}

/// <summary>Opt-in setup tools: never replace an existing terrain material or repaint a designer's terrain.</summary>
public static class TerrainMossTools
{
    private const string Root = "Assets/_Project/Art/Moss";
    public const string PreviewPath = "Assets/_Project/Scenes/MossPreview.unity";
    private const string ShaderName = "Safe Haven/Terrain Moss";

    [MenuItem("Safe Haven/World/Terrain/Add Moss to Selected Terrain")]
    public static void AddToSelectedTerrain()
    {
        Terrain terrain = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Terrain>() : null;
        if (terrain == null || terrain.terrainData == null)
        {
            EditorUtility.DisplayDialog("Terrain Moss", "Select a Unity Terrain with Terrain Data first.", "OK");
            return;
        }

        if (terrain.GetComponent<TerrainMoss>() != null)
        {
            EditorUtility.DisplayDialog("Terrain Moss", "This terrain already has Terrain Moss. Adjust its existing component instead.", "OK");
            return;
        }

        TerrainLayer layer = DefaultLayer();
        Shader shader = RequiredShader();
        AddLayer(terrain.terrainData, layer);
        TerrainMoss moss = terrain.GetComponent<TerrainMoss>();
        if (moss == null) moss = Undo.AddComponent<TerrainMoss>(terrain.gameObject);
        Undo.RecordObject(moss, "Configure terrain moss");
        moss.Configure(layer, shader);
        EditorUtility.SetDirty(moss);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        AddWind(terrain.gameObject.scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = terrain.gameObject;
    }

    [MenuItem("Safe Haven/World/Sky and Weather/Add Shared Moss Wind")]
    public static void AddWindToScene()
    {
        Selection.activeGameObject = AddWind(SceneManager.GetActiveScene()).gameObject;
    }

    private static MossWind AddWind(Scene scene)
    {
        MossWind existing = Object.FindAnyObjectByType<MossWind>();
        if (existing != null) return existing;
        var source = new GameObject("Forest Wind");
        source.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
        SceneManager.MoveGameObjectToScene(source, scene);
        Undo.RegisterCreatedObjectUndo(source, "Add moss wind");
        // The WindZone is the shared wind for moss, Tree Wind trees and particles; MossWind finds it on this object.
        var zone = source.AddComponent<WindZone>();
        zone.mode = WindZoneMode.Directional;
        zone.windMain = 1f;
        zone.windPulseMagnitude = 0.35f;
        return Undo.AddComponent<MossWind>(source);
    }

    [MenuItem("Safe Haven/Setup/Art Pipeline/Open Moss Preview", false, 200)]
    public static void OpenPreview()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath) != null)
        {
            EditorSceneManager.OpenScene(PreviewPath);
            return;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreatePreview();
        EditorSceneManager.OpenScene(PreviewPath);
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null) view.LookAt(new Vector3(16f, 1f, 16f), Quaternion.Euler(35f, 0f, 0f), 24f);
    }

    private static Scene CreatePreview()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(PreviewPath) != null)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loaded = SceneManager.GetSceneAt(i);
                if (loaded.path == PreviewPath) return loaded;
            }
            return EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Additive);
        }

        Shader shader = RequiredShader();
        TerrainLayer mossLayer = DefaultLayer();
        TerrainLayer soil = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Root + "/ForestSoil.terrainlayer");
        if (soil == null)
        {
            soil = new TerrainLayer { name = "Forest Soil", diffuseTexture = SolidTexture("SoilColor", new Color(0.19f, 0.15f, 0.1f)), tileSize = new Vector2(3f, 3f) };
            AssetDatabase.CreateAsset(soil, Root + "/ForestSoil.terrainlayer");
        }

        var data = new TerrainData { name = "Moss Preview", heightmapResolution = 65, alphamapResolution = 64, size = new Vector3(32f, 4f, 32f) };
        AssetDatabase.CreateAsset(data, Root + "/MossPreviewTerrain.asset");
        var heights = new float[65, 65];
        for (int z = 0; z < 65; z++)
            for (int x = 0; x < 65; x++)
                heights[z, x] = 0.06f + Mathf.PerlinNoise(x * 0.04f, z * 0.04f) * 0.16f;
        data.SetHeights(0, 0, heights);
        data.terrainLayers = new[] { soil, mossLayer };
        var paint = new float[64, 64, 2];
        for (int z = 0; z < 64; z++)
        {
            for (int x = 0; x < 64; x++)
            {
                float path = Mathf.Abs(x - 31f - Mathf.Sin(z * 0.13f) * 5f);
                float weight = Mathf.SmoothStep(0f, 1f, (path - 3f) / 5f);
                paint[z, x, 0] = 1f - weight;
                paint[z, x, 1] = weight;
            }
        }
        data.SetAlphamaps(0, 0, paint);
        EditorUtility.SetDirty(data);
        foreach (Texture2D control in data.alphamapTextures) EditorUtility.SetDirty(control);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        GameObject ground = Terrain.CreateTerrainGameObject(data);
        ground.name = "Paintable Moss Terrain";
        Terrain terrain = ground.GetComponent<Terrain>();
        terrain.heightmapPixelError = 1f;
        var terrainMaterial = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")) { name = "Moss Preview Ground" };
        AssetDatabase.CreateAsset(terrainMaterial, Root + "/MossPreviewGround.mat");
        terrain.materialTemplate = terrainMaterial;
        ground.AddComponent<TerrainMoss>().Configure(mossLayer, shader);
        AddWind(scene);

        var sun = new GameObject("Warm Forest Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.91f, 0.74f);
        sun.intensity = 1.6f;
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
        var camera = new GameObject("Preview Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(16f, 12f, -9f);
        camera.transform.LookAt(new Vector3(16f, 0.5f, 15f));
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.12f, 0.17f, 0.18f);
        camera.fieldOfView = 52f;
        camera.farClipPlane = 120f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.43f, 0.5f, 0.55f);
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.32f, 0.23f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.14f, 0.08f);
        RenderSettings.sun = sun;
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, PreviewPath);
        Selection.activeGameObject = ground;
        return scene;
    }

    private static TerrainLayer DefaultLayer()
    {
        EnsureFolder("Assets/_Project", "Art");
        EnsureFolder("Assets/_Project/Art", "Moss");
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(Root + "/ForestMoss.terrainlayer");
        if (layer != null) return layer;
        layer = new TerrainLayer { name = "Forest Moss", diffuseTexture = SolidTexture("MossColor", new Color(0.15f, 0.23f, 0.06f)), tileSize = new Vector2(2f, 2f) };
        AssetDatabase.CreateAsset(layer, Root + "/ForestMoss.terrainlayer");
        return layer;
    }

    private static Texture2D SolidTexture(string name, Color color)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name };
        texture.SetPixels(new[] { color, color, color, color });
        texture.Apply();
        AssetDatabase.CreateAsset(texture, Root + "/" + name + ".asset");
        return texture;
    }

    private static Shader RequiredShader()
    {
        Shader shader = Shader.Find(ShaderName);
        if (shader == null) throw new System.InvalidOperationException("Terrain Moss shader has not imported. Resolve shader errors before setup.");
        return shader;
    }

    private static void AddLayer(TerrainData data, TerrainLayer layer)
    {
        TerrainLayer[] layers = data.terrainLayers;
        if (System.Array.IndexOf(layers, layer) >= 0) return;
        Undo.RecordObject(data, "Add moss terrain layer");
        System.Array.Resize(ref layers, layers.Length + 1);
        layers[layers.Length - 1] = layer;
        data.terrainLayers = layers;
        EditorUtility.SetDirty(data);
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }
}
