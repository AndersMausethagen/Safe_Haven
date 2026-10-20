using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tools for saveable objects: gives prefabs their prefab id and scene objects a unique save id, and keeps the
/// content database's list of spawnable prefabs current. Runs from Safe Haven > Save > Assign Missing Save Ids,
/// whenever a prefab with a SaveableEntity is imported, and for every scene right before it is saved.
/// </summary>
public sealed class SaveableEntityTools : AssetPostprocessor
{
    [InitializeOnLoadMethod]
    private static void WatchSceneSaves()
    {
        EditorSceneManager.sceneSaving -= OnSceneSaving;
        EditorSceneManager.sceneSaving += OnSceneSaving;
    }

    // A saved scene always has complete, unique ids, so a designer never has to remember the menu item.
    private static void OnSceneSaving(Scene scene, string path)
    {
        if (!EditorSceneManager.IsPreviewScene(scene))
        {
            AssignSceneIds(scene);
        }
    }

    [MenuItem("Safe Haven/Save/Assign Missing Save Ids")]
    public static void AssignMissingIds()
    {
        int prefabs = RefreshPrefabs();
        int sceneObjects = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            sceneObjects += AssignSceneIds(SceneManager.GetSceneAt(i));
        }

        Debug.Log($"Save ids: {prefabs} prefab(s) updated, {sceneObjects} scene object(s) given an id in the open scenes. Save the scenes to keep them.");
    }

    /// <summary>
    /// Gives every saveable object in the scene an id, and a new one to objects sharing an id or only inheriting it from
    /// their prefab. Returns how many changed.
    /// </summary>
    public static int AssignSceneIds(Scene scene)
    {
        var seen = new HashSet<string>();
        int changed = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (SaveableEntity entity in root.GetComponentsInChildren<SaveableEntity>(true))
            {
                if (string.IsNullOrEmpty(entity.Id) || !seen.Add(entity.Id) || InheritsIdFromPrefab(entity))
                {
                    Undo.RecordObject(entity, "Assign Save Id");
                    entity.AssignNewId();
                    seen.Add(entity.Id);
                    changed++;
                }
            }
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        return changed;
    }

    // An object turned into a prefab passes its id to the new prefab asset, where RefreshPrefabId later clears it. A copy
    // still reading the id from the prefab would then lose it, so it gets its own id stored on the copy.
    private static bool InheritsIdFromPrefab(SaveableEntity entity)
    {
        SaveableEntity source = PrefabUtility.GetCorrespondingObjectFromSource(entity);
        return source != null && source.Id == entity.Id;
    }

    private static int RefreshPrefabs()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { ContentUtility.SearchRoot }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab != null && prefab.TryGetComponent(out SaveableEntity entity) && entity.RefreshPrefabId())
            {
                changed++;
            }
        }

        if (changed > 0)
        {
            AssetDatabase.SaveAssets();
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            database?.CollectAll();
            AssetDatabase.SaveAssetIfDirty(database);
        }

        return changed;
    }

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        foreach (string path in imported)
        {
            if (path.EndsWith(".prefab") && AssetDatabase.LoadAssetAtPath<SaveableEntity>(path) != null)
            {
                // Assets can't be changed during the import callback, so the refresh runs right after it.
                EditorApplication.delayCall -= DelayedRefresh;
                EditorApplication.delayCall += DelayedRefresh;
                return;
            }
        }
    }

    private static void DelayedRefresh()
    {
        RefreshPrefabs();
    }
}
