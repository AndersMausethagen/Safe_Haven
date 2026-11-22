using System.Collections.Generic;
using UnityEngine;

/// <summary>A party sent out from the Haven to one of its held lands while the player raids.</summary>
public enum LandPartyKind
{
    /// <summary>The Hunter: meat and hides home, and the land's hold wears less.</summary>
    Hunt = 0,
    /// <summary>The Fisher: fish home, and the land's hold wears less.</summary>
    Fish = 1,
    /// <summary>The Tracker: the forest's next push on the land is seen coming sooner.</summary>
    Scout = 2,
}

/// <summary>
/// Parties to the held lands (Docs/ColonyModeResearch_2026-11-21.md §4.6, Docs/Territory.md "Not yet"): standing orders, set in the Haven
/// window's Lands tab, counted in raids, not hours, so nobody is watched walking and co-op has nothing to sync. On each raid long enough
/// (TerritorySettings › Party Min Seconds) the first resident holding a party's job (not the one along, not paused) goes out to its land:
/// a hunting or fishing party brings a small yield home (a range shown before sending, more for a practised hand) and the land's hold
/// wears less that turn; a scouting party sees the forest gathering against the land a turn sooner. Never a raise in hold, and nobody is
/// hurt or lost. Meanwhile they are away from their own station (SettlementSystem, WorkerProduction), so a party costs a station's work for
/// the raid: a choice. Plain rules (LandPartiesTests); Territory settles them as the raid's turn passes.
/// </summary>
public static class LandParties
{
    private static readonly LandPartyKind[] Kinds = { LandPartyKind.Hunt, LandPartyKind.Fish, LandPartyKind.Scout };

    /// <summary>Every kind of party, in the order the Lands tab offers them.</summary>
    public static IReadOnlyList<LandPartyKind> All => Kinds;

    /// <summary>"hunting", "fishing", "scouting".</summary>
    public static string Doing(LandPartyKind kind) => kind == LandPartyKind.Hunt ? "hunting" : kind == LandPartyKind.Fish ? "fishing" : "scouting";

    /// <summary>
    /// Who goes on a party of <paramref name="kind"/>: the first resident holding its job, at work (not paused) and not
    /// <paramref name="along"/> on the raid; null when nobody can.
    /// </summary>
    public static ResidentRecord WhoGoes(IHavenView home, LandPartyKind kind, ResidentDefinition along, TerritorySettings settings)
    {
        TerritorySettings.PartyInfo info = settings.PartyOf(kind);
        if (info == null || info.job == null)
        {
            return null;
        }

        foreach (ResidentRecord record in home.Residents)
        {
            if (record.Job == info.job && !record.WorkPaused && record.Resident != null && record.Resident != along)
            {
                return record;
            }
        }

        return null;
    }

    /// <summary>True once the raid under way has run long enough for the parties to have set out (Party Min Seconds): before that, and
    /// on a raid too short to bring anything, their stations are worked as ever.</summary>
    public static bool SetOut(TerritorySettings settings) =>
        RaidDirector.Current != null && RaidDirector.Current.Session != null && RaidDirector.Current.Session.Elapsed >= settings.PartyMinSeconds;

