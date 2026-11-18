using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What residents do on their own: someone whose job has work orders keeps their workplace's queue going from what lies in the
/// stash, and carries what is finished back to it. They never touch the player's bag, stop short of emptying the stash
/// (WorkOrder.keepInput) and stop once there is plenty (WorkOrder.stockTarget). Only a queue that exists can run, which is to say
/// the workplace stands; HavenProgress keeps those queues current at home, so this goes on during raids. Called by SettlementSystem.
/// </summary>
public static class WorkerProduction
{
    /// <summary>One look at every worker: finished work goes to the stash, and an idle station gets its next order.</summary>
    public static void Work(SettlementState settlement, StashStorage stash)
    {
        foreach (ResidentRecord record in settlement.Residents)
        {
            StationQueue queue = QueueOf(settlement, record);
            if (queue == null)
            {
                continue;
            }

            Carry(queue, stash);
            if (!record.WorkPaused && queue.IsIdle)
            {
                StartNext(record.Job, queue, stash);
            }
        }
    }

    /// <summary>
    /// The queue of the station this resident works at: the one nearest their home (ResidentRecord.WorkQueue), else the
    /// first of their job's kind. Null when their job has no workplace or none stands.
    /// </summary>
    public static StationQueue QueueOf(IHavenView settlement, ResidentRecord record)
    {
        if (record == null)
        {
            return null;
        }

        if (!string.IsNullOrEmpty(record.WorkQueue) && record.Job != null && record.Job.Workplace != null)
        {
            foreach (StationQueue queue in settlement.Workshops.Queues)
            {
                if (queue.StationId == record.WorkQueue && queue.Type == record.Job.Workplace.Station)
                {
                    return queue;
                }
            }
        }

        return QueueOf(settlement, record.Job);
    }

    /// <summary>The queue of the first station of a job's kind, or null when it has no workplace or none stands.</summary>
    public static StationQueue QueueOf(IHavenView settlement, JobDefinition job)
    {
        if (job == null || job.Workplace == null || job.Workplace.Station == CraftingStationType.None)
        {
            return null;
        }

        foreach (StationQueue queue in settlement.Workshops.Queues)
        {
            if (queue.Type == job.Workplace.Station)
            {
                return queue;
            }
        }

        return null;
    }

    /// <summary>Why this order isn't being worked on now, for the player; null when it could be.</summary>
    public static string WhyNot(WorkOrder order, StationQueue queue, StashStorage stash)
    {
        RecipeDefinition recipe = order.recipe;
        if (recipe == null || !recipe.HasOutput)
        {
            return "nothing to make";
        }

        if (recipe.RequiredStationTier > queue.Tier)
        {
            return $"needs Tier {Tiers.ToRoman(recipe.RequiredStationTier)}";
        }

        if (StockOf(recipe, queue, stash) >= order.stockTarget)
        {
            return "plenty in the stash";
        }

        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item != null && stash.Items.Count(cost.Item) < cost.Amount + order.keepInput)
            {
                return $"not enough {cost.Item.DisplayName} in the stash";
            }
        }

        foreach (ResourceAmount cost in recipe.ResourceCost)
        {
            if (cost.Resource != null && stash.Resources.GetAmount(cost.Resource) < cost.Amount + order.keepInput)
            {
                return $"not enough {cost.Resource.DisplayName} in the stash";
            }
        }

        return null;
    }

    private static int StockOf(RecipeDefinition recipe, StationQueue queue, StashStorage stash)
    {
        int waiting = queue.Output == recipe.OutputDefinition ? queue.OutputAmount : 0;
        return waiting + (recipe.Output != null ? stash.Items.Count(recipe.Output) : stash.Resources.GetAmount(recipe.OutputResource));
    }

    private static void StartNext(JobDefinition job, StationQueue queue, StashStorage stash)
    {
        foreach (WorkOrder order in job.WorkOrders)
        {
            if (WhyNot(order, queue, stash) != null)
            {
                continue;
            }

            foreach (ItemAmount cost in order.recipe.ItemCost)
            {
                stash.Items.Remove(cost.Item, cost.Amount);
            }

            foreach (ResourceAmount cost in order.recipe.ResourceCost)
            {
                stash.Resources.Remove(cost.Resource, cost.Amount);
            }

            queue.Enqueue(order.recipe);
            queue.WorkerCrafts++;
            return;
        }
    }

    // Anything finished at their station goes to the stash, whoever asked for it, but gear: a piece is made by whoever collects it
    // (StationCrafting.Collect, CraftedWork: its quality, condition and maker rolled there). Carried as it lay in the slot, every piece
    // ordered at a staffed station came Plain, nameless and new, and Fine or Masterwork could never be made there. What won't fit stays.
    private static void Carry(StationQueue queue, StashStorage stash)
    {
        if (queue.Output is ItemDefinition item && queue.OutputAmount > 0 && !item.KeepsCondition && !item.HasDurability)
        {
            queue.TakeOutput(stash.Items.Add(item, queue.OutputAmount));
        }
    }
}
