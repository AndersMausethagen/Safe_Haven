using UnityEngine;

/// <summary>
/// Where on the body a piece of armor or jewelry is worn. Set on the ItemDefinition. Values are serialized as numbers,
/// so add new ones at the end and never renumber.
/// </summary>
public enum ArmorSlot
{
    None = 0,
    Head = 1,
    /// <summary>Amulets.</summary>
    Neck = 2,
    /// <summary>Retired: nothing is worn here any more (see GearSlots.IsRetired).</summary>
    Shoulders = 3,
    /// <summary>Retired.</summary>
    Back = 4,
    /// <summary>The body: torso and legs.</summary>
    Chest = 5,
    /// <summary>Retired.</summary>
    Wrists = 6,
    Hands = 7,
    /// <summary>Belts (the buckle carries stats).</summary>
    Waist = 8,
    /// <summary>Retired: the body covers the legs.</summary>
    Legs = 9,
    Feet = 10,
    /// <summary>Rings: fit either finger slot.</summary>
    Finger = 11,
}

/// <summary>
/// The player's gear slots (the character screen, key C). Each value is a slot index in PlayerEquipment.Gear and in
/// saves, so add new slots at the end and never renumber.
/// </summary>
public enum GearSlot
{
    Head = 0,
    Neck = 1,
    Shoulders = 2,
    Back = 3,
    Chest = 4,
    Wrists = 5,
    Hands = 6,
    Waist = 7,
    Legs = 8,
    Feet = 9,
    Finger1 = 10,
    Finger2 = 11,
    MainHand = 12,
    Backpack = 13,
    AxeTool = 14,
    PickaxeTool = 15,
    SickleTool = 16,
    OffHand = 17,
}

/// <summary>
/// The rules of the gear slots: what fits where, and their names. Worn armor uses seven slots: Head, Body (Chest),
/// Hands, Feet, Belt (Waist), Amulet (Neck) and one Ring (Finger1). Shoulders, Back, Wrists, Legs and the second ring
/// are retired: they keep their numbers (saves store slots by number) but take nothing, and gear found there in an old
/// save goes to the bag (PlayerEquipment.Restore).
/// </summary>
public static class GearSlots
{
    /// <summary>How many gear slots there are, retired ones included.</summary>
    public const int Count = 18;

    /// <summary>Slots that no longer take anything.</summary>
    public static bool IsRetired(GearSlot slot) =>
        slot == GearSlot.Shoulders || slot == GearSlot.Back || slot == GearSlot.Wrists || slot == GearSlot.Legs || slot == GearSlot.Finger2;

    /// <summary>Whether the item can go into the gear slot.</summary>
    public static bool Accepts(GearSlot slot, ItemDefinition item)
    {
        if (item == null || IsRetired(slot))
        {
            return false;
        }

        switch (slot)
        {
            case GearSlot.MainHand:
                return item.IsWeapon;
            case GearSlot.OffHand:
                return item.IsWeapon && item.Weapon.CanUseOffHand;
            case GearSlot.Backpack:
                return item.IsBackpack;
            case GearSlot.AxeTool:
                return item.IsTool && item.ToolType == ToolType.Axe;
            case GearSlot.PickaxeTool:
                return item.IsTool && item.ToolType == ToolType.Pickaxe;
            case GearSlot.SickleTool:
                return item.IsTool && item.ToolType == ToolType.Sickle;
            case GearSlot.Finger1:
            case GearSlot.Finger2:
                return item.ArmorSlot == ArmorSlot.Finger;
            default:
                return item.ArmorSlot != ArmorSlot.None && item.ArmorSlot != ArmorSlot.Finger && (int)item.ArmorSlot - 1 == (int)slot;
        }
    }

    /// <summary>Index-based form of Accepts, used as the gear container's slot rule.</summary>
    public static bool Accepts(int index, ItemDefinition item) => index >= 0 && index < Count && Accepts((GearSlot)index, item);

    /// <summary>
    /// The slot an item goes into when equipped: the first matching slot that is empty, or the first matching slot when
    /// all are taken (equipping then swaps). False when the item isn't gear.
    /// </summary>
    public static bool TryFindSlot(ItemDefinition item, ItemContainer gear, out GearSlot slot)
    {
        bool found = false;
        slot = GearSlot.Head;
        if (Accepts(GearSlot.OffHand, item) && (gear == null || gear.GetSlot((int)GearSlot.OffHand).IsEmpty))
        {
            slot = GearSlot.OffHand;
            return true;
        }
        for (int i = 0; i < Count; i++)
        {
            if (!Accepts((GearSlot)i, item))
            {
                continue;
            }

            if (gear == null || gear.GetSlot(i).IsEmpty)
            {
                slot = (GearSlot)i;
                return true;
            }

            if (!found)
            {
                slot = (GearSlot)i;
                found = true;
            }
        }

        return found;
    }

