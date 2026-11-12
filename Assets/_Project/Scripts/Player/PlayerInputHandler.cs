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

    private PlayerStealth stealth;
    private PlayerEquipment equipment;
    private PlayerLantern lantern;
    private CommandWheel commandWheel;
    // The skill actions' callbacks, one per slot, kept so they can be unsubscribed.
    private readonly System.Action<InputAction.CallbackContext>[] skillCallbacks = new System.Action<InputAction.CallbackContext>[SkillLoadout.SlotCount];

    /// <summary>The keyboard key that casts a skill bar slot, for labels in the UI (from the Input Actions asset, so it
    /// follows a rebinding).</summary>
    public static string SkillKeyLabel(int slot) => slot >= 0 && slot < SkillLoadout.SlotCount ? GameInput.KeyLabel(GameInput.Skill(slot)) : "";

    private Vector2 moveInput;
    private bool mouseAttackPending;
    private bool mouseGuardPending;
    private bool mouseAttackReleasePending;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        stateMachine = GetComponent<PlayerStateMachine>();
        stealth = GetComponent<PlayerStealth>();
        equipment = GetComponent<PlayerEquipment>();
        lantern = GetComponent<PlayerLantern>();
        movementCamera = movementCamera != null ? movementCamera : Camera.main;

        if (movementCamera == null)
        {
            Debug.LogError("PlayerInputHandler requires a camera to calculate camera-relative movement.", this);
            enabled = false;
            return;
        }

        commandWheel = GetComponent<CommandWheel>();
        if (commandWheel == null)
        {
            commandWheel = gameObject.AddComponent<CommandWheel>();
        }

        ItemUseBar.For(gameObject);

        if (!TryGetComponent(out EnemyVisibility _))
        {
            gameObject.AddComponent<EnemyVisibility>();
        }

        for (int i = 0; i < skillCallbacks.Length; i++)
        {
            int slot = i;
            skillCallbacks[i] = _ => stateMachine.RequestSkill(slot);
        }
    }

    // The actions are the project-wide ones (GameInput), shared with the rest of the game: subscribed here and
    // unsubscribed in OnDisable, so a player that is gone leaves no callbacks behind.
    private void OnEnable()
    {
        GamePause.Changed += OnPauseChanged;
        BackHandlers.Register(BackHandlers.InteractionPriority, CancelInteraction);
        Subscribe(true);
        SetActionsEnabled(!GamePause.IsPaused);
    }

    private void OnDisable()
    {
        commandWheel?.Cancel();
        GamePause.Changed -= OnPauseChanged;
        BackHandlers.Unregister(CancelInteraction);
        Subscribe(false);
        SetActionsEnabled(false);
    }

    private void OnPauseChanged(bool paused)
    {
        SetActionsEnabled(!paused);
    }

    // The whole Player map goes off while paused (or without a player), so no gameplay key acts behind the menu.
    private void SetActionsEnabled(bool enable)
    {
        if (!enable)
        {
            commandWheel?.Cancel();
        }

        InputActionMap map = GameInput.PlayerMap;
        if (map != null)
        {
            if (enable)
            {
                map.Enable();
            }
            else
            {
                map.Disable();
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
        stateMachine.ReleaseAttack();
        stateMachine.RequestGuard(false);
        movement.SetMovementInput(Vector3.zero);
        movement.SetMoveKeys(Vector2.zero);
        movement.ClearAimDirection();
        if (stealth != null)
        {
            stealth.SetSneaking(false);
        }
    }

    // The player stands and listens to a conversation; in a fight the feet stay free, whatever is being asked.
    private static bool IsListening => DialogueBox.IsOpen && !MonsterAI.AnyHuntingPlayerSide();

    private void Update()
    {
        if (GamePause.IsPaused)
        {
            return;
        }

        ProcessPendingMouseClicks();

        Vector3 cameraForward = Vector3.ProjectOnPlane(movementCamera.transform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(movementCamera.transform.right, Vector3.up).normalized;
        Vector2 walk = IsListening ? Vector2.zero : moveInput;
        Vector3 direction = cameraRight * walk.x + cameraForward * walk.y;
        movement.SetMovementInput(Vector3.ClampMagnitude(direction, 1f));
        movement.SetMoveKeys(walk);
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

        // On a controller the mouse lies where it was left; facing it would turn every swing towards that spot. The player
        // faces where they walk instead (and the enemy fought, with Focus on Enemy).
        if (InputDevices.UsingGamepad || !CameraPointer.TryGetRay(movementCamera, out Ray ray))
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

    private void Subscribe(bool on)
    {
        Hook(GameInput.Move, OnMove, on, canceled: true);
        Hook(GameInput.ToggleCombat, OnCombatToggle, on);
        Hook(GameInput.Interact, OnInteract, on);
        Hook(GameInput.CancelInteraction, OnCancel, on);
        Hook(GameInput.Attack, OnAttack, on);
        Hook(GameInput.Attack, OnAttackReleased, on, performed: false, canceled: true);
        Hook(GameInput.Guard, OnGuard, on);
        Hook(GameInput.Guard, OnGuardReleased, on, performed: false, canceled: true);
        Hook(GameInput.Inventory, OnInventoryToggle, on);
        Hook(GameInput.Items, OnItemsToggle, on);
        Hook(GameInput.Dodge, OnDodge, on);
        Hook(GameInput.Skills, OnSkillsToggle, on);
        Hook(GameInput.Character, OnCharacterToggle, on);
        Hook(GameInput.Build, OnBuildToggle, on);
        Hook(GameInput.Sneak, OnSneak, on, canceled: true);
        Hook(GameInput.CycleArrows, OnCycleArrows, on);
        Hook(GameInput.ToggleTorch, OnToggleTorch, on);
        Hook(GameInput.SwapWeapon, OnSwapWeapon, on);
        Hook(GameInput.Talents, OnTalents, on);
        Hook(GameInput.Journal, OnJournal, on);
        Hook(GameInput.Tutorial, OnTutorial, on);
        Hook(GameInput.Haven, OnHaven, on);
        Hook(GameInput.WorldMap, OnWorldMap, on);
        Hook(GameInput.CommandWheel, OnCommandWheel, on);
        Hook(GameInput.CommandWheel, OnCommandWheelReleased, on, performed: false, canceled: true);
        Hook(GameInput.QuickItems, OnQuickItems, on);
        Hook(GameInput.QuickItems, OnCommandWheelReleased, on, performed: false, canceled: true);
        for (int i = 0; i < skillCallbacks.Length; i++)
        {
            Hook(GameInput.Skill(i), skillCallbacks[i], on);
        }
    }

    private static void Hook(InputAction action, System.Action<InputAction.CallbackContext> callback, bool on,
        bool performed = true, bool canceled = false)
    {
        if (action == null)
        {
            return;
        }

        if (performed)
        {
            if (on) action.performed += callback; else action.performed -= callback;
        }

        if (canceled)
        {
            if (on) action.canceled += callback; else action.canceled -= callback;
        }
    }

    private void OnSneak(InputAction.CallbackContext context)
    {
        if (stealth == null)
        {
            return;
        }

        if (context.performed)
        {
            // The first crouch opens the sneaking tutorial, before any creature is near enough to train it.
            JournalNotices.Tried(ProficiencyTraining.Sneaking);
        }

        if (!GameOptions.ToggleSneak.On)
        {
            stealth.SetSneaking(context.performed);
        }
        else if (context.performed)
        {
            stealth.SetSneaking(!stealth.IsSneaking);
        }
    }

    private void OnCycleArrows(InputAction.CallbackContext context) => stateMachine.RequestArrowCycle();

    // The light key: the lantern on the belt when one is worn, else the torch in the off hand.
    private void OnToggleTorch(InputAction.CallbackContext context)
    {
        if (lantern != null && lantern.Toggle())
        {
            return;
        }

        if (equipment != null)
        {
            equipment.ToggleTorch();
        }
    }

    private void OnSwapWeapon(InputAction.CallbackContext context)
    {
        if (equipment != null)
        {
            equipment.SwapWeapon();
        }
    }

    private void OnTalents(InputAction.CallbackContext context) => RequestWindow(UIWindowId.Proficiencies);
    private void OnJournal(InputAction.CallbackContext context) => RequestWindow(UIWindowId.Journal);

    private void OnTutorial(InputAction.CallbackContext context) => TutorialWindow.Toggle();

    private void OnHaven(InputAction.CallbackContext context) => RequestWindow(UIWindowId.Haven);

    private void OnWorldMap(InputAction.CallbackContext context)
    {
        WorldMapHud.Toggle();
    }

    private void OnCommandWheel(InputAction.CallbackContext context) => commandWheel?.Open();

    private void OnQuickItems(InputAction.CallbackContext context) => commandWheel?.OpenQuickItems();

    private void OnCommandWheelReleased(InputAction.CallbackContext context) => commandWheel?.CloseAndExecute();

    private void OnDodge(InputAction.CallbackContext context)
    {
        if (IsListening)
        {
            return;
        }

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

        GameStateManager gameState = GameStateManager.Current;
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
        // Essences are shown in the bag now, so both keys open it.
        RequestWindow(UIWindowId.Items);
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
        UIStateManager uiState = UIStateManager.Current;
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
        if (!overUI && !DialogueBox.ClaimsClick && !BuildPlacer.IsBusy)
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
