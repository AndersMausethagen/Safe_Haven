using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The deep water of a scene with a baked navmesh (the Haven), as NavMesh Modifier Volumes in the Water area under one holder
/// ("Deep Water Marks"), so its bake keeps creatures that don't swim out of it, as a raid's runtime build does (DeepWater). Made again
/// before every rebake (WorldBuilder and Relief call <see cref="Rebuild"/>), as the water may have moved; by hand: Safe Haven ›
/// Navigation › Mark Deep Water, then bake.
/// </summary>
public static class DeepWaterMarks
{
    public const string HolderName = "Deep Water Marks";

    [MenuItem("Safe Haven/Navigation/Mark Deep Water")]
    private static void MarkFromMenu()
    {
        int made = Rebuild();
        Debug.Log($"Deep water: {made} marks made. Rebake the navmesh to use them.");
    }

    /// <summary>Makes the marks of the active scene again (none where there is no deep water); returns how many.</summary>
    public static int Rebuild()
    {
        int area = DeepWater.Area;
        var scene = EditorSceneManager.GetActiveScene();
        GameObject holder = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform found = root.name == HolderName ? root.transform : root.transform.Find(HolderName);
            if (found != null)
            {
                holder = found.gameObject;
                break;
            }
        }

        List<Bounds> boxes = area >= 0 ? DeepWater.Boxes(new Bounds(Vector3.zero, Vector3.one * 100000f)) : new List<Bounds>();
        if (boxes.Count == 0 && holder == null)
        {
            return 0;
        }

        if (holder == null)
        {
            holder = new GameObject(HolderName);
            Undo.RegisterCreatedObjectUndo(holder, "Mark Deep Water");
            NavMeshSurface surface = Object.FindAnyObjectByType<NavMeshSurface>();
            if (surface != null)
            {
                holder.transform.SetParent(surface.transform, false);
            }
        }

        for (int i = holder.transform.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(holder.transform.GetChild(i).gameObject);
        }

        for (int i = 0; i < boxes.Count; i++)
        {
            var mark = new GameObject($"Deep Water {i + 1}");
            mark.transform.SetParent(holder.transform, false);
            mark.transform.position = boxes[i].center;
            var volume = mark.AddComponent<NavMeshModifierVolume>();
            volume.center = Vector3.zero;
            volume.size = boxes[i].size;
            volume.area = area;
            Undo.RegisterCreatedObjectUndo(mark, "Mark Deep Water");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        return boxes.Count;
    }
}
