using UnityEngine;

/// <summary>
/// Puts the left hand on a two-handed weapon. The clips were made for other weapons, so the left hand would otherwise follow its own
/// path beside the handle: after the animation has posed the body, this bends the left arm so the palm closes on the handle, a set
/// distance along it from the right hand (the weapon's Second Hand Distance), with the fingers wrapped across it. It lets go, with a
/// short fade, for what doesn't hold the weapon that way: gathering or other work, eating, swimming. It holds on through a dodge
/// (each stance dodges with its own two-handed clip; letting go popped the hand off the haft and back each time, audit 2026-11-20),
/// and lets go anyway where the clip carries the handle out of reach.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerCombat))]
[RequireComponent(typeof(HeldItemView))]
// After the animator's own update and the look at the aim, before anything reads the hand.
[DefaultExecutionOrder(100)]
public sealed class TwoHandGrip : MonoBehaviour
{
    private const float FadePerSecond = 12f;
    // How far from the wrist toward the knuckles the handle sits.
    private const float PalmShare = 0.75f;
    // The left hand has let go by the time the handle is this many arm's lengths from the shoulder.
    private const float LetGoShare = 1.3f;
    // The closest the left hand slides to the right on the handle, in metres.
    private const float FistWidth = 0.1f;

    private PlayerCombat combat;
    private HeldItemView held;
    private PlayerMovement movement;
    private PlayerStateMachine stateMachine;
    private PlayerEating eating;
    private PlayerSwimming swimming;
    private Damageable health;
    private Transform upperArm;
    private Transform lowerArm;
    private Transform hand;
    private Transform knuckle;
    private float upperLength;
    private float lowerLength;
    private GameObject measuredModel;
    private Vector3 localTip;
    private Vector3 localButt;
    private bool hasHandle;
    private float weight;

    private void Awake()
    {
        combat = GetComponent<PlayerCombat>();
        held = GetComponent<HeldItemView>();
        movement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();
        eating = GetComponent<PlayerEating>();
        swimming = GetComponent<PlayerSwimming>();
        health = GetComponent<Damageable>();
        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            enabled = false;
            return;
        }

        upperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        lowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        knuckle = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
        if (upperArm == null || lowerArm == null || hand == null)
        {
            enabled = false;
            return;
        }

