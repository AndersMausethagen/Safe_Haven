using System;
using System.Collections.Generic;

public enum QuestStatus
{
    NotStarted = 0,
    Active = 1,
    Completed = 2,
    /// <summary>Every objective is done; the quest waits to be handed in at a Quest Giver (Turn In To Giver).</summary>
    ReadyToTurnIn = 3,
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

    /// <summary>Ids of every Quest Event raised in this game, so story moments are remembered across saves.</summary>
    public IEnumerable<string> HappenedEventIds => happenedEvents;

    /// <summary>True when this Quest Event has been raised at any point in this game (it is saved).</summary>
    public bool HasHappened(GameEventDefinition gameEvent)
    {
        return gameEvent != null && happenedEvents.Contains(gameEvent.Id);
    }

    /// <summary>Remembers a Quest Event by id; used when loading a save.</summary>
    public void RestoreHappened(string eventId)
    {
        if (!string.IsNullOrEmpty(eventId))
        {
            happenedEvents.Add(eventId);
        }
    }

    public event Action<QuestDefinition> Started;
    /// <summary>Raised when an objective's count changes. Arguments: quest, objective index.</summary>
    public event Action<QuestDefinition, int> Progressed;
    /// <summary>Raised when a Turn In To Giver quest has every objective done and waits to be handed in.</summary>
    public event Action<QuestDefinition> ReadyToTurnIn;
    public event Action<QuestDefinition> Completed;

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

        return true;
    }

    /// <summary>Starts the quest if it hasn't started and its prerequisites are done. Returns true when it started.</summary>
    public bool TryStart(QuestDefinition quest)
    {
        if (quest == null || StatusOf(quest) != QuestStatus.NotStarted || !PrerequisitesMet(quest))
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
        if (gameEvent.Kind == GameEventKind.Custom && gameEvent.Subject is GameEventDefinition custom)
        {
            happenedEvents.Add(custom.Id);
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

    /// <summary>Puts back saved progress without raising any events.</summary>
    public void Restore(QuestDefinition quest, QuestStatus status, IReadOnlyList<int> counts)
    {
        if (quest == null || status == QuestStatus.NotStarted)
        {
            return;
        }

        var entry = new Entry(quest) { Status = status };
        for (int i = 0; i < entry.Counts.Length && counts != null && i < counts.Count; i++)
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
