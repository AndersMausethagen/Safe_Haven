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
///   physical damage x (1 - armor / (armor + armor for half reduction)), then every type x (1 - resistance),
///   then a physical blow x (1 - its kind's resistance) (Blunt/Slash/Stab; below zero is a weakness).
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
        float amount = CalculateOutgoing(attacker.Stats, attacker.Rules, attack, weaponDamage, targetCategory, critRoll, out bool isCritical)
            * DealtScale(attacker, targetCategory);
        return new DamageInfo(attacker, attack, amount, attack.DamageType, isCritical, hitPoint, direction);
    }

    /// <summary>
    /// What <paramref name="attacker"/>'s blows against <paramref name="category"/> are multiplied by: its Damage Dealt Scale (the
    /// player's Combat Technique › Player Damage, every path a blow takes meets here), but not against structures, which keep their pace.
    /// </summary>
    public static float DealtScale(Combatant attacker, TargetCategory category) =>
        attacker == null || category == TargetCategory.Structure ? 1f : attacker.DamageDealtScale * StatusDealt(attacker);

    // What the attacker's own statuses leave of its blows (Cowed by a war cry deals less).
    private static float StatusDealt(Combatant attacker) =>
        attacker.TryGetComponent(out StatusEffects effects) ? effects.DamageDealtScale : 1f;

    /// <summary>
    /// What <paramref name="source"/>'s Damage Bonus (the raid's tier and stage, an elite's share, a boss's phase, gear) makes of damage
    /// over time and of hazards, as it does of its blows (the user, 2026-11-16: "scale like blows"; a tier V fire knight's trail burnt as
    /// a tier I one's). 1 for no source. Taken when the hazard, status or fire begins, so it keeps its power after its maker dies.
    /// </summary>
    public static float BonusScale(Combatant source) =>
        source != null ? Mathf.Max(0f, 1f + source.Stats.Get(StatType.DamageBonus)) * source.DamageDealtScale * StatusDealt(source) : 1f;

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
    public static float Mitigate(float amount, DamageType type, StatSheet defender, CombatRules rules) =>
        Mitigate(amount, type, StrikeKind.None, defender, rules);

    /// <summary>Damage after the defender's armor, resistances and its resistance or weakness to this kind of blow.</summary>
    public static float Mitigate(float amount, DamageType type, StrikeKind strike, StatSheet defender, CombatRules rules)
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
        if (type.IsPhysical() && strike.TryResistanceStat(out StatType kind))
        {
            result *= 1f - rules.ClampResistance(defender.Get(kind));
        }

        // A hit that did damage keeps doing a little, but never more than it arrived with.
        return Mathf.Max(result, Mathf.Min(amount, rules.MinimumDamage));
    }

    /// <summary>
    /// The damage to send so that the defender loses <paramref name="wanted"/> after its armour, resistances and Damage Taken multiplier: a
    /// blow measured by what it costs rather than by what it carries. A toppled boss's heart strike is a share of its health whatever its
    /// bark has left (Monster Definition › Topple Strike), and sent this way it lands the same in a friend's game, whose blows reach the
    /// host's creature before its defenses. <paramref name="wanted"/> itself when its defenses stop everything.
    /// </summary>
    public static float BeforeDefenses(float wanted, DamageType type, StrikeKind strike, StatSheet defender, CombatRules rules, float takenMultiplier = 1f)
    {
        if (wanted <= 0f)
        {
            return 0f;
        }

        // Defenses scale a blow alike at any size (but for the Minimum Damage floor), so one large probe measures what gets through.
        const float Probe = 1000f;
        float through = Mitigate(Probe, type, strike, defender, rules) / Probe * Mathf.Max(0f, takenMultiplier);
        return through > 0.001f ? wanted / through : wanted;
    }
}
