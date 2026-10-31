using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// The player's building (GDD §14.1): G opens the build menu with every known structure and its cost; choosing one shows
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
/// Where and what may be built is the current build area's (<see cref="IBuildArea"/>: the Heart Tree's ward at home,
/// the expedition's camp in the prologue), so this class knows neither.
/// </para>
/// <para>
/// Placed buildings are created with SaveableEntity.Spawn so the save system recreates them, and raise a Built game event
/// for quests. Where it belongs: on the PlayerArmature prefab. Tuning (grid, snapping, colors) is here; costs and
/// placement rules are on each BuildingDefinition; the area's own rules on the area.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildPlacer : MonoBehaviour
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
    private static bool HasBuildArea => Area != null && Area.WhyClosed == null;
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

    /// <summary>Opens or closes the build menu. Only works in an open build area.</summary>
    public void ToggleMenu()
    {
        ActionMenuWindow menu = ActionMenuWindow.Current;
        if (IsBusy)
        {
            Cancel();
            return;
        }

        if (!HasBuildArea)
        {
            GameMessages.Post(Area != null ? Area.WhyClosed : "You can only build in Safe Haven, inside the Heart Tree's ward.", MessageKind.Warning);
            return;
        }

        if (menu == null)
        {
            return;
        }

        if (menu.IsOpen)
        {
            menu.Close();
            return;
        }

        menu.Open(BuildPage, gameObject);
    }

    private MenuPage BuildPage()
    {
        var page = new MenuPage
        {
            Title = "Build",
            Body = Area.Guidance + "Left-click to place, mouse wheel to turn "
                + "(Shift for small steps), hold Alt to place freely without snapping. Walls are drawn by dragging. Right-click or Esc cancels.",
        };

        var crafting = GetComponent<PlayerCrafting>();
        if (crafting == null)
        {
            return page;
        }

        var buildings = new List<BuildingDefinition>();
        foreach (BuildingDefinition building in Area.Buildable(crafting))
        {
            if (building != null && building.Prefab != null)
            {
                buildings.Add(building);
            }
        }

        buildings.Sort(RecipeBook.CompareForDisplay);
        MaterialPool pool = MaterialPool.For(gameObject);
        foreach (BuildingDefinition building in buildings)
        {
            bool affordable = pool.CanAfford(building.ConstructionCost, building.ConstructionItems, out string missing);
            string cost = DescribeCost(building) + (building.PlaceInLines ? " each" : "");
            page.Add(new MenuAction(building.DisplayName, affordable ? cost : $"{cost}   <color=#FF7B6B>{missing}</color>", affordable,
                () => BeginPlacing(building), closesMenu: true));
        }

        if (Area.AllowsChanges)
        {
            page.Add(new MenuAction("Upgrade or repair a building", "Make something you built better, or bring a ruin back", true,
                () => BeginPicking(true), closesMenu: true));
            page.Add(new MenuAction("Move a building", "Pick up something you built and place it again, for free", true, () => BeginPicking(false), closesMenu: true));
        }
        return page;
    }

    private static string DescribeCost(BuildingDefinition building)
    {
        var builder = new StringBuilder(AmountText.Describe(building.ConstructionCost));
        if (building.ConstructionItems.Count > 0)
        {
            builder.Append(builder.Length > 0 ? ", " : "").Append(AmountText.Describe(building.ConstructionItems));
        }

        return builder.Length > 0 ? builder.ToString() : "free";
    }

    private void BeginPlacing(BuildingDefinition building)
    {
        Cancel();
        placing = building;
        yaw = 0f;
        CreateGhost(building.Prefab.transform);

        var hint = new StringBuilder($"Place the {building.DisplayName}: left-click to build");
        hint.Append(building.PlaceInLines ? ", or drag to build a line." : ".");
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

    // The next level of a building: what it costs and brings, and a button to order it (the scaffold goes up).
    private void ShowUpgrade(BuildingObject building)
    {
        if (!building.TryGetComponent(out Construction construction))
        {
            GameMessages.Post($"The {building.Definition.DisplayName} can't be upgraded.", MessageKind.Warning);
            return;
        }

        if (construction.IsBuilding)
        {
            GameMessages.Post(construction.Paid ? $"The {building.Definition.DisplayName} is waiting for you: hold E at its scaffold."
                : $"Walk up to the {building.Definition.DisplayName} and hold E to repair it.");
            return;
        }

        BuildingLevel next = building.Definition.UpgradeTo(construction.Level + 1);
        if (next == null)
        {
            GameMessages.Post($"The {construction.Title} is as good as it gets.");
            return;
        }

        ActionMenuWindow menu = ActionMenuWindow.Current;
        if (menu == null)
        {
            return;
        }

        menu.Open(() => UpgradePage(building, construction, next), gameObject);
    }

    private MenuPage UpgradePage(BuildingObject building, Construction construction, BuildingLevel next)
    {
        var bonuses = new List<string>();
        if (next.craftSpeed > 0f) bonuses.Add($"works {next.craftSpeed * 100f:0}% faster");
        if (next.extraQueue > 0) bonuses.Add($"+{next.extraQueue} queue");
        if (next.extraOutput > 0) bonuses.Add($"+{next.extraOutput} output");
        if (next.extraStorage > 0) bonuses.Add($"+{next.extraStorage} storage slots");
        bonuses.Add($"+{next.renown} renown");
        var page = new MenuPage
        {
            Title = $"{next.title} {building.Definition.DisplayName}",
            Body = $"Level {construction.Level + 1} of {building.Definition.MaxLevel}. {next.description}\n<color=#8BE08B>{string.Join(", ", bonuses)}</color>",
        };
        bool affordable = MaterialPool.For(gameObject).CanAfford(next.essence, next.cost, out string missing);
        string cost = next.CostText;
        page.Add(new MenuAction("Upgrade", affordable ? cost : $"{cost}   <color=#FF7B6B>{missing}</color>", affordable,
            () => construction.TryOrderUpgrade(gameObject), closesMenu: true));
        return page;
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
            ShowUpgrade(building);
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

    private Vector3 Grid(Vector3 point)
    {
        if (gridSize > 0f)
        {
            point.x = Mathf.Round(point.x / gridSize) * gridSize;
            point.z = Mathf.Round(point.z / gridSize) * gridSize;
        }

        return point;
    }

    // Free placement follows the cursor; otherwise snap flush beside the nearest building, or to the grid.
    private void ResolveSingle(Vector3 cursor, bool free, out Vector3 point, out float pieceYaw)
    {
        point = cursor;
        pieceYaw = yaw;
        if (free)
        {
            return;
        }

        if (snapDistance > 0f && TrySnapBesideNeighbour(cursor, out Vector3 snapped, out float snappedYaw))
        {
            point = Area.OnGround(snapped);
            pieceYaw = snappedYaw;
            return;
        }

        point = Area.OnGround(Grid(cursor));
    }

    private bool TrySnapBesideNeighbour(Vector3 cursor, out Vector3 point, out float pieceYaw)
    {
        point = cursor;
        pieceYaw = yaw;
        float bestGap = float.MaxValue;
        float ownReach = Mathf.Max(LocalBounds.extents.x, LocalBounds.extents.z);
        foreach (BuildingObject other in BuildingObject.Active)
        {
            FootprintRect footprint = other.Footprint;
            float reach = Mathf.Max(footprint.HalfSize.x, footprint.HalfSize.y) + ownReach + snapDistance;
            if ((new Vector2(cursor.x, cursor.z) - footprint.Center).sqrMagnitude > reach * reach)
            {
                continue;
            }

            if (BuildPlacement.TrySnapBeside(cursor, yaw, LocalBounds, footprint, snapDistance, out Vector3 snapped, out float snappedYaw, out float gap)
                && gap < bestGap)
            {
                bestGap = gap;
                point = snapped;
                pieceYaw = snappedYaw;
            }
        }

        return bestGap < float.MaxValue;
    }

    // The nearest free end of a placed piece of the same kind (a palisade end to join), other than except.
    private bool TryFindLineEnd(Vector3 cursor, Vector3? except, out Vector3 end)
    {
        end = cursor;
        float radius = Mathf.Max(snapDistance, 1f);
        float best = radius * radius;
        bool found = false;
        foreach (BuildingObject other in BuildingObject.Active)
        {
            if (other.Definition != placing)
            {
                continue;
            }

            BuildPlacement.LineEnds(other.transform.position, other.transform.eulerAngles.y, other.LocalBounds, out Vector3 a, out Vector3 b);
            foreach (Vector3 candidate in new[] { a, b })
            {
                Vector3 flat = new Vector3(candidate.x, cursor.y, candidate.z);
                if (except.HasValue && (flat - except.Value).sqrMagnitude < 0.01f)
                {
                    continue;
                }

                float distance = (flat - cursor).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    end = flat;
                    found = true;
                }
            }
        }

        return found;
    }

    private void UpdateLine(Vector3 cursor, bool free)
    {
        Vector3 end = cursor;
        bool joined = !free && TryFindLineEnd(cursor, lineStart, out end);
        if (!joined)
        {
            end = cursor;
        }

        Vector3 delta = end - lineStart;
        delta.y = 0f;
        float length = BuildPlacement.PieceLength(LocalBounds);
        if (delta.magnitude < length * 0.5f)
        {
            ghost.Show(1);
            string problem = CheckSpot(pressPoint, pressYaw);
            if (problem == null && !CanAfford(placing, 1, out string missing))
            {
                problem = missing;
            }

            ghost.Place(0, pressPoint, pressYaw, problem, Kind);
            return;
        }

        if (!joined && !free)
        {
            end = lineStart + BuildPlacement.SnapDirection(delta, lineAngleStep) * delta.magnitude;
        }

        Vector3 direction = BuildPlacement.LayLine(lineStart, end, length, maxLinePieces, lineCenters);
        float pieceYaw = BuildPlacement.LineYaw(direction, BuildPlacement.RunsAlongX(LocalBounds));
        ghost.Show(lineCenters.Count);
        int paid = 0;
        for (int i = 0; i < lineCenters.Count; i++)
        {
            Vector3 position = BuildPlacement.PivotFor(lineCenters[i], pieceYaw, LocalBounds);
            string problem = CheckSpot(position, pieceYaw);
            if (problem == null)
            {
                if (CanAfford(placing, paid + 1, out string missing))
                {
                    paid++;
                }
                else
                {
                    problem = missing;
                }
            }

            ghost.Place(i, position, pieceYaw, problem, Kind);
        }
    }

    /// <summary>Why a building can't stand here, in words for the player, or null when it can. Doesn't check the cost.</summary>
    private string CheckSpot(Vector3 point, float pieceYaw)
    {
        FootprintRect footprint = BuildPlacement.Footprint(point, pieceYaw, LocalBounds);
        string outside = Area.CheckSpot(footprint, point);
        if (outside != null)
        {
            return outside;
        }

        Quaternion rotation = Quaternion.Euler(0f, pieceYaw, 0f);
        Vector3 extents = new Vector3(LocalBounds.extents.x * 0.92f, Mathf.Max(0.05f, LocalBounds.extents.y - GroundLift * 0.5f), LocalBounds.extents.z * 0.92f);
        Vector3 center = point + rotation * (LocalBounds.center + Vector3.up * GroundLift * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center, extents, Overlaps, rotation, blockingLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!ghost.Contains(Overlaps[i]))
            {
                return "Something is in the way.";
            }
        }

        foreach (BuildingObject other in BuildingObject.Active)
        {
            if (other.TryGetYard(out FootprintRect otherYard) && BuildPlacement.Overlaps(footprint, otherYard))
            {
                return $"That's in front of the {other.Definition.DisplayName}. Keep its entrance clear.";
            }
        }

        BuildingDefinition kind = Kind;
        if (kind != null && BuildPlacement.TryGetYard(point, pieceYaw, LocalBounds, kind.YardSide, kind.YardDepth, out FootprintRect yard))
        {
            float height = Mathf.Max(0.5f, LocalBounds.size.y);
            Vector3 yardCenter = yard.WorldCenter(point.y + GroundLift + height * 0.5f);
            Vector3 yardExtents = new Vector3(yard.HalfSize.x * 0.92f, height * 0.5f, yard.HalfSize.y * 0.92f);
            count = Physics.OverlapBoxNonAlloc(yardCenter, yardExtents, Overlaps, rotation, blockingLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!ghost.Contains(Overlaps[i]) && !IsMoving(Overlaps[i]))
                {
                    return $"Its entrance is blocked. Keep {kind.YardDepth:0.#} m clear in front of it.";
                }
            }
        }

        return null;
    }

    // People and monsters walk out of the way, so they don't block a yard.
    private static bool IsMoving(Collider collider)
    {
        return collider is CharacterController || collider.attachedRigidbody != null || collider.GetComponentInParent<NavMeshAgent>() != null;
    }

    private bool CanAfford(BuildingDefinition building, int copies, out string missing)
    {
        MaterialPool pool = MaterialPool.For(gameObject);
        if (copies <= 1)
        {
            return pool.CanAfford(building.ConstructionCost, building.ConstructionItems, out missing);
        }

        scaledResources.Clear();
        foreach (ResourceAmount cost in building.ConstructionCost)
        {
            scaledResources.Add(new ResourceAmount(cost.Resource, cost.Amount * copies));
        }

        scaledItems.Clear();
        foreach (ItemAmount cost in building.ConstructionItems)
        {
            scaledItems.Add(new ItemAmount(cost.Item, cost.Amount * copies));
        }

        return pool.CanAfford(scaledResources, scaledItems, out missing);
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

    private void ConfirmLine()
    {
        drawingLine = false;
        BuildingDefinition building = placing;
        int built = 0;
        int blocked = 0;
        string firstProblem = null;
        for (int i = 0; i < ghost.Count; i++)
        {
            string problem = ghost.ProblemAt(i);
            if (problem != null)
            {
                blocked++;
                firstProblem ??= problem;
                continue;
            }

            Transform piece = ghost.PieceAt(i);
            if (Build(building, piece.position, piece.rotation) == null)
            {
                break;
            }

            built++;
        }

        if (built == 0)
        {
            GameMessages.Post(firstProblem ?? "Nothing could be built there.", MessageKind.Warning);
            return;
        }

        string name = built == 1 ? $"the {building.DisplayName}" : $"{built} × {building.DisplayName}";
        GameMessages.Post(blocked > 0 ? $"Built {name}. {blocked} spot(s) were blocked or unaffordable." : $"Built {name}.", MessageKind.Gain);
        GameEvents.Raise(new GameEvent(GameEventKind.Built, building, built, gameObject));
        Cancel();
    }

    // Pays and places one building: a scaffold to hammer up when its kind takes work (Construction). Null when it can't
    // be paid for.
    private GameObject Build(BuildingDefinition building, Vector3 position, Quaternion rotation)
    {
        string refused = Area != null ? Area.CheckBuilding(building) : "You can only build in Safe Haven, inside the Heart Tree's ward.";
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
