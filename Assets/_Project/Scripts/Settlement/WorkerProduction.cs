using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What residents do on their own: someone whose job has work orders keeps their workplace's queue going from what lies in the
/// stash, and carries what is finished back to it. They never touch the player's bag, stop short of emptying the stash
/// (WorkOrder.keepInput) and stop once there is plenty (WorkOrder.stockTarget). Only a queue that exists can run, which is to say
/// the workplace stands; HavenProgress keeps those queues current at home, so this goes on during raids. Called by SettlementSystem.
/// <para>
/// Fetching (the user, 2026-11-21: "They have to get resources from storage, but they have reduced carry capacity and slots";
/// WorkerSettings): an order is paid from what its worker has fetched to the station (StationQueue.Supplies), not straight from the
/// stash. At home a worker walks to a chest and back (ResidentLife, with <see cref="PlanFetch"/> and <see cref="Deliver"/>); while the
/// player is away each trip takes the time the walk would (<see cref="TripAway"/>), carrying finished work back and inputs over
/// within their carry (WorkerCarry). Before any chest stands near the workplace, or where their body isn't in the scene at home, it
/// goes as before: straight from the stash and back (so nothing waits on what can't be done).
/// </para>
/// </summary>
public static class WorkerProduction
{
    /// <summary>How a worker's materials move.</summary>
    public enum Hauling
    {
        /// <summary>Straight from the stash and back (no chest near their workplace, or no body here).</summary>
        Instantly,
        /// <summary>On foot at home: ResidentLife fetches.</summary>
        OnFoot,
        /// <summary>The player is away: trips take the time the walk would.</summary>
        Away,
    }

    // Away: each worker's seconds towards their next trip, by resident id. Not saved: a trip under way when the game is saved starts over.
    private static readonly Dictionary<string, float> TripClock = new Dictionary<string, float>();

    // At most this many trips caught up in one look (a long frame, a load).
    private const int MostTripsAtOnce = 8;

