using UnityEditor;
using UnityEngine;

/// <summary>
/// Resources/NightSettings, and what each hour of the Misty Woods holds and says. Run with
/// unity command run_script --file AgentScripts/BuildNight.cs --entry BuildNight.All
/// </summary>
public static class BuildNight
{
    private const string SettingsPath = "Assets/_Project/Resources/NightSettings.asset";
    private const string Raid = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";

    public static string All()
    {
        var settings = AssetDatabase.LoadAssetAtPath<NightSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<NightSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        var so = new SerializedObject(settings);
        SerializedProperty deep = so.FindProperty("deepProwlers");
        string[] names = { "BlightBoar", "Draug" };
        deep.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            deep.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + names[i] + ".prefab");
        }

        so.ApplyModifiedPropertiesWithoutUndo();

        var raid = new SerializedObject(AssetDatabase.LoadAssetAtPath<RaidDefinition>(Raid));
        SerializedProperty hours = raid.FindProperty("timesOfDay");
        for (int i = 0; i < hours.arraySize; i++)
        {
            SerializedProperty hour = hours.GetArrayElementAtIndex(i);
            switch (hour.FindPropertyRelative("name").stringValue)
            {
                case "Dusk":
                    hour.FindPropertyRelative("enemyDensity").floatValue = 1.15f;
                    break;
                case "Night":
                    hour.FindPropertyRelative("enemyDensity").floatValue = 1.3f;
                    hour.FindPropertyRelative("briefing").stringValue =
                        "Grey dark beyond arm's reach. More of them are out, and stronger; they see no better than you, but they hear as well as ever. They carry twice the Cursed Essence.";
                    break;
            }
        }

        raid.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(raid.targetObject);
        AssetDatabase.SaveAssets();
        return $"deep prowlers {settings.DeepProwler(9, 0f)?.name}, {settings.DeepProwler(9, 0.29f)?.name}";
    }
}
