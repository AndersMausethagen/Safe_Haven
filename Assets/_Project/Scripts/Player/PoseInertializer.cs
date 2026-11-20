using UnityEngine;

/// <summary>
/// Smoother hand-overs between a humanoid body's moves (the user, 2026-11-20: "start with 1,2,3", the first being inertialized
/// blends). A crossfade mixes two moving clips, so a limb going one way in the old move is dragged by the new one from the first frame
/// and its pace jumps at both ends. Here the old move is let go of instead: the pose last shown coasts on in the direction each bone was
/// turning, slowing as it goes, and blends into the new move with an ease that starts and ends flat ("dead blending", Holden 2023, the
/// follow-up to Bollo's inertialization, GDC 2018). A plain offset ease was tried first and measured worse: it shows the new move's own
/// motion at full speed from the first frame, a pop of its own (A/B probe, 2026-11-20).
/// <para>Where it is used was measured too (the pose's frame-to-frame acceleration through each blend, three weapons): it beats the
/// crossfade where the pose let go of is moving, a chain's swing into its next and walking out of a swing (chains 15-45% less), and
/// loses where it is still, a swing from the ready pose, a counter out of the guard, a hit (the new move's start shows sooner). So
/// PlayerAnimationController asks for it on those two hand-overs only and crossfades the rest.</para>
/// <list type="bullet">
/// <item><see cref="Begin"/> asks for one; the caller then switches states with no crossfade (PlayerAnimationController.Blend);</item>
/// <item>the next LateUpdate, before any IK or pose writer (execution order -200), keeps each bone's pose shown the frame before and how
/// fast it was turning (from the frame before that);</item>
/// <item>each frame of the blend it shows <see cref="Coast"/> of that pose eased into the Animator's new one by <see cref="Ease"/>;</item>
/// <item>every frame it keeps the pose it showed, so a blend begun inside another starts from what is on screen.</item>
/// </list>
/// Local rotations of the humanoid bones and the hips' position: no muscle conversion, so a frame with no blend running is left exactly
/// as the Animator made it. Turned off, every caller falls back to its crossfade.
/// </summary>
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Animator))]
public sealed class PoseInertializer : MonoBehaviour
{
    [Tooltip("Longest blend it eases over, in seconds; a longer one asked for is cut to this.")]
    [SerializeField, Min(0.02f)] private float longestBlend = 0.5f;
    [Tooltip("How quickly the old move's motion dies away as it coasts, in seconds for it to halve.")]
    [SerializeField, Min(0.005f)] private float coastHalfLife = 0.05f;
    [Tooltip("The most a bone coasts on past the pose it was let go in, in degrees: a whirling limb doesn't fly off.")]
    [SerializeField, Range(1f, 90f)] private float longestCoast = 30f;

    private Animator animator;
    private Transform[] bones;
    private Quaternion[] shown, shownBefore, from;
    private Vector3[] turning;
    private Vector3 hipsShown, hipsShownBefore, hipsFrom, hipsMoving;
    private Transform hips;
    private int[] watched = new int[0];
    private float pending = -1f;
    private float elapsed = -1f;
    private float duration;
    private float lastDelta;
    private int recorded;

    /// <summary>It can blend now: it has seen the two frames a blend starts from and the Animator writes the bones each frame. When it
    /// can't, the caller crossfades instead (an instant switch would pop).</summary>
    public bool Ready => isActiveAndEnabled && recorded >= 2 && animator != null && animator.isActiveAndEnabled && animator.updateMode != AnimatorUpdateMode.Fixed;

    /// <summary>A blend running or waiting for its first frame.</summary>
    public bool Blending => pending > 0f || elapsed >= 0f;

    /// <summary>Asks for a blend over <paramref name="seconds"/>, from the pose shown now to whatever the Animator shows next. Several
    /// in one frame make one, over the longest.</summary>
    public void Begin(float seconds)
    {
        if (seconds > 0f)
        {
            pending = Mathf.Max(pending, Mathf.Min(seconds, longestBlend));
        }
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            enabled = false;
            return;
        }

        var found = new System.Collections.Generic.List<Transform>();
        for (var bone = HumanBodyBones.Hips; bone < HumanBodyBones.LastBone; bone++)
        {
            Transform t = animator.GetBoneTransform(bone);
            if (t != null)
            {
                found.Add(t);
            }
        }

        bones = found.ToArray();
        var watch = new System.Collections.Generic.List<int>();
        foreach (HumanBodyBones bone in new[] { HumanBodyBones.Head, HumanBodyBones.Neck, HumanBodyBones.RightShoulder })
        {
            int at = found.IndexOf(animator.GetBoneTransform(bone));
            if (at >= 0)
            {
                watch.Add(at);
            }
        }

