using UnityEngine;

/// <summary>
/// The scaffold the player works at (hold E): made by Construction on a child of the building while it is being put up,
/// repaired or upgraded. Says what the work is ("[E] Build", "Repair", "Upgrade") and, for a ruin not paid for yet,
/// what it costs and whether the player has it. Reach is measured to the edge of its box around the building: the box
/// is a trigger, which Interactable does not measure to, so without this the reach ran from the building's middle and a
/// large building (the workshop) could not be worked at all.
/// </summary>
[DisallowMultipleComponent]
public sealed class ConstructionSite : MonoBehaviour, IInteractionCondition, IInteractionVerb, IInteractionShape
{
    private BoxCollider box;

    public Construction Owner { get; set; }

    public string Verb => Owner == null ? "Build" : Owner.Level == 0 && Owner.StartsRuined ? "Repair" : Owner.Level == 0 ? "Build" : "Upgrade";

    public bool CanInteract(GameObject user, out string reason) => Owner.CanWork(user, out reason);

    public Vector3 ClosestPoint(Vector3 point)
    {
        if (box == null && !TryGetComponent(out box))
        {
            return transform.position;
        }

        Vector3 local = transform.InverseTransformPoint(point) - box.center;
        Vector3 half = box.size * 0.5f;
        local = new Vector3(Mathf.Clamp(local.x, -half.x, half.x), Mathf.Clamp(local.y, -half.y, half.y), Mathf.Clamp(local.z, -half.z, half.z));
        return transform.TransformPoint(local + box.center);
    }
}
