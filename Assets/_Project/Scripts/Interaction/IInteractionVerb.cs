/// <summary>
/// Optional, on an Interactable: what pressing Interact does, shown in the prompt as "[E] Light the way home" instead of
/// "[E] Interact". For interactions whose consequence the player should read before pressing.
/// </summary>
public interface IInteractionVerb
{
    string Verb { get; }
}