        watched = watch.ToArray();
        hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        shown = new Quaternion[bones.Length];
        shownBefore = new Quaternion[bones.Length];
        from = new Quaternion[bones.Length];
        turning = new Vector3[bones.Length];
    }

    private void OnDisable()
    {
        pending = elapsed = -1f;
        recorded = 0;
    }

    private void LateUpdate()
    {
        // A paused game (time stopped) holds the blend where it is; a slowed body (a hit-stop) blends as slowly as it moves.
        float dt = Time.deltaTime * Mathf.Max(0f, animator != null ? animator.speed : 1f);
        if (dt <= 0f)
        {
            if (elapsed >= 0f && animator.isActiveAndEnabled && !Unwritten())
            {
                Apply();
            }

            return;
        }

        // Only on bones the Animator wrote this frame: on any others (a ragdoll, a disabled Animator) the blend would pile up.
        if (!animator.isActiveAndEnabled || animator.updateMode == AnimatorUpdateMode.Fixed)
        {
            pending = elapsed = -1f;
            recorded = 0;
            return;
        }

        // Culled (CullUpdateTransforms off screen), the Animator left the bones as this showed them: nothing to blend from or onto.
        if (elapsed >= 0f && Unwritten())
        {
            elapsed = -1f;
        }

        if (pending > 0f && recorded >= 2)
        {
            Start(pending);
        }

        pending = -1f;
        if (elapsed >= 0f)
        {
            elapsed += dt;
            Apply();
            if (elapsed >= duration)
            {
                elapsed = -1f;
            }
        }

        Record();
        lastDelta = dt;
    }

    // The pose let go of (the one shown the frame before) and how each bone was turning then, as a rotation vector (radians a second).
    private void Start(float seconds)
    {
        duration = seconds;
        elapsed = 0f;
        float dt = Mathf.Max(1e-4f, lastDelta);
        for (int i = 0; i < bones.Length; i++)
        {
            from[i] = shown[i];
            Quaternion step = Shortest(shown[i] * Quaternion.Inverse(shownBefore[i]));
            step.ToAngleAxis(out float degrees, out Vector3 axis);
            turning[i] = degrees > 1e-3f && !float.IsNaN(axis.x) && !float.IsInfinity(axis.x) ? axis * (degrees * Mathf.Deg2Rad / dt) : Vector3.zero;
        }

        hipsFrom = hipsShown;
        hipsMoving = (hipsShown - hipsShownBefore) / dt;
    }

    private void Apply()
    {
        float ease = Ease(elapsed / duration);
        float coast = Coast(elapsed, coastHalfLife);
        float most = longestCoast * Mathf.Deg2Rad;
        for (int i = 0; i < bones.Length; i++)
        {
            float speed = turning[i].magnitude;
            Quaternion coasted = speed > 0f
                ? Quaternion.AngleAxis(Mathf.Min(speed * coast, most) * Mathf.Rad2Deg, turning[i] / speed) * from[i]
                : from[i];
            bones[i].localRotation = Quaternion.Slerp(coasted, bones[i].localRotation, ease);
        }

        if (hips != null)
        {
            hips.localPosition = Vector3.Lerp(hipsFrom + Vector3.ClampMagnitude(hipsMoving * coast, 0.15f), hips.localPosition, ease);
        }
    }

    private void Record()
    {
        for (int i = 0; i < bones.Length; i++)
        {
            shownBefore[i] = shown[i];
            shown[i] = bones[i].localRotation;
        }

        if (hips != null)
        {
            hipsShownBefore = hipsShown;
            hipsShown = hips.localPosition;
        }

        recorded++;
    }

    /// <summary>The share of the new move shown at <paramref name="share"/> of the blend: a smoothstep, flat at both ends, so the
    /// pose leaves the old move at its own pace and arrives in the new one at the new one's. (A smootherstep was measured worse: on
    /// these short blends it crowds the change into the middle.)</summary>
    public static float Ease(float share)
    {
        float s = Mathf.Clamp01(share);
        return s * s * (3f - 2f * s);
    }

    /// <summary>How far (in seconds of its last speed) a pose let go of has coasted on after <paramref name="t"/>, its motion halving
    /// every <paramref name="halfLife"/>: t at first, never more than halfLife / ln 2.</summary>
    public static float Coast(float t, float halfLife)
    {
        float rate = 0.69314718f / Mathf.Max(1e-4f, halfLife);
        return (1f - Mathf.Exp(-rate * Mathf.Max(0f, t))) / rate;
    }

    // The head, the neck and the right shoulder exactly as shown last frame: the Animator didn't write them this frame (one that did
    // put its own pose back, which differs from the blended one). Bones nothing after this writes: the feet's lock rewrites the legs,
    // the two-handed grip the left arm, the bow the spine (review 2026-11-20).
    private bool Unwritten()
    {
        if (watched.Length == 0)
        {
            return false;
        }

        foreach (int i in watched)
        {
            Quaternion now = bones[i].localRotation, then = shown[i];
            if (now.x != then.x || now.y != then.y || now.z != then.z || now.w != then.w)
            {
                return false;
            }
        }

        return true;
    }

    private static Quaternion Shortest(Quaternion q) => q.w < 0f ? new Quaternion(-q.x, -q.y, -q.z, -q.w) : q;
}
