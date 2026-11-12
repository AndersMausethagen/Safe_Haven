using UnityEditor;
using UnityEngine;

/// <summary>
/// The In-Game Cinematic's inspector: its shots as usual, and for each a button to look through its start and end in the
/// Scene view (where the camera stands, what it sees, at the shot's field of view) so a shot can be framed without
/// playing. In Play mode it can be played from here. GameObject › Safe Haven › In-Game Cinematic adds one.
/// </summary>
[CustomEditor(typeof(InGameCinematic))]
public sealed class InGameCinematicEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var cinematic = (InGameCinematic)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Look through a shot", EditorStyles.boldLabel);
        for (int i = 0; i < cinematic.Shots.Count; i++)
        {
            CameraShot shot = cinematic.Shots[i];
            if (shot == null)
            {
                continue;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{i + 1}. {shot.name} ({shot.seconds:0.#} s)", GUILayout.MinWidth(120f));
                if (GUILayout.Button("Start", GUILayout.Width(60f))) LookThrough(shot, 0f);
                if (GUILayout.Button("End", GUILayout.Width(60f))) LookThrough(shot, 1f);
            }
        }

        EditorGUILayout.LabelField($"{cinematic.Seconds:0.#} s of shots, then the glide back.", EditorStyles.miniLabel);
        foreach (string problem in cinematic.FindProblems())
        {
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button(Application.isPlaying ? "Play it now" : "Play it now (in Play mode)"))
            {
                cinematic.Play();
            }
        }
    }

    // The Scene view moved to the shot's camera; an empty subject stands for the player, who isn't there out of Play.
    private static void LookThrough(CameraShot shot, float progress)
    {
        Transform subject = shot.subject != null ? shot.subject : Player.Current != null ? Player.Current.transform : null;
        if (subject == null)
        {
            Debug.LogWarning("The shot has no subject to look from (an empty subject is the player, only there in Play mode).");
            return;
        }

        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
        {
            return;
        }

        CameraShot.Anchor at = CameraShot.Anchor.Of(subject);
        CameraShot.Anchor looked = shot.lookAt != null ? CameraShot.Anchor.Of(shot.lookAt) : at;
        CameraShot.Pose pose = shot.Evaluate(at, looked, progress);
        view.cameraSettings.fieldOfView = pose.FieldOfView;
        view.orthographic = false;
        // The Scene view orbits a pivot at its size's distance ahead: put the pivot ahead of the camera.
        const float distance = 5f;
        view.LookAtDirect(pose.Position + pose.Rotation * Vector3.forward * distance, pose.Rotation, distance * Mathf.Sin(pose.FieldOfView * 0.5f * Mathf.Deg2Rad));
        view.Repaint();
    }

    [MenuItem("GameObject/Safe Haven/In-Game Cinematic", false, 40)]
    private static void Create(MenuCommand command)
    {
        var go = new GameObject("In-Game Cinematic");
        GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
        go.AddComponent<InGameCinematic>();
        Undo.RegisterCreatedObjectUndo(go, "Create In-Game Cinematic");
        Selection.activeObject = go;
    }
}
