using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Designer tools for the atmosphere (Docs/Atmosphere.md):
/// <list type="bullet">
/// <item>Safe Haven › World › Sky and Weather › Fog › Set Up Open Scene: everything a new map needs (the renderer feature, an
/// "Atmosphere" object with ExplorationMap and FogGround, a global volume whose profile has Atmospheric Fog and Fog of
/// War). Safe to run again: it only adds what is missing and reports what it did.</item>
/// <item>… › Apply Preset › …: a starting look for the fog (Morning Mist, Ground Blanket, Misty Woods, Marsh, Night Mist,
/// Clear) on the selected Volume Profile, or else the open scene's base volume. Undoable.</item>
/// <item>GameObject › Safe Haven › World › Fog Area: a pocket of extra fog, placed in the Scene view (under Map Content in a raid).</item>
/// </list>
/// </summary>
public static class AtmosphereSetup
{
    private const string Menu = "Safe Haven/World/Sky and Weather/Fog/";
    private const string Renderer = "Assets/_Project/Settings/Rendering/PC_Renderer.asset";
    private const string AtmosphereObject = "Atmosphere";

    public enum Preset
    {
        Clear,
        MorningMist,
        GroundBlanket,
        MistyWoods,
        Marsh,
        NightMist,
    }

    [MenuItem(Menu + "Set Up Open Scene", false, 0)]
    private static void SetUpOpenSceneMenu()
    {
        string report = SetUpOpenScene();
        Debug.Log(report);
        EditorUtility.DisplayDialog("Atmosphere", report, "OK");
    }

    [MenuItem(Menu + "Apply Preset/Clear (no fog)", false, 20)] private static void PresetClear() => ApplyToTarget(Preset.Clear);
    [MenuItem(Menu + "Apply Preset/Morning Mist", false, 21)] private static void PresetMorning() => ApplyToTarget(Preset.MorningMist);
    [MenuItem(Menu + "Apply Preset/Ground Blanket", false, 22)] private static void PresetBlanket() => ApplyToTarget(Preset.GroundBlanket);
    [MenuItem(Menu + "Apply Preset/Misty Woods", false, 23)] private static void PresetWoods() => ApplyToTarget(Preset.MistyWoods);
    [MenuItem(Menu + "Apply Preset/Marsh", false, 24)] private static void PresetMarsh() => ApplyToTarget(Preset.Marsh);
    [MenuItem(Menu + "Apply Preset/Night Mist (for a night volume)", false, 25)] private static void PresetNight() => ApplyToTarget(Preset.NightMist);

    [MenuItem("GameObject/Safe Haven/World/Fog Area", false, 30)]
    private static void CreateFogArea(MenuCommand command)
    {
        var area = new GameObject("Fog Area");
        area.AddComponent<FogArea>();
        if (command.context is GameObject parent)
        {
            GameObjectUtility.SetParentAndAlign(area, parent);
        }
        else
        {
            // In a raid, fog areas live under Map Content so they turn with the map.
            GameObject content = GameObject.Find("Raid Map/Map Content");
            if (content != null)
            {
                area.transform.SetParent(content.transform, false);
            }

            if (SceneView.lastActiveSceneView != null)
            {
                Vector3 pivot = SceneView.lastActiveSceneView.pivot;
                area.transform.position = Physics.Raycast(pivot + Vector3.up * 200f, Vector3.down, out RaycastHit hit, 400f, 1)
                    ? hit.point + Vector3.up * 1.5f
                    : pivot;
            }
        }

        Undo.RegisterCreatedObjectUndo(area, "Create Fog Area");
        Selection.activeGameObject = area;
    }

