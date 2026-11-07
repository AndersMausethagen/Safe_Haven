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
/// What one inventory slot holds: an item, how many, and (for gear) how worn it is, how well it was made (its quality), how
/// far it has been empowered and the enchantment worked into it (GDD §13.5). An empty slot has no item and a count of 0. Durability runs from 1 (as new) to 0
/// (broken); items without durability always report 1. Gear never stacks, so these belong to the one item.
/// </summary>
public readonly struct ItemStack
{
    public static readonly ItemStack Empty = default;

    public readonly ItemDefinition Item;
    public readonly int Count;
    /// <summary>Levels of empowering at the altar, shown as +1 after the name. 0 for everything else.</summary>
    public readonly int Empower;
    public readonly EnchantmentDefinition Enchantment;
    /// <summary>How well it was made: a crafting skill's Fine or Masterwork. Plain for everything else.</summary>
    public readonly ItemQuality Quality;
    private readonly float wear;

    public ItemStack(ItemDefinition item, int count) : this(item, count, 1f)
    {
    }

    public ItemStack(ItemDefinition item, int count, float durability, int empower = 0, EnchantmentDefinition enchantment = null,
        ItemQuality quality = ItemQuality.Plain)
    {
        bool empty = item == null || count <= 0;
        Item = empty ? null : item;
        Count = empty ? 0 : count;
        // Stored as wear so default(ItemStack) and stacks made without a durability count as undamaged.
        // Gear keeps its durability here; a water container keeps how full it is.
        wear = empty || !item.KeepsCondition ? 0f : 1f - Mathf.Clamp01(durability);
        bool gear = !empty && item.HasDurability;
        Empower = gear ? Mathf.Max(0, empower) : 0;
        Enchantment = gear ? enchantment : null;
        Quality = gear ? quality : ItemQuality.Plain;
    }

    public bool IsEmpty => Item == null;

    /// <summary>The item's name with what has been done to it: "Hearthbound Fine Iron Helm +2".</summary>
    public string DisplayName
    {
        get
        {
            if (IsEmpty)
            {
                return "";
            }

            string name = Quality != ItemQuality.Plain ? $"{Quality} {Item.DisplayName}" : Item.DisplayName;
            name = Enchantment != null ? $"{Enchantment.DisplayName} {name}" : name;
            return Empower > 0 ? $"{name} +{Empower}" : name;
        }
    }

    /// <summary>1 when as new, 0 when broken. Always 1 for items without durability.</summary>
    public float Durability => 1f - wear;

    /// <summary>True for worn-out gear: it is kept, but can't be used until repaired.</summary>
    public bool IsBroken => !IsEmpty && Item.HasDurability && Durability <= 0.0001f;

    /// <summary>True when the item has lost some durability and can be repaired.</summary>
    public bool IsDamaged => !IsEmpty && Item.HasDurability && wear > 0.0001f;

    /// <summary>The same stack with another durability.</summary>
    public ItemStack WithDurability(float durability) => new ItemStack(Item, Count, durability, Empower, Enchantment, Quality);

    /// <summary>The water in a water container (ItemDefinition.WaterCapacity), 0 for anything else.</summary>
    public float Water => IsEmpty || !Item.HoldsWater ? 0f : Durability * Item.WaterCapacity;

    /// <summary>The same container holding <paramref name="water"/>.</summary>
    public ItemStack WithWater(float water) =>
        IsEmpty || !Item.HoldsWater ? this : WithDurability(Mathf.Clamp01(water / Item.WaterCapacity));

    /// <summary>The same stack empowered to a level.</summary>
    public ItemStack WithEmpower(int level) => new ItemStack(Item, Count, Durability, level, Enchantment, Quality);

    /// <summary>The same stack carrying another enchantment. Null takes it away.</summary>
    public ItemStack WithEnchantment(EnchantmentDefinition enchantment) => new ItemStack(Item, Count, Durability, Empower, enchantment, Quality);

    /// <summary>The same stack made to another quality.</summary>
    public ItemStack WithQuality(ItemQuality quality) => new ItemStack(Item, Count, Durability, Empower, Enchantment, quality);

    /// <summary>How many more of the same item fit on this stack.</summary>
    public int SpaceLeft => IsEmpty ? 0 : Mathf.Max(0, Item.MaximumStack - Count);
}
