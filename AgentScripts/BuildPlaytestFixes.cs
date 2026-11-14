#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// The asset half of a playtest's fixes (`unity command script run --file AgentScripts/BuildPlaytestFixes.cs --entry
/// BuildPlaytestFixes.Apply`): a quiet interaction prompt, a calm gathering bar, the Misty Woods' own weather (always mist), their
/// cold look, the action menu their events open, and normals for the meshes missing some. Safe to run again.
/// </summary>
public static class BuildPlaytestFixes
{
    private const string Raid = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";
    private const string Volumes = "Assets/_Project/Settings/Rendering/Volumes/";
    private const string Environment = "Assets/_Project/Data/Environment/";

    public static string Apply()
    {
        string normals = RepairNormals();
        QuietPrompt();
        CalmGathering();
        ClimateDefinition climate = MistyClimate();
        ColdLook();
        Object dayNight = ColdDayNight();
        WireRaidScene(climate, dayNight);
        AssetDatabase.SaveAssets();
        return normals;
    }

    /// <summary>Only the look's assets, for tuning it while a raid is running.</summary>
    public static string Look()
    {
        ColdLook();
        ColdDayNight();
        AssetDatabase.SaveAssets();
        return "look applied";
    }

    // ----- Meshes -----

    private static string RepairNormals()
    {
        int repaired = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/_Project/Art" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null || !mesh.isReadable || !path.EndsWith(".asset"))
            {
                continue;
            }

            Vector3[] normals = mesh.normals;
            if (normals.All(normal => normal.sqrMagnitude > 0.5f))
            {
                continue;
            }

            Vector3[] vertices = mesh.vertices;
            for (int i = 0; i < normals.Length; i++)
            {
                if (normals[i].sqrMagnitude < 0.5f)
                {
                    normals[i] = vertices[i].normalized;
                    repaired++;
                }
            }

            mesh.normals = normals;
            EditorUtility.SetDirty(mesh);
        }

