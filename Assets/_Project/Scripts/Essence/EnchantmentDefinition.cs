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
    /// <summary>A relic's power: found in raids only, never worked at the altar (RelicSettings).</summary>
    Relic = 2,
}

/// <summary>
/// What gear an enchantment can be worked into. Serialized as numbers, so never renumber. The first four are kinds of gear; the
/// rest name one slot or one kind of weapon, so an enchantment can be made for boots alone, or for a bow (Docs/EnchantingResearch.md).
/// </summary>
[Flags]
public enum EnchantTarget
{
    None = 0,
    /// <summary>A weapon in hand: swords, axes, maces, spears, bows, staves. Not a shield (since 2026-11-15).</summary>
    Weapon = 1,
    /// <summary>Any worn armour.</summary>
    Armor = 2,
    /// <summary>Amulets and rings.</summary>
    Jewelry = 4,
    Tool = 8,
    Head = 16,
    Chest = 32,
    Hands = 64,
    Waist = 128,
    Feet = 256,
    Neck = 512,
    Ring = 1024,
    /// <summary>A shield in the off hand.</summary>
    Shield = 2048,
    Bow = 4096,
    /// <summary>A staff that casts (Signature Channel).</summary>
    Staff = 8192,
}

/// <summary>Where an enchantment's inscription is found (Docs/EnchantingResearch.md #3). Serialized as numbers: add at the end.</summary>
public enum EnchantmentSource
{
    /// <summary>Known from the start of a new game.</summary>
    Start = 0,
    /// <summary>A rubbing of a carved stone in a raid, on a Blank Parchment.</summary>
    CarvedStone = 1,
    /// <summary>An elite carries the inscription of its own mark (Elite Affix › Inscription).</summary>
    Elite = 2,
    /// <summary>A great creature's own (Loot On Death › Inscriptions): the first kills always give one not yet known.</summary>
    Boss = 3,
    /// <summary>Found: supply caches and the woods' strange events.</summary>
    Found = 4,
}

