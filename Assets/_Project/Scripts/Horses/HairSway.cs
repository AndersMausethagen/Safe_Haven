using System;
using System.Linq;
using UnityEngine;

/// <summary>
/// A horse's mane and tail swinging with its movement, on top of its animation: each hair bone's tip trails where the
/// clip puts it (lagging, overshooting a little and settling), pulled down a little, never further than Max Angle from
/// the animated pose, its length kept. Runs after the Animator (LateUpdate), so the clips still lead: a tail the clip
/// swishes swishes, and swings as well. Chains go root to tip; a bone's tip is the next bone, the last one's its End
/// (a small end bone the model carries, Tools/Blender/horse_hair.py). Far from the camera it rests on the clip alone.
/// </summary>
[DisallowMultipleComponent]
public sealed class HairSway : MonoBehaviour
{
    [Serializable]
    public sealed class Chain
    {
        [Tooltip("Root to tip.")]
        public Transform[] bones = Array.Empty<Transform>();
        [Tooltip("Where the last bone ends.")]
        public Transform end;
    }

    private const float ResetDistance = 2f;

    [SerializeField] private Chain[] chains = Array.Empty<Chain>();
    [Tooltip("How quickly the hair returns to the animated pose: 0 never, 1 at once (per 60th of a second).")]
    [SerializeField, Range(0.01f, 1f)] private float stiffness = 0.12f;
    [Tooltip("How much of its swing it keeps from one step to the next: 0 none, 1 all.")]
    [SerializeField, Range(0f, 1f)] private float carry = 0.82f;
    [Tooltip("How hard it hangs down (m/s²).")]
    [SerializeField, Min(0f)] private float gravity = 2.5f;
    [Tooltip("The furthest it swings from the animated pose (degrees).")]
    [SerializeField, Range(0f, 90f)] private float maxAngle = 40f;
    [Tooltip("Metres from the camera beyond which it doesn't sway.")]
    [SerializeField, Min(1f)] private float swayWithin = 45f;

    private Vector3[][] now;
    private Vector3[][] before;
    private Quaternion[][] rest;
    private Quaternion[][] given;
    private bool started;

    /// <summary>For tests and the builder.</summary>
    public int ChainCount => chains.Length;

    private void OnEnable() => started = false;

    // Back to the rest pose when it stops swaying (far off, or switched off), so a mane isn't left mid-swing.
    private void Settle()
    {
        if (rest == null || given == null) return;
        for (int c = 0; c < chains.Length; c++)
        {
            for (int i = 0; i < chains[c].bones.Length; i++)
            {
                Transform bone = chains[c].bones[i];
                if (bone != null && bone.localRotation == given[c][i]) bone.localRotation = rest[c][i];
            }
        }
    }

    private void OnDisable()
    {
        Settle();
        started = false;
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || chains.Length == 0)
        {
            return;
        }

        Camera view = Camera.main;
        if (view != null && (view.transform.position - transform.position).sqrMagnitude > swayWithin * swayWithin)
        {
            if (started) Settle();
            started = false;
            return;
        }

        if (!started)
        {
            Begin();
        }

        // A bone no clip animates (the mane's) still has last frame's swing on it: back to its rest first, or the swings
        // add up. One the clip wrote this frame (the tail's) has changed since, and is left as the clip put it.
        for (int c = 0; c < chains.Length; c++)
        {
            Transform[] bones = chains[c].bones;
            for (int i = 0; i < bones.Length; i++)
            {
                if (bones[i] != null && bones[i].localRotation == given[c][i])
                {
                    bones[i].localRotation = rest[c][i];
                }
            }
        }

        float pull = 1f - Mathf.Pow(1f - stiffness, dt * 60f);
        float keep = Mathf.Pow(carry, dt * 60f);
        Vector3 fall = Vector3.down * (gravity * dt * dt);
        for (int c = 0; c < chains.Length; c++)
        {
            Transform[] bones = chains[c].bones;
            for (int i = 0; i < bones.Length; i++)
            {
                Transform bone = bones[i];
                Transform tip = i + 1 < bones.Length ? bones[i + 1] : chains[c].end;
                if (bone == null || tip == null)
                {
                    continue;
                }

                // Where the clip (and the bones above, already swung) put the tip this frame.
                Vector3 root = bone.position;
                Vector3 animated = tip.position;
                float length = (animated - root).magnitude;
                if (length < 1e-4f)
                {
                    continue;
                }

                Vector3 p = now[c][i];
                if ((p - animated).sqrMagnitude > ResetDistance * ResetDistance)
                {
                    // Moved too far at once (a teleport, a load): start from the pose.
                    p = animated;
                    before[c][i] = animated;
                }

                Vector3 swing = (p - before[c][i]) * keep;
                before[c][i] = p;
                p += swing + fall;
                p = Vector3.Lerp(p, animated, pull);

                Vector3 wanted = (animated - root) / length;
                Vector3 dir = (p - root).sqrMagnitude > 1e-8f ? (p - root).normalized : wanted;
                dir = Vector3.RotateTowards(wanted, dir, maxAngle * Mathf.Deg2Rad, 0f);
                now[c][i] = root + dir * length;
                bone.rotation = Quaternion.FromToRotation(wanted, dir) * bone.rotation;
                given[c][i] = bone.localRotation;
            }
        }
    }

    private void Begin()
    {
        now = new Vector3[chains.Length][];
        before = new Vector3[chains.Length][];
        rest ??= chains.Select(chain => chain.bones.Select(b => b != null ? b.localRotation : Quaternion.identity).ToArray()).ToArray();
        given = chains.Select(chain => new Quaternion[chain.bones.Length]).ToArray();
        for (int c = 0; c < chains.Length; c++)
        {
            Transform[] bones = chains[c].bones;
            now[c] = new Vector3[bones.Length];
            before[c] = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                Transform tip = i + 1 < bones.Length ? bones[i + 1] : chains[c].end;
                Vector3 at = tip != null ? tip.position : bones[i] != null ? bones[i].position : Vector3.zero;
                now[c][i] = at;
                before[c][i] = at;
            }
        }

        started = true;
    }

    /// <summary>Sets the chains (the builder).</summary>
    public void Configure(Chain[] made) => chains = made ?? Array.Empty<Chain>();
}
