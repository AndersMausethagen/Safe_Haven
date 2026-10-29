using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>An authored offer in a giver's ordered quest sequence. Progress stays in QuestLog.</summary>
[Serializable]
public sealed class QuestOffer
{
    [SerializeField] private QuestDefinition quest;
    [SerializeField, TextArea(2, 6)] private string text;
    [SerializeField] private string acceptText = "I'll do it.";
    [SerializeField, TextArea] private string waitingText;

    public QuestDefinition Quest => quest;
    public string Text => text;
    public string AcceptText => acceptText;
    public string WaitingText => waitingText;
}

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
/// With a Talkable beside it, the giver is part of a conversation instead: its line opens it and taking or handing in the
/// quest are answers, chosen explicitly. Without an Interactable, call StartQuest or TurnIn from any Inspector event.
/// Quick setup: GameObject › Safe Haven › Quest Giver.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Safe Haven/Quests/Quest Giver")]
public sealed class QuestGiver : MonoBehaviour, IInteractionCondition, IInteractionNote, IConversationTopics
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
    [Tooltip("Optional ordered offers, replacing the single Quest above. The first unfinished offer is selected; prerequisites still apply.")]
    [SerializeField] private QuestOffer[] offers = Array.Empty<QuestOffer>();
    [Tooltip("Who says the lines when there is no Talkable (which then names the speaker). Empty: the object's name.")]
    [SerializeField] private string speakerName;
    [Tooltip("Shown in the dialogue box instead of a live portrait of this object. Optional; without a Talkable.")]
    [SerializeField] private Sprite portrait;
    [SerializeField, Range(0.3f, 1f)] private float dialogueCameraDistance = 0.65f;

    private Interactable interactable;
    private QuestMarker marker;
    private QuestGiverState state = QuestGiverState.None;
    private float lastAsked = float.NegativeInfinity;
    private QuestOffer offer;
    private Talkable talkable;

    public IReadOnlyList<QuestOffer> Offers => offers;
    public QuestDefinition Quest => offer != null ? offer.Quest : quest;
    public QuestGiverState State => state;
    public bool IsNote => talkable == null && (state == QuestGiverState.InProgress || state == QuestGiverState.Done);

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    private void OnEnable()
    {
        // A Talkable owns the interaction: the quest is taken in its conversation.
        talkable = GetComponent<Talkable>();
        if (interactable != null && talkable == null)
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

    /// <summary>Selects the first unfinished offer, never skipping a locked quest to offer a later one.</summary>
    public static QuestOffer SelectOffer(IReadOnlyList<QuestOffer> sequence, QuestLog log)
    {
        QuestOffer last = null;
        foreach (var candidate in sequence)
        {
            if (candidate?.Quest == null) continue;
            last = candidate;
            if (log.StatusOf(candidate.Quest) != QuestStatus.Completed) return candidate;
        }
        return last;
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
        if (talkable != null) return true;
        switch (state)
        {
            case QuestGiverState.Available:
            case QuestGiverState.ReadyToTurnIn:
                return true;
            case QuestGiverState.NotYet:
                reason = QuestManager.Instance != null ? "Not yet: finish " + QuestManager.Instance.DescribeMissing(Quest) + " first." : "Not yet.";
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

        // Starting can synchronously complete quests and select the next offer.
        string line = offer != null ? offer.Text : greeting;
        if (QuestManager.Instance.TryStartQuest(Quest, out string reason))
        {
            if (talkable == null) Say(line);
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

        if (QuestManager.Instance.TryTurnIn(Quest, out string reason))
        {
            Say(turnInText);
        }
        else
        {
            GameMessages.Post(reason, MessageKind.Warning);
        }
    }

    // The giver's line in the dialogue box (outside Play mode, such as in tests, in the message feed).
    private void Say(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        if (!Application.isPlaying)
        {
            GameMessages.Post(line, MessageKind.Info);
            return;
        }

        DialogueBox.Current.Say(talkable != null ? talkable.Speaker : new DialogueSpeaker(SpeakerName, transform, portrait, dialogueCameraDistance), line);
    }

    private string SpeakerName => string.IsNullOrWhiteSpace(speakerName) ? gameObject.name : speakerName;

    private void Refresh()
    {
        QuestLog log = QuestManager.Instance != null ? QuestManager.Instance.Log : GameSession.Quests;
        offer = SelectOffer(offers, log);
        state = StateFor(Quest, log);
        if (interactable != null && talkable == null)
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

    /// <summary>What the giver says in a conversation and the answers that take or hand in the quest.</summary>
    public MenuPage BuildDialoguePage()
    {
        Refresh();
        var page = new MenuPage { Title = talkable != null ? talkable.SpeakerName : SpeakerName };
        QuestDefinition offeredQuest = Quest;
        if (state == QuestGiverState.Available)
        {
            page.Body = offer != null ? offer.Text : greeting;
            page.Add(new MenuAction(offer != null ? offer.AcceptText : "I'll do it.", offeredQuest.DisplayName, true,
                () => AcceptOffer(offeredQuest), closesMenu: true));
        }
        else if (state == QuestGiverState.ReadyToTurnIn)
        {
            page.Body = turnInText;
            page.Add(new MenuAction("It's done.", offeredQuest.DisplayName, true, () => {
                Refresh();
                if (Quest == offeredQuest && state == QuestGiverState.ReadyToTurnIn) TurnIn();
            }, closesMenu: true));
        }
        else if (state != QuestGiverState.None)
        {
            page.Body = state == QuestGiverState.Done ? Line(afterLines) :
                offer != null && !string.IsNullOrWhiteSpace(offer.WaitingText) ? offer.WaitingText :
                waitingLines.HasAny ? Line(waitingLines) : DefaultWaitingText;
        }
        return page;
    }

    private void AcceptOffer(QuestDefinition expected)
    {
        Refresh();
        // A menu may outlive its offer (an event or another interaction can advance the log).
        if (Quest == expected && state == QuestGiverState.Available) StartQuest();
    }

    /// <summary>Adds the giver's line (when nothing before it spoke) and its quest answers to a Talkable's conversation.</summary>
    public void AddTopics(MenuPage page, DialogueSpeaker speaker, Action afterAnswer)
    {
        MenuPage mine = BuildDialoguePage();
        if (string.IsNullOrWhiteSpace(page.Body))
        {
            page.Body = mine.Body;
        }

        foreach (MenuAction action in mine.Actions)
        {
            page.Add(action);
        }
    }
}
