#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEngine;

/// <summary>
/// The prologue's last stand at the portal (`unity command run_script --file AgentScripts/BuildPrologueFinale.cs --entry
/// BuildPrologueFinale.Apply`): one minute instead of three, with the expedition fighting beside the player now, so
/// more comes through the dark and not only wolves. Makes expedition copies of the Small Moss Troll and the Mistwing
/// (Data/Prologue, without a SaveableEntity: encounters restart at checkpoints, like the expedition wolf) and sets the
/// hold's length, waves and enemy mix on ForestPrologue. Safe to run again.
/// </summary>
public static class BuildPrologueFinale
{
    private const string Prologue = "Assets/_Project/Data/Prologue/";

    public static string Apply()
    {
        GameObject wolf = AssetDatabase.LoadAssetAtPath<GameObject>(Prologue + "ExpeditionWolf.prefab");
        GameObject troll = Unsaved("Assets/_Project/Prefabs/Enemies/SmallMossTroll.prefab", Prologue + "ExpeditionSmallMossTroll.prefab");
        GameObject mistwing = Unsaved("Assets/_Project/Prefabs/Enemies/Mistwing.prefab", Prologue + "ExpeditionMistwing.prefab");

        var definition = AssetDatabase.LoadAssetAtPath<PrologueDefinition>(Prologue + "ForestPrologue.asset");
        var data = new SerializedObject(definition);
        SerializedProperty chapters = data.FindProperty("chapters");
        SerializedProperty enemies = null;
        for (int i = 0; i < chapters.arraySize; i++)
        {
            SerializedProperty chapter = chapters.GetArrayElementAtIndex(i);
            if (chapter.FindPropertyRelative("behaviour").intValue != (int)PrologueStep.HoldPortal) continue;
            chapter.FindPropertyRelative("duration").floatValue = 60f;
            chapter.FindPropertyRelative("initialEnemies").intValue = 4;
            enemies = chapter.FindPropertyRelative("enemies");
        }

        // Waves at 12, 24, 36 and 48 s of 3-6 enemies, never more than 16 at once.
        data.FindProperty("waveInterval").floatValue = 12f;
        data.FindProperty("firstWaveSize").intValue = 3;
        data.FindProperty("waveGrowth").intValue = 1;
        data.FindProperty("maximumWaveSize").intValue = 6;
        data.FindProperty("maximumEnemies").intValue = 16;

        enemies.ClearArray();
        Enemy(enemies, wolf, 3f, 1);
        Enemy(enemies, troll, 2f, 1);
        Enemy(enemies, mistwing, 1.5f, 2);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssets();
        return $"portal hold: 60 s, enemies {enemies.arraySize}; problem: {definition.FindProblem() ?? "none"}";
    }

    // A prefab variant of a raid monster with its SaveableEntity removed.
    private static GameObject Unsaved(string source, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && existing.GetComponentInChildren<SaveableEntity>(true) == null) return existing;

        var original = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(original);
        try
        {
            foreach (SaveableEntity saveable in instance.GetComponentsInChildren<SaveableEntity>(true))
                Object.DestroyImmediate(saveable);
            instance.name = System.IO.Path.GetFileNameWithoutExtension(path);
            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static void Enemy(SerializedProperty list, GameObject prefab, float weight, int fromWave)
    {
        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("weight").floatValue = weight;
        entry.FindPropertyRelative("fromWave").intValue = fromWave;
    }
}
