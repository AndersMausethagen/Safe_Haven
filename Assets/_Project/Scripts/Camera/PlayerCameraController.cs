using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

/// <summary>
/// The player camera, in two styles that the player toggles with V (see <see cref="CameraPreferences"/>; the options can
/// fix a style per situation):
/// <list type="bullet">
/// <item><b>Top-down</b> (V Rising style): a fixed high-angle view with wheel zoom, whose framing depends on the
/// GameState. Exploration is wide and loose with right-mouse orbit; Combat is steeper and tighter, locks orbit and leans
/// toward the cursor.</item>
/// <item><b>Action</b> (Enshrouded style): an over-the-shoulder view. The cursor is locked, the mouse turns the camera,
/// the crosshair aims (<see cref="CameraPointer"/>) and the camera moves in rather than pass through walls.</item>
/// </list>
/// Values come from PlayerCameraSettings; the two styles blend into each other, as do the state profiles. The Cinemachine
/// camera follows an internal focus point so the cursor lean and shoulder offset do not move the player.
/// While a UI window is open (UIStateManager) the player can't turn or zoom the camera and the cursor is free, so
/// scrolling a list or moving the mouse over a window doesn't move the view.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
[RequireComponent(typeof(CinemachineFollow))]
public sealed class PlayerCameraController : MonoBehaviour
{
    private const float MinClipDistance = 0.4f;

    [SerializeField] private PlayerCameraSettings settings;

    /// <summary>The scene's player camera rig, while it is enabled; null in scenes without one.</summary>
    public static PlayerCameraController Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    private static readonly RaycastHit[] ClipHits = new RaycastHit[16];

    private CinemachineCamera cinemachineCamera;
    private CinemachineFollow follow;
    private CinemachineRotationComposer composer;
    private Vector2 composerDamping;
    private GameStateManager gameState;
    private UIStateManager uiState;
    private Camera outputCamera;
    private Transform target;
    private Transform focus;

    private InputAction orbitAction;
    private InputAction zoomAction;
    private InputAction toggleAction;
    private InputAction lookAction;

    private PlayerCameraSettings.Profile profile;
    private float pitch;
    private float minDistance;
    private float maxDistance;
    private float fieldOfView;
    private Vector3 positionDamping;
    private float lookAheadFraction;
    private float maxLookAhead;

    private float zoomTarget;
    private float zoom;
    private float yaw;
    private Vector3 lookAheadOffset;
    private Vector3 lookAheadVelocity;

    private CameraStyle style;
    // 0 is fully top-down, 1 fully action; everything that differs between the styles blends on this.
    private float actionWeight;
    private float actionPitch;
    private bool lockedCursor;
    private Transform dialogueTarget;
    private float dialogueDistance = 1f;
    private float dialogueDistanceNow = 1f;
    private float dialogueDistanceVelocity;
    private float dialogueWeight;
    private Vector3 dialoguePoint;
    private Vector3 dialoguePointVelocity;
    private bool dialoguePointSet;
    private float dialogueHoldUntil;
    private Vector3 focusPointVelocity;
    private bool focusPointSet;
    private bool aiming;
    private bool bowInHand;
    private Transform focusTarget;
    private float focusWeight;
    private Vector3 focusPoint;

    /// <summary>Turns the view to look at <paramref name="target"/> instead of the player (a story moment: the troll
    /// roaring on the ridge), until EndFocus. The player keeps control.</summary>
    public void BeginFocus(Transform target) => focusTarget = target;

    /// <summary>Co-op, fallen on a raid: the view follows <paramref name="other"/> (a friend) instead of the player; null goes back.</summary>
    public void Watch(Transform other)
    {
        playerTarget = playerTarget != null ? playerTarget : target;
        target = other != null ? other : playerTarget;
    }

    private Transform playerTarget;

    public void EndFocus(Transform target)
    {
        if (focusTarget == target) focusTarget = null;
    }

    /// <summary>Aiming or drawing a bow (ArcheryCameraAim): the right button doesn't turn the view under the aim.</summary>
    public void SetAiming(bool aim) => aiming = aim;

