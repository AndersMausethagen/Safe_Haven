using System;
using System.Collections.Generic;

public enum QuestStatus
{
    NotStarted = 0,
    Active = 1,
    Completed = 2,
    /// <summary>Every objective is done; the quest waits to be handed in at a Quest Giver (Turn In To Giver).</summary>
    ReadyToTurnIn = 3,
    /// <summary>The story moved on while it was under way (QuestDefinition.ClosedBy): over, without its rewards.</summary>
    Lapsed = 4,
}

/// <summary>
/// One player's quests: which are active or done and how far each objective has come. It counts game events against
/// the active quests' objectives and completes quests when every objective is met. Plain C#, so it can be tested and
/// saved without a scene; it lives in GameSession while playing and is copied to save files by SaveMapper. Starting
/// quests at the right time and handing out rewards is the QuestManager's job.
/// </summary>
public sealed class QuestLog
{
    public sealed class Entry
    {
        public Entry(QuestDefinition quest)
        {
            Quest = quest;
            Counts = new int[quest.Objectives.Count];
        }

        public QuestDefinition Quest { get; }
        public QuestStatus Status { get; internal set; }
        public int[] Counts { get; internal set; }

        /// <summary>Completed after a raid was decided: its rewards wait to be given at home (saved, so a crash on the results screen
        /// doesn't lose them).</summary>
        public bool RewardsWaiting { get; internal set; }

        public bool IsObjectiveDone(int index) => index >= 0 && index < Counts.Length && Counts[index] >= Quest.Objectives[index].Amount;

        /// <summary>The objectives that count right now: all of them, or only the first unfinished one for ordered quests.</summary>
        public bool IsObjectiveOpen(int index)
        {
            if (IsObjectiveDone(index))
            {
                return false;
            }

            if (!Quest.ObjectivesInOrder)
            {
                return true;
            }

            for (int i = 0; i < index; i++)
            {
                if (!IsObjectiveDone(i))
                {
                    return false;
                }
            }

            return true;
        }