/// <summary>
/// One enchantment a piece of gear can carry (GDD §13.5): its name, the school of essence behind it, what it fits, what it
/// costs and the stats it changes while the gear is worn. A drawback is simply a negative bonus, so the tooltip and the
/// altar state it in the same words. An item carries at most one; which one is runtime state on its ItemStack. Listed in
/// EssenceSettings, and saved by Id.
/// <para>
/// Learned, not bought (Docs/EnchantingResearch.md): a new game knows the ones marked Known From Start; the rest are learned from
/// an Inscription (a parchment carrying it, read at home), found where Source says from raids of Tier and up. Carving it needs
/// Runecraft (Required Runecraft), or Liv's hand at home. A great enchantment changes how something plays (its Effects) rather
/// than a number, and fits only weapons, shields and jewellery. However many pieces carry it, it counts once (GearSet).
/// </para>
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
    [Tooltip("What it does beyond numbers while worn: a relic's power, or a great enchantment's.")]
    [SerializeReference] private List<RelicEffect> effects = new List<RelicEffect>();

    [Header("Learning")]
    [Tooltip("A new game knows it: the safe school worked at home.")]
    [SerializeField] private bool knownFromStart;
    [Tooltip("Saves from before inscriptions know it, so nobody loses what they could carve (the first fourteen).")]
    [SerializeField] private bool knownInOldSaves;
    [Tooltip("Where its inscription is found.")]
    [SerializeField] private EnchantmentSource source;
    [Tooltip("The lowest raid tier whose stones, caches and elites hand its inscription out; also its tier for Runecraft practice.")]
    [SerializeField, Range(1, Tiers.Max)] private int tier = 1;
    [Tooltip("The Runecraft level needed to carve it (Liv carves past yours by her rank). 0: anyone.")]
    [SerializeField, Min(0)] private int requiredRunecraft;
    [Tooltip("A great enchantment: it changes how something plays (Effects). Weapons, shields and jewellery only.")]
    [SerializeField] private bool great;
    [Tooltip("Where to look, for the altar's unknown rows: \"Elites marked Vengeful (tier III)\". Empty: written from Source and Tier.")]
    [SerializeField] private string sourceHint;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public EssenceSchool School => school;
    public EnchantTarget Targets => targets;
    public IReadOnlyList<ResourceAmount> Cost => cost;
    public IReadOnlyList<StatBonus> Bonuses => bonuses;
    public IReadOnlyList<RelicEffect> Effects => effects;
    public bool KnownFromStart => knownFromStart;
    public bool KnownInOldSaves => knownInOldSaves;
    public EnchantmentSource Source => source;
    public int Tier => Mathf.Clamp(tier, 1, Tiers.Max);
    public int RequiredRunecraft => requiredRunecraft;
    public bool IsGreat => great;

    public bool IsRelic => school == EssenceSchool.Relic;

    /// <summary>Where its inscription is found, in words for the altar.</summary>
    public string SourceHint
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(sourceHint))
            {
                return sourceHint;
            }

            string from = $"tier {Tiers.ToRoman(Tier)}";
            switch (source)
            {
                case EnchantmentSource.Start: return "Known from the start";
                case EnchantmentSource.CarvedStone: return $"A rubbing of a carved stone ({from} and up)";
                case EnchantmentSource.Elite: return $"Carried by elites ({from} and up)";
                case EnchantmentSource.Boss: return "Carried by a great creature";
                default: return $"Found in caches and strange places ({from} and up)";
            }
        }
    }

    public bool Fits(ItemDefinition item) => (TargetOf(item) & targets) != EnchantTarget.None;

    /// <summary>
    /// What gear an item counts as here: its kind (Weapon, Armor, Jewelry, Tool) and its slot or kind of weapon, or None for gear that
    /// takes no enchantment (backpacks). A shield is a Shield, not a Weapon: one weapon enchantment per hand-to-hand fighter.
    /// </summary>
    public static EnchantTarget TargetOf(ItemDefinition item)
    {
        if (item == null)
        {
            return EnchantTarget.None;
        }

        if (item.IsWeapon)
        {
            WeaponDefinition weapon = item.Weapon;
            if (weapon.IsShield)
            {
                return EnchantTarget.Shield;
            }

            if (weapon.IsBow)
            {
                return EnchantTarget.Weapon | EnchantTarget.Bow;
            }

            return weapon.Signature == WeaponSignature.Channel ? EnchantTarget.Weapon | EnchantTarget.Staff : EnchantTarget.Weapon;
        }

        switch (item.ArmorSlot)
        {
            case ArmorSlot.Neck: return EnchantTarget.Jewelry | EnchantTarget.Neck;
            case ArmorSlot.Finger: return EnchantTarget.Jewelry | EnchantTarget.Ring;
        }

        if (item.IsArmor)
        {
            switch (item.ArmorSlot)
            {
                case ArmorSlot.Head: return EnchantTarget.Armor | EnchantTarget.Head;
                case ArmorSlot.Chest: return EnchantTarget.Armor | EnchantTarget.Chest;
                case ArmorSlot.Hands: return EnchantTarget.Armor | EnchantTarget.Hands;
                case ArmorSlot.Waist: return EnchantTarget.Armor | EnchantTarget.Waist;
                case ArmorSlot.Feet: return EnchantTarget.Armor | EnchantTarget.Feet;
                default: return EnchantTarget.Armor;
            }
        }

        return item.IsTool ? EnchantTarget.Tool : EnchantTarget.None;
    }

    /// <summary>What it fits, in words: "boots", "weapons and shields".</summary>
    public string DescribeTargets()
    {
        var parts = new List<string>();
        void Add(EnchantTarget flag, string words)
        {
            if ((targets & flag) != 0)
            {
                parts.Add(words);
            }
        }

        Add(EnchantTarget.Weapon, "weapons");
        Add(EnchantTarget.Bow, "bows");
        Add(EnchantTarget.Staff, "staves");
        Add(EnchantTarget.Shield, "shields");
        Add(EnchantTarget.Armor, "armour");
        Add(EnchantTarget.Head, "helmets");
        Add(EnchantTarget.Chest, "body armour");
        Add(EnchantTarget.Hands, "gloves");
        Add(EnchantTarget.Waist, "belts");
        Add(EnchantTarget.Feet, "boots");
        Add(EnchantTarget.Jewelry, "jewellery");
        Add(EnchantTarget.Neck, "amulets");
        Add(EnchantTarget.Ring, "rings");
        Add(EnchantTarget.Tool, "tools");
        return parts.Count == 0 ? "nothing" : parts.Count == 1 ? parts[0] : string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " and " + parts[parts.Count - 1];
    }
}
