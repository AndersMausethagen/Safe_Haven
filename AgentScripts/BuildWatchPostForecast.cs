#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;

/// <summary>
/// The Watch Post's upgrades see further: the first names one of a raid's coming events in the briefing, the next two. Safe to run again.
/// Run: unity command script run -- --file AgentScripts/BuildWatchPostForecast.cs --entry BuildWatchPostForecast.Run
/// </summary>
public static class BuildWatchPostForecast
{
    public static string Run()
    {
        var post = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/WatchPost.asset");
        var so = new SerializedObject(post);
        SerializedProperty levels = so.FindProperty("upgrades");
        for (int i = 0; i < levels.arraySize; i++)
        {
            levels.GetArrayElementAtIndex(i).FindPropertyRelative("foretoldEvents").intValue = i + 1;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(post);
        AssetDatabase.SaveAssets();
        return $"{levels.arraySize} Watch Post levels foretell up to {levels.arraySize} events";
    }
}