    /// <summary>The tool belt slot for a kind of tool.</summary>
    public static bool TryGetToolSlot(ToolType tool, out GearSlot slot)
    {
        switch (tool)
        {
            case ToolType.Axe:
                slot = GearSlot.AxeTool;
                return true;
            case ToolType.Pickaxe:
                slot = GearSlot.PickaxeTool;
                return true;
            case ToolType.Sickle:
                slot = GearSlot.SickleTool;
                return true;
            default:
                slot = GearSlot.Head;
                return false;
        }
    }

    public static string Label(GearSlot slot)
    {
        switch (slot)
        {
            case GearSlot.Finger1:
            case GearSlot.Finger2:
                return "Ring";
            case GearSlot.Chest:
                return "Body";
            case GearSlot.Waist:
                return "Belt";
            case GearSlot.Neck:
                return "Amulet";
            case GearSlot.MainHand:
                return "Main Hand";
            case GearSlot.OffHand:
                return "Off Hand";
            case GearSlot.AxeTool:
                return "Axe";
            case GearSlot.PickaxeTool:
                return "Pickaxe";
            case GearSlot.SickleTool:
                return "Sickle";
            default:
                return slot.ToString();
        }
    }

    /// <summary>Human name of the slots an item fits, for tooltips. Empty for items that aren't gear.</summary>
    public static string Describe(ItemDefinition item)
    {
        if (item == null)
        {
            return "";
        }

        if (item.ArmorSlot != ArmorSlot.None)
        {
            return item.ArmorSlot.ToString();
        }

        if (item.IsWeapon)
        {
            return item.Weapon.CanUseOffHand ? "Main Hand / Off Hand" : item.Weapon.RequiresBothHands ? "Both Hands" : "Main Hand";
        }

        if (item.IsBackpack)
        {
            return "Backpack";
        }

        return item.IsTool && TryGetToolSlot(item.ToolType, out GearSlot tool) ? $"Tool belt ({Label(tool)})" : "";
    }
}

/// <summary>One stat an item gives while equipped, such as +20 Armor or +5% Max Health.</summary>
[System.Serializable]
public struct StatBonus
{
    public StatType stat;
    public StatModifierOp op;
    public float value;

    public StatBonus(StatType stat, float value, StatModifierOp op = StatModifierOp.Flat)
    {
        this.stat = stat;
        this.op = op;
        this.value = value;
    }

    /// <summary>"+20 Armor", "+5% Max Health", "+10% Fire Resistance".</summary>
    public string Describe()
    {
        bool fraction = op == StatModifierOp.Percent || StatNames.IsFraction(stat);
        string number = fraction ? $"{Mathf.RoundToInt(value * 100f)}%" : $"{value:0.#}";
        return $"{(value >= 0f ? "+" : "")}{number} {StatNames.Of(stat)}";
    }
}

/// <summary>Display names for stats.</summary>
public static class StatNames
{
    public static string Of(StatType stat)
    {
        switch (stat)
        {
            case StatType.MaxHealth: return "Health";
            case StatType.PhysicalPower: return "Physical Power";
            case StatType.SpellPower: return "Spell Power";
            case StatType.PhysicalCritChance: return "Physical Crit Chance";
            case StatType.PhysicalCritDamage: return "Physical Crit Damage";
            case StatType.SpellCritChance: return "Spell Crit Chance";
            case StatType.SpellCritDamage: return "Spell Crit Damage";
            case StatType.PhysicalResistance: return "Physical Resistance";
            case StatType.FireResistance: return "Fire Resistance";
            case StatType.FrostResistance: return "Frost Resistance";
            case StatType.HolyResistance: return "Holy Resistance";
            case StatType.StructureDamage: return "Structure Damage";
            case StatType.DamageBonus: return "Damage";
            case StatType.MaxStamina: return "Stamina";
            case StatType.MoveSpeed: return "Movement Speed";
            case StatType.DrawSpeed: return "Draw Speed";
            case StatType.GatherYield: return "Extra Yield Chance";
            case StatType.ToolCare: return "Less Tool Wear";
            default: return stat.ToString();
        }
    }

    /// <summary>Stats stored as fractions (0.25 = 25%): crit, resistances and structure damage.</summary>
    public static bool IsFraction(StatType stat)
    {
        switch (stat)
        {
            case StatType.PhysicalCritChance:
            case StatType.PhysicalCritDamage:
            case StatType.SpellCritChance:
            case StatType.SpellCritDamage:
            case StatType.PhysicalResistance:
            case StatType.FireResistance:
            case StatType.FrostResistance:
            case StatType.HolyResistance:
            case StatType.StructureDamage:
            case StatType.DamageBonus:
            case StatType.MoveSpeed:
            case StatType.DrawSpeed:
            case StatType.GatherYield:
            case StatType.ToolCare:
                return true;
            default:
                return false;
        }
    }
}
