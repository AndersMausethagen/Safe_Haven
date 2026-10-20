using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// One crafting station's work: a short queue of crafts (ingredients already paid) and an output slot. The station works
/// on the first craft for its recipe's Craft Seconds, then delivers the result: items go to the output slot; resources go
/// to base storage first and only what doesn't fit lands in the output slot. The slot holds one kind of thing, up to
/// Capacity. When the result has nowhere to go the station waits (Blocked) until the slot is emptied.
/// <para>
/// Plain data owned by WorkshopState (part of SettlementState, so it is saved and keeps working during raids). Stations
/// in the scene find their queue by their SaveableEntity id.
/// </para>
/// </summary>
public sealed class StationQueue
{
    /// <summary>A recipe waiting in the queue, Count times.</summary>
    public sealed class Job
    {
        public Job(RecipeDefinition recipe, int count)
        {
            Recipe = recipe;
            Count = count;
        }

        public RecipeDefinition Recipe { get; }
        public int Count { get; set; }
    }

    private readonly List<Job> jobs = new List<Job>();

    public StationQueue(string stationId, CraftingStationType type, int capacity, int maxQueued)
    {
        StationId = stationId;
        Type = type;
        Configure(capacity, maxQueued);
    }

    public string StationId { get; }
    public CraftingStationType Type { get; }
    public int Capacity { get; private set; }
    public int MaxQueued { get; private set; }
    public IReadOnlyList<Job> Jobs => jobs;
    /// <summary>Seconds spent on the craft in progress.</summary>
    public float Progress { get; private set; }
    /// <summary>What sits in the output slot (an ItemDefinition or a ResourceDefinition), or null when empty.</summary>
    public Object Output { get; private set; }
    public int OutputAmount { get; private set; }
    /// <summary>True when the last finished craft couldn't be delivered: the station waits for the slot to be emptied.</summary>
    public bool Blocked { get; private set; }

    public RecipeDefinition Current => jobs.Count > 0 ? jobs[0].Recipe : null;
    public bool IsIdle => jobs.Count == 0;

    public int QueuedCount
    {
        get
        {
            int count = 0;
            foreach (Job job in jobs)
            {
                count += job.Count;
            }

            return count;
        }
    }

    public int FreeQueueSlots => Mathf.Max(0, MaxQueued - QueuedCount);

    /// <summary>Seconds left on the craft in progress.</summary>
    public float CurrentRemaining => Current != null ? Mathf.Max(0f, Current.CraftSeconds - Progress) : 0f;

    /// <summary>Applies the station's current output size and queue length (they come from its building definition).</summary>
    public void Configure(int capacity, int maxQueued)
    {
        Capacity = Mathf.Max(1, capacity);
        MaxQueued = Mathf.Max(1, maxQueued);
    }

    /// <summary>Adds crafts of the recipe (already paid for) to the end of the queue. Returns how many fit.</summary>
    public int Enqueue(RecipeDefinition recipe, int count = 1)
    {
        int added = Mathf.Min(count, FreeQueueSlots);
        if (recipe == null || !recipe.HasOutput || added <= 0)
        {
            return 0;
        }

        Job last = jobs.Count > 0 ? jobs[jobs.Count - 1] : null;
        if (last != null && last.Recipe == recipe)
        {
            last.Count += added;
        }
        else
        {
            jobs.Add(new Job(recipe, added));
        }

        return added;
    }

    /// <summary>True when the last craft in the queue hasn't been started, so it can be cancelled and refunded.</summary>
    public bool CanCancelLast => jobs.Count > 1 || (jobs.Count == 1 && (jobs[0].Count > 1 || (Progress <= 0f && !Blocked)));

    /// <summary>Removes the last unstarted craft and returns its recipe so the caller can refund it; null when none.</summary>
    public RecipeDefinition CancelLast()
    {
        if (!CanCancelLast)
        {
            return null;
        }

        Job last = jobs[jobs.Count - 1];
        last.Count--;
        if (last.Count <= 0)
        {
            jobs.RemoveAt(jobs.Count - 1);
        }

        return last.Recipe;
    }

    /// <summary>
    /// Works for the given seconds, delivering every craft that finishes. Resources go to storage first (may be null).
    /// Calls finished once per delivered craft. Returns how many crafts were delivered.
    /// </summary>
    public int Tick(float seconds, ResourceStorage storage, Action<RecipeDefinition> finished = null)
    {
        if (seconds <= 0f || jobs.Count == 0)
        {
            return 0;
        }

        Progress += seconds;
        int delivered = 0;
        while (jobs.Count > 0 && Progress >= jobs[0].Recipe.CraftSeconds)
        {
            RecipeDefinition recipe = jobs[0].Recipe;
            if (!TryDeliver(recipe, storage))
            {
                Blocked = true;
                Progress = recipe.CraftSeconds;
                return delivered;
            }

            Blocked = false;
            Progress -= recipe.CraftSeconds;
            jobs[0].Count--;
            if (jobs[0].Count <= 0)
            {
                jobs.RemoveAt(0);
            }

            delivered++;
            finished?.Invoke(recipe);
        }

        if (jobs.Count == 0)
        {
            Progress = 0f;
        }

        return delivered;
    }

