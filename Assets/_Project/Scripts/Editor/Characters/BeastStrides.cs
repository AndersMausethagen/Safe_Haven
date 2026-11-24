using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Beasts' feet kept on the ground (Docs/QualityLevers_2026-11-24.md §3): a wolf's gallop covered 3.4 m/s at playback 1 and it chased at
/// 5.5, with Stride Speed set to the chase speed, so it slid 1.6 times over the ground; foxes, deer and stags the same. This measures each
/// clip of a beast's Locomotion tree (the state played at StrideRate) on its own prefab: the speed a planted foot moves back under the
/// body, which is the ground speed the clip keeps up with. Fitting writes those speeds as the tree's thresholds and as the drivers'
/// Walk Stride Speed (the walk) and Stride Speed (the fastest), so MonsterAnimationDriver.StrideRate plays the blend at 1 up to the
/// gallop's speed and faster beyond it. Bodies with one stride clip (boars, the bog spitter) are left as they are: no clip of theirs
/// keeps up with a charge. BeastStrideTests checks the fit on the shipped assets.
/// </summary>
public static class BeastStrides
{
    private const int Samples = 80;
    // A foot counts as planted within this height of its lowest point in the clip.
    private const float PlantedHeight = 0.04f;

    /// <summary>A beast controller's Locomotion tree, the prefabs that play it and its clips' feet speeds (m/s, by child; idle 0).</summary>
    public sealed class Fit
    {
        public AnimatorController Controller;
        public BlendTree Tree;
        public readonly List<string> Prefabs = new List<string>();
        public float[] Feet;

        public int Walk => Feet.Length >= 3 ? 1 : -1;
        public float Fastest => Feet.Length > 0 ? Feet[Feet.Length - 1] : 0f;
        public bool WalksAndGallops => Feet.Length >= 3;
    }

    /// <summary>Every beast whose controller has StrideRate, measured on the first prefab that plays it.</summary>
    public static List<Fit> MeasureAll()
    {
        var fits = new Dictionary<AnimatorController, Fit>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            MonsterAnimationDriver driver = prefab != null ? prefab.GetComponentInChildren<MonsterAnimationDriver>(true) : null;
            Animator animator = prefab != null ? prefab.GetComponentInChildren<Animator>(true) : null;
            AnimatorController controller = animator != null ? ControllerOf(animator.runtimeAnimatorController) : null;
            BlendTree tree = controller != null ? StrideTree(controller) : null;
            if (driver == null || tree == null)
            {
                continue;
            }

            if (!fits.TryGetValue(controller, out Fit fit))
            {
                fit = new Fit { Controller = controller, Tree = tree, Feet = Measure(path, tree) };
                fits[controller] = fit;
            }

            fit.Prefabs.Add(path);
        }

