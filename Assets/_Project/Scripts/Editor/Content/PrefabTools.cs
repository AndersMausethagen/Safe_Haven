using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Prefab panels shared by the Content Editor: a preview picture, placing a prefab in the open scene and editing the
/// prefab's components directly.
/// </summary>
public sealed class PrefabTools
{
    private readonly Dictionary<Component, Editor> editors = new Dictionary<Component, Editor>();
    private readonly HashSet<Component> openComponents = new HashSet<Component>();
    private readonly Action repaint;
    private bool showComponents;

    public PrefabTools(Action repaint)
    {
        this.repaint = repaint;
    }

    public void DrawPreview(GameObject prefab, float size)
    {
        Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.25f));
        Texture preview = AssetPreview.GetAssetPreview(prefab);
        if (preview == null)
        {
            if (AssetPreview.IsLoadingAssetPreview(prefab.GetEntityId()))
            {
                repaint();
            }

            preview = AssetPreview.GetMiniThumbnail(prefab);
        }

        if (preview != null)
        {
            GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
        }
    }

    /// <summary>Puts a copy of the prefab in the open scene where the Scene view is looking, ready to be moved.</summary>
    public static void PlaceInScene(GameObject prefab)
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Stop playing first", "Objects placed while playing are lost when play mode ends.", "OK");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        Vector3 position = Vector3.zero;
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            position = view.pivot;
            if (Physics.Raycast(position + Vector3.up * 100f, Vector3.down, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore))
            {
                position = hit.point;
            }
        }

        instance.transform.position = position;
        GroundPlacement.Drop(instance.transform, false); // by the bottom of its meshes, not its pivot
        Undo.RegisterCreatedObjectUndo(instance, "Place " + prefab.name);
        Selection.activeGameObject = instance;
        if (view != null)
        {
            view.FrameSelected();
        }
    }

    /// <summary>The prefab's own components with their normal inspectors, for settings the data doesn't cover.</summary>
    public void DrawComponents(GameObject prefab, string note)
    {
        showComponents = EditorGUILayout.Foldout(showComponents, "Prefab components (advanced)", true);
        if (!showComponents)
        {
            return;
        }

        EditorGUILayout.HelpBox(note, MessageType.None);
        var components = new List<Component>();
        foreach (Component component in prefab.GetComponents<Component>())
        {
            if (component != null && !(component is Transform))
            {
                components.Add(component);
            }
        }

        foreach (WorldHealthBar bar in prefab.GetComponentsInChildren<WorldHealthBar>(true))
        {
            components.Add(bar);
        }

        foreach (Component component in components)
        {
            bool open = openComponents.Contains(component);
            bool now = EditorGUILayout.InspectorTitlebar(open, component);
            if (now != open)
            {
                if (now)
                {
                    openComponents.Add(component);
                }
                else
                {
                    openComponents.Remove(component);
                }
            }

            if (!now)
            {
                continue;
            }

            editors.TryGetValue(component, out Editor editor);
            Editor.CreateCachedEditor(component, null, ref editor);
            editors[component] = editor;

            EditorGUI.BeginChangeCheck();
            editor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(component);
                PrefabUtility.SavePrefabAsset(prefab);
            }
        }
    }

    public void Dispose()
    {
        foreach (Editor editor in editors.Values)
        {
            if (editor != null)
            {
                Object.DestroyImmediate(editor);
            }
        }

        editors.Clear();
        openComponents.Clear();
    }
}
