using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Day, night and weather for a scene (see Docs/Weather.md): creates the data assets (Data/Environment: a
/// day/night profile, Clear / Breezy / Mist / Rain / Storm weathers and the Safe Haven climate), the Night, Golden Hour
/// and Overcast volume profiles (Settings/Rendering/Volumes) and the Rain prefab, then adds a "Sky and Weather" rig to
/// the open scene wired to its directional light and Moss Wind. Existing assets keep your tuning; only missing ones are
/// created.</summary>
public static class WeatherSetup
{
    public const string DataFolder = "Assets/_Project/Data/Environment";
    public const string WeatherFolder = DataFolder + "/Weather";
    public const string VolumesFolder = "Assets/_Project/Settings/Rendering/Volumes";
    public const string ArtFolder = "Assets/_Project/Art/Generated/Weather";
    public const string RigName = "Sky and Weather";

    public sealed class Set
    {
        public DayNightProfile DayNight;
        public ClimateDefinition Climate;
        public WeatherDefinition Clear, Breezy, Mist, Rain, Storm;
        public VolumeProfile Night, GoldenHour, Overcast;
        public GameObject RainPrefab;
    }

    [MenuItem("Safe Haven/World/Sky and Weather/Add Day, Night and Weather to Scene")]
    private static void AddMenu()
    {
        GameObject rig = AddToScene(EnsureAssets());
        Selection.activeGameObject = rig;
        Debug.Log("Weather: added a day/night cycle and weather to the scene. Drag Day Night › Start Hour to preview " +
                  "any time, or set Weather › Start Weather to preview a weather. Tune them in " + DataFolder + ".");
    }

    public static Set EnsureAssets()
    {
        Directory.CreateDirectory(WeatherFolder);
        Directory.CreateDirectory(ArtFolder);
        var set = new Set
        {
            DayNight = EnsureAsset<DayNightProfile>(DataFolder + "/Safe Haven Day Night.asset", _ => { }),
            Clear = Weather("Clear", 0.8f, 0f, 0f, 0f, 1f, new Color(0.6f, 0.65f, 0.7f, 0f), 0f, 5f, 9f),
            Breezy = Weather("Breezy", 1.6f, 0.25f, 0f, 0.2f, 0.8f, new Color(0.6f, 0.65f, 0.7f, 0f), 0f, 3f, 6f),
            Mist = Weather("Mist", 0.4f, 0f, 0f, 0.35f, 2.2f, new Color(0.85f, 0.88f, 0.9f, 0.6f), 0f, 2f, 4f),
            Rain = Weather("Rain", 1.5f, 0.2f, 0.7f, 0.8f, 1.8f, new Color(0.55f, 0.6f, 0.65f, 0.5f), 0f, 2f, 5f),
            Storm = Weather("Storm", 2.8f, 0.5f, 1f, 1f, 2.2f, new Color(0.4f, 0.45f, 0.52f, 0.6f), 4f, 1.5f, 3f),
        };
        set.Climate = EnsureAsset<ClimateDefinition>(DataFolder + "/Safe Haven Climate.asset", climate => climate.Configure(set.Clear, 45f,
            Entry(set.Clear, 5f), Entry(set.Breezy, 3f), Entry(set.Mist, 1f), Entry(set.Rain, 1.5f), Entry(set.Storm, 0.5f)));
        set.Night = EnsureProfile("SafeHaven_Night", profile =>
        {
            Add<WhiteBalance>(profile, c => { c.temperature.Override(-18f); c.tint.Override(4f); });
            Add<ColorAdjustments>(profile, c =>
            {
                c.postExposure.Override(0f);
                c.saturation.Override(-25f);
                c.contrast.Override(12f);
                c.colorFilter.Override(new Color(0.9f, 0.95f, 1f));
            });
            Add<ShadowsMidtonesHighlights>(profile, c => c.shadows.Override(new Vector4(0.92f, 0.97f, 1.12f, -0.04f)));
            Add<Vignette>(profile, c => { c.intensity.Override(0.32f); c.color.Override(new Color(0.01f, 0.02f, 0.06f)); });
            Add<Bloom>(profile, c => { c.intensity.Override(0.9f); c.threshold.Override(0.8f); c.scatter.Override(0.72f); });
        });
        set.GoldenHour = EnsureProfile("SafeHaven_GoldenHour", profile =>
        {
            Add<WhiteBalance>(profile, c => { c.temperature.Override(15f); c.tint.Override(-3f); });
            Add<ColorAdjustments>(profile, c =>
            {
                c.postExposure.Override(0.1f);
                c.saturation.Override(10f);
                c.contrast.Override(6f);
            });
            Add<SplitToning>(profile, c =>
            {
                c.shadows.Override(new Color(0.47f, 0.47f, 0.55f));
                c.highlights.Override(new Color(0.6f, 0.54f, 0.46f));
            });
            Add<Bloom>(profile, c => { c.intensity.Override(0.85f); c.threshold.Override(0.85f); c.tint.Override(new Color(1f, 0.88f, 0.75f)); });
            Add<Vignette>(profile, c => { c.intensity.Override(0.28f); c.color.Override(new Color(0.12f, 0.05f, 0.02f)); });
        });
        set.Overcast = EnsureProfile("SafeHaven_Overcast", profile =>
        {
            Add<ColorAdjustments>(profile, c =>
            {
                c.postExposure.Override(-0.12f);
                c.saturation.Override(-28f);
                c.contrast.Override(-10f);
                c.colorFilter.Override(new Color(0.9f, 0.94f, 0.98f));
            });
            Add<WhiteBalance>(profile, c => c.temperature.Override(-12f));
            Add<Vignette>(profile, c => { c.intensity.Override(0.3f); c.color.Override(new Color(0.03f, 0.04f, 0.05f)); });
            Add<Bloom>(profile, c => c.intensity.Override(0.45f));
        });
        set.RainPrefab = EnsureRainPrefab(WaterSetup.EnsureAssets().Spray);
        AssetDatabase.SaveAssets();
        return set;
    }

    /// <summary>Adds (or completes) the "Sky and Weather" rig in the open scene. Undoable.</summary>
    public static GameObject AddToScene(Set set)
    {
        GameObject rig = GameObject.Find(RigName);
        if (rig == null)
        {
            rig = new GameObject(RigName);
            Undo.RegisterCreatedObjectUndo(rig, "Add Day, Night and Weather");
        }

        Volume night = EnsureVolume(rig.transform, "Night Volume", set.Night, 1f);
        Volume golden = EnsureVolume(rig.transform, "Golden Hour Volume", set.GoldenHour, 1f);
        Volume overcast = EnsureVolume(rig.transform, "Overcast Volume", set.Overcast, 2f);

        Transform rainTransform = rig.transform.Find("Rain");
        if (rainTransform == null)
        {
            var rainObject = (GameObject)PrefabUtility.InstantiatePrefab(set.RainPrefab, rig.transform);
            Undo.RegisterCreatedObjectUndo(rainObject, "Add Rain");
            rainTransform = rainObject.transform;
        }

        WeatherSystem weather = EnsureChild<WeatherSystem>(rig.transform, "Weather");
        var weatherObject = new SerializedObject(weather);
        weatherObject.FindProperty("climate").objectReferenceValue = set.Climate;
        weatherObject.FindProperty("wind").objectReferenceValue = Object.FindFirstObjectByType<MossWind>();
        weatherObject.FindProperty("rain").objectReferenceValue = rainTransform.GetComponent<RainEffect>();
        weatherObject.FindProperty("overcastVolume").objectReferenceValue = overcast;
        weatherObject.ApplyModifiedProperties();

        DayNightCycle cycle = EnsureChild<DayNightCycle>(rig.transform, "Day Night");
        var cycleObject = new SerializedObject(cycle);
        cycleObject.FindProperty("profile").objectReferenceValue = set.DayNight;
        cycleObject.FindProperty("sun").objectReferenceValue = SceneSun();
        cycleObject.FindProperty("nightVolume").objectReferenceValue = night;
        cycleObject.FindProperty("goldenHourVolume").objectReferenceValue = golden;
        cycleObject.FindProperty("weather").objectReferenceValue = weather;
        cycleObject.ApplyModifiedProperties();
        return rig;
    }

    private static Light SceneSun()
    {
        if (RenderSettings.sun != null)
        {
            return RenderSettings.sun;
        }

        foreach (Light light in Object.FindObjectsByType<Light>())
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }

        return null;
    }

    private static T EnsureChild<T>(Transform parent, string name) where T : Component
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Add " + name);
            Undo.SetTransformParent(go.transform, parent, "Add " + name);
            child = go.transform;
        }

        T component = child.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(child.gameObject);
    }

    private static Volume EnsureVolume(Transform parent, string name, VolumeProfile profile, float priority)
    {
        Volume volume = EnsureChild<Volume>(parent, name);
        Undo.RecordObject(volume, "Set Up " + name);
        volume.isGlobal = true;
        volume.priority = priority;
        volume.sharedProfile = profile;
        return volume;
    }

    private static ClimateDefinition.Entry Entry(WeatherDefinition weather, float chance) =>
        new ClimateDefinition.Entry { weather = weather, chance = chance };

    private static WeatherDefinition Weather(string name, float wind, float gusts, float rain, float clouds, float fog,
        Color tint, float lightning, float shortest, float longest) =>
        EnsureAsset<WeatherDefinition>($"{WeatherFolder}/{name}.asset",
            weather => weather.Configure(name, wind, gusts, rain, clouds, fog, tint, lightning, shortest, longest));

    private static T EnsureAsset<T>(string path, Action<T> setup) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            setup(asset);
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }

    private static VolumeProfile EnsureProfile(string name, Action<VolumeProfile> fill)
    {
        string path = $"{VolumesFolder}/{name}.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            fill(profile);
            EditorUtility.SetDirty(profile);
        }

        return profile;
    }

    /// <summary>Adds a volume override with only the parameters <paramref name="set"/> overrides, stored in the asset.</summary>
    private static void Add<T>(VolumeProfile profile, Action<T> set) where T : VolumeComponent
    {
        T component = profile.Add<T>();
        component.name = typeof(T).Name;
        set(component);
        AssetDatabase.AddObjectToAsset(component, profile);
    }

    private static GameObject EnsureRainPrefab(Material spray)
    {
        string path = $"{ArtFolder}/Rain.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            return prefab;
        }

        var root = new GameObject("Rain");
        try
        {
            var drops = new GameObject("Drops");
            drops.transform.SetParent(root.transform, false);
            ParticleSystem dropSystem = WaterSetup.AddParticles(drops, spray, loop: true, lifetime: (0.75f, 0.85f), speed: (0f, 0f),
                size: (0.025f, 0.04f), gravity: 0f, alpha: 0.4f, max: 6000, grow: false);
            ParticleSystem.MainModule dropMain = dropSystem.main;
            dropMain.prewarm = false;
            dropMain.startRotation = 0f;
            ParticleSystem.ShapeModule dropShape = dropSystem.shape;
            dropShape.shapeType = ParticleSystemShapeType.Box;
            dropShape.scale = new Vector3(36f, 0.1f, 36f);
            ParticleSystem.VelocityOverLifetimeModule fall = dropSystem.velocityOverLifetime;
            fall.enabled = true;
            fall.space = ParticleSystemSimulationSpace.World;
            fall.x = 0f;
            fall.y = -16f;
            fall.z = 0f;
            var dropRenderer = drops.GetComponent<ParticleSystemRenderer>();
            dropRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            dropRenderer.velocityScale = 0.04f;
            dropRenderer.lengthScale = 1f;

            var splashes = new GameObject("Splashes");
            splashes.transform.SetParent(root.transform, false);
            ParticleSystem splashSystem = WaterSetup.AddParticles(splashes, spray, loop: true, lifetime: (0.25f, 0.4f), speed: (0.5f, 1.2f),
                size: (0.06f, 0.14f), gravity: 0.6f, alpha: 0.5f, max: 600, grow: true);
            ParticleSystem.MainModule splashMain = splashSystem.main;
            splashMain.prewarm = false;
            WaterSetup.Box(splashSystem, new Vector3(36f, 0f, 36f), 0f);

            RainEffect rain = root.AddComponent<RainEffect>();
            var serialized = new SerializedObject(rain);
            serialized.FindProperty("drops").objectReferenceValue = dropSystem;
            serialized.FindProperty("splashes").objectReferenceValue = splashSystem;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
