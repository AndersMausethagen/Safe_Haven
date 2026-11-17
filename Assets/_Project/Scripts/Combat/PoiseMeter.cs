using UnityEngine;

/// <summary>
/// How much more a fighter can take before it staggers. Every hit wears it down by the hit's poise damage (heavy swings,
/// finishers and counters the most); at zero it breaks and refills, and the owner staggers. It comes back by itself after
/// a while without hits. Plain C# so it can be tested; MonsterAI keeps one per monster.
/// <para>
/// A boss that topples (Monster Definition › Poise Growth) grows it after each topple, so the next needs more and topples don't come
/// back to back; each new stage of its fight sets it back to its own poise (<see cref="ResetGrowth"/>).
/// </para>
/// </summary>
public sealed class PoiseMeter
{
    private readonly float recoveryDelay;
    private readonly float recoveryPerSecond;
    private float lastHit = float.NegativeInfinity;

    public PoiseMeter(float max, float recoveryDelay, float recoveryPerSecond)
    {
        BaseMax = Mathf.Max(1f, max);
        Max = BaseMax;
        Current = Max;
        this.recoveryDelay = recoveryDelay;
        this.recoveryPerSecond = recoveryPerSecond;
    }

    /// <summary>Its own poise, before any growth: what a parry's share is measured against, so growth makes parries count for less too.</summary>
    public float BaseMax { get; }

    /// <summary>What it takes to break it now: its own poise, plus what topples have grown it by.</summary>
    public float Max { get; private set; }

    public float Current { get; private set; }
    public float Fraction => Current / Max;

    /// <summary>Seconds without a hit before it starts coming back.</summary>
    public float RecoveryDelay => recoveryDelay;

    /// <summary>Wears poise down by <paramref name="amount"/>; true when that breaks it (it then refills).</summary>
    public bool Hit(float amount, float now)
    {
        if (amount <= 0f)
        {
            return false;
        }

        lastHit = now;
        Current -= amount;
        if (Current > 0f)
        {
            return false;
        }

        Current = Max;
        return true;
    }

    /// <summary>Breaks it now whatever is left (a felling blow on a reeling creature): it refills, as a break does.</summary>
    public void Break(float now)
    {
        lastHit = now;
        Current = Max;
    }

    /// <summary>
    /// After a topple: the next break needs <paramref name="share"/> of its own poise more (0.3: 30% more each time), and it stands full
    /// at that. Growth adds up until <see cref="ResetGrowth"/>. 0 changes nothing.
    /// </summary>
    public void Grow(float share)
    {
        if (share <= 0f)
        {
            return;
        }

        Max += BaseMax * share;
        Current = Max;
    }

    /// <summary>A new stage of a boss's fight: back to its own poise, as full (as a share) as it was.</summary>
    public void ResetGrowth()
    {
        if (Max <= BaseMax)
        {
            return;
        }

        float share = Fraction;
        Max = BaseMax;
        Current = share * Max;
    }

    /// <summary>Co-op, a friend's copy: shows the host's creature's poise (<paramref name="fraction"/> of it). The copy wears none of its own.</summary>
    public void Show(float fraction)
    {
        Current = Mathf.Clamp01(fraction) * Max;
    }

    public void Tick(float now, float deltaTime)
    {
        if (now - lastHit >= recoveryDelay)
        {
            Current = Mathf.Min(Max, Current + recoveryPerSecond * deltaTime);
        }
    }
}
