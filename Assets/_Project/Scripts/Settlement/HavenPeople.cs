using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Everyone Safe Haven is for, for the Haven window: who is home, what job they hold and what it does for the haven, and who of
/// the expedition is still out there and what they would bring.
/// </summary>
public static class HavenPeople
{
    public const string HomeShelf = "Home";
    public const string AwayShelf = "Still out there";

    /// <summary>The expedition from the settings, plus anyone else who has come to live here.</summary>
    public static List<HavenShelf> Read() => Read(HavenSettings.Current.Expedition, GameSession.Settlement);

    public static List<HavenShelf> Read(IEnumerable<ResidentDefinition> expedition, SettlementState settlement)
    {
        var home = new HavenShelf(HomeShelf);
        var away = new HavenShelf(AwayShelf);
        List<ResidentDefinition> everyone = expedition.Where(person => person != null).ToList();
        everyone.AddRange(settlement.Residents.Select(record => record.Resident).Where(person => person != null && !everyone.Contains(person)));
        foreach (ResidentDefinition person in everyone)
        {
            ResidentRecord record = settlement.Find(person);
            if (record != null)
            {
                home.Entries.Add(HomeEntry(person, record, settlement));
            }
            else
            {
                away.Entries.Add(AwayEntry(person));
            }
        }

        return new List<HavenShelf> { home, away };
    }

    private static HavenEntry HomeEntry(ResidentDefinition person, ResidentRecord record, SettlementState settlement)
    {
        JobDefinition job = record.Job;
        bool canWork = GameSession.Settlement.JobsOpenTo(person).Count > 0;
        var entry = new HavenEntry
        {
            Key = person,
            Name = person.DisplayName,
            Line = job != null ? job.DisplayName : canWork ? "No job yet" : "Home",
            State = job == null && canWork ? HavenEntryState.Attention : HavenEntryState.Good,
            Status = job != null ? $"Home, working as {job.DisplayName}" : "Home",
            Description = person.Description,
            Icon = person.Portrait,
        };

        if (job != null)
        {
            entry.Details.Add(new HavenDetail(job.DisplayName, JobText(job)));
            SettlementSettings people = SettlementSettings.Current;
            if (job.WorkOrders.Count > 0)
            {
                entry.Details.Add(new HavenDetail("Rank", $"{people.RankFor(record.WorkDone).name} ({record.WorkDone} made)"));
            }

            if (job.Teaches != null)
            {
                entry.Details.Add(new HavenDetail("Teaches", record.TaughtSinceRaid ? $"{job.Teaches.DisplayName} (again after a raid survived)" : job.Teaches.DisplayName));
            }
            string workplace = WorkplaceText(job);
            if (workplace != null)
            {
                entry.Details.Add(new HavenDetail("Where they work", workplace));
            }
        }

        BuildingObject house = HavenHomes.HomeOf(record);
        entry.Details.Add(new HavenDetail("Where they sleep", house != null
            ? $"In the {HavenHomes.Describe(house)}, a bed of their own: their work goes {CraftingStation.BedSpeedBonus * 100f:0}% faster, and home is that bit more comfortable."
            : settlement.HasBed(record)
            ? $"In a bed of their own: their work goes {CraftingStation.BedSpeedBonus * 100f:0}% faster, and home is that bit more comfortable."
            : "By the fire. They manage, but a Hut or Cottage would help."));
        // Their own page: the house they live in (and moving them), their work and their story.
        entry.Actions.Add(new HavenAction("Their page", () => ResidentWindow.Show(person, "Home"), primary: false));
        AddWork(entry, record, settlement);
        List<JobDefinition> others = GameSession.Settlement.JobsOpenTo(person).Where(one => one != null && one != job).ToList();
        if (others.Count > 0)
        {
            entry.Details.Add(new HavenDetail(job != null ? "Could also work as" : "Could work as",
                string.Join("\n\n", others.Select(one => $"<b>{one.DisplayName}</b>\n{JobText(one)}"))));
        }

        foreach (JobDefinition other in others)
        {
            JobDefinition offered = other;
            entry.Actions.Add(new HavenAction($"Work as {offered.DisplayName}", () => ResidentNpc.GiveJob(person, offered, Player.Current != null ? Player.Current.gameObject : null), closesWindow: false));
        }

        foreach (ResidentNpc npc in Object.FindObjectsByType<ResidentNpc>())
        {
            if (npc.Resident == person)
            {
                HavenTravel.AddGoTo(entry, npc.transform);
                break;
            }
        }

        return entry;
    }

