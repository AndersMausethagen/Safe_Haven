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
        // Swimming plays the Swim state (Stylized Idle_Swim and Swim_* on MoveX/MoveZ), PlayerSwimAnimation placing the body in the
        // water; on a controller without it, PlayerSwimAnimation poses the swimmer over the standing idle. The legs don't walk.
        bool swim = IsSwimming;
        bool swimClips = swim && HasSwimState;
        SetFloat(SpeedHash, swim ? 0f : movement.HorizontalSpeed, speedDampTime, deltaTime);
        SetBool(SwimmingHash, swim);
        UpdateArmsInWater(swim, deltaTime);

        Vector3 local = swim && !swimClips ? Vector3.zero : movement.LocalVelocity;
        SetFloat(MoveXHash, local.x, speedDampTime, deltaTime);
        SetFloat(MoveZHash, local.z, speedDampTime, deltaTime);

        // Damped as the direction is: raw, a turn from forward to back played the forward run up to 1.6x fast for a few frames.
        SetFloat(MotionSpeedHash, MotionSpeed(local), speedDampTime, deltaTime);
        UpdateSwingLegs(deltaTime);
        SetBool(HasMoveInputHash, !swim && movement.InputMagnitude > MoveInputThreshold);
        // Off in the air, in the water and in death, where Base's own poses have to show.
        bool alive = health == null || health.IsAlive;
        SetBool(SneakingHash, stealth != null && stealth.IsSneaking && movement.IsGrounded && !swim && alive);
    }

    private bool IsSwimming => swimming != null && swimming.IsSwimming;

    private static readonly int SwimmingHash = Animator.StringToHash("Swimming");
    private static readonly int SwimStateHash = Animator.StringToHash("Swim");
    /// <summary>The controller swims with clips (its Swim state), so PlayerSwimAnimation only places the body.</summary>
    public bool HasSwimState => animator != null && animator.HasState(0, SwimStateHash);

    // The weapon-holding arm layers let the swim's arms show (the held model is hidden in the water, HeldItemView).
    private static readonly string[] HoldingLayers = { "Arms", "Arm Left", "Arm Right", "Hands" };
    private int[] holdingLayers;
    private float[] holdingWeights;
    private float inWater;

    private void UpdateArmsInWater(bool swim, float deltaTime)
    {
        if (holdingLayers == null)
        {
            holdingLayers = new int[HoldingLayers.Length];
            holdingWeights = new float[HoldingLayers.Length];
            for (int i = 0; i < HoldingLayers.Length; i++)
            {
                holdingLayers[i] = animator.GetLayerIndex(HoldingLayers[i]);
                holdingWeights[i] = holdingLayers[i] >= 0 ? animator.GetLayerWeight(holdingLayers[i]) : 0f;
            }
        }

        float was = inWater;
        inWater = Mathf.MoveTowards(inWater, swim && HasSwimState ? 1f : 0f, 4f * deltaTime);
        if (Mathf.Approximately(was, inWater) && inWater <= 0f)
        {
            return;
        }

        for (int i = 0; i < holdingLayers.Length; i++)
        {
            if (holdingLayers[i] >= 0)
            {
                animator.SetLayerWeight(holdingLayers[i], holdingWeights[i] * (1f - inWater));
            }
        }
    }

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
