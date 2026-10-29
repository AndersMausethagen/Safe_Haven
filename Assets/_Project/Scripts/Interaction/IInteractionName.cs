/// <summary>
/// The name the interaction prompt shows for an object, the one players know it by ("Iron Ore", "Someone hiding",
/// "Ruined Portal"). Put it on the Interactable's GameObject; null or empty falls back to the object's name.
/// </summary>
public interface IInteractionName
{
    string InteractionName { get; }
}
