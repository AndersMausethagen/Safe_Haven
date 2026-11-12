using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

/// <summary>Drag handles on a Camera Zone's box in the Scene view (its Size), and GameObject › Safe Haven › Camera Zone
/// to place the prefab.</summary>
[CustomEditor(typeof(CameraZone))]
public sealed class CameraZoneEditor : Editor
{
    private const string PrefabPath = "Assets/_Project/Prefabs/Camera/Camera Zone.prefab";
    private readonly BoxBoundsHandle handle = new BoxBoundsHandle();

    private void OnSceneGUI()
    {
        var zone = (CameraZone)target;
        using (new Handles.DrawingScope(new Color(0.4f, 0.8f, 1f), zone.transform.localToWorldMatrix))
        {
            handle.center = Vector3.zero;
            handle.size = zone.Size;
            EditorGUI.BeginChangeCheck();
            handle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(new Object[] { zone, zone.transform }, "Resize Camera Zone");
                // The box grows from the side dragged: the object follows the centre.
                zone.transform.position = zone.transform.TransformPoint(handle.center);
                zone.Size = handle.size;
            }
        }
    }

    [MenuItem("GameObject/Safe Haven/Camera Zone", false, 41)]
    private static void Create(MenuCommand command)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        GameObject zone = prefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(prefab) : new GameObject("Camera Zone", typeof(CameraZone));
        GameObjectUtility.SetParentAndAlign(zone, command.context as GameObject);
        if (command.context == null && SceneView.lastActiveSceneView != null)
        {
            zone.transform.position = SceneView.lastActiveSceneView.pivot;
        }

        Undo.RegisterCreatedObjectUndo(zone, "Create Camera Zone");
        Selection.activeObject = zone;
    }
}
