using UnityEngine;

/// <summary>
/// The rider's hands on the reins and feet in the stirrups (an IK pass on the player Animator's Riding layer): the hands
/// reach the rein bones (Reins_Bn_Hand_L/R on the horse's back), the feet hang where the stirrups are beside the saddle.
/// It eases off while the rider swings a sword or draws a bow, when their hands are busy, and with the Riding layer.
/// Added to the player's Animator by PlayerRidingAnimation.
/// </summary>
[DisallowMultipleComponent]
public sealed class RiderIK : MonoBehaviour
{
    private const float HandWeight = 0.85f;
    private const float FootWeight = 0.6f;
    // Where the stirrups hang from the seat: to the side, down and a touch forward (metres).
    private const float StirrupSide = 0.33f;
    private const float StirrupDrop = 0.9f;
    private const float StirrupAhead = 0.08f;

    private Animator animator;
    private PlayerRiding riding;
    private PlayerRidingAnimation body;
    private Horse reinsOf;
    private Transform leftRein;
    private Transform rightRein;
    private float weight;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        riding = GetComponentInParent<PlayerRiding>();
        body = GetComponentInParent<PlayerRidingAnimation>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || riding == null || body == null || layerIndex != body.LayerIndex)
        {
            return;
        }

        Horse horse = riding.Riding;
        bool hold = horse != null && !riding.IsMounting && !body.IsActing && !horse.Motion.IsSwimming;
        weight = Mathf.MoveTowards(weight, hold ? 1f : 0f, Time.deltaTime * 4f);
        if (horse == null || weight <= 0.001f)
        {
            SetHands(0f);
            SetFeet(0f);
            return;
        }

        if (reinsOf != horse)
        {
            reinsOf = horse;
            leftRein = Find(horse.transform, "Reins_Bn_Hand_L");
            rightRein = Find(horse.transform, "Reins_Bn_Hand_R");
        }

        float layer = animator.GetLayerWeight(body.LayerIndex);
        if (leftRein != null && rightRein != null)
        {
            SetHands(HandWeight * weight * layer);
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftRein.position);
            animator.SetIKPosition(AvatarIKGoal.RightHand, rightRein.position);
        }

        Transform seat = horse.Seat;
        Vector3 across = horse.transform.right;
        Vector3 ahead = horse.transform.forward;
        Vector3 hang = seat.position + Vector3.down * StirrupDrop + ahead * StirrupAhead;
        SetFeet(FootWeight * weight * layer);
        animator.SetIKPosition(AvatarIKGoal.LeftFoot, hang - across * StirrupSide);
        animator.SetIKPosition(AvatarIKGoal.RightFoot, hang + across * StirrupSide);
        animator.SetIKRotation(AvatarIKGoal.LeftFoot, Quaternion.LookRotation(ahead, Vector3.up));
        animator.SetIKRotation(AvatarIKGoal.RightFoot, Quaternion.LookRotation(ahead, Vector3.up));
    }

    private void SetHands(float w)
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
    }

    private void SetFeet(float w)
    {
        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, w);
        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, w);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot, w * 0.7f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightFoot, w * 0.7f);
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
        {
            if (bone.name == name)
            {
                return bone;
            }
        }

        return null;
    }
}
