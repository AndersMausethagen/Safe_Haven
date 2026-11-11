using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The Haven's lands for the Haven window's Lands tab (Territory): the regions held, how firmly, the one the forest is pushing on,
/// and the wild ones still to claim, each with what it is like, what it gives and what it asks. Only reads.
/// </summary>
public static class HavenLands
{
    public static List<HavenShelf> Read()
    {
        var pushed = new HavenShelf("The forest is pushing");
        var held = new HavenShelf("The Haven's lands");
        var wild = new HavenShelf("Wild");
        TerritorySettings settings = TerritorySettings.Current;
        TerritoryState state = GameSession.Settlement.Territory;
        foreach (WorldAtlas.Region region in Territory.Claimable)
        {
            HavenEntry entry = EntryOf(region.name, settings, state);
            bool isPushed = state.Incursion != null && state.Incursion.Region == region.name;
            (isPushed ? pushed : state.IsClaimed(region.name) ? held : wild).Entries.Add(entry);
        }

        held.Entries.Sort((a, b) => state.HoldOf(b.Name).CompareTo(state.HoldOf(a.Name)));
        return new[] { pushed, held, wild }.Where(s => s.Entries.Count > 0).ToList();
    }

    /// <summary>"3 of 11 held · the forest pushes on Ravenwood in 2 turns".</summary>
    public static string Summary()
    {
        TerritoryState state = GameSession.Settlement.Territory;
        int all = Territory.Claimable.Count();
        string summary = $"{state.ClaimedCount} of {all} regions held · {Territory.Renown} renown from them";
        if (state.Incursion != null)
        {
            int turns = System.Math.Max(0, state.Incursion.DueTurn - state.Turn);
            summary += $" · the forest pushes on {state.Incursion.Region} in {turns} turn{(turns == 1 ? "" : "s")}";
        }

        return summary;
    }

    private static HavenEntry EntryOf(string region, TerritorySettings settings, TerritoryState state)
    {
        TerritorySettings.RegionInfo info = settings.Info(region);
        RegionStanding standing = Territory.StandingOf(region);
        bool isPushed = state.Incursion != null && state.Incursion.Region == region;
        var entry = new HavenEntry
        {
            Key = region,
            Name = region,
            Line = Territory.Describe(region),
            Status = Territory.Describe(region),
            Description = info != null ? info.description : "",
            State = isPushed || standing == RegionStanding.Slipping ? HavenEntryState.Attention
                : standing == RegionStanding.Wild ? HavenEntryState.Locked : HavenEntryState.Good,
        };

        if (info != null)
        {
            entry.Details.Add(new HavenDetail("Danger", info.danger.ToString()));
            entry.Details.Add(new HavenDetail("What it gives", info.gives));
        }

        entry.Details.Add(new HavenDetail("Places found", $"{Territory.PlacesFound(region)} of {Territory.PlacesIn(region).Count}"));
        if (isPushed)
        {
            TerritoryState.Push push = state.Incursion;
            entry.Details.Add(new HavenDetail("The forest's push",
                $"Comes in {System.Math.Max(0, push.DueTurn - state.Turn)} turns and would take {push.Remaining:0} hold. Stand watch at its boundary stone to break it, " +
                "or burn the rot's heart on a raid (the portal's briefing shows where): each one halves it."));
        }

        string task = Territory.TaskText(region);
        if (task != null)
        {
            entry.Details.Add(new HavenDetail("What it asks", task));
        }

        if (info != null && info.tithe.Count > 0)
        {
            string tithe = string.Join(", ", info.tithe.Where(t => t.Item != null).Select(t => $"{t.Amount} {t.Item.DisplayName}"));
            entry.Details.Add(new HavenDetail(standing == RegionStanding.Secure ? "Sends home each dawn" : "Would send home each dawn, once Secure", tithe));
        }

        if (standing == RegionStanding.Wild)
        {
            entry.Details.Add(new HavenDetail("To claim it",
                $"Find one of its places, then light its boundary stone{(state.ClaimNeedsRune(settings) ? " with a Pact Rune" : " (the first is free)")}."));
        }

        return entry;
    }
}