    // Crafts fetched for at once, at most: by hand that is what 25 weight holds (a handful), with a cart the stock target's worth.
    private const int MostCraftsPerTrip = 20;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        TripClock.Clear();
        Claimed.Clear();
    }

    /// <summary>Forgets trips under way (a new game, a load).</summary>
    public static void ForgetGame() => TripClock.Clear();

    /// <summary>One look at every worker with materials moving straight from the stash and back, as before fetching was walked.</summary>
    public static void Work(SettlementState settlement, StashStorage stash) => Work(settlement, stash, 0f, true, _ => Hauling.Instantly);

    /// <summary>
    /// One look at every worker, <paramref name="seconds"/> after the last: finished work goes to the stash, an idle station gets its next
    /// order, and each worker's materials move as <paramref name="howFor"/> says (null: instantly at home, by trips away).
    /// </summary>
    public static void Work(SettlementState settlement, StashStorage stash, float seconds, bool atHome, System.Func<ResidentRecord, Hauling> howFor,
        ResidentDefinition away = null)
    {
        Claimed.Clear();
        foreach (ResidentRecord record in settlement.Residents)
        {
            StationQueue queue = QueueOf(settlement, record);
            if (queue == null)
            {
                continue;
            }

            Claimed.Add(queue);
            // The one along on the raid works nowhere meanwhile, nor those out of the Haven (ResidentsOut: a party, an outpost, a foray).
            if ((record.Resident != null && record.Resident == away) || ResidentsOut.Now(settlement, record, away))
            {
                continue;
            }

            Hauling how = howFor != null ? howFor(record) : atHome ? Hauling.Instantly : Hauling.Away;
            switch (how)
            {
                case Hauling.OnFoot:
                    break;
                case Hauling.Away:
                    if (!record.WorkPaused)
                    {
                        TripAway(record, queue, stash, seconds);
                    }

                    break;
                default:
                    Carry(queue, stash);
                    break;
            }

            // Fetching on foot or away, a craft starts only from what was fetched: their carrying is what paces the work.
            if (!record.WorkPaused && queue.IsIdle)
            {
                StartNext(record.Job, queue, stash, how != Hauling.Instantly);
            }
        }

        // What was fetched to a station nobody works at any more (their job changed, they moved to another of the kind, it came down)
        // goes back to the chests: it is never lost at a station nobody looks after.
        foreach (StationQueue queue in settlement.Workshops.Queues)
        {
            if (queue.Supplies.Count > 0 && !Claimed.Contains(queue))
            {
                GiveBack(queue, stash);
            }
        }
    }

    // The queues someone works at, this look.
    private static readonly HashSet<StationQueue> Claimed = new HashSet<StationQueue>();

    /// <summary>Everything fetched to <paramref name="queue"/> back into the chests (what won't fit stays at the station).</summary>
    public static void GiveBack(StationQueue queue, StashStorage stash)
    {
        foreach (ItemAmount supply in queue.TakeAllSupplies())
        {
            int back = stash.Items.Add(supply.Item, supply.Amount);
            if (back < supply.Amount)
            {
                queue.AddSupply(supply.Item, supply.Amount - back);
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

    /// <summary>Why this order isn't being worked on now, for the player; null when it could be. What was fetched counts with the stash.</summary>
    public static string WhyNot(WorkOrder order, StationQueue queue, StashStorage stash)
    {
        // The thriftier variant where its module is fitted (RecipeVariants): a worker at a Frame Saw cuts planks as the player would.
        RecipeDefinition recipe = RecipeVariants.OpenAt(order.recipe, queue);
        if (recipe == null || !recipe.HasOutput)
        {
            return "nothing to make";
        }

        if (recipe.RequiredStationTier > queue.Tier)
        {
            return $"needs Tier {Tiers.ToRoman(recipe.RequiredStationTier)}";
        }

        if (!queue.HasModule(recipe.NeedsModule))
        {
            return $"needs the {recipe.NeedsModule.DisplayName}";
        }

        if (StockOf(recipe, queue, stash) >= order.stockTarget)
        {
            return "plenty in the stash";
        }

        // What was fetched counts first; Keep Input is left in the stash only for what still comes from it.
        foreach (ItemAmount cost in recipe.ItemCost)
        {
            int fromStash = cost.Item != null ? cost.Amount - queue.Supplied(cost.Item) : 0;
            if (cost.Item != null && fromStash > 0 && stash.Items.Count(cost.Item) < fromStash + order.keepInput)
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

    /// <summary>
    /// What a worker should fetch next for <paramref name="job"/> at <paramref name="queue"/> within <paramref name="limits"/>: for the first
    /// order that can be worked and is short of what was fetched, enough for a handful of crafts (no more than its stock target wants,
    /// leaving Keep Input in the stash). Empty when nothing needs fetching.
    /// </summary>
    public static List<ItemAmount> PlanFetch(JobDefinition job, StationQueue queue, StashStorage stash, CarryLimits limits)
    {
        if (job == null || queue == null)
        {
            return new List<ItemAmount>();
        }

        foreach (WorkOrder order in job.WorkOrders)
        {
            if (WhyNot(order, queue, stash) != null)
            {
                continue;
            }

            RecipeDefinition recipe = RecipeVariants.OpenAt(order.recipe, queue);
            // The first order that can be worked has what it needs: it starts next, so nothing is fetched for a later one.
            if (HasSupplies(recipe, queue))
            {
                return new List<ItemAmount>();
            }

            int each = Mathf.Max(1, recipe.OutputAmount);
            int crafts = Mathf.Clamp(Mathf.CeilToInt((order.stockTarget - StockOf(recipe, queue, stash)) / (float)each), 1, MostCraftsPerTrip);
            int keep = order.keepInput;
            List<ItemAmount> load = WorkerCarry.PlanFetch(recipe.ItemCost, crafts, queue.Supplied,
                item => Mathf.Max(0, stash.Items.Count(item) - keep), limits);
            if (load.Count > 0)
            {
                return load;
            }
        }

        return new List<ItemAmount>();
    }

    /// <summary>
    /// A fetched load arriving at the station: what of it is still in the stash moves to the station's supplies (the player may have
    /// taken some meanwhile). Counts the weight to the worker's Strength. Returns the weight moved.
    /// </summary>
    public static float Deliver(ResidentRecord record, StationQueue queue, StashStorage stash, IReadOnlyList<ItemAmount> load)
    {
        float moved = 0f;
        foreach (ItemAmount part in load)
        {
            int taken = part.Item != null ? stash.Items.Remove(part.Item, Mathf.Min(part.Amount, stash.Items.Count(part.Item))) : 0;
            if (taken > 0)
            {
                queue.AddSupply(part.Item, taken);
                moved += taken * Mathf.Max(0f, part.Item.Weight);
            }
        }

        if (record != null && moved > 0f)
        {
            record.HaulDone += moved;
        }

        return moved;
    }

    /// <summary>
    /// Finished work taken back to the chests on a trip: as much of the station's output as <paramref name="limits"/> carry (not gear,
    /// which is made by whoever collects it). Returns the weight moved.
    /// </summary>
    public static float TakeBack(ResidentRecord record, StationQueue queue, StashStorage stash, CarryLimits limits)
    {
        if (!(queue.Output is ItemDefinition item) || queue.OutputAmount <= 0 || item.KeepsCondition || item.HasDurability)
        {
            return 0f;
        }

        int fits = WorkerCarry.CanTake(item, queue.OutputAmount, limits);
        int moved = fits > 0 ? queue.TakeOutput(stash.Items.Add(item, fits)) : 0;
        NoteGrown(queue, item, moved);
        float weight = moved * Mathf.Max(0f, item.Weight);
        if (record != null && weight > 0f)
        {
            record.HaulDone += weight;
        }

        return weight;
    }

    /// <summary>Whether there is finished work at the station for its worker to take back.</summary>
    /// <summary>
    /// Whether finished work is worth a trip to the chests now (the user, 2026-11-23: "NPC's should automatically move items from crafting
    /// stations output to nearby storage"): the station has stopped (idle or blocked by its full output), or the output already fills a
    /// whole load (<paramref name="load"/>, what one trip carries). Pure, for tests.
    /// </summary>
    public static bool WorthATrip(int outputAmount, int load, bool idle, bool blocked) =>
        outputAmount > 0 && (idle || blocked || (load > 0 && outputAmount >= load));

    public static bool HasWorkToTakeBack(StationQueue queue) =>
        queue != null && queue.Output is ItemDefinition item && queue.OutputAmount > 0 && !item.KeepsCondition && !item.HasDurability;

    /// <summary>
    /// The player away: <paramref name="seconds"/> more towards the worker's next trip; each trip done takes finished work back and brings
    /// inputs over, within what they carry (their hands and a horse at the station; carts stay at home).
    /// </summary>
    public static void TripAway(ResidentRecord record, StationQueue queue, StashStorage stash, float seconds)
    {
        WorkerSettings settings = WorkerSettings.Current;
        string id = record.Resident != null ? record.Resident.Id : "";
        TripClock.TryGetValue(id, out float clock);
        clock += Mathf.Max(0f, seconds);
        float trip = WorkerCarry.AwayTripSeconds(queue.TripMetres > 0f ? queue.TripMetres : settings.AssumedTripDistance, settings);
        CarryLimits limits = WorkerCarry.WithHelp(WorkerCarry.ByHand(record, settings), 0f, queue.HorseHelps, settings);
        for (int trips = 0; clock >= trip && trips < MostTripsAtOnce; trips++)
        {
            List<ItemAmount> load = PlanFetch(record.Job, queue, stash, limits);
            if (load.Count == 0 && !HasWorkToTakeBack(queue))
            {
                // Nothing to carry: the clock waits at one trip, so work coming up is fetched at once.
                clock = trip;
                break;
            }

            clock -= trip;
            TakeBack(record, queue, stash, limits);
            Deliver(record, queue, stash, load);
        }

        TripClock[id] = clock;
    }

    // A recipe's whole need of one item, over every line naming it.
    private static int NeedOf(RecipeDefinition recipe, ItemDefinition item)
    {
        int need = 0;
        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item == item)
            {
                need += cost.Amount;
            }
        }

        return need;
    }

    private static bool HasSupplies(RecipeDefinition recipe, StationQueue queue)
    {
        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item != null && queue.Supplied(cost.Item) < NeedOf(recipe, cost.Item))
            {
                return false;
            }
        }

        return true;
    }

    private static int StockOf(RecipeDefinition recipe, StationQueue queue, StashStorage stash)
    {
        int waiting = queue.Output == recipe.OutputDefinition ? queue.OutputAmount : 0;
        return waiting + (recipe.Output != null ? stash.Items.Count(recipe.Output) : stash.Resources.GetAmount(recipe.OutputResource));
    }

    /// <summary>
    /// The next order that can be worked, paid in full or not at all. With <paramref name="fetchedOnly"/> (a worker fetching on foot or
    /// away) its items come only from what was fetched to the station, so their carrying paces the work; otherwise (no chest yet, no body
    /// at home) what was fetched first and the rest from the stash. A craft never starts short: one that would have let a worker's later
    /// crafts come free. Resources (essence) always come from the stash: nobody carries them.
    /// </summary>
    public static bool StartNext(JobDefinition job, StationQueue queue, StashStorage stash, bool fetchedOnly = false)
    {
        if (job == null)
        {
            return false;
        }

        foreach (WorkOrder order in job.WorkOrders)
        {
            if (WhyNot(order, queue, stash) != null)
            {
                continue;
            }

            RecipeDefinition recipe = RecipeVariants.OpenAt(order.recipe, queue);
            if (!CanPay(recipe, queue, stash, fetchedOnly))
            {
                continue;
            }

            foreach (ItemAmount cost in recipe.ItemCost)
            {
                if (cost.Item == null)
                {
                    continue;
                }

                int fromSupplies = Mathf.Min(cost.Amount, queue.Supplied(cost.Item));
                queue.TakeSupply(cost.Item, fromSupplies);
                if (cost.Amount > fromSupplies)
                {
                    stash.Items.Remove(cost.Item, cost.Amount - fromSupplies);
                }
            }

            foreach (ResourceAmount cost in recipe.ResourceCost)
            {
                stash.Resources.Remove(cost.Resource, cost.Amount);
            }

            queue.Enqueue(recipe);
            queue.WorkerCrafts++;
            return true;
        }

        return false;
    }

    // Whether the recipe's whole cost is there: fetched, and (unless fetched only) the rest in the stash. A recipe naming an item on two
    // lines needs both (NeedOf), as MaterialPool sums them.
    private static bool CanPay(RecipeDefinition recipe, StationQueue queue, StashStorage stash, bool fetchedOnly)
    {
        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item != null && queue.Supplied(cost.Item) + (fetchedOnly ? 0 : stash.Items.Count(cost.Item)) < NeedOf(recipe, cost.Item))
            {
                return false;
            }
        }

        foreach (ResourceAmount cost in recipe.ResourceCost)
        {
            if (cost.Resource != null && stash.Resources.GetAmount(cost.Resource) < cost.Amount)
            {
                return false;
            }
        }

        return true;
    }

    // Anything finished at their station goes to the stash, whoever asked for it, but gear: a piece is made by whoever collects it
    // (StationCrafting.Collect, CraftedWork: its quality, condition and maker rolled there). Carried as it lay in the slot, every piece
    // ordered at a staffed station came Plain, nameless and new, and Fine or Masterwork could never be made there. What won't fit stays.
    private static void Carry(StationQueue queue, StashStorage stash)
    {
        if (queue.Output is ItemDefinition item && queue.OutputAmount > 0 && !item.KeepsCondition && !item.HasDurability)
        {
            NoteGrown(queue, item, queue.TakeOutput(stash.Items.Add(item, queue.OutputAmount)));
        }
    }

    // What grows or is caught at home, for the playtest log (PlaytestLog.CameHome): a farm, a dock, a hive. Stations that turn one
    // thing into another make nothing new and aren't written.
    private static void NoteGrown(StationQueue queue, ItemDefinition item, int amount)
    {
        if (amount > 0 && (queue.Type == CraftingStationType.Farm || queue.Type == CraftingStationType.Dock || queue.Type == CraftingStationType.Beehive))
        {
            PlaytestLogger.Note(PlaytestLog.CameHome, queue.Type.ToString(), amount, item.Id);
        }
    }
}