        upperLength = Vector3.Distance(upperArm.position, lowerArm.position);
        lowerLength = Vector3.Distance(lowerArm.position, hand.position);
    }

    private bool ShouldGrip
    {
        get
        {
            WeaponDefinition weapon = combat.Weapon;
            return weapon != null && weapon.Kind == WeaponKind.Melee && weapon.RequiresBothHands && held.Shown != null && held.ShownModel != null
                   && stateMachine.CurrentState != PlayerState.Interacting && (eating == null || !eating.IsEating)
                   && (swimming == null || !swimming.IsSwimming) && (health == null || health.IsAlive);
        }
    }

    private void LateUpdate()
    {
        weight = Mathf.MoveTowards(weight, ShouldGrip ? 1f : 0f, FadePerSecond * Time.deltaTime);
        if (weight <= 0f || held.ShownModel == null || !FindHandle())
        {
            return;
        }

        Transform model = held.ShownModel.transform;
        Vector3 tip = model.TransformPoint(localTip);
        Vector3 butt = model.TransformPoint(localButt);
        Vector3 axis = (tip - butt).normalized;
        Vector3 hold = HoldPoint(tip, butt, axis, combat.Weapon.SecondHandDistance, out Vector3 towardRight, out float slide);

        // The fingers go across the handle; keep the rest of how the clip holds the hand.
        Vector3 toKnuckle = knuckle != null ? knuckle.position - hand.position : hand.right * 0.08f;
        Vector3 across = Vector3.ProjectOnPlane(toKnuckle, axis);
        Quaternion wrap = across.sqrMagnitude > 1e-6f ? Quaternion.FromToRotation(toKnuckle.normalized, across.normalized) : Quaternion.identity;
        Quaternion gripped = wrap * hand.rotation;
        Vector3 palm = wrap * toKnuckle * PalmShare;

        // A big swing can carry the hold out of reach: the hand slides down the handle toward the right hand as far as it must (hands
        // gather on the haft through a wide swing), and only lets go when even that can't be reached, rather than the arm stretching.
        float reach = upperLength + lowerLength;
        Vector3 wanted = WithinReach(hold - palm, towardRight, slide, upperArm.position, reach);
        float amount = weight * Mathf.InverseLerp(reach * LetGoShare, reach, Vector3.Distance(upperArm.position, wanted));
        if (amount <= 0f)
        {
            return;
        }

        Vector3 target = Vector3.Lerp(hand.position, wanted, amount);
        Quaternion handRotation = Quaternion.Slerp(hand.rotation, gripped, amount);
        Vector3 elbow = ArmSolver.Solve(upperArm.position, lowerArm.position, target, upperLength, lowerLength, out Vector3 wrist);
        upperArm.rotation = Quaternion.FromToRotation(lowerArm.position - upperArm.position, elbow - upperArm.position) * upperArm.rotation;
        lowerArm.rotation = Quaternion.FromToRotation(hand.position - lowerArm.position, wrist - lowerArm.position) * lowerArm.rotation;
        hand.rotation = handRotation;
    }

    // Down the handle from the right hand, stopping at the butt; a weapon held at its very end takes the left hand up it instead.
    // Also which way along the handle the right hand is, and how far the left may slide that way (to a fist's width from it).
    private Vector3 HoldPoint(Vector3 tip, Vector3 butt, Vector3 axis, float distance, out Vector3 towardRight, out float slide)
    {
        Vector3 right = held.Grip != null ? held.Grip.position : butt;
        float fromButt = Vector3.Dot(right - butt, axis);
        float length = Vector3.Dot(tip - butt, axis);
        float along = fromButt >= distance ? fromButt - distance : Mathf.Min(fromButt + distance, length);
        towardRight = fromButt >= along ? axis : -axis;
        slide = Mathf.Max(0f, Mathf.Abs(fromButt - along) - FistWidth);
        return butt + axis * along;
    }

    /// <summary>
    /// <paramref name="wanted"/> if it is within <paramref name="reach"/> of the shoulder; else the first point that is, going up to
    /// <paramref name="slide"/> along <paramref name="toward"/> (a unit direction); else the nearest of those points. Continuous: the
    /// hold moves off the wanted point only as far as the reach makes it.
    /// </summary>
    public static Vector3 WithinReach(Vector3 wanted, Vector3 toward, float slide, Vector3 shoulder, float reach)
    {
        Vector3 from = wanted - shoulder;
        float c = from.sqrMagnitude - reach * reach;
        if (c <= 0f || slide <= 0f)
        {
            return wanted;
        }

        // |from + toward s| = reach: s^2 + 2 b s + c = 0. Outside the reach both roots have one sign; the smaller is the first in reach.
        float b = Vector3.Dot(from, toward);
        float disc = b * b - c;
        if (disc >= 0f)
        {
            float first = -b - Mathf.Sqrt(disc);
            if (first >= 0f && first <= slide)
            {
                return wanted + toward * first;
            }
        }

        return wanted + toward * Mathf.Clamp(-b, 0f, slide);
    }

    private bool FindHandle()
    {
        GameObject model = held.ShownModel;
        if (model != measuredModel)
        {
            measuredModel = model;
            hasHandle = HeldWeaponShape.Measure(model, held.Grip, out localTip, out localButt);
            if (hasHandle && held.Grip != null)
            {
                // The handle runs through the right hand, along the model's length. The middle of the model's bounds runs beside the
                // haft of anything with its head to one side: on an axe it sat 13 cm off it, so the left hand closed on air (the user's
                // picture, 2026-11-20).
                Vector3 axis = (localTip - localButt).normalized;
                Vector3 right = model.transform.InverseTransformPoint(held.Grip.position);
                Vector3 shift = right - (localButt + axis * Vector3.Dot(right - localButt, axis));
                localTip += shift;
                localButt += shift;
            }
        }

        return hasHandle;
    }
}
