using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Presentation layer for the player. Reads gameplay state (movement, GameState, interactions) and writes
/// Animator parameters; nothing in the gameplay code depends on this component. Parameters the assigned
/// Animator Controller does not define are skipped, so simpler controllers keep working.
/// Gameplay requests one-shot animations through the Play* methods and sets equipment through the properties.
/// <para>
/// The parameters, layers and lifecycle are here; what drives them is in parts by concern: .Locomotion, .Combat, .Work
/// (interactions, work swings, eating) and .Debug (context-menu test hooks).
/// </para>
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public sealed partial class PlayerAnimationController : MonoBehaviour
{
    // Below this input magnitude the player counts as not asking to move.
    private const float MoveInputThreshold = 0.1f;
    private const float MaxTurnRate = 1080f;

    // Locomotion. MoveX / MoveZ are the facing-relative velocity in m/s (x = right, z = forward) for strafe blend trees.
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveZHash = Animator.StringToHash("MoveZ");
    // Which legs Combat Locomotion walks on: 0 the Stylized pack's strafes, 1 the attack pack's own walk (Kevin Iglesias Walk01/Run01).
    private static readonly int SwingLegsHash = Animator.StringToHash("SwingLegs");
    // Playback multiplier for the combat locomotion: above 1 once the player outruns the authored clip speed.
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    // Yaw speed in degrees per second, positive when turning right.
    private static readonly int TurnRateHash = Animator.StringToHash("TurnRate");
    private static readonly int HasMoveInputHash = Animator.StringToHash("HasMoveInput");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    // Crouches the whole body on the Sneak layer, over Base and under every action.
    private static readonly int SneakingHash = Animator.StringToHash("Sneaking");
    // The GameState value, which selects the exploration or combat locomotion set.
    private static readonly int GameStateHash = Animator.StringToHash("GameState");

    // One-shot actions.
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int AttackIndexHash = Animator.StringToHash("AttackIndex");
    private static readonly int ShieldBashHash = Animator.StringToHash("ShieldBash");
    private static readonly int HitHash = Animator.StringToHash("Hit");
    private static readonly int DeadHash = Animator.StringToHash("Dead");

    // Combat actions are played by crossfading into states by name (the attack's Animation State), so the swing starts
    // exactly when the gameplay swing does; ActionSpeed scales them to the swing's speed.
    private static readonly int ActionSpeedHash = Animator.StringToHash("ActionSpeed");
    private static readonly int GuardingHash = Animator.StringToHash("Guarding");
    private static readonly int HasShieldHash = Animator.StringToHash("HasShield");
    private static readonly int EmptyStateHash = Animator.StringToHash("Empty");
    private static readonly int GuardStateHash = Animator.StringToHash("Guard");
    private static readonly int GuardShieldStateHash = Animator.StringToHash("Guard Shield");
    private static readonly int ParryStateHash = Animator.StringToHash("Parry");
    private static readonly int GuardBreakStateHash = Animator.StringToHash("Guard Break");
    private static readonly int KnockbackStateHash = Animator.StringToHash("Knockback");
    private static readonly int GuardHitStateHash = Animator.StringToHash("Guard Hit");
    private static readonly int GuardShieldHitStateHash = Animator.StringToHash("Guard Shield Hit");
    private static readonly int ParryWeaponStateHash = Animator.StringToHash("Parry Weapon");
    private static readonly int GuardStunnedStateHash = Animator.StringToHash("Guard Stunned");
    private static readonly int DrawHipStateHash = Animator.StringToHash("Draw Hip");
    private static readonly int DrawBackStateHash = Animator.StringToHash("Draw Back");
    private static readonly int SheatheHipStateHash = Animator.StringToHash("Sheathe Hip");
    private static readonly int SheatheBackStateHash = Animator.StringToHash("Sheathe Back");
    private PlayerEquipment equipment;
    // A swing's blend in from whatever came before. 0.06 s was two to four frames between very different poses, which snapped when
    // swings followed each other (the user, 2026-11-17: "it snaps in between states and does not flow well"). Only the look: when a
    // blow lands is PlayerCombat's.
    private const float ActionFade = 0.1f;
    // A heavy swing grows out of the light one its press began, so that blend is long enough not to read as a restart.
    private const float HeavyFade = 0.16f;
    // A charged attack let go from the hold is already at the top of its backswing: a short blend, done before its blow.
    private const float ChargedReleaseFade = PlayerCombat.ChargedReleaseSeconds;
    // How slowly the light swing's legs give way when a held press grows it into the planted heavy one.
    // (0.25 read late: the heavy reached half weight 8-11 frames after the grow, review 2026-11-20.)
    private const float GrownLegsFade = 0.18f;
    // Out of a dodge the body is already moving, so a rush attack needn't snap into its first frame.
    private const float RushFade = 0.1f;
    private const float ChainFade = 0.18f;
    // How long before it lands a planted heavy swing brings the legs in, and how fast.
    private const float PlantedStrikeLead = 0.12f;
    private const float PlantedStrikeFade = 0.12f;
    // Moving faster than this, a swing belongs on the arms only, so the legs can walk.
    private const float ArmsOnlySpeed = 0.5f;
    private const float WalkOutFade = 0.12f;
    // A hit worth this share of max health knocks the player back instead of a flinch.
    private const float KnockbackShare = 0.2f;

    // Equipment.
    private static readonly int WeaponStanceHash = Animator.StringToHash("WeaponStance");
    private static readonly int ReadyPoseHash = Animator.StringToHash("ReadyPose");
    /// <summary>ReadyPose for a weapon with a pose of its own (WeaponReadyPose.Staff); otherwise it is the stance.</summary>
    public const float StaffReadyPose = 5f;
    private static readonly int CarryHash = Animator.StringToHash("Carry");
    private static readonly int LeftHandedHash = Animator.StringToHash("LeftHanded");

    // Interaction.
    private static readonly int InteractingHash = Animator.StringToHash("Interacting");
    private static readonly int InteractionHash = Animator.StringToHash("Interaction");
    private static readonly int InteractionStageHash = Animator.StringToHash("InteractionStage");
    // Swings at work: the tool waits raised and comes down when the player swings, not on a loop of its own.
    private static readonly int WorkSpeedHash = Animator.StringToHash("WorkSpeed");
    private static readonly int[] WorkStates =
    {
        Animator.StringToHash("Mine Wall Right"), Animator.StringToHash("Mine Wall Left"),
        Animator.StringToHash("Mine Ground Right"), Animator.StringToHash("Mine Ground Left"),
    };
    private const string InteractionLayer = "Interaction";
    private const float WorkReturnFade = 0.06f;

    [SerializeField] private Animator animator;
    [SerializeField, Min(0f)] private float speedDampTime = 0.08f;
    [SerializeField, Min(0f)] private float turnDampTime = 0.1f;
    [SerializeField, Min(0f)] private float fallDelay = 0.12f;
    [Tooltip("Speed in m/s the forward and sideways run clips were authored at (their feet's ground speed). Above it the locomotion plays faster so the feet don't slide.")]
    [SerializeField, Min(0.1f)] private float runClipSpeed = 4.8f;
    [Tooltip("Speed in m/s of the backpedal run clips, which take shorter steps.")]
    [SerializeField, Min(0.1f)] private float backRunClipSpeed = 2.1f;
    [Tooltip("Fastest the locomotion plays to keep up. Beyond it the feet slide a little rather than spin.")]
    [SerializeField, Min(1f)] private float maxMotionSpeed = 1.6f;

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
    private PlayerGuard guard;
    private PlayerEating eating;
    private PlayerSwimming swimming;
    private PlayerStealth stealth;
    private static readonly int ConsumeStateHash = Animator.StringToHash("Consume");
    private int upperBodyLayer = -1;
    private int attackArmsLayer = -1;
    private int fullBodyLayer = -1;
    private GameStateManager gameState;
    private WorkPhase workPhase;
    private float workContact;
    private float workHold;
    // Speed of the tool's rise to the raised point. 1 plays the clip as made; hold-to-raise gathering sets it from how
    // long the button is held (0 keeps the tool down until the button is pressed).
    private float workRaiseSpeed = 1f;
    private float workTime;
    private HashSet<int> parameters;
    private float airborneTime;
    private float lastYaw;
    // Starts at 1 so the first attack uses index 0.
    private int attackIndex = 1;
    private float swingSpeed = 1f;
    // The heavy swing winding up on the upper body, by state; 0 when none is.
    private int plantedSwing;
    // How far into its wind-up a heavy swing grown out of a light one started (PlayerCombat.HeavyGrownFrom); 0 for any other swing.
    private float grownFrom;
    // Seconds the current swing's wind-up lasts as it started (0: not shaped), for SwingWindup.
    private float windupSpan;

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
        guard = GetComponent<PlayerGuard>();
        equipment = GetComponent<PlayerEquipment>();
        eating = GetComponent<PlayerEating>();
        swimming = GetComponent<PlayerSwimming>();
        stealth = GetComponent<PlayerStealth>();
        animator = animator != null ? animator : GetComponentInChildren<Animator>();

        if (animator == null)
        {
            Debug.LogError("PlayerAnimationController requires an Animator.", this);
            enabled = false;
            return;
        }

        CacheParameters();
        upperBodyLayer = animator.GetLayerIndex("Upper Body");
        attackArmsLayer = animator.GetLayerIndex("Attack Arms");
        fullBodyLayer = animator.GetLayerIndex("Combat Full Body");
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
            combat.SwingStarted += OnSwingStarted;
            combat.SwingLanded += OnSwingLanded;
            combat.WeaponChanged += OnWeaponChanged;
            combat.KnockedDown += OnKnockedDown;
            combat.Flinched += OnFlinched;
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
            dodge.DodgeStarted += OnDodgeStarted;
        }

        if (eating != null)
        {
            eating.Started += OnEatingStarted;
            eating.Ended += OnEatingEnded;
        }

        if (skills != null)
        {
            skills.SkillCast += OnSkillCast;
        }

        if (equipment != null)
        {
            equipment.WeaponMoving += OnWeaponMoving;
            equipment.HandMoveCancelled += OnHandMoveCancelled;
        }

        if (guard != null)
        {
            guard.GuardChanged += OnGuardChanged;
            guard.Parried += OnParried;
            guard.Blocked += OnBlocked;
            guard.Broken += OnGuardBroken;
        }

        gameState = GameStateManager.Current;
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
            combat.SwingStarted -= OnSwingStarted;
            combat.KnockedDown -= OnKnockedDown;
            combat.Flinched -= OnFlinched;
            combat.SwingLanded -= OnSwingLanded;
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
            dodge.DodgeStarted -= OnDodgeStarted;
        }

        if (eating != null)
        {
            eating.Started -= OnEatingStarted;
            eating.Ended -= OnEatingEnded;
        }

        if (skills != null)
        {
            skills.SkillCast -= OnSkillCast;
        }

        if (equipment != null)
        {
            equipment.WeaponMoving -= OnWeaponMoving;
            equipment.HandMoveCancelled -= OnHandMoveCancelled;
        }

        if (guard != null)
        {
            guard.GuardChanged -= OnGuardChanged;
            guard.Parried -= OnParried;
            guard.Blocked -= OnBlocked;
            guard.Broken -= OnGuardBroken;
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
        ClearUnderFullBody();
        ApplyPendingEquipment();
        UpdateWorkSwing();
        float deltaTime = Time.deltaTime;
        UpdateLocomotion(deltaTime);
        UpdateTurn(deltaTime);
        UpdateAirborne(deltaTime);
        ResolveHitReaction();
        UpdateGuardStun();
        UpdateSwingSpeed();
        WalkOutOfSwing();
    }

    private void OnValidate()
    {
        // Lets stance, carry pose and handedness be changed from the Inspector while playing.
        if (Application.isPlaying && parameters != null)
        {
            ApplyEquipment();
        }
    }

    private void CrossFadeUpper(int hash, float fade)
    {
        if (upperBodyLayer >= 0 && animator.HasState(upperBodyLayer, hash))
        {
            SetFloat(ActionSpeedHash, 1f);
            // The full-body layer sits above it: a swing still playing there is handed over, the reaction going in beneath it while
            // only the swing fades (PlayerAnimationController.Layers).
            UnderFullBody(upperBodyLayer, hash, fade, 0f, fade);
        }
    }

    /// <summary>Holds the death pose while true; false stands back up.</summary>
    public void SetDead(bool dead)
    {
        SetBool(DeadHash, dead);
    }

    private void OnGameStateChanged(GameState previous, GameState current)
    {
        ApplyGameState(current);
    }

    // A weapon taken out or put away by hand (Z) changes the stance's pose at the hand's grab, not at the press: it snapped the
    // ready pose 2.7-3.0 in one frame (review 2026-11-20). Otherwise the next LateUpdate.
    private bool equipmentPending;

    private void OnWeaponChanged(WeaponDefinition weapon)
    {
        weaponStance = weapon != null ? weapon.Stance : WeaponStance.Unarmed;
        equipmentPending = true;
    }

    private void ApplyPendingEquipment()
    {
        if (equipmentPending && (equipment == null || !equipment.HandMoving))
        {
            equipmentPending = false;
            ApplyEquipment();
        }
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
        // The pose it stands ready and guards in: the stance's, or the weapon's own (the staff).
        WeaponDefinition weapon = combat != null ? combat.Weapon : null;
        SetFloat(ReadyPoseHash, weapon != null && weapon.ReadyPose == WeaponReadyPose.Staff ? StaffReadyPose : (float)weaponStance);
        SetInt(CarryHash, (int)carryPose);
        SetBool(LeftHandedHash, leftHanded);
    }

    private enum WorkPhase
    {
        Rising,
        Holding,
        Following,
        /// <summary>Let go too soon: the tool goes back down to the ready pose, no blow.</summary>
        Lowering,
    }

    /// <summary>The tool is at the ready or rising (not following through a blow or going back down): a new hold may raise it.</summary>
    public bool WorkReady => workContact <= 0f || workPhase == WorkPhase.Rising || workPhase == WorkPhase.Holding;

    /// <summary>Seconds of the swing clip from its start to the raised point (where the tool waits to strike).</summary>
    public float WorkRaiseSeconds => workHold;

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

    /// <summary>Raised by the clips' Footstep animation events that carry weight in the current blend.</summary>
    public event System.Action Footstep;

    /// <summary>Raised by the clips' Land animation event.</summary>
    public event System.Action Landed;
}
