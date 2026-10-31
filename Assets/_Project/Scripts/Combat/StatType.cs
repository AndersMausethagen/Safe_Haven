/// <summary>
/// The stats combat is calculated from. Power and crit are split into physical (weapons) and spell (abilities), the
/// way V Rising does it. Values are serialized as numbers, so add new stats at the end and never renumber.
/// </summary>
public enum StatType
{
    /// <summary>Health points.</summary>
    MaxHealth = 0,
    /// <summary>Each point adds a share of damage to physical hits (see CombatRules.DamagePerPower).</summary>
    PhysicalPower = 1,
    /// <summary>The same for spell damage: fire, frost, holy.</summary>
    SpellPower = 2,
    /// <summary>Chance from 0 to 1 that a physical hit is critical.</summary>
    PhysicalCritChance = 3,
    /// <summary>Extra damage on a physical crit. 0.5 means +50%.</summary>
    PhysicalCritDamage = 4,
    SpellCritChance = 5,
    SpellCritDamage = 6,
    /// <summary>Reduces physical damage with diminishing returns (see CombatRules.ArmorForHalfReduction).</summary>
    Armor = 7,
    /// <summary>Fraction of that damage type taken away. 0.25 is 25% less, a negative value is a weakness.</summary>
    PhysicalResistance = 8,
    FireResistance = 9,
    FrostResistance = 10,
    HolyResistance = 11,
    /// <summary>Extra damage dealt to structures. 0.5 means +50%. Like V Rising's damage vs castle objects.</summary>
    StructureDamage = 12,
    /// <summary>Raw might, mostly from gear. Each point adds physical power (see CombatRules.PowerPerStrength).</summary>
    Strength = 13,
    /// <summary>Share of extra damage on every hit dealt. 0.25 means +25%. Raids and breaches use it to scale enemies.</summary>
    DamageBonus = 14,
    /// <summary>Extra stamina points on top of the player's base (talents).</summary>
    MaxStamina = 15,
    /// <summary>Share of extra movement speed. 0.05 means +5%.</summary>
    MoveSpeed = 16,
    /// <summary>Share faster a bow draws. 0.1 means it reaches full draw in 1/1.1 of the time.</summary>
    DrawSpeed = 17,
    /// <summary>Chance from 0 to 1 that gathering a node gives one extra of its main yield.</summary>
    GatherYield = 18,
    /// <summary>Share less wear on gathering tools. 0.25 means a quarter less.</summary>
    ToolCare = 19,
    /// <summary>Share of the health a blow takes that comes back to the attacker. 0.05 means 5%.</summary>
    Lifesteal = 20,
    /// <summary>Share more Cursed Essence from kills. 0.25 means a quarter more on average.</summary>
    EssenceFind = 21,
}
