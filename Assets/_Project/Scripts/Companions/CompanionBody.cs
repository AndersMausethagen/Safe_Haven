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
    // Where the walk sits on the controllers' Speed blend, and how fast it covers ground. Beyond it the blend mixes in the
    // run: its lean, and a stride that doesn't fit the ground.
    private const float WalkOnBlend = 1.5f;
    // Where the run sits on the locomotion blend (Villager and the Quaternius bodies: walk 1.5, run 3.5 m/s).
    private const float RunOnBlend = 3.5f;

    private readonly Animator animator;
    private readonly bool hasSpeed;
    private readonly bool hasAttack;
    private readonly bool hasDead;
    private GameObject weaponModel;
    private bool wasVisible;

    public CompanionBody(Animator animator, CompanionDefinition definition)
    {
        this.animator = animator;
        if (animator != null && definition != null && definition.Animations != null)
        {
            animator.runtimeAnimatorController = definition.Animations;
            // A modular woman's body puts her own clips back over the male ones of the new controller.
            ModularCharacter body = animator.GetComponent<ModularCharacter>();
            if (body != null)
            {
                body.ApplyAnimationSwaps();
            }
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
    public bool IsSheathed { get; private set; }

    public void SetSheathed(bool sheathed)
    {
        if (IsSheathed == sheathed) return;
        IsSheathed = sheathed;
        if (weaponModel == null) return;
        if (sheathed)
        {
            wasVisible = weaponModel.activeSelf;
            weaponModel.SetActive(false);
        }
        else weaponModel.SetActive(wasVisible);
    }

    /// <summary>How fast it moves. With a <paramref name="walkingPace"/> (a party keeping step) it keeps to one clip, the
    /// walk or (for a pace nearer a run) the run, played as fast as that pace needs, instead of a blend between them that
    /// changes with every step. The prologue's road party (3.5 m/s) ran its walk at 2.3 times speed.</summary>
    public void SetSpeed(float speed, float walkingPace = 0f)
    {
        if (!hasSpeed)
        {
            return;
        }

        if (walkingPace > 0f)
        {
            float clip = walkingPace >= (WalkOnBlend + RunOnBlend) * 0.5f ? RunOnBlend : WalkOnBlend;
            float moving = Mathf.Clamp01(speed / walkingPace);
            animator.SetFloat(SpeedHash, moving * clip, 0.15f, Time.deltaTime);
            animator.speed = Mathf.Lerp(1f, walkingPace / clip, moving);
            return;
        }

        animator.speed = 1f;
        animator.SetFloat(SpeedHash, speed, 0.15f, Time.deltaTime);
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

    /// <summary>Puts the definition's weapon in the hand at its grip (a bow in the left hand), or <paramref name="given"/>, the one the
    /// player gave them (CompanionKit). Once; later calls do nothing.</summary>
    public void Arm(CompanionDefinition definition, ItemDefinition given = null)
    {
        if (IsArmed)
        {
            return;
        }

        IsArmed = true;
        ItemDefinition weapon = given != null ? given : definition.Weapon;
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
        weaponModel = HeldItemView.CreateModel(weapon, grip);
        if (weaponModel != null && IsSheathed)
        {
            wasVisible = weaponModel.activeSelf;
            weaponModel.SetActive(false);
        }
    }
}
