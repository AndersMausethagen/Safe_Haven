/// <summary>
/// Anything a hit can land on: enemies, buildings, destructible props, and later the player. Attackers only know this
/// contract, never a concrete enemy type, so a new kind of target needs no change to the attacks.
/// </summary>
public interface IDamageable
{
    bool IsAlive { get; }
    Faction Faction { get; }
    TargetCategory Category { get; }

    /// <summary>Applies the hit, reduced by this target's defenses, and reports what happened.</summary>
    DamageResult ApplyDamage(in DamageInfo damage);
}
