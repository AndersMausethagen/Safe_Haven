using UnityEngine;
using UnityEngine.Events;

/// <summary>What a GameEventListener waits for.</summary>
public enum ListenFor
{
    /// <summary>A Quest Event is raised.</summary>
    QuestEvent = 0,
    /// <summary>A quest starts.</summary>
    QuestStarted = 1,
    /// <summary>A quest is completed.</summary>
    QuestCompleted = 2,
}

/// <summary>
/// Makes something happen in the scene when a Quest Event is raised or a quest starts or completes: open a gate, show a
/// bridge, play a sound, switch on a light. Fill in Response in the Inspector (for example GameObject > SetActive). It
/// can also respond when the scene loads if it already happened (quests, and Quest Events raised earlier in this game),
/// so a built bridge stays built. Listed in Add
/// Component as Safe Haven › Quests › Quest Event Listener.
/// </summary>
[AddComponentMenu("Safe Haven/Quests/Quest Event Listener")]
public sealed class GameEventListener : MonoBehaviour
{
    [SerializeField] private ListenFor listenFor = ListenFor.QuestEvent;
    [SerializeField] private GameEventDefinition gameEvent;
    [SerializeField] private QuestDefinition quest;
    [Tooltip("Also respond when the scene loads if it already happened (the quest started/completed, or the Quest Event was raised in this game), so a built bridge stays built.")]
    [SerializeField] private bool respondIfAlreadyHappened = true;
    [SerializeField] private UnityEvent response = new UnityEvent();
    [Tooltip("Optional. Used instead of Response on scene load, e.g. jump straight to the end state without sounds or effects. Leave empty to reuse Response.")]
    [SerializeField] private UnityEvent alreadyHappenedResponse = new UnityEvent();

    private void OnEnable()
    {
        GameEvents.Raised += OnGameEvent;
    }

    private void OnDisable()
    {
        GameEvents.Raised -= OnGameEvent;
    }

    private void Start()
    {
        if (respondIfAlreadyHappened && AlreadyHappened())
        {
            UnityEvent onLoad = alreadyHappenedResponse.GetPersistentEventCount() > 0 ? alreadyHappenedResponse : response;
            onLoad.Invoke();
        }
    }

    private bool AlreadyHappened()
    {
        QuestLog log = GameSession.Quests;
        return listenFor switch
        {
            ListenFor.QuestEvent => log.HasHappened(gameEvent),
            ListenFor.QuestStarted => quest != null && log.StatusOf(quest) != QuestStatus.NotStarted,
            ListenFor.QuestCompleted => quest != null && log.StatusOf(quest) == QuestStatus.Completed,
            _ => false,
        };
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        bool matches = listenFor switch
        {
            ListenFor.QuestEvent => gameEvent.Kind == GameEventKind.Custom && this.gameEvent != null && gameEvent.Subject == this.gameEvent,
            ListenFor.QuestStarted => gameEvent.Kind == GameEventKind.QuestStarted && quest != null && gameEvent.Subject == quest,
            ListenFor.QuestCompleted => gameEvent.Kind == GameEventKind.QuestCompleted && quest != null && gameEvent.Subject == quest,
            _ => false,
        };

        if (matches)
        {
            response.Invoke();
        }
    }
}
