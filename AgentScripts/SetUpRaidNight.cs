using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gives raids a time of day (run with `unity command run_script --file AgentScripts/SetUpRaidNight.cs --entry
/// SetUpRaidNight.<Step>`): Data() fills the raids' Time of Day lists and makes a Misty Woods night volume; Scene() adds
/// the sky rig with a RaidSky to the open raid scene and saves it.
/// </summary>
public static class SetUpRaidNight
{
    private const string Volumes = "Assets/_Project/Settings/Rendering/Volumes/";

    public static string Data()
    {
        var log = new System.Text.StringBuilder();
        foreach (string raid in new[] { "MistyWoods", "TrollValley" })
        {
            var definition = AssetDatabase.LoadAssetAtPath<RaidDefinition>($"Assets/_Project/Data/Raids/{raid}.asset");
            var so = new SerializedObject(definition);
            SerializedProperty times = so.FindProperty("timesOfDay");
            times.ClearArray();
            Add(times, "Day", 11f, 55f, 1f, "Mist hangs in the valleys; the forest sees you coming.");
            Add(times, "Dusk", 19.2f, 25f, 1.15f, "Long shadows and failing light. Things wake.");
            Add(times, "Night", 23.5f, 20f, 1.4f, "Grey dark beyond arm's reach. You see little, and so do they; they hear as well as ever.");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            log.Append($"{raid}: {definition.TimesOfDay.Count} times; ");
        }

        string nightPath = Volumes + "MistyWoods_Night.asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(nightPath) == null)
        {
            AssetDatabase.CopyAsset(Volumes + "SafeHaven_Night.asset", nightPath);
            log.Append("created MistyWoods_Night; ");
        }

        var night = AssetDatabase.LoadAssetAtPath<VolumeProfile>(nightPath);
        if (night.TryGet(out AtmosphericFog fog))
        {
            // Thicker mist in the woods at night than at home.
            fog.density.Override(0.05f);
            EditorUtility.SetDirty(fog);
        }

        EditorUtility.SetDirty(night);
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    public static string Scene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.StartsWith("Raid_") || scene.isDirty)
        {
            return $"open a clean raid scene first ({scene.name}, dirty={scene.isDirty})";
        }

        GameObject rig = WeatherSetup.AddToScene(WeatherSetup.EnsureAssets());
        Transform nightVolume = rig.transform.Find("Night Volume");
        if (nightVolume != null)
        {
            nightVolume.GetComponent<Volume>().sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Volumes + "MistyWoods_Night.asset");
        }

        var cycle = rig.GetComponentInChildren<DayNightCycle>();
        var cycleData = new SerializedObject(cycle);
        cycleData.FindProperty("startHour").floatValue = 11f;
        cycleData.FindProperty("clockRuns").boolValue = false;
        cycleData.ApplyModifiedProperties();
        if (rig.GetComponent<RaidSky>() == null)
        {
            Undo.AddComponent<RaidSky>(rig);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Light sun = new SerializedObject(cycle).FindProperty("sun").objectReferenceValue as Light;
        return $"{scene.name}: sky rig with RaidSky, sun={(sun != null ? sun.name : "none")}, night volume=MistyWoods_Night; saved dirty={scene.isDirty}";
    }

    private static void Add(SerializedProperty times, string name, float hour, float chance, float loot, string briefing)
    {
        times.InsertArrayElementAtIndex(times.arraySize);
        SerializedProperty entry = times.GetArrayElementAtIndex(times.arraySize - 1);
        entry.FindPropertyRelative("name").stringValue = name;
        entry.FindPropertyRelative("hour").floatValue = hour;
        entry.FindPropertyRelative("chance").floatValue = chance;
        entry.FindPropertyRelative("lootMultiplier").floatValue = loot;
        entry.FindPropertyRelative("briefing").stringValue = briefing;
    }
}
