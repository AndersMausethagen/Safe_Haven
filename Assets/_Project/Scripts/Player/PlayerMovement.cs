using UnityEngine;

/// <summary>
/// Physical movement of the player. Input comes in through SetMovementInput and
/// the resulting motion is exposed as read-only state for other systems
/// (animation, audio, VFX) to observe.
/// Two independent gates apply: the global GameState (speed/turn multipliers from the
/// settings asset) and PlayerStateMachine.CanMove (player-specific actions such as interacting). Dash overrides input
/// for a moment (the dodge, see PlayerDodge). While floating (swimming, see PlayerSwimming) buoyancy replaces gravity,
/// holding the feet at the height asked for, and turning is slower.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerStateMachine))]
public sealed class PlayerMovement : MonoBehaviour
{
    private const float MovingSpeedThreshold = 0.01f;

    [SerializeField] private PlayerMovementSettings settings;
    private Combatant combatant;

    private CharacterController characterController;
    private PlayerStateMachine stateMachine;
    private GameStateManager gameState;

    private GameState currentGameState;
    private float verticalVelocity;
    private Vector3 movementInput;
    private Vector3 aimDirection;
    private bool hasAimDirection;
    private Vector3 focusDirection;
    private bool hasFocus;
    private Vector3 horizontalVelocity;
    private Vector3 dashVelocity;
    private float dashUntil = -1f;
    private Vector3 lungeVelocity;
    private float lungeUntil = -1f;
    private readonly System.Collections.Generic.Dictionary<object, float> speedScales = new System.Collections.Generic.Dictionary<object, float>();
    private float speedScale = 1f;
    private bool floating;
    private float floatHeight;
    private Transform seat;
    private float seatTurn;
    private Vector3 seatOffset;

    public PlayerMovementSettings Settings => settings;
    /// <summary>True while the water holds the player up (<see cref="FloatAt"/>): gravity is off.</summary>
    public bool IsFloating => floating;
    /// <summary>True while sitting on something that moves (a boat): it carries the character, and input is its to read.</summary>
    public bool IsSeated => seat != null;
    /// <summary>True during a dash (a dodge): movement input is ignored and the character slides at the dash velocity.</summary>
    public bool IsDashing => dashUntil >= 0f;
    /// <summary>Holds the way the character faces (a swing winding up): neither the aim nor travel turns it.</summary>
    public bool FacingLocked { get; set; }
    /// <summary>The requested movement direction (camera-relative, 0-1 long), before any gating.</summary>
    public Vector3 MovementInput => movementInput;

    /// <summary>The move keys or stick as pressed: x is left and right, y is forward and back. For what is steered from its own
    /// seat (a boat), where the camera has no say in which way is forward.</summary>
    public Vector2 MoveKeys { get; private set; }

    public void SetMoveKeys(Vector2 keys) => MoveKeys = keys;
    public Vector3 HorizontalVelocity => horizontalVelocity;
    public float HorizontalSpeed => horizontalVelocity.magnitude;
    /// <summary>Horizontal velocity relative to the character's facing (x = strafe, z = forward), for strafe blend trees.</summary>
    public Vector3 LocalVelocity => transform.InverseTransformDirection(horizontalVelocity);
    public float VerticalVelocity => verticalVelocity;
    public bool IsMoving => HorizontalSpeed > MovingSpeedThreshold;
    public bool IsGrounded { get; private set; } = true;

    /// <summary>Analog magnitude (0-1) of the requested movement, before any gating.</summary>
    public float InputMagnitude => movementInput.magnitude;

    /// <summary>The way the cursor or crosshair points, whatever the focus says; the way the character faces when there is none.</summary>
    public Vector3 RawAimDirection => hasAimDirection ? aimDirection : FacingDirection;

