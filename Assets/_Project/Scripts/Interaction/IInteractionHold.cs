using UnityEngine;

/// <summary>
/// Optional, on an Interactable: holding Interact does something other than a tap (a tamed horse: a tap opens its menu, holding
/// gets straight on; the user, 2026-11-22). While <see cref="HoldVerb"/> names something, PlayerInteractor waits on the press: let go
/// before PlayerInteractor.HoldSeconds and the tap runs as always; still held then, <see cref="Held"/> runs instead.
/// </summary>
public interface IInteractionHold
{
    /// <summary>What holding does for <paramref name="user"/> now, for the prompt ("Ride"); null when holding does nothing else.</summary>
    string HoldVerb(GameObject user);

    /// <summary>Interact was held long enough: do it.</summary>
    void Held(GameObject user);
}
