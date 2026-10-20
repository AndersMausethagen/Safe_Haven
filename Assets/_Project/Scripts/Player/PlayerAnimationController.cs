using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation layer for the player. Reads gameplay state (movement, GameState, interactions) and writes
/// Animator parameters; nothing in the gameplay code depends on this component. Parameters the assigned
/// Animator Controller does not define are skipped, so simpler controllers keep working.
/// Gameplay requests one-shot animations through the Play* methods and sets equipment through the properties.
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public sealed class PlayerAnimationController : MonoBehaviour
{
    // Below this input magnitude the player counts as not asking to move.
    private const float MoveInputThreshold = 0.1f;
    private const float MaxTurnRate = 1080f;

    // Locomotion. MoveX / MoveZ are the facing-relative velocity in m/s (x = right, z = forward) for strafe blend trees.
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    // Playback multiplier for the combat locomotion: above 1 once the player outruns the authored clip speed.
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    // Yaw speed in degrees per second, positive when turning right.
    private static readonly int TurnRateHash = Animator.StringToHash("TurnRate");
    private static readonly int HasMoveInputHash = Animator.StringToHash("HasMoveInput");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    // The GameState value, which selects the exploration or combat locomotion set.
    private static readonly int GameStateHash = Animator.StringToHash("GameState");

    // One-shot actions.
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
    private static readonly int ShieldBashHash = Animator.StringToHash("ShieldBash");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    // Equipment.
    private static readonly int WeaponStanceHash = Animator.StringToHash("WeaponStance");
    private static readonly int CarryHash = Animator.StringToHash("Carry");
    private static readonly int LeftHandedHash = Animator.StringToHash("LeftHanded");

    // Interaction.
    private static readonly int InteractingHash = Animator.StringToHash("Interacting");
    private static readonly int InteractionHash = Animator.StringToHash("Interaction");
    private static readonly int InteractionStageHash = Animator.StringToHash("InteractionStage");

    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float speedDampTime = 0.08f;
    [SerializeField, Min(0f)] private float turnDampTime = 0.1f;
    [SerializeField, Min(0f)] private float fallDelay = 0.12f;
    [Tooltip("Speed in m/s the Run and StrafeRun clips were authored at. Above it the combat locomotion plays faster so the feet don't slide.")]
    [SerializeField, Min(0.1f)] private float runClipSpeed = 3.8f;

    [Header("Equipment")]
    [Tooltip("The stance when no weapon is equipped. An equipped weapon sets its own.")]
    [SerializeField] private WeaponStance weaponStance = WeaponStance.Unarmed;
    [SerializeField] private CarryPose carryPose = CarryPose.None;
    [Tooltip("Uses the left-handed clips for mining and hammering.")]
    [SerializeField] private bool leftHanded;

    private PlayerMovement movement;
    private PlayerCombat combat;
    private PlayerInteractor interactor;
    private Damageable health;
    private PlayerDodge dodge;
    private PlayerSkills skills;
    private GameStateManager gameState;
    private HashSet<int> parameters;
    private float airborneTime;
    private float lastYaw;
    // Starts at 1 so the first attack uses index 0.
    private int attackIndex = 1;

    public WeaponStance WeaponStance
    {
        get => weaponStance;
        set
        {
            weaponStance = value;
            ApplyEquipment();
        }
    }

    public CarryPose CarryPose
    {
        get => carryPose;
        set
        {
            carryPose = value;
            ApplyEquipment();
        }
    }

    public bool LeftHanded
    {
        get => leftHanded;
        set
        {
            leftHanded = value;
            ApplyEquipment();
        }
    }

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        combat = GetComponent<PlayerCombat>();
        interactor = GetComponent<PlayerInteractor>();
        health = GetComponent<Damageable>();
        dodge = GetComponent<PlayerDodge>();
        skills = GetComponent<PlayerSkills>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError("PlayerAnimationController requires an Animator.", this);
            enabled = false;
            return;
        }

        CacheParameters();
    }

    private void OnEnable()
    {
        lastYaw = transform.eulerAngles.y;
        // The equipped weapon decides the stance, so the inspector value below only applies when nothing is equipped.
        if (combat != null && combat.Weapon != null)
        {
            weaponStance = combat.Weapon.Stance;
        }

        ApplyEquipment();
        if (combat != null)
        {
            combat.AttackPerformed += PlayAttack;
            combat.ShieldBashPerformed += PlayShieldBash;
            combat.WeaponChanged += OnWeaponChanged;
        }

        if (interactor != null)
        {
            interactor.InteractionStarted += OnInteractionStarted;
            interactor.InteractionEnded += OnInteractionEnded;
            if (interactor.Current != null)
            {
                OnInteractionStarted(interactor.Current);
            }
        }

        if (health != null)
        {
            health.Damaged += OnDamaged;
        }

        if (dodge != null)
        {
            dodge.Dodged += OnDodged;
        }

        if (skills != null)
        {
            skills.SkillCast += OnSkillCast;
        }

        gameState = GameStateManager.Instance;
        if (gameState == null)
        {
            return;
        }

        gameState.GameStateChanged += OnGameStateChanged;
        ApplyGameState(gameState.CurrentState);
    }

    private void OnDisable()
    {
        if (combat != null)
        {
            combat.AttackPerformed -= PlayAttack;
            combat.ShieldBashPerformed -= PlayShieldBash;
            combat.WeaponChanged -= OnWeaponChanged;
        }

        if (interactor != null)
        {
            interactor.InteractionStarted -= OnInteractionStarted;
            interactor.InteractionEnded -= OnInteractionEnded;
        }

        if (health != null)
        {
            health.Damaged -= OnDamaged;
        }

        if (dodge != null)
        {
            dodge.Dodged -= OnDodged;
        }

        if (skills != null)
        {
            skills.SkillCast -= OnSkillCast;
        }

        if (gameState != null)
        {
            gameState.GameStateChanged -= OnGameStateChanged;
            gameState = null;
        }
    }

    // After PlayerMovement.Update so the animation sees this frame's movement.
    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        UpdateLocomotion(deltaTime);
        UpdateTurn(deltaTime);
        UpdateAirborne(deltaTime);
    }

    private void OnValidate()
    {
        // Lets stance, carry pose and handedness be changed from the Inspector while playing.
        if (Application.isPlaying && parameters != null)
        {
            ApplyEquipment();
        }
    }

    /// <summary>Plays the attack for the current weapon stance. One-handed attacks alternate between the two slashes.</summary>
    public void PlayAttack()
    {
        // A request made while one is still waiting for the animator folds into it, so only the first one alternates.
        bool alreadyQueued = parameters.Contains(AttackHash) && animator.GetBool(AttackHash);
        if (weaponStance <= WeaponStance.OneHanded && !alreadyQueued)
        {
            attackIndex = 1 - attackIndex;
            SetInt(AttackIndexHash, attackIndex);
        }

        SetTrigger(AttackHash);
    }

    public void PlayShieldBash()
    {
        SetTrigger(ShieldBashHash);
    }

    // Skills reuse the weapon swing until they have animations of their own.
    private void OnSkillCast(int slot, SkillDefinition skill)
    {
        if (skill.PlayAttackAnimation)
        {
            PlayAttack();
        }
    }

    public void PlayHit()
    {
        SetTrigger(HitHash);
    }

    // A flinch on every hit that hurts; the killing blow is left to whatever handles death.
    private void OnDamaged(DamageInfo damage, DamageResult result)
    {
        if (result.Damage > 0f && !result.Killed)
        {
            PlayHit();
        }
    }

    // A dodge cancels the swing in gameplay, so a swing still waiting for the animator shouldn't play afterwards.
    private void OnDodged(Vector3 direction)
    {
        if (parameters.Contains(AttackHash))
        {
            animator.ResetTrigger(AttackHash);
        }

        if (parameters.Contains(ShieldBashHash))
        {
            animator.ResetTrigger(ShieldBashHash);
        }
    }

    public void PlayJump()
    {
        SetTrigger(JumpHash);
    }

    /// <summary>Holds the death pose while true; false stands back up.</summary>
    public void SetDead(bool dead)
    {
        SetBool(DeadHash, dead);
    }

    /// <summary>
    /// Selects a phase of the current interaction for animations with several (Fish: 0 waiting, 1 fighting,
    /// 2 catch, 3 hauling). The stage persists until set again; return it to 0 when the phase is over.
    /// </summary>
    public void SetInteractionStage(int stage)
    {
        SetInt(InteractionStageHash, stage);
    }

    private void UpdateLocomotion(float deltaTime)
    {
        SetFloat(SpeedHash, movement.HorizontalSpeed, speedDampTime, deltaTime);

        Vector3 local = movement.LocalVelocity;
        SetFloat(MoveXHash, local.x, speedDampTime, deltaTime);
        SetFloat(MoveZHash, local.z, speedDampTime, deltaTime);

        SetFloat(MotionSpeedHash, Mathf.Max(1f, movement.HorizontalSpeed / runClipSpeed));
        SetBool(HasMoveInputHash, movement.InputMagnitude > MoveInputThreshold);
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
        airborneTime = movement.IsGrounded ? 0f : airborneTime + deltaTime;

        SetBool(GroundedHash, movement.IsGrounded);
        // The delay avoids flicker when walking over small drops and slopes.
        SetBool(FreeFallHash, airborneTime > fallDelay);
    }

    private void OnGameStateChanged(GameState previous, GameState current)
    {
        ApplyGameState(current);
    }

    private void OnWeaponChanged(WeaponDefinition weapon)
    {
        WeaponStance = weapon != null ? weapon.Stance : WeaponStance.Unarmed;
    }

    private void ApplyGameState(GameState state)
    {
        SetInt(GameStateHash, (int)state);
    }

    private void ApplyEquipment()
    {
        if (parameters == null)
        {
            return;
        }

        SetFloat(WeaponStanceHash, (float)weaponStance);
        SetInt(CarryHash, (int)carryPose);
        SetBool(LeftHandedHash, leftHanded);
    }

    private void OnInteractionStarted(Interactable interactable)
    {
        SetInt(InteractionHash, (int)interactable.AnimationType);
        SetInt(InteractionStageHash, 0);
        SetBool(InteractingHash, true);
    }

    private void OnInteractionEnded(Interactable interactable, InteractionResult result)
    {
        SetBool(InteractingHash, false);
    }

    private void CacheParameters()
    {
        parameters = new HashSet<int>();
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            parameters.Add(parameter.nameHash);
        }
    }

    private void SetFloat(int hash, float value)
    {
        if (parameters.Contains(hash))
        {
            animator.SetFloat(hash, value);
        }
    }

    private void SetFloat(int hash, float value, float dampTime, float deltaTime)
    {
        if (parameters.Contains(hash))
        {
            animator.SetFloat(hash, value, dampTime, deltaTime);
        }
    }

    private void SetInt(int hash, int value)
    {
        if (parameters.Contains(hash))
        {
            animator.SetInteger(hash, value);
        }
    }

    private void SetBool(int hash, bool value)
    {
        if (parameters.Contains(hash))
        {
            animator.SetBool(hash, value);
        }
    }

    private void SetTrigger(int hash)
    {
        if (parameters.Contains(hash))
        {
            animator.SetTrigger(hash);
        }
    }

    // Test hooks: right-click the component header while playing.
    [ContextMenu("Debug/Play Attack")]
    private void DebugAttack()
    {
        if (Application.isPlaying)
        {
            PlayAttack();
        }
    }

    [ContextMenu("Debug/Play Shield Bash")]
    private void DebugShieldBash()
    {
        if (Application.isPlaying)
        {
            PlayShieldBash();
        }
    }

    [ContextMenu("Debug/Play Hit")]
    private void DebugHit()
    {
        if (Application.isPlaying)
        {
            PlayHit();
        }
    }

    [ContextMenu("Debug/Play Jump")]
    private void DebugJump()
    {
        if (Application.isPlaying)
        {
            PlayJump();
        }
    }

    [ContextMenu("Debug/Toggle Dead")]
    private void DebugToggleDead()
    {
        if (Application.isPlaying)
        {
            SetDead(!animator.GetBool(DeadHash));
        }
    }

    // Animation event receivers. Add Footstep / Land events to clips and hook audio or VFX here.
    private void OnFootstep(AnimationEvent animationEvent)
    {
    }

    private void OnLand(AnimationEvent animationEvent)
    {
    }
}
