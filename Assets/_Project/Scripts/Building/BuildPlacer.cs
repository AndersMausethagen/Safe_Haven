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
/// fit and can be paid for are built. Geometry lives in BuildPlacement.
/// </para>
/// <para>
/// Placed buildings are created with SaveableEntity.Spawn so the save system recreates them, and raise a Built game event
/// for quests. Where it belongs: on the PlayerArmature prefab. Tuning (grid, snapping, colors) is here; costs and
/// placement rules are on each BuildingDefinition; the ward radius in SettlementSettings.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildPlacer : MonoBehaviour
{
    private const string YardName = "Yard";
    // Yard markers float this far above the build plane so they don't flicker against the ground.
    private const float MarkerHeight = 0.04f;
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

    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly Collider[] Overlaps = new Collider[16];

    private readonly List<GhostPiece> pieces = new List<GhostPiece>();
    private readonly List<GameObject> placedYards = new List<GameObject>();
    private readonly List<Vector3> lineCenters = new List<Vector3>();
    private readonly List<ResourceAmount> scaledResources = new List<ResourceAmount>();
    private readonly List<ItemAmount> scaledItems = new List<ItemAmount>();

    private BuildingDefinition placing;
    private BuildingObject moving;
    private bool picking;
    private Bounds localBounds;
    private MaterialPropertyBlock block;
    private float yaw;
    private int shownPieces;
    private Camera view;

    // A line being dragged out. A short drag builds the single piece shown when the button went down.
    private bool drawingLine;
    private Vector3 lineStart;
    private Vector3 pressPoint;
    private float pressYaw;

    private sealed class GhostPiece
    {
        public GameObject Root;
        public Renderer[] Body;
        public Renderer Yard;
        public string Problem;
    }

    /// <summary>True while any player is placing, moving or picking a building: mouse buttons and wheel belong to building.</summary>
    public static bool IsBusy { get; private set; }

    private BuildingDefinition Kind => placing != null ? placing : moving != null ? moving.Definition : null;

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

    /// <summary>Opens or closes the build menu. Only works where there is a Heart Tree.</summary>
    public void ToggleMenu()
    {
        ActionMenuWindow menu = ActionMenuWindow.Current;
        if (IsBusy)
        {
            Cancel();
            return;
        }

        if (HeartTree.Current == null)
        {
            GameMessages.Post("You can only build in Safe Haven, inside the Heart Tree's ward.", MessageKind.Warning);
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
            Body = $"Build inside the Heart Tree's ward ({HeartTree.Current.WardRadius:0} m). Left-click to place, mouse wheel to turn "
                + "(Shift for small steps), hold Alt to place freely without snapping. Walls are drawn by dragging. Right-click or Esc cancels.",
        };

        var crafting = GetComponent<PlayerCrafting>();
        if (crafting == null)
        {
            return page;
        }

        var buildings = new List<BuildingDefinition>();
        foreach (BuildingDefinition building in crafting.Buildings)
        {
            if (building != null && building.ShowInBuildMenu && building.Prefab != null && crafting.IsKnown(building))
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

        page.Add(new MenuAction("Move a building", "Pick up something you built and place it again, for free", true, BeginPicking, closesMenu: true));
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
        ShowPlacedYards();

        var hint = new StringBuilder($"Place the {building.DisplayName}: left-click to build");
        hint.Append(building.PlaceInLines ? ", or drag to build a line." : ".");
        if (building.YardDepth > 0f)
        {
            hint.Append(" Keep the marked ground in front of it clear.");
        }

        GameMessages.Post(hint.ToString());
    }

    private void BeginPicking()
    {
        Cancel();
        picking = true;
        IsBusy = true;
        GameMessages.Post("Click a building to move it. Right-click to cancel.");
    }

    private void Update()
    {
        if (!IsBusy)
        {
            return;
        }

        // Escape cancels through BackHandlers (CancelByBack), so the Escape menu doesn't also open.
        if (Mouse.current == null || HeartTree.Current == null)
        {
            Cancel();
            return;
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
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

        Keyboard keyboard = Keyboard.current;
        bool free = keyboard != null && keyboard.altKey.isPressed;
        bool fine = keyboard != null && keyboard.shiftKey.isPressed;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (!drawingLine && Mathf.Abs(scroll) > 0.01f)
        {
            yaw = Mathf.Repeat(yaw + Mathf.Sign(scroll) * (fine ? fineRotationStep : rotationStep), 360f);
        }

        if (!TryGetGroundPoint(out Vector3 cursor))
        {
            return;
        }

        if (drawingLine)
        {
            UpdateLine(cursor, free);
            if (Mouse.current.leftButton.wasReleasedThisFrame)
            {
                ConfirmLine();
            }

            return;
        }

        ResolveSingle(cursor, free, out Vector3 point, out float pieceYaw);
        ShowPieces(1);
        string problem = CheckSpot(point, pieceYaw);
        if (problem == null && placing != null && !CanAfford(placing, 1, out string missing))
        {
            problem = missing;
        }

        Place(pieces[0], point, pieceYaw, problem);

        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (!Mouse.current.leftButton.wasPressedThisFrame || overUI)
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
        if (!Mouse.current.leftButton.wasPressedThisFrame || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()))
        {
            return;
        }

        if (!CameraPointer.TryGetRay(view, out Ray ray) || !Physics.Raycast(ray, out RaycastHit hit, 200f, blockingLayers, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        BuildingObject building = hit.collider.GetComponentInParent<BuildingObject>();
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
        ShowPlacedYards();
    }

    private bool TryGetGroundPoint(out Vector3 point)
    {
        var ground = new Plane(Vector3.up, HeartTree.Current.transform.position);
        return CameraPointer.TryGetPointOnPlane(view, ground, out point);
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
            point = snapped;
            pieceYaw = snappedYaw;
            return;
        }

        point = Grid(cursor);
    }

    private bool TrySnapBesideNeighbour(Vector3 cursor, out Vector3 point, out float pieceYaw)
    {
        point = cursor;
        pieceYaw = yaw;
        float bestGap = float.MaxValue;
        float ownReach = Mathf.Max(localBounds.extents.x, localBounds.extents.z);
        foreach (BuildingObject other in BuildingObject.Active)
        {
            FootprintRect footprint = other.Footprint;
            float reach = Mathf.Max(footprint.HalfSize.x, footprint.HalfSize.y) + ownReach + snapDistance;
            if ((new Vector2(cursor.x, cursor.z) - footprint.Center).sqrMagnitude > reach * reach)
            {
                continue;
            }

            if (BuildPlacement.TrySnapBeside(cursor, yaw, localBounds, footprint, snapDistance, out Vector3 snapped, out float snappedYaw, out float gap)
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
        float length = BuildPlacement.PieceLength(localBounds);
        if (delta.magnitude < length * 0.5f)
        {
            ShowPieces(1);
            string problem = CheckSpot(pressPoint, pressYaw);
            if (problem == null && !CanAfford(placing, 1, out string missing))
            {
                problem = missing;
            }

            Place(pieces[0], pressPoint, pressYaw, problem);
            return;
        }

        if (!joined && !free)
        {
            end = lineStart + BuildPlacement.SnapDirection(delta, lineAngleStep) * delta.magnitude;
        }

        Vector3 direction = BuildPlacement.LayLine(lineStart, end, length, maxLinePieces, lineCenters);
        float pieceYaw = BuildPlacement.LineYaw(direction, BuildPlacement.RunsAlongX(localBounds));
        ShowPieces(lineCenters.Count);
        int paid = 0;
        for (int i = 0; i < lineCenters.Count; i++)
        {
            Vector3 position = BuildPlacement.PivotFor(lineCenters[i], pieceYaw, localBounds);
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

            Place(pieces[i], position, pieceYaw, problem);
        }
    }

    /// <summary>Why a building can't stand here, in words for the player, or null when it can. Doesn't check the cost.</summary>
    private string CheckSpot(Vector3 point, float pieceYaw)
    {
        FootprintRect footprint = BuildPlacement.Footprint(point, pieceYaw, localBounds);
        float reach = Mathf.Max(footprint.HalfSize.x, footprint.HalfSize.y);
        if (!HeartTree.Current.Contains(footprint.WorldCenter(point.y), reach))
        {
            return "Outside the Heart Tree's ward. Build closer to the tree.";
        }

        Quaternion rotation = Quaternion.Euler(0f, pieceYaw, 0f);
        Vector3 extents = new Vector3(localBounds.extents.x * 0.92f, Mathf.Max(0.05f, localBounds.extents.y - GroundLift * 0.5f), localBounds.extents.z * 0.92f);
        Vector3 center = point + rotation * (localBounds.center + Vector3.up * GroundLift * 0.5f);
        int count = Physics.OverlapBoxNonAlloc(center, extents, Overlaps, rotation, blockingLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!IsGhost(Overlaps[i]))
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
        if (kind != null && BuildPlacement.TryGetYard(point, pieceYaw, localBounds, kind.YardSide, kind.YardDepth, out FootprintRect yard))
        {
            float height = Mathf.Max(0.5f, localBounds.size.y);
            Vector3 yardCenter = yard.WorldCenter(point.y + GroundLift + height * 0.5f);
            Vector3 yardExtents = new Vector3(yard.HalfSize.x * 0.92f, height * 0.5f, yard.HalfSize.y * 0.92f);
            count = Physics.OverlapBoxNonAlloc(yardCenter, yardExtents, Overlaps, rotation, blockingLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!IsGhost(Overlaps[i]) && !IsMoving(Overlaps[i]))
                {
                    return $"Its entrance is blocked. Keep {kind.YardDepth:0.#} m clear in front of it.";
                }
            }
        }

        return null;
    }

    private bool IsGhost(Collider collider)
    {
        foreach (GhostPiece piece in pieces)
        {
            if (collider.transform.IsChildOf(piece.Root.transform))
            {
                return true;
            }
        }

        return false;
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
        if (!Build(building, point, rotation))
        {
            return;
        }

        GameMessages.Post($"Built the {building.DisplayName}.", MessageKind.Gain);
        GameEvents.Raise(new GameEvent(GameEventKind.Built, building, 1, gameObject));
        Cancel();
    }

    private void ConfirmLine()
    {
        drawingLine = false;
        BuildingDefinition building = placing;
        int built = 0;
        int blocked = 0;
        string firstProblem = null;
        for (int i = 0; i < shownPieces; i++)
        {
            GhostPiece piece = pieces[i];
            if (piece.Problem != null)
            {
                blocked++;
                firstProblem ??= piece.Problem;
                continue;
            }

            if (!Build(building, piece.Root.transform.position, piece.Root.transform.rotation))
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

    // Pays and places one building. False when it can't be paid for.
    private bool Build(BuildingDefinition building, Vector3 position, Quaternion rotation)
    {
        if (!MaterialPool.For(gameObject).TrySpend(building.ConstructionCost, building.ConstructionItems))
        {
            return false;
        }

        if (building.Prefab.TryGetComponent(out SaveableEntity saveable))
        {
            SaveableEntity.Spawn(saveable, position, rotation);
        }
        else
        {
            Instantiate(building.Prefab, position, rotation);
        }

        return true;
    }

    private void Cancel()
    {
        if (moving != null)
        {
            moving.gameObject.SetActive(true);
            moving = null;
        }

        foreach (GhostPiece piece in pieces)
        {
            if (piece.Root != null)
            {
                Destroy(piece.Root);
            }
        }

        foreach (GameObject marker in placedYards)
        {
            if (marker != null)
            {
                Destroy(marker);
            }
        }

        pieces.Clear();
        placedYards.Clear();
        shownPieces = 0;
        drawingLine = false;
        placing = null;
        picking = false;
        IsBusy = false;
    }

    // The ghost copies only the meshes, so nothing on the real building (saving, crafting, health) wakes up. More
    // pieces for a line are copies of this first one.
    private void CreateGhost(Transform source)
    {
        var ghost = new GameObject("Build Ghost");
        localBounds = BuildPlacement.LocalBounds(source);
        Matrix4x4 toRoot = source.worldToLocalMatrix;
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.TryGetComponent(out MeshRenderer original) || !original.enabled)
            {
                continue;
            }

            Matrix4x4 relative = toRoot * filter.transform.localToWorldMatrix;
            var part = new GameObject(filter.name);
            part.transform.SetParent(ghost.transform, false);
            part.transform.localPosition = relative.GetColumn(3);
            part.transform.localRotation = relative.rotation;
            part.transform.localScale = relative.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = part.AddComponent<MeshRenderer>();
            var materials = new Material[filter.sharedMesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = ghostMaterial;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        BuildingDefinition kind = Kind;
        if (kind != null && kind.YardDepth > 0f)
        {
            CreateMarker(YardName).transform.SetParent(ghost.transform, false);
        }

        block ??= new MaterialPropertyBlock();
        pieces.Add(Describe(ghost));
        shownPieces = 1;
        IsBusy = true;
    }

    private static GhostPiece Describe(GameObject root)
    {
        var piece = new GhostPiece { Root = root };
        var body = new List<Renderer>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name == YardName)
            {
                piece.Yard = renderer;
            }
            else
            {
                body.Add(renderer);
            }
        }

        piece.Body = body.ToArray();
        return piece;
    }

    private void ShowPieces(int count)
    {
        while (pieces.Count < count)
        {
            pieces.Add(Describe(Instantiate(pieces[0].Root)));
        }

        for (int i = 0; i < pieces.Count; i++)
        {
            pieces[i].Root.SetActive(i < count);
        }

        shownPieces = count;
    }

    private void Place(GhostPiece piece, Vector3 point, float pieceYaw, string problem)
    {
        piece.Problem = problem;
        piece.Root.transform.SetPositionAndRotation(point, Quaternion.Euler(0f, pieceYaw, 0f));
        Tint(piece.Body, problem == null ? validColor : invalidColor);
        BuildingDefinition kind = Kind;
        if (piece.Yard != null && kind != null
            && BuildPlacement.TryGetYard(point, pieceYaw, localBounds, kind.YardSide, kind.YardDepth, out FootprintRect yard))
        {
            Lay(piece.Yard.transform, yard, point.y + MarkerHeight);
            Tint(piece.Yard, problem == null ? yardColor : invalidColor * new Color(1f, 1f, 1f, 0.6f));
        }
    }

    // Marks the yards of the buildings already standing, so the player sees where not to build.
    private void ShowPlacedYards()
    {
        float height = HeartTree.Current != null ? HeartTree.Current.transform.position.y + MarkerHeight : MarkerHeight;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (!building.TryGetYard(out FootprintRect yard))
            {
                continue;
            }

            GameObject marker = CreateMarker("Placed Yard");
            Lay(marker.transform, yard, height);
            Tint(marker.GetComponent<Renderer>(), placedYardColor);
            placedYards.Add(marker);
        }
    }

    private GameObject CreateMarker(string markerName)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Quad);
        DestroyImmediate(marker.GetComponent<Collider>());
        marker.name = markerName;
        var renderer = marker.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = ghostMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return marker;
    }

    // A quad lies flat (its face up) when tilted a quarter turn about x; its height then runs along the ground's z.
    private static void Lay(Transform marker, FootprintRect area, float height)
    {
        marker.SetPositionAndRotation(area.WorldCenter(height), Quaternion.Euler(90f, area.Yaw, 0f));
        Vector3 parentScale = marker.parent != null ? marker.parent.lossyScale : Vector3.one;
        marker.localScale = new Vector3(area.HalfSize.x * 2f / parentScale.x, area.HalfSize.y * 2f / parentScale.z, 1f);
    }

    private void Tint(Renderer[] renderers, Color color)
    {
        foreach (Renderer renderer in renderers)
        {
            Tint(renderer, color);
        }
    }

    private void Tint(Renderer renderer, Color color)
    {
        block ??= new MaterialPropertyBlock();
        block.SetColor(BaseColor, color);
        block.SetColor(ColorId, color);
        renderer.SetPropertyBlock(block);
    }
}
