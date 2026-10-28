using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerStateMachine))]
public sealed class PlayerInputHandler : MonoBehaviour
{
    // Ignore the cursor this close to the player so facing doesn't jitter.
    private const float MinAimDistance = 0.5f;
    // How far the action camera's crosshair looks for something to aim at.
    private const float CrosshairRange = 60f;
    // Movement input above this magnitude counts as a deliberate press that cancels an interaction.
    private const float InteractionCancelThreshold = 0.5f;

    [SerializeField] private Camera movementCamera;

    private PlayerMovement movement;
    private PlayerStateMachine stateMachine;

    private InputAction moveAction;
    private InputAction combatAction;
    private InputAction interactAction;
    private InputAction cancelAction;
    private InputAction attackAction;
    private InputAction guardAction;
    private InputAction inventoryAction;
    private InputAction itemsAction;
    private InputAction dodgeAction;
    private InputAction skillsWindowAction;
    private InputAction characterAction;
    private InputAction buildAction;
    private InputAction sneakAction;
    private InputAction arrowAction;
    private PlayerStealth stealth;
    private readonly InputAction[] skillActions = new InputAction[SkillLoadout.SlotCount];

    // V Rising style skill keys (E is taken by Interact, C by the character screen), with 1-5 as alternatives.
    private static readonly string[] SkillKeys = { "q", "r", "f", "x", "t" };
    private static readonly string[] SkillPads = { "buttonNorth", "dpad/up", "dpad/right", "dpad/down", "dpad/left" };

    /// <summary>The keyboard key that casts a skill bar slot, for labels in the UI.</summary>
    public static string SkillKeyLabel(int slot) => slot >= 0 && slot < SkillKeys.Length ? SkillKeys[slot].ToUpperInvariant() : "";

    private Vector2 moveInput;
    private bool mouseAttackPending;
    private bool mouseGuardPending;
    private bool mouseAttackReleasePending;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();
        stealth = GetComponent<PlayerStealth>();
        movementCamera = movementCamera != null ? movementCamera : Camera.main;

        if (movementCamera == null)
        {
            Debug.LogError("PlayerInputHandler requires a camera to calculate camera-relative movement.", this);
            enabled = false;
            return;
        }

