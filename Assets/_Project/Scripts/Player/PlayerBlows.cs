using UnityEngine;

/// <summary>
/// What a player's blow adds however it is delivered (a blade, an arrow, a staff's cast, a thrown weapon): a blow from the
/// shadows is a sure critical, and Ambush Damage adds to it; and what such a blow does to a creature caught unaware, for
/// Silent Takedown before the blow and as it lands. Every path asks here, so none forgets one (Docs/BugPatterns.md, guard 7:
/// thrown weapons had no sneak attack, the focus ring's takedown estimate left out Ambush Damage). ConventionTests keeps
/// PlayerProficiencies.CaughtUnaware and AmbushScale to this class.
/// </summary>
public static class PlayerBlows
{
    /// <summary>A blow by <paramref name="attacker"/> reaching <paramref name="target"/> from <paramref name="from"/>: true for a sure
    /// critical (<paramref name="forced"/> by the swing, a counter say, or the target caught unaware), with what Ambush Damage multiplies
    /// it by.</summary>
    public static bool FromTheShadows(Combatant attacker, IDamageable target, Vector3 from, bool forced, out float ambushScale)
    {
        ambushScale = attacker != null ? PlayerProficiencies.AmbushScale(attacker.Stats, target, from) : 1f;
        return forced || PlayerProficiencies.CaughtUnaware(target, from);
    }

    /// <summary>Whether a blow from <paramref name="from"/> would catch <paramref name="target"/> unaware (what outlines and the focus
    /// ring show).</summary>
    public static bool CatchesUnaware(IDamageable target, Vector3 from) => PlayerProficiencies.CaughtUnaware(target, from);

    /// <summary>
    /// What <paramref name="attack"/> does to <paramref name="body"/> caught unaware: as the blow leaves the hand (no lucky critical),
    /// times <paramref name="multiplier"/> (the swing's, with the Melee skill and Ambush Damage), through its defenses, and as much
    /// more as being caught unaware adds (Perception Settings › Ambush Damage Taken, which the blow is judged by as it lands).
    /// </summary>
    public static float UnawareBlowWorth(Combatant attacker, Damageable body, AttackDefinition attack, float weaponDamage, float multiplier)
    {
        if (attacker == null || body == null || attack == null || !body.TryGetComponent(out Combatant theirs))
        {
            return 0f;
        }

        float outgoing = DamageCalculator.CalculateOutgoing(attacker.Stats, attacker.Rules, attack, weaponDamage, body.Category, 1f, out _) * multiplier;
        return DamageCalculator.Mitigate(outgoing, attack.DamageType, StrikeKindExtensions.Of(attacker, attack), theirs.Stats, theirs.Rules)
            * PerceptionSettings.Current.AmbushDamageTaken;
    }
}
