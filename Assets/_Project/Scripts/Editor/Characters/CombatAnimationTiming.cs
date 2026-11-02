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

    /// <summary>
    /// When a big swing connects: the moment the hand drives hardest forward, down or across while out in front of the body. On
    /// clips with a long wind-up the hand's top speed is often the weapon going up (<see cref="ContactTimes"/> would land the blow
    /// there, with the weapon still over the head), so here rising counts against it and so does being behind the body.
    /// </summary>
    public static float DrivenContact(AnimationClip clip, HumanBodyBones hand = HumanBodyBones.RightHand)
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Animator source = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (clip == null || source == null || source.avatar == null)
        {
            return 0.3f;
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
                return clip.length * 0.5f;
            }

            float best = -1f, bestTime = clip.length * 0.5f;
            Vector3 previous = Vector3.zero;
            for (float t = 0f; t <= clip.length + 1e-4f; t += SampleStep)
            {
                clip.SampleAnimation(rig, t);
                Vector3 position = rig.transform.InverseTransformPoint(bone.position);
                // The first frames snap from the ready pose, and the last are the return to it.
                if (t >= 0.12f && t <= clip.length * 0.8f)
                {
                    Vector3 velocity = (position - previous) / SampleStep;
                    float speed = velocity.magnitude;
                    float notRising = speed > 0.01f ? Mathf.Clamp01(0.6f - velocity.y / speed) : 0f;
                    float inFront = Mathf.Clamp01(0.3f + position.z * 2f);
                    float driving = Mathf.Max(0f, velocity.z) + Mathf.Max(0f, -velocity.y) + Mathf.Abs(velocity.x) * 0.7f;
                    float score = driving * notRising * inFront;
                    if (score > best)
                    {
                        best = score;
                        bestTime = t;
                    }
                }

                previous = position;
            }

            return bestTime;
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }
    }

    /// <summary>True when the clip leaves the ground (both feet well up at once); <paramref name="landing"/> is when it comes down.</summary>
    public static bool Leaps(AnimationClip clip, out float landing)
    {
        const float Clearance = 0.12f;
        landing = 0f;
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Animator source = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (clip == null || source == null || source.avatar == null)
        {
            return false;
        }

        GameObject rig = Object.Instantiate(source.gameObject);
        rig.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var animator = rig.GetComponent<Animator>();
            animator.applyRootMotion = false;
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot), right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            float ground = float.MaxValue;
            var lowest = new List<(float time, float height)>();
            for (float t = 0f; t <= clip.length + 1e-4f; t += SampleStep)
            {
                clip.SampleAnimation(rig, t);
                float low = Mathf.Min(left.position.y, right.position.y);
                ground = Mathf.Min(ground, low);
                lowest.Add((t, low));
            }

            bool leaps = false;
            foreach ((float time, float height) in lowest)
            {
                if (height - ground > Clearance)
                {
                    leaps = true;
                    landing = time;
                }
            }

            return leaps;
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }
    }

    /// <summary>The way the hand moves around <paramref name="time"/> (seconds into the clip), in the character's own
    /// space (x right, y up, z forward), in metres per second. Zero when the clip can't be played.</summary>
    public static Vector3 HandVelocity(AnimationClip clip, float time, HumanBodyBones hand = HumanBodyBones.RightHand)
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Animator source = player != null ? player.GetComponentInChildren<Animator>() : null;
        if (clip == null || source == null || source.avatar == null)
        {
            return Vector3.zero;
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
                return Vector3.zero;
            }

            // Over a short window round the contact, so one jittery frame doesn't decide it.
            const float half = 0.05f;
            float from = Mathf.Max(0f, time - half), to = Mathf.Min(clip.length, time + half);
            clip.SampleAnimation(rig, from);
            Vector3 start = rig.transform.InverseTransformPoint(bone.position);
            clip.SampleAnimation(rig, to);
            Vector3 end = rig.transform.InverseTransformPoint(bone.position);
            return to > from ? (end - start) / (to - from) : Vector3.zero;
        }
        finally
        {
            Object.DestroyImmediate(rig);
        }
    }

    /// <summary>How the slash arc should run for a hand moving at <paramref name="velocity"/> (character space): the way it
    /// sweeps and its roll (positive raises the right end). A swing that comes down steeply gives a steep arc; one that
    /// mostly goes forward (a thrust, a jab) a flat one.</summary>
    public static (SlashSweep sweep, float tilt) SlashFor(Vector3 velocity)
    {
        float across = Mathf.Abs(velocity.x);
        SlashSweep sweep = velocity.x >= 0f ? SlashSweep.LeftToRight : SlashSweep.RightToLeft;
        if (across < 0.25f * Mathf.Abs(velocity.z) && Mathf.Abs(velocity.y) < 0.5f * Mathf.Abs(velocity.z))
        {
            return (sweep, 0f);
        }

        // Positive when it comes down. The end it starts from is the end that sits higher.
        float down = Mathf.Atan2(-velocity.y, Mathf.Max(across, 0.01f)) * Mathf.Rad2Deg;
        float tilt = (sweep == SlashSweep.RightToLeft ? 1f : -1f) * down;
        return (sweep, Mathf.Clamp(tilt, -80f, 80f));
    }
}
