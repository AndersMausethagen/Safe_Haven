using System;
using UnityEngine;

/// <summary>A quantity of one item: a recipe cost, a starting item, a drop result.</summary>
[Serializable]
public struct ItemAmount
{
    [SerializeField] private ItemDefinition item;
    [SerializeField, Min(0)] private int amount;

    public ItemAmount(ItemDefinition item, int amount)
    {
        this.item = item;
        this.amount = amount;
    }

    public ItemDefinition Item => item;
    public int Amount => amount;
}

/// <summary>
/// What one inventory slot holds: an item, how many, and (for tools, weapons and backpacks) how worn it is. An empty slot
/// has no item and a count of 0. Durability runs from 1 (as new) to 0 (broken); items without durability always report 1.
/// </summary>
public readonly struct ItemStack
{
    public static readonly ItemStack Empty = default;

    public readonly ItemDefinition Item;
    public readonly int Count;
    private readonly float wear;

    public ItemStack(ItemDefinition item, int count) : this(item, count, 1f)
    {
    }

    public ItemStack(ItemDefinition item, int count, float durability)
    {
        bool empty = item == null || count <= 0;
        Item = empty ? null : item;
        Count = empty ? 0 : count;
        // Stored as wear so default(ItemStack) and stacks made without a durability count as undamaged.
        wear = empty || !item.HasDurability ? 0f : 1f - Mathf.Clamp01(durability);
    }

    public bool IsEmpty => Item == null;

    /// <summary>1 when as new, 0 when broken. Always 1 for items without durability.</summary>
    public float Durability => 1f - wear;

    /// <summary>True for worn-out gear: it is kept, but can't be used until repaired.</summary>
    public bool IsBroken => !IsEmpty && Item.HasDurability && Durability <= 0.0001f;

    /// <summary>True when the item has lost some durability and can be repaired.</summary>
    public bool IsDamaged => !IsEmpty && Item.HasDurability && wear > 0.0001f;

    /// <summary>The same stack with another durability.</summary>
    public ItemStack WithDurability(float durability) => new ItemStack(Item, Count, durability);

    /// <summary>How many more of the same item fit on this stack.</summary>
    public int SpaceLeft => IsEmpty ? 0 : Mathf.Max(0, Item.MaximumStack - Count);
}
