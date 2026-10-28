using UnityEngine;

/// <summary>
/// The scaffold the player works at (hold E): made by Construction on a child of the building while it is being put up,
/// repaired or upgraded. Says what the work is ("[E] Build", "Repair", "Upgrade") and, for a ruin not paid for yet,
/// what it costs and whether the player has it.
/// </summary>
[DisallowMultipleComponent]
public sealed class ConstructionSite : MonoBehaviour, IInteractionCondition, IInteractionVerb
{
    public Construction Owner { get; set; }

    public string Verb => Owner == null ? "Build" : Owner.Level == 0 && Owner.StartsRuined ? "Repair" : Owner.Level == 0 ? "Build" : "Upgrade";

    public bool CanInteract(GameObject user, out string reason) => Owner.CanWork(user, out reason);
}
