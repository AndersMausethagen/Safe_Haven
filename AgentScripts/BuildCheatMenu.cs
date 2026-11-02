using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The cheat menu's settings (`unity command script run --file AgentScripts/BuildCheatMenu.cs --entry BuildCheatMenu.Apply`): every
/// creature with a prefab, raid, kind of weather and essence, listed in Resources/CheatSettings, as a build can't search for them.
/// Run again after adding one (CheatTests says when). Safe to run again.
/// </summary>
public static class BuildCheatMenu
{
    private const string Path = "Assets/_Project/Resources/CheatSettings.asset";

    public static string Apply()
    {
        var settings = AssetDatabase.LoadAssetAtPath<CheatSettings>(Path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<CheatSettings>();
            AssetDatabase.CreateAsset(settings, Path);
        }

        var so = new SerializedObject(settings);
        Fill(so.FindProperty("creatures"), All<MonsterDefinition>().Where(monster => monster.Prefab != null).OrderBy(monster => monster.DisplayName).Cast<Object>().ToArray());
        Fill(so.FindProperty("raids"), All<RaidDefinition>().Cast<Object>().ToArray());
        Fill(so.FindProperty("weathers"), All<WeatherDefinition>().OrderBy(weather => weather.name).Cast<Object>().ToArray());
        Fill(so.FindProperty("essences"), All<ResourceDefinition>().OrderBy(resource => resource.DisplayName).Cast<Object>().ToArray());
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return $"{settings.Creatures.Count} creatures, {settings.Raids.Count} raids, {settings.Weathers.Count} weathers, {settings.Essences.Count} essences";
    }

    private static System.Collections.Generic.IEnumerable<T> All<T>() where T : Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));

    private static void Fill(SerializedProperty list, Object[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
