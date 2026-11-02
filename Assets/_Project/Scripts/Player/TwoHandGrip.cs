using UnityEngine;

/// <summary>
/// Puts the left hand on a two-handed weapon. The clips were made for other weapons, so the left hand would otherwise follow its own
/// path beside the handle: after the animation has posed the body, this bends the left arm so the palm closes on the handle, a set
/// distance along it from the right hand (the weapon's Second Hand Distance), with the fingers wrapped across it. It lets go, with a
/// short fade, for what doesn't hold the weapon that way: a dodge, gathering or other work, eating, swimming.
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
                   && stateMachine.CurrentState != PlayerState.Interacting && !movement.IsDashing && (eating == null || !eating.IsEating)
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
        Vector3 hold = HoldPoint(tip, butt, axis, combat.Weapon.SecondHandDistance);

        // The fingers go across the handle; keep the rest of how the clip holds the hand.
        Vector3 toKnuckle = knuckle != null ? knuckle.position - hand.position : hand.right * 0.08f;
        Vector3 across = Vector3.ProjectOnPlane(toKnuckle, axis);
        Quaternion wrap = across.sqrMagnitude > 1e-6f ? Quaternion.FromToRotation(toKnuckle.normalized, across.normalized) : Quaternion.identity;
        Quaternion gripped = wrap * hand.rotation;
        Vector3 palm = wrap * toKnuckle * PalmShare;

        // The follow-through of a big swing can carry the handle out of reach: the hand lets go rather than the arm stretching after it.
        Vector3 wanted = hold - palm;
        float reach = upperLength + lowerLength;
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
    private Vector3 HoldPoint(Vector3 tip, Vector3 butt, Vector3 axis, float distance)
    {
        Vector3 right = held.Grip != null ? held.Grip.position : butt;
        float fromButt = Vector3.Dot(right - butt, axis);
        float length = Vector3.Dot(tip - butt, axis);
        float along = fromButt >= distance ? fromButt - distance : Mathf.Min(fromButt + distance, length);
        return butt + axis * along;
    }

    private bool FindHandle()
    {
        GameObject model = held.ShownModel;
        if (model != measuredModel)
        {
            measuredModel = model;
            hasHandle = HeldWeaponShape.Measure(model, held.Grip, out localTip, out localButt);
        }

        return hasHandle;
    }
}