    /// <summary>A bow in hand (ArcheryCameraAim): the framing stays as it is out of a fight, so starting to draw doesn't zoom
    /// and tilt the view, and it doesn't lean with the cursor, which moved the ground under the aim.</summary>
    public void SetBowInHand(bool bow)
    {
        bowInHand = bow;
        profile = settings.GetProfile(FramingState);
    }

    private GameState FramingState => bowInHand || gameState == null ? GameState.Exploration : gameState.CurrentState;

    public void BeginDialogue(Transform speaker, float distanceScale)
    {
        dialogueTarget = speaker;
        dialogueDistance = Mathf.Clamp(distanceScale, 0.3f, 1f);
    }

    public void EndDialogue(Transform speaker)
    {
        if (dialogueTarget == speaker)
        {
            dialogueTarget = null;
            dialogueHoldUntil = Time.unscaledTime + settings.DialogueHoldTime;
        }
    }

    private void Awake()
    {
        cinemachineCamera = GetComponent<CinemachineCamera>();
        follow = GetComponent<CinemachineFollow>();
        composer = GetComponent<CinemachineRotationComposer>();
        composerDamping = composer != null ? composer.Damping : Vector2.zero;
        target = cinemachineCamera.Follow;

        if (settings == null || target == null)
        {
            Debug.LogError("PlayerCameraController requires a PlayerCameraSettings asset and a Cinemachine Follow target.", this);
            enabled = false;
            return;
        }

        zoomTarget = zoom = settings.InitialZoom;
        actionPitch = settings.ActionStartPitch;

        focus = new GameObject("Camera Focus").transform;
        focus.position = target.position;

        // Follow and look at the focus point; drop any custom LookAt so the camera never aims at a stale object.
        CameraTarget cameraTarget = cinemachineCamera.Target;
        cameraTarget.TrackingTarget = focus;
        cameraTarget.CustomLookAtTarget = false;
        cameraTarget.LookAtTarget = null;
        cinemachineCamera.Target = cameraTarget;

        // The Camera map of the project-wide actions (GameInput); it stays enabled, so nothing to enable or dispose here.
        orbitAction = GameInput.CameraOrbit;
        zoomAction = GameInput.CameraZoom;
        toggleAction = GameInput.CameraToggleStyle;
        lookAction = GameInput.CameraLook;
    }

    private void Start()
    {
        // A camera left behind by a tool or test would draw over this one (it happened: a bare "Main Camera" at the root).
        StrayCameras.SwitchOff(Camera.main);
    }

    private void OnEnable()
    {
        Current = this;

        gameState = GameStateManager.Current;
        GameState state = GameState.Exploration;
        if (gameState != null)
        {
            state = gameState.CurrentState;
            gameState.GameStateChanged += OnGameStateChanged;
        }
        else
        {
            Debug.LogWarning("PlayerCameraController found no GameStateManager; using the Exploration profile.", this);
        }

        SnapToProfile(settings.GetProfile(state));

        uiState = UIStateManager.Current;
        if (uiState == null)
        {
            Debug.LogWarning("PlayerCameraController found no UIStateManager; the camera stays controllable while UI is open.", this);
        }

        // Start in the right style without blending in from the other one.
        style = CameraPreferences.Resolve(CurrentSituation(), out _);
        actionWeight = style == CameraStyle.Action ? 1f : 0f;
        CameraPointer.UsesScreenCenter = style == CameraStyle.Action;
        CameraPointer.Reach = settings.ActionPointerReach;
    }

    private void OnDisable()
    {
        TerrainDetailFocus.Clear();
        if (Current == this)
        {
            Current = null;
        }

        if (gameState != null)
        {
            gameState.GameStateChanged -= OnGameStateChanged;
            gameState = null;
        }

        CameraPointer.UsesScreenCenter = false;
        dialogueTarget = null;
        dialogueWeight = 0f;
        dialoguePointSet = false;
        focusTarget = null;
        focusWeight = 0f;
        focusPointSet = false;
        ReleaseCursor();
    }