        return $"{repaired} normals repaired";
    }

    // ----- The prompt: words on the world, not a window -----

    private static void QuietPrompt()
    {
        const string path = "Assets/_Project/Prefabs/UI/InteractionIndicator.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            UIStyle style = UIStyle.Current;
            foreach (string part in new[] { "Slate", "Halo", "OrbRim", "Orb", "OrbHighlight" })
            {
                root.transform.Find(part).gameObject.SetActive(false);
            }

            var title = root.transform.Find("TargetName").GetComponent<TMP_Text>();
            var status = root.transform.Find("Readiness").GetComponent<TMP_Text>();
            style.OnWorld(title);
            style.OnWorld(status);
            title.fontSize = 19f;
            title.color = style.Muted;
            title.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            status.fontSize = 22f;
            status.rectTransform.anchoredPosition = new Vector2(0f, -24f);

            var view = new SerializedObject(root.GetComponent<WorldInteractionIndicator>());
            view.FindProperty("unitSize").floatValue = 0.0095f;
            view.FindProperty("readyColor").colorValue = style.Accent;
            view.FindProperty("blockedColor").colorValue = Color.Lerp(style.Danger, style.Text, 0.3f);
            view.FindProperty("noteColor").colorValue = style.Muted;
            view.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ----- Chopping -----

    private static void CalmGathering()
    {
        var settings = new SerializedObject(GatheringMinigameSettings.Current);
        SerializedProperty profiles = settings.FindProperty("profiles");
        for (int i = 0; i < profiles.arraySize; i++)
        {
            SerializedProperty profile = profiles.GetArrayElementAtIndex(i);
            var tool = (ToolType)profile.FindPropertyRelative("tool").intValue;
            profile.FindPropertyRelative("riseSeconds").floatValue = tool == ToolType.Pickaxe ? 2.4f : tool == ToolType.Sickle ? 1.5f : 2.2f;
            profile.FindPropertyRelative("normalZoneWidth").floatValue = 0.4f;
            profile.FindPropertyRelative("criticalZoneWidth").floatValue = tool == ToolType.Pickaxe ? 0.13f : tool == ToolType.Sickle ? 0.18f : 0.14f;
        }

        settings.FindProperty("sweetSpot").floatValue = 0.8f;
        settings.FindProperty("zoneJitter").floatValue = 0f;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(GatheringMinigameSettings.Current);
    }

    // ----- Weather: the woods are never without their mist -----

    private static ClimateDefinition MistyClimate()
    {
        WeatherDefinition mist = Weather("Mist");
        WeatherDefinition breezy = Weather("Breezy + Mist");
        WeatherDefinition storm = Weather("Storm + Mist");
        WeatherDefinition rain = RainAndMist();

        string path = Environment + "Misty Woods Climate.asset";
        if (AssetDatabase.LoadAssetAtPath<ClimateDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(Environment + "Safe Haven Climate.asset", path);
        }

        var climate = AssetDatabase.LoadAssetAtPath<ClimateDefinition>(path);
        climate.Configure(mist, climate.TransitionSeconds,
            new ClimateDefinition.Entry { weather = mist, chance = 5f },
            new ClimateDefinition.Entry { weather = breezy, chance = 3f },
            new ClimateDefinition.Entry { weather = rain, chance = 1.5f },
            new ClimateDefinition.Entry { weather = storm, chance = 0.5f });
        EditorUtility.SetDirty(climate);
        return climate;
    }

    private static WeatherDefinition Weather(string name) => AssetDatabase.LoadAssetAtPath<WeatherDefinition>(Environment + "Weather/" + name + ".asset");

    private static WeatherDefinition RainAndMist()
    {
        string path = Environment + "Weather/Rain + Mist.asset";
        if (AssetDatabase.LoadAssetAtPath<WeatherDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(Environment + "Weather/Rain.asset", path);
        }

        var weather = AssetDatabase.LoadAssetAtPath<WeatherDefinition>(path);
        var data = new SerializedObject(weather);
        data.FindProperty("mist").floatValue = 1f;
        data.ApplyModifiedPropertiesWithoutUndo();
        return weather;
    }

    // ----- The look: Haven is green and warm; the woods are blue, cold and darker, and still colourful -----

    private static void ColdLook()
    {
        VolumeProfile day = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Volumes + "MistyWoods_Volume.asset");
        Grade(day, 0.3f, 18f, 12f, new Color(0.96f, 0.98f, 1f), -9f, 3f);
        Tones(day, new Vector4(0.84f, 0.92f, 1f, -0.04f), new Vector4(0.97f, 0.99f, 1f, 0f), new Vector4(0.97f, 0.99f, 1f, 0f));
        Split(day, new Color(0.44f, 0.48f, 0.56f), new Color(0.5f, 0.51f, 0.54f));
        Mixer(day);
        Glow(day, 0.9f, 0.55f, new Color(0.76f, 0.88f, 1f));
        Edge(day, new Color(0.004f, 0.012f, 0.04f), 0.36f);
        Haze(day, 0.03f, new Color(0.78f, 0.84f, 0.92f), new Color(0.18f, 0.23f, 0.3f), 1.05f, new Color(0.8f, 0.88f, 1f));
        Save(day);

        VolumeProfile night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Volumes + "MistyWoods_Night.asset");
        Grade(night, 0.2f, 16f, -6f, new Color(0.95f, 0.97f, 1f), 0f, 2f);
        Haze(night, 0.026f, new Color(0.74f, 0.78f, 0.86f), new Color(0.03f, 0.04f, 0.065f), 0.9f, new Color(0.78f, 0.84f, 1f));
        Get<AtmosphericFog>(night).shaftIntensity.Override(4f);
        Get<AtmosphericFog>(night).shaftDensity.Override(0.006f);
        Save(night);

        // Sunset in these woods doesn't warm up: it goes violet.
        VolumeProfile dusk = Copy("SafeHaven_GoldenHour", "MistyWoods_GoldenHour");
        Grade(dusk, 0.1f, 8f, 14f, new Color(1f, 0.93f, 0.98f), 2f, 6f);
        Split(dusk, new Color(0.4f, 0.44f, 0.58f), new Color(0.58f, 0.52f, 0.56f));
        Glow(dusk, 0.85f, 0.7f, new Color(0.86f, 0.8f, 1f));
        Edge(dusk, new Color(0.02f, 0.01f, 0.05f), 0.32f);
        Save(dusk);

        VolumeProfile overcast = Copy("SafeHaven_Overcast", "MistyWoods_Overcast");
        Grade(overcast, -0.2f, -4f, -16f, new Color(0.82f, 0.9f, 1f), -16f, 3f);
        Save(overcast);
    }

    private static VolumeProfile Copy(string from, string to)
    {
        string path = Volumes + to + ".asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) == null)
        {
            AssetDatabase.CopyAsset(Volumes + from + ".asset", path);
        }

        return AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
    }

    private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet(out T component))
        {
            component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        component.active = true;
        EditorUtility.SetDirty(component);
        return component;
    }

    private static void Grade(VolumeProfile profile, float exposure, float contrast, float saturation, Color filter, float temperature, float tint)
    {
        var colour = Get<ColorAdjustments>(profile);
        colour.postExposure.Override(exposure);
        colour.contrast.Override(contrast);
        colour.saturation.Override(saturation);
        colour.colorFilter.Override(filter);
        var balance = Get<WhiteBalance>(profile);
        balance.temperature.Override(temperature);
        balance.tint.Override(tint);
    }

    private static void Tones(VolumeProfile profile, Vector4 shadows, Vector4 midtones, Vector4 highlights)
    {
        var tones = Get<ShadowsMidtonesHighlights>(profile);
        tones.shadows.Override(shadows);
        tones.midtones.Override(midtones);
        tones.highlights.Override(highlights);
    }

    private static void Split(VolumeProfile profile, Color shadows, Color highlights)
    {
        var split = Get<SplitToning>(profile);
        split.shadows.Override(shadows);
        split.highlights.Override(highlights);
    }

    // Pulls green toward teal and lends blue some of it, rather than only lowering it: the greens stay rich, just not warm.
    private static void Mixer(VolumeProfile profile)
    {
        var mixer = Get<ChannelMixer>(profile);
        mixer.redOutRedIn.Override(96f);
        mixer.redOutGreenIn.Override(0f);
        mixer.redOutBlueIn.Override(4f);
        mixer.greenOutRedIn.Override(-2f);
        mixer.greenOutGreenIn.Override(92f);
        mixer.greenOutBlueIn.Override(8f);
        mixer.blueOutRedIn.Override(0f);
        mixer.blueOutGreenIn.Override(10f);
        mixer.blueOutBlueIn.Override(100f);
    }

    private static void Glow(VolumeProfile profile, float threshold, float intensity, Color tint)
    {
        var bloom = Get<Bloom>(profile);
        bloom.threshold.Override(threshold);
        bloom.intensity.Override(intensity);
        bloom.tint.Override(tint);
    }

    private static void Edge(VolumeProfile profile, Color colour, float intensity)
    {
        var vignette = Get<Vignette>(profile);
        vignette.color.Override(colour);
        vignette.intensity.Override(intensity);
    }

    private static void Haze(VolumeProfile profile, float density, Color albedo, Color ambient, float mainLight, Color shafts)
    {
        var fog = Get<AtmosphericFog>(profile);
        fog.density.Override(density);
        fog.albedo.Override(albedo);
        fog.ambient.Override(ambient);
        fog.mainLight.Override(mainLight);
        fog.shaftColor.Override(shafts);
    }

    private static void Save(VolumeProfile profile) => EditorUtility.SetDirty(profile);

    private static Object ColdDayNight()
    {
        string path = Environment + "Misty Woods Day Night.asset";
        if (AssetDatabase.LoadMainAssetAtPath(path) == null)
        {
            AssetDatabase.CopyAsset(Environment + "Safe Haven Day Night.asset", path);
        }

        Object asset = AssetDatabase.LoadMainAssetAtPath(path);
        Object haven = AssetDatabase.LoadMainAssetAtPath(Environment + "Safe Haven Day Night.asset");
        var data = new SerializedObject(asset);
        var from = new SerializedObject(haven);
        data.FindProperty("sunIntensity").floatValue = from.FindProperty("sunIntensity").floatValue * 0.8f;
        foreach (string gradient in new[] { "sunColour", "ambientSky", "ambientEquator", "ambientGround", "fogColour" })
        {
            data.FindProperty(gradient).gradientValue = Cold(from.FindProperty(gradient).gradientValue, gradient == "sunColour" ? 1f : 0.88f);
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static Gradient Cold(Gradient gradient, float dim)
    {
        GradientColorKey[] keys = gradient.colorKeys;
        for (int i = 0; i < keys.Length; i++)
        {
            Color colour = keys[i].color;
            float light = colour.r * 0.3f + colour.g * 0.59f + colour.b * 0.11f;
            keys[i].color = Color.Lerp(colour, new Color(light * 0.8f, light * 0.92f, light * 1.08f), 0.5f) * dim;
        }

        var cold = new Gradient { mode = gradient.mode };
        cold.SetKeys(keys, gradient.alphaKeys);
        return cold;
    }

    // ----- The raid scene -----

    private static void WireRaidScene(ClimateDefinition climate, Object dayNight)
    {
        var scene = EditorSceneManager.OpenScene(Raid, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            WeatherSystem weather = roots.SelectMany(root => root.GetComponentsInChildren<WeatherSystem>(true)).First();
            Set(weather, "climate", climate);
            Set(weather, "startWeather", climate.Start);
            DayNightCycle cycle = roots.SelectMany(root => root.GetComponentsInChildren<DayNightCycle>(true)).First();
            Set(cycle, "profile", dayNight);
            foreach (Volume volume in roots.SelectMany(root => root.GetComponentsInChildren<Volume>(true)))
            {
                if (volume.name == "Golden Hour Volume")
                {
                    volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Volumes + "MistyWoods_GoldenHour.asset");
                    EditorUtility.SetDirty(volume);
                }
                else if (volume.name == "Overcast Volume")
                {
                    volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Volumes + "MistyWoods_Overcast.asset");
                    EditorUtility.SetDirty(volume);
                }
            }

            if (!roots.Any(root => root.GetComponentInChildren<ActionMenuWindow>(true) != null))
            {
                Transform canvas = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).First(child => child.name == "Screen Canvas");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/ActionMenuWindow.prefab");
                var menu = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas);
                var rect = (RectTransform)menu.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void Set(Object target, string field, Object value)
    {
        var data = new SerializedObject(target);
        data.FindProperty(field).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
