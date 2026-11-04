using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Reads the game into the journal's tabs, as plain shelves for a HavenListPage to draw. Nothing here touches the screen, and nothing is shown that the
/// player hasn't come to: pages wait for their quest, creature, person or find, and a creature gives up what it is a little at a time, as more of them fall.
/// </summary>
public static class JournalContent
{
    /// <summary>The name a creature not met yet goes by, and the first letter its row shows.</summary>
    public const string UnknownName = "???";
    /// <summary>What marks a row not read yet.</summary>
    public const string UnreadMark = "•";

    private const string PlainStrike = "Monster Strike";

    public static List<HavenShelf> Backstory(JournalSettings settings, QuestLog log, CharacterProgress progress, out string summary)
    {
        var shelves = new List<HavenShelf>();
        int known = 0;
        foreach (JournalPage page in settings.Backstory)
        {
            if (!IsUnlocked(page, log, progress))
            {
                continue;
            }

            known++;
            ShelfFor(shelves, page.group).Entries.Add(EntryOf(page, progress));
        }

        summary = $"{known} of {settings.Backstory.Count} pages written";
        return shelves;
    }

    public static List<HavenShelf> Mechanics(JournalSettings settings, QuestLog log, CharacterProgress progress, out string summary)
    {
        var shelves = new List<HavenShelf>();
        int known = 0;
        foreach (JournalPage page in settings.Mechanics)
        {
            if (IsUnlocked(page, log, progress))
            {
                known++;
                ShelfFor(shelves, page.group).Entries.Add(EntryOf(page, progress));
            }
        }

        summary = known < settings.Mechanics.Count ? "How things work, as far as you've come" : "How things work";
        return shelves;
    }

    public static List<HavenShelf> Quests(QuestLog log, CharacterProgress progress, IReadOnlyList<QuestDefinition> allQuests, System.Action<QuestDefinition> pin, out string summary)
    {
        var underWay = new HavenShelf("Under way");
        var done = new HavenShelf("Done");
        var letGo = new HavenShelf("The story moved on");
        foreach (QuestLog.Entry entry in log.Entries)
        {
            if (entry.Status == QuestStatus.NotStarted)
            {
                continue;
            }

            HavenEntry shown = EntryOf(entry, log, progress, allQuests);
            bool open = entry.Status == QuestStatus.Active || entry.Status == QuestStatus.ReadyToTurnIn;
            if (open && pin != null)
            {
                bool pinned = progress.Journal.PinnedQuest == entry.Quest.Id;
                shown.Actions.Add(new HavenAction(pinned ? "Unpin from the tracker" : "Pin to the top of the tracker", () => pin(pinned ? null : entry.Quest), true, false, false));
                AddGoThere(shown, entry.Quest, log);
            }

            (entry.Status == QuestStatus.Completed ? done : entry.Status == QuestStatus.Lapsed ? letGo : underWay).Entries.Add(shown);
        }

        // The newest thing done is the one most likely to be looked up.
        done.Entries.Reverse();
        summary = $"{underWay.Entries.Count} under way, {done.Entries.Count} done";
        return new List<HavenShelf> { underWay, done, letGo };
    }

    // Only at home, where walking there is what going there means; a raid's ways are the raid's own.
    private static void AddGoThere(HavenEntry shown, QuestDefinition quest, QuestLog log)
    {
        if (HomePortal.Current == null)
        {
            return;
        }

        foreach (QuestWaypoint waypoint in QuestWaypoint.Active)
        {
            if (waypoint != null && waypoint.Quest == quest && waypoint.IsWanted(log))
            {
                HavenTravel.AddGoTo(shown, waypoint.transform);
                return;
            }
        }
    }

    public static List<HavenShelf> Creatures(JournalSettings settings, CharacterProgress progress, out string summary)
    {
        var met = new HavenShelf("Met");
        var unknown = new HavenShelf("Not met yet");
        foreach (MonsterDefinition creature in settings.Creatures)
        {
            if (creature == null)
            {
                continue;
            }

            if (progress.HasMet(creature.Id))
            {
                met.Entries.Add(EntryOf(creature, settings, progress));
            }
            else
            {
                unknown.Entries.Add(new HavenEntry
                {
                    Key = creature,
                    Name = UnknownName,
                    Line = "Not met yet",
                    State = HavenEntryState.Locked,
                    Status = "Not met yet",
                    Description = "Nothing is written here yet. Whatever it is, you haven't met it and lived to write it down.",
                });
            }
        }

        summary = $"{met.Entries.Count} of {met.Entries.Count + unknown.Entries.Count} met";
        return new List<HavenShelf> { met, unknown };
    }

