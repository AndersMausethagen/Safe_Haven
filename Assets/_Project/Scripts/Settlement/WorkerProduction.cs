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
            StationQueue queue = QueueOf(settlement, record.Job);
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

    /// <summary>The queue of the station a job works at, or null when it has no workplace or none stands.</summary>
    public static StationQueue QueueOf(SettlementState settlement, JobDefinition job)
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

    // Anything finished at their station goes to the stash, whoever asked for it. What won't fit stays in the slot.
    private static void Carry(StationQueue queue, StashStorage stash)
    {
        if (queue.Output is ItemDefinition item && queue.OutputAmount > 0)
        {
            queue.TakeOutput(stash.Items.Add(item, queue.OutputAmount));
        }
    }
}
