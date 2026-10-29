using UnityEngine;

/// <summary>
/// How a companion looks while it acts: its Animator (the definition's animations, driven through Speed, Attack and Dead
/// when the controller has them) and the definition's weapon in its hand. Plain C#, owned by a Companion.
/// </summary>
public sealed class CompanionBody
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    private readonly Animator animator;
    private readonly bool hasSpeed;
    private readonly bool hasAttack;
    private readonly bool hasDead;

    public CompanionBody(Animator animator, CompanionDefinition definition)
    {
        this.animator = animator;
        if (animator != null && definition != null && definition.Animations != null)
        {
            animator.runtimeAnimatorController = definition.Animations;
        }

        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            hasSpeed |= parameter.nameHash == SpeedHash && parameter.type == AnimatorControllerParameterType.Float;
            hasAttack |= parameter.nameHash == AttackHash && parameter.type == AnimatorControllerParameterType.Trigger;
            hasDead |= parameter.nameHash == DeadHash && parameter.type == AnimatorControllerParameterType.Bool;
        }
    }

    public bool IsArmed { get; private set; }

    public void SetSpeed(float speed)
    {
        if (hasSpeed)
        {
            animator.SetFloat(SpeedHash, speed, 0.15f, Time.deltaTime);
        }
    }

    public void PlayAttack()
    {
        if (hasAttack)
        {
            animator.SetTrigger(AttackHash);
        }
    }

    public void SetDown(bool down)
    {
        if (hasDead)
        {
            animator.SetBool(DeadHash, down);
        }
    }

    /// <summary>Puts the definition's weapon in the hand at its grip (a bow in the left hand). Once; later calls do
    /// nothing.</summary>
    public void Arm(CompanionDefinition definition)
    {
        if (IsArmed)
        {
            return;
        }

        IsArmed = true;
        ItemDefinition weapon = definition.Weapon;
        if (weapon == null || animator == null || !animator.isHuman)
        {
            return;
        }

        bool bow = HeldItemView.IsBow(weapon);
        Transform hand = animator.GetBoneTransform(bow ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
        if (hand == null)
        {
            return;
        }

        Pose pose = bow ? definition.OffHandGripPose : definition.GripPose;
        var grip = new GameObject("Weapon Grip").transform;
        grip.SetParent(hand, false);
        grip.SetLocalPositionAndRotation(pose.position, pose.rotation);
        HeldItemView.CreateModel(weapon, grip);
    }
}
