using System.Collections.Generic;
using UnityEngine;

/// <summary>What happened on a raid beside the player, for the marks it may leave (ResidentMarks.AfterRaid).</summary>
public struct RaidFacts
{
    public bool Extracted;
    public int TimesDowned;
    public int BossesBeaten;
    /// <summary>Foes they felled, by the creature's id.</summary>
    public IReadOnlyDictionary<string, int> KillsBy;
}

/// <summary>
/// The marks raids leave on people (Docs/PeopleResearch_2026-11-23.md §3.6; Darkest Dungeon's quirks without death): what may come of a
/// raid, at most three good and three hard (a new one puts out the oldest of its kind), and each hard mark's way back (an item given in
/// conversation, a Friend's scene, deeds that turn a fear into its opposite). The forest's own mark comes with being found again and
/// stays until every memory is back. The rules, plain and testable (PeopleTests); the marks are PeopleSettings › Marks.
/// </summary>
public static class ResidentMarks
{
    private static Dictionary<string, MarkDefinition> byId;
    private static PeopleSettings indexed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        byId = null;
        indexed = null;
    }

    /// <summary>The mark with this id, or null.</summary>
    public static MarkDefinition Find(string id)
    {
        PeopleSettings settings = PeopleSettings.Current;
        if (byId == null || indexed != settings)
        {
            byId = new Dictionary<string, MarkDefinition>();
            indexed = settings;
            foreach (MarkDefinition mark in settings.Marks)
            {
                if (mark != null)
                {
                    byId[mark.Id] = mark;
                }
            }
        }

        return id != null && byId.TryGetValue(id, out MarkDefinition found) ? found : null;
    }

    /// <summary>Their marks, oldest first.</summary>
    public static List<MarkDefinition> Of(ResidentRecord record)
    {
        var marks = new List<MarkDefinition>();
        if (record == null)
        {
            return marks;
        }

        foreach (string id in record.Marks)
        {
            MarkDefinition mark = Find(id);
            if (mark != null)
            {
                marks.Add(mark);
            }
        }

        return marks;
    }

    /// <summary>Gives them <paramref name="mark"/> (not twice): beyond Most Marks of its kind the oldest of that kind goes. Counts deeds
    /// toward its turning from now. False when they had it.</summary>
    public static bool Give(ResidentRecord record, MarkDefinition mark)
    {
        if (record == null || mark == null || record.HasMark(mark.Id))
        {
            return false;
        }

        int most = PeopleSettings.Current.MostMarks;
        var sameKind = new List<MarkDefinition>();
        foreach (MarkDefinition had in Of(record))
        {
            if (had.Good == mark.Good)
            {
                sameKind.Add(had);
            }
        }

        for (int i = 0; i <= sameKind.Count - most && i < sameKind.Count; i++)
        {
            record.RemoveMark(sameKind[i].Id);
        }

        record.AddMark(mark.Id);
        if (mark.TurnsInto != null && !string.IsNullOrEmpty(mark.Creature))
        {
            record.ForgetDeed(SinceKey(mark));
        }

        return true;
    }

    /// <summary>
    /// A raid come home from beside the player: foes felled are counted by kind, fears turn into their opposites once enough of what
    /// they feared has fallen, and each mark whose cause happened may come at its chance (<paramref name="roll"/> 0-1 each). Returns
    /// what came and what turned, for the homecoming's words.
    /// </summary>
    public static List<MarkDefinition> AfterRaid(ResidentRecord record, RaidFacts facts, System.Func<float> roll)
    {
        var came = new List<MarkDefinition>();
        if (record == null)
        {
            return came;
        }

        if (facts.KillsBy != null)
        {
            foreach (KeyValuePair<string, int> kill in facts.KillsBy)
            {
                record.AddDeed(KillKey(kill.Key), kill.Value);
                foreach (MarkDefinition mark in Of(record))
                {
                    if (mark.TurnsInto != null && Matches(mark.Creature, kill.Key))
                    {
                        record.AddDeed(SinceKey(mark), kill.Value);
                    }
                }
            }
        }

        // Fears faced down: the mark turns into what it says (Afraid of Widows → Spider-bane).
        foreach (MarkDefinition mark in Of(record))
        {
            if (mark.TurnsInto != null && mark.Count > 0 && record.DeedOf(SinceKey(mark)) >= mark.Count)
            {
                record.RemoveMark(mark.Id);
                record.ForgetDeed(SinceKey(mark));
                if (Give(record, mark.TurnsInto))
                {
                    came.Add(mark.TurnsInto);
                }
            }
        }

        foreach (MarkDefinition mark in PeopleSettings.Current.Marks)
        {
            if (mark == null || record.HasMark(mark.Id) || !CauseHappened(record, mark, facts))
            {
                continue;
            }

            if (roll() < mark.Chance && Give(record, mark))
            {
                came.Add(mark);
            }
        }

        return came;
    }

    private static bool CauseHappened(ResidentRecord record, MarkDefinition mark, RaidFacts facts)
    {
        switch (mark.Cause)
        {
            case MarkCause.GoodRaid:
                return facts.Extracted && facts.TimesDowned == 0;
            case MarkCause.Downed:
                return facts.TimesDowned > 0;
            case MarkCause.Failed:
                return !facts.Extracted;
            case MarkCause.Boss:
                return facts.BossesBeaten > 0;
            case MarkCause.Kills:
                return mark.Count > 0 && KillsOf(record, mark.Creature) >= mark.Count;
            default:
                return false;
        }
    }

    /// <summary>Foes of a kind (a word in their id) felled along, over every raid.</summary>
    public static int KillsOf(ResidentRecord record, string creature)
    {
        int total = 0;
        foreach (KeyValuePair<string, int> deed in record.Deeds)
        {
            if (deed.Key.StartsWith("kills:") && Matches(creature, deed.Key.Substring(6)))
            {
                total += deed.Value;
            }
        }

        return total;
    }

    /// <summary>Found again: the forest's mark (PeopleSettings › Forest Mark), held until every memory is back.</summary>
    public static void FoundAgain(ResidentRecord record)
    {
        Give(record, PeopleSettings.Current.ForestMark);
    }

    /// <summary>True when <paramref name="mark"/> can be healed now (the forest's holds while memories are still lost).</summary>
    public static bool CanHeal(ResidentRecord record, MarkDefinition mark) =>
        record != null && mark != null && !mark.Good && (!mark.HeldByTheForest || record.Forgotten.Count == 0);

    /// <summary>The first hard mark <paramref name="item"/> heals, healable now, or null.</summary>
    public static MarkDefinition HealedBy(ResidentRecord record, ItemDefinition item)
    {
        foreach (MarkDefinition mark in Of(record))
        {
            if (item != null && mark.HealedBy == item && CanHeal(record, mark))
            {
                return mark;
            }
        }

        return null;
    }

    /// <summary>Heals <paramref name="mark"/>. False when it can't be healed now.</summary>
    public static bool Heal(ResidentRecord record, MarkDefinition mark)
    {
        if (!CanHeal(record, mark) || !record.HasMark(mark.Id))
        {
            return false;
        }

        record.RemoveMark(mark.Id);
        record.ForgetDeed(SinceKey(mark));
        return true;
    }

    /// <summary>A small scene with a Friend: one hard mark a friend can ease is healed. Returns it, or null.</summary>
    public static MarkDefinition EasedByFriends(ResidentRecord record)
    {
        foreach (MarkDefinition mark in Of(record))
        {
            if (mark.FriendsHeal && CanHeal(record, mark))
            {
                Heal(record, mark);
                return mark;
            }
        }

        return null;
    }

    /// <summary>Hard marks still on them (each lowers their spirits a step while it stays).</summary>
    public static int HardCount(ResidentRecord record)
    {
        int count = 0;
        foreach (MarkDefinition mark in Of(record))
        {
            count += mark.Good ? 0 : 1;
        }

        return count;
    }

    private static string KillKey(string creatureId) => "kills:" + creatureId;

    private static string SinceKey(MarkDefinition mark) => "since:" + mark.Id;

    private static bool Matches(string word, string creatureId) =>
        !string.IsNullOrEmpty(word) && !string.IsNullOrEmpty(creatureId) && creatureId.IndexOf(word, System.StringComparison.OrdinalIgnoreCase) >= 0;
}
