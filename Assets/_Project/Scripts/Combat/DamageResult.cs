/// <summary>What happened when a hit was applied: for damage numbers, hit effects, lifesteal and kill credit.</summary>
public readonly struct DamageResult
{
    /// <summary>The damage that arrived, before the target's defenses.</summary>
    public readonly float Incoming;
    /// <summary>The damage after armor and resistances. This is what the hit was worth.</summary>
    public readonly float Damage;
    /// <summary>Health actually removed. Less than Damage when the hit was more than the target had left.</summary>
    public readonly float HealthLost;
    public readonly bool Killed;
    public readonly bool IsCritical;
    public readonly DamageType Type;
    /// <summary>How a guard took it (see <see cref="IHitGuard"/>): blocked hits arrive reduced, parried ones not at all.</summary>
    public readonly GuardOutcome Guard;

    public DamageResult(float incoming, float damage, float healthLost, bool killed, bool isCritical, DamageType type,
        GuardOutcome guard = GuardOutcome.None)
    {
        Incoming = incoming;
        Damage = damage;
        HealthLost = healthLost;
        Killed = killed;
        IsCritical = isCritical;
        Type = type;
        Guard = guard;
    }

    public bool Blocked => Guard == GuardOutcome.Blocked;
    public bool Parried => Guard == GuardOutcome.Parried;

    /// <summary>What armor and resistances took off the hit.</summary>
    public float Mitigated => Incoming - Damage;

    /// <summary>The result of hitting something that can no longer be hit.</summary>
    public static DamageResult None => default;
}
