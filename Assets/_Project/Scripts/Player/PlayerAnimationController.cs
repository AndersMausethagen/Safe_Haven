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
    private const float ActionFade = 0.06f;
    // A heavy swing grows out of the light one its press began, so that blend is long enough not to read as a restart.
    private const float HeavyFade = 0.16f;
    // Out of a dodge the body is already moving, so a rush attack needn't snap into its first frame.
    private const float RushFade = 0.1f;
    // How long before it lands a planted heavy swing brings the legs in, and how fast.
    private const float PlantedStrikeLead = 0.12f;
    private const float PlantedStrikeFade = 0.07f;
    // Moving faster than this, a swing belongs on the arms only, so the legs can walk.
    private const float ArmsOnlySpeed = 0.5f;
    private const float WalkOutFade = 0.12f;
    // A hit worth this share of max health knocks the player back instead of a flinch.
    private const float KnockbackShare = 0.2f;

    // Equipment.
    private static readonly int WeaponStanceHash = Animator.StringToHash("WeaponStance");
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

        if (guard != null)
        {
            guard.GuardChanged += OnGuardChanged;
            guard.Parried += OnParried;
            guard.Broken += OnGuardBroken;
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
            combat.SwingStarted -= OnSwingStarted;
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

        if (guard != null)
        {
            guard.GuardChanged -= OnGuardChanged;
            guard.Parried -= OnParried;
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
        UpdateWorkSwing();
        float deltaTime = Time.deltaTime;
        UpdateLocomotion(deltaTime);
        UpdateTurn(deltaTime);
        UpdateAirborne(deltaTime);
        UpdateSwingSpeed();
        WalkOutOfSwing();
    }

    // A swing's shape: a slow build-up, a fast strike, and it stands at the top of its wind-up while a heavy swing charges.
    private void UpdateSwingSpeed()
    {
        if (combat == null || !combat.IsWindingUp)
        {
            ReleasePlantedSwing();
            return;
        }

        AttackDefinition attack = combat.ActiveAttack;
        if (attack == null || string.IsNullOrEmpty(attack.AnimationState))
        {
            return;
        }

        float speed = combat.IsCharging ? 0f : combat.TimeToHit <= attack.StrikeLead ? attack.StrikeSpeed : attack.AnimationSpeed;
        SetFloat(ActionSpeedHash, speed * swingSpeed);
        if (plantedSwing != 0 && !combat.IsCharging && combat.TimeToHit <= Mathf.Max(attack.StrikeLead, PlantedStrikeLead))
        {
            StrikeWithWholeBody();
        }
    }

    // A heavy swing winds up and charges on the upper body, with the feet planted on the ground: its clip may stride or leap, and
    // holding a pose like that for as long as the button is held leaves the player hanging in the air. The legs join in for the strike.
    private void StrikeWithWholeBody()
    {
        int upper = upperBodyLayer;
        AnimatorStateInfo state = animator.IsInTransition(upper) && animator.GetNextAnimatorStateInfo(upper).shortNameHash == plantedSwing
            ? animator.GetNextAnimatorStateInfo(upper) : animator.GetCurrentAnimatorStateInfo(upper);
        AnimatorClipInfo[] clips = animator.IsInTransition(upper) ? animator.GetNextAnimatorClipInfo(upper) : animator.GetCurrentAnimatorClipInfo(upper);
        float clipTime = clips.Length > 0 ? state.normalizedTime * clips[0].clip.length : 0f;
        if (fullBodyLayer >= 0 && animator.HasState(fullBodyLayer, plantedSwing))
        {
            animator.CrossFadeInFixedTime(plantedSwing, PlantedStrikeFade, fullBodyLayer, clipTime);
            animator.CrossFadeInFixedTime(EmptyStateHash, PlantedStrikeFade, upper);
        }

        plantedSwing = 0;
    }

    // A heavy swing lost before it struck (a flinch): the arms mustn't stay standing at the top of its wind-up.
    private void ReleasePlantedSwing()
    {
        if (plantedSwing == 0)
        {
            return;
        }

        plantedSwing = 0;
        if (dodge == null || !dodge.IsDodging)
        {
            SetFloat(ActionSpeedHash, 1f);
            if (animator.GetCurrentAnimatorStateInfo(upperBodyLayer).shortNameHash != KnockbackStateHash)
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, HeavyFade, upperBodyLayer);
            }
        }
    }

    // A blow that has landed gives the legs back: when the player walks off, the arms finish the swing over them from
    // where the body had got to, instead of a whole-body swing skating along the ground. That includes a swing whose
    // gameplay has ended but whose animation tail is still on the whole body.
    private void WalkOutOfSwing()
    {
        if (combat == null || combat.IsWindingUp || movement.HorizontalSpeed <= ArmsOnlySpeed
            || fullBodyLayer < 0 || attackArmsLayer < 0 || animator.IsInTransition(fullBodyLayer))
        {
            return;
        }

        AnimatorStateInfo swing = animator.GetCurrentAnimatorStateInfo(fullBodyLayer);
        if (swing.shortNameHash == EmptyStateHash)
        {
            return;
        }

        // Swings are the full-body states the arms layer also has; dodges and hit reactions are left alone.
        bool isSwing = animator.HasState(attackArmsLayer, swing.shortNameHash);
        if (!isSwing && !combat.IsAttacking)
        {
            return;
        }

        if (isSwing)
        {
            animator.CrossFadeInFixedTime(swing.shortNameHash, WalkOutFade, attackArmsLayer, swing.normalizedTime * swing.length);
        }

        animator.CrossFadeInFixedTime(EmptyStateHash, WalkOutFade, fullBodyLayer);
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

    // A swing with its own animation state plays it at once: with the whole body (the legs step into the blow, which is
    // what reads from above), as a swing plants the feet. Only if the settings let the player run through a wind-up does it
    // go on the arms. One without a state uses the stance's older triggered attack.
    private void OnSwingStarted(SwingInfo swing)
    {
        string stateName = swing.Attack != null ? swing.Attack.AnimationState : null;
        int standing = fullBodyLayer >= 0 ? fullBodyLayer : upperBodyLayer;
        bool onTheRun = CombatTechniqueSettings.Current.WindupMoveSpeed > 0f && movement.HorizontalSpeed > ArmsOnlySpeed;
        int layer = onTheRun && attackArmsLayer >= 0 ? attackArmsLayer : standing;
        int hash = string.IsNullOrEmpty(stateName) ? 0 : Animator.StringToHash(stateName);
        plantedSwing = 0;
        if (swing.Kind == SwingKind.Heavy && hash != 0 && upperBodyLayer >= 0 && animator.HasState(upperBodyLayer, hash))
        {
            layer = upperBodyLayer;
            plantedSwing = hash;
        }

        if (hash == 0 || layer < 0 || !animator.HasState(layer, hash))
        {
            if (swing.Kind == SwingKind.ShieldBash)
            {
                PlayShieldBash();
            }
            else
            {
                PlayAttack();
            }

            return;
        }

        swingSpeed = swing.Speed;
        SetFloat(ActionSpeedHash, swing.Attack.AnimationSpeed * swing.Speed);
        float fade = swing.Kind == SwingKind.Heavy ? HeavyFade : swing.Kind == SwingKind.Rush ? RushFade : ActionFade;
        animator.CrossFadeInFixedTime(hash, fade, layer, 0f);
        foreach (int other in new[] { upperBodyLayer, attackArmsLayer, fullBodyLayer })
        {
            if (other >= 0 && other != layer && animator.HasState(other, EmptyStateHash))
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, fade, other);
            }
        }
    }

    // The wind-up plays slower so it can be read; from the hit on, the strike plays at its own speed and snaps through.
    private void OnSwingLanded(SwingInfo swing, int hits)
    {
        if (swing.Attack != null && !string.IsNullOrEmpty(swing.Attack.AnimationState))
        {
            SetFloat(ActionSpeedHash, swing.Attack.FollowThroughSpeed * swing.Speed);
        }
    }

    private void OnGuardChanged(bool guarding)
    {
        SetBool(HasShieldHash, combat != null && combat.HasShield);
        SetBool(GuardingHash, guarding);
        if (guarding)
        {
            // Straight into the guard, cutting off whatever swing was ending.
            CrossFadeUpper(combat != null && combat.HasShield ? GuardShieldStateHash : GuardStateHash, 0.08f);
            foreach (int other in new[] { attackArmsLayer, fullBodyLayer })
            {
                if (other >= 0 && animator.HasState(other, EmptyStateHash))
                {
                    animator.CrossFadeInFixedTime(EmptyStateHash, 0.08f, other);
                }
            }
        }
    }

    private void OnParried(DamageInfo damage) => CrossFadeUpper(ParryStateHash, 0.04f);

    // The dodge the gear allows (quickstep, roll, heavy roll) plays with the whole body; anything on the other layers stops.
    private void OnDodgeStarted(DodgeStyle style, DodgeShape shape)
    {
        int hash = string.IsNullOrEmpty(shape.animationState) ? 0 : Animator.StringToHash(shape.animationState);
        if (fullBodyLayer < 0 || hash == 0 || !animator.HasState(fullBodyLayer, hash))
        {
            return;
        }

        SetFloat(ActionSpeedHash, shape.animationSpeed * (dodge != null ? dodge.AnimationPace : 1f));
        animator.CrossFadeInFixedTime(hash, 0.04f, fullBodyLayer, 0f);
        foreach (int other in new[] { upperBodyLayer, attackArmsLayer })
        {
            if (other >= 0 && animator.HasState(other, EmptyStateHash))
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, 0.04f, other);
            }
        }
    }

    private void OnEatingStarted(ItemDefinition item) => CrossFadeUpper(ConsumeStateHash, 0.15f);

    private void OnEatingEnded(ItemDefinition item, bool eaten)
    {
        if (!eaten && upperBodyLayer >= 0 && animator.HasState(upperBodyLayer, EmptyStateHash))
        {
            animator.CrossFadeInFixedTime(EmptyStateHash, 0.12f, upperBodyLayer);
        }
    }

    private void OnGuardBroken(float seconds) => CrossFadeUpper(GuardBreakStateHash, 0.05f);

    private void CrossFadeUpper(int hash, float fade)
    {
        if (upperBodyLayer >= 0 && animator.HasState(upperBodyLayer, hash))
        {
            SetFloat(ActionSpeedHash, 1f);
            animator.CrossFadeInFixedTime(hash, fade, upperBodyLayer, 0f);
            // The full-body layer sits above it, so a swing still playing there would hide the reaction.
            if (fullBodyLayer >= 0 && animator.HasState(fullBodyLayer, EmptyStateHash))
            {
                animator.CrossFadeInFixedTime(EmptyStateHash, fade, fullBodyLayer);
            }
        }
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
        if (result.Damage <= 0f || result.Killed || result.Blocked || damage.IsOverTime)
        {
            return;
        }

        if (health != null && result.Damage >= health.MaxHealth * KnockbackShare && upperBodyLayer >= 0
            && animator.HasState(upperBodyLayer, KnockbackStateHash))
        {
            CrossFadeUpper(KnockbackStateHash, 0.05f);
            return;
        }

        PlayHit();
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
        // Swimming is posed by PlayerSwimAnimation over the standing idle, so the legs don't walk under the water.
        bool swim = IsSwimming;
        SetFloat(SpeedHash, swim ? 0f : movement.HorizontalSpeed, speedDampTime, deltaTime);

        Vector3 local = swim ? Vector3.zero : movement.LocalVelocity;
        SetFloat(MoveXHash, local.x, speedDampTime, deltaTime);
        SetFloat(MoveZHash, local.z, speedDampTime, deltaTime);

        SetFloat(MotionSpeedHash, MotionSpeed(local));
        SetBool(HasMoveInputHash, !swim && movement.InputMagnitude > MoveInputThreshold);
        // Off in the air, in the water and in death, where Base's own poses have to show.
        bool alive = health == null || health.IsAlive;
        SetBool(SneakingHash, stealth != null && stealth.IsSneaking && movement.IsGrounded && !swim && alive);
    }

    private bool IsSwimming => swimming != null && swimming.IsSwimming;

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
        workContact = 0f;
        SetFloat(WorkSpeedHash, 1f);
    }

    private enum WorkPhase
    {
        Rising,
        Holding,
        Following,
    }

    /// <summary>Hands this interaction's swings to the player: the tool rises and waits instead of swinging by itself.
    /// Does nothing for an animation that isn't a swing.</summary>
    public void HoldWorkSwings(InteractionAnimation animation)
    {
        GatheringMinigameSettings settings = GatheringMinigameSettings.Current;
        workContact = animator != null ? settings.SwingContact(animation) : 0f;
        workHold = Mathf.Max(0f, workContact - settings.SwingWindUp);
        workPhase = WorkPhase.Rising;
        workTime = 0f;
        workRaiseSpeed = 1f;
    }

    /// <summary>Seconds of the swing clip from its start to the raised point (where the tool waits to strike).</summary>
    public float WorkRaiseSeconds => workHold;

    /// <summary>How fast the tool rises to the raised point (1 = as the clip was made, 0 = it waits where it is).</summary>
    public void SetWorkRaise(float speed)
    {
        workRaiseSpeed = Mathf.Max(0f, speed);
        if (workPhase == WorkPhase.Rising)
        {
            SetFloat(WorkSpeedHash, workRaiseSpeed);
        }
    }

    /// <summary>Brings the tool down. Returns the seconds until it lands, or 0 when swings aren't being held.</summary>
    public float StrikeWork()
    {
        if (workContact <= 0f)
        {
            return 0f;
        }

        int layer = animator.GetLayerIndex(InteractionLayer);
        float wait = workContact - workHold;
        if (workPhase != WorkPhase.Holding && layer >= 0)
        {
            // Still on its way back up: to the raised point at once, so a quick swing isn't made to wait for the last one.
            int state = animator.GetCurrentAnimatorStateInfo(layer).shortNameHash;
            if (System.Array.IndexOf(WorkStates, state) >= 0)
            {
                animator.CrossFadeInFixedTime(state, WorkReturnFade, layer, workHold);
                wait += WorkReturnFade;
            }
        }

        SetFloat(WorkSpeedHash, 1f);
        workPhase = WorkPhase.Following;
        workTime = workHold;
        return wait;
    }

    private void UpdateWorkSwing()
    {
        // A held clip has stopped, and reports its length as infinite: there is nothing to watch until it is released.
        if (workContact <= 0f || workPhase == WorkPhase.Holding)
        {
            return;
        }

        int layer = animator.GetLayerIndex(InteractionLayer);
        if (layer < 0 || animator.IsInTransition(layer))
        {
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
        if (System.Array.IndexOf(WorkStates, state.shortNameHash) < 0)
        {
            return;
        }

        float time = state.normalizedTime % 1f * state.length;
        if (workPhase == WorkPhase.Following && time < workTime)
        {
            workPhase = WorkPhase.Rising;
            SetFloat(WorkSpeedHash, workRaiseSpeed);
        }

        if (workPhase == WorkPhase.Rising && time >= workHold)
        {
            workPhase = WorkPhase.Holding;
            SetFloat(WorkSpeedHash, 0f);
        }

        workTime = time;
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

    /// <summary>Raised by the clips' Footstep animation events that carry weight in the current blend.</summary>
    public event System.Action Footstep;

    /// <summary>Raised by the clips' Land animation event.</summary>
    public event System.Action Landed;

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
