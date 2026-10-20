/// <summary>
/// A rule that can stop an interaction from starting, such as a resource node that needs a better tool. Put it on the
/// same GameObject as the Interactable; the Interactable asks every condition before it begins.
/// </summary>
public interface IInteractionCondition
{
    /// <summary>Side-effect-free preview: false, with a reason for the player, when the user can't start the interaction.</summary>
    bool CanInteract(UnityEngine.GameObject user, out string reason);
}
