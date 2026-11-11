using UnityEngine;

public enum PrologueInteractionKind { Campfire, Portal }

[RequireComponent(typeof(Interactable))]
public sealed class PrologueInteraction : MonoBehaviour, IInteractionCondition
{
    [SerializeField] private PrologueInteractionKind kind;
    [SerializeField] private string beforeStepText = "Follow the expedition's current objective.";
    [SerializeField] private string afterStepText = "This step is already complete.";
    [SerializeField] private string notReadyText = "The expedition is not ready.";
    private Interactable interactable;

    private void Awake() => interactable = GetComponent<Interactable>();
    private void OnEnable() => GetComponent<Interactable>().Completed.AddListener(Use);
    private void OnDisable()
    {
        if (interactable != null) interactable.Completed.RemoveListener(Use);
    }

    public bool CanInteract(GameObject user, out string reason)
    {
        PrologueDirector director = PrologueDirector.Current;
        if (director == null)
        {
            reason = notReadyText;
            return false;
        }
        PrologueStep required = kind == PrologueInteractionKind.Campfire
            ? PrologueStep.LightFire : PrologueStep.ActivatePortal;
        bool correctStep = director.Step == required && director.HasAcceptedChapter;
        reason = correctStep ? null : director.Step == required && !director.HasAcceptedChapter
            ? director.Definition.WaitingForLeaderText : director.Step > required && director.Step != PrologueStep.Story ? afterStepText : beforeStepText;
        return correctStep;
    }

    private void Use()
    {
        PrologueDirector director = PrologueDirector.Current;
        if (director == null) return;
        if (!CanInteract(interactable.Interactor, out string reason))
        {
            GameMessages.Post(reason, MessageKind.Warning);
            return;
        }
        if (kind == PrologueInteractionKind.Campfire)
        {
            GameMessages.Post("Build a campfire through the build menu ({key:Build}).", MessageKind.Info);
        }
        else if (director.Step == PrologueStep.ActivatePortal)
            GameEvents.Raise(director.Definition.PortalActivatedEvent, interactable.Interactor);
    }
}
