using UnityEngine;

/// <summary>
/// The rules of a stamina pool, kept free of Unity lifecycle so they can be tested on their own. An action requires
/// its full stamina cost; refused actions leave the pool unchanged. An empty pool refuses every action until it has
/// regenerated.
/// </summary>
public sealed class StaminaPool
{
    private const float Epsilon = 0.0001f;

    public float Current { get; private set; }
    public float Max { get; private set; }
    public float Fraction => Mathf.Clamp01(Current / Max);
    public bool IsEmpty => Current <= Epsilon;
    public bool IsFull => Current >= Max - Epsilon;

    public StaminaPool(float max)
    {
        Max = Mathf.Max(1f, max);
        Current = Max;
    }

    /// <summary>Pays the full action cost. Returns false, and spends nothing, when stamina is insufficient.</summary>
    public bool TrySpend(float cost)
    {
        cost = Mathf.Max(0f, cost);
        if (IsEmpty || Current < cost)
        {
            return false;
        }

        Current = Mathf.Max(0f, Current - cost);
        if (Current <= Epsilon)
        {
            Current = 0f;
        }

        return true;
    }

    /// <summary>Restores stamina up to the maximum. Returns true when the value changed.</summary>
    public bool Regenerate(float amount)
    {
        if (amount <= 0f || IsFull)
        {
            return false;
        }

        Current = Mathf.Min(Max, Current + amount);
        return true;
    }

    /// <summary>Changes the maximum (hunger lowers it). Current stamina is clamped, never raised.</summary>
    public void SetMax(float max)
    {
        Max = Mathf.Max(1f, max);
        Current = Mathf.Min(Current, Max);
    }
}
