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

    // Skills (Progression/ProficiencyDefinition) and their perks. Shares: 0.1 means 10% more, a negative value less.
    /// <summary>Share faster the axe rises on the timing bar (felling and splitting).</summary>
    ChopSpeed = 22,
    /// <summary>Share wider the axe's critical zone on the timing bar.</summary>
    ChopSweetSpot = 23,
    /// <summary>Chance from 0 to 1 of one extra of a tree's main yield, on top of Extra Yield Chance.</summary>
    ChopYield = 24,
    /// <summary>Share faster the pickaxe rises on the timing bar.</summary>
    MineSpeed = 25,
    /// <summary>Share wider the pickaxe's critical zone on the timing bar.</summary>
    MineSweetSpot = 26,
    /// <summary>Chance from 0 to 1 of one extra of a rock's main yield, on top of Extra Yield Chance.</summary>
    MineYield = 27,
    /// <summary>Share more damage from weapon swings (not arrows or spells). Applied before backstabs, so their order holds.</summary>
    MeleeDamage = 28,
    /// <summary>Share more stamina a weapon swing costs. Negative is cheaper.</summary>
    MeleeStaminaCost = 29,
    /// <summary>Share more stamina a blocked blow costs. Negative is cheaper.</summary>
    BlockStaminaCost = 30,
    /// <summary>Share louder the player's footsteps while sneaking, and picking locks. Negative is quieter.</summary>
    SneakNoise = 31,
    /// <summary>Share easier the player is to see while sneaking. Negative is harder: monsters take longer to spot them.</summary>
    SneakVisibility = 32,
    /// <summary>Share more damage on a hit against an unaware monster, on top of the ambush bonus.</summary>
    AmbushDamage = 33,
    /// <summary>Share wider the zones on the lockpicking bar.</summary>
    LockpickZone = 34,
    /// <summary>Chance from 0 to 1 that a weapon, tool or arrow the player makes comes out Fine; a share of that is Masterwork.</summary>
    WeaponcraftQuality = 35,
    /// <summary>Share of full durability a new weapon or tool the player makes starts with, less: -0.2 starts it at 80%.</summary>
    WeaponcraftFinish = 36,
    /// <summary>Chance from 0 to 1 that armour or a shield the player makes comes out Fine; a share of that is Masterwork.</summary>
    ArmorcraftQuality = 37,
    /// <summary>Share of full durability new armour the player makes starts with, less.</summary>
    ArmorcraftFinish = 38,
    /// <summary>Share longer the bonuses of meals the player eats last.</summary>
    MealDuration = 39,
    /// <summary>Chance from 0 to 1 of one more portion when the player cooks.</summary>
    CookingYield = 40,
    /// <summary>Share longer the bonuses of tonics the player drinks last.</summary>
    TonicDuration = 41,
    /// <summary>Chance from 0 to 1 of one more draught when the player brews.</summary>
    AlchemyYield = 42,
    /// <summary>Share faster the player hammers a building up.</summary>
    BuildSpeed = 43,
    /// <summary>Chance from 0 to 1 of one more plank, ingot or block when the player refines materials.</summary>
    RefiningYield = 44,
    /// <summary>Share faster the player picks by hand and swings the sickle.</summary>
    ForageSpeed = 45,
    /// <summary>Chance from 0 to 1 of one extra of a plant's main yield (by hand or sickle), on top of Extra Yield Chance.</summary>
    ForageYield = 46,
    /// <summary>Share wider the catch zone on the fishing reel.</summary>
    ReelZone = 47,
    /// <summary>Share more damage from the player's arrows.</summary>
    ArrowDamage = 48,
    /// <summary>Share more damage from the player's spells.</summary>
    SpellDamage = 49,
    /// <summary>Share more stamina a spell costs. Negative is cheaper.</summary>
    SpellStaminaCost = 50,
    /// <summary>Share more experience from all practice. 0.2 means 20% more: being rested, a warm sauna.</summary>
    ExperienceGain = 51,
}
