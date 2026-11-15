using UnityEngine;

/// <summary>
/// How much more a fighter can take before it staggers. Every hit wears it down by the hit's poise damage (heavy swings,
/// finishers and counters the most); at zero it breaks and refills, and the owner staggers. It comes back by itself after
/// a while without hits. Plain C# so it can be tested; MonsterAI keeps one per monster.
/// </summary>
public sealed class PoiseMeter
{
    private readonly float recoveryDelay;
    private readonly float recoveryPerSecond;
    private float lastHit = float.NegativeInfinity;

    public PoiseMeter(float max, float recoveryDelay, float recoveryPerSecond)
    {
        Max = Mathf.Max(1f, max);
        Current = Max;
        this.recoveryDelay = recoveryDelay;
        this.recoveryPerSecond = recoveryPerSecond;
    }

    public float Max { get; }
    public float Current { get; private set; }
    public float Fraction => Current / Max;

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
