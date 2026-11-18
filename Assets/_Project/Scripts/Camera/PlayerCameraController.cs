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
    private InputAction lookStickAction;

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
    // Where the orbit is turning to, and how fast the view follows (SmoothDampAngle).
    private float yawTarget;
    private float yawVelocity;
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

    private Transform closeUpSubject;
    private float closeUpWeight;
    private Vector3 closeUpPoint;
    private Vector3 closeUpPointVelocity;
    private bool closeUpPointSet;
    private float closeUpFit;
    private float closeUpYaw;
    private bool closeUpNear;
    private float closeUpNearWeight;
    private readonly System.Collections.Generic.Dictionary<object, float> closeness = new System.Collections.Generic.Dictionary<object, float>();
    private readonly System.Collections.Generic.Dictionary<object, Vector3> leads = new System.Collections.Generic.Dictionary<object, Vector3>();
    private Vector3 leadNow;
    private Vector3 leadVelocity;
    private float closenessNow = 1f;
    private float closenessVelocity;
    private readonly System.Collections.Generic.Dictionary<object, float> tilts = new System.Collections.Generic.Dictionary<object, float>();
    private float tiltNow;
    private float tiltVelocity;

    /// <summary>Tilts the view while <paramref name="source"/> asks (a CameraZone): degrees added to its pitch, negative
    /// looking flatter across the ground, positive more from above; 0 lets it go. Several add up, within 30 degrees.</summary>
    public void SetTilt(object source, float degrees)
    {
        if (source == null)
        {
            return;
        }

        if (Mathf.Abs(degrees) < 0.01f)
        {
            tilts.Remove(source);
        }
        else
        {
            tilts[source] = degrees;
        }
    }

    /// <summary>Brings the view in (or out) a little while <paramref name="source"/> asks (in for taming a horse, out at a
    /// gallop): its distance times <paramref name="scale"/>, eased in and out; 1 lets it go. With several asking, the nearest
    /// and the farthest of them meet.</summary>
    public void SetCloseness(object source, float scale)
    {
        if (source == null)
        {
            return;
        }

        if (Mathf.Abs(scale - 1f) < 0.001f)
        {
            closeness.Remove(source);
        }
        else
        {
            closeness[source] = Mathf.Clamp(scale, 0.3f, 1.5f);
        }
    }

    /// <summary>Leads the view ahead of the player while <paramref name="source"/> asks (a gallop: more of the way ahead in
    /// view), by <paramref name="offset"/> on the ground, eased; zero lets it go. Several leads add up.</summary>
    public void SetLead(object source, Vector3 offset)
    {
        if (source == null)
        {
            return;
        }

        offset.y = 0f;
        if (offset.sqrMagnitude < 0.0001f)
        {
            leads.Remove(source);
        }
        else
        {
            leads[source] = Vector3.ClampMagnitude(offset, 6f);
        }
    }

    private Vector3 WantedLead()
    {
        Vector3 total = Vector3.zero;
        object gone = null;
        foreach (var asked in leads)
        {
            if (asked.Key is Object thing && thing == null)
            {
                gone = asked.Key;
                continue;
            }

            total += asked.Value;
        }

        if (gone != null)
        {
            leads.Remove(gone);
        }

        return total;
    }

    /// <summary>Brings the view down and in on the player and <paramref name="subject"/> together (a fish on the line): it
    /// swings round behind the player to look out at the subject, lower to the ground, keeping both in view, until
    /// <see cref="EndCloseUp"/>, then turns back to the player's own view. Top-down only.</summary>
    public void BeginCloseUp(Transform subject)
    {
        closeUpSubject = subject;
        closeUpNear = true;
    }

    /// <summary>Only leans the view towards <paramref name="subject"/> so it stays in the picture with the player (a float on
    /// the water), without coming closer or turning, until <see cref="EndCloseUp"/> or a <see cref="BeginCloseUp"/>.</summary>
    public void KeepInView(Transform subject)
    {
        closeUpSubject = subject;
        closeUpNear = false;
    }

    public void EndCloseUp(Transform subject)
    {
        if (closeUpSubject == subject)
        {
            closeUpSubject = null;
        }
    }

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
        lookStickAction = GameInput.CameraLookStick;
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
        UpdateYaw();
        BlendTowardProfile();
        // It stays framed through the short gaps between one line and the next, rather than pulling out and back in each time.
        bool framing = dialogueTarget != null || Time.unscaledTime < dialogueHoldUntil;
        dialogueWeight = Mathf.MoveTowards(dialogueWeight, framing ? 1f : 0f, Time.unscaledDeltaTime / settings.DialogueBlendTime);
        focusWeight = Mathf.MoveTowards(focusWeight, focusTarget != null ? 1f : 0f, Time.unscaledDeltaTime / settings.FocusBlendTime);
        bool closeUp = closeUpSubject != null && closeUpSubject.gameObject.activeInHierarchy;
        closeUpWeight = Mathf.MoveTowards(closeUpWeight, closeUp ? 1f : 0f, Time.unscaledDeltaTime / settings.CloseUpBlendTime);
        closeUpNearWeight = Mathf.MoveTowards(closeUpNearWeight, closeUp && closeUpNear ? 1f : 0f, Time.unscaledDeltaTime / settings.CloseUpBlendTime);
        UpdateFocus();
        ApplyToCinemachine();
    }

    /// <summary>
    /// True while the right stick aims rather than turns the view, on a controller in the top-down style: in a fight (its
    /// framing doesn't orbit) and while aiming a bow. PlayerInputHandler then aims with it, twin-stick style.
    /// </summary>
    public bool StickAims => style != CameraStyle.Action && (!profile.allowOrbit || aiming);

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
        if (!InCinematic && GameInput.WasPressed(toggleAction))
        {
            ToggleStyle();
        }

        style = CameraPreferences.Resolve(CurrentSituation(), out _);
        bool action = style == CameraStyle.Action;
        CameraPointer.UsesScreenCenter = action;
        actionWeight = Mathf.MoveTowards(actionWeight, action ? 1f : 0f, Time.unscaledDeltaTime / settings.StyleTransitionTime);
        // The sun's shadow cascades follow where the camera looks: near ones over the shoulder and in cinematics, the top-down set otherwise.
        GraphicsOptions.UseNearShadows(actionWeight > 0.5f || InGameCinematic.IsPlaying || CinematicScreen.IsPlaying);
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

    // Nor through a cinematic: the wheel, the orbit and the style key changed the view behind it, and the player came back to it
    // turned or zoomed.
    private static bool InCinematic => CinematicScreen.IsPlaying || InGameCinematic.IsPlaying;

    private void ReadInput()
    {
        if (IsUIOpen || InCinematic)
        {
            return;
        }

        float scroll = zoomAction.ReadValue<float>();
        // While placing a building the wheel rotates the building instead; over the minimap it sets the map's distance.
        if (Mathf.Abs(scroll) > 0.01f && !BuildPlacer.IsBusy && !MinimapHud.TakesWheel)
        {
            // Direction only: scroll magnitudes differ between platforms and devices.
            zoomTarget = Mathf.Clamp01(zoomTarget - Mathf.Sign(scroll) * settings.ZoomStep);
        }

        ReadStick();
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
            yawTarget = yaw;
            yawVelocity = 0f;
            actionPitch += settings.ActionInvertY != GameOptions.InvertY.On ? delta.y : -delta.y;
            actionPitch = Mathf.Clamp(actionPitch, settings.ActionMinPitch, settings.ActionMaxPitch);
        }
        // Aiming a bow holds the same right button, so the view must not turn under the aim.
        else if (profile.allowOrbit && !aiming && orbitAction.IsPressed())
        {
            yawTarget += lookAction.ReadValue<Vector2>().x * (settings.OrbitSensitivity * GameOptions.LookSensitivity.Value);
        }
    }

    // The right stick turns the view at a rate (degrees a second), where the mouse moves it by how far it travelled. In the
    // action camera it looks around; top-down it turns the view where orbiting is allowed, and up and down zoom.
    private void ReadStick()
    {
        // Placing a building top-down on a controller, the stick moves the spot instead (BuildPlacer).
        if (lookStickAction == null || GamePause.IsPaused || (BuildPlacer.UsesPadSpot && style != CameraStyle.Action))
        {
            return;
        }

        Vector2 stick = lookStickAction.ReadValue<Vector2>();
        if (stick.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;
        stick = stick.normalized * Mathf.Pow(Mathf.Clamp01(stick.magnitude), settings.StickResponse);
        float sensitivity = GameOptions.LookSensitivity.Value;
        if (style == CameraStyle.Action)
        {
            yaw += stick.x * settings.StickLookSpeed.x * sensitivity * dt;
            yawTarget = yaw;
            yawVelocity = 0f;
            float tilt = stick.y * settings.StickLookSpeed.y * sensitivity * dt;
            actionPitch += settings.ActionInvertY != GameOptions.InvertY.On ? tilt : -tilt;
            actionPitch = Mathf.Clamp(actionPitch, settings.ActionMinPitch, settings.ActionMaxPitch);
            return;
        }

        // In a fight, or aiming, the stick is the aim instead (StickAims).
        if (StickAims)
        {
            return;
        }

        yawTarget += stick.x * settings.StickOrbitSpeed * sensitivity * dt;

        if (!BuildPlacer.IsBusy)
        {
            zoomTarget = Mathf.Clamp01(zoomTarget - stick.y * settings.StickZoomSpeed * dt);
        }
    }

    // The orbit follows the mouse a moment behind: raw deltas turned the view in steps, one per frame's mouse report.
    private void UpdateYaw()
    {
        if (style == CameraStyle.Action || settings.OrbitSmoothTime <= 0f)
        {
            yaw = yawTarget;
            yawVelocity = 0f;
            return;
        }

        yaw = Mathf.SmoothDampAngle(yaw, yawTarget, ref yawVelocity, settings.OrbitSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
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
        // Slow to lean out and to come back, so a gallop's lead doesn't jerk the view at each change of pace.
        leadNow = Vector3.SmoothDamp(leadNow, WantedLead(), ref leadVelocity, 0.9f, Mathf.Infinity, Time.unscaledDeltaTime);
        focus.position = target.position + lookAheadOffset + actionOffset + leadNow;
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
        UpdateCloseUp();
        if (focusTarget != null)
        {
            focusPoint = Glide(focusPoint, focusTarget.position + Vector3.up, ref focusPointVelocity, ref focusPointSet);
        }
        else if (focusWeight <= 0f)
        {
            focusPointSet = false;
        }

        focus.position = Vector3.Lerp(focus.position, focusPoint, Mathf.SmoothStep(0f, 1f, focusWeight));
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

    private Vector3 GetCursorOffset()
    {
        // On a controller the mouse lies where it was left: leaning towards it would pull the view to one side for good.
        if (Mouse.current == null || InputDevices.UsingGamepad)
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

    // The close view looks at the ground halfway between the player and the subject, and remembers how far back it
    // must sit to keep both in the picture.
    private void UpdateCloseUp()
    {
        if (closeUpSubject != null && closeUpSubject.gameObject.activeInHierarchy)
        {
            Vector3 player = target.position;
            Vector3 subject = closeUpSubject.position;
            // Nearer the player than the subject: the player stands clear of the bottom of the picture.
            Vector3 middle = Vector3.Lerp(player, subject, 0.4f);
            middle.y = Mathf.Min(player.y, subject.y) + 0.4f;
            closeUpPoint = Glide(closeUpPoint, middle, ref closeUpPointVelocity, ref closeUpPointSet);
            // From behind the player, looking out at the subject: the player low in the picture, the subject beyond.
            Vector3 toward = subject - player;
            toward.y = 0f;
            if (toward.sqrMagnitude > 0.25f)
            {
                closeUpYaw = Mathf.Atan2(toward.x, toward.z) * Mathf.Rad2Deg;
            }

            closeUpFit = FitDistance(subject - player);
        }
        else if (closeUpWeight <= 0f)
        {
            closeUpPointSet = false;
        }

        float w = Mathf.SmoothStep(0f, 1f, closeUpWeight) * (1f - actionWeight);
        if (w > 0f)
        {
            focus.position = Vector3.Lerp(focus.position, closeUpPoint, w);
        }
    }

    // How far back along the close view the camera must be so that two points this far apart both fit, with the margin.
    private float FitDistance(Vector3 apart)
    {
        Camera view = Camera.main;
        float aspect = view != null ? view.aspect : 16f / 9f;
        float vertical = Mathf.Deg2Rad * fieldOfView * 0.5f;
        float horizontal = Mathf.Atan(Mathf.Tan(vertical) * aspect);
        Quaternion facing = Quaternion.Euler(0f, closeUpYaw, 0f);
        Vector3 across = facing * Vector3.right;
        Vector3 along = facing * Vector3.forward;
        float wide = Mathf.Abs(Vector3.Dot(apart, across)) * 0.5f + settings.CloseUpMargin;
        // Seen from the close view's pitch, distance along the ground shows foreshortened on screen.
        float deep = Mathf.Abs(Vector3.Dot(apart, along)) * 0.5f * Mathf.Sin(Mathf.Deg2Rad * settings.CloseUpPitch) + settings.CloseUpMargin;
        return Mathf.Max(settings.CloseUpDistance, wide / Mathf.Tan(horizontal), deep / Mathf.Tan(vertical));
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
        float nearest = 1f, farthest = 1f;
        object gone = null;
        foreach (var asked in closeness)
        {
            // Whatever asked and is gone (destroyed) no longer holds the view.
            if (asked.Key is Object thing && thing == null)
            {
                gone = asked.Key;
                continue;
            }

            nearest = Mathf.Min(nearest, asked.Value);
            farthest = Mathf.Max(farthest, asked.Value);
        }

        float wantedCloseness = nearest * farthest;

        if (gone != null)
        {
            closeness.Remove(gone);
        }

        closenessNow = Mathf.SmoothDamp(closenessNow, wantedCloseness, ref closenessVelocity, 0.6f, Mathf.Infinity, Time.unscaledDeltaTime);
        distance *= closenessNow;
        float wantedTilt = 0f;
        foreach (var asked in tilts)
        {
            if (!(asked.Key is Object thing) || thing != null)
            {
                wantedTilt += asked.Value;
            }
        }

        tiltNow = Mathf.SmoothDamp(tiltNow, Mathf.Clamp(wantedTilt, -30f, 30f), ref tiltVelocity, 0.6f, Mathf.Infinity, Time.unscaledDeltaTime);
        // Only the top-down view: the action camera's pitch is the player's own.
        viewPitch = Mathf.Clamp(viewPitch + tiltNow * (1f - w), 5f, 89f);
        float close = Mathf.SmoothStep(0f, 1f, Mathf.Min(closeUpWeight, closeUpNearWeight)) * (1f - w);
        float viewYaw = yaw;
        if (close > 0f)
        {
            distance = Mathf.Lerp(distance, Mathf.Min(closeUpFit, distance), close);
            viewPitch = Mathf.Lerp(viewPitch, settings.CloseUpPitch, close);
            // The close view swings round behind the player; the player's own view direction is kept and comes back.
            viewYaw = Mathf.LerpAngle(yaw, closeUpYaw, close);
        }

        Vector3 back = Quaternion.Euler(viewPitch, viewYaw, 0f) * Vector3.back;

        // The action camera orbits the point the composer looks at, not the focus below it.
        Vector3 lookOffset = composer != null ? composer.TargetOffset : Vector3.zero;
        Vector3 lift = lookOffset * w;
        if (w > 0.001f)
        {
            distance = Mathf.Min(distance, ClearDistance(focus.position + lookOffset, back, distance));
        }

        follow.FollowOffset = lift + back * distance;

        var trackerSettings = follow.TrackerSettings;
        // The follow's damping is for the player's steps: on a turn of the orbit it made the camera's place trail its
        // aim, so the picture swam while turning. While the view turns, the damping gives way (back as it settles).
        float turning = Mathf.Clamp01(Mathf.Abs(yawVelocity) / 40f);
        trackerSettings.PositionDamping = Vector3.Lerp(Vector3.Lerp(positionDamping, settings.ActionPositionDamping, w), Vector3.zero, turning);
        follow.TrackerSettings = trackerSettings;

        if (composer != null)
        {
            composer.Damping = Vector2.Lerp(Vector2.Lerp(composerDamping, Vector2.zero, w), Vector2.zero, turning);
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
