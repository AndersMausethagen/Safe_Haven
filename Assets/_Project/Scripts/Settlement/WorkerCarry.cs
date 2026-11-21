using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a resident can carry on one trip: weight, and kinds of thing at once.</summary>
public readonly struct CarryLimits
{
    public readonly float Weight;
    public readonly int Slots;

    public CarryLimits(float weight, int slots)
    {
        Weight = Mathf.Max(0f, weight);
        Slots = Mathf.Max(1, slots);
    }

    /// <summary>The larger of each (a cart's weight with a worker's own kinds, or the other way round).</summary>
    public CarryLimits AtLeast(float weight, int slots) => new CarryLimits(Mathf.Max(Weight, weight), Mathf.Max(Slots, slots));
}

/// <summary>
/// The rules of residents' carrying (WorkerSettings; the user, 2026-11-21: "reduced carry capacity and slots... train and upgrade the
/// NPCs with equipment or by having them with us in raids"): how much they carry by hand (their Strength from hauling and raids, and
/// their worker gear), with help (a cart taken along, a horse at the station), and what to bring for their next crafts within it.
/// Plain C# (WorkerCarryTests); WorkerProduction and ResidentLife use it.
/// </summary>
public static class WorkerCarry
{
    /// <summary>Their Strength (0 up): weight hauled and raids gone along on.</summary>
    public static int Strength(ResidentRecord record, WorkerSettings settings) =>
        record == null ? 0 : settings.StrengthFor(record.HaulDone, record.RaidsAlong);

    /// <summary>What they carry by hand: WorkerSettings › Carry Weight and Slots, their Strength, and their worker gear.</summary>
    public static CarryLimits ByHand(ResidentRecord record, WorkerSettings settings)
    {
        int strength = Strength(record, settings);
        ItemDefinition gear = record != null ? record.WorkGear : null;
        float weight = settings.CarryWeight + strength * settings.WeightPerStrength + (gear != null ? gear.WorkerCarryWeight : 0f);
        int slots = settings.SlotsAt(strength) + (gear != null ? gear.WorkerCarrySlots : 0);
        return new CarryLimits(weight, slots);
    }

    /// <summary>
    /// What they carry with help: a cart taken along (<paramref name="cartCapacity"/> weight, WorkerSettings › Cart Slots kinds; 0 for
    /// none), and a horse working at their station (its packs: Horse Carry times the weight, one more kind).
    /// </summary>
    public static CarryLimits WithHelp(CarryLimits hand, float cartCapacity, bool horse, WorkerSettings settings)
    {
        CarryLimits limits = cartCapacity > 0f ? hand.AtLeast(cartCapacity, settings.CartSlots) : hand;
        return horse ? new CarryLimits(limits.Weight * settings.HorseCarry, limits.Slots + 1) : limits;
    }

    /// <summary>
    /// What to fetch for <paramref name="costs"/> (one craft's items): enough for as many crafts as fit, up to <paramref name="crafts"/>,
    /// less what is <paramref name="supplied"/> at the station already, no more than <paramref name="inStash"/> holds, within the
    /// weight and kinds of <paramref name="limits"/>. When not even one craft fits, a start on it: the first kinds still wanted, as many
    /// as the weight allows, so a heavy order is fetched over several trips. Empty when nothing is wanted or the stash has none.
    /// </summary>
    public static List<ItemAmount> PlanFetch(IReadOnlyList<ItemAmount> costs, int crafts, Func<ItemDefinition, int> supplied,
        Func<ItemDefinition, int> inStash, CarryLimits limits)
    {
        for (int count = Mathf.Max(1, crafts); count >= 1; count--)
        {
            List<ItemAmount> wanted = Wanted(costs, count, supplied, inStash, out bool short_);
            if (short_ && count > 1)
            {
                continue;
            }

            if (wanted.Count <= limits.Slots && WeightOf(wanted) <= limits.Weight + 0.001f)
            {
                return wanted;
            }
        }

        // Not one craft fits: a start on it, by kinds in order and by weight.
        List<ItemAmount> first = Wanted(costs, 1, supplied, inStash, out _);
        var load = new List<ItemAmount>();
        float room = limits.Weight;
        foreach (ItemAmount want in first)
        {
            if (load.Count >= limits.Slots || room <= 0f)
            {
                break;
            }

            float each = Mathf.Max(0f, want.Item.Weight);
            int fits = each <= 0f ? want.Amount : Mathf.Min(want.Amount, Mathf.FloorToInt((room + 0.001f) / each));
            if (fits <= 0)
            {
                continue;
            }

            load.Add(new ItemAmount(want.Item, fits));
            room -= fits * each;
        }

        return load;
    }

    /// <summary>How many of <paramref name="amount"/> <paramref name="item"/> they can take on a trip (finished work back to the chests).</summary>
    public static int CanTake(ItemDefinition item, int amount, CarryLimits limits)
    {
        if (item == null || amount <= 0)
        {
            return 0;
        }

        float each = Mathf.Max(0f, item.Weight);
        return each <= 0f ? amount : Mathf.Min(amount, Mathf.FloorToInt((limits.Weight + 0.001f) / each));
    }

    /// <summary>The weight of a load.</summary>
    public static float WeightOf(IReadOnlyList<ItemAmount> load)
    {
        float weight = 0f;
        foreach (ItemAmount part in load)
        {
            weight += part.Item != null ? Mathf.Max(0f, part.Item.Weight) * part.Amount : 0f;
        }

        return weight;
    }

    /// <summary>Seconds a trip takes while the player is away: there and back at the away walk, and the handling.</summary>
    public static float AwayTripSeconds(float metres, WorkerSettings settings) =>
        Mathf.Max(1f, metres) * 2f / settings.AwayWalkSpeed + settings.TripHandling;

    // Each item of `count` crafts still to fetch (less what is supplied), capped by the stash; `short_` when the stash can't cover them.
    private static List<ItemAmount> Wanted(IReadOnlyList<ItemAmount> costs, int count, Func<ItemDefinition, int> supplied,
        Func<ItemDefinition, int> inStash, out bool short_)
    {
        short_ = false;
        var wanted = new List<ItemAmount>();
        foreach (ItemAmount cost in costs)
        {
            if (cost.Item == null || cost.Amount <= 0)
            {
                continue;
            }

            int need = cost.Amount * count - (supplied != null ? supplied(cost.Item) : 0);
            if (need <= 0)
            {
                continue;
            }

            int have = inStash != null ? inStash(cost.Item) : need;
            if (have < need)
            {
                short_ = true;
            }

            int take = Mathf.Min(need, have);
            if (take > 0)
            {
                wanted.Add(new ItemAmount(cost.Item, take));
            }
        }

        return wanted;
    }
}
