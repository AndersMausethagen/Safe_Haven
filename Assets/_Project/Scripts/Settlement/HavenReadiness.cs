using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// What the base does for raids: Reach (more time out there), Endurance (Well Rested, from comfort) and Knowledge (rumours, and
/// what the Watch Post has taught). Read live from what stands at home, so it is only worth anything before setting out. The
/// best building of a kind counts rather than each one, so variety pays and copies don't.
/// </summary>
public static class HavenReadiness
{
    public const string RestedId = "haven_rested";
    public const string RestedName = "Well Rested";

    /// <summary>Seconds every raid gets from the ward and the buildings.</summary>
    public static float RaidSeconds => WardSeconds + BestOfEachKind(level => level.raidSeconds);

    public static int ExtraRumours => Mathf.RoundToInt(BestOfEachKind(level => level.extraRumours));

    /// <summary>How many of a raid's coming events are known before setting out.</summary>
    public static int ForetoldEvents => Mathf.RoundToInt(BestOfEachKind(level => level.foretoldEvents));

    public static int ExtraLogs => Mathf.RoundToInt(BestOfEachKind(level => level.extraLogs));

    /// <summary>Comfort at home: what each kind of building gives once, plus what its upgrades add, everyone with a bed, the
    /// best home once (HomeLife) and each kind of yard once (Yards).</summary>
    public static int Comfort => Standing().Select(ComfortOf).GroupBy(entry => entry.Building).Sum(kind => kind.Max(entry => entry.Comfort)) + GameSession.Settlement.Housed
        + HomeLife.Comfort + Yards.Comfort;

    public static float RestedSeconds => Comfort * HavenSettings.Current.RestedSecondsPerComfort;

    private static float WardSeconds
    {
        get
        {
            SettlementSettings settings = SettlementSettings.Current;
            float seconds = 0f;
            for (int i = 0; i < GameSession.Settlement.WardLevel && i < settings.WardLevels.Count; i++)
            {
                seconds += settings.WardLevels[i].raidSeconds;
            }

            return seconds;
        }
    }

    /// <summary>Sets the player out rested, for as long as the comfort at home allows.</summary>
    public static void GiveRest(GameObject player)
    {
        float seconds = RestedSeconds;
        HavenSettings settings = HavenSettings.Current;
        if (seconds <= 0f || player == null || settings.RestedBonuses.Count == 0 || !player.TryGetComponent(out PlayerBoons boons))
        {
            return;
        }

        if (boons.Add(RestedId, RestedName, settings.RestedBonuses, seconds, false))
        {
            GameMessages.Post($"{RestedName}: {ItemTooltipView.DescribeBonuses(settings.RestedBonuses)} for {RaidStats.FormatTime(seconds)}", MessageKind.Gain);
        }
    }

    /// <summary>The Raids tab of the Haven window.</summary>
    public static List<HavenShelf> Read()
    {
        var reach = new HavenShelf("Reach");
        reach.Entries.Add(ReachEntry());
        var endurance = new HavenShelf("Endurance");
        endurance.Entries.Add(RestEntry());
        var knowledge = new HavenShelf("Knowledge");
        knowledge.Entries.Add(RumourEntry());
        return new List<HavenShelf> { reach, endurance, knowledge };
    }

    private static HavenEntry ReachEntry()
    {
        float total = RaidSeconds + GameSession.Settlement.JobBonus(job => job.RaidSeconds);
        SettlementSettings settings = SettlementSettings.Current;
        int level = GameSession.Settlement.WardLevel;
        var entry = new HavenEntry
        {
            Key = settings,
            Name = "Time in the woods",
            Line = total > 0f ? $"+{RaidStats.FormatTime(total)} on every raid" : "No extra time yet",
            Badge = total > 0f ? $"+{RaidStats.FormatTime(total)}" : "",
            State = total > 0f ? HavenEntryState.Good : HavenEntryState.Ready,
            Status = total > 0f ? $"Every raid lasts {RaidStats.FormatTime(total)} longer." : "Raids run on their own clock.",
            Description = "The stronger the portal, the longer it holds the way home open. People with the right job add to it, and Portal Sparks buy more for one raid.",
        };
        entry.Details.Add(new HavenDetail("From the ward", $"{RaidStats.FormatTime(WardSeconds)}"));
        if (level < settings.WardLevels.Count)
        {
            string opens = settings.WardLevels[level].opens;
            entry.Details.Add(new HavenDetail("Next", $"Strengthen the ward at the portal: {RaidStats.FormatTime(settings.WardLevels[level].raidSeconds)} more.{(string.IsNullOrEmpty(opens) ? "" : " " + opens)}"));
        }

        return entry;
    }