        CreateActions();
        BindActions();
    }

    private void OnEnable()
    {
        GamePause.Changed += OnPauseChanged;
        BackHandlers.Register(BackHandlers.InteractionPriority, CancelInteraction);
        SetActionsEnabled(!GamePause.IsPaused);
    }

    private void OnDisable()
    {
        GamePause.Changed -= OnPauseChanged;
        BackHandlers.Unregister(CancelInteraction);
        SetActionsEnabled(false);
    }

    private void OnPauseChanged(bool paused)
    {
        SetActionsEnabled(!paused);
    }

    private void SetActionsEnabled(bool enable)
    {
        foreach (InputAction action in AllActions())
        {
            if (action == null)
            {
                continue;
            }

            if (enable)
            {
                action.Enable();
            }
            else
            {
                action.Disable();
            }
        }

        if (enable)
        {
            return;
        }

        moveInput = Vector2.zero;
        mouseAttackPending = false;
        mouseGuardPending = false;
        mouseAttackReleasePending = false;
        stateMachine?.ReleaseAttack();
        stateMachine?.RequestGuard(false);
        movement?.SetMovementInput(Vector3.zero);
        movement?.ClearAimDirection();
        stealth?.SetSneaking(false);
    }

    private System.Collections.Generic.IEnumerable<InputAction> AllActions()
    {
        yield return moveAction;
        yield return combatAction;
        yield return interactAction;
        yield return cancelAction;
        yield return attackAction;
        yield return guardAction;
        yield return inventoryAction;
        yield return itemsAction;
        yield return dodgeAction;
        yield return skillsWindowAction;
        yield return characterAction;
        yield return buildAction;
        yield return sneakAction;
        yield return arrowAction;
        foreach (InputAction action in skillActions)
        {
            yield return action;
        }
    }

    private void Update()
    {
        if (GamePause.IsPaused)
        {
            return;
        }

        ProcessPendingMouseClicks();

        Vector3 cameraForward = Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(movementCamera.transform.right, Vector3.up).normalized;
        Vector3 direction = cameraRight * moveInput.x + cameraForward * moveInput.y;
        movement.SetMovementInput(Vector3.ClampMagnitude(direction, 1f));
        UpdateAimDirection();
    }

    // Direction from the player to the point being aimed at: the cursor's point on the player's ground plane, or, with the
    // action camera, whatever is under the crosshair. Cleared without a mouse in the top-down style.
    private void UpdateAimDirection()
    {
        if (CameraPointer.UsesScreenCenter)
        {
            movement.SetAimDirection(CrosshairAimDirection());
            return;
        }

        if (!CameraPointer.TryGetRay(movementCamera, out Ray ray))
        {
            movement.ClearAimDirection();
            return;
        }

        Plane ground = new Plane(Vector3.up, transform.position);
        if (!ground.Raycast(ray, out float distance))
        {
            movement.ClearAimDirection();
            return;
        }

        Vector3 toCursor = ray.GetPoint(distance) - transform.position;
        toCursor.y = 0f;
        if (toCursor.sqrMagnitude < MinAimDistance * MinAimDistance)
        {
            movement.ClearAimDirection();
            return;
        }

        movement.SetAimDirection(toCursor);
    }

    // The crosshair ray starts behind the player, so the cast begins level with the player to skip the player's own
    // colliders. Aims at what it hits, or far along the view; level with the ground either way.
    private Vector3 CrosshairAimDirection()
    {
        Vector3 forward = Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up);
        if (!CameraPointer.TryGetRay(movementCamera, out Ray ray))
        {
            return forward;
        }

        float skip = Mathf.Max(0f, Vector3.Dot(transform.position - ray.origin, ray.direction)) + 0.5f;
        var start = new Ray(ray.GetPoint(skip), ray.direction);
        Vector3 point = Physics.Raycast(start, out RaycastHit hit, CrosshairRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            ? hit.point
            : start.GetPoint(CrosshairRange);

        Vector3 toPoint = point - transform.position;
        toPoint.y = 0f;
        return toPoint.sqrMagnitude < MinAimDistance * MinAimDistance ? forward : toPoint;
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
        combatAction?.Dispose();
        interactAction?.Dispose();
        cancelAction?.Dispose();
        attackAction?.Dispose();
        guardAction?.Dispose();
        inventoryAction?.Dispose();
        itemsAction?.Dispose();
        dodgeAction?.Dispose();
        skillsWindowAction?.Dispose();
        characterAction?.Dispose();
        buildAction?.Dispose();
        sneakAction?.Dispose();
        arrowAction?.Dispose();
        foreach (InputAction action in skillActions)
        {
            action?.Dispose();
        }
    }

    private void CreateActions()
    {
        moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");

        combatAction = new InputAction("ToggleCombat", InputActionType.Button);
        combatAction.AddBinding("<Keyboard>/tab");
        combatAction.AddBinding("<Gamepad>/rightShoulder");

        interactAction = new InputAction("Interact", InputActionType.Button);
        interactAction.AddBinding("<Keyboard>/e");
        interactAction.AddBinding("<Gamepad>/buttonWest");

        // Escape is routed through BackHandlers by the Escape menu (see CancelInteraction); the gamepad keeps B.
        cancelAction = new InputAction("CancelInteraction", InputActionType.Button);
        cancelAction.AddBinding("<Gamepad>/buttonEast");

        attackAction = new InputAction("Attack", InputActionType.Button);
        attackAction.AddBinding("<Mouse>/leftButton");
        attackAction.AddBinding("<Gamepad>/rightTrigger");

        // Hold to guard; raised just before a hit it parries. Attack while guarding with a shield is the shield bash.
        guardAction = new InputAction("Guard", InputActionType.Button);
        guardAction.AddBinding("<Mouse>/rightButton");
        guardAction.AddBinding("<Gamepad>/leftTrigger");

        inventoryAction = new InputAction("ToggleInventory", InputActionType.Button);
        inventoryAction.AddBinding("<Keyboard>/i");
        inventoryAction.AddBinding("<Gamepad>/select");

        itemsAction = new InputAction("ToggleItems", InputActionType.Button);
        itemsAction.AddBinding("<Keyboard>/b");
        itemsAction.AddBinding("<Gamepad>/start");

        dodgeAction = new InputAction("Dodge", InputActionType.Button);
        dodgeAction.AddBinding("<Keyboard>/space");
        dodgeAction.AddBinding("<Gamepad>/buttonSouth");

        skillsWindowAction = new InputAction("ToggleSkills", InputActionType.Button);
        skillsWindowAction.AddBinding("<Keyboard>/k");
        skillsWindowAction.AddBinding("<Gamepad>/leftStickPress");

        characterAction = new InputAction("ToggleCharacter", InputActionType.Button);
        characterAction.AddBinding("<Keyboard>/c");

        buildAction = new InputAction("ToggleBuild", InputActionType.Button);
        buildAction.AddBinding("<Keyboard>/g");
        buildAction.AddBinding("<Gamepad>/rightStickPress");

        // Held: sneaking lasts as long as the button is down.
        sneakAction = new InputAction("Sneak", InputActionType.Button);
        sneakAction.AddBinding("<Keyboard>/leftCtrl");
        sneakAction.AddBinding("<Gamepad>/leftShoulder");

        // A bow's next kind of arrow.
        arrowAction = new InputAction("CycleArrows", InputActionType.Button);
        arrowAction.AddBinding("<Keyboard>/z");

        for (int i = 0; i < skillActions.Length; i++)
        {
            var action = new InputAction("Skill" + (i + 1), InputActionType.Button);
            action.AddBinding("<Keyboard>/" + SkillKeys[i]);
            action.AddBinding("<Keyboard>/" + (i + 1));
            action.AddBinding("<Gamepad>/" + SkillPads[i]);
            skillActions[i] = action;
        }
    }

    private void BindActions()
    {
        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        combatAction.performed += OnCombatToggle;
        interactAction.performed += OnInteract;
        cancelAction.performed += OnCancel;
        attackAction.performed += OnAttack;
        attackAction.canceled += OnAttackReleased;
        guardAction.performed += OnGuard;
        guardAction.canceled += OnGuardReleased;
        inventoryAction.performed += OnInventoryToggle;
        itemsAction.performed += OnItemsToggle;
        dodgeAction.performed += OnDodge;
        skillsWindowAction.performed += OnSkillsToggle;
        characterAction.performed += OnCharacterToggle;
        buildAction.performed += OnBuildToggle;
        sneakAction.performed += _ => stealth?.SetSneaking(true);
        sneakAction.canceled += _ => stealth?.SetSneaking(false);
        arrowAction.performed += _ => stateMachine.RequestArrowCycle();
        for (int i = 0; i < skillActions.Length; i++)
        {
            int slot = i;
            skillActions[i].performed += _ => stateMachine.RequestSkill(slot);
        }
    }

    private void OnDodge(InputAction.CallbackContext context)
    {
        stateMachine.RequestDodge();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        moveInput = Vector2.ClampMagnitude(context.ReadValue<Vector2>(), 1f);

        // Only a new press cancels, so a direction already held when the interaction began does not.
        if (context.performed
            && stateMachine.CurrentState == PlayerState.Interacting
            && moveInput.sqrMagnitude >= InteractionCancelThreshold * InteractionCancelThreshold)
        {
            stateMachine.EndInteraction();
        }
    }

    private void OnCombatToggle(InputAction.CallbackContext context)
    {
        if (stateMachine.CurrentState == PlayerState.Interacting)
        {
            return;
        }

        GameStateManager gameState = GameStateManager.Instance;
        if (gameState == null)
        {
            Debug.LogError("PlayerInputHandler requires a GameStateManager in the scene to toggle combat.", this);
            return;
        }

        gameState.TrySetState(gameState.CurrentState == GameState.Combat ? GameState.Exploration : GameState.Combat);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        stateMachine.RequestInteraction();
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (IsMouseClick(context))
        {
            mouseAttackPending = true;
            return;
        }

        stateMachine.RequestAttack();
    }

    // A release that comes before its press was processed (a very quick click) waits for it.
    private void OnAttackReleased(InputAction.CallbackContext context)
    {
        if (mouseAttackPending)
        {
            mouseAttackReleasePending = true;
            return;
        }

        stateMachine.ReleaseAttack();
    }

    private void OnGuard(InputAction.CallbackContext context)
    {
        if (IsMouseClick(context))
        {
            mouseGuardPending = true;
            return;
        }

        stateMachine.RequestGuard(true);
    }

    private void OnGuardReleased(InputAction.CallbackContext context)
    {
        mouseGuardPending = false;
        stateMachine.RequestGuard(false);
    }

    private void OnInventoryToggle(InputAction.CallbackContext context)
    {
        RequestWindow(UIWindowId.Resources);
    }

    private void OnItemsToggle(InputAction.CallbackContext context)
    {
        RequestWindow(UIWindowId.Items);
    }

    private void OnSkillsToggle(InputAction.CallbackContext context)
    {
        RequestWindow(UIWindowId.Skills);
    }

    private void OnCharacterToggle(InputAction.CallbackContext context)
    {
        RequestWindow(UIWindowId.Character);
    }

    private void OnBuildToggle(InputAction.CallbackContext context)
    {
        if (TryGetComponent(out BuildPlacer builder))
        {
            builder.ToggleMenu();
        }
    }

    private void RequestWindow(UIWindowId window)
    {
        UIStateManager uiState = UIStateManager.Instance;
        if (uiState == null)
        {
            Debug.LogError($"PlayerInputHandler requires a UIStateManager in the scene to toggle the {window} window.", this);
            return;
        }

        uiState.RequestToggle(window);
    }

    private static bool IsMouseClick(InputAction.CallbackContext context)
    {
        return context.control.device is Mouse;
    }

    // The UI can't be queried from inside an input callback (it reports last frame's state), so mouse clicks are
    // recorded there and checked here. Clicks on UI (resource tabs, scrollbar) must not also swing the weapon.
    private void ProcessPendingMouseClicks()
    {
        if (!mouseAttackPending && !mouseGuardPending)
        {
            return;
        }

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        // While placing a building the mouse buttons place and cancel instead of attacking.
        if (!overUI && !BuildPlacer.IsBusy)
        {
            if (mouseAttackPending)
            {
                stateMachine.RequestAttack();
            }

            if (mouseAttackPending && mouseAttackReleasePending)
            {
                stateMachine.ReleaseAttack();
            }

            if (mouseGuardPending)
            {
                stateMachine.RequestGuard(true);
            }
        }

        mouseAttackPending = false;
        mouseAttackReleasePending = false;
        mouseGuardPending = false;
    }

    private void OnCancel(InputAction.CallbackContext context)
    {
        stateMachine.EndInteraction();
    }

    private bool CancelInteraction()
    {
        return stateMachine != null && stateMachine.EndInteraction();
    }
}