    public static List<HavenShelf> Raids(CharacterProgress progress, out string summary)
    {
        IReadOnlyList<RaidRecord> raids = progress.Journal.Raids;
        var shelf = new HavenShelf("Newest first");
        for (int i = raids.Count - 1; i >= 0; i--)
        {
            shelf.Entries.Add(EntryOf(raids[i], i + 1));
        }

        int home = raids.Count(r => r.Outcome == "Extracted!");
        summary = raids.Count == 0 ? "None yet" : $"{raids.Count} set out, {home} came home";
        return new List<HavenShelf> { shelf };
    }

    private static HavenEntry EntryOf(RaidRecord raid, int number)
    {
        bool home = raid.Outcome == "Extracted!";
        string length = RaidStats.FormatTime(raid.Seconds);
        var text = new StringBuilder();
        if (!string.IsNullOrEmpty(raid.BroughtDownBy))
        {
            text.Append($"Brought down by a {raid.BroughtDownBy}.\n\n");
        }

        foreach (string line in raid.Lines)
        {
            text.Append(line).Append('\n');
        }

        if (text.Length == 0)
        {
            text.Append(home ? "A quiet one. You went, you took what you could and you came back." : "Nothing worth writing down, besides the ending.");
        }

        return new HavenEntry
        {
            Key = $"raid:{number}",
            Name = $"{number}. {raid.Raid}",
            Line = $"{raid.Outcome.TrimEnd('!')}, {length}",
            Badge = "",
            State = home ? HavenEntryState.Good : HavenEntryState.Attention,
            Status = home ? $"{raid.Outcome.TrimEnd('!')} after {length}, with a haul worth {raid.Value}" : $"{raid.Outcome} after {length}, losing {raid.Value} in value",
            Description = text.ToString().TrimEnd(),
        };
    }

    public static bool IsUnlocked(JournalPage page, QuestLog log, CharacterProgress progress)
    {
        if (page.unlockedByQuest != null && log.StatusOf(page.unlockedByQuest) == QuestStatus.NotStarted)
        {
            return false;
        }

        if (page.unlockedByCreature != null && !progress.HasMet(page.unlockedByCreature.Id))
        {
            return false;
        }

        if (page.unlockedByResident != null && !IsHome(page.unlockedByResident))
        {
            return false;
        }

        return !page.foundInRaids || progress.Journal.HasFound(page.title);
    }

    /// <summary>
    /// How much the journal can say of a creature: 0 only that it has been met, 1 how it fights and what it leaves (one has fallen), 2 how to live through it
    /// (enough have fallen, or one has killed the player), 3 what is told of it (twice that, or the first for what is only ever fought once or twice).
    /// </summary>
    public static int KnowledgeOf(MonsterDefinition creature, CharacterProgress progress)
    {
        int slain = progress.TimesSlain(creature.Id);
        int enough = creature.JournalKillsToKnow;
        if (slain >= (enough <= 1 ? 1 : enough * 2))
        {
            return 3;
        }

        if (slain >= enough || progress.Journal.WasKilledBy(creature.Id))
        {
            return 2;
        }

        return slain >= 1 ? 1 : 0;
    }

    private static bool IsHome(ResidentDefinition who)
    {
        foreach (ResidentRecord record in GameSession.Settlement.Residents)
        {
            if (record.Resident == who)
            {
                return true;
            }
        }

        return false;
    }

    private static HavenShelf ShelfFor(List<HavenShelf> shelves, string title)
    {
        string wanted = string.IsNullOrWhiteSpace(title) ? "Notes" : title;
        foreach (HavenShelf shelf in shelves)
        {
            if (shelf.Title == wanted)
            {
                return shelf;
            }
        }

        var added = new HavenShelf(wanted);
        shelves.Add(added);
        return added;
    }

    private static void MarkUnread(HavenEntry entry, CharacterProgress progress, string key)
    {
        entry.ReadKey = key;
        if (!progress.Journal.HasRead(key))
        {
            entry.Badge = UnreadMark;
            entry.State = HavenEntryState.Ready;
        }
    }

    private static HavenEntry EntryOf(JournalPage page, CharacterProgress progress)
    {
        var entry = new HavenEntry
        {
            Key = page.title,
            Name = page.title,
            Line = page.line,
            Status = page.line,
            State = HavenEntryState.Good,
            Description = GameInput.WithRebinds(page.body),
        };
        MarkUnread(entry, progress, "page:" + page.title);
        entry.Status = page.line;
        return entry;
    }

