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

    /// <summary>How much this hit wears down the target's poise (see <see cref="PoiseMeter"/>).</summary>
    public readonly float Poise;
    public readonly HitTechnique Technique;

    public DamageInfo(Combatant source, AttackDefinition attack, float amount, DamageType type, bool isCritical, Vector3 hitPoint, Vector3 direction)
        : this(source, attack, amount, type, isCritical, hitPoint, direction, attack != null ? attack.PoiseDamage : 0f, HitTechnique.None)
    {
    }

    public DamageInfo(Combatant source, AttackDefinition attack, float amount, DamageType type, bool isCritical, Vector3 hitPoint,
        Vector3 direction, float poise, HitTechnique technique)
    {
        Source = source;
        Attack = attack;
        Amount = amount;
        Type = type;
        IsCritical = isCritical;
        HitPoint = hitPoint;
        Direction = direction;
        Poise = poise;
        Technique = technique;
    }

    /// <summary>The same hit scaled by <paramref name="multiplier"/> (damage and poise), with extra techniques.</summary>
    public DamageInfo Scaled(float multiplier, float poiseMultiplier, HitTechnique technique, bool critical)
    {
        return new DamageInfo(Source, Attack, Amount * multiplier, Type, IsCritical || critical, HitPoint, Direction,
            Poise * poiseMultiplier, Technique | technique);
    }

    /// <summary>The same hit with a different amount, as a guard lets part of it through.</summary>
    public DamageInfo WithAmount(float amount) =>
        new DamageInfo(Source, Attack, amount, Type, IsCritical, HitPoint, Direction, Poise, Technique);
}
