using System;
using System.Collections.Generic;

/// <summary>Where one quest sits on the Quest Map: its column (how many quests come before it) and its row.</summary>
public sealed class QuestMapNode
{
    public QuestDefinition Quest;
    public int Column;
    public int Row;
    public string Band;
}

/// <summary>
/// Lays quests out for the Quest Map: a column per prerequisite depth (longest path, so a quest is always right of every
/// quest it needs) and one horizontal band per chain, so each storyline reads left to right. Cycles are cut, not looped.
/// </summary>
public static class QuestMapLayout
{
    public const string NoChain = "No chain";

    public static List<QuestMapNode> Build(IEnumerable<QuestDefinition> quests)
    {
        var all = new List<QuestDefinition>();
        foreach (QuestDefinition quest in quests)
        {
            if (quest != null && !all.Contains(quest))
            {
                all.Add(quest);
            }
        }

        var depths = new Dictionary<QuestDefinition, int>();
        var visiting = new HashSet<QuestDefinition>();
        foreach (QuestDefinition quest in all)
        {
            Depth(quest, all, depths, visiting);
        }

        var bands = new SortedDictionary<string, List<QuestDefinition>>(Comparer<string>.Create(CompareBands));
        foreach (QuestDefinition quest in all)
        {
            string band = string.IsNullOrWhiteSpace(quest.Chain) ? NoChain : quest.Chain.Trim();
            if (!bands.TryGetValue(band, out List<QuestDefinition> members))
            {
                bands[band] = members = new List<QuestDefinition>();
            }

            members.Add(quest);
        }

        var nodes = new List<QuestMapNode>();
        int bandTop = 0;
        foreach (KeyValuePair<string, List<QuestDefinition>> band in bands)
        {
            band.Value.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            var used = new Dictionary<int, int>();
            int tallest = 0;
            foreach (QuestDefinition quest in band.Value)
            {
                int column = depths[quest];
                used.TryGetValue(column, out int row);
                used[column] = row + 1;
                tallest = Math.Max(tallest, row + 1);
                nodes.Add(new QuestMapNode { Quest = quest, Column = column, Row = bandTop + row, Band = band.Key });
            }

            bandTop += tallest;
        }

        return nodes;
    }

    /// <summary>Why the quest "to" can't be made to come after "from", or null when the link is fine.</summary>
    public static string CheckLink(QuestDefinition from, QuestDefinition to)
    {
        if (from == null || to == null || from == to)
        {
            return "A quest can't come after itself.";
        }

        foreach (QuestDefinition prerequisite in to.Prerequisites)
        {
            if (prerequisite == from)
            {
                return $"{to.DisplayName} already comes after {from.DisplayName}.";
            }
        }

        return Requires(from, to, new HashSet<QuestDefinition>())
            ? $"{from.DisplayName} already needs {to.DisplayName}, so this would make a loop."
            : null;
    }

    // Whether the quest needs the other one somewhere down its prerequisites.
    private static bool Requires(QuestDefinition quest, QuestDefinition other, HashSet<QuestDefinition> visited)
    {
        if (!visited.Add(quest))
        {
            return false;
        }

        foreach (QuestDefinition prerequisite in quest.Prerequisites)
        {
            if (prerequisite != null && (prerequisite == other || Requires(prerequisite, other, visited)))
            {
                return true;
            }
        }

        return false;
    }

    private static int Depth(QuestDefinition quest, List<QuestDefinition> all, Dictionary<QuestDefinition, int> depths, HashSet<QuestDefinition> visiting)
    {
        if (depths.TryGetValue(quest, out int known))
        {
            return known;
        }

        if (!visiting.Add(quest))
        {
            return -1;
        }

        int depth = 0;
        foreach (QuestDefinition prerequisite in quest.Prerequisites)
        {
            if (prerequisite != null && prerequisite != quest && all.Contains(prerequisite))
            {
                depth = Math.Max(depth, Depth(prerequisite, all, depths, visiting) + 1);
            }
        }

        visiting.Remove(quest);
        depths[quest] = depth;
        return depth;
    }

    // Named chains alphabetically, quests without a chain last.
    private static int CompareBands(string a, string b)
    {
        if (a == b)
        {
            return 0;
        }

        if (a == NoChain)
        {
            return 1;
        }

        return b == NoChain ? -1 : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
