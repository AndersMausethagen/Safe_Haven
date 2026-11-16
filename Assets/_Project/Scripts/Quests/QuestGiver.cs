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
    [Tooltip("Side offers: the question the player asks to hear about it. Empty: \"Anything I can do?\"")]
    [SerializeField] private string askText;
    [Tooltip("Side offers: what the giver says when it is handed in.")]
    [SerializeField, TextArea] private string thanksText;

    public QuestDefinition Quest => quest;
    public string Text => text;
    public string AcceptText => acceptText;
    public string WaitingText => waitingText;
    public string AskText => string.IsNullOrWhiteSpace(askText) ? "Anything I can do?" : askText;
    public string ThanksText => thanksText;
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
    [Tooltip("Optional quests this giver also has, each on its own: the player asks about them in conversation (needs a Talkable), in any order, beside the ordered offers. Give them Closed By to end them when the story moves on.")]
    [SerializeField] private QuestOffer[] sideOffers = Array.Empty<QuestOffer>();
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
    private static readonly List<QuestGiver> active = new List<QuestGiver>();

    // Entering Play doesn't reload scripts in this project, so statics are cleared when it starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => active.Clear();

    /// <summary>Every enabled giver, for the maps.</summary>
    public static IReadOnlyList<QuestGiver> Active => active;
    /// <summary>What the giver's floating mark shows now, and for which quest (null: its own).</summary>
    public QuestGiverState ShownState { get; private set; } = QuestGiverState.None;
    public QuestDefinition ShownQuest { get; private set; }
    /// <summary>Who the player would talk to.</summary>
    public string DisplayName => talkable != null ? talkable.SpeakerName : SpeakerName;

    public IReadOnlyList<QuestOffer> Offers => offers;
    public IReadOnlyList<QuestOffer> SideOffers => sideOffers;
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

        active.Add(this);
        QuestManager.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.Completed.RemoveListener(Interact);
        }

        active.Remove(this);
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
            // A lapsed quest is over too (the story moved on): it doesn't hold back the offers after it.
            QuestStatus status = log.StatusOf(candidate.Quest);
            if (status != QuestStatus.Completed && status != QuestStatus.Lapsed) return candidate;
        }
        return last;
    }

    /// <summary>What a giver for this quest offers, given the player's progress.</summary>
    public static QuestGiverState StateFor(QuestDefinition quest, QuestLog log)
    {
        // Co-op: a friend's people (and their notice board) ask things of the friend; a visitor's own quests wait for home.
        if (quest == null || log == null || HavenVisit.InHostsHaven)
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
            case QuestStatus.Lapsed:
                return QuestGiverState.None;
            default:
                if (quest.Start != QuestStart.ByQuestGiver || log.IsClosed(quest))
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
                reason = QuestManager.Current != null ? QuestManager.Current.NotYetReason(Quest) : "Not yet.";
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
        if (QuestManager.Current == null)
        {
            return;
        }

        // Starting can synchronously complete quests and select the next offer.
        string line = offer != null ? offer.Text : greeting;
        if (QuestManager.Current.TryStartQuest(Quest, out string reason))
        {
            EmoteDefinition taken = CharacterCatalog.Current.QuestTaken;
            CharacterGestures.Play(transform, taken);
            if (Player.Current != null) CharacterGestures.Play(Player.Current.transform, taken);
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
        if (QuestManager.Current == null)
        {
            return;
        }

        if (QuestManager.Current.TryTurnIn(Quest, out string reason))
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
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        offer = SelectOffer(offers, log);
        state = StateFor(Quest, log);
        if (interactable != null && talkable == null)
        {
            // A finished giver with nothing more to say stops offering the interaction at all.
            interactable.enabled = !(state == QuestGiverState.Done && !afterLines.HasAny) && state != QuestGiverState.None;
        }

        ShownState = MarkerState(log, out QuestDefinition shownQuest);
        ShownQuest = shownQuest;
        if (showMarker && Application.isPlaying)
        {
            if (marker == null)
            {
                marker = QuestMarker.CreateFor(transform);
            }

            marker.Show(ShownState, shownQuest != null ? shownQuest.Rank : QuestRank.MainStory);
        }
    }

    // What the floating mark shows: something to hand in first, then something to take, then what the ordered offer says.
    // The ordered offer goes first among equals, as it is the giver's main business.
    private QuestGiverState MarkerState(QuestLog log, out QuestDefinition shownQuest)
    {
        QuestGiverState shown = state;
        shownQuest = Quest;
        foreach (QuestOffer side in sideOffers)
        {
            QuestGiverState own = StateFor(side?.Quest, log);
            if (own == QuestGiverState.ReadyToTurnIn && shown != QuestGiverState.ReadyToTurnIn || own == QuestGiverState.Available && shown != QuestGiverState.ReadyToTurnIn && shown != QuestGiverState.Available
                || own == QuestGiverState.InProgress && (shown == QuestGiverState.None || shown == QuestGiverState.Done))
            {
                shown = own;
                shownQuest = side.Quest;
            }
        }

        return shown;
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
                () => AcceptOffer(offeredQuest), closesMenu: true, quest: offeredQuest, does: QuestAnswer.Starts));
        }
        else if (state == QuestGiverState.ReadyToTurnIn)
        {
            QuestOffer next = FollowingOffer();
            if (next != null)
            {
                page.Body = string.IsNullOrWhiteSpace(turnInText) ? next.Text : turnInText + "\n\n" + next.Text;
                page.Add(new MenuAction(next.AcceptText, next.Quest.DisplayName, true,
                    () => ContinueOffer(offeredQuest, next.Quest), closesMenu: true, quest: next.Quest, does: QuestAnswer.Starts));
            }
            else
            {
                page.Body = turnInText;
                page.Add(new MenuAction("It's done.", offeredQuest.DisplayName, true, () => {
                    Refresh();
                    if (Quest == offeredQuest && state == QuestGiverState.ReadyToTurnIn) TurnIn();
                }, closesMenu: true, quest: offeredQuest, does: QuestAnswer.HandsIn));
            }
        }
        else if (state != QuestGiverState.None)
        {
            page.Body = state == QuestGiverState.Done ? Line(afterLines) :
                offer != null && !string.IsNullOrWhiteSpace(offer.WaitingText) ? offer.WaitingText :
                waitingLines.HasAny ? Line(waitingLines) : DefaultWaitingText;
        }
        return page;
    }

    private QuestOffer FollowingOffer()
    {
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        bool found = false;
        foreach (QuestOffer candidate in offers)
        {
            if (candidate?.Quest == null) continue;
            if (!found)
            {
                found = candidate.Quest == Quest;
                continue;
            }

            QuestDefinition next = candidate.Quest;
            if (next.Start != QuestStart.ByQuestGiver || log.StatusOf(next) != QuestStatus.NotStarted) return null;
            bool follows = false;
            foreach (QuestDefinition prerequisite in next.Prerequisites)
            {
                if (prerequisite == Quest) follows = true;
                else if (prerequisite != null && log.StatusOf(prerequisite) != QuestStatus.Completed) return null;
            }
            return follows ? candidate : null;
        }
        return null;
    }

    private void ContinueOffer(QuestDefinition finished, QuestDefinition next)
    {
        Refresh();
        if (Quest != finished || state != QuestGiverState.ReadyToTurnIn || QuestManager.Current == null) return;
        if (!QuestManager.Current.TryTurnIn(finished, out string reason))
        {
            GameMessages.Post(reason, MessageKind.Warning);
            return;
        }

        Refresh();
        if (Quest == next && state == QuestGiverState.Available) StartQuest();
        else if (QuestManager.Current.Log.StatusOf(next) == QuestStatus.NotStarted)
            GameMessages.Post("The next quest is no longer available. Speak to the quest giver again.", MessageKind.Warning);
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

        AddSideOffers(page, speaker, afterAnswer);
    }

    // Each optional quest as its own answer: ask about it, take it, or hand it in.
    private void AddSideOffers(MenuPage page, DialogueSpeaker speaker, Action afterAnswer)
    {
        QuestLog log = QuestManager.Current != null ? QuestManager.Current.Log : GameSession.Quests;
        foreach (QuestOffer side in sideOffers)
        {
            if (side?.Quest == null) continue;
            QuestOffer chosen = side;
            switch (StateFor(side.Quest, log))
            {
                case QuestGiverState.Available:
                    page.Add(new MenuAction(side.AskText, side.Quest.DisplayName, true, () => DialogueBox.Current.Ask(speaker, chosen.Text,
                        new[] { new DialogueChoice(chosen.AcceptText, null, true, chosen.Quest, QuestAnswer.Starts), new DialogueChoice("Not now.") }, answer =>
                        {
                            if (answer == 0) TakeSideOffer(chosen);
                            afterAnswer?.Invoke();
                        }), quest: side.Quest, does: QuestAnswer.AsksAbout));
                    break;
                case QuestGiverState.ReadyToTurnIn:
                    page.Add(new MenuAction("It's done.", side.Quest.DisplayName, true, () =>
                    {
                        if (QuestManager.Current != null && QuestManager.Current.TryTurnIn(chosen.Quest, out string reason))
                        {
                            if (string.IsNullOrWhiteSpace(chosen.ThanksText)) afterAnswer?.Invoke();
                            else DialogueBox.Current.Say(speaker, chosen.ThanksText, afterAnswer);
                        }
                    }, quest: side.Quest, does: QuestAnswer.HandsIn));
                    break;
                case QuestGiverState.InProgress:
                    if (string.IsNullOrWhiteSpace(page.Body)) page.Body = side.WaitingText;
                    break;
            }
        }
    }

    private void TakeSideOffer(QuestOffer side)
    {
        if (QuestManager.Current == null) return;
        if (QuestManager.Current.TryStartQuest(side.Quest, out string reason))
        {
            EmoteDefinition taken = CharacterCatalog.Current.QuestTaken;
            CharacterGestures.Play(transform, taken);
            if (Player.Current != null) CharacterGestures.Play(Player.Current.transform, taken);
        }
        else
        {
            GameMessages.Post(reason, MessageKind.Warning);
        }
    }
}
