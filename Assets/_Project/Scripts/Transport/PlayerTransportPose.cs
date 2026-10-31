using UnityEngine;

/// <summary>
/// The player's body while moving a transport. There are no animations for it, so it bends the bones after the Animator
/// has posed them: arms reach for the handles, and at the oars the legs fold to sit and the arms pull with the stroke.
/// Blends in and out, like the other pose layers (PlayerArcheryAnimation, PlayerSwimAnimation).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(300)]
[RequireComponent(typeof(PlayerTransport))]
public sealed class PlayerTransportPose : MonoBehaviour
{
    private const float BlendSpeed = 6f;
    private const float PushArms = -44f;
    private const float PullArms = 28f;
    private const float RowArms = -68f;
    private const float RowReach = 24f;
    private const float SeatThigh = -82f;
    private const float SeatShin = 78f;

    private PlayerTransport transportUser;
    private Transform leftArm;
    private Transform rightArm;
    private Transform leftThigh;
    private Transform rightThigh;
    private Transform leftShin;
    private Transform rightShin;
    private float armWeight;
    private float legWeight;
    private float armAngle;

    private void Awake()
    {
        transportUser = GetComponent<PlayerTransport>();
        Animator animator = GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            enabled = false;
            return;
        }

        leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        leftShin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
    }

    private void LateUpdate()
    {
        Transport used = transportUser.Using;
        var boat = used != null ? used.Driver as Rowboat : null;
        var cart = used != null ? used.Driver as Haulable : null;
        armWeight = Mathf.MoveTowards(armWeight, used != null ? 1f : 0f, BlendSpeed * Time.deltaTime);
        legWeight = Mathf.MoveTowards(legWeight, boat != null ? 1f : 0f, BlendSpeed * Time.deltaTime);
        if (boat != null)
        {
            armAngle = RowArms + boat.Stroke * RowReach;
        }
        else if (cart != null)
        {
            armAngle = cart.Pushed ? PushArms : PullArms;
        }

        Vector3 across = transform.right;
        Bend(leftArm, across, armAngle * armWeight);
        Bend(rightArm, across, armAngle * armWeight);
        Bend(leftThigh, across, SeatThigh * legWeight);
        Bend(rightThigh, across, SeatThigh * legWeight);
        Bend(leftShin, across, SeatShin * legWeight);
        Bend(rightShin, across, SeatShin * legWeight);
    }

    private static void Bend(Transform bone, Vector3 axis, float degrees)
    {
        if (bone != null && degrees != 0f)
        {
            bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
        }
    }
}
