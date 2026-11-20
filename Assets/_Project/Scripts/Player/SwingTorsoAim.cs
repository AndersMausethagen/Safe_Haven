using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a swing's torso aimed as its clip aims it while the legs walk (the user, 2026-11-20: "start with the fixes you learned from
/// V Rising"). A swing walked through plays on the Upper Body layer, masked to the spine, arms and head, over the combat walk's legs.
/// The spine's muscles are relative to the hips, so the torso took the walk's hips (a strafe or a backpedal turns them) instead of
/// the swing's own: the blow aimed off by the hips' twist, and the clip's hips turning into the blow were lost. Stunlock's fix in V
/// Rising (Albin Thunström, "V Rising's animation layering in Unity", 80.lv, 2022) is a chest proxy joint baked from each clip and IK
/// that puts the chest back on it. The same here, without a rig change:
/// <list type="bullet">
/// <item>for every swing state, which way the shoulders face relative to the body through the clip, baked (Safe Haven › Tools ›
/// Fit Swing Torso Aim, SwingTorsoAimTests keeps it current);</item>
/// <item>after the animation (before IK), while the Upper Body layer shows a baked swing and nothing covers it, the spine, chest and
/// upper chest turn about the vertical by what puts the shoulders back on the clip's facing, eased in and out with the layer.</item>
/// </list>
/// Yaw only: the clip's lean and the legs' stride stay as they are.
/// </summary>
// Before PoseInertializer (-200): a blend starts from, and coasts, the corrected torso (review 2026-11-20: it had blended from the
// uncorrected one, so walking out of a swing snapped the torso straight). Its watched bones (head, neck, shoulder) aren't turned.
[DefaultExecutionOrder(-210)]
[RequireComponent(typeof(Animator))]
public sealed class SwingTorsoAim : MonoBehaviour
{
    [Serializable]
    public struct Facing
    {
        public string state;
        /// <summary>Shoulder yaw (degrees, about the body's up, positive turning right) at even steps through the clip, first to last
        /// frame.</summary>
        public float[] yaw;

        /// <summary>The facing at <paramref name="normalized"/> (0 to 1) of the clip, between the steps.</summary>
        public float At(float normalized)
        {
            if (yaw == null || yaw.Length == 0)
            {
                return 0f;
            }

            float at = Mathf.Clamp01(normalized) * (yaw.Length - 1);
            int i = Mathf.Min((int)at, yaw.Length - 2);
            return yaw.Length == 1 ? yaw[0] : Mathf.Lerp(yaw[i], yaw[i + 1], at - i);
        }
    }

    [Tooltip("Measured by Safe Haven › Tools › Fit Swing Torso Aim: each swing state's shoulder facing through its clip.")]
    [SerializeField] private Facing[] facings = new Facing[0];
    [Tooltip("The most the torso is turned back, in degrees.")]
    [SerializeField, Range(0f, 120f)] private float mostTurn = 75f;
    [Tooltip("How fast the correction comes and goes, in full weights a second.")]
    [SerializeField, Min(1f)] private float fadeRate = 12f;

    private static readonly int EmptyHash = Animator.StringToHash("Empty");
    private static readonly int NoneHash = Animator.StringToHash("None");
    // Unmasked layers above Upper Body: whatever they show covers the swing beneath (the whole-body swing with its own hips, a
    // saddle, an interaction, an emote), so there is nothing to put back where they show (review 2026-11-20).
    private static readonly string[] CoveringLayers = { "Interaction", "Combat Full Body", "Riding", "Emote" };
    private Animator animator;
    private Transform spine, chest, upperChest, leftArm, rightArm;
    private readonly Dictionary<int, Facing> byState = new Dictionary<int, Facing>();
    private readonly List<int> covering = new List<int>();
    private int upperLayer = -1;
    private float weight;
    // The last correction at full weight: it fades out from there when the swing has gone (an instant hand-over to Empty), rather
    // than dropping at once.
    private float fullTurn;
    private float rawTurn;

    public IReadOnlyList<Facing> Facings => facings;

    /// <summary>The turn last applied, in degrees (probes).</summary>
    public float LastTurn { get; private set; }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (!animator.isHuman)
        {
            enabled = false;
            return;
        }

        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        upperLayer = animator.GetLayerIndex("Upper Body");
        foreach (string name in CoveringLayers)
        {
            int layer = animator.GetLayerIndex(name);
            if (layer > upperLayer)
            {
                covering.Add(layer);
            }
        }

        foreach (Facing facing in facings)
        {
            if (facing.yaw != null && facing.yaw.Length > 0)
            {
                byState[Animator.StringToHash(facing.state)] = facing;
            }
        }

