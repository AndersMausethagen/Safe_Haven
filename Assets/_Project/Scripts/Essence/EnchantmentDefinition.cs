using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Which of the two essences an enchantment is worked with (GDD §13.5).</summary>
public enum EssenceSchool
{
    /// <summary>Safe: paid in Purified Essence, with no drawback.</summary>
    Purified = 0,
    /// <summary>Raw: paid in Cursed Essence straight from the raid. Stronger, and always with a stated price.</summary>
    Cursed = 1,
}

/// <summary>What gear an enchantment can be worked into. Serialized as numbers, so never renumber.</summary>
[Flags]
public enum EnchantTarget
{
    None = 0,
    Weapon = 1,
    Armor = 2,
    Jewelry = 4,
    Tool = 8,
}

/// <summary>
/// One enchantment a piece of gear can carry (GDD §13.5): its name, the school of essence behind it, what it fits, what it
/// costs and the stats it changes while the gear is worn. A drawback is simply a negative bonus, so the tooltip and the
/// altar state it in the same words. An item carries at most one; which one is runtime state on its ItemStack. Listed in
/// EssenceSettings, and saved by Id.
/// </summary>
[CreateAssetMenu(fileName = "NewEnchantment", menuName = "Safe Haven/Essence/Enchantment")]
public sealed class EnchantmentDefinition : ScriptableObject
{
    [Tooltip("Stable unique identifier, used for saves.")]
    [SerializeField] private string id;
    [Tooltip("Goes before the item's name: Hearthbound Iron Helm. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 3)] private string description;
    [SerializeField] private EssenceSchool school;
    [SerializeField] private EnchantTarget targets;
    [SerializeField] private List<ResourceAmount> cost = new List<ResourceAmount>();
    [Tooltip("Added while the enchanted gear is worn and not broken. Negative values are its drawback.")]
    [SerializeField] private List<StatBonus> bonuses = new List<StatBonus>();

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public EssenceSchool School => school;
    public EnchantTarget Targets => targets;
    public IReadOnlyList<ResourceAmount> Cost => cost;
    public IReadOnlyList<StatBonus> Bonuses => bonuses;

    public bool Fits(ItemDefinition item) => (TargetOf(item) & targets) != EnchantTarget.None;

    /// <summary>What kind of gear an item counts as here, or None for gear that takes no enchantment (backpacks).</summary>
    public static EnchantTarget TargetOf(ItemDefinition item)
    {
        if (item == null)
        {
            return EnchantTarget.None;
        }

        if (item.IsWeapon)
        {
            return EnchantTarget.Weapon;
        }

        if (item.ArmorSlot == ArmorSlot.Neck || item.ArmorSlot == ArmorSlot.Finger)
        {
            return EnchantTarget.Jewelry;
        }

        if (item.IsArmor)
        {
            return EnchantTarget.Armor;
        }

        return item.IsTool ? EnchantTarget.Tool : EnchantTarget.None;
    }
}