    /// <summary>Adds whatever the open scene lacks for fog, fog of war and night, and says what it did.</summary>
    public static string SetUpOpenScene()
    {
        var report = new StringBuilder();
        var scene = EditorSceneManager.GetActiveScene();
        bool raid = scene.name.StartsWith("Raid_");
        report.AppendLine($"{scene.name} ({(raid ? "raid: exploration starts dark every run" : "home: exploration is saved")})");

        report.AppendLine(EnsureRendererFeature());

        GameObject atmosphere = FindRoot(scene, AtmosphereObject);
        if (atmosphere == null)
        {
            atmosphere = new GameObject(AtmosphereObject);
            Undo.RegisterCreatedObjectUndo(atmosphere, "Set Up Atmosphere");
            report.AppendLine("• Added the \"Atmosphere\" object.");
        }

        if (atmosphere.GetComponent<ExplorationMap>() == null)
        {
            var map = Undo.AddComponent<ExplorationMap>(atmosphere);
            var data = new SerializedObject(map);
            data.FindProperty("persist").boolValue = !raid;
            data.FindProperty("cellSize").floatValue = raid ? 1f : 1.5f;
            data.ApplyModifiedProperties();
            report.AppendLine("• Added an Exploration Map (fog of war memory).");
        }

        if (!raid && atmosphere.GetComponent<SaveableEntity>() == null)
        {
            Undo.AddComponent<SaveableEntity>(atmosphere);
            report.AppendLine("• Added a Saveable Entity, so exploration is saved.");
        }

        if (atmosphere.GetComponent<FogGround>() == null)
        {
            Undo.AddComponent<FogGround>(atmosphere);
            report.AppendLine("• Added Fog Ground (fog that follows the terrain).");
        }

        VolumeProfile profile = BaseProfile(scene, report);
        if (profile != null)
        {
            if (!profile.Has<AtmosphericFog>())
            {
                Apply(Get<AtmosphericFog>(profile), raid ? Preset.MistyWoods : Preset.MorningMist);
                report.AppendLine($"• Added Atmospheric Fog to {profile.name} ({(raid ? "Misty Woods" : "Morning Mist")} preset).");
            }

            if (!profile.Has<FogOfWar>())
            {
                SetFogOfWar(Get<FogOfWar>(profile));
                report.AppendLine($"• Added Fog of War to {profile.name}.");
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        if (Object.FindAnyObjectByType<DayNightCycle>() == null)
        {
            report.AppendLine("• No day/night rig: the night veil stays off here (Safe Haven › Environment › Weather adds one).");
        }

        report.AppendLine("Fog areas: GameObject › Safe Haven › World › Fog Area. Looks: … › Atmosphere › Apply Preset. Save the scene to keep it.");
        EditorSceneManager.MarkSceneDirty(scene);
        return report.ToString();
    }

    /// <summary>Gives a fog override a preset's look. Only the fields a preset decides are overridden.</summary>
    public static void Apply(AtmosphericFog fog, Preset preset)
    {
        Undo.RecordObject(fog, "Apply Fog Preset");
        switch (preset)
        {
            case Preset.Clear:
                fog.density.Override(0f);
                break;
            case Preset.MorningMist:
                Set(fog, 0.012f, 0.2f, 0.6f, 1f, 18f, 0.55f, 160f, 0.4f, 0.4f);
                Light(fog, new Color(0.9f, 0.92f, 0.95f), new Color(0.38f, 0.41f, 0.44f), 1.2f, 0.5f, 1.5f, 0.6f);
                break;
            case Preset.GroundBlanket:
                Set(fog, 0.03f, 0.5f, 0.45f, 1f, 14f, 0.5f, 120f, 0.6f, 0.5f);
                Light(fog, new Color(0.88f, 0.9f, 0.94f), new Color(0.3f, 0.33f, 0.36f), 1.3f, 0.5f, 2f, 0.5f);
                break;
            case Preset.MistyWoods:
                Set(fog, 0.035f, 1.5f, 0.25f, 0.6f, 16f, 0.7f, 140f, 0.55f, 0.5f);
                Light(fog, new Color(0.8f, 0.88f, 0.9f), new Color(0.24f, 0.29f, 0.31f), 1.5f, 0.55f, 2f, 0.45f);
                break;
            case Preset.Marsh:
                Set(fog, 0.05f, 0.3f, 0.6f, 1f, 10f, 0.8f, 80f, 0.7f, 0.3f);
                Light(fog, new Color(0.75f, 0.85f, 0.75f), new Color(0.2f, 0.26f, 0.22f), 1.2f, 0.4f, 2.5f, 0.4f);
                break;
            case Preset.NightMist:
                fog.density.Override(0.03f);
                Light(fog, new Color(0.7f, 0.78f, 0.9f), new Color(0.035f, 0.045f, 0.07f), 2.2f, 0.5f, 2.5f, 0.5f);
                break;
        }

        EditorUtility.SetDirty(fog);
    }

    private static void Set(AtmosphericFog fog, float density, float baseHeight, float falloff, float follow, float noiseSize,
        float noiseStrength, float bankSize, float bankStrength, float bankSpeed)
    {
        fog.density.Override(density);
        fog.baseHeight.Override(baseHeight);
        fog.heightFalloff.Override(falloff);
        fog.followTerrain.Override(follow);
        fog.noiseSize.Override(noiseSize);
        fog.noiseStrength.Override(noiseStrength);
        fog.bankSize.Override(bankSize);
        fog.bankStrength.Override(bankStrength);
        fog.bankSpeed.Override(bankSpeed);
        fog.weatherInfluence.Override(0.6f);
        if (!fog.wind.overrideState)
        {
            fog.wind.Override(new Vector3(0.6f, 0.05f, 0.3f));
        }
    }

    private static void Light(AtmosphericFog fog, Color albedo, Color ambient, float mainLight, float anisotropy,
        float localLights, float clearing)
    {
        fog.albedo.Override(albedo);
        fog.ambient.Override(ambient);
        fog.mainLight.Override(mainLight);
        fog.anisotropy.Override(anisotropy);
        fog.localLights.Override(localLights);
        fog.clearing.Override(clearing);
    }

    private static void SetFogOfWar(FogOfWar war)
    {
        war.unexplored.Override(1f);
        war.unexploredColor.Override(new Color(0.025f, 0.03f, 0.035f));
        war.nightVeil.Override(0.85f);
        war.veilColor.Override(new Color(0.4f, 0.43f, 0.5f));
        war.veilDesaturation.Override(0.95f);
        war.veilDarkening.Override(0.55f);
        war.veilHaze.Override(0.06f);
    }

    private static void ApplyToTarget(Preset preset)
    {
        VolumeProfile profile = Selection.activeObject as VolumeProfile;
        if (profile == null)
        {
            profile = BaseProfile(EditorSceneManager.GetActiveScene(), null);
        }

        if (profile == null)
        {
            EditorUtility.DisplayDialog("Atmosphere", "Select a Volume Profile, or open a scene with a global Volume.", "OK");
            return;
        }

        Apply(Get<AtmosphericFog>(profile), preset);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Selection.activeObject = profile;
        Debug.Log($"Fog preset {ObjectNames.NicifyVariableName(preset.ToString())} applied to {profile.name}.", profile);
    }

    // The scene's base look: its lowest-priority global volume, created with a profile beside the scene when missing.
    private static VolumeProfile BaseProfile(UnityEngine.SceneManagement.Scene scene, StringBuilder report)
    {
        Volume best = null;
        foreach (Volume volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include))
        {
            if (volume.isGlobal && volume.gameObject.scene == scene && (best == null || volume.priority < best.priority))
            {
                best = volume;
            }
        }

        if (best != null && best.sharedProfile != null)
        {
            return best.sharedProfile;
        }

        if (report == null)
        {
            return null;
        }

        if (best == null)
        {
            var go = new GameObject("Global Volume");
            Undo.RegisterCreatedObjectUndo(go, "Set Up Atmosphere");
            best = go.AddComponent<Volume>();
            best.isGlobal = true;
            report.AppendLine("• Added a Global Volume.");
        }

        string folder = System.IO.Path.GetDirectoryName(scene.path);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{scene.name}_Volume.asset");
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        best.sharedProfile = profile;
        report.AppendLine($"• Created the volume profile {path}.");
        return profile;
    }

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

    private static string EnsureRendererFeature()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer);
        if (data == null)
        {
            return "• PC_Renderer not found: add the Atmosphere renderer feature to the renderer by hand.";
        }

        foreach (ScriptableRendererFeature existing in data.rendererFeatures)
        {
            if (existing is AtmosphereFeature)
            {
                return "• The renderer already draws the atmosphere.";
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
        return "• Added the Atmosphere renderer feature to PC_Renderer.";
    }

    private static GameObject FindRoot(UnityEngine.SceneManagement.Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
            {
                return root;
            }
        }

        return null;
    }
}
