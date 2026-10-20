using UnityEngine;

/// <summary>
/// One hit on its way to a target: everything the target needs to apply it and everything a listener might want to know
/// about it. It is immutable and carries the damage before the target's defenses, so armor and resistances are the
/// target's business. Abilities, projectiles and damage over time all deliver damage as a DamageInfo.
/// </summary>
public readonly struct DamageInfo
{
    /// <summary>Who dealt the hit. Null for the environment.</summary>
    public readonly Combatant Source;
    /// <summary>The attack or ability it came from. Null when it didn't come from one.</summary>
    public readonly AttackDefinition Attack;
    /// <summary>Damage after the attacker's power and crit, before the target's armor and resistances.</summary>
    public readonly float Amount;
    public readonly DamageType Type;
    public readonly bool IsCritical;
    public readonly Vector3 HitPoint;
    /// <summary>The direction the hit travelled, from attacker to target.</summary>
    public readonly Vector3 Direction;

    public DamageInfo(Combatant source, AttackDefinition attack, float amount, DamageType type, bool isCritical, Vector3 hitPoint, Vector3 direction)
    {
        Source = source;
        Attack = attack;
        Amount = amount;
        Type = type;
        IsCritical = isCritical;
        HitPoint = hitPoint;
        Direction = direction;
    }
}