    private static HavenEntry EntryOf(QuestLog.Entry entry, QuestLog log, CharacterProgress progress, IReadOnlyList<QuestDefinition> allQuests)
    {
        QuestDefinition quest = entry.Quest;
        var shown = new HavenEntry
        {
            Key = quest,
            Name = quest.DisplayName,
            Line = ChainLine(quest, log, allQuests),
            State = entry.Status == QuestStatus.ReadyToTurnIn ? HavenEntryState.Ready
                : entry.Status == QuestStatus.Active ? HavenEntryState.Working
                : HavenEntryState.Good,
            Status = StatusOf(entry, progress),
            Description = GameInput.WithRebinds(quest.Description),
        };

        if (quest.Objectives.Count > 0)
        {
            shown.Details.Add(new HavenDetail("Objectives", ObjectivesOf(entry)));
        }

        if (!string.IsNullOrWhiteSpace(quest.JournalNotes))
        {
            shown.Details.Add(new HavenDetail("What you know", GameInput.WithRebinds(quest.JournalNotes)));
        }

        string rewards = quest.DescribeRewards();
        if (!string.IsNullOrWhiteSpace(rewards))
        {
            shown.Details.Add(new HavenDetail("Reward", rewards));
        }

        return shown;
    }

    // Where it stands in its chain: how far along that thread of the story is.
    private static string ChainLine(QuestDefinition quest, QuestLog log, IReadOnlyList<QuestDefinition> allQuests)
    {
        if (string.IsNullOrWhiteSpace(quest.Chain))
        {
            return "";
        }

        int total = 0;
        int done = 0;
        if (allQuests != null)
        {
            foreach (QuestDefinition other in allQuests)
            {
                if (other != null && other.Chain == quest.Chain)
                {
                    total++;
                    done += log.StatusOf(other) == QuestStatus.Completed ? 1 : 0;
                }
            }
        }

        return total > 1 ? $"{quest.Chain}, {done} of {total}" : quest.Chain;
    }

    private static string StatusOf(QuestLog.Entry entry, CharacterProgress progress)
    {
        string pinned = progress.Journal.PinnedQuest == entry.Quest.Id ? ", pinned" : "";
        switch (entry.Status)
        {
            case QuestStatus.ReadyToTurnIn:
                return $"Done. Speak to {entry.Quest.TurnInTo}{pinned}.";
            case QuestStatus.Completed:
                return "Done";
            case QuestStatus.Lapsed:
                return "The story moved on without it";
            default:
                return (entry.Quest.TurnInToGiver ? $"Under way, then back to {entry.Quest.TurnInTo}" : "Under way") + pinned;
        }
    }

    private static string ObjectivesOf(QuestLog.Entry entry)
    {
        UIStyle style = UIStyle.Current;
        string good = UIStyle.Hex(style.Good);
        string muted = UIStyle.Hex(style.Muted);
        var text = new StringBuilder();
        IReadOnlyList<QuestObjective> objectives = entry.Quest.Objectives;
        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjective objective = objectives[i];
            string described = GameInput.WithRebinds(objective.Describe());
            int count = i < entry.Counts.Length ? entry.Counts[i] : 0;
            string tally = objective.Amount > 1 ? $"  {UnityEngine.Mathf.Min(count, objective.Amount)}/{objective.Amount}" : "";
            if (entry.IsObjectiveDone(i))
            {
                text.Append($"<color={good}>• {described}{tally}</color>\n");
            }
            else if (entry.Status == QuestStatus.Active && !entry.IsObjectiveOpen(i))
            {
                text.Append($"<color={muted}>• {described}{tally}</color>\n");
            }
            else
            {
                text.Append($"• {described}{tally}\n");
            }
        }

