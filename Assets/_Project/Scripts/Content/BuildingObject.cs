using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a placed building and says which BuildingDefinition it was made from. Every enabled building is listed in
/// Active, so systems (quests, residents, the ward breach) don't have to search the scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildingObject : MonoBehaviour, IInteractionName, IEventSubject
{
    // Nothing destroys what the player built (GDD §6.9): worn down to this it is broken, until it is mended.
    private const float BrokenHealth = 1f;

    private static readonly List<BuildingObject> active = new List<BuildingObject>();

    [SerializeField] private BuildingDefinition definition;

    private Bounds? localBounds;
    private SaveableEntity saveable;
    private Construction construction;

    /// <summary>
    /// Raised when a building appears, goes, or is carried off and set down again (moving switches it off and on), so what
    /// depends on where buildings stand (homes, yards, the Here readout) works itself out again then, never every frame.
    /// </summary>
    public static event System.Action ActiveChanged;

    public BuildingDefinition Definition => definition;

    /// <summary>Its save id: the same on every machine in co-op and after loading, so homes and workplaces name it. Null when it has none.</summary>
    public string Id => saveable != null && !string.IsNullOrEmpty(saveable.Id) ? saveable.Id : null;

    /// <summary>It stands (built, or being upgraded): not a scaffold going up for the first time, nor a ruin.</summary>
    public bool IsStanding => construction == null || construction.IsStanding;

    /// <summary>Beds in it now: its own and its upgrades', once it stands. A scaffold has none yet.</summary>
    public int Beds => definition == null || !IsStanding ? 0
        : definition.Beds + (construction != null ? Mathf.RoundToInt(construction.Sum(level => level.beds)) : 0);

    /// <summary>
    /// Where its door is: the middle of its entrance side (the yard's side), just outside the wall, and the way out of it.
    /// People walk in and out here; nothing about the door is modelled, so it works for every building.
    /// </summary>
    public Vector3 Door(out Quaternion outward)
    {
        const float clearance = 0.55f;
        Bounds local = LocalBounds;
        YardSide side = definition != null ? definition.YardSide : YardSide.Front;
        Vector3 edge = side switch
        {
            YardSide.Back => new Vector3(local.center.x, 0f, local.min.z - clearance),
            YardSide.Right => new Vector3(local.max.x + clearance, 0f, local.center.z),
            YardSide.Left => new Vector3(local.min.x - clearance, 0f, local.center.z),
            _ => new Vector3(local.center.x, 0f, local.max.z + clearance),
        };
        Vector3 facing = side switch { YardSide.Back => Vector3.back, YardSide.Right => Vector3.right, YardSide.Left => Vector3.left, _ => Vector3.forward };
        Quaternion turn = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        outward = Quaternion.LookRotation(turn * facing);
        Vector3 door = transform.position + turn * edge;
        door.y = transform.position.y;
        return door;
    }
    Object IEventSubject.EventSubject => definition;
    public string InteractionName => definition != null ? definition.DisplayName : null;

    /// <summary>Bounds of the building's meshes in its own space. Measured once; buildings don't change shape.</summary>
    public Bounds LocalBounds => localBounds ??= BuildPlacement.LocalBounds(transform);

    /// <summary>The ground the building stands on, where it is now.</summary>
    public FootprintRect Footprint => BuildPlacement.Footprint(transform.position, transform.eulerAngles.y, LocalBounds);

    /// <summary>The ground in front of its entrance that must stay clear, when its definition gives it a yard.</summary>
    public bool TryGetYard(out FootprintRect yard)
    {
        if (definition == null)
        {
            yard = default;
            return false;
        }

        return BuildPlacement.TryGetYard(transform.position, transform.eulerAngles.y, LocalBounds, definition.YardSide, definition.YardDepth, out yard);
    }

    public static IReadOnlyList<BuildingObject> Active => active;

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        active.Clear();
    }

    /// <summary>How many active buildings are of the given kind, or of any kind when building is null.</summary>
    public static int Count(BuildingDefinition building)
    {
        int count = 0;
        foreach (BuildingObject built in active)
        {
            if (built.definition != null && (building == null || built.definition == building))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The active building with this save id, or null.</summary>
    public static BuildingObject WithId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (BuildingObject built in active)
        {
            if (built.Id == id)
            {
                return built;
            }
        }

        return null;
    }

    /// <summary>The standing building of the given kind nearest <paramref name="to"/> (on the ground), or null.</summary>
    public static BuildingObject Nearest(BuildingDefinition building, Vector3 to)
    {
        BuildingObject best = null;
        float bestDistance = float.MaxValue;
        foreach (BuildingObject built in active)
        {
            if (built.definition != building || building == null || !built.IsStanding)
            {
                continue;
            }

            Vector3 offset = built.transform.position - to;
            offset.y = 0f;
            float distance = offset.sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = built;
            }
        }

        return best;
    }

    /// <summary>The first active building of the given kind, or null.</summary>
    public static BuildingObject Find(BuildingDefinition building)
    {
        foreach (BuildingObject built in active)
        {
            if (built.definition == building)
            {
                return built;
            }
        }

        return null;
    }

    private void Awake()
    {
        saveable = GetComponent<SaveableEntity>();
        construction = GetComponent<Construction>();
    }

    private void OnEnable()
    {
        active.Add(this);
        if (TryGetComponent(out Damageable health))
        {
            health.HealthFloor = BrokenHealth;
        }

        ActiveChanged?.Invoke();
    }

    private void OnDisable()
    {
        active.Remove(this);
        ActiveChanged?.Invoke();
    }
}
