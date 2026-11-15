using UnityEngine;

/// <summary>
/// What a set of stats comes to in a fight (Docs/PlayerStatsResearch.md #1-2), one formula for the raid tiers' balance model
/// (GearBalance) and the character window's Fight card: damage as one expected share of the weapon's (power from Strength and Physical
/// Power, Damage Bonus, the Melee skill, and critical hits by their chance and damage), toughness as health over what armour lets through,
/// and blows to fell and blows to take against a creature. Plain C#; resistances and the kind of blow are left out, as they are in the
/// tiers' reference creature.
/// </summary>
public static class FightMath
{
    /// <summary>Stat totals as a kit, a meal or a perk adds to them; health counts its flat and percent parts.</summary>
    public sealed class Totals
    {
        public float Armor;
        public float Health = 100f;
        public float HealthShare;
        public float Strength;
        public float Power;
        public float CritChance = 0.05f;
        public float CritDamage = 0.5f;
        public float DamageBonus;
        public float MeleeDamage;

        public Totals Copy() => (Totals)MemberwiseClone();

        /// <summary>Adds a bonus, times <paramref name="scale"/> (an empowering level adds a share of a piece's own).</summary>
        public void Add(StatBonus bonus, float scale = 1f)
        {
            float value = bonus.value * scale;
            bool share = bonus.op == StatModifierOp.Percent;
            switch (bonus.stat)
            {
                case StatType.Armor: Armor += share ? Armor * value : value; break;
                case StatType.MaxHealth: if (share) HealthShare += value; else Health += value; break;
                case StatType.Strength: Strength += value; break;
                case StatType.PhysicalPower: Power += value; break;
                case StatType.PhysicalCritChance: CritChance += value; break;
                case StatType.PhysicalCritDamage: CritDamage += value; break;
                case StatType.DamageBonus: DamageBonus += value; break;
                case StatType.MeleeDamage: MeleeDamage += value; break;
            }
        }

        /// <summary>Health with its percent parts.</summary>
        public float FullHealth => Health * (1f + HealthShare);

        /// <summary>The expected damage of a swing of a weapon of <paramref name="weaponDamage"/>.</summary>
        public float Damage(float weaponDamage, CombatRules rules) => weaponDamage * DamageShare(rules);

        /// <summary>Everything that multiplies a weapon's damage, as one share (1 = the weapon as it is).</summary>
        public float DamageShare(CombatRules rules) =>
            Mathf.Max(0f, 1f + (Strength * rules.PowerPerStrength + Power) * rules.DamagePerPower) * Mathf.Max(0f, 1f + DamageBonus)
            * Mathf.Max(0f, 1f + MeleeDamage) * (1f + Mathf.Clamp01(CritChance) * Mathf.Max(0f, CritDamage));

        /// <summary>How much damage it takes to kill: health over what armour lets through.</summary>
        public float Toughness(CombatRules rules) => FullHealth / Mathf.Max(0.01f, 1f - rules.GetArmorReduction(Armor));
    }

    /// <summary>The totals a live stat sheet comes to (the player now), with health as its <paramref name="maxHealth"/>.</summary>
    public static Totals Of(StatSheet stats, float maxHealth)
    {
        return new Totals
        {
            Armor = stats.Get(StatType.Armor), Health = maxHealth, Strength = stats.Get(StatType.Strength), Power = stats.Get(StatType.PhysicalPower),
            CritChance = stats.Get(StatType.PhysicalCritChance), CritDamage = stats.Get(StatType.PhysicalCritDamage),
            DamageBonus = stats.Get(StatType.DamageBonus), MeleeDamage = stats.Get(StatType.MeleeDamage),
        };
    }

    /// <summary>Blows of <paramref name="damage"/> to fell a creature of this health and armour (at a raid tier's health scale).</summary>
    public static float HitsToKill(float damage, float health, float armor, float healthScale, CombatRules rules) =>
        health * healthScale / Mathf.Max(0.01f, damage * (1f - rules.GetArmorReduction(armor)));

    /// <summary>Blows of <paramref name="damage"/> (at a raid tier's power scale) it takes to wear <paramref name="toughness"/> down.</summary>
    public static float HitsToDie(float toughness, float damage, float powerScale) => toughness / Mathf.Max(0.01f, damage * powerScale);
}