    private Vector3 FacingDirection
    {
        get
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }
    }

    /// <summary>The horizontal direction the character means to act in: toward the enemy it is focused on (PlayerFocusTarget), else
    /// toward the aim point (the cursor), or the way it faces when there is none.</summary>
    public Vector3 AimDirection
    {
        get
        {
            if (hasFocus)
            {
                return focusDirection;
            }

            if (hasAimDirection)
            {
                return aimDirection;
            }

            return FacingDirection;
        }
    }

    private void Awake()
    {
        combatant = GetComponent<Combatant>();
        characterController = GetComponent<CharacterController>();
        stateMachine = GetComponent<PlayerStateMachine>();

        if (settings == null)
        {
            Debug.LogError("PlayerMovement requires a PlayerMovementSettings asset.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        gameState = GameStateManager.Current;
        if (gameState == null)
        {
            Debug.LogWarning("PlayerMovement found no GameStateManager; using Exploration movement values.", this);
            return;
        }

        currentGameState = gameState.CurrentState;
        gameState.GameStateChanged += OnGameStateChanged;
    }

    private void OnDisable()
    {
        if (gameState != null)
        {
            gameState.GameStateChanged -= OnGameStateChanged;
            gameState = null;
        }

        horizontalVelocity = Vector3.zero;
        hasAimDirection = false;
        hasFocus = false;
        dashUntil = -1f;
        lungeUntil = -1f;
        speedScales.Clear();
        speedScale = 1f;
        floating = false;
        FacingLocked = false;
        seat = null;
    }

    private void Update()
    {
        if (seat != null)
        {
            transform.SetPositionAndRotation(seat.TransformPoint(seatOffset), seat.rotation * Quaternion.Euler(0f, seatTurn, 0f));
            return;
        }

        UpdateHorizontalVelocity();
        ApplyGravity();
        Move();
    }

    public void SetMovementInput(Vector3 direction)
    {
        direction.y = 0f;
        movementInput = Vector3.ClampMagnitude(direction, 1f);
    }

    /// <summary>
    /// Slides the character at a fixed velocity for a moment, ignoring input, and faces the way it goes. Used by
    /// PlayerDodge; still blocked by walls through the CharacterController.
    /// </summary>
    public void Dash(Vector3 velocity, float duration)
    {
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 0.0001f || duration <= 0f)
        {
            return;
        }

        dashVelocity = velocity;
        dashUntil = Time.time + duration;
        transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
    }

    /// <summary>Ends a dash early (an attack cutting the end of a dodge short).</summary>
    public void EndDash() => dashUntil = -1f;

    /// <summary>Turns a dash in progress (a dodge curving around an enemy), facing the way it now goes.</summary>
    public void SteerDash(Vector3 velocity)
    {
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.0001f)
        {
            dashVelocity = velocity;
            transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
        }
    }

    /// <summary>
    /// A short push on top of normal movement, without counting as a dash (an attack stepping into its swing). Faces
    /// the way it pushes.
    /// </summary>
    public void Lunge(Vector3 velocity, float duration)
    {
        velocity.y = 0f;
        if (velocity.sqrMagnitude < 0.0001f || duration <= 0f)
        {
            return;
        }

        lungeVelocity = velocity;
        lungeUntil = Time.time + duration;
    }

    /// <summary>Stops a lunge still running (a swing lost before its blow).</summary>
    public void CancelLunge() => lungeUntil = -1f;

    /// <summary>Scales movement speed while <paramref name="source"/> asks (guarding, a stun, heavy gear); 1 removes it.
    /// Several sources multiply.</summary>
    public void SetSpeedScale(object source, float scale)
    {
        if (Mathf.Abs(scale - 1f) < 0.001f)
        {
            speedScales.Remove(source);
        }
        else
        {
            speedScales[source] = Mathf.Max(0f, scale);
        }

        speedScale = 1f;
        foreach (float value in speedScales.Values)
        {
            speedScale *= value;
        }
    }

    /// <summary>Holds the feet at <paramref name="feetHeight"/> instead of letting gravity act, pushed there by the
    /// settings' Buoyancy (so a jump in dips under first). Asked every frame by PlayerSwimming while swimming.</summary>
    public void FloatAt(float feetHeight)
    {
        floating = true;
        floatHeight = feetHeight;
    }

    /// <summary>Sits on <paramref name="at"/>: the controller is switched off and the character goes where the seat goes,
    /// turned <paramref name="turn"/> degrees from the seat's facing (a rider climbing on faces the horse's side).</summary>
    public void SitAt(Transform at, Vector3 offset, float turn = 0f)
    {
        seat = at;
        seatOffset = offset;
        seatTurn = turn;
        horizontalVelocity = Vector3.zero;
        verticalVelocity = 0f;
        floating = false;
        characterController.enabled = false;
    }

    /// <summary>Gets up, onto <paramref name="position"/>.</summary>
    public void StandAt(Vector3 position)
    {
        seat = null;
        transform.position = position;
        Physics.SyncTransforms();
        characterController.enabled = true;
    }

    /// <summary>Gravity again (out of the water).</summary>
    public void StopFloating() => floating = false;

    /// <summary>Faces <paramref name="direction"/> at once (the start of a swing), whatever the movement gate.</summary>
    public void SnapFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    /// <summary>Sets the horizontal direction toward the aim point (e.g. the cursor). Used only in states that face the cursor.</summary>
    public void SetAimDirection(Vector3 direction)
    {
        direction.y = 0f;
        hasAimDirection = direction.sqrMagnitude > 0.0001f;
        aimDirection = hasAimDirection ? direction.normalized : Vector3.zero;
    }

    public void ClearAimDirection()
    {
        hasAimDirection = false;
        aimDirection = Vector3.zero;
    }

    /// <summary>Faces and acts toward an enemy rather than the cursor, until cleared (PlayerFocusTarget).</summary>
    public void SetFocusDirection(Vector3 direction)
    {
        direction.y = 0f;
        hasFocus = direction.sqrMagnitude > 0.0001f;
        focusDirection = hasFocus ? direction.normalized : Vector3.zero;
    }

    public void ClearFocusDirection() => hasFocus = false;

    public void FaceDirection(Vector3 direction)
    {
        if (!stateMachine.CanMove || FacingLocked)
        {
            return;
        }

        TurnToward(direction);
    }

    /// <summary>Turns toward a direction even while movement is gated, e.g. to face an object being interacted with.</summary>
    public void TurnToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float turnSpeed = settings.TurnSpeed * settings.GetTurnSpeedMultiplier(currentGameState) * (floating ? settings.SwimTurnSpeed : 1f);
        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);
    }

    private void OnGameStateChanged(GameState previous, GameState current)
    {
        currentGameState = current;
    }

    private void UpdateHorizontalVelocity()
    {
        if (dashUntil >= 0f)
        {
            if (Time.time < dashUntil)
            {
                horizontalVelocity = dashVelocity;
                return;
            }

            dashUntil = -1f;
        }

        Vector3 direction = stateMachine.CanMove ? movementInput : Vector3.zero;

        // Cursor-facing states keep aiming at the cursor even while standing still; otherwise (and swimming) face the
        // travel direction.
        bool facingAim = settings.FacesCursor(currentGameState) && (hasAimDirection || hasFocus) && !floating;
        if (facingAim)
        {
            FaceDirection(hasFocus ? focusDirection : aimDirection);
        }
        else if (direction.sqrMagnitude > 0.0001f)
        {
            FaceDirection(direction);
        }

        // A share of the player's own speed: a Percent modifier scales it, a Flat one adds to it (StatSheet.GetOn).
        float bonus = combatant != null ? Mathf.Max(-0.5f, combatant.Stats.GetOn(StatType.MoveSpeed, 1f) - 1f) : 0f;
        float speed = settings.MoveSpeed * settings.GetSpeedMultiplier(currentGameState) * speedScale * (1f + bonus);
        if ((facingAim || FacingLocked) && direction.sqrMagnitude > 0.0001f)
        {
            speed *= settings.BackpedalShare(Vector3.Angle(transform.forward, direction));
        }

        horizontalVelocity = direction * speed;
        if (lungeUntil >= 0f)
        {
            if (Time.time < lungeUntil)
            {
                horizontalVelocity += lungeVelocity;
            }
            else
            {
                lungeUntil = -1f;
            }
        }
    }

    private void ApplyGravity()
    {
        if (floating)
        {
            // Eases towards the float height (the gap closes in about a second), the change of speed capped by buoyancy.
            float wanted = (floatHeight - transform.position.y) * 3f;
            verticalVelocity = Mathf.MoveTowards(verticalVelocity, wanted, settings.Buoyancy * Time.deltaTime);
            return;
        }

        if (IsGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = settings.GroundedVerticalVelocity;
        }

        verticalVelocity += settings.Gravity * Time.deltaTime;
    }

    private void Move()
    {
        Vector3 velocity = horizontalVelocity;
        velocity.y = verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
        IsGrounded = characterController.isGrounded;
    }
}