        return text.ToString().TrimEnd();
    }

    private static HavenEntry EntryOf(MonsterDefinition creature, JournalSettings settings, CharacterProgress progress)
    {
        int slain = progress.TimesSlain(creature.Id);
        int knowledge = KnowledgeOf(creature, progress);
        var shown = new HavenEntry
        {
            Key = creature,
            Name = creature.DisplayName,
            Line = slain > 0 ? $"Slain {slain}" : "Met, not yet slain",
            Icon = creature.Portrait,
            State = HavenEntryState.Good,
            Status = (slain > 0 ? $"You have brought down {slain}. " : "You have not brought one down yet. ") + NextToLearn(creature, knowledge, slain),
            Description = creature.Description,
        };
        MarkUnread(shown, progress, $"creature:{creature.Id}:{knowledge}");

        if (!string.IsNullOrWhiteSpace(creature.JournalHabitat))
        {
            shown.Details.Add(new HavenDetail("Where it is found", creature.JournalHabitat));
        }

        if (knowledge >= 1)
        {
            shown.Details.Add(new HavenDetail("Its ways", WaysOf(creature)));
            string drops = DropsOf(creature);
            if (drops != null)
            {
                shown.Details.Add(new HavenDetail("What it leaves", drops));
            }

            if (creature.AttackSound != null)
            {
                shown.Actions.Add(new HavenAction("Hear its warning", () => AudioManager.Play(creature.AttackSound, AudioManager.EarPosition), true, false, false));
            }
        }

        if (knowledge >= 2 && !string.IsNullOrWhiteSpace(creature.JournalAdvice))
        {
            shown.Details.Add(new HavenDetail("How to live through it", creature.JournalAdvice));
        }

        if (knowledge >= 3 && !string.IsNullOrWhiteSpace(creature.JournalLore))
        {
            shown.Details.Add(new HavenDetail("What is told", creature.JournalLore));
        }

        // What people at home know doesn't wait for the player to have learned it the hard way: it is why it is worth bringing them home.
        foreach (ResidentNote note in settings.ResidentNotes)
        {
            if (note.about == creature && note.who != null && IsHome(note.who))
            {
                shown.Details.Add(new HavenDetail($"{note.who.DisplayName} says", note.text));
            }
        }

        return shown;
    }

    private static string NextToLearn(MonsterDefinition creature, int knowledge, int slain)
    {
        int enough = creature.JournalKillsToKnow;
        switch (knowledge)
        {
            case 0:
                return "Bring one down to learn how it fights.";
            case 1:
                return $"{enough - slain} more to learn how to live through it.";
            case 2:
                int full = enough <= 1 ? 1 : enough * 2;
                return slain < full ? $"{full - slain} more to learn what is told of it." : "";
            default:
                return "";
        }
    }

    // What the creature's own data says, so these lines can't fall out of step with how it fights.
    private static string WaysOf(MonsterDefinition creature)
    {
        var text = new StringBuilder();
        if (creature.CombatStats != null)
        {
            // The plain creature's figure. A raid's tier and an elite's marks both add to it.
            text.Append($"Health {UnityEngine.Mathf.RoundToInt(creature.CombatStats.GetValue(StatType.MaxHealth))}, more in harder raids\n");
        }

        var seen = new HashSet<string>();
        var moves = new List<string>();
        void Add(AttackDefinition attack)
        {
            // The shared plain strike has no name worth writing down.
            if (attack != null && attack.DisplayName != PlainStrike && seen.Add(attack.DisplayName))
            {
                moves.Add(attack.DisplayName);
            }
        }

        Add(creature.Attack);
        foreach (MonsterAttack action in creature.Attacks)
        {
            if (action != null)
            {
                Add(action.attack);
            }
        }

        if (moves.Count > 0)
        {
            text.Append("Attacks: ").Append(string.Join(", ", moves)).Append('\n');
        }

        if (creature.ShieldArc > 0f)
        {
            text.Append("Carries a shield: little gets through from the front.\n");
        }

        if (creature.CanBeFinished)
        {
            text.Append("Person-shaped: staggered, it can be finished from behind.\n");
        }

        if (creature.ParriesBackstabs)
        {
            text.Append("Has the hands to parry a blow it sees coming from behind.\n");
        }

        if (!creature.Bleeds)
        {
            text.Append("Does not bleed.\n");
        }

        return text.ToString().TrimEnd();
    }

    private static string DropsOf(MonsterDefinition creature)
    {
        if (creature.Drops == null || creature.Drops.Entries.Count == 0)
        {
            return null;
        }

        var always = new List<string>();
        var sometimes = new List<string>();
        foreach (ItemDropEntry entry in creature.Drops.Entries)
        {
            if (entry.Drop != null)
            {
                (entry.Probability >= 0.99f ? always : sometimes).Add(entry.DropName);
            }
        }

        var parts = new List<string>();
        if (always.Count > 0)
        {
            parts.Add(string.Join(", ", always));
        }

        if (sometimes.Count > 0)
        {
            parts.Add("sometimes " + string.Join(", ", sometimes));
        }

        return parts.Count > 0 ? string.Join("; ", parts) : null;
    }
}
