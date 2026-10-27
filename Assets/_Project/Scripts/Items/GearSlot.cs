using UnityEngine;

/// <summary>
/// Where on the body a piece of armor or jewelry is worn. Set on the ItemDefinition. Values are serialized as numbers,
/// so add new ones at the end and never renumber.
/// </summary>
public enum ArmorSlot
{
    None = 0,
    Head = 1,
    Neck = 2,
    Shoulders = 3,
    Back = 4,
    Chest = 5,
    Wrists = 6,
    Hands = 7,
    Waist = 8,
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

/// <summary>The rules of the gear slots: what fits where, and their names.</summary>
public static class GearSlots
{
    /// <summary>How many gear slots there are.</summary>
    public const int Count = 18;

    /// <summary>Whether the item can go into the gear slot.</summary>
    public static bool Accepts(GearSlot slot, ItemDefinition item)
    {
        if (item == null)
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
                return "Finger";
            case GearSlot.Finger2:
                return "Finger";
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
                return true;
            default:
                return false;
        }
    }
}
