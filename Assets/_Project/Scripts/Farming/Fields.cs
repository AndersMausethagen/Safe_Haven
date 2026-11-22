using UnityEngine;

/// <summary>
/// The fields (the Keeper's view, phase 5; Palworld's plots: sow, water, grow, harvest, haul): a Farm Plot's crop is a recipe paid
/// with its seed (sown by the player or the Farmer from the chests), grows while the soil is watered and slows when it is dry
/// (FarmSettings › Dry Speed, never dead), and waits to be harvested; the Farmer takes it to the chests on their trips. Water comes
/// from the rain at home, a Waterskin when the player uses the plot, or the Farmer, who fetches it for every plot round theirs (at
/// home on foot; away, kept up while they are home). The water is the plot's queue's (StationQueue.Water, saved). Plain rules
/// (FarmingTests); the plot's look is FarmPlotLook.
/// </summary>
public static class Fields
{
    /// <summary>Whether <paramref name="queue"/> is a field's (it needs water).</summary>
    public static bool Grows(StationQueue queue) => queue != null && queue.Type == CraftingStationType.Farm;

    /// <summary>What the soil does to a crop's pace: 1 watered (or not a field), Dry Speed dry.</summary>
    public static float GrowthFactor(StationQueue queue) => !Grows(queue) || queue.Water > 0f ? 1f : FarmSettings.Current.DrySpeed;

    /// <summary>Waters a plot: a full watering's seconds of growing.</summary>
    public static void Fill(StationQueue queue, FarmSettings settings)
    {
        if (Grows(queue))
        {
            queue.Water = settings.WateredSeconds;
        }
    }

    /// <summary>Whether a plot with a crop in it is running low (a Farmer goes for water).</summary>
    public static bool WantsWater(StationQueue queue, FarmSettings settings) =>
        Grows(queue) && !queue.IsIdle && !queue.Blocked && queue.Water < settings.WaterWhenBelow * settings.WateredSeconds;

    /// <summary>Rain at home (its strength, 0-1): strong enough, every plot is watered. Returns how many.</summary>
    public static int Rain(WorkshopState workshops, float rain, FarmSettings settings)
    {
        if (rain < settings.RainWaters)
        {
            return 0;
        }

        int watered = 0;
        foreach (StationQueue queue in workshops.Queues)
        {
            if (Grows(queue) && queue.Water < settings.WateredSeconds)
            {
                Fill(queue, settings);
                watered++;
            }
        }

        return watered;
    }

    /// <summary>Whether <paramref name="record"/> farms: their job's workplace is a Farm Plot, at work and not paused.</summary>
    public static bool Farms(ResidentRecord record)
    {
        if (record == null || record.WorkPaused || record.Job == null || record.Job.Workplace == null)
        {
            return false;
        }

        CraftingStation station = record.Job.Workplace.Prefab != null ? record.Job.Workplace.Prefab.GetComponent<CraftingStation>() : null;
        return station != null && station.Type == CraftingStationType.Farm;
    }

    /// <summary>
    /// The player away (SettlementSystem): a Farmer at home (not the one along) keeps every plot watered, as they would on foot.
    /// Without one the plots dry and their crops slow. Returns whether anyone tended them.
    /// </summary>
    public static bool WorkAway(SettlementState home, ResidentDefinition along, FarmSettings settings)
    {
        foreach (ResidentRecord record in ResidentsOut.Working(home, along))
        {
            if (Farms(record))
            {
                foreach (StationQueue queue in home.Workshops.Queues)
                {
                    if (WantsWater(queue, settings))
                    {
                        Fill(queue, settings);
                    }
                }

                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Waters a plot from a water container in <paramref name="bag"/> (FarmSettings › Water Per Plot of a full one) once it runs low
    /// (Water When Below), sown or not: a plot watered before it is sown starts its crop wet. False, with why, when it can't.
    /// </summary>
    public static bool TryWaterFrom(ItemContainer bag, StationQueue queue, FarmSettings settings, out string why)
    {
        why = null;
        if (!Grows(queue) || queue.Water >= settings.WaterWhenBelow * settings.WateredSeconds || bag == null)
        {
            return false;
        }

        for (int i = 0; i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.HoldsWater && stack.Durability >= settings.WaterPerPlot - 0.001f)
            {
                bag.SetDurability(i, Mathf.Max(0f, stack.Durability - settings.WaterPerPlot));
                Fill(queue, settings);
                return true;
            }
        }

        // Said only when a crop is in dry soil: an empty plot or a damp one needs nothing yet.
        why = queue.Water <= 0f && !queue.IsIdle
            ? "The soil is dry: fill a Waterskin at a well or stream to water it, or let the rain or a Farmer see to it." : null;
        return false;
    }

    /// <summary>A short line on the plot's water, for its window; empty for anything else.</summary>
    public static string WaterLine(StationQueue queue, FarmSettings settings)
    {
        if (!Grows(queue))
        {
            return "";
        }

        if (queue.Water > 0f)
        {
            int minutes = Mathf.Max(1, Mathf.CeilToInt(queue.Water / 60f));
            return queue.IsIdle ? "Watered, ready for seed." : $"Watered: it grows at its pace for {minutes} more minute{(minutes == 1 ? "" : "s")}.";
        }

        return queue.IsIdle ? "" : $"Dry: it grows at {Mathf.RoundToInt(settings.DrySpeed * 100f)}% until it is watered.";
    }
}
