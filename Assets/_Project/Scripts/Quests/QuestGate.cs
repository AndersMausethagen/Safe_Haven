using UnityEngine;

/// <summary>
/// Keeps an interaction locked until a quest has started (or is done), so the story is met in order: the rootbound
/// stones wait for "The Rootbound Stones", the portal for "Awakening the Portal". The Interactable asks it before work
/// begins and the interaction prompt shows the locked text.
/// <para>
/// Where it belongs: next to an Interactable (on the same GameObject). Leave the quest empty to never lock.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Interactable))]
[AddComponentMenu("Safe Haven/Quests/Quest Gate")]
public sealed class QuestGate : MonoBehaviour, IInteractionCondition
{
    public enum Requirement
    {
        [Tooltip("Open while the quest is active and after it is done.")]
        Started = 0,
        [Tooltip("Open only after the quest is done.")]
        Completed = 1,
    }

    [Tooltip("The quest that opens this interaction.")]
    [SerializeField] private QuestDefinition quest;
    [SerializeField] private Requirement requirement = Requirement.Started;
    [Tooltip("Shown on the interaction prompt while locked.")]
    [SerializeField] private string lockedText = "Not yet. Listen to the Heart Tree.";

    public QuestDefinition Quest => quest;

    public bool IsOpen => QuestCondition.Check(quest, requirement, GameSession.Quests);

    public bool CanInteract(GameObject user, out string reason)
    {
        bool open = IsOpen;
        reason = open ? null : lockedText;
        return open;
    }
}
