using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs the quests. GameSystems creates it when the game starts (with every quest from the ContentDatabase in a
/// Resources folder) and it lives across scenes. It:
/// <list type="bullet">
/// <item>starts quests when they become available: automatically after their prerequisites, on their start event, or
/// when a QuestGiver asks. Those that start by themselves wait their turn (StoryPacing): a pause after the last quest and
/// after a fight, and never over someone talking. Someone asking (a giver, a story's Start Quest) starts one at once;</item>
/// <item>feeds every game event to the player's QuestLog, which counts progress;</item>
/// <item>hands out rewards, raises the quest's completion event and tells the player what happened.</item>
/// </list>
/// Progress itself lives in GameSession.Quests, so it survives scene changes and is saved with the game.
/// <para>
/// Extending: new objective types go in QuestObjective (the counting rule) and QuestObjectiveDrawer (which fields it
/// shows), fed by a GameEventKind. New reward types go in QuestDefinition (the data) and GiveRewards here. New ways to
/// start a quest go in QuestStart and OnGameEvent or StartAvailableQuests here.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class QuestManager : MonoBehaviour, IGameSystem
{
    /// <summary>A quest that can start, waiting for its moment. Not saved: it is found again on entering a scene.</summary>
    private readonly struct WaitingQuest
    {
        public readonly QuestDefinition Quest;
        public readonly float ReadyAt;

        public WaitingQuest(QuestDefinition quest, float readyAt)
        {
            Quest = quest;
            ReadyAt = readyAt;
        }
    }

    private static QuestManager instance;

    private ContentDatabase content;
    private QuestLog log;
    private readonly List<QuestDefinition> pendingRewards = new List<QuestDefinition>();
    private readonly List<WaitingQuest> waiting = new List<WaitingQuest>();

    public static QuestManager Instance => instance;

    /// <summary>When the next story beat may come. Stories ask it before speaking.</summary>
    public StoryPacing Pacing { get; } = new StoryPacing();

    /// <summary>Raised whenever any quest starts, progresses or completes, for UI such as the quest tracker.</summary>
    public static event Action Changed;

    /// <summary>Something the quest displays read has changed without a quest changing (a pin).</summary>
    public static void NotifyChanged() => Changed?.Invoke();

    /// <summary>Every quest in the game.</summary>
    public IReadOnlyList<QuestDefinition> Quests => content != null ? content.Quests : (IReadOnlyList<QuestDefinition>)Array.Empty<QuestDefinition>();

    /// <summary>The current player's quest log. Re-bound automatically when a game is started or loaded.</summary>
    public QuestLog Log
    {
        get
        {
            Bind();
            return log;
        }
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Changed = null;
    }

    /// <summary>Called once by GameSystems when the game starts.</summary>
    public void Initialise(ContentDatabase database)
    {
        instance = this;
        content = database;
        GameEvents.Raised += OnGameEvent;
    }

    private void OnDestroy()
    {
        GameEvents.Raised -= OnGameEvent;
        Unbind();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        Pacing.Tick(Time.time, MonsterAI.AnyHuntingPlayerSide(), DialogueBox.IsSpeaking);
        // Quests begin once there is a player to receive them and they can see it, not behind a loading screen.
        if (waiting.Count > 0 && Player.Current != null && !SceneTransition.IsBusy)
        {
            StartWaitingQuests(Time.time);
        }
    }

    /// <summary>True while the quest can start and is only waiting for its moment, for the designer's Play Test panel.</summary>
    public bool IsWaiting(QuestDefinition quest) => waiting.Exists(entry => entry.Quest == quest);

    /// <summary>Starts every waiting quest whose moment has come.</summary>
    internal void StartWaitingQuests(float now)
    {
        StoryPacingSettings rules = StoryPacingSettings.Current;
        // Backwards: starting one can complete it and add its followers to the end of the list.
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            WaitingQuest entry = waiting[i];
            if (!CanBegin(entry.Quest))
            {
                waiting.RemoveAt(i);
            }
            else if (Pacing.CanStart(entry.ReadyAt, now, rules))
            {
                waiting.RemoveAt(i);
                Log.TryStart(entry.Quest);
            }
        }
    }

    private bool CanBegin(QuestDefinition quest) => Log.StatusOf(quest) == QuestStatus.NotStarted && Log.PrerequisitesMet(quest) && !Log.IsClosed(quest);

    // Starts it now if nothing is in the way, or else keeps it until something isn't.
    private void Begin(QuestDefinition quest)
    {
        if (!CanBegin(quest) || IsWaiting(quest))
        {
            return;
        }

        StoryPacingSettings rules = StoryPacingSettings.Current;
        float readyAt = Pacing.ReadyAt(quest.StartDelay, Time.time, rules);
        if (Pacing.CanStart(readyAt, Time.time, rules))
        {
            Log.TryStart(quest);
        }
        else
        {
            waiting.Add(new WaitingQuest(quest, readyAt));
            RaiseChanged();
        }
    }

    /// <summary>
    /// Starts a quest if it hasn't started and its prerequisites are done. False, with a reason for the player, when it
    /// can't.
    /// </summary>
    public bool TryStartQuest(QuestDefinition quest, out string reason)
    {
        reason = null;
        QuestStatus status = Log.StatusOf(quest);
        if (quest == null)
        {
            reason = "There is no quest here.";
        }
        else if (status == QuestStatus.Active)
        {
            reason = $"Already on \"{quest.DisplayName}\".";
        }
        else if (status == QuestStatus.ReadyToTurnIn)
        {
            reason = $"\"{quest.DisplayName}\" is done: return to {quest.TurnInTo}.";
        }
        else if (status == QuestStatus.Completed)
        {
            reason = $"\"{quest.DisplayName}\" is already done.";
        }
        else if (status == QuestStatus.Lapsed || Log.IsClosed(quest))
        {
            reason = $"\"{quest.DisplayName}\" can't be done any more.";
        }
        else if (!Log.PrerequisitesMet(quest))
        {
            reason = "Not yet: finish " + DescribeMissing(quest) + " first.";
        }

        return reason == null && Log.TryStart(quest);
    }

    /// <summary>
    /// Hands in a quest whose objectives are done (Turn In To Giver), giving its rewards. False, with a reason, when it
    /// isn't ready.
    /// </summary>
    public bool TryTurnIn(QuestDefinition quest, out string reason)
    {
        reason = null;
        if (quest == null)
        {
            reason = "There is no quest here.";
        }
        else if (Log.StatusOf(quest) != QuestStatus.ReadyToTurnIn)
        {
            reason = Log.StatusOf(quest) == QuestStatus.Completed ? $"\"{quest.DisplayName}\" is already done." : "That isn't finished yet.";
        }

        return reason == null && Log.TryTurnIn(quest);
    }

    private static void RaiseChanged()
    {
        Action handlers = Changed;
        if (handlers == null)
        {
            return;
        }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    // A new or loaded game replaces the log in GameSession, so the manager follows it.
    private void Bind()
    {
        if (log == GameSession.Quests)
        {
            return;
        }

        Unbind();
        pendingRewards.Clear();
        waiting.Clear();
        log = GameSession.Quests;
        log.Started += OnQuestStarted;
        log.Progressed += OnQuestProgressed;
        log.ReadyToTurnIn += OnQuestReadyToTurnIn;
        log.Completed += OnQuestCompleted;
        log.Lapsed += OnQuestLapsed;
        RaiseChanged();
    }

    private void Unbind()
    {
        if (log == null)
        {
            return;
        }

        log.Started -= OnQuestStarted;
        log.Progressed -= OnQuestProgressed;
        log.ReadyToTurnIn -= OnQuestReadyToTurnIn;
        log.Completed -= OnQuestCompleted;
        log.Lapsed -= OnQuestLapsed;
        log = null;
    }

    public void OnSceneEntered(Scene scene)
    {
        // Quests begin once there is a player to receive them, not in the menu.
        if (Player.Current != null)
        {
            foreach (QuestDefinition quest in new List<QuestDefinition>(pendingRewards))
            {
                string rewards = GiveRewards(quest);
                GameMessages.Post($"Rewards for {quest.DisplayName}: {rewards}", MessageKind.Gain);
            }

            pendingRewards.Clear();
            StartAvailableQuests();
            StartQuestsWhoseEventHappened();
        }

        RaiseChanged();
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        Log.Handle(gameEvent);
        foreach (QuestDefinition quest in Quests)
        {
            if (quest == null)
            {
                continue;
            }

            bool starts = quest.Start == QuestStart.OnQuestEvent
                ? gameEvent.Kind == GameEventKind.Custom && quest.StartEvent == gameEvent.Subject
                : quest.Start == QuestStart.WhenPlayerDoes && quest.StartWhen != null && gameEvent.ForPlayerSide && quest.StartWhen.Matches(gameEvent);
            if (starts)
            {
                Begin(quest);
            }
        }
    }

    private void StartAvailableQuests()
    {
        foreach (QuestDefinition quest in Quests)
        {
            if (quest != null && quest.Start == QuestStart.Automatically)
            {
                Begin(quest);
            }
        }
    }

    // A game saved while such a quest waited its turn has the event but not the quest, and the event won't come again.
    internal void StartQuestsWhoseEventHappened()
    {
        foreach (QuestDefinition quest in Quests)
        {
            if (quest != null && quest.Start == QuestStart.OnQuestEvent && Log.HasHappened(quest.StartEvent))
            {
                Begin(quest);
            }
            else if (quest != null && quest.Start == QuestStart.WhenPlayerDoes && quest.StartWhen != null && Log.HasDone(quest.StartWhen.DeedKey))
            {
                Begin(quest);
            }
        }
    }

    private void OnQuestStarted(QuestDefinition quest)
    {
        if (quest.AnnounceStart)
            GameMessages.Post($"New {(quest.Rank == QuestRank.MainStory ? "" : QuestMarkerSettings.RankName(quest.Rank).Split(' ')[0].ToLowerInvariant() + " ")}quest: {quest.DisplayName}", MessageKind.Info);
        GameEvents.Raise(new GameEvent(GameEventKind.QuestStarted, quest));
        RaiseAuthoredEvents(quest.OnStarted, quest);
        CountOwned(quest);
        CloseQuestsEndedBy(quest);
        RaiseChanged();
    }

    // Optional quests this one closes: what is finished is handed in, what is under way is left behind.
    private void CloseQuestsEndedBy(QuestDefinition started)
    {
        foreach (QuestDefinition quest in Quests)
        {
            if (quest == null || quest.ClosedBy != started)
            {
                continue;
            }

            if (log.StatusOf(quest) == QuestStatus.ReadyToTurnIn)
            {
                log.TryTurnIn(quest);
            }
            else
            {
                log.TryLapse(quest);
            }
        }
    }

    private void OnQuestLapsed(QuestDefinition quest)
    {
        GameMessages.Post($"Left undone: {quest.DisplayName}", MessageKind.Info);
        RaiseChanged();
    }

    private void OnQuestProgressed(QuestDefinition quest, int objective)
    {
        QuestLog.Entry entry = log.Get(quest);
        QuestObjective goal = quest.Objectives[objective];
        if (entry.Status == QuestStatus.Active)
        {
            GameMessages.Post($"{goal.Describe()}  {entry.Counts[objective]}/{goal.Amount}", MessageKind.Info);
        }

        RaiseChanged();
    }

    private void OnQuestReadyToTurnIn(QuestDefinition quest)
    {
        GameMessages.Post($"{quest.DisplayName}: return to {quest.TurnInTo}", MessageKind.Info);
        RaiseChanged();
    }

    private void OnQuestCompleted(QuestDefinition quest)
    {
        Pacing.QuestCompleted(Time.time);
        string rewards;
        // Once a raid is decided, what the player takes home is already settled, so rewards wait until they arrive.
        if (RaidDirector.Current != null && RaidDirector.Current.Session.IsOver && quest.DescribeRewards().Length > 0)
        {
            pendingRewards.Add(quest);
            rewards = "rewards waiting at home";
        }
        else
        {
            rewards = GiveRewards(quest);
        }

        if (quest.AnnounceCompletion)
            GameMessages.Post($"Quest complete: {quest.DisplayName}" + (rewards.Length > 0 ? $"  ({rewards})" : ""), MessageKind.Gain);
        else if (rewards.Length > 0)
            GameMessages.Post($"Rewards for {quest.DisplayName}: {rewards}", MessageKind.Gain);
        GameEvents.Raise(new GameEvent(GameEventKind.QuestCompleted, quest));
        if (quest.CompletionEvent != null)
        {
            GameEvents.Raise(quest.CompletionEvent);
        }
        RaiseAuthoredEvents(quest.OnCompleted, quest);

        RaiseChanged();
        // The next quests of a chain may be waiting for this one.
        StartAvailableQuests();
    }

    private string GiveRewards(QuestDefinition quest)
    {
        Player player = Player.Current;
        if (player == null)
        {
            Debug.LogWarning($"Quest \"{quest.DisplayName}\" was completed with no player in the scene, so its rewards were lost.");
            return "";
        }

        ItemInventory bag = player.Bag;
        ResourceStorage storage = player.Resources.Storage;
        foreach (ItemAmount reward in quest.RewardItems)
        {
            if (reward.Item != null && bag.Give(reward.Item, reward.Amount) < reward.Amount)
            {
                GameMessages.Post($"Bag full: some {reward.Item.DisplayName} from the quest was lost.", MessageKind.Warning);
            }
        }

        foreach (ResourceAmount reward in quest.RewardResources)
        {
            if (reward.Resource != null && storage.Add(reward.Resource, reward.Amount) < reward.Amount)
            {
                GameMessages.Post($"Can't carry all the {reward.Resource.DisplayName} from the quest.", MessageKind.Warning);
            }
        }

        if (quest.RewardRecipes.Count > 0 && player.Crafting != null)
        {
            player.Crafting.Unlock(quest.RewardRecipes);
        }

        return quest.DescribeRewards();
    }

    private static void RaiseAuthoredEvents(IReadOnlyList<GameEventDefinition> events, QuestDefinition quest)
    {
        foreach (var gameEvent in events)
        {
            if (gameEvent == null) Debug.LogError($"Quest '{quest.DisplayName}' has an empty lifecycle event.", quest);
            else GameEvents.Raise(gameEvent);
        }
    }

    // "Collect 10 ore" with Count Already Owned starts from what is in the bag.
    private void CountOwned(QuestDefinition quest)
    {
        for (int i = 0; i < quest.Objectives.Count; i++)
        {
            log.AddProgress(quest, i, quest.Objectives[i].Owned(Player.Current, log));
        }
    }

    /// <summary>The unfinished prerequisites of a quest, e.g. "\"Copper\" and \"Tools\"".</summary>
    public string DescribeMissing(QuestDefinition quest)
    {
        var missing = new List<string>();
        foreach (QuestDefinition prerequisite in quest.Prerequisites)
        {
            if (prerequisite != null && Log.StatusOf(prerequisite) != QuestStatus.Completed)
            {
                missing.Add($"\"{prerequisite.DisplayName}\"");
            }
        }

        return string.Join(" and ", missing);
    }
}
