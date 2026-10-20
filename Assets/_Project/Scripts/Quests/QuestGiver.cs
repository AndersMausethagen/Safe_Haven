using UnityEngine;

/// <summary>What a Quest Giver currently offers the player, which decides its marker and whether it can be used.</summary>
public enum QuestGiverState
{
    /// <summary>Nothing to do here (no quest, or one that starts some other way and hasn't started).</summary>
    None,
    /// <summary>The quest's prerequisites aren't done yet.</summary>
    NotYet,
    /// <summary>The quest can be taken: "!".</summary>
    Available,
    /// <summary>The player is on the quest: "…".</summary>
    InProgress,
    /// <summary>The objectives are done and the quest is handed in here: "?".</summary>
    ReadyToTurnIn,
    /// <summary>The quest is complete.</summary>
    Done,
}

/// <summary>
/// Hands out a quest and, when the quest is set to Turn In To Giver, takes it back: an NPC, a notice board, a strange
/// stone. With an Interactable on the same object, the giver reacts to the quest's state:
/// <list type="bullet">
/// <item>available: shows "!" and interacting starts the quest (with the greeting);</item>
/// <item>prerequisites missing: says what to finish first;</item>
/// <item>in progress: shows "…" and says the waiting line;</item>
/// <item>ready to hand in: shows "?" and interacting completes the quest (with the thanks line);</item>
/// <item>done: says the after line, or switches the interaction off when there is none.</item>
/// </list>
/// Without an Interactable, call StartQuest or TurnIn from any Inspector event. Quick setup: GameObject › Safe Haven ›
/// Quest Giver.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/Quests/Quest Giver")]
public sealed class QuestGiver : MonoBehaviour, IInteractionCondition, IInteractionNote
{
    private const string DefaultWaitingText = "Come back when it's done.";
    private const float LookAwaySeconds = 1f;

    [SerializeField] private QuestDefinition quest;
    [Tooltip("Shown when the quest starts, e.g. what the giver says. Optional.")]
    [SerializeField, TextArea] private string greeting;
    [Tooltip("Said while the player is on the quest and looks at the giver; a new line each time they come back. Empty: \"" + DefaultWaitingText + "\"")]
    [SerializeField] private DialogueLines waitingLines = new DialogueLines();
    [Tooltip("Shown when the quest is handed in here (Turn In To Giver quests). Optional.")]
    [SerializeField, TextArea] private string turnInText;
    [Tooltip("Said when the player looks at the giver after the quest is done. Empty switches the interaction off instead.")]
    [SerializeField] private DialogueLines afterLines = new DialogueLines();
    [Tooltip("Shows the floating !, … and ? above the giver. Their look is shared: Systems Editor › Quests.")]
    [SerializeField] private bool showMarker = true;

    private Interactable interactable;
    private QuestMarker marker;
    private QuestGiverState state = QuestGiverState.None;
    private float lastAsked = float.NegativeInfinity;

    public QuestDefinition Quest => quest;
    public QuestGiverState State => state;
    public bool IsNote => state == QuestGiverState.InProgress || state == QuestGiverState.Done;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.Completed.AddListener(Interact);
        }

        QuestManager.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(Interact);
        }

        QuestManager.Changed -= Refresh;
    }

    /// <summary>What a giver for this quest offers, given the player's progress.</summary>
    public static QuestGiverState StateFor(QuestDefinition quest, QuestLog log)
    {
        if (quest == null || log == null)
        {
            return QuestGiverState.None;
        }

        switch (log.StatusOf(quest))
        {
            case QuestStatus.Completed:
                return QuestGiverState.Done;
            case QuestStatus.ReadyToTurnIn:
                return quest.TurnInToGiver ? QuestGiverState.ReadyToTurnIn : QuestGiverState.InProgress;
            case QuestStatus.Active:
                return QuestGiverState.InProgress;
            default:
                if (quest.Start != QuestStart.ByQuestGiver)
                {
                    return QuestGiverState.None;
                }

                return log.PrerequisitesMet(quest) ? QuestGiverState.Available : QuestGiverState.NotYet;
        }
    }

    public bool CanInteract(GameObject user, out string reason)
    {
        reason = null;
        switch (state)
        {
            case QuestGiverState.Available:
            case QuestGiverState.ReadyToTurnIn:
                return true;
            case QuestGiverState.NotYet:
                reason = QuestManager.Instance != null ? "Not yet: finish " + QuestManager.Instance.DescribeMissing(quest) + " first." : "Not yet.";
                return false;
            case QuestGiverState.InProgress:
                reason = waitingLines.HasAny ? Line(waitingLines) : DefaultWaitingText;
                return false;
            case QuestGiverState.Done:
                reason = Line(afterLines);
                return false;
            default:
                reason = "Nothing to do here yet.";
                return false;
        }
    }

    // The prompt asks every frame while the player looks; a new line only once they looked away and came back.
    private string Line(DialogueLines lines)
    {
        float now = Time.unscaledTime;
        bool cameBack = now - lastAsked > LookAwaySeconds;
        lastAsked = now;
        return cameBack ? lines.Advance() : lines.Current;
    }

    /// <summary>Starts or hands in the quest, whichever fits its state. What the Interactable calls.</summary>
    public void Interact()
    {
        if (state == QuestGiverState.ReadyToTurnIn)
        {
            TurnIn();
        }
        else
        {
            StartQuest();
        }
    }

    /// <summary>Starts the quest, or tells the player why it can't start. Can be chosen from any Inspector event.</summary>
    public void StartQuest()
    {
        if (QuestManager.Instance == null)
        {
            return;
        }

        if (QuestManager.Instance.TryStartQuest(quest, out string reason))
        {
            Post(greeting);
        }
        else
        {
            GameMessages.Post(reason, MessageKind.Warning);
        }
    }

    /// <summary>Hands in the quest when its objectives are done. Can be chosen from any Inspector event.</summary>
    public void TurnIn()
    {
        if (QuestManager.Instance == null)
        {
            return;
        }

        if (QuestManager.Instance.TryTurnIn(quest, out string reason))
        {
            Post(turnInText);
        }
        else
        {
            GameMessages.Post(reason, MessageKind.Warning);
        }
    }

    private static void Post(string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            GameMessages.Post(line, MessageKind.Info);
        }
    }

    private void Refresh()
    {
        state = QuestManager.Instance != null ? StateFor(quest, QuestManager.Instance.Log) : StateFor(quest, GameSession.Quests);
        if (interactable != null)
        {
            // A finished giver with nothing more to say stops offering the interaction at all.
            interactable.enabled = !(state == QuestGiverState.Done && !afterLines.HasAny) && state != QuestGiverState.None;
        }

        if (showMarker && Application.isPlaying)
        {
            if (marker == null)
            {
                marker = QuestMarker.CreateFor(transform);
            }

            marker.Show(state);
        }
    }
}