    // What they make on their own, and why they aren't right now, with the way to stop them using up the stash.
    private static void AddWork(HavenEntry entry, ResidentRecord record, SettlementState settlement)
    {
        if (record.Job == null || record.Job.WorkOrders.Count == 0)
        {
            return;
        }

        StationQueue queue = WorkerProduction.QueueOf(settlement, record);
        if (queue == null)
        {
            entry.Line = $"{record.Job.DisplayName}, nowhere to work";
            entry.State = HavenEntryState.Attention;
            return;
        }

        if (record.WorkPaused)
        {
            entry.Line = $"{record.Job.DisplayName}, resting";
        }
        else if (queue.Current != null)
        {
            entry.Line = $"{record.Job.DisplayName}, making {queue.Current.DisplayName}";
            entry.State = HavenEntryState.Working;
        }

        var orders = new List<string>();
        foreach (WorkOrder order in record.Job.WorkOrders)
        {
            if (order.recipe != null)
            {
                string why = WorkerProduction.WhyNot(order, queue, GameSession.Stash);
                orders.Add(why == null ? $"{order.recipe.DisplayName}: ready" : $"{order.recipe.DisplayName}: {why}");
            }
        }

        entry.Details.Add(new HavenDetail("Keeps making, from the stash", string.Join("\n", orders)));
        // Made where the Haven is kept: a visitor's word goes to their friend's game (HavenDecisions).
        entry.Actions.Add(new HavenAction(record.WorkPaused ? "Back to work" : "Pause their work",
            () => HavenDecisions.Do("resident.pause", record.Resident.Id), closesWindow: false, primary: false));
    }

    [HavenDecision("resident.pause")]
    private static HavenDecisions.Outcome TogglePause(string residentId)
    {
        ResidentRecord record = RecordOf(residentId);
        if (record == null || record.Job == null)
        {
            return HavenDecisions.Outcome.Refused("They have no work to pause now.");
        }

        record.WorkPaused = !record.WorkPaused;
        return HavenDecisions.Outcome.Made(record.WorkPaused ? $"{record.Resident.DisplayName} rests from their work." : $"{record.Resident.DisplayName} goes back to work.");
    }

    /// <summary>A resident of this Haven by their id, or null (what a friend's decision names them by).</summary>
    public static ResidentRecord RecordOf(string residentId)
    {
        foreach (ResidentRecord record in GameSession.Settlement.Residents)
        {
            if (record != null && record.Resident != null && record.Resident.Id == residentId)
            {
                return record;
            }
        }

        return null;
    }

    private static HavenEntry AwayEntry(ResidentDefinition person)
    {
        var entry = new HavenEntry
        {
            Key = person,
            Name = person.DisplayName,
            Line = "Still out there",
            State = HavenEntryState.Locked,
            Status = "Still out there",
            Description = person.Description,
            Icon = person.Portrait,
        };

        List<JobDefinition> jobs = GameSession.Settlement.JobsOpenTo(person).Where(one => one != null).ToList();
        if (jobs.Count > 0)
        {
            entry.Details.Add(new HavenDetail("What they would bring",
                string.Join("\n\n", jobs.Select(one => $"<b>{one.DisplayName}</b>\n{JobText(one)}"))));
        }

        return entry;
    }

    private static string JobText(JobDefinition job)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(job.Description)) parts.Add(job.Description);
        string bonus = job.DescribeBonus();
        bool alreadySaid = job.Description != null && job.Description.IndexOf(bonus, System.StringComparison.OrdinalIgnoreCase) >= 0;
        if (bonus.Length > 0 && !alreadySaid) parts.Add(char.ToUpperInvariant(bonus[0]) + bonus.Substring(1) + ".");
        string taught = string.Join(", ", job.TeachesBuildings.Where(b => b != null).Select(b => b.DisplayName)
            .Concat(job.TeachesRecipes.Where(r => r != null).Select(r => r.DisplayName)));
        if (taught.Length > 0) parts.Add($"Teaches: {taught}.");
        return string.Join("\n", parts);
    }

    private static string WorkplaceText(JobDefinition job)
    {
        if (job.Workplace == null)
        {
            return null;
        }

        return BuildingObject.Count(job.Workplace) > 0
            ? $"At the {job.Workplace.DisplayName}."
            : $"Needs a {job.Workplace.DisplayName}: none stands yet.";
    }
}
