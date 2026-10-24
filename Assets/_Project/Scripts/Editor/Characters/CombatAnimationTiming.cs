using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds when a swing connects by playing the clip on the player's rig and watching the weapon hand: the moments it
/// moves fastest are the contacts. Attack timings (Hit Delay) are set from these, so they match the animation rather
/// than a guess.
/// </summary>
public static partial class CombatAnimationSetup
{
    public const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
    private const float SampleStep = 1f / 60f;

    /// <summary>Times (seconds into the clip) where the hand's speed peaks, fastest first, at least 0.2 s apart.</summary>
    public static List<float> ContactTimes(AnimationClip clip, HumanBodyBones hand = HumanBodyBones.RightHand, int max = 3)
    {
        var peaks = new List<float>();
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Animator source = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (clip == null || source == null || source.avatar == null)
        {
            return peaks;
        }

        GameObject rig = Object.Instantiate(source.gameObject);
        rig.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var animator = rig.GetComponent<Animator>();
            animator.applyRootMotion = false;
            Transform bone = animator.GetBoneTransform(hand);
            if (bone == null)
            {
                return peaks;
            }

            var speeds = new List<(float time, float speed)>();
            Vector3 previous = Vector3.zero;
            for (float t = 0f; t <= clip.length + 1e-4f; t += SampleStep)
            {
                clip.SampleAnimation(rig, t);
                Vector3 position = rig.transform.InverseTransformPoint(bone.position);
                if (t > 0f)
                {
                    speeds.Add((t, (position - previous).magnitude / SampleStep));
                }

                previous = position;
            }

            speeds.Sort((a, b) => b.speed.CompareTo(a.speed));
            foreach ((float time, float speed) sample in speeds)
            {
                if (peaks.Count >= max)
                {
                    break;
                }

                if (peaks.TrueForAll(p => Mathf.Abs(p - sample.time) >= 0.2f))
                {
                    peaks.Add(sample.time);
                }
            }
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }

        return peaks;
    }
}