        public bool AllObjectivesDone
        {
            get
            {
                for (int i = 0; i < Counts.Length; i++)
                {
                    if (!IsObjectiveDone(i))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }

    private readonly Dictionary<QuestDefinition, Entry> entries = new Dictionary<QuestDefinition, Entry>();
    private readonly HashSet<string> happenedEvents = new HashSet<string>();

    /// <summary>Every quest that has started, in no particular order.</summary>
    public IEnumerable<Entry> Entries => entries.Values;

    /// <summary>Ids of every Quest Event raised in this game, and the keys of the player's deeds (QuestDeeds: talked to,
    /// reached, used, freed, brought home), so story moments and deeds are remembered across saves.</summary>
    public IEnumerable<string> HappenedEventIds => happenedEvents;

    /// <summary>True when this Quest Event has been raised at any point in this game (it is saved).</summary>
    public bool HasHappened(GameEventDefinition gameEvent)
    {
        return gameEvent != null && happenedEvents.Contains(gameEvent.Id);
    }

    /// <summary>True when the player has done this deed at any point in this game (QuestDeeds key; it is saved).</summary>
    public bool HasDone(string deedKey)
    {
        return !string.IsNullOrEmpty(deedKey) && happenedEvents.Contains(deedKey);
    }

    // A building the player took down takes one back from what an open Build objective for it has counted, so building, taking down and
    // building again isn't two (the review of 2026-11-21). A step already done stays done.
    private void TakeBackBuilt(in GameEvent gameEvent)
    {
        if (gameEvent.Kind != GameEventKind.TakenDown || !(gameEvent.Subject is BuildingDefinition building) || gameEvent.Amount <= 0)
        {
            return;
        }

        foreach (Entry entry in entries.Values)
        {
            if (entry.Status != QuestStatus.Active)
            {
                continue;
            }

            for (int i = 0; i < entry.Counts.Length; i++)
            {
                QuestObjective objective = entry.Quest.Objectives[i];
                if (objective.Type == ObjectiveType.BuildStructure && objective.Building == building && entry.IsObjectiveOpen(i) && entry.Counts[i] > 0)
                {
                    entry.Counts[i] = Math.Max(0, entry.Counts[i] - gameEvent.Amount);
                    Progressed?.Invoke(entry.Quest, i);
                }
            }
        }
    }

    /// <summary>Remembers a Quest Event by id; used when loading a save.</summary>
    public void RestoreHappened(string eventId)
    {
        if (!string.IsNullOrEmpty(eventId))
        {
            happenedEvents.Add(eventId);
            string deed = QuestDeeds.FromLegacy(eventId);
            if (deed != null)
            {
                happenedEvents.Add(deed);
            }
        }
    }

    public event Action<QuestDefinition> Started;
    /// <summary>Raised when an objective's count changes. Arguments: quest, objective index.</summary>
    public event Action<QuestDefinition, int> Progressed;
    /// <summary>Raised when a Turn In To Giver quest has every objective done and waits to be handed in.</summary>
    public event Action<QuestDefinition> ReadyToTurnIn;
    public event Action<QuestDefinition> Completed;
    /// <summary>Raised when a quest under way lapses because the story moved on.</summary>
    public event Action<QuestDefinition> Lapsed;

    /// <summary>True when a quest under way still asks for <paramref name="building"/> to be built (an open Build Structure step): the
    /// build menu lights its icon (the user, 2026-11-25).</summary>
    public bool WantsBuilt(BuildingDefinition building)
    {
        if (building == null)
        {
            return false;
        }

        foreach (Entry entry in entries.Values)
        {
            if (entry.Status != QuestStatus.Active)
            {
                continue;
            }

            for (int i = 0; i < entry.Counts.Length; i++)
            {
                QuestObjective objective = entry.Quest.Objectives[i];
                if (objective.Type == ObjectiveType.BuildStructure && objective.Building == building && entry.IsObjectiveOpen(i))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public QuestStatus StatusOf(QuestDefinition quest)
    {
        return quest != null && entries.TryGetValue(quest, out Entry entry) ? entry.Status : QuestStatus.NotStarted;
    }

    public Entry Get(QuestDefinition quest)
    {
        return quest != null && entries.TryGetValue(quest, out Entry entry) ? entry : null;
    }

    public bool PrerequisitesMet(QuestDefinition quest)
    {
        foreach (QuestDefinition prerequisite in quest.Prerequisites)
        {
            if (prerequisite != null && StatusOf(prerequisite) != QuestStatus.Completed)
            {
                return false;
            }
        }

        foreach (QuestDefinition begun in quest.StartedFirst)
        {
            if (begun != null && StatusOf(begun) == QuestStatus.NotStarted)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>True once the quest that closes this one has started: an optional quest is no longer offered then.</summary>
    public bool IsClosed(QuestDefinition quest) =>
        quest != null && quest.ClosedBy != null && StatusOf(quest.ClosedBy) != QuestStatus.NotStarted;

    /// <summary>Starts the quest if it hasn't started, its prerequisites are done and it isn't closed. Returns true when it started.</summary>
    public bool TryStart(QuestDefinition quest) => TryStart(quest, false);

    /// <summary>
    /// The same, with its prerequisites set aside when <paramref name="prerequisitesAside"/>: a rescued resident's first quest starts at
    /// the rescue whatever stands before it (ResidentQuests).
    /// </summary>
    public bool TryStart(QuestDefinition quest, bool prerequisitesAside)
    {
        if (quest == null || StatusOf(quest) != QuestStatus.NotStarted || (!prerequisitesAside && !PrerequisitesMet(quest)) || IsClosed(quest))
        {
            return false;
        }

        var entry = new Entry(quest) { Status = QuestStatus.Active };
        entries[quest] = entry;
        Started?.Invoke(quest);
        CompleteIfDone(entry);
        return true;
    }

    /// <summary>Counts an event against every active quest. Quests whose objectives are all met are completed. Quest Events are remembered.</summary>
    public void Handle(in GameEvent gameEvent)
    {
        // Co-op: in a friend's Haven, what belongs to a Haven isn't remembered either, or it would count once home
        // (QuestObjective.CountsInAFriendsHaven; a Story Event's Home Only).
        bool friends = HavenVisit.InHostsHaven;
        if (gameEvent.Kind == GameEventKind.Custom && gameEvent.Subject is GameEventDefinition custom && !(friends && custom.HomeOnly))
        {
            happenedEvents.Add(custom.Id);
        }

        string deed = gameEvent.ForPlayerSide && (!friends || QuestDeeds.CountsInAFriendsHaven(gameEvent.Kind)) ? QuestDeeds.KeyOf(gameEvent) : null;
        if (deed != null)
        {
            happenedEvents.Add(deed);
        }

        // Taking down is refused in a friend's Haven, so this is the player's own.
        if (!friends)
        {
            TakeBackBuilt(gameEvent);
        }

        // Copied first: completing a quest can start others, which changes the collection.
        foreach (Entry entry in new List<Entry>(entries.Values))
        {
            if (entry.Status != QuestStatus.Active)
            {
                continue;
            }

            for (int i = 0; i < entry.Counts.Length; i++)
            {
                if (entry.IsObjectiveOpen(i))
                {
                    int amount = entry.Quest.Objectives[i].Progress(gameEvent);
                    if (amount > 0)
                    {
                        AddProgress(entry, i, amount);
                    }
                }
            }

            CompleteIfDone(entry);
        }
    }

    /// <summary>Adds to one objective directly, for objectives that count what the player already owns.</summary>
    public void AddProgress(QuestDefinition quest, int objective, int amount)
    {
        Entry entry = Get(quest);
        if (entry != null && entry.Status == QuestStatus.Active && amount > 0 && objective >= 0 && objective < entry.Counts.Length)
        {
            AddProgress(entry, objective, amount);
            CompleteIfDone(entry);
        }
    }

    /// <summary>Hands in a quest waiting at its giver, completing it. Returns true when it completed.</summary>
    public bool TryTurnIn(QuestDefinition quest)
    {
        Entry entry = Get(quest);
        if (entry == null || entry.Status != QuestStatus.ReadyToTurnIn)
        {
            return false;
        }

        Complete(entry);
        return true;
    }

    /// <summary>Ends a quest under way without its rewards, because the story moved on. Returns true when it lapsed.</summary>
    public bool TryLapse(QuestDefinition quest)
    {
        Entry entry = Get(quest);
        if (entry == null || entry.Status != QuestStatus.Active)
        {
            return false;
        }

        entry.Status = QuestStatus.Lapsed;
        Lapsed?.Invoke(quest);
        return true;
    }

    /// <summary>
    /// Marks the quest complete regardless of its objectives, for debugging and scripted moments. Skips the turn-in.
    /// </summary>
    public void ForceComplete(QuestDefinition quest)
    {
        if (StatusOf(quest) == QuestStatus.NotStarted)
        {
            entries[quest] = new Entry(quest) { Status = QuestStatus.Active };
        }

        Entry entry = entries[quest];
        if (entry.Status == QuestStatus.Completed)
        {
            return;
        }

        for (int i = 0; i < entry.Counts.Length; i++)
        {
            entry.Counts[i] = quest.Objectives[i].Amount;
        }

        Complete(entry);
    }

    /// <summary>Its rewards wait for the player to be home (QuestManager pays them with <see cref="TakeWaitingRewards"/>).</summary>
    public void HoldRewards(QuestDefinition quest)
    {
        if (quest != null && entries.TryGetValue(quest, out Entry entry))
        {
            entry.RewardsWaiting = true;
        }
    }

    /// <summary>The quests whose rewards wait, no longer waiting once taken.</summary>
    public List<QuestDefinition> TakeWaitingRewards()
    {
        var waiting = new List<QuestDefinition>();
        foreach (Entry entry in entries.Values)
        {
            if (entry.RewardsWaiting)
            {
                entry.RewardsWaiting = false;
                waiting.Add(entry.Quest);
            }
        }

        return waiting;
    }

    /// <summary>Puts back saved progress without raising any events.</summary>
    public void Restore(QuestDefinition quest, QuestStatus status, IReadOnlyList<int> counts, bool rewardsWaiting = false)
    {
        if (quest == null || status == QuestStatus.NotStarted)
        {
            return;
        }

        var entry = new Entry(quest) { Status = status, RewardsWaiting = rewardsWaiting && status == QuestStatus.Completed };
        // Saved before the quest's steps changed (a step added or taken out): counts kept by place would land on the wrong
        // steps (review 2026-11-16: Smith's Request's new "Raise the Smithy"). They start again from nothing instead; what
        // is already owned or built is counted again once the scene is in (QuestManager.RecountOwned).
        // Unless steps were only taken off its end (Former Step Count: A Horse of Your Own's Stable, 2026-11-27): the first ones are
        // the same steps, so their counts stay.
        bool trimmed = counts != null && quest.FormerStepCount == counts.Count && counts.Count > entry.Counts.Length;
        bool stepsChanged = counts != null && counts.Count != entry.Counts.Length && status != QuestStatus.Completed && !trimmed;
        for (int i = 0; !stepsChanged && i < entry.Counts.Length && counts != null && i < counts.Count; i++)
        {
            entry.Counts[i] = Math.Max(0, counts[i]);
        }

        entries[quest] = entry;
    }

    private void AddProgress(Entry entry, int objective, int amount)
    {
        int needed = entry.Quest.Objectives[objective].Amount;
        int before = entry.Counts[objective];
        entry.Counts[objective] = Math.Min(needed, before + amount);
        if (entry.Counts[objective] != before)
        {
            Progressed?.Invoke(entry.Quest, objective);
        }
    }

    private void CompleteIfDone(Entry entry)
    {
        if (entry.Status != QuestStatus.Active || !entry.AllObjectivesDone)
        {
            return;
        }

        if (entry.Quest.TurnInToGiver)
        {
            entry.Status = QuestStatus.ReadyToTurnIn;
            ReadyToTurnIn?.Invoke(entry.Quest);
        }
        else
        {
            Complete(entry);
        }
    }

    private void Complete(Entry entry)
    {
        // Marked first, so anything reacting to the completion sees it as done.
        entry.Status = QuestStatus.Completed;
        Completed?.Invoke(entry.Quest);
    }
}
