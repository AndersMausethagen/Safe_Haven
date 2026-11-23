using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// One line of a loot table: an item or a resource (set one), a quantity range and an optional chance. Resources are how
/// monsters drop hides, fangs and Cursed Essence (GDD §12.5): they go to the resource bars, items go to the bag.
/// </summary>
[Serializable]
public sealed class ItemDropEntry
{
    [Tooltip("The item dropped. Leave empty to drop a resource instead.")]
    [SerializeField] private ItemDefinition item;
    [Tooltip("The resource dropped when no item is set, such as Cursed Essence or a hide.")]
    [SerializeField] private ResourceDefinition resource;
    [SerializeField, Min(0)] private int minQuantity = 1;
    [SerializeField, Min(0)] private int maxQuantity = 1;
    [Tooltip("Chance from 0 to 1 that this entry drops at all. 1 always drops.")]
    [SerializeField, Range(0f, 1f)] private float probability = 1f;
    [Tooltip("The lowest raid tier this drops at. 0: any tier, and outside raids too; a row with a From Tier never drops outside a raid.")]
    [SerializeField, Range(0, Tiers.Max)] private int fromTier;
    [Tooltip("The highest raid tier this drops at. 0: no limit.")]
    [SerializeField, Range(0, Tiers.Max)] private int toTier;
    [Tooltip("Never scaled by the raid's loot multiplier: a trophy stays one trophy at every tier.")]
    [SerializeField] private bool fixedAmount;
    [Tooltip("Drops only on this raid's map (Cursed Sap from the Misty Woods' trees, not the Haven's). Empty: anywhere.")]
    [SerializeField] private RaidDefinition onlyInRaid;

    public ItemDefinition Item => item;
    public RaidDefinition OnlyInRaid => onlyInRaid;

    /// <summary>False on a map other than its Only In Raid (and at home, when it has one).</summary>
    public bool DropsHere => onlyInRaid == null || (GameSession.ActiveRaid != null && GameSession.ActiveRaid.Definition == onlyInRaid);
    public ResourceDefinition Resource => item == null ? resource : null;
    public int MinQuantity => minQuantity;
    public int MaxQuantity => maxQuantity;
    public float Probability => probability;
    public int FromTier => fromTier;
    public int ToTier => toTier;
    public bool FixedAmount => fixedAmount;
    /// <summary>The item or resource dropped, for display and lookups.</summary>
    public UnityEngine.Object Drop => item != null ? item : resource;
    public string DropName => item != null ? item.DisplayName : resource != null ? resource.DisplayName : "nothing";

    /// <summary>
    /// Whether this row can drop at a raid tier; tier 0 is outside a raid, where only rows without a From Tier drop.
    /// </summary>
    public bool AppearsAt(int tier) => tier <= 0 ? fromTier <= 0 : (fromTier <= 0 || tier >= fromTier) && (toTier <= 0 || tier <= toTier);

    /// <summary>The average amount one roll gives, chance included, before any multiplier.</summary>
    public float AverageAmount => probability * (Mathf.Min(minQuantity, maxQuantity) + Mathf.Max(minQuantity, maxQuantity)) * 0.5f;

    /// <summary>Rolls the chance and then the quantity of an item entry. Returns false when nothing drops.</summary>
    public bool TryRoll(out ItemAmount drop)
    {
        drop = default;
        if (item == null || !TryRollQuantity(out int quantity))
        {
            return false;
        }

        drop = new ItemAmount(item, quantity);
        return true;
    }

    /// <summary>Rolls the chance and then the quantity of a resource entry. Returns false when nothing drops.</summary>
    public bool TryRoll(out ResourceAmount drop)
    {
        drop = default;
        if (item != null || resource == null || !TryRollQuantity(out int quantity))
        {
            return false;
        }

        drop = new ResourceAmount(resource, quantity);
        return true;
    }

    private bool TryRollQuantity(out int quantity)
    {
        quantity = 0;
        if (probability < 1f && Random.value >= probability)
        {
            return false;
        }

        quantity = Random.Range(Mathf.Min(minQuantity, maxQuantity), Mathf.Max(minQuantity, maxQuantity) + 1);
        return quantity > 0;
    }

    /// <summary>A rolled quantity scaled by a raid's loot multiplier, unless the row keeps a fixed amount.</summary>
    public int Scaled(int quantity, float multiplier, float roll) =>
        fixedAmount || Mathf.Approximately(multiplier, 1f) ? quantity : RaidParameters.ScaleAmount(quantity, multiplier, roll);

    public void Validate()
    {
        minQuantity = Mathf.Max(0, minQuantity);
        maxQuantity = Mathf.Max(minQuantity, maxQuantity);
        if (toTier > 0 && fromTier > toTier)
        {
            toTier = fromTier;
        }
    }
}

/// <summary>
/// A loot table: what a monster carries, what a chest holds. Each line drops an item or a resource. It only generates
/// loot and knows nothing about inventories, so whoever rolls it decides where things go (see LootGrant).
/// </summary>
[Serializable]
public sealed class ItemDropTable
{
    [SerializeField] private List<ItemDropEntry> entries = new List<ItemDropEntry>();

    public IReadOnlyList<ItemDropEntry> Entries => entries;

    /// <summary>Rolls the item lines that drop outside a raid (those without a From Tier), unscaled.</summary>
    public List<ItemAmount> Roll() => Roll(0, 1f);

    /// <summary>Rolls the resource lines that drop outside a raid (those without a From Tier), unscaled.</summary>
    public List<ResourceAmount> RollResources() => RollResources(0, 1f);

    /// <summary>
    /// Rolls the item lines that drop at a raid tier (0: outside a raid), each amount scaled by the multiplier unless its row keeps a
    /// fixed amount. Adds to <paramref name="results"/> when given, so several tables can roll into one list.
    /// </summary>
    public List<ItemAmount> Roll(int tier, float multiplier, List<ItemAmount> results = null)
    {
        results ??= new List<ItemAmount>();
        foreach (ItemDropEntry entry in entries)
        {
            if (entry != null && entry.AppearsAt(tier) && entry.DropsHere && entry.TryRoll(out ItemAmount drop))
            {
                int amount = entry.Scaled(drop.Amount, multiplier, Random.value);
                if (amount > 0)
                {
                    results.Add(new ItemAmount(drop.Item, amount));
                }
            }
        }

        return results;
    }

    /// <summary>The resource lines that drop at a raid tier, as <see cref="Roll(int, float, List{ItemAmount})"/>.</summary>
    public List<ResourceAmount> RollResources(int tier, float multiplier, List<ResourceAmount> results = null)
    {
        results ??= new List<ResourceAmount>();
        foreach (ItemDropEntry entry in entries)
        {
            if (entry != null && entry.AppearsAt(tier) && entry.DropsHere && entry.TryRoll(out ResourceAmount drop))
            {
                int amount = entry.Scaled(drop.Amount, multiplier, Random.value);
                if (amount > 0)
                {
                    results.Add(new ResourceAmount(drop.Resource, amount));
                }
            }
        }

        return results;
    }

    public void Validate()
    {
        foreach (ItemDropEntry entry in entries)
        {
            entry?.Validate();
        }
    }
}