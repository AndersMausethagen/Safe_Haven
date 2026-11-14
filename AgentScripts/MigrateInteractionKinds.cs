#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// One-time migration to InteractionKind (`unity command run_script --file AgentScripts/MigrateInteractionKinds.cs
/// --entry MigrateInteractionKinds.Apply`): every Interactable that talks, opens something or lights an exit becomes a
/// Use (done on the press) instead of a 0.2-0.4 s piece of fake work, in all prefabs and scenes. Safe to run again.
/// Reopens the scene that was open.
/// </summary>
public static class MigrateInteractionKinds
{
    // Components whose interaction is a press, not work.
    private static readonly Type[] UseOwners =
    {
        typeof(QuestGiver), typeof(PrologueCompanion), typeof(ResidentNpc), typeof(HomePortal), typeof(CraftingStation),
        typeof(StashChest), typeof(RaidPortal), typeof(PurificationAltar), typeof(RepairBench), typeof(ExtractionPoint),
    };

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";

        int prefabs = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Needs(AssetDatabase.LoadAssetAtPath<GameObject>(path))) continue;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (Mark(contents) > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    prefabs++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        var scenes = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(path);
            int marked = 0;
            foreach (GameObject root in scene.GetRootGameObjects()) marked += Mark(root);
            if (marked > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                scenes.Add($"{scene.name} ({marked})");
            }
        }

        if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
        return $"prefabs: {prefabs}; scenes: {string.Join(", ", scenes)}";
    }

    private static bool Needs(GameObject root)
    {
        if (root == null) return false;
        foreach (Interactable interactable in root.GetComponentsInChildren<Interactable>(true))
            if (IsUse(interactable) && interactable.Kind != InteractionKind.Use) return true;
        return false;
    }

    private static int Mark(GameObject root)
    {
        int marked = 0;
        foreach (Interactable interactable in root.GetComponentsInChildren<Interactable>(true))
        {
            if (!IsUse(interactable) || interactable.Kind == InteractionKind.Use) continue;
            var data = new SerializedObject(interactable);
            data.FindProperty("kind").intValue = (int)InteractionKind.Use;
            data.ApplyModifiedPropertiesWithoutUndo();
            marked++;
        }

        return marked;
    }

    private static bool IsUse(Interactable interactable)
    {
        foreach (Type owner in UseOwners)
            if (interactable.GetComponent(owner) != null) return true;
        return false;
    }
}
