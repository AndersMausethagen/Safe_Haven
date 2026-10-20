using UnityEngine;

/// <summary>
/// What kind of damage a hit deals. It decides which power and crit stats scale the hit and which resistance reduces it.
/// Physical comes from weapons; the others are spell schools for abilities. Values are serialized as numbers, so add
/// new types at the end and never renumber.
/// </summary>
public enum DamageType
{
    Physical = 0,
    Fire = 1,
    Frost = 2,
    Holy = 3,
}

/// <summary>
/// Which stat each damage type uses. Adding a damage type means adding a case here, and the calculator needs no change.
/// </summary>
public static class DamageTypeExtensions
{
    public static bool IsPhysical(this DamageType type)
    {
        return type == DamageType.Physical;
    }

    public static StatType PowerStat(this DamageType type)
    {
        return type.IsPhysical() ? StatType.PhysicalPower : StatType.SpellPower;
    }

    public static StatType CritChanceStat(this DamageType type)
    {
        return type.IsPhysical() ? StatType.PhysicalCritChance : StatType.SpellCritChance;
    }

    public static StatType CritDamageStat(this DamageType type)
    {
        return type.IsPhysical() ? StatType.PhysicalCritDamage : StatType.SpellCritDamage;
    }

    public static StatType ResistanceStat(this DamageType type)
    {
        switch (type)
        {
            case DamageType.Fire:
                return StatType.FireResistance;
            case DamageType.Frost:
                return StatType.FrostResistance;
            case DamageType.Holy:
                return StatType.HolyResistance;
            default:
                return StatType.PhysicalResistance;
        }
    }
}
