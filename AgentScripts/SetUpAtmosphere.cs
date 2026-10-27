using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Wires the atmosphere into the project (run each step with `unity command run_script --file
/// AgentScripts/SetUpAtmosphere.cs --entry SetUpAtmosphere.<Step>`): the renderer feature, the volume overrides for
/// Safe Haven, its night and the Misty Woods, the player's sight, and the scene objects. Re-running updates in place.
/// </summary>
public static class SetUpAtmosphere
{
    private const string Renderer = "Assets/_Project/Settings/Rendering/PC_Renderer.asset";
    private const string Volumes = "Assets/_Project/Settings/Rendering/Volumes/";
    private const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";

    public static string Feature()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer);
        foreach (ScriptableRendererFeature existing in data.rendererFeatures)
        {
            if (existing is AtmosphereFeature)
            {
                return "already on " + data.name;
            }
        }

        var feature = ScriptableObject.CreateInstance<AtmosphereFeature>();
        feature.name = "Atmosphere";
        var featureData = new SerializedObject(feature);
        featureData.FindProperty("shader").objectReferenceValue = Shader.Find("Hidden/Safe Haven/Atmosphere");
        featureData.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.AddObjectToAsset(feature, data);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

        var so = new SerializedObject(data);
        SerializedProperty features = so.FindProperty("m_RendererFeatures");
        SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
        features.arraySize++;
        features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
        map.arraySize++;
        map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
        so.ApplyModifiedPropertiesWithoutUndo();
        data.SetDirty();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        return $"added to {data.name}: {data.rendererFeatures.Count} features";
    }

    public static string VolumeOverrides()
    {
        var log = new System.Text.StringBuilder();

        // Safe Haven by day: a thin morning mist in the low ground, the unexplored world dark, the night veil ready.
        VolumeProfile home = Profile("SafeHaven_Volume");
        AtmosphericFog fog = Get<AtmosphericFog>(home);
        fog.density.Override(0.012f);
        fog.baseHeight.Override(-0.5f);
        fog.heightFalloff.Override(0.35f);
        fog.albedo.Override(new Color(0.9f, 0.92f, 0.95f));
        fog.ambient.Override(new Color(0.38f, 0.41f, 0.44f));
        fog.mainLight.Override(1.2f);
        fog.anisotropy.Override(0.5f);
        fog.localLights.Override(1.5f);
        fog.noiseSize.Override(18f);
        fog.noiseStrength.Override(0.55f);
        fog.wind.Override(new Vector3(0.5f, 0.05f, 0.25f));
        fog.clearing.Override(0.6f);
        fog.maxDistance.Override(90f);
        fog.steps.Override(24);
        FogOfWar war = Get<FogOfWar>(home);
        SetWar(war);
        Save(home, log);

        // Night at home: the mist thickens and goes dark, so moonlight and torches show in it.
        VolumeProfile night = Profile("SafeHaven_Night");
        AtmosphericFog nightFog = Get<AtmosphericFog>(night);
        nightFog.density.Override(0.03f);
        nightFog.ambient.Override(new Color(0.035f, 0.045f, 0.07f));
        nightFog.albedo.Override(new Color(0.7f, 0.78f, 0.9f));
        nightFog.mainLight.Override(2.2f);
        nightFog.localLights.Override(2.5f);
        Save(night, log);

        // The Misty Woods: thick, drifting mist that hides what lies ahead.
        VolumeProfile raid = Profile("MistyWoods_Volume");
        AtmosphericFog raidFog = Get<AtmosphericFog>(raid);
        raidFog.density.Override(0.035f);
        raidFog.baseHeight.Override(3f);
        raidFog.heightFalloff.Override(0.2f);
        raidFog.albedo.Override(new Color(0.8f, 0.88f, 0.9f));
        raidFog.ambient.Override(new Color(0.24f, 0.29f, 0.31f));
        raidFog.mainLight.Override(1.5f);
        raidFog.anisotropy.Override(0.55f);
        raidFog.localLights.Override(2f);
        raidFog.noiseSize.Override(16f);
        raidFog.noiseStrength.Override(0.7f);
        raidFog.wind.Override(new Vector3(0.7f, 0.08f, 0.35f));
        raidFog.clearing.Override(0.45f);
        raidFog.maxDistance.Override(100f);
        raidFog.steps.Override(28);
        SetWar(Get<FogOfWar>(raid));
        Save(raid, log);
        return log.ToString();
    }

    public static string PlayerSight()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var sight = root.GetComponent<VisibilitySource>() ?? root.AddComponent<VisibilitySource>();
            var so = new SerializedObject(sight);
            so.FindProperty("radius").floatValue = 12f;
            so.FindProperty("softness").floatValue = 0.45f;
            so.FindProperty("reveals").boolValue = true;
            so.FindProperty("revealRadius").floatValue = 20f;
            so.FindProperty("followsWard").boolValue = false;
            so.FindProperty("isLight").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        var saved = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab).GetComponent<VisibilitySource>();
        return $"player sight: radius {saved.Radius}, reveals {saved.RevealRadius}, light {saved.IsLight}";
    }

    /// <summary>In the open SafeHaven scene: the Heart Tree's ward sight and the saved exploration map. Saves the scene.</summary>
    public static string HomeScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "SafeHaven")
        {
            return "open SafeHaven first";
        }

        if (scene.isDirty)
        {
            return "SafeHaven has unsaved changes; not touching it";
        }

        HeartTree tree = Object.FindAnyObjectByType<HeartTree>();
        var ward = tree.GetComponent<VisibilitySource>() ?? Undo.AddComponent<VisibilitySource>(tree.gameObject);
        var so = new SerializedObject(ward);
        so.FindProperty("radius").floatValue = 4f;
        so.FindProperty("softness").floatValue = 0.3f;
        so.FindProperty("reveals").boolValue = true;
        so.FindProperty("revealRadius").floatValue = 20f;
        so.FindProperty("followsWard").boolValue = true;
        so.FindProperty("isLight").boolValue = true;
        so.ApplyModifiedProperties();

        GameObject atmosphere = Atmosphere(scene);
        var map = atmosphere.GetComponent<ExplorationMap>() ?? Undo.AddComponent<ExplorationMap>(atmosphere);
        var mapData = new SerializedObject(map);
        mapData.FindProperty("cellSize").floatValue = 1.5f;
        mapData.FindProperty("persist").boolValue = true;
        mapData.ApplyModifiedProperties();
        if (atmosphere.GetComponent<SaveableEntity>() == null)
        {
            Undo.AddComponent<SaveableEntity>(atmosphere);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"ward sight on {tree.name}; exploration map saved with the game; scene saved (dirty={scene.isDirty})";
    }

    /// <summary>In the open raid scene: an exploration map that starts dark every raid. Saves the scene.</summary>
    public static string RaidScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.StartsWith("Raid_"))
        {
            return "open a raid scene first";
        }

        if (scene.isDirty)
        {
            return scene.name + " has unsaved changes; not touching it";
        }

        GameObject atmosphere = Atmosphere(scene);
        var map = atmosphere.GetComponent<ExplorationMap>() ?? Undo.AddComponent<ExplorationMap>(atmosphere);
        var mapData = new SerializedObject(map);
        mapData.FindProperty("cellSize").floatValue = 1f;
        mapData.FindProperty("persist").boolValue = false;
        mapData.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"exploration map in {scene.name}; scene saved (dirty={scene.isDirty})";
    }

    private static GameObject Atmosphere(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "Atmosphere")
            {
                return root;
            }
        }

        var atmosphere = new GameObject("Atmosphere");
        Undo.RegisterCreatedObjectUndo(atmosphere, "Add Atmosphere");
        return atmosphere;
    }

    private static void SetWar(FogOfWar war)
    {
        war.unexplored.Override(1f);
        war.unexploredColor.Override(new Color(0.025f, 0.03f, 0.035f));
        war.nightVeil.Override(0.85f);
        war.veilColor.Override(new Color(0.4f, 0.43f, 0.5f));
        war.veilDesaturation.Override(0.95f);
        war.veilDarkening.Override(0.55f);
        war.veilHaze.Override(0.06f);
    }

    private static VolumeProfile Profile(string name) => AssetDatabase.LoadAssetAtPath<VolumeProfile>(Volumes + name + ".asset");

    private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (profile.TryGet(out T component))
        {
            return component;
        }

        component = profile.Add<T>();
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        return component;
    }

    private static void Save(VolumeProfile profile, System.Text.StringBuilder log)
    {
        foreach (VolumeComponent component in profile.components)
        {
            EditorUtility.SetDirty(component);
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        log.Append(profile.name + ": " + profile.components.Count + " overrides; ");
    }
}