    /// <summary>How many of the thing the output slot could still take.</summary>
    public int SlotRoomFor(Object definition)
    {
        if (definition == null)
        {
            return 0;
        }

        return Output == null || Output == definition ? Capacity - OutputAmount : 0;
    }

    private bool TryDeliver(RecipeDefinition recipe, ResourceStorage storage)
    {
        int amount = recipe.OutputAmount;
        if (recipe.OutputResource != null)
        {
            int toStorage = storage != null ? Mathf.Min(amount, storage.GetFreeSpace(recipe.OutputResource)) : 0;
            int toSlot = amount - toStorage;
            if (toSlot > SlotRoomFor(recipe.OutputResource))
            {
                return false;
            }

            if (toStorage > 0)
            {
                toSlot += toStorage - storage.Add(recipe.OutputResource, toStorage);
            }

            PutInSlot(recipe.OutputResource, toSlot);
            return true;
        }

        if (amount > SlotRoomFor(recipe.Output))
        {
            return false;
        }

        PutInSlot(recipe.Output, amount);
        return true;
    }

    private void PutInSlot(Object definition, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Output = definition;
        OutputAmount += amount;
    }

    /// <summary>Takes up to the given amount out of the output slot. Returns how many were taken.</summary>
    public int TakeOutput(int amount)
    {
        int taken = Mathf.Clamp(amount, 0, OutputAmount);
        OutputAmount -= taken;
        if (OutputAmount <= 0)
        {
            OutputAmount = 0;
            Output = null;
        }

        if (taken > 0)
        {
            Blocked = false;
        }

        return taken;
    }

    /// <summary>Puts back saved state. Used by SaveMapper only.</summary>
    public void Restore(IEnumerable<Job> savedJobs, float progress, Object output, int outputAmount)
    {
        jobs.Clear();
        foreach (Job job in savedJobs)
        {
            if (job.Recipe != null && job.Count > 0)
            {
                jobs.Add(new Job(job.Recipe, job.Count));
            }
        }

        Progress = jobs.Count > 0 ? Mathf.Max(0f, progress) : 0f;
        Output = outputAmount > 0 ? output : null;
        OutputAmount = Output != null ? outputAmount : 0;
        Blocked = false;
    }
}

/// <summary>
/// Every crafting station's queue in Safe Haven, by station id. Part of SettlementState so it is wiped by New Game,
/// replaced by Load, saved, and ticked by SettlementSystem wherever the player is (stations keep working during raids).
/// </summary>
public sealed class WorkshopState
{
    private readonly Dictionary<string, StationQueue> queues = new Dictionary<string, StationQueue>();

    public IEnumerable<StationQueue> Queues => queues.Values;

    public StationQueue Find(string stationId)
    {
        return stationId != null && queues.TryGetValue(stationId, out StationQueue queue) ? queue : null;
    }

    /// <summary>The station's queue, created on first use. Capacity and queue length follow the station's current values.</summary>
    public StationQueue GetOrCreate(string stationId, CraftingStationType type, int capacity, int maxQueued)
    {
        if (string.IsNullOrEmpty(stationId))
        {
            return null;
        }

        if (queues.TryGetValue(stationId, out StationQueue queue))
        {
            queue.Configure(capacity, maxQueued);
            return queue;
        }

        queue = new StationQueue(stationId, type, capacity, maxQueued);
        queues.Add(stationId, queue);
        return queue;
    }

    /// <summary>Adds a queue read from a save. Used by SaveMapper only.</summary>
    public void Add(StationQueue queue)
    {
        if (queue != null && !string.IsNullOrEmpty(queue.StationId))
        {
            queues[queue.StationId] = queue;
        }
    }

    /// <summary>Forgets a station's queue (it was demolished). Its contents are lost; refund them first.</summary>
    public void Remove(string stationId)
    {
        if (stationId != null)
        {
            queues.Remove(stationId);
        }
    }

    /// <summary>Works every station for the given seconds. Returns how many crafts were delivered in total.</summary>
    public int Tick(float seconds, ResourceStorage storage, Action<StationQueue, RecipeDefinition> finished = null)
    {
        int delivered = 0;
        foreach (StationQueue queue in queues.Values)
        {
            if (queue.IsIdle)
            {
                continue;
            }

            StationQueue current = queue;
            delivered += queue.Tick(seconds, storage, finished != null ? recipe => finished(current, recipe) : (Action<RecipeDefinition>)null);
        }

        return delivered;
    }
}
