using UnityEngine;

/// <summary>
/// The player moving: speed and direction through the blend tree, turning, the fall after a short grace, the jump, and
/// the footstep and landing events the clips raise. Part of PlayerAnimationController, in its own file.
/// </summary>
public sealed partial class PlayerAnimationController
{
    public void PlayJump()
    {
        SetTrigger(JumpHash);
    }

    private void UpdateLocomotion(float deltaTime)
    {
        // Swimming is posed by PlayerSwimAnimation over the standing idle, so the legs don't walk under the water.
        bool swim = IsSwimming;
        SetFloat(SpeedHash, swim ? 0f : movement.HorizontalSpeed, speedDampTime, deltaTime);

        Vector3 local = swim ? Vector3.zero : movement.LocalVelocity;
        SetFloat(MoveXHash, local.x, speedDampTime, deltaTime);
        SetFloat(MoveZHash, local.z, speedDampTime, deltaTime);

        SetFloat(MotionSpeedHash, MotionSpeed(local));
        UpdateSwingLegs(deltaTime);
        SetBool(HasMoveInputHash, !swim && movement.InputMagnitude > MoveInputThreshold);
        // Off in the air, in the water and in death, where Base's own poses have to show.
        bool alive = health == null || health.IsAlive;
        SetBool(SneakingHash, stealth != null && stealth.IsSneaking && movement.IsGrounded && !swim && alive);
    }

    private bool IsSwimming => swimming != null && swimming.IsSwimming;

    // Seconds the attack pack's legs stay after a swing ends, while its arms finish over them (WalkOutOfSwing), and how fast the
    // legs change over.
    private const float SwingLegsHold = 0.35f;
    private const float SwingLegsDamp = 0.08f;
    private float swingLegsUntil = float.NegativeInfinity;

    // Combat Locomotion's legs (each stance's Combat Legs tree): the attack pack's own walk under a swing, as its author mixes them
    // (another pack's walk under its swings glitched), and the Stylized pack's strafes the rest of the time, which read better
    // circling a foe locked on (the user, 2026-11-16: "all strafing looks weird while locked on").
    private void UpdateSwingLegs(float deltaTime)
    {
        if (combat != null && combat.IsAttacking)
        {
            swingLegsUntil = Time.time + SwingLegsHold;
        }

        SetFloat(SwingLegsHash, Time.time < swingLegsUntil ? 1f : 0f, SwingLegsDamp, deltaTime);
    }

    // Playback rate that makes the run clip for this direction cover the ground actually moved: the clips run at
    // runClipSpeed up to sideways and at backRunClipSpeed from 45° behind, as the blend trees place them.
    private float MotionSpeed(Vector3 local)
    {
        float speed = new Vector2(local.x, local.z).magnitude;
        if (speed < 0.01f)
        {
            return 1f;
        }

        float angle = Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg);
        float clipSpeed = Mathf.Lerp(runClipSpeed, backRunClipSpeed, Mathf.InverseLerp(90f, 135f, angle));
        return Mathf.Clamp(speed / clipSpeed, 1f, maxMotionSpeed);
    }

    private void UpdateTurn(float deltaTime)
    {
        float yaw = transform.eulerAngles.y;
        if (deltaTime > 0f)
        {
            float turnRate = Mathf.Clamp(Mathf.DeltaAngle(lastYaw, yaw) / deltaTime, -MaxTurnRate, MaxTurnRate);
            SetFloat(TurnRateHash, turnRate, turnDampTime, deltaTime);
        }

        lastYaw = yaw;
    }

    private void UpdateAirborne(float deltaTime)
    {
        // Held up by the water counts as on the ground: no falling pose.
        bool grounded = movement.IsGrounded || IsSwimming;
        airborneTime = grounded ? 0f : airborneTime + deltaTime;

        SetBool(GroundedHash, grounded);
        // The delay avoids flicker when walking over small drops and slopes.
        SetBool(FreeFallHash, airborneTime > fallDelay);
    }

    // Animation event receivers. In a blend tree every clip fires its events, so only the ones that carry weight count.
    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            Footstep?.Invoke();
        }
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        Landed?.Invoke();
    }
}
