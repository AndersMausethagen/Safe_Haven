using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// "Stick to Ground" (World Tools overlay / Place tab): while it is on, prefab objects dragged into the Scene view from
/// the Project window or duplicated settle on the ground under them, and objects moved with the Move tool settle when
/// the mouse is let go. They are set down by the bottom of their meshes (not their pivot), sunk a little and tilted with
/// the slope if Align to Slope is on. The settling joins the move's undo step.
/// Left alone: cameras, lights, volumes, UI, terrains, whole maps (anything wider than 40 m), objects with nothing to
/// see, and what the World Builder, Scatter Brush or Make Playable place themselves.
/// </summary>
[InitializeOnLoad]
public static class GroundStick
{
    private static readonly Dictionary<Transform, Vector3> startPositions = new Dictionary<Transform, Vector3>();
    private static int startGroup = -1;

    private static double suspendedUntil;

    /// <summary>For tools that place objects themselves (they are already where they should be). Object changes are
    /// reported at the end of the frame, so the pause lasts a moment beyond the call.</summary>
    public static void Suspend(double seconds = 0.5) =>
        suspendedUntil = System.Math.Max(suspendedUntil, EditorApplication.timeSinceStartup + seconds);

    private static bool Suspended => EditorApplication.timeSinceStartup < suspendedUntil;

    static GroundStick()
    {
        SceneView.duringSceneGui += OnSceneGui;
        ObjectChangeEvents.changesPublished += OnChanges;
    }

    /// <summary>Whether an object is the kind that stands on the ground (a prop, rock, tree, node or building).</summary>
    public static bool IsGroundObject(GameObject go)
    {
        if (go == null || EditorUtility.IsPersistent(go) || !go.activeInHierarchy || (go.hideFlags & HideFlags.DontSave) != 0)
        {
            return false;
        }

        if (go.GetComponentInChildren<Terrain>(true) != null || go.GetComponentInChildren<Camera>(true) != null ||
            go.GetComponentInChildren<Light>(true) != null || go.GetComponentInChildren<Volume>(true) != null ||
            go.GetComponentInChildren<Canvas>(true) != null || go.GetComponentInChildren<WorldFeature>(true) != null ||
            go.GetComponentInParent<WorldFeature>(true) != null || go.GetComponentInChildren<RaidMap>(true) != null)
        {
            return false;
        }

        Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return false;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        return bounds.size.x < 40f && bounds.size.z < 40f;
    }

    private static void OnSceneGui(SceneView view)
    {
        if (!PlacementSettings.StickToGround || EditorApplication.isPlaying)
        {
            return;
        }

        Event e = Event.current;
        if (e.rawType == EventType.MouseDown && e.button == 0)
        {
            startPositions.Clear();
            foreach (Transform t in Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable))
            {
                startPositions[t] = t.position;
            }

            startGroup = Undo.GetCurrentGroup();
        }
        else if (e.rawType == EventType.MouseUp && e.button == 0 && startPositions.Count > 0)
        {
            bool settled = false;
            foreach (KeyValuePair<Transform, Vector3> start in startPositions)
            {
                Transform t = start.Key;
                if (t == null)
                {
                    continue;
                }

                Vector3 moved = t.position - start.Value;
                // Only a sideways move: lifting or lowering an object by hand is left as it is.
                if (new Vector2(moved.x, moved.z).sqrMagnitude > 1e-6f && IsGroundObject(t.gameObject))
                {
                    settled |= GroundPlacement.Drop(t, PlacementSettings.AlignToSlope, PlacementSettings.Sink);
                }
            }

            if (settled && startGroup >= 0)
            {
                Undo.CollapseUndoOperations(startGroup);
            }

            startPositions.Clear();
        }
    }

    private static void OnChanges(ref ObjectChangeEventStream stream)
    {
        if (!PlacementSettings.StickToGround || Suspended || EditorApplication.isPlayingOrWillChangePlaymode || WorldBuilder.Building)
        {
            return;
        }

        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) != ObjectChangeKind.CreateGameObjectHierarchy)
            {
                continue;
            }

            stream.GetCreateGameObjectHierarchyEvent(i, out CreateGameObjectHierarchyEventArgs data);
            var go = EditorUtility.EntityIdToObject(data.entityId) as GameObject;
            // Prefab objects only (what comes from the Project window or a duplicate), and not the Scatter Brush's:
            // it sets its own down.
            if (go == null || !PrefabUtility.IsOutermostPrefabInstanceRoot(go) || go.transform.root.name == ScatterBrush.RootName)
            {
                continue;
            }

            if (IsGroundObject(go))
            {
                GroundPlacement.Drop(go.transform, PlacementSettings.AlignToSlope, PlacementSettings.Sink);
            }
        }
    }
}
