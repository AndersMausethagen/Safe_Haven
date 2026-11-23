using UnityEngine;

/// <summary>
/// A coated blade (a weapon coating such as Ember Pitch, through the player's stats): each weapon swing that lands on a creature
/// carries a blow of fire after it, a share of what the swing brought (Stat › Melee Fire Damage), and now and then sets it alight
/// (Stat › Melee Ignite Chance, then Burning as a fire arrow does). The fire is its own hit of Fire damage: armour doesn't stop it,
/// fire resistance does, and a weakness to fire (the Heartroot's) makes it more. It comes as a tick does (OverTime): a number and a
/// glow, but no second hit stop, flinch or poise. A blocked or parried swing carries no fire.
/// </summary>
public static class CoatedFire
{
    /// <summary>The fire a swing that brought <paramref name="incoming"/> damage (before defences) carries at this share.</summary>
    public static float Amount(float incoming, float share) => share > 0f && incoming > 0f ? incoming * share : 0f;

    /// <summary>True when a roll (0 to 1) under the chance sets the creature alight.</summary>
    public static bool Ignites(float chance, float roll) => chance > 0f && roll < chance;

    /// <summary>A coated swing has landed on <paramref name="target"/> with <paramref name="result"/>: its fire, and maybe a burn.</summary>
    public static void AfterSwing(Combatant by, IDamageable target, in DamageResult result, float weaponDamage, Vector3 point, Vector3 direction)
    {
        if (by == null || target == null || !result.Landed || result.Blocked || result.Parried || target.Category != TargetCategory.Creature)
        {
            return;
        }

        StatSheet stats = by.Stats;
        float fire = Amount(result.Incoming, stats.Get(StatType.MeleeFireDamage));
        if (fire > 0f && target.IsAlive)
        {
            // Not reported as a blow of the player's (as Burning's ticks aren't): no training or Insight for it, no second lifesteal.
            target.ApplyDamage(new DamageInfo(by, null, fire, DamageType.Fire, false, point, direction, 0f, HitTechnique.OverTime));
        }

        if (target.IsAlive && target is Damageable body && Ignites(stats.Get(StatType.MeleeIgniteChance), Random.value))
        {
            CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
            Burning.Apply(body, by, rules.MeleeBurnShare * weaponDamage, rules.MeleeBurnSeconds);
        }
    }
}
