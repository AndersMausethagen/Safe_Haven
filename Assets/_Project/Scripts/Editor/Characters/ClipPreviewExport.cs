using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// Plays an animator controller's clips on a rigged model the way the game does (the model's own Animator and avatar,
/// so Humanoid retargeting, muscle limits and foot IK are Unity's) and writes every bone's pose per frame to JSON, for
/// Tools/Blender/preview_unity_clips.py to replay on the same rig in Blender while its weights are tweaked.
/// Works in a preview scene, so the open scene is never touched. Rotations and positions are relative to the model's
/// root; the Blender side fits Unity's rest positions to the armature's to find the axes, so no axis convention is assumed.
/// </summary>
public static class ClipPreviewExport
{
    public const string DefaultController = "Assets/_Project/Animation/Controllers/MonsterHumanoid.controller";
    public const string OutputFolder = "Tools/Blender/ClipPreview";
    private const float Fps = 30f;

    [MenuItem("Safe Haven/Tools/Export Clips For Blender Preview (Selected Model)", false, 400)]
    private static void ExportSelected()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(path) || !(AssetDatabase.LoadMainAssetAtPath(path) is GameObject))
        {
            EditorUtility.DisplayDialog("Export Clips", "Select a rigged model (FBX) or prefab in the Project window.", "OK");
            return;
        }
        Debug.Log(Export(path));
    }

    /// <summary>Exports every clip in <paramref name="controllerPath"/> played on the model at <paramref name="modelPath"/>.</summary>
    public static string Export(string modelPath, string controllerPath = DefaultController)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (model == null) return "No model at " + modelPath;
        if (controller == null) return "No animator controller at " + controllerPath;

        var clips = new List<(string name, AnimationClip clip, bool footIK)>();
        foreach (var layer in controller.layers) CollectClips(layer.stateMachine, clips);

        var scene = EditorSceneManager.NewPreviewScene();
        var instance = Object.Instantiate(model);
        try
        {
            EditorSceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null) return modelPath + " has no Animator (is its rig Generic or Humanoid?)";
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var root = instance.transform;
            var bones = new List<Transform>(root.GetComponentsInChildren<Transform>(true));
            bones.Remove(root);

            var json = new StringBuilder(1 << 20);
            json.Append("{\"model\":").Append(Quote(modelPath)).Append(",\"controller\":").Append(Quote(controllerPath));
            json.Append(",\"fps\":").Append(Num(Fps)).Append(",\"bones\":[");
            for (int i = 0; i < bones.Count; i++) json.Append(i > 0 ? "," : "").Append(Quote(bones[i].name));
            json.Append("],\"rest\":");
            AppendPose(json, root, bones);
            json.Append(",\"clips\":[");

            for (int c = 0; c < clips.Count; c++)
            {
                var (name, clip, footIK) = clips[c];
                var graph = PlayableGraph.Create("ClipPreviewExport");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetApplyFootIK(footIK);
                AnimationPlayableOutput.Create(graph, "Out", animator).SetSourcePlayable(playable);

                int frames = Mathf.Max(1, Mathf.RoundToInt(clip.length * Fps) + 1);
                json.Append(c > 0 ? "," : "").Append("{\"name\":").Append(Quote(name));
                json.Append(",\"clip\":").Append(Quote(clip.name)).Append(",\"loop\":").Append(clip.isLooping ? "true" : "false");
                json.Append(",\"frames\":[");
                for (int f = 0; f < frames; f++)
                {
                    playable.SetTime(Mathf.Min(f / Fps, clip.length));
                    graph.Evaluate();
                    if (f > 0) json.Append(',');
                    AppendPose(json, root, bones);
                }
                json.Append("]}");
                graph.Destroy();
            }
            json.Append("]}");

            Directory.CreateDirectory(OutputFolder);
            string outPath = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(modelPath) + ".json");
            File.WriteAllText(outPath, json.ToString());
            return $"Wrote {clips.Count} clips x {bones.Count} bones to {Path.GetFullPath(outPath)}";
        }
        finally
        {
            Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void CollectClips(AnimatorStateMachine machine, List<(string, AnimationClip, bool)> clips)
    {
        foreach (var child in machine.states)
            CollectMotion(child.state.name, child.state.motion, child.state.iKOnFeet, clips);
        foreach (var sub in machine.stateMachines) CollectClips(sub.stateMachine, clips);
    }

    private static void CollectMotion(string name, Motion motion, bool footIK, List<(string, AnimationClip, bool)> clips)
    {
        if (motion is AnimationClip clip)
        {
            if (!clips.Exists(c => c.Item1 == name)) clips.Add((name, clip, footIK));
        }
        else if (motion is BlendTree tree)
        {
            foreach (var child in tree.children)
                if (child.motion != null) CollectMotion(name + "/" + child.motion.name, child.motion, footIK, clips);
        }
    }

    // One pose: per bone [qx,qy,qz,qw,px,py,pz] relative to the model root, flattened.
    private static void AppendPose(StringBuilder json, Transform root, List<Transform> bones)
    {
        var inverse = Quaternion.Inverse(root.rotation);
        json.Append('[');
        for (int i = 0; i < bones.Count; i++)
        {
            var q = inverse * bones[i].rotation;
            var p = root.InverseTransformPoint(bones[i].position);
            if (i > 0) json.Append(',');
            json.Append(Num(q.x)).Append(',').Append(Num(q.y)).Append(',').Append(Num(q.z)).Append(',').Append(Num(q.w)).Append(',');
            json.Append(Num(p.x)).Append(',').Append(Num(p.y)).Append(',').Append(Num(p.z));
        }
        json.Append(']');
    }

    private static string Num(float v) => v.ToString("0.#####", CultureInfo.InvariantCulture);
    private static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
