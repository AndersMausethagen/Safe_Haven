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
    private const float MinSpeed = 0.01f;

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

    /// <summary>How many kinds of thing may wait in the output at once: 1, more with a module that adds output slots (the Storage
    /// Loft). The first is Output; the others wait in <see cref="MoreOutput"/> and move up as it is emptied. Set by its station.</summary>
    public int OutputSlots
    {
        get => outputSlots;
        set => outputSlots = Mathf.Max(1, value);
    }

    private int outputSlots = 1;
    private readonly List<(Object made, int amount)> moreOutput = new List<(Object, int)>();

    /// <summary>What waits in the output slots after the first.</summary>
    public IReadOnlyList<(Object made, int amount)> MoreOutput => moreOutput;

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
    public float CurrentRemaining => Current != null ? Mathf.Max(0f, Current.CraftSeconds - Progress) / Mathf.Max(MinSpeed, Speed) : 0f;

    /// <summary>Real seconds until everything in the queue is made, at the station's speed.</summary>
    public float TotalRemaining
    {
        get
        {
            float work = -Progress;
            foreach (Job job in jobs)
            {
                work += job.Recipe.CraftSeconds * job.Count;
            }

            return Mathf.Max(0f, work) / Mathf.Max(MinSpeed, Speed);
        }
    }

    /// <summary>How many crafts of a recipe are waiting or being made.</summary>
    public int CountOf(RecipeDefinition recipe)
    {
        int count = 0;
        foreach (Job job in jobs)
        {
            if (job.Recipe == recipe)
            {
                count += job.Count;
            }
        }

        return count;
    }

    /// <summary>
    /// What a finished craft is waiting for, in words for the player, or null when nothing holds it up. The slot takes one
    /// kind at a time, so it can stop with room to spare: "full" would be wrong then.
    /// </summary>
    public string WaitingFor
    {
        get
        {
            if (!Blocked || Current == null)
            {
                return null;
            }

            return Output != null && Output != Current.OutputDefinition && Output is IDisplayName other
                ? $"collect the {other.DisplayName} first"
                : "the output is full, collect it";
        }
    }

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

    /// <summary>True when the last craft in the queue hasn't been started and is the player's, so it can be cancelled and refunded. A
    /// worker's (they queue only on an idle station, so theirs come first) is theirs: cancelled, the player was refunded what the worker
    /// took from the stash, and their own next order was counted as the worker's.</summary>
    public bool CanCancelLast => QueuedCount > WorkerCrafts
        && (jobs.Count > 1 || (jobs.Count == 1 && (jobs[0].Count > 1 || (Progress <= 0f && !Blocked))));

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

    /// <summary>How fast it works on its own: 1 is a recipe's Craft Seconds. Set by its CraftingStation from WorkerSettings › Unattended
    /// Speed and its upgrades, yard and extensions; not saved.</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>What its worker adds while at it (their work speed, rank, bed, home and gear; Palworld's work adding up). Set by its
    /// CraftingStation; kept through raids, when no station is loaded. Not saved.</summary>
    public float WorkerSpeed { get; set; }

    /// <summary>What its worker adds for being cheered by a small scene nearby (SmallScenes), set by its CraftingStation at home and
    /// cleared while the player is away (a cheer is for the day at home, not the raid). Not saved.</summary>
    public float WorkerCheer { get; set; }

    /// <summary>Whether its worker is at it now: standing at the station at home (ResidentLife), and taken to be so while the player is
    /// away (WorkshopState.Tick). Not saved.</summary>
    public bool WorkerHere { get; set; }

    /// <summary>A horse works at its station (HorseJobs): its worker carries more. Set at home with the speed; not saved.</summary>
    public bool HorseHelps { get; set; }

    /// <summary>Metres from its station to the chest its worker fetches from, measured at home (0 until then: WorkerSettings › Assumed
    /// Trip Distance). Sets how long a trip takes while the player is away. Not saved.</summary>
    public float TripMetres { get; set; }

    /// <summary>What the player adds while standing at its station (WorkerSettings › Player Speed; Palworld's player at the bench). Set
    /// at home with the speed; not saved.</summary>
    public float PlayerSpeed { get; set; }

    /// <summary>Whether the player stands at its station now (set at home; never while away).</summary>
    public bool PlayerHere { get; set; }

    /// <summary>How fast it works now: on its own, plus its worker and the player while they are at it.</summary>
    public float Speed => (SpeedMultiplier + (WorkerHere ? WorkerSpeed + WorkerCheer : 0f) + (PlayerHere ? PlayerSpeed : 0f)) * Fields.GrowthFactor(this);

    /// <summary>A field's water (Fields): seconds of growing left before its soil is dry; drains only while something grows. Saved.</summary>
    public float Water { get; set; }

    // What its worker fetched from the chests for their work orders: what a worker's order is paid from (WorkerProduction).
    private readonly Dictionary<ItemDefinition, int> supplies = new Dictionary<ItemDefinition, int>();

    /// <summary>What its worker has fetched from the chests and not yet used (saved with the station).</summary>
    public IReadOnlyDictionary<ItemDefinition, int> Supplies => supplies;

    /// <summary>How many of <paramref name="item"/> lie fetched here.</summary>
    public int Supplied(ItemDefinition item) => item != null && supplies.TryGetValue(item, out int count) ? count : 0;

    /// <summary>Puts what was fetched by the station, for the worker's orders.</summary>
    public void AddSupply(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return;
        }

        supplies[item] = Supplied(item) + amount;
    }

    /// <summary>Takes <paramref name="amount"/> of what was fetched (paying an order); false, taking nothing, when there is too little.</summary>
    public bool TakeSupply(ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return true;
        }

        int have = Supplied(item);
        if (have < amount)
        {
            return false;
        }

        if (have == amount)
        {
            supplies.Remove(item);
        }
        else
        {
            supplies[item] = have - amount;
        }

        return true;
    }

    /// <summary>Gives back everything fetched (the station taken down, the worker gone): what lay here, emptied.</summary>
    public List<ItemAmount> TakeAllSupplies()
    {
        var all = new List<ItemAmount>();
        foreach (var pair in supplies)
        {
            all.Add(new ItemAmount(pair.Key, pair.Value));
        }

        supplies.Clear();
        return all;
    }

    /// <summary>The extra chance of one more here, from the station's extensions and modules, for workers and the player (set at home with the speed).</summary>
    public float ExtraChance { get; set; }

    /// <summary>How much more often gear made here comes out Fine, from the station's modules (set at home with the speed); not saved.</summary>
    public float FineChance { get; set; }

    /// <summary>The tier of the station working this queue, kept up by the station. Stations that aren't buildings have no limit.</summary>
    public int Tier { get; set; } = Tiers.Max;

    /// <summary>The modules fitted to the station working this queue, kept up by the station (a recipe's Needs Module); not saved.</summary>
    public IReadOnlyList<BuildingModuleDefinition> Modules { get; set; } = System.Array.Empty<BuildingModuleDefinition>();

    public bool HasModule(BuildingModuleDefinition module)
    {
        if (module == null)
        {
            return true;
        }

        foreach (BuildingModuleDefinition fitted in Modules)
        {
            if (fitted == module)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>How many crafts in the queue a worker started: they aren't announced one by one, and they are the worker's work, not the
    /// player's practice (SettlementSystem). Saved with the station.</summary>
    public int WorkerCrafts { get; set; }

    /// <summary>Finer pieces its worker made, waiting for whoever collects the gear ("quality|maker": ResidentTalents.WorkerPiece):
    /// each lifts one piece collected to that quality and signs it with their name. Saved with the station.</summary>
    public List<string> Lifts { get; } = new List<string>();

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

        Progress += seconds * Speed;
        // A field's soil dries as its crop drinks (Fields); empty, or its harvest waiting with no room, it keeps its water.
        if (Type == CraftingStationType.Farm && !Blocked)
        {
            Water = Mathf.Max(0f, Water - seconds);
        }

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

        if (Output == null || Output == definition)
        {
            return Capacity - OutputAmount;
        }

        foreach ((Object made, int amount) in moreOutput)
        {
            if (made == definition)
            {
                return Capacity - amount;
            }
        }

        return moreOutput.Count < OutputSlots - 1 ? Capacity : 0;
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

        if (Output == null || Output == definition)
        {
            Output = definition;
            OutputAmount += amount;
            return;
        }

        for (int i = 0; i < moreOutput.Count; i++)
        {
            if (moreOutput[i].made == definition)
            {
                moreOutput[i] = (definition, moreOutput[i].amount + amount);
                return;
            }
        }

        moreOutput.Add((definition, amount));
    }

    /// <summary>Puts a little more of what was just made into the output slot (a practised hand's extra), if there is
    /// room. Returns whether it went in.</summary>
    public bool AddExtra(Object definition, int amount)
    {
        if (definition == null || amount <= 0 || SlotRoomFor(definition) < amount)
        {
            return false;
        }

        PutInSlot(definition, amount);
        return true;
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
            // The next slot's kind moves up.
            if (moreOutput.Count > 0)
            {
                (Output, OutputAmount) = moreOutput[0];
                moreOutput.RemoveAt(0);
            }
        }

        if (taken > 0)
        {
            Blocked = false;
        }

        return taken;
    }

    /// <summary>Puts back saved state. Used by SaveMapper only.</summary>
    public void Restore(IEnumerable<Job> savedJobs, float progress, Object output, int outputAmount, IEnumerable<(Object made, int amount)> more = null)
    {
        moreOutput.Clear();
        foreach ((Object made, int amount) in more ?? System.Array.Empty<(Object, int)>())
        {
            if (made != null && amount > 0)
            {
                moreOutput.Add((made, amount));
            }
        }

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
        if (Output == null && moreOutput.Count > 0)
        {
            (Output, OutputAmount) = moreOutput[0];
            moreOutput.RemoveAt(0);
        }

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

    /// <summary>True while the player is away from home (SettlementSystem): no workplace is loaded, so each queue's worker is taken to be at
    /// their station, as the builders are taken to be at their scaffolds; the player is at none of them.</summary>
    [field: NotSaved("whether the player is home, set by SettlementSystem every frame")]
    public bool AwayFromHome { get; set; }

    /// <summary>The station whose worker went along on the raid (its id; SettlementSystem): not worked meanwhile.</summary>
    [field: NotSaved("the raid companion's station, set by SettlementSystem every frame")]
    public string WorkerAbsentAt { get; set; }

    /// <summary>The stations whose workers are out on a party to a held land meanwhile (their ids; SettlementSystem, LandParties): not
    /// worked while the player is away.</summary>
    [field: NotSaved("the parties' stations, set by SettlementSystem while the player is away")]
    public HashSet<string> PartyAbsentAt { get; } = new HashSet<string>();

    /// <summary>
    /// Works every queue for the given seconds, delivering every craft that finishes. Resources go to storage first (may be null).
    /// Calls finished once per delivered craft. Returns how many crafts were delivered.
    /// </summary>
    public int Tick(float seconds, ResourceStorage storage, Action<StationQueue, RecipeDefinition> finished = null)
    {
        int delivered = 0;
        foreach (StationQueue queue in queues.Values)
        {
            if (AwayFromHome)
            {
                queue.WorkerHere = queue.WorkerSpeed > 0f && queue.StationId != WorkerAbsentAt && !PartyAbsentAt.Contains(queue.StationId);
                queue.WorkerCheer = 0f;
                queue.PlayerHere = false;
            }

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
