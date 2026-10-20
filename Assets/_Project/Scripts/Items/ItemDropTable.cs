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

    public ItemDefinition Item => item;
    public ResourceDefinition Resource => item == null ? resource : null;
    public int MinQuantity => minQuantity;
    public int MaxQuantity => maxQuantity;
    public float Probability => probability;
    /// <summary>The item or resource dropped, for display and lookups.</summary>
    public UnityEngine.Object Drop => item != null ? item : resource;
    public string DropName => item != null ? item.DisplayName : resource != null ? resource.DisplayName : "nothing";

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

    public void Validate()
    {
        minQuantity = Mathf.Max(0, minQuantity);
        maxQuantity = Mathf.Max(minQuantity, maxQuantity);
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

    /// <summary>Rolls the item lines.</summary>
    public List<ItemAmount> Roll()
    {
        var results = new List<ItemAmount>();
        foreach (ItemDropEntry entry in entries)
        {
            if (entry != null && entry.TryRoll(out ItemAmount drop))
            {
                results.Add(drop);
            }
        }

        return results;
    }

    /// <summary>Rolls the resource lines.</summary>
    public List<ResourceAmount> RollResources()
    {
        var results = new List<ResourceAmount>();
        foreach (ItemDropEntry entry in entries)
        {
            if (entry != null && entry.TryRoll(out ResourceAmount drop))
            {
                results.Add(drop);
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