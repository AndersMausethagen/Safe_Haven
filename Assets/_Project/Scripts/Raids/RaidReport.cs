using System;
using System.Collections.Generic;

/// <summary>One piece of deployed equipment kept after a failed raid, and how worn it was before and after.</summary>
public readonly struct EquipmentWear
{
    public readonly ItemDefinition Item;
    public readonly float Before;
    public readonly float After;

    public EquipmentWear(ItemDefinition item, float before, float after)
    {
        Item = item;
        Before = before;
        After = after;
    }
}

/// <summary>
/// How a raid went, for the summary shown back in Safe Haven: the outcome, how long it lasted, the modifiers used, what
/// the player came home with or lost compared with what they carried in, and – after a failed raid – the deployed
/// equipment that was kept but damaged (shown separately from losses, GDD §8.3).
/// </summary>
public sealed class RaidReport
{
    private readonly List<ItemAmount> itemsGained = new List<ItemAmount>();
    private readonly List<ItemAmount> itemsLost = new List<ItemAmount>();
    private readonly List<ResourceAmount> resourcesGained = new List<ResourceAmount>();
    private readonly List<ResourceAmount> resourcesLost = new List<ResourceAmount>();
    private readonly List<EquipmentWear> equipmentDamaged = new List<EquipmentWear>();

    private RaidReport(RaidSession session)
    {
        Definition = session.Definition;
        RaidName = session.Definition.DisplayName;
        Outcome = session.Outcome;
        TimeSpent = session.Elapsed;
        Modifiers = session.Modifiers;
        SparksUsed = session.SparksUsed;
        ReturnScene = session.ReturnScene;
        Gathering = session.Gathering;
    }

    public RaidDefinition Definition { get; }
    /// <summary>How the player gathered: swings, criticals and the forest's peak annoyance.</summary>
    public GatheringStats Gathering { get; }
    public string RaidName { get; }
    /// <summary>The scene the player goes back to after reading the report.</summary>
    public string ReturnScene { get; }
    public RaidOutcome Outcome { get; }
    public float TimeSpent { get; }
    public IReadOnlyList<RaidModifierDefinition> Modifiers { get; }
    public int SparksUsed { get; }
    public bool Extracted => Outcome == RaidOutcome.Extracted;
    /// <summary>Residents brought home by this raid.</summary>
    public List<ResidentDefinition> Rescued { get; } = new List<ResidentDefinition>();

    /// <summary>Equipment kept after a failed raid, with its durability before and after.</summary>
    public IReadOnlyList<EquipmentWear> EquipmentDamaged => equipmentDamaged;

    /// <summary>Items the player has more of than they carried in: loot.</summary>
    public IReadOnlyList<ItemAmount> ItemsGained => itemsGained;
    /// <summary>Items the player has fewer of: used up, or everything carried after a failed raid.</summary>
    public IReadOnlyList<ItemAmount> ItemsLost => itemsLost;
    public IReadOnlyList<ResourceAmount> ResourcesGained => resourcesGained;
    public IReadOnlyList<ResourceAmount> ResourcesLost => resourcesLost;

    /// <summary>The total value (ItemDefinition.Value) of the items gained: the haul.</summary>
    public int ValueGained => ValueOf(itemsGained);
    /// <summary>The total value of the items lost or used up.</summary>
    public int ValueLost => ValueOf(itemsLost);

    public static int ValueOf(IEnumerable<ItemAmount> items)
    {
        int total = 0;
        foreach (ItemAmount entry in items)
        {
            if (entry.Item != null)
            {
                total += entry.Item.Value * entry.Amount;
            }
        }

        return total;
    }

    /// <param name="before">What the player carried into the raid. Null counts as nothing.</param>
    /// <param name="after">What the player comes home with: everything after an extraction, what is kept after a failure.</param>
    /// <param name="final">
    /// What the player held when the raid ended. Loot found during a failed raid shows as lost only when this is given.
    /// Null counts as the same as after.
    /// </param>
    /// <param name="damaged">Equipment kept with durability damage after a failed raid.</param>
    public static RaidReport Create(RaidSession session, PlayerSnapshot before, PlayerSnapshot after, PlayerSnapshot final = null,
        IEnumerable<EquipmentWear> damaged = null)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        var report = new RaidReport(session);
        if (damaged != null)
        {
            report.equipmentDamaged.AddRange(damaged);
        }

        Fill(Count(before, s => s.CountItems()), Count(after, s => s.CountItems()), Count(final ?? after, s => s.CountItems()),
            (item, amount) => report.itemsGained.Add(new ItemAmount(item, amount)),
            (item, amount) => report.itemsLost.Add(new ItemAmount(item, amount)));
        Fill(Count(before, s => s.CountResources()), Count(after, s => s.CountResources()), Count(final ?? after, s => s.CountResources()),
            (resource, amount) => report.resourcesGained.Add(new ResourceAmount(resource, amount)),
            (resource, amount) => report.resourcesLost.Add(new ResourceAmount(resource, amount)));

        // Rarest first, so the best finds lead the summary.
        Comparison<ItemAmount> byRarityThenName = (a, b) =>
        {
            int rarity = b.Item.Rarity.CompareTo(a.Item.Rarity);
            return rarity != 0 ? rarity : string.Compare(a.Item.DisplayName, b.Item.DisplayName, StringComparison.OrdinalIgnoreCase);
        };
        Comparison<ResourceAmount> byResourceName = (a, b) => string.Compare(a.Resource.DisplayName, b.Resource.DisplayName, StringComparison.OrdinalIgnoreCase);
        report.itemsGained.Sort(byRarityThenName);
        report.itemsLost.Sort(byRarityThenName);
        report.resourcesGained.Sort(byResourceName);
        report.resourcesLost.Sort(byResourceName);
        return report;
    }

    private static Dictionary<T, int> Count<T>(PlayerSnapshot snapshot, Func<PlayerSnapshot, Dictionary<T, int>> counter)
    {
        return snapshot != null ? counter(snapshot) : new Dictionary<T, int>();
    }

    // Gained: what comes home beyond what went in. Lost: the most the player ever held of something (carried in, or held
    // at the end) minus what comes home, so loot found and then lost in a failed raid is listed too.
    private static void Fill<T>(Dictionary<T, int> before, Dictionary<T, int> after, Dictionary<T, int> final, Action<T, int> gained, Action<T, int> lost)
    {
        foreach (KeyValuePair<T, int> entry in after)
        {
            before.TryGetValue(entry.Key, out int had);
            if (entry.Value > had)
            {
                gained(entry.Key, entry.Value - had);
            }
        }

        var peak = new Dictionary<T, int>(before);
        foreach (KeyValuePair<T, int> entry in final)
        {
            peak.TryGetValue(entry.Key, out int had);
            peak[entry.Key] = Math.Max(had, entry.Value);
        }

        foreach (KeyValuePair<T, int> entry in peak)
        {
            after.TryGetValue(entry.Key, out int has);
            if (has < entry.Value)
            {
                lost(entry.Key, entry.Value - has);
            }
        }
    }
}
