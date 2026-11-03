using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the player may build, and what: the portal's ward at home, the expedition's camp in the prologue. BuildPlacer
/// asks the current one (<see cref="BuildArea.Current"/>) instead of knowing either; an area enters and leaves through
/// BuildArea.
/// </summary>
public interface IBuildArea
{
    /// <summary>Why nothing can be built right now, in words for the player, or null when building is open.</summary>
    string WhyClosed { get; }

    /// <summary>The build menu's opening line: where and what to build.</summary>
    string Guidance { get; }

    /// <summary>True when buildings may be moved, upgraded and repaired here.</summary>
    bool AllowsChanges { get; }

    /// <summary>The height of the build ground, for markers drawn on it.</summary>
    float GroundHeight { get; }

    /// <summary>What can be built here, from what the player knows.</summary>
    IEnumerable<BuildingDefinition> Buildable(PlayerCrafting crafting);

    /// <summary>Where the camera's pointer meets the build ground.</summary>
    bool TryGetGroundPoint(Camera view, out Vector3 point);

    /// <summary>The point moved up or down onto the build ground.</summary>
    Vector3 OnGround(Vector3 point);

    /// <summary>Why a building with <paramref name="footprint"/> can't stand at <paramref name="point"/>, or null.
    /// Collisions and cost are BuildPlacer's.</summary>
    string CheckSpot(FootprintRect footprint, Vector3 point);

    /// <summary>Why <paramref name="building"/> can't be built here now, or null.</summary>
    string CheckBuilding(BuildingDefinition building);
}

/// <summary>The build areas in the loaded scenes; the latest to enter is the current one.</summary>
public static class BuildArea
{
    private static readonly List<IBuildArea> open = new List<IBuildArea>();

    public static IBuildArea Current => open.Count > 0 ? open[open.Count - 1] : null;

    public static void Enter(IBuildArea area)
    {
        if (!open.Contains(area))
        {
            open.Add(area);
        }
    }

    public static void Leave(IBuildArea area)
    {
        open.Remove(area);
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        open.Clear();
    }
}
