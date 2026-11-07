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
public sealed partial class BuildPlacer : MonoBehaviour
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
    }

    private void OnDisable()
    {
        BackHandlers.Unregister(CancelByBack);
        Cancel();
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
            GameMessages.Post(Area != null ? Area.WhyClosed : "You can only build in Safe Haven, inside the portal's ward.", MessageKind.Warning);
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

        if (building != null && building.Prefab != null && HasBuildArea)
        {
            BeginPlacing(building);
        }
    }

    /// <summary>Lets the player click a building they built, to upgrade or repair it (true) or to move it (false).</summary>
    public void PickBuilding(bool toUpgrade)
    {
        if (!HavenVisit.BuildingNeedsAsking() && AllowsChanges)
        {
            BeginPicking(toUpgrade);
        }
    }

    private void BeginPlacing(BuildingDefinition building)
    {
        Cancel();
        placing = building;
        yaw = 0f;
        CreateGhost(building.Prefab.transform);

        var hint = new StringBuilder($"Place the {building.DisplayName}: left-click to build");
        hint.Append(building.PlaceInLines ? ", or drag to build a line (scroll while dragging to bend it round the portal)." : ".");
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

        if (picking)
        {
            UpdatePicking();
            return;
        }

        bool free = GameInput.BuildFreePlace.IsPressed();
        bool fine = GameInput.BuildFineRotate.IsPressed();
        float scroll = GameInput.BuildRotate.ReadValue<float>();
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
        if (!GameInput.BuildPlace.WasPressedThisFrame() || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            return;
        }

        if (!CameraPointer.TryGetRay(view, out Ray ray) || !Physics.Raycast(ray, out RaycastHit hit, 200f, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        BuildingObject building = hit.collider.GetComponentInParent<BuildingObject>();
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
        CreateGhost(building.transform);
        building.gameObject.SetActive(false);
    }

    private void Confirm(Vector3 point, Quaternion rotation)
    {
        if (moving != null)
        {
            moving.transform.SetPositionAndRotation(point, rotation);
            moving.gameObject.SetActive(true);
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

        if (placed.TryGetComponent(out Construction construction) && construction.IsBuilding)
        {
            GameMessages.Post($"The scaffold for the {building.DisplayName} is up. Hold E at it to build.");
        }
        else
        {
            GameMessages.Post($"Built the {building.DisplayName}.", MessageKind.Gain);
            GameEvents.Raise(new GameEvent(GameEventKind.Built, building, 1, gameObject));
        }

        Cancel();
    }

    // Pays and places one building: a scaffold to hammer up when its kind takes work (Construction). Null when it can't
    // be paid for.
    private GameObject Build(BuildingDefinition building, Vector3 position, Quaternion rotation)
    {
        string refused = Area != null ? Area.CheckBuilding(building) : "You can only build in Safe Haven, inside the portal's ward.";
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

    private void Cancel()
    {
        if (moving != null)
        {
            moving.gameObject.SetActive(true);
            moving = null;
        }

        ghost?.Clear();
        drawingLine = false;
        placing = null;
        picking = false;
        upgrading = false;
        IsBusy = false;
    }

    // The ghost of what is placed, and the yards already standing, while building.
    private void CreateGhost(Transform source)
    {
        BuildingDefinition kind = Kind;
        ghost.Create(source, kind != null && kind.YardDepth > 0f);
        ghost.ShowPlacedYards(Area.GroundHeight);
        IsBusy = true;
    }
}
