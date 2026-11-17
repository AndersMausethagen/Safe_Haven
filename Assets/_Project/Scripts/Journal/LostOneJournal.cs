using System.Collections.Generic;
using System.Text;

/// <summary>
/// The journal's tab of the Lost: each one the player has met, still walking or laid to rest, with who it was, its rank, what the player
/// has seen through of it, what it carries of theirs and what has passed between them. Reads the roster (CharacterProgress.LostOnes)
/// into shelves for the journal's list page; nothing here touches the screen. One not met yet isn't shown at all, not even as a blank.
/// </summary>
public static class LostOneJournal
{
    /// <summary>The tab's shelves: those still walking, then those laid to rest. Empty until one is met (the tab is hidden then).</summary>
    public static List<HavenShelf> Shelves(LostOneSettings settings, CharacterProgress progress, out string summary)
    {
        var walking = new HavenShelf("Still walking");
        var rested = new HavenShelf("Laid to rest");
        foreach (LostOneRecord record in progress.LostOnes.All)
        {
            if (record.Met)
            {
                (record.IsWalking ? walking : rested).Entries.Add(EntryOf(record, settings, progress));
            }
        }

        int met = walking.Entries.Count + rested.Entries.Count;
        summary = met == 0 ? "None met yet" : $"{walking.Entries.Count} still walking, {rested.Entries.Count} laid to rest";
        var shelves = new List<HavenShelf>();
        if (walking.Entries.Count > 0)
        {
            shelves.Add(walking);
        }

        if (rested.Entries.Count > 0)
        {
            shelves.Add(rested);
        }

        return shelves;
    }

    private static HavenEntry EntryOf(LostOneRecord record, LostOneSettings settings, CharacterProgress progress)
    {
        LostOneSettings.Rank rank = settings.RankAt(record.Rank);
        string rankLine = string.IsNullOrWhiteSpace(rank.title) ? $"Rank {Tiers.ToRoman(record.Rank)}" : $"Rank {Tiers.ToRoman(record.Rank)}, {rank.title}";
        var entry = new HavenEntry
        {
            Key = "lost:" + record.Id,
            Name = record.FullName,
            Line = record.IsWalking ? rankLine : "Laid to rest",
            Badge = "",
            State = record.IsWalking ? HavenEntryState.Attention : HavenEntryState.Good,
            Status = record.IsWalking ? $"Walks the mist. {rankLine}." : string.IsNullOrWhiteSpace(record.Epitaph) ? "Laid to rest." : record.Epitaph,
            Description = LostOneRules.Say(settings.JournalIntro, record, record.LastSeenIn),
        };

        entry.Details.Add(new HavenDetail(record.IsWalking ? "What you have seen through" : "What it was", Traits(record, settings)));
        if (record.Carried.Count > 0)
        {
            var carried = new List<string>();
            foreach (ItemStack stack in record.Carried)
            {
                carried.Add(stack.Count > 1 ? $"{stack.Count} {stack.DisplayName}" : stack.DisplayName);
            }

            entry.Details.Add(new HavenDetail("It carries what it took from you", string.Join(", ", carried) + ". Lay it to rest and it is yours again, if you can carry it home."));
        }

        entry.Details.Add(new HavenDetail("Between you", Between(record)));

        // New to read until opened, as the journal's other pages are.
        string key = "lost:" + record.Id + ":" + record.Status;
        entry.ReadKey = key;
        if (!progress.Journal.HasRead(key))
        {
            entry.Badge = JournalContent.UnreadMark;
            entry.State = HavenEntryState.Ready;
        }

        return entry;
    }

    // Each trait seen through, with what to do about it; a count of the rest.
    private static string Traits(LostOneRecord record, LostOneSettings settings)
    {
        var text = new StringBuilder();
        int unknown = 0;
        foreach (string name in record.Traits)
        {
            if (!record.Knows(name))
            {
                unknown++;
                continue;
            }

            EliteAffixDefinition trait = settings.FindTrait(name);
            if (trait != null)
            {
                text.Append($"<b>{trait.DisplayName}</b>: {trait.Description}\n");
            }
        }

        if (unknown > 0)
        {
            text.Append(unknown == 1 ? "There is one thing more to it you haven't seen through yet." : $"There are {unknown} things more to it you haven't seen through yet.");
        }

        return text.Length == 0 ? "Nothing yet. Make it run, and you will see more of it." : text.ToString().TrimEnd();
    }

    private static string Between(LostOneRecord record)
    {
        var text = new StringBuilder();
        // Counted as the raid ends: one met on the raid under way has been met on one.
        int raids = System.Math.Max(1, record.RaidsMet);
        text.Append(raids == 1 ? "Met on one raid" : $"Met on {raids} raids");
        text.Append(string.IsNullOrWhiteSpace(record.LastSeenIn) ? ". " : $", last in {LostOneRules.Place(record.LastSeenIn)}. ");
        text.Append(record.Kills == 0 ? "It has never brought you down. " : record.Kills == 1 ? "It has brought you down once. " : $"It has brought you down {record.Kills} times. ");
        text.Append(record.Escapes == 0 ? "It has never fled from you." : record.Escapes == 1 ? "It has fled from you once." : $"It has fled from you {record.Escapes} times.");
        return text.ToString();
    }
}