    /// <summary>Whether <paramref name="record"/> is out on a party while the player is away (their station unworked meanwhile).</summary>
    public static bool IsOut(SettlementState home, ResidentRecord record, ResidentDefinition along, TerritorySettings settings)
    {
        if (record == null || home.Parties.Count == 0)
        {
            return false;
        }

        foreach (KeyValuePair<LandPartyKind, string> party in home.Parties)
        {
            if (home.Territory.IsClaimed(party.Value) && WhoGoes(home, party.Key, along, settings) == record)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Why a party of <paramref name="kind"/> can't be sent to <paramref name="region"/> now, or null.</summary>
    public static string WhyNot(LandPartyKind kind, string region, bool visiting, IHavenView home, TerritorySettings settings)
    {
        if (visiting)
        {
            return "Parties go out from your own Haven.";
        }

        if (!home.Territory.IsClaimed(region))
        {
            return "Only to a land the Haven holds.";
        }

        TerritorySettings.PartyInfo info = settings.PartyOf(kind);
        if (info == null || info.job == null)
        {
            return "Nobody goes out for that.";
        }

        return WhoGoes(home, kind, null, settings) == null ? $"Nobody here works as a {info.job.DisplayName} yet." : null;
    }

    /// <summary>
    /// What a party of <paramref name="kind"/> led by <paramref name="who"/> brings from one raid, each a range ("2-4 Boar Meat, 1-2 Rough
    /// Hide"), or what it does instead (a scouting party); for the Lands tab, before sending.
    /// </summary>
    public static string Odds(LandPartyKind kind, ResidentRecord who, TerritorySettings settings)
    {
        TerritorySettings.PartyInfo info = settings.PartyOf(kind);
        if (kind == LandPartyKind.Scout || info == null || info.yields.Count == 0)
        {
            return $"the forest's next push there is seen {settings.ScoutWarningTurns} turn{(settings.ScoutWarningTurns == 1 ? "" : "s")} sooner";
        }

        int bonus = Bonus(who, settings);
        var parts = new List<string>();
        foreach (TerritorySettings.PartyYield yield in info.yields)
        {
            if (yield != null && yield.item != null && yield.most > 0)
            {
                int least = Mathf.Min(yield.least, yield.most) + bonus;
                int most = yield.most + bonus;
                parts.Add(least == most ? $"{most} {yield.item.DisplayName}" : $"{least}-{most} {yield.item.DisplayName}");
            }
        }

        return string.Join(", ", parts) + $", and the hold wears {Mathf.RoundToInt((1f - settings.PartyWearShare) * 100f)}% less";
    }

    // More of each yield for each worker rank above the first (SettlementSettings › Worker Ranks).
    private static int Bonus(ResidentRecord who, TerritorySettings settings) =>
        who != null ? Mathf.Max(0, SettlementSettings.Current.RankIndex(who.WorkDone)) * settings.PartyRankBonus : 0;

    /// <summary>What one party of <paramref name="kind"/> led by <paramref name="who"/> brings home, rolled with <paramref name="roll"/>
    /// (least and most, inclusive, to a number).</summary>
    public static List<ItemAmount> Roll(LandPartyKind kind, ResidentRecord who, TerritorySettings settings, System.Func<int, int, int> roll)
    {
        var brought = new List<ItemAmount>();
        TerritorySettings.PartyInfo info = settings.PartyOf(kind);
        if (info == null)
        {
            return brought;
        }

        int bonus = Bonus(who, settings);
        foreach (TerritorySettings.PartyYield yield in info.yields)
        {
            if (yield == null || yield.item == null || yield.most <= 0)
            {
                continue;
            }

            int amount = roll(Mathf.Min(yield.least, yield.most) + bonus, yield.most + bonus);
            if (amount > 0)
            {
                brought.Add(new ItemAmount(yield.item, amount));
            }
        }

        return brought;
    }

    /// <summary>The parties that go out on this raid: who leads each, to which land. None for a raid too short, or none set.</summary>
    public static List<(LandPartyKind kind, string region, ResidentRecord who)> Going(SettlementState home, float raidSeconds,
        ResidentDefinition along, TerritorySettings settings)
    {
        var going = new List<(LandPartyKind, string, ResidentRecord)>();
        if (raidSeconds < settings.PartyMinSeconds)
        {
            return going;
        }

        foreach (LandPartyKind kind in Kinds)
        {
            string region = home.PartyOn(kind);
            ResidentRecord who = region != null && home.Territory.IsClaimed(region) ? WhoGoes(home, kind, along, settings) : null;
            if (who != null)
            {
                going.Add((kind, region, who));
            }
        }

        return going;
    }
}
