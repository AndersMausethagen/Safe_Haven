using UnityEngine;

/// <summary>
/// What pressing Interact on this object does, written for the prompt ("[E] Break down"), for objects whose interaction
/// says nothing of its own. Beside the Interactable.
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractionVerb : MonoBehaviour, IInteractionVerb
{
    [Tooltip("Shown in the prompt instead of \"Interact\".")]
    [SerializeField] private string verb = "Use";

    public string Verb => verb;
}
