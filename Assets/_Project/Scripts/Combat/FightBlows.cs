using UnityEngine;

/// <summary>
/// The one rule for what a landed blow counts as fighting, for everything a fight pays: Focus, Regain, lifesteal (Docs/BugPatterns.md,
/// eighth round). Each kept its own test, and crates, barrels and fences (Neutral, but found by every swing) paid Focus per target, healed
/// through Regain and fed lifesteal; so did a blow the creature parried or blocked (review 2026-11-27).
/// </summary>
public static class FightBlows
{
    /// <summary>
    /// True when <paramref name="attacker"/>'s blow landed on a foe: something that takes a side against it (not Neutral: props, crates,
    /// wildlife), through the guard and the parry, not a tick over time, and it took health (one a floor held off cost nothing).
    /// The training dummy is on the Enemy side, so practice pays as a fight does.
    /// </summary>
    public static bool Counts(Combatant attacker, IDamageable target, DamageInfo damage, DamageResult result)
    {
        if (result.Blocked || result.Parried || damage.IsOverTime || result.HealthLost <= 0f)
        {
            return false;
        }

        if (!(target is Component body) || body == null || !body.TryGetComponent(out Combatant foe))
        {
            return false;
        }

        return foe.Faction != Faction.Neutral && (attacker == null || attacker.IsHostileTo(foe.Faction));
    }
}
