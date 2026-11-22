using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Outposts at the Secure lands (Docs/EconomyAndPhases_2026-11-22.md §4.4, approved 2026-11-22: "You decide whats best for now"): raised
/// from the Haven window's Lands tab at a land held Secure, paid for once (TerritorySettings › Outpost Cost), and kept by a resident who
/// goes out to it on each raid long enough, as a party does (LandParties: their own station waits meanwhile). Each such raid the outpost
/// sends home the land's own material (RegionInfo › Outpost: Greyfell's iron ore, the Wolfwood's thick hide, Ravenwood's deepstone), a
/// small capped share of the tier's need: the one home source allowed above tier II. A forest push on the land, or the land slipping
/// below Secure, pauses it; the land lost, it is given up. Elderwood stays a raid wood (the user's answer 2). Plain rules (OutpostsTests);
/// Territory settles them as a raid's turn passes.
/// </summary>
public static class Outposts
{
    /// <summary>The resident keeping the outpost on <paramref name="region"/>, or null (none, or they no longer live here).</summary>
    public static ResidentRecord KeeperOf(IHavenView home, string region)
    {
        string id = home.OutpostKeeper(region);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        foreach (ResidentRecord record in home.Residents)
        {
            if (record.Resident != null && record.Resident.Id == id)
            {
                return record;
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="record"/> keeps an outpost.</summary>
    public static bool Keeps(IHavenView home, ResidentRecord record)
    {
        if (record == null || record.Resident == null)
        {
            return false;
        }

        foreach (KeyValuePair<string, string> outpost in home.Outposts)
        {
            if (outpost.Value == record.Resident.Id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Who would keep a new outpost (or take over <paramref name="region"/>'s): the first resident, after <paramref name="after"/> in the
    /// list, at work (not paused), keeping no other outpost, building nothing (a builder's work is the Haven's) and leading no party; null
    /// when nobody can.
    /// </summary>
    public static ResidentRecord Candidate(IHavenView home, TerritorySettings settings, ResidentRecord after = null)
    {
        IReadOnlyList<ResidentRecord> people = home.Residents;
        int start = after != null ? IndexOf(people, after) + 1 : 0;
        for (int n = 0; n < people.Count; n++)
        {
            ResidentRecord record = people[(start + n) % people.Count];
            if (record == after || record.Resident == null || record.WorkPaused || Keeps(home, record) || ResidentBuilders.Builds(record)
                || LandParties.Leads(home, record, settings))
            {
                continue;
            }

            return record;
        }

        return null;
    }

    private static int IndexOf(IReadOnlyList<ResidentRecord> people, ResidentRecord record)
    {
        for (int i = 0; i < people.Count; i++)
        {
            if (people[i] == record)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Why an outpost can't be raised on <paramref name="region"/> now, or null.</summary>
    public static string WhyNot(string region, bool visiting, IHavenView home, TerritorySettings settings)
    {
        if (visiting)
        {
            return "Outposts are raised from your own Haven.";
        }

        TerritorySettings.RegionInfo info = settings.Info(region);
        if (info == null || info.outpost.Count == 0)
        {
            return "There is nothing out there worth an outpost.";
        }

        if (home.Territory.StandingOf(region, settings) != RegionStanding.Secure)
        {
            return "Only on a land held Secure.";
        }

        if (!string.IsNullOrEmpty(home.OutpostKeeper(region)))
        {
            return "It has one.";
        }

        return Candidate(home, settings) == null ? "Nobody is free to keep it: everyone builds, leads a party or keeps another." : null;
    }

    /// <summary>Whether the outpost on <paramref name="region"/> sends anything home this turn: the land Secure and no push against it.</summary>
    public static string Paused(IHavenView home, string region, TerritorySettings settings)
    {
        if (home.Territory.StandingOf(region, settings) != RegionStanding.Secure)
        {
            return "The land isn't Secure: the outpost waits until it is again.";
        }

        TerritoryState.Push push = home.Territory.Incursion;
        return push != null && push.Region == region ? "The forest gathers against the land: the outpost waits until the push is answered." : null;
    }

    /// <summary>What one raid's turn at the outpost on <paramref name="region"/> brings, rolled with <paramref name="roll"/> (least and
    /// most, inclusive).</summary>
    public static List<ItemAmount> Roll(string region, TerritorySettings settings, System.Func<int, int, int> roll)
    {
        var brought = new List<ItemAmount>();
        TerritorySettings.RegionInfo info = settings.Info(region);
        if (info == null)
        {
            return brought;
        }

        foreach (TerritorySettings.PartyYield yield in info.outpost)
        {
            if (yield == null || yield.item == null || yield.most <= 0)
            {
                continue;
            }

            int amount = roll(Mathf.Min(yield.least, yield.most), yield.most);
            if (amount > 0)
            {
                brought.Add(new ItemAmount(yield.item, amount));
            }
        }

        return brought;
    }

    /// <summary>What the outpost on <paramref name="region"/> sends home a raid, in words ("3-5 Iron Ore").</summary>
    public static string Odds(string region, TerritorySettings settings)
    {
        TerritorySettings.RegionInfo info = settings.Info(region);
        var parts = new List<string>();
        foreach (TerritorySettings.PartyYield yield in info != null ? info.outpost : new List<TerritorySettings.PartyYield>())
        {
            if (yield != null && yield.item != null && yield.most > 0)
            {
                int least = Mathf.Min(yield.least, yield.most);
                parts.Add(least == yield.most ? $"{yield.most} {yield.item.DisplayName}" : $"{least}-{yield.most} {yield.item.DisplayName}");
            }
        }

        return string.Join(", ", parts);
    }

    /// <summary>
    /// The outposts that send something home for a raid of <paramref name="raidSeconds"/>: their land, and who kept it (not
    /// <paramref name="along"/> on the raid). None for a raid shorter than Party Min Seconds.
    /// </summary>
    public static List<(string region, ResidentRecord keeper)> Working(SettlementState home, float raidSeconds, ResidentDefinition along, TerritorySettings settings)
    {
        var working = new List<(string, ResidentRecord)>();
        if (raidSeconds < settings.PartyMinSeconds)
        {
            return working;
        }

        foreach (KeyValuePair<string, string> outpost in home.Outposts)
        {
            ResidentRecord keeper = KeeperOf(home, outpost.Key);
            if (keeper != null && keeper.Resident != along && !keeper.WorkPaused && Paused(home, outpost.Key, settings) == null)
            {
                working.Add((outpost.Key, keeper));
            }
        }

        return working;
    }

    /// <summary>Whether <paramref name="record"/> is out at their outpost while the player raids (their station unworked meanwhile).</summary>
    public static bool IsOut(SettlementState home, ResidentRecord record, ResidentDefinition along, TerritorySettings settings)
    {
        if (record == null || record.Resident == along || record.WorkPaused || !Keeps(home, record))
        {
            return false;
        }

        // Only to an outpost that works this turn: one that waits keeps its keeper at home (and their station worked).
        foreach (KeyValuePair<string, string> outpost in home.Outposts)
        {
            if (outpost.Value == record.Resident.Id)
            {
                return Paused(home, outpost.Key, settings) == null;
            }
        }

        return false;
    }
}