        return fits.Values.ToList();
    }

    /// <summary>Writes the measured speeds: the tree's thresholds and every driver's stride speeds, for each beast that walks and
    /// gallops. Returns what it did, a line a beast.</summary>
    [MenuItem("Safe Haven/Tools/Fit Beast Strides")]
    public static string FitAll()
    {
        var report = new StringBuilder();
        foreach (Fit fit in MeasureAll())
        {
            if (!fit.WalksAndGallops)
            {
                report.AppendLine($"{fit.Controller.name}: one stride clip, left as it is");
                continue;
            }

            ChildMotion[] children = fit.Tree.children;
            for (int i = 1; i < children.Length; i++)
            {
                children[i].threshold = Mathf.Round(fit.Feet[i] * 100f) / 100f;
            }

            fit.Tree.useAutomaticThresholds = false;
            fit.Tree.children = children;
            EditorUtility.SetDirty(fit.Tree);
            EditorUtility.SetDirty(fit.Controller);

            // The driver where it is authored: a nested wolf in a pack takes its base prefab's numbers, with no overrides made.
            var sources = new HashSet<MonsterAnimationDriver>();
            foreach (string path in fit.Prefabs)
            {
                foreach (MonsterAnimationDriver driver in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<MonsterAnimationDriver>(true))
                {
                    MonsterAnimationDriver source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(driver);
                    sources.Add(source != null ? source : driver);
                }
            }

            foreach (MonsterAnimationDriver driver in sources)
            {
                var so = new SerializedObject(driver);
                so.FindProperty("strideSpeed").floatValue = Mathf.Round(fit.Fastest * 100f) / 100f;
                so.FindProperty("walkStrideSpeed").floatValue = Mathf.Round(fit.Feet[fit.Walk] * 100f) / 100f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(driver);
            }

            report.AppendLine($"{fit.Controller.name}: feet {string.Join(" / ", fit.Feet.Select(f => f.ToString("0.00")))} m/s; {sources.Count} driver(s), {fit.Prefabs.Count} prefab(s)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[BeastStrides]\n" + report);
        return report.ToString();
    }

    /// <summary>The state played at StrideRate, if its motion is a blend tree.</summary>
    public static BlendTree StrideTree(AnimatorController controller)
    {
        if (!controller.parameters.Any(p => p.name == "StrideRate"))
        {
            return null;
        }

        foreach (AnimatorControllerLayer layer in controller.layers)
        {
            foreach (ChildAnimatorState state in layer.stateMachine.states)
            {
                if (state.state.speedParameterActive && state.state.speedParameter == "StrideRate" && state.state.motion is BlendTree tree)
                {
                    return tree;
                }
            }
        }

        return null;
    }

    private static AnimatorController ControllerOf(RuntimeAnimatorController controller) =>
        controller as AnimatorController ?? (controller as AnimatorOverrideController)?.runtimeAnimatorController as AnimatorController;

    // Each child's feet speed on the prefab's own body (its import scale included); the first child, the idle, is 0.
    private static float[] Measure(string prefabPath, BlendTree tree)
    {
        ChildMotion[] children = tree.children;
        var feet = new float[children.Length];
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            Animator animator = root.GetComponentInChildren<Animator>(true);
            List<Transform> ends = Feet(animator);
            for (int i = 1; i < children.Length; i++)
            {
                feet[i] = children[i].motion is AnimationClip clip ? FeetSpeed(animator, ends, clip) : 0f;
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return feet;
    }

    /// <summary>The feet: the legs' IK foot targets where the rig has them (FF, FFB), else the lowest leaf bones.</summary>
    public static List<Transform> Feet(Animator animator)
    {
        Transform body = animator.transform;
        List<Transform> bones = animator.GetComponentsInChildren<Transform>(true).Where(t => t != body && t.GetComponent<Renderer>() == null).ToList();
        List<Transform> feet = bones.Where(t => t.name == "FF.L" || t.name == "FF.R" || t.name == "FFB.L" || t.name == "FFB.R").ToList();
        if (feet.Count >= 2)
        {
            return feet;
        }

        float lowest = bones.Min(t => t.position.y);
        return bones.Where(t => t.childCount == 0 && t.position.y < lowest + 0.08f).ToList();
    }

    /// <summary>How fast a planted foot moves back under the body over <paramref name="clip"/> at playback 1 (the 75th percentile of
    /// planted samples: touchdown and lift-off are slower and would pull a median down).</summary>
    public static float FeetSpeed(Animator animator, List<Transform> feet, AnimationClip clip)
    {
        Transform body = animator.transform;
        float dt = clip.length / Samples;
        var lowest = new Dictionary<Transform, float>();
        for (int i = 0; i <= Samples; i++)
        {
            clip.SampleAnimation(animator.gameObject, i * dt);
            foreach (Transform foot in feet)
            {
                lowest[foot] = lowest.TryGetValue(foot, out float low) ? Mathf.Min(low, foot.position.y) : foot.position.y;
            }
        }

        var speeds = new List<float>();
        var previous = new Dictionary<Transform, float>();
        for (int i = 0; i <= Samples; i++)
        {
            clip.SampleAnimation(animator.gameObject, i * dt);
            foreach (Transform foot in feet)
            {
                float along = Vector3.Dot(foot.position - body.position, body.forward);
                if (previous.TryGetValue(foot, out float was))
                {
                    float back = -(along - was) / dt;
                    if (back > 0f && foot.position.y < lowest[foot] + PlantedHeight)
                    {
                        speeds.Add(back);
                    }
                }

                previous[foot] = along;
            }
        }

        if (speeds.Count == 0)
        {
            return 0f;
        }

        speeds.Sort();
        return speeds[speeds.Count * 3 / 4];
    }
}
