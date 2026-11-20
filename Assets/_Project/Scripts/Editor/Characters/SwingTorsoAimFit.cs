using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Measures the shoulder facing of every swing the player can walk through (the Upper Body layer's states the Attack Arms layer also
/// has) on the player's own rig, and writes it to the player's SwingTorsoAim. The V Rising chest proxy joint, baked from the clips
/// instead of exported from the rig. Not a builder: it only writes the measured list, and SwingTorsoAimTests fails when it is stale.
/// </summary>
public static class SwingTorsoAimFit
{
    private const float Step = 1f / 20f;
    // Degrees of turn past which a swing counts as a spin.
    public const float MostSwing = 200f;

    // Measured as the game shows it: the player's controller evaluated on a hidden copy of the rig with root motion off, each swing
    // played on the whole-body layer (where it has its own hips). Sampling the clip straight onto the rig kept its root's turn, which
    // the game throws away (a straight punch measured as an 80-degree twist).
    public static SwingTorsoAim.Facing[] Measure()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AttackFlow.ControllerPath);
        AnimatorControllerLayer upper = controller.layers.First(l => l.name == "Upper Body");
        var arms = new HashSet<string>(controller.layers.First(l => l.name == "Attack Arms").stateMachine.states.Select(c => c.state.name));
        var full = controller.layers.First(l => l.name == "Combat Full Body").stateMachine.states.Select(c => c.state).ToDictionary(s => s.name);
        int fullLayer = System.Array.FindIndex(controller.layers, l => l.name == "Combat Full Body");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CombatAnimationSetup.PlayerPrefabPath);
        GameObject rig = CombatAnimationSetup.BareRig(prefab.GetComponentInChildren<Animator>());
        rig.hideFlags = HideFlags.HideAndDontSave;
        var result = new List<SwingTorsoAim.Facing>();
        try
        {
            var animator = rig.GetComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            animator.Update(0f);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            foreach (AnimatorState state in upper.stateMachine.states.Select(c => c.state).Where(s => arms.Contains(s.name)).OrderBy(s => s.name, System.StringComparer.Ordinal))
            {
                if (!(state.motion is AnimationClip clip) || clip.length <= 0f || !full.ContainsKey(state.name))
                {
                    continue;
                }

                int hash = Animator.StringToHash(state.name);
                var keys = new List<float>();
                int steps = Mathf.Max(2, Mathf.CeilToInt(clip.length / Step));
                float last = 0f;
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    animator.Play(hash, fullLayer, Mathf.Min(t, 0.999f));
                    animator.Update(0f);
                    float yaw = SwingTorsoAim.ShoulderYaw(rig.transform, left.position, right.position);
                    // Kept continuous through a full turn (a spin): no jump at +-180.
                    if (i > 0)
                    {
                        yaw = last + Mathf.DeltaAngle(last, yaw);
                    }

                    last = yaw;
                    keys.Add(Mathf.Round(yaw * 10f) / 10f);
                }

                // A spin (the whole body turning further than this) isn't aimed back: walked through, the torso would fight the legs.
                if (keys.Max() - keys.Min() > MostSwing)
                {
                    continue;
                }

                result.Add(new SwingTorsoAim.Facing { state = state.name, yaw = keys.ToArray() });
            }
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }

        return result.ToArray();
    }

    [MenuItem("Safe Haven/Tools/Fit Swing Torso Aim")]
    public static string Fit()
    {
        SwingTorsoAim.Facing[] measured = Measure();
        GameObject root = PrefabUtility.LoadPrefabContents(CombatAnimationSetup.PlayerPrefabPath);
        try
        {
            var aim = root.GetComponentInChildren<SwingTorsoAim>(true);
            if (aim == null)
            {
                aim = root.GetComponentInChildren<Animator>(true).gameObject.AddComponent<SwingTorsoAim>();
            }

            if (Same(aim.Facings, measured))
            {
                return "Fit Swing Torso Aim: current (" + measured.Length + " swings).";
            }

            var data = new SerializedObject(aim);
            SerializedProperty list = data.FindProperty("facings");
            list.arraySize = measured.Length;
            for (int i = 0; i < measured.Length; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("state").stringValue = measured[i].state;
                SerializedProperty yaw = item.FindPropertyRelative("yaw");
                yaw.arraySize = measured[i].yaw.Length;
                for (int k = 0; k < measured[i].yaw.Length; k++)
                {
                    yaw.GetArrayElementAtIndex(k).floatValue = measured[i].yaw[k];
                }
            }

            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CombatAnimationSetup.PlayerPrefabPath);
            return "Fit Swing Torso Aim: " + measured.Length + " swings written.";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Same states and facings, to half a degree at every key.</summary>
    public static bool Same(IReadOnlyList<SwingTorsoAim.Facing> a, IReadOnlyList<SwingTorsoAim.Facing> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].state != b[i].state || a[i].yaw == null || a[i].yaw.Length != b[i].yaw.Length)
            {
                return false;
            }

            for (int k = 0; k < b[i].yaw.Length; k++)
            {
                if (Mathf.Abs(a[i].yaw[k] - b[i].yaw[k]) > 0.5f)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
