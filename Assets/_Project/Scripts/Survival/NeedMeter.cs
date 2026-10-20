using UnityEngine;

/// <summary>
/// One survival meter, such as hunger: a value between 0 and a maximum that drains over time and is restored by food or
/// drink. Plain C# so its rules can be tested without a scene (see SurvivalTests).
/// </summary>
public sealed class NeedMeter
{
    public NeedMeter(float max, float startingFraction)
    {
        Max = Mathf.Max(1f, max);
        Current = Max * Mathf.Clamp01(startingFraction);
    }

    public float Max { get; }
    public float Current { get; private set; }
    public float Fraction => Current / Max;
    public bool IsEmpty => Current <= 0.0001f;
    public bool IsFull => Current >= Max - 0.0001f;

    /// <summary>Lowers the meter, never below 0. Returns true when the value changed.</summary>
    public bool Drain(float amount)
    {
        if (amount <= 0f || IsEmpty)
        {
            return false;
        }

        Current = Mathf.Max(0f, Current - amount);
        return true;
    }

    /// <summary>Raises the meter, never above the maximum. Returns how much it actually rose.</summary>
    public float Restore(float amount)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        float before = Current;
        Current = Mathf.Min(Max, Current + amount);
        return Current - before;
    }

    /// <summary>Sets the value directly, for loading a save or carrying it to another scene.</summary>
    public void Set(float value)
    {
        Current = Mathf.Clamp(value, 0f, Max);
    }
}
