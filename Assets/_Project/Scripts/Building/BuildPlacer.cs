using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// The player's building (GDD §14.1): G opens the build menu (BuildMenuWindow) with every known structure and its cost; choosing one shows
/// a ghost under the mouse that is green where it may stand and red elsewhere. Left-click builds it there instantly,
/// paying the cost from what is carried and stored; right-click or Esc cancels. "Move a building" picks up a placed
/// building and puts it down somewhere else for free.
/// <para>
/// Placement: the mouse wheel turns the ghost (hold Shift for small steps). It snaps to a grid, and flush against the
/// side of a nearby building, turned to match it; hold Alt to place freely. Buildings with a yard (BuildingDefinition)
/// keep the ground in front of their entrance clear: the yard is drawn under the ghost and under every placed building
/// while building, and nothing may be built on one. Buildings that are placed in lines (walls, palisades) are drawn by
/// dragging: pieces are laid end to end, start and finish snap to the ends of others like them, and only the pieces that
/// fit and can be paid for are built. Geometry lives in BuildPlacement; what the player sees while placing in BuildGhost.
/// </para>
/// <para>
/// Where and what may be built is the current build area's (<see cref="IBuildArea"/>: the portal's ward at home,
/// the expedition's camp in the prologue), so this class knows neither.
/// </para>
/// <para>
/// Placed buildings are created with SaveableEntity.Spawn so the save system recreates them, and raise a Built game event
/// for quests. Where it belongs: on the PlayerArmature prefab. Tuning (grid, snapping, colors) is here; costs and
/// placement rules are on each BuildingDefinition; the area's own rules on the area.
/// </para>
/// <para>
/// The placing session (the menu, picking, the ghost, confirming and building) is here; its parts are .Spots (where a
/// piece may stand and whether it is paid for) and .Lines (walls drawn by dragging).
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed partial class BuildPlacer : MonoBehaviour, IPlayerActivity
{
    // Overlap boxes start this far above the build plane so the ground itself doesn't count.
    private const float GroundLift = 0.15f;

    [Tooltip("Transparent material the ghost is drawn with. Tinted with the colors below.")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField] private Color validColor = new Color(0.45f, 1f, 0.55f, 0.45f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.35f, 0.3f, 0.45f);
    [Tooltip("The ghost's yard: the ground kept clear in front of its entrance.")]
    [SerializeField] private Color yardColor = new Color(1f, 0.85f, 0.35f, 0.4f);
    [Tooltip("Yards of buildings already placed, shown while building.")]
    [SerializeField] private Color placedYardColor = new Color(1f, 0.95f, 0.75f, 0.28f);
    [Tooltip("Positions snap to this grid, in meters. 0 turns grid snapping off.")]
    [SerializeField, Min(0f)] private float gridSize = 0.5f;
    [Tooltip("Degrees turned per mouse-wheel step.")]
    [SerializeField, Range(5f, 90f)] private float rotationStep = 45f;
    [Tooltip("Degrees turned per mouse-wheel step while Shift is held.")]
    [SerializeField, Range(1f, 45f)] private float fineRotationStep = 15f;
    [Tooltip("A building this close (meters) to the side of another snaps flush against it. 0 turns it off.")]
    [SerializeField, Min(0f)] private float snapDistance = 0.75f;
    [Tooltip("Lines (walls, palisades) point in multiples of this many degrees unless Alt is held. 0 = any angle.")]
    [SerializeField, Range(0f, 90f)] private float lineAngleStep = 15f;
    [Tooltip("Most pieces one drag can build.")]
    [SerializeField, Range(2, 60)] private int maxLinePieces = 20;
    [Tooltip("Layers whose colliders block a building. Ground is ignored because the check starts above it.")]
    [SerializeField] private LayerMask blockingLayers = ~0;

    private static readonly Collider[] Overlaps = new Collider[16];

    private readonly List<Vector3> lineCenters = new List<Vector3>();
    private readonly List<Vector3> lineDirections = new List<Vector3>();
    // A line bent round the portal (scroll while dragging a line), so a fence can follow the ward's circle.
    private bool lineRound;
    private readonly List<ResourceAmount> scaledResources = new List<ResourceAmount>();
    private readonly List<ItemAmount> scaledItems = new List<ItemAmount>();

    private BuildingDefinition placing;
    private BuildingObject moving;
    private bool picking;
    // Picking a building to upgrade or repair rather than to move.
    private bool upgrading;
    private BuildGhost ghost;
    private float yaw;
    private Camera view;

    // A line being dragged out. A short drag builds the single piece shown when the button went down.
    private bool drawingLine;
    private Vector3 lineStart;
    private Vector3 pressPoint;
    private float pressYaw;

    /// <summary>True while any player is placing, moving or picking a building: mouse buttons and wheel belong to building.</summary>
    public static bool IsBusy { get; private set; }

    /// <summary>True while a controller's right stick moves the building's spot (top-down view): the camera leaves it alone.</summary>
    public static bool UsesPadSpot { get; private set; }

    // On a controller in the top-down view: where the spot is from the player, who carries it along while walking.
    private Vector3 padOffset;
    private bool padSpotPlaced;
    // Where the spot is while picking a building on a controller (no ghost shows then): a flat disc on the ground.
    private GameObject padMarker;

    // The town while building: the "Here" readout, radii and yards (BuildOverlay), made the first time it is needed.
    private BuildOverlay overlay;
    private BuildOverlay Overlay => overlay != null ? overlay : overlay = BuildOverlay.Create(ghostMaterial);

    private BuildingDefinition Kind => placing != null ? placing : moving != null ? moving.Definition : null;
    private static IBuildArea Area => BuildArea.Current;
    /// <summary>True inside an open build area (Safe Haven's ward), where the build menu works.</summary>
    public static bool HasBuildArea => Area != null && Area.WhyClosed == null;
    private Bounds LocalBounds => ghost.LocalBounds;

    private void Awake()
    {
        ghost = new BuildGhost(ghostMaterial, validColor, invalidColor, yardColor, placedYardColor);
    }

    private void OnEnable()
    {
        BackHandlers.Register(BackHandlers.BuildPlacementPriority, CancelByBack);
        PlayerInterrupts.For(gameObject).Add(this);
    }

    private void OnDisable()
    {
        BackHandlers.Unregister(CancelByBack);
        if (TryGetComponent(out PlayerInterrupts interrupts))
        {
            interrupts.Remove(this);
        }

        Cancel();
    }

    // The Build map stays on behind a pause (the cheat window, a cinematic) and on a body: a click there placed a building. Building
    // stops instead, a carried building going back where it stood.
    public PlayerInterrupt EndsOn => PlayerInterrupt.Paused;

    // A pause or a death (PlayerInterrupts).
    public void Interrupt(PlayerInterrupt reason)
    {
        if (IsBusy)
        {
            Cancel();
        }
    }

    private bool CancelByBack()
    {
        if (!IsBusy)
        {
            return false;
        }

        Cancel();
        return true;
    }

    /// <summary>Opens or closes the build menu (BuildMenuWindow). Only works in an open build area.</summary>
    public void ToggleMenu()
    {
        if (IsBusy)
        {
            Cancel();
            return;
        }

        BuildMenuWindow menu = BuildMenuWindow.Current;
        if (menu != null && menu.IsOpen)
        {
            menu.Close();
            return;
        }

        // A friend's Haven: building there takes their yes (what is built then reaches them through HavenSync).
        if (HavenVisit.BuildingNeedsAsking())
        {
            return;
        }

        if (!HasBuildArea)
        {
            GameMessages.Post(Area != null ? Area.WhyClosed : "You can only build in Safe Haven.", MessageKind.Warning);
            return;
        }

        BuildMenuWindow.Create().Open(this);
    }

    /// <summary>The build area's opening line: where and what to build.</summary>
    public string Guidance => HasBuildArea ? Area.Guidance : "";

    /// <summary>True when buildings may be moved, upgraded and repaired here (in a friend's Haven, with their yes).</summary>
    public bool AllowsChanges => HasBuildArea && Area.AllowsChanges && (!HavenVisit.IsVisiting || HavenVisit.MayBuild);

    /// <summary>What can be built here now, in display order. Empty outside an open build area.</summary>
    public List<BuildingDefinition> Buildable()
    {
        var buildings = new List<BuildingDefinition>();
        var crafting = GetComponent<PlayerCrafting>();
        if (!HasBuildArea || crafting == null)
        {
            return buildings;
        }

        foreach (BuildingDefinition building in Area.Buildable(crafting))
        {
            if (building != null && building.Prefab != null)
            {
                buildings.Add(building);
            }
        }

        buildings.Sort(RecipeBook.CompareForDisplay);
        return buildings;
    }

    /// <summary>Starts placing <paramref name="building"/>: a ghost follows the pointer until it is built or cancelled.</summary>
    public void Place(BuildingDefinition building)
    {
        // Every way in (the build menu, the Haven window) asks a friend's yes in their Haven.
        if (HavenVisit.BuildingNeedsAsking())
        {
            return;
        }

        if (building == null || building.Prefab == null || !HasBuildArea)
        {
            return;
        }

        // Every way in is held to what the build menu offers (known, its requirements met: IBuildArea.Buildable); the Haven
        // window's "Build another" asked only whether it was known.
        var crafting = GetComponent<PlayerCrafting>();
        if (crafting != null && !IsBuildableHere(building, crafting) && !(crafting.IsKnown(building) && BuildingUnlocks.IsOpen(building)))
        {
            GameMessages.Post(!crafting.IsKnown(building) ? $"You don't know how to build the {building.DisplayName} yet."
                : !BuildingUnlocks.IsOpen(building) ? $"Not open yet: {BuildingUnlocks.Missing(building)}"
                : $"The {building.DisplayName} can't be built here.", MessageKind.Warning);
            return;
        }

        BeginPlacing(building);
    }

    private static bool IsBuildableHere(BuildingDefinition building, PlayerCrafting crafting)
    {
        foreach (BuildingDefinition offered in Area.Buildable(crafting))
        {
            if (offered == building)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Lets the player click a building they built, to upgrade or repair it (true) or to move it (false).</summary>
    public void PickBuilding(bool toUpgrade)
    {
        // Not moved during a breach: carried off, it left BuildingObject.Active, so the attackers, the repairs and the renown
        // lost it until it was set down again.
        if (!toUpgrade && !HavenTravel.IsOpen)
        {
            GameMessages.Post("Not while the ward is breached.", MessageKind.Warning);
            return;
        }

        if (!HavenVisit.BuildingNeedsAsking() && AllowsChanges)
        {
            BeginPicking(toUpgrade);
        }
    }

    /// <summary>The turn that points a piece's front (+Z) at the camera, on the rotation step.</summary>
    private float FacingCamera()
    {
        Camera view = Camera.main;
        Vector3 toCamera = view != null ? -view.transform.forward : Vector3.back;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 1e-4f)
        {
            return 180f;
        }

        float facing = Mathf.Atan2(toCamera.x, toCamera.z) * Mathf.Rad2Deg;
        return Mathf.Repeat(Mathf.Round(facing / rotationStep) * rotationStep, 360f);
    }

    private void BeginPlacing(BuildingDefinition building)
    {
        Cancel();
        placing = building;
        // A building with a front (its yard) starts turned to show it to the camera.
        yaw = building.YardDepth > 0f && building.YardSide == YardSide.Front ? FacingCamera() : 0f;
        CreateGhost(building.Prefab.transform);

        var hint = new StringBuilder();
        if (InputDevices.UsingGamepad)
        {
            PadFamily pad = InputDevices.Family;
            hint.Append($"Place the {building.DisplayName}: {GameInput.KeyLabel(GameInput.BuildPlace)} to build");
            hint.Append(building.PlaceInLines ? ", or hold it and walk to build a line" : "");
            hint.Append(CameraPointer.UsesScreenCenter ? ". Aim with the camera" : ". The right stick moves the spot");
            hint.Append($", {ControllerGlyphs.For("<Gamepad>/leftShoulder", pad)} {ControllerGlyphs.For("<Gamepad>/rightShoulder", pad)} turn it");
            hint.Append($" (hold {GameInput.KeyLabel(GameInput.BuildFineRotate)} for small turns, {GameInput.KeyLabel(GameInput.BuildFreePlace)} to place freely), {GameInput.KeyLabel(GameInput.Back)} cancels.");
        }
        else
        {
            hint.Append($"Place the {building.DisplayName}: left-click to build");
            hint.Append(building.PlaceInLines ? ", or drag to build a line (scroll while dragging to bend it round the portal)." : ".");
        }
        if (building.YardDepth > 0f)
        {
            hint.Append(" Keep the marked ground in front of it clear.");
        }

        GameMessages.Post(hint.ToString());
    }

    private void BeginPicking(bool toUpgrade)
    {
        Cancel();
        picking = true;
        upgrading = toUpgrade;
        IsBusy = true;
        if (InputDevices.UsingGamepad)
        {
            string choose = $"{(CameraPointer.UsesScreenCenter ? "Aim at" : "Move the spot onto")} a building and press {GameInput.KeyLabel(GameInput.BuildPlace)}";
            GameMessages.Post($"{choose} to {(toUpgrade ? "upgrade or repair it" : "move it")}. {GameInput.KeyLabel(GameInput.Back)} cancels.");
            return;
        }

        GameMessages.Post(toUpgrade ? "Click a building to upgrade or repair it. Right-click to cancel." : "Click a building to move it. Right-click to cancel.");
    }

    private void Update()
    {
        if (!IsBusy)
        {
            return;
        }

        // Escape cancels through BackHandlers (CancelByBack), so the Escape menu doesn't also open.
        if (Mouse.current == null || !HasBuildArea)
        {
            Cancel();
            return;
        }

        // Build map of the project-wide actions (GameInput): place, cancel, turn, and the Shift/Alt modifiers.
        if (GameInput.BuildCancel.WasPressedThisFrame())
        {
            if (drawingLine)
            {
                drawingLine = false;
            }
            else
            {
                Cancel();
            }

            return;
        }

        view = view != null ? view : Camera.main;
        if (view == null)
        {
            return;
        }

        UpdatePadSpot();
        if (picking)
        {
            UpdatePicking();
            return;
        }

        bool free = GameInput.BuildFreePlace.IsPressed();
        bool fine = GameInput.BuildFineRotate.IsPressed();
        float scroll = GameInput.BuildRotate.ReadValue<float>();
        // The wheel turns a notch per frame it moves; a bumper is held, so it turns once per press.
        if (InputDevices.UsingGamepad && !GameInput.BuildRotate.WasPressedThisFrame())
        {
            scroll = 0f;
        }
        if (!drawingLine && Mathf.Abs(scroll) > 0.01f)
        {
            yaw = Mathf.Repeat(yaw + Mathf.Sign(scroll) * (fine ? fineRotationStep : rotationStep), 360f);
        }
        else if (drawingLine && Mathf.Abs(scroll) > 0.01f && lineRound != scroll > 0f)
        {
            // Scrolling up bends the line round the portal; down straightens it again.
            lineRound = scroll > 0f;
            GameMessages.Post(lineRound ? "The line bends round the portal." : "The line runs straight.");
        }

        if (!Area.TryGetGroundPoint(view, out Vector3 cursor))
        {
            return;
        }

        if (drawingLine)
        {
            UpdateLine(cursor, free);
            if (GameInput.BuildPlace.WasReleasedThisFrame())
            {
                ConfirmLine();
            }

            return;
        }

        ResolveSingle(cursor, free, out Vector3 point, out float pieceYaw);
        ghost.Show(1);
        string problem = CheckSpot(point, pieceYaw);
        if (problem == null && placing != null && !CanAfford(placing, 1, out string missing))
        {
            problem = missing;
        }

        ghost.Place(0, point, pieceYaw, problem, Kind);
        Overlay.Here(Kind, point, pieceYaw, LocalBounds);

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!GameInput.BuildPlace.WasPressedThisFrame() || overUI)
        {
            return;
        }

        if (placing != null && placing.PlaceInLines)
        {
            drawingLine = true;
            pressPoint = point;
            pressYaw = pieceYaw;
            lineStart = !free && TryFindLineEnd(cursor, null, out Vector3 end) ? end : free ? cursor : Grid(cursor);
            return;
        }

        if (problem != null)
        {
            GameMessages.Post(problem, MessageKind.Warning);
        }
        else
        {
            Confirm(point, Quaternion.Euler(0f, pieceYaw, 0f));
        }
    }

    private void UpdatePicking()
    {
        MarkPadSpot();
        if (!GameInput.BuildPlace.WasPressedThisFrame() || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            return;
        }

        if (!CameraPointer.TryGetRay(view, out Ray ray) || !Physics.Raycast(ray, out RaycastHit hit, 200f, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        BuildingObject building = hit.collider.GetComponentInParent<BuildingObject>();
        // A path has nothing to click on: the ground under the pointer picks the piece lying there.
        if (building == null && !upgrading)
        {
            foreach (PathPiece piece in PathPiece.All)
            {
                if (piece.Covers(hit.point, 0f))
                {
                    building = piece.GetComponent<BuildingObject>();
                    break;
                }
            }
        }

        if (upgrading)
        {
            if (building == null || building.Definition == null)
            {
                GameMessages.Post("Click one of your buildings.", MessageKind.Warning);
                return;
            }

            Cancel();
            ConstructionUpgradeOffer.Show(building, gameObject);
            return;
        }

        if (building == null || building.Definition == null || !building.Definition.ShowInBuildMenu)
        {
            GameMessages.Post("That can't be moved.", MessageKind.Warning);
            return;
        }

        picking = false;
        moving = building;
        yaw = building.transform.eulerAngles.y;
        // Checked against the building's own footprint (measured on its model before any dressing), not the scaffold, the sunk
        // rising model or the level dressing it wears now: those made a site unmovable and a dressed building's box too big.
        CreateGhost(building.transform, building.LocalBounds);
        SetCarried(building, true);
        building.gameObject.SetActive(false);
    }

    // Saved as standing at its old place while carried (SaveableEntity.Carried): a save or a raid launched mid-move kept it
    // switched off for good.
    private static void SetCarried(BuildingObject building, bool carried)
    {
        if (building != null && building.TryGetComponent(out SaveableEntity entity))
        {
            entity.Carried = carried;
        }
    }

    private void Confirm(Vector3 point, Quaternion rotation)
    {
        if (moving != null)
        {
            moving.transform.SetPositionAndRotation(point, rotation);
            moving.gameObject.SetActive(true);
            SetCarried(moving, false);
            GameMessages.Post($"Moved the {moving.Definition.DisplayName}.");
            moving = null;
            Cancel();
            return;
        }

        BuildingDefinition building = placing;
        GameObject placed = Build(building, point, rotation);
        if (placed == null)
        {
            return;
        }

        if (!StandsAtOnce(placed))
        {
            GameMessages.Post($"The scaffold for the {building.DisplayName} is up. Hold {{key:Hammer}} at it to build.");
        }
        else
        {
            GameMessages.Post($"Built the {building.DisplayName}.", MessageKind.Gain);
            GameEvents.Raise(new GameEvent(GameEventKind.Built, building, 1, gameObject));
        }

        Cancel();
    }

    // True when what was placed stands at once; false for a scaffold to hammer up (Construction), which is Built when finished.
    private static bool StandsAtOnce(GameObject placed) => !(placed.TryGetComponent(out Construction construction) && construction.IsBuilding);

    // Pays and places one building: a scaffold to hammer up when its kind takes work (Construction). Null when it can't
    // be paid for.
    private GameObject Build(BuildingDefinition building, Vector3 position, Quaternion rotation)
    {
        string refused = Area != null ? Area.CheckBuilding(building) : "You can only build in Safe Haven.";
        if (refused != null)
        {
            GameMessages.Post(refused, MessageKind.Warning);
            return null;
        }
        if (!MaterialPool.For(gameObject).TrySpend(building.ConstructionCost, building.ConstructionItems))
        {
            return null;
        }

        GameObject placed = building.Prefab.TryGetComponent(out SaveableEntity saveable)
            ? SaveableEntity.Spawn(saveable, position, rotation).gameObject
            : Instantiate(building.Prefab, position, rotation);
        if (building.BuildWork > 0f && placed.TryGetComponent(out Construction construction))
        {
            construction.BeginBuilding();
        }

        return placed;
    }

    // A controller in the top-down view has no pointer on the ground: the spot starts ahead of the player, away from the
    // camera, the right stick moves it (within reach), and walking carries it along. In the action view the crosshair aims.
    private void UpdatePadSpot()
    {
        bool use = InputDevices.UsingGamepad && !CameraPointer.UsesScreenCenter;
        UsesPadSpot = use;
        if (!use)
        {
            CameraPointer.PadTarget = null;
            padSpotPlaced = false;
            return;
        }

        ControllerSettings settings = ControllerSettings.Current;
        Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized;
        if (!padSpotPlaced)
        {
            padOffset = forward * settings.BuildStartDistance;
            padSpotPlaced = true;
        }

        Vector2 stick = GameInput.CameraLookStick != null ? GameInput.CameraLookStick.ReadValue<Vector2>() : Vector2.zero;
        padOffset += (right * stick.x + forward * stick.y) * (settings.BuildSpotSpeed * Time.unscaledDeltaTime);
        padOffset = Vector3.ClampMagnitude(padOffset, settings.BuildReach);
        CameraPointer.PadTarget = transform.position + padOffset;
    }

    private void MarkPadSpot()
    {
        RaycastHit hit = default;
        bool show = UsesPadSpot && CameraPointer.TryGetRay(view, out Ray ray)
                    && Physics.Raycast(ray, out hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (!show)
        {
            if (padMarker != null)
            {
                padMarker.SetActive(false);
            }

            return;
        }

        if (padMarker == null)
        {
            padMarker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            padMarker.name = "Pad Build Spot";
            Destroy(padMarker.GetComponent<Collider>());
            padMarker.transform.localScale = new Vector3(0.8f, 0.02f, 0.8f);
            var renderer = padMarker.GetComponent<Renderer>();
            renderer.sharedMaterial = ghostMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", validColor);
            block.SetColor("_Color", validColor);
            renderer.SetPropertyBlock(block);
        }

        padMarker.SetActive(true);
        padMarker.transform.position = hit.point + Vector3.up * 0.03f;
    }

    // The overlay shows while the build menu is open or something is being placed; a ghost's readout is laid in Update.
    private void LateUpdate()
    {
        BuildMenuWindow menu = BuildMenuWindow.Current;
        bool menuOpen = menu != null && menu.IsOpen;
        if (!IsBusy && !menuOpen)
        {
            if (overlay != null)
            {
                overlay.Show(false);
            }

            return;
        }

        if (placing == null && moving == null)
        {
            Overlay.NoGhost();
        }
    }

    private void OnDestroy()
    {
        if (padMarker != null)
        {
            Destroy(padMarker);
        }

        if (overlay != null)
        {
            Destroy(overlay.gameObject);
        }
    }

    private void Cancel()
    {
        if (padMarker != null)
        {
            padMarker.SetActive(false);
        }

        CameraPointer.PadTarget = null;
        UsesPadSpot = false;
        padSpotPlaced = false;
        if (moving != null)
        {
            moving.gameObject.SetActive(true);
            SetCarried(moving, false);
            moving = null;
        }

        ghost?.Clear();
        drawingLine = false;
        placing = null;
        picking = false;
        upgrading = false;
        if (IsBusy)
        {
            endedFrame = Time.frameCount;
        }

        IsBusy = false;
    }

    private static int endedFrame = -1;

    /// <summary>
    /// True in the frame building ended: the click that placed, picked or cancelled ended it, and must not also swing the weapon
    /// (PlayerInputHandler reads its pending clicks after this has run or before it, in no set order).
    /// </summary>
    public static bool EndedThisFrame => endedFrame == Time.frameCount;

    // The ghost of what is placed, and the yards already standing, while building.
    private void CreateGhost(Transform source, Bounds? bounds = null)
    {
        BuildingDefinition kind = Kind;
        ghost.Create(source, kind != null && kind.YardDepth > 0f, bounds);
        ghost.ShowPlacedYards(Area.GroundHeight);
        IsBusy = true;
    }
}