    private void OnDestroy()
    {
        if (focus != null)
        {
            Destroy(focus.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        TerrainDetailFocus.Set(target.position);

        UpdateStyle();
        ReadInput();
        BlendTowardProfile();
        // It stays framed through the short gaps between one line and the next, rather than pulling out and back in each time.
        bool framing = dialogueTarget != null || Time.unscaledTime < dialogueHoldUntil;
        dialogueWeight = Mathf.MoveTowards(dialogueWeight, framing ? 1f : 0f, Time.unscaledDeltaTime / settings.DialogueBlendTime);
        focusWeight = Mathf.MoveTowards(focusWeight, focusTarget != null ? 1f : 0f, Time.unscaledDeltaTime / settings.FocusBlendTime);
        UpdateFocus();
        ApplyToCinemachine();
    }

    private bool IsUIOpen => (uiState != null && uiState.IsAnyWindowOpen) || CommandWheel.IsHeld;

    private CameraSituation CurrentSituation()
    {
        bool combat = gameState != null && gameState.CurrentState == GameState.Combat;
        return new CameraSituation(BuildPlacer.IsBusy, combat, RaidDirector.Current != null);
    }

    private void OnGameStateChanged(GameState previous, GameState current)
    {
        profile = settings.GetProfile(FramingState);
    }

    private void SnapToProfile(PlayerCameraSettings.Profile newProfile)
    {
        profile = newProfile;
        pitch = profile.pitch;
        minDistance = profile.minDistance;
        maxDistance = profile.maxDistance;
        fieldOfView = profile.fieldOfView;
        positionDamping = profile.positionDamping;
        lookAheadFraction = profile.cursorLookAhead;
        maxLookAhead = profile.maxLookAhead;
    }

    // ----- Style -----

    private void UpdateStyle()
    {
        if (toggleAction.WasPressedThisFrame())
        {
            ToggleStyle();
        }

        style = CameraPreferences.Resolve(CurrentSituation(), out _);
        bool action = style == CameraStyle.Action;
        CameraPointer.UsesScreenCenter = action;
        actionWeight = Mathf.MoveTowards(actionWeight, action ? 1f : 0f, Time.unscaledDeltaTime / settings.StyleTransitionTime);
        UpdateCursor(action);
    }

    // The toggle always changes the player's choice; a situation fixed in the options still wins, and says so.
    private void ToggleStyle()
    {
        CameraStyle next = CameraPreferences.Chosen == CameraStyle.Action ? CameraStyle.TopDown : CameraStyle.Action;
        CameraPreferences.Chosen = next;

        CameraStyle shown = CameraPreferences.Resolve(CurrentSituation(), out CameraContext? decidedBy);
        if (shown != next && decidedBy.HasValue)
        {
            GameMessages.Post($"Camera: {CameraStyleRules.Label(next)}. {decidedBy.Value} always uses {CameraStyleRules.Label(shown)} (Options › Camera).", MessageKind.Warning);
        }
        else
        {
            GameMessages.Post($"Camera: {CameraStyleRules.Label(next)}");
        }
    }

    private void UpdateCursor(bool action)
    {
        bool wantLocked = action && !IsUIOpen && !GamePause.IsPaused;
        if (wantLocked)
        {
            // Re-lock after alt-tab and similar, where the system releases the cursor on its own.
            if (Cursor.lockState != CursorLockMode.Locked && Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            lockedCursor = true;
        }
        else
        {
            ReleaseCursor();
        }
    }

    // Only undo a lock this camera made, so screens that manage the cursor themselves are left alone.
    private void ReleaseCursor()
    {
        if (!lockedCursor)
        {
            return;
        }

        lockedCursor = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // ----- Input and blending -----

    private void ReadInput()
    {
        if (IsUIOpen)
        {
            return;
        }

        float scroll = zoomAction.ReadValue<float>();
        // While placing a building the wheel rotates the building instead.
        if (Mathf.Abs(scroll) > 0.01f && !BuildPlacer.IsBusy)
        {
            // Direction only: scroll magnitudes differ between platforms and devices.
            zoomTarget = Mathf.Clamp01(zoomTarget - Mathf.Sign(scroll) * settings.ZoomStep);
        }

        if (Mouse.current == null)
        {
            return;
        }

        if (style == CameraStyle.Action)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 delta = lookAction.ReadValue<Vector2>() * (settings.ActionLookSensitivity * GameOptions.LookSensitivity.Value);
            yaw += delta.x;
            actionPitch += settings.ActionInvertY != GameOptions.InvertY.On ? delta.y : -delta.y;
            actionPitch = Mathf.Clamp(actionPitch, settings.ActionMinPitch, settings.ActionMaxPitch);
        }
        // Aiming a bow holds the same right button, so the view must not turn under the aim.
        else if (profile.allowOrbit && !aiming && orbitAction.IsPressed())
        {
            yaw += lookAction.ReadValue<Vector2>().x * (settings.OrbitSensitivity * GameOptions.LookSensitivity.Value);
        }
    }

    private void BlendTowardProfile()
    {
        float dt = Time.deltaTime;
        float t = Blend(settings.TransitionTime, dt);

        pitch = Mathf.Lerp(pitch, profile.pitch, t);
        minDistance = Mathf.Lerp(minDistance, profile.minDistance, t);
        maxDistance = Mathf.Lerp(maxDistance, profile.maxDistance, t);
        fieldOfView = Mathf.Lerp(fieldOfView, profile.fieldOfView, t);
        positionDamping = Vector3.Lerp(positionDamping, profile.positionDamping, t);
        lookAheadFraction = Mathf.Lerp(lookAheadFraction, profile.cursorLookAhead, t);
        maxLookAhead = Mathf.Lerp(maxLookAhead, profile.maxLookAhead, t);

        zoom = Mathf.Lerp(zoom, zoomTarget, Blend(settings.ZoomSmoothTime, dt));
    }

    private void UpdateFocus()
    {
        // The cursor lean holds its current offset while UI is open; the focus still follows the player.
        if (IsUIOpen)
        {
            lookAheadVelocity = Vector3.zero;
        }
        else
        {
            bool lean = style == CameraStyle.TopDown && lookAheadFraction > 0.001f;
            Vector3 desiredOffset = lean ? GetCursorOffset() : Vector3.zero;
            lookAheadOffset = Vector3.SmoothDamp(lookAheadOffset, desiredOffset, ref lookAheadVelocity, settings.LookAheadSmoothTime);
        }

        Vector3 shoulder = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * settings.ActionShoulderOffset;
        Vector3 actionOffset = (shoulder + Vector3.up * settings.ActionLookHeight) * actionWeight;
        focus.position = target.position + lookAheadOffset + actionOffset;
        // Glides from one speaker to the next rather than jumping when the speaker changes, and only leans a little way
        // towards someone far off, so a shout from across the meadow doesn't throw the view after them.
        if (dialogueTarget != null)
        {
            Vector3 lean = Vector3.ClampMagnitude((dialogueTarget.position + Vector3.up - target.position) * 0.5f, settings.DialogueMaxLean);
            dialoguePoint = Glide(dialoguePoint, target.position + lean, ref dialoguePointVelocity, ref dialoguePointSet);
        }
        else if (dialogueWeight <= 0f)
        {
            dialoguePointSet = false;
        }

        focus.position = Vector3.Lerp(focus.position, dialoguePoint, Mathf.SmoothStep(0f, 1f, dialogueWeight));
        if (focusTarget != null)
        {
            focusPoint = Glide(focusPoint, focusTarget.position + Vector3.up, ref focusPointVelocity, ref focusPointSet);
        }
        else if (focusWeight <= 0f)
        {
            focusPointSet = false;
        }

        focus.position = Vector3.Lerp(focus.position, focusPoint, Mathf.SmoothStep(0f, 1f, focusWeight));
        KeepViewInBounds();
    }

    // A new look-at starts where it is (the weight blends the view over from the player); only changes after that glide.
    private Vector3 Glide(Vector3 from, Vector3 to, ref Vector3 velocity, ref bool started)
    {
        if (!started)
        {
            started = true;
            velocity = Vector3.zero;
            return to;
        }

        return Vector3.SmoothDamp(from, to, ref velocity, settings.DialogueTargetSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    // Top-down only: the action camera looks at the horizon, where the world's edge is a matter for the map itself.
    private void KeepViewInBounds()
    {
        ViewBounds bounds = ViewBounds.Current;
        if (bounds == null || actionWeight >= 1f)
        {
            return;
        }

        outputCamera = outputCamera != null ? outputCamera : Camera.main;
        if (outputCamera != null)
        {
            Vector3 kept = bounds.Keep(focus.position, follow.FollowOffset, cinemachineCamera.Lens.FieldOfView, outputCamera.aspect);
            focus.position = Vector3.Lerp(kept, focus.position, actionWeight);
        }
    }

    private Vector3 GetCursorOffset()
    {
        if (Mouse.current == null)
        {
            return Vector3.zero;
        }

        if (outputCamera == null)
        {
            outputCamera = Camera.main;
            if (outputCamera == null)
            {
                return Vector3.zero;
            }
        }

        // Measured from the ground under the middle of the screen, not from the player: the camera then holds still while
        // the cursor does. Measured from the player, the lean moved the view, which moved the cursor's ground point,
        // which leaned further, so the view kept sliding (2.3x the cursor's distance while aiming).
        Plane ground = new Plane(Vector3.up, target.position);
        Ray cursorRay = outputCamera.ScreenPointToRay(GameInput.PointerPosition);
        Ray centreRay = outputCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!ground.Raycast(cursorRay, out float cursorDistance) || !ground.Raycast(centreRay, out float centreDistance))
        {
            return Vector3.zero;
        }

        Vector3 offset = (cursorRay.GetPoint(cursorDistance) - centreRay.GetPoint(centreDistance)) * lookAheadFraction;
        offset.y = 0f;
        return Vector3.ClampMagnitude(offset, maxLookAhead);
    }

    private void ApplyToCinemachine()
    {
        float w = actionWeight;
        float distance = Mathf.Lerp(
            Mathf.Lerp(minDistance, maxDistance, zoom),
            Mathf.Lerp(settings.ActionMinDistance, settings.ActionMaxDistance, zoom),
            w);
        float viewPitch = Mathf.Lerp(pitch, actionPitch, w);
        dialogueDistanceNow = Mathf.SmoothDamp(dialogueDistanceNow, dialogueDistance, ref dialogueDistanceVelocity, settings.DialogueTargetSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        distance *= Mathf.Lerp(1f, dialogueDistanceNow, Mathf.SmoothStep(0f, 1f, dialogueWeight));
        Vector3 back = Quaternion.Euler(viewPitch, yaw, 0f) * Vector3.back;

        // The action camera orbits the point the composer looks at, not the focus below it.
        Vector3 lookOffset = composer != null ? composer.TargetOffset : Vector3.zero;
        Vector3 lift = lookOffset * w;
        if (w > 0.001f)
        {
            distance = Mathf.Min(distance, ClearDistance(focus.position + lookOffset, back, distance));
        }

        follow.FollowOffset = lift + back * distance;

        var trackerSettings = follow.TrackerSettings;
        trackerSettings.PositionDamping = Vector3.Lerp(positionDamping, settings.ActionPositionDamping, w);
        follow.TrackerSettings = trackerSettings;

        if (composer != null)
        {
            composer.Damping = Vector2.Lerp(composerDamping, Vector2.zero, w);
        }

        LensSettings lens = cinemachineCamera.Lens;
        lens.FieldOfView = Mathf.Lerp(fieldOfView, settings.ActionFieldOfView, w);
        cinemachineCamera.Lens = lens;
    }

    // How far the camera can sit from the look point along the view before scenery would come between them.
    // Characters and other moving things are ignored so the view doesn't jump whenever something walks past.
    private float ClearDistance(Vector3 origin, Vector3 direction, float distance)
    {
        int count = Physics.SphereCastNonAlloc(origin, settings.ActionCollisionRadius, direction, ClipHits, distance,
            settings.ActionObstructionLayers, QueryTriggerInteraction.Ignore);
        float clear = distance;
        Transform player = target.root;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = ClipHits[i];
            // Distance 0 means the cast started inside this collider; there is no useful distance to keep.
            if (hit.distance <= 0f || hit.distance >= clear || hit.transform.IsChildOf(player) || IsMoving(hit.collider))
            {
                continue;
            }

            clear = hit.distance;
        }

        return Mathf.Max(MinClipDistance, clear);
    }

    private static bool IsMoving(Collider collider)
    {
        return collider.attachedRigidbody != null
            || collider is CharacterController
            || collider.GetComponentInParent<NavMeshAgent>() != null;
    }

    // Frame-rate independent exponential smoothing factor that settles in roughly smoothTime seconds.
    private static float Blend(float smoothTime, float deltaTime)
    {
        return 1f - Mathf.Exp(-3f * deltaTime / smoothTime);
    }
}
