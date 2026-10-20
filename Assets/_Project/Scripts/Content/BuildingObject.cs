using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks a placed building and says which BuildingDefinition it was made from. Every enabled building is listed in
/// Active, so systems (quests, residents, the ward breach) don't have to search the scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class BuildingObject : MonoBehaviour
{
    private static readonly List<BuildingObject> active = new List<BuildingObject>();

    [SerializeField] private BuildingDefinition definition;

    private Bounds? localBounds;

    public BuildingDefinition Definition => definition;

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

    private void OnEnable()
    {
        active.Add(this);
    }

    private void OnDisable()
    {
        active.Remove(this);
    }
}
