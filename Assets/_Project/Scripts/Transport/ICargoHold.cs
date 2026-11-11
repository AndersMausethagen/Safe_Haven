using UnityEngine;

/// <summary>
/// Something with a cargo the player loads from the bag in the cargo window: a cart's or boat's hold (Transport), a horse's
/// saddle bags (Horse). The window shows its name, a line about it, the load against what it carries easily and what the
/// load costs it in speed.
/// </summary>
public interface ICargoHold
{
    // The component's own transform: where it is, for the window's reach.
    Transform transform { get; }

    ItemContainer Cargo { get; }

    string CargoTitle { get; }

    string CargoDescription { get; }

    float Weight { get; }

    float Capacity { get; }

    bool IsOverfilled { get; }

    /// <summary>Its speed with this load, as a share of its speed empty.</summary>
    float SpeedScale { get; }
}
