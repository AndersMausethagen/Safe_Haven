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


    private ContentDatabase content;
    private QuestLog log;
    private readonly List<WaitingQuest> waiting = new List<WaitingQuest>();
    private readonly List<WaitingQuest> due = new List<WaitingQuest>();

    public static QuestManager Current { get; private set; }

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
        Current = null;
        Changed = null;
    }

    /// <summary>Called once by GameSystems when the game starts.</summary>
    public void Initialise(ContentDatabase database)
    {
        Current = this;
        content = database;
        GameEvents.Raised += OnGameEvent;
    }

    private void OnDestroy()
    {
        GameEvents.Raised -= OnGameEvent;
        Unbind();
        if (Current == this)
        {
            Current = null;
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

    /// <summary>
    /// True while a Haven, side or optional quest is ready but held for a homecoming (QuestDefinition.HeldForHomecoming): the
    /// player is away, or this return's share has begun. The journal lists these, so nothing is hidden.
    /// </summary>
    public bool IsHeld(QuestDefinition quest) => quest != null && quest.HeldForHomecoming && IsWaiting(quest) && !MayStartHeld(quest);

    /// <summary>Why a held quest (IsHeld) hasn't begun, in words for the journal.</summary>
    public string HeldReason(QuestDefinition quest)
    {
        if (!AtHome)
        {
            return "Begins when you come home from a raid";
        }

        // As MayStartHeld has it: once this homecoming's share is used, when no other of its kind is under way. RankName already says
        // "quest" where it should ("Haven quest", "Tale").
        return $"Begins when no other {QuestMarkerSettings.RankName(quest.Rank).ToLowerInvariant()} is under way, or after your next raid";
    }

    /// <summary>Starts every waiting quest whose moment has come, in the order they became ready.</summary>
    internal void StartWaitingQuests(float now)
    {
        StoryPacingSettings rules = StoryPacingSettings.Current;
        // A copy: starting one can complete it and add its followers to the list (they get their turn next frame). Held quests can
        // wait a whole raid, so the copy is kept rather than made each frame.
        due.Clear();
        due.AddRange(waiting);
        for (int i = 0; i < due.Count; i++)
        {
            WaitingQuest entry = due[i];
            if (!CanBegin(entry.Quest))
            {
                Forget(entry.Quest);
            }
            else if (!MayStartHeld(entry.Quest) || entry.Quest.HeldForHomecoming && now < heldSpacedUntil)
            {
                // Held: its moment counts from when it may begin, so on coming home it still waits out a welcome or a fight
                // (a moment long past would otherwise have outlasted Longest Hold and started over someone talking).
                Hold(entry.Quest, now);
            }
            else if (entry.Quest.StartsInAFight ? now >= entry.ReadyAt : Pacing.CanStart(entry.ReadyAt, now, rules))
            {
                Forget(entry.Quest);
                StartNow(entry.Quest);
            }
        }
    }

    private void Forget(QuestDefinition quest) => waiting.RemoveAll(entry => entry.Quest == quest);

    // Runs each frame for every held quest while away (a whole raid), so no closure.
    private void Hold(QuestDefinition quest, float now)
    {
        for (int i = 0; i < waiting.Count; i++)
        {
            if (waiting[i].Quest == quest)
            {
                if (waiting[i].ReadyAt < now)
                {
                    waiting[i] = new WaitingQuest(quest, now);
                }

                return;
            }
        }
    }

    // A quest is done: nothing waiting starts within Between Quests of it. A held quest it frees (one of its kind no longer under
    // way) was held at "now" and began in the same frame, the two notices together and its stories at once.
    internal void SpaceWaitingFrom(float now)
    {
        float earliest = now + StoryPacingSettings.Current.BetweenQuestsSeconds;
        for (int i = 0; i < waiting.Count; i++)
        {
            if (waiting[i].ReadyAt < earliest)
            {
                waiting[i] = new WaitingQuest(waiting[i].Quest, earliest);
            }
        }
    }

    // Coming home: held quests count their moment from the arrival, not from the last frame before the loading screen.
    private void HoldAllHeld(float now)
    {
        for (int i = 0; i < waiting.Count; i++)
        {
            if (waiting[i].Quest != null && waiting[i].Quest.HeldForHomecoming && waiting[i].ReadyAt < now)
            {
                waiting[i] = new WaitingQuest(waiting[i].Quest, now);
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
        // Not behind a loading screen (a scene just entered runs this before it is shown): it waits, and Update starts it once seen.
        if (!SceneTransition.IsBusy && (quest.StartsInAFight ? Time.time >= readyAt : Pacing.CanStart(readyAt, Time.time, rules)) && MayStartHeld(quest)
            && (!quest.HeldForHomecoming || Time.time >= heldSpacedUntil))
        {
            StartNow(quest);
        }
        else
        {
            waiting.Add(new WaitingQuest(quest, readyAt));
            RaiseChanged();
        }
    }

    // Until when the next held quest waits after one has begun.
    private float heldSpacedUntil = float.NegativeInfinity;

    // The share of held quests each homecoming is counted in the settlement (SettlementState.HeldQuestsStarted), which is saved:
    // a Continue at the same homecoming doesn't open a fresh share.
    private void StartNow(QuestDefinition quest)
    {
        if (Log.TryStart(quest) && quest.HeldForHomecoming)
        {
            // Held quests begin only at home (MayStartHeld), where one's own Haven is the one here.
            GameSession.Home.NoteHeldQuestStarted();
            // The next held one waits its turn after this one (not two toasts in one frame on coming home).
            heldSpacedUntil = Time.time + StoryPacingSettings.Current.BetweenQuestsSeconds;
        }
    }

    /// <summary>At home: not on a raid (nor in its scene after the extraction, whose results are still showing), not in a
    /// friend's Haven. Held quests begin only here.</summary>
    private static bool AtHome => Place.AtHome;

    // A held quest begins only at home, within this homecoming's share, or when nothing of its kind is under way.
    private bool MayStartHeld(QuestDefinition quest)
    {
        if (!quest.HeldForHomecoming)
        {
            return true;
        }

        if (!AtHome)
        {
            return false;
        }

        return GameSession.Here.HeldQuestsStarted < StoryPacingSettings.Current.NewPerHomecoming || !AnyUnderWay(quest.Rank);
    }

    // Which kinds have a quest under way, worked out again only after a quest starts, completes or lapses. Only quests begun at this
    // homecoming count (the quest flow review's C4): one left open for a whole raid (Strength for the Road, 43 minutes in the 11-20 run)
    // held every quest of its colour behind it. Kept in memory: after a load none counts, so one more may begin.
    private readonly bool[] underWay = new bool[8];
    private bool underWayKnown;
    private readonly Dictionary<QuestDefinition, int> startedInHomecoming = new Dictionary<QuestDefinition, int>();

    private int underWayHomecoming = -1;

    private bool AnyUnderWay(QuestRank rank)
    {
        // A new homecoming opens a fresh share: what was under way at the last one no longer counts.
        if (underWayHomecoming != GameSession.Home.HeldQuestsHomecoming)
        {
            underWayHomecoming = GameSession.Home.HeldQuestsHomecoming;
            underWayKnown = false;
        }

        if (!underWayKnown)
        {
            System.Array.Clear(underWay, 0, underWay.Length);
            foreach (QuestLog.Entry entry in Log.Entries)
            {
                if (entry.Quest != null && (int)entry.Quest.Rank < underWay.Length
                    && (entry.Status == QuestStatus.Active || entry.Status == QuestStatus.ReadyToTurnIn)
                    && startedInHomecoming.TryGetValue(entry.Quest, out int homecoming) && homecoming == GameSession.Home.HeldQuestsHomecoming)
                {
                    underWay[(int)entry.Quest.Rank] = true;
                }
            }

            underWayKnown = true;
        }

        return (int)rank < underWay.Length && underWay[(int)rank];
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
            reason = NotYetReason(quest);
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
        waiting.Clear();
        startedInHomecoming.Clear();
        underWayKnown = false;
        heldSpacedUntil = float.NegativeInfinity;
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
        underWayKnown = false; // a loaded game's quests came back without starting events
        HoldAllHeld(Time.time);
        // Quests begin once there is a player to receive them, not in the menu.
        if (Player.Current != null)
        {
            // Rewards waiting for home are paid at home: a guest back in the host's Haven after a raid lost what didn't fit (the
            // overflow fell into the host's world or was thrown away). They stay saved until the player's own Haven.
            IReadOnlyList<QuestDefinition> waiting = AtHome ? (IReadOnlyList<QuestDefinition>)Log.TakeWaitingRewards() : Array.Empty<QuestDefinition>();
            foreach (QuestDefinition quest in waiting)
            {
                string rewards = GiveRewards(quest);
                GameMessages.Post($"Rewards for {quest.DisplayName}: {rewards}", MessageKind.Gain);
            }

            StartAvailableQuests();
            StartQuestsWhoseEventHappened();
            // A frame on: this runs before the scene's Start, where saved objects get their state back (a building its tier).
            StartCoroutine(RecountOwnedNextFrame());
        }

        RaiseChanged();
    }

    private System.Collections.IEnumerator RecountOwnedNextFrame()
    {
        yield return null;
        if (Player.Current != null)
        {
            RecountOwned();
        }
    }

    /// <summary>
    /// Counts again, on arriving anywhere, what Count Already Owned objectives can see now and couldn't when they started: a
    /// quest begun on a raid (a rescue finished there) or in a friend's Haven saw none of the player's own buildings, stash or
    /// rank. Only ever adds, up to what is there.
    /// </summary>
    internal void RecountOwned()
    {
        foreach (QuestDefinition quest in Quests)
        {
            QuestLog.Entry entry = quest != null ? Log.Get(quest) : null;
            if (entry == null || entry.Status != QuestStatus.Active)
            {
                continue;
            }

            for (int i = 0; i < quest.Objectives.Count && i < entry.Counts.Length; i++)
            {
                if (!entry.IsObjectiveOpen(i) || entry.IsObjectiveDone(i))
                {
                    continue;
                }

                int more = Mathf.Min(quest.Objectives[i].Owned(Player.Current, Log), quest.Objectives[i].Amount) - entry.Counts[i];
                if (more > 0)
                {
                    Log.AddProgress(quest, i, more);
                }
            }
        }
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

            // Co-op: in a friend's Haven a Home Only event starts nothing (Start When's deed has its type's rule in Matches).
            bool starts = quest.Start == QuestStart.OnQuestEvent
                ? gameEvent.Kind == GameEventKind.Custom && quest.StartEvent == gameEvent.Subject && !(HavenVisit.InHostsHaven && quest.StartEvent.HomeOnly)
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
        underWayKnown = false;
        startedInHomecoming[quest] = GameSession.Home.HeldQuestsHomecoming;
        // What it introduces is learned now, announced across the screen (BigNotice): one building per quest.
        BuildingGate.TeachOnStart(quest, Player.Current != null ? Player.Current.gameObject : null);
        BuildingModules.AnnounceLearned(quest.UnlocksModules);
        if (quest.AnnounceStart)
            GameMessages.Post(QuestMarkerSettings.NewQuestText(quest), MessageKind.Info);
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
        underWayKnown = false;
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
        underWayKnown = false;
        Pacing.QuestCompleted(Time.time);
        SpaceWaitingFrom(Time.time);
        string rewards;
        // Once a raid is decided, what the player takes home is already settled, so rewards wait until they arrive.
        if (Place.RaidDecided && quest.DescribeRewards().Length > 0)
        {
            log.HoldRewards(quest);
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
        // As every reward arrives (ItemDelivery): the bag, then the stash at home, then the ground at the player's feet. The rewards of a
        // quest done on a raid are paid on coming home, into a bag full of the raid's loot, and were lost there.
        bool atHome = Place.AtHome;
        foreach (ItemAmount reward in quest.RewardItems)
        {
            if (reward.Item != null && reward.Amount > 0
                && ItemDelivery.Give(bag, new ItemStack(reward.Item, reward.Amount), newFind: true, toStash: atHome, player.transform, ArrivalCause.Rewarded).Fell)
            {
                GameMessages.Post($"No room for all the {reward.Item.DisplayName} from the quest: it lies at your feet.", MessageKind.Warning);
            }
        }

        foreach (ResourceAmount reward in quest.RewardResources)
        {
            int carried = reward.Resource != null ? storage.Add(reward.Resource, reward.Amount) : reward.Amount;
            int stored = carried < reward.Amount && atHome ? GameSession.Stash.Resources.Add(reward.Resource, reward.Amount - carried) : 0;
            if (carried + stored < reward.Amount)
            {
                GameMessages.Post($"Can't carry all the {reward.Resource.DisplayName} from the quest.", MessageKind.Warning);
            }
        }

        if (quest.RewardRecipes.Count > 0 && player.Crafting != null)
        {
            player.Crafting.Unlock(quest.RewardRecipes);
        }

        foreach (EnchantmentDefinition enchantment in quest.RewardEnchantments)
        {
            Inscriptions.Learn(enchantment, player.gameObject, "quest");
        }

        // In the player's own Haven: while visiting (in the friend's Haven or on their raid) the settlement here is the host's, and the
        // work went nowhere (or to the host's copy of the person).
        if (quest.RewardWorkFor != null && quest.RewardWork > 0)
        {
            void AddWork(SettlementState settlement)
            {
                ResidentRecord worker = settlement.Find(quest.RewardWorkFor);
                if (worker != null)
                {
                    worker.WorkDone += quest.RewardWork;
                }
            }

            AddWork(GameSession.Home);
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

    /// <summary>Why a quest can't be taken yet, for its giver: what must be finished first, and what only has to have begun (Started
    /// First, which QuestLog.PrerequisitesMet asks too: without it the reason read "finish  first", naming nothing).</summary>
    public string NotYetReason(QuestDefinition quest)
    {
        var begun = new List<string>();
        foreach (QuestDefinition first in quest.StartedFirst)
        {
            if (first != null && Log.StatusOf(first) == QuestStatus.NotStarted)
            {
                begun.Add($"\"{first.DisplayName}\"");
            }
        }

        string finish = DescribeMissing(quest);
        string take = string.Join(" and ", begun);
        return finish.Length > 0 && take.Length > 0 ? $"Not yet: take up {take} and finish {finish} first."
            : finish.Length > 0 ? $"Not yet: finish {finish} first."
            : take.Length > 0 ? $"Not yet: take up {take} first."
            : "Not yet.";
    }
}
