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
    /// <summary>Who made a Fine or Masterwork piece, for the tooltip ("Made by Astrid"). Empty for everything else.</summary>
    public readonly string Maker;
    /// <summary>A caught fish's length in centimetres, the biggest one's when they stack (FishSizes); 0 for everything else.
    /// For show only: it changes nothing about what it is or what it feeds.</summary>
    public readonly float Size;
    private readonly float wear;

    public ItemStack(ItemDefinition item, int count) : this(item, count, 1f)
    {
    }

    public ItemStack(ItemDefinition item, int count, float durability, int empower = 0, EnchantmentDefinition enchantment = null,
        ItemQuality quality = ItemQuality.Plain, string maker = null, float size = 0f)
    {
        bool empty = item == null || count <= 0;
        Item = empty ? null : item;
        Count = empty ? 0 : count;
        // Stored as wear so default(ItemStack) and stacks made without a durability count as undamaged.
        // Gear keeps its durability here; a water container keeps how full it is.
        wear = empty || !item.KeepsCondition ? 0f : 1f - Mathf.Clamp01(durability);
        bool gear = !empty && item.HasDurability;
        Empower = gear ? Mathf.Max(0, empower) : 0;
        // An inscription carries the enchantment it teaches (Inscriptions), though it is no gear.
        Enchantment = gear || (!empty && item.IsInscription) ? enchantment : null;
        Quality = gear ? quality : ItemQuality.Plain;
        Maker = Quality != ItemQuality.Plain && !string.IsNullOrEmpty(maker) ? maker : "";
        Size = empty ? 0f : Mathf.Max(0f, size);
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

    /// <summary>True for worn-out gear: it is kept, but can't be used until repaired. A lantern's condition is its light, not
    /// wear: an empty one is dark, not broken (a firefly fills it, PlayerLantern), so it never counts here.</summary>
    public bool IsBroken => !IsEmpty && Item.HasDurability && !Item.IsLantern && Durability <= 0.0001f;

    /// <summary>True when the item has lost some durability and can be repaired. Never a lantern burning down (see IsBroken).</summary>
    public bool IsDamaged => !IsEmpty && Item.HasDurability && !Item.IsLantern && wear > 0.0001f;

    /// <summary>The same stack with another durability.</summary>
    public ItemStack WithDurability(float durability) => new ItemStack(Item, Count, durability, Empower, Enchantment, Quality, Maker, Size);

    /// <summary>The water in a water container (ItemDefinition.WaterCapacity), 0 for anything else.</summary>
    public float Water => IsEmpty || !Item.HoldsWater ? 0f : Durability * Item.WaterCapacity;

    /// <summary>The same container holding <paramref name="water"/>.</summary>
    /// <summary>In-game hours of light left in a lantern (ItemDefinition.Lantern), 0 for anything else.</summary>
    public float LightHours => IsEmpty || !Item.IsLantern ? 0f : Durability * Item.Lantern.Hours;

    public ItemStack WithLightHours(float hours) =>
        IsEmpty || !Item.IsLantern ? this : WithDurability(Mathf.Clamp01(hours / Item.Lantern.Hours));

    public ItemStack WithWater(float water) =>
        IsEmpty || !Item.HoldsWater ? this : WithDurability(Mathf.Clamp01(water / Item.WaterCapacity));

    /// <summary>The same stack empowered to a level.</summary>
    public ItemStack WithEmpower(int level) => new ItemStack(Item, Count, Durability, level, Enchantment, Quality, Maker, Size);

    /// <summary>The same stack carrying another enchantment. Null takes it away.</summary>
    public ItemStack WithEnchantment(EnchantmentDefinition enchantment) => new ItemStack(Item, Count, Durability, Empower, enchantment, Quality, Maker, Size);

    /// <summary>The same stack made to another quality.</summary>
    public ItemStack WithQuality(ItemQuality quality) => new ItemStack(Item, Count, Durability, Empower, Enchantment, quality, Maker, Size);

    /// <summary>The same stack signed by its maker.</summary>
    public ItemStack WithMaker(string maker) => new ItemStack(Item, Count, Durability, Empower, Enchantment, Quality, maker, Size);

    /// <summary>The same stack with a fish's length (centimetres).</summary>
    public ItemStack WithSize(float centimetres) => new ItemStack(Item, Count, Durability, Empower, Enchantment, Quality, Maker, centimetres);

    /// <summary>The same stack with another count, keeping all the rest.</summary>
    public ItemStack WithCount(int count) => new ItemStack(Item, count, Durability, Empower, Enchantment, Quality, Maker, Size);

    /// <summary>How many more of the same item fit on this stack.</summary>
    public int SpaceLeft => IsEmpty ? 0 : Mathf.Max(0, Item.MaximumStack - Count);
}