        if (spine == null || leftArm == null || rightArm == null || upperLayer < 0 || byState.Count == 0)
        {
            enabled = false;
        }
    }

    private void OnDisable()
    {
        weight = fullTurn = rawTurn = 0f;
    }

    private void LateUpdate()
    {
        if (!animator.isActiveAndEnabled)
        {
            return;
        }

        // Paused (time stopped) the Animator still writes its pose: the correction stays on it, only its fade waits.
        bool found = Target(out float target, out float wanted);
        if (Time.deltaTime > 0f)
        {
            weight = Mathf.MoveTowards(weight, wanted, fadeRate * Time.deltaTime);
        }

        LastTurn = 0f;
        if (weight <= 0f)
        {
            fullTurn = rawTurn = 0f;
            return;
        }

        if (found)
        {
            // Read as the angle nearest last frame's, so a correction near half a turn can't flip from one side to the other
            // between frames (review 2026-11-20).
            float raw = Mathf.DeltaAngle(ShoulderYaw(animator.transform, leftArm.position, rightArm.position), target);
            raw += 360f * Mathf.Round((rawTurn - raw) / 360f);
            rawTurn = raw;
            fullTurn = Mathf.Clamp(raw, -mostTurn, mostTurn);
        }

        float turn = fullTurn * weight;
        LastTurn = turn;
        // Shared up the spine, each bone turning the ones above it: the pieces add up at the shoulders.
        Vector3 up = animator.transform.up;
        if (upperChest != null && chest != null)
        {
            Turn(spine, up, turn * 0.3f);
            Turn(chest, up, turn * 0.35f);
            Turn(upperChest, up, turn * 0.35f);
        }
        else if (chest != null)
        {
            Turn(spine, up, turn * 0.5f);
            Turn(chest, up, turn * 0.5f);
        }
        else
        {
            Turn(spine, up, turn);
        }
    }

    // The facing the Upper Body layer's swing wants now (blended through a hand-over between two baked swings), and how much of the
    // pose that swing is: its share of the layer, less what the unmasked layers above it cover.
    private bool Target(out float target, out float share)
    {
        target = 0f;
        share = 0f;
        float layerWeight = animator.GetLayerWeight(upperLayer);
        if (layerWeight <= 0f)
        {
            return false;
        }

        AnimatorStateInfo now = animator.GetCurrentAnimatorStateInfo(upperLayer);
        bool nowBaked = byState.TryGetValue(now.shortNameHash, out Facing nowFacing);
        if (animator.IsInTransition(upperLayer))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(upperLayer);
            float t = animator.GetAnimatorTransitionInfo(upperLayer).normalizedTime;
            bool nextBaked = byState.TryGetValue(next.shortNameHash, out Facing nextFacing);
            if (nowBaked && nextBaked)
            {
                target = Mathf.LerpAngle(nowFacing.At(now.normalizedTime), nextFacing.At(next.normalizedTime), t);
                share = 1f;
            }
            else if (nextBaked)
            {
                target = nextFacing.At(next.normalizedTime);
                share = t;
            }
            else if (nowBaked)
            {
                target = nowFacing.At(now.normalizedTime);
                share = 1f - t;
            }
            else
            {
                return false;
            }
        }
        else if (nowBaked)
        {
            target = nowFacing.At(now.normalizedTime);
            share = 1f;
        }
        else
        {
            return false;
        }

        share *= layerWeight;
        foreach (int layer in covering)
        {
            share *= 1f - animator.GetLayerWeight(layer) * Covers(layer);
        }

        return true;
    }

    // How much a layer shows something other than its resting state (Empty, or None for Interaction), 0 to 1.
    private float Covers(int layer)
    {
        bool busy = !Resting(animator.GetCurrentAnimatorStateInfo(layer).shortNameHash);
        if (!animator.IsInTransition(layer))
        {
            return busy ? 1f : 0f;
        }

        float t = animator.GetAnimatorTransitionInfo(layer).normalizedTime;
        bool nextBusy = !Resting(animator.GetNextAnimatorStateInfo(layer).shortNameHash);
        return busy && nextBusy ? 1f : busy ? 1f - t : nextBusy ? t : 0f;
    }

    private static bool Resting(int hash) => hash == EmptyHash || hash == NoneHash;

    /// <summary>The shoulders' facing about the body's up, in degrees (0 straight ahead, positive to the right): the direction square to
    /// the line from the left shoulder to the right, as the body sees it.</summary>
    public static float ShoulderYaw(Transform body, Vector3 leftShoulder, Vector3 rightShoulder)
    {
        Vector3 across = body.InverseTransformDirection(rightShoulder - leftShoulder);
        // Facing is the across line turned a quarter left: across (1,0,0) faces (0,0,1).
        return Mathf.Atan2(-across.z, across.x) * Mathf.Rad2Deg;
    }

    private static void Turn(Transform bone, Vector3 up, float degrees)
    {
        if (bone != null && Mathf.Abs(degrees) > 0.01f)
        {
            bone.rotation = Quaternion.AngleAxis(degrees, up) * bone.rotation;
        }
    }
}