    private static HavenEntry RestEntry()
    {
        int comfort = Comfort;
        var entry = new HavenEntry
        {
            Key = HavenSettings.Current,
            Name = RestedName,
            Line = comfort > 0 ? $"Comfort {comfort}: {RaidStats.FormatTime(RestedSeconds)} on setting out" : "No comfort yet",
            Badge = comfort > 0 ? comfort.ToString() : "",
            State = comfort > 0 ? HavenEntryState.Good : HavenEntryState.Ready,
            Status = comfort > 0 ? $"You set out rested for {RaidStats.FormatTime(RestedSeconds)}." : "You set out as you are.",
            Description = $"{ItemTooltipView.DescribeBonuses(HavenSettings.Current.RestedBonuses)} when you set out. Each kind of comfortable building adds {RaidStats.FormatTime(HavenSettings.Current.RestedSecondsPerComfort)} per point, once.",
        };
        var sources = Standing().Select(ComfortOf).Where(one => one.Comfort > 0).GroupBy(one => one.Building)
            .Select(kind => $"{kind.Key.DisplayName}: {kind.Max(one => one.Comfort)}").ToList();
        int housed = GameSession.Settlement.Housed;
        if (housed > 0)
        {
            sources.Add($"People with a bed: {housed}");
        }

        entry.Details.Add(new HavenDetail("Comfort from", sources.Count > 0 ? string.Join("\n", sources) : "Nothing yet. A Hut is the best start."));
        return entry;
    }

    private static HavenEntry RumourEntry()
    {
        int rumours = ExtraRumours + GameSession.Settlement.JobBonus(job => job.ExtraRumours);
        BuildingObject post = Standing().FirstOrDefault(one => one.Definition.Upgrades.Any(level => level.extraRumours > 0));
        var entry = new HavenEntry
        {
            Key = post != null ? post.Definition : null,
            Name = "Word from the woods",
            Line = rumours > 0 ? $"{rumours} more rumour{(rumours == 1 ? "" : "s")} before each raid" : "No extra rumours yet",
            Badge = rumours > 0 ? $"+{rumours}" : "",
            State = rumours > 0 ? HavenEntryState.Good : HavenEntryState.Ready,
            Status = rumours > 0 ? "You know more of what is out there." : "You go in blind.",
            Description = "A Watch Post hears what moves in the woods, and teaches the raid runes: some make a raid harder and pay for it in loot. You choose them at the portal.",
        };
        entry.Details.Add(new HavenDetail("Seen coming", ForetoldEvents > 0 ? $"The briefing names {ForetoldEvents} of the raid's events beforehand." : "Later Watch Post levels name a raid's events beforehand."));
        entry.Details.Add(new HavenDetail("Harder raids", "Watch Post levels teach the runes that change a raid's rules. Make them at the Cauldron, then pick them at the portal."));
        if (post != null)
        {
            HavenTravel.AddGoTo(entry, post.transform);
        }

        return entry;
    }

    private static IEnumerable<BuildingObject> Standing() => BuildingObject.Active.Where(one =>
        one.Definition != null && one.Definition.Faction == Faction.Player
        && (!one.TryGetComponent(out Construction construction) || construction.IsStanding));

    private static (BuildingDefinition Building, int Comfort) ComfortOf(BuildingObject one)
    {
        int comfort = one.Definition.Comfort;
        if (one.TryGetComponent(out Construction construction))
        {
            comfort += Mathf.RoundToInt(construction.Sum(level => level.comfort));
        }

        return (one.Definition, comfort);
    }

    // Each kind counts its best building: three Watch Posts hear no more than one.
    private static float BestOfEachKind(Func<BuildingLevel, float> bonus)
    {
        var best = new Dictionary<BuildingDefinition, float>();
        foreach (BuildingObject one in Standing())
        {
            if (one.TryGetComponent(out Construction construction))
            {
                best.TryGetValue(one.Definition, out float known);
                best[one.Definition] = Mathf.Max(known, construction.Sum(bonus));
            }
        }

        return best.Values.Sum();
    }
}
