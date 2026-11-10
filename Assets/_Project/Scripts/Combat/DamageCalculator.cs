using UnityEngine;

/// <summary>
/// The damage formulas, in two halves that mirror how a hit travels. These are plain functions of stats and
/// definitions, so they are easy to test and the same for a player's sword, a monster's claw and an ability.
///
/// Attacker side (Build / CalculateOutgoing):
///   (base damage + weapon damage x weapon ratio) x (1 + power x damage per power x power scaling) (physical power includes Strength)
///   x (1 + damage bonus),
///   x structure multiplier and the structure damage stat against structures, x (1 + crit damage) on a crit.
/// Defender side (Mitigate):
///   physical damage x (1 - armor / (armor + armor for half reduction)), then every type x (1 - resistance).
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// A crit roll that always comes up critical (for an attack that can crit): a counter, a backstab on a staggered
    /// target, a perfect release. A roll of 0 would not, for an attacker with no crit chance at all.
    /// </summary>
    public const float SureCrit = -1f;

    /// <summary>Builds the hit an attacker's attack deals to one target. critRoll is a random value from 0 to 1.</summary>
    public static DamageInfo Build(
        Combatant attacker,
        AttackDefinition attack,
        float weaponDamage,
        TargetCategory targetCategory,
        Vector3 hitPoint,
        Vector3 direction,
        float critRoll)
    {
        float amount = CalculateOutgoing(attacker.Stats, attacker.Rules, attack, weaponDamage, targetCategory, critRoll, out bool isCritical);
        return new DamageInfo(attacker, attack, amount, attack.DamageType, isCritical, hitPoint, direction);
    }

    /// <summary>Damage after the attacker's power and crit, before the target's defenses.</summary>
    public static float CalculateOutgoing(
        StatSheet attacker,
        CombatRules rules,
        AttackDefinition attack,
        float weaponDamage,
        TargetCategory targetCategory,
        float critRoll,
        out bool isCritical)
    {
        DamageType type = attack.DamageType;

        float damage = attack.BaseDamage + weaponDamage * attack.WeaponDamageRatio;
        float power = rules.PowerFor(attacker, type);
        damage *= Mathf.Max(0f, 1f + power * rules.DamagePerPower * attack.PowerScaling);
        damage *= Mathf.Max(0f, 1f + attacker.Get(StatType.DamageBonus));

        if (targetCategory == TargetCategory.Structure)
        {
            damage *= attack.StructureDamageMultiplier * Mathf.Max(0f, 1f + attacker.Get(StatType.StructureDamage));
        }

        float critChance = Mathf.Clamp01(attacker.Get(type.CritChanceStat()));
        isCritical = attack.CanCrit && critRoll < critChance;
        if (isCritical)
        {
            damage *= 1f + Mathf.Max(0f, attacker.Get(type.CritDamageStat()));
        }

        return Mathf.Max(0f, damage);
    }

    /// <summary>Damage after the defender's armor and resistances.</summary>
    public static float Mitigate(float amount, DamageType type, StatSheet defender, CombatRules rules)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        float result = amount;
        if (type.IsPhysical())
        {
            result *= 1f - rules.GetArmorReduction(defender.Get(StatType.Armor));
        }

        result *= 1f - rules.ClampResistance(defender.Get(type.ResistanceStat()));

        // A hit that did damage keeps doing a little, but never more than it arrived with.
        return Mathf.Max(result, Mathf.Min(amount, rules.MinimumDamage));
    }
}
