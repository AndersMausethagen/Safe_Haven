using System;
using System.Collections.Generic;

/// <summary>
/// The live stat values of one combatant: base values from its CombatStatsDefinition plus any modifiers currently
/// applied. A stat is (base + flat modifiers) x (1 + percent modifiers). It is plain data with no Unity dependencies,
/// separate from the authored definition, which is never changed at runtime.
/// </summary>
public sealed class StatSheet
{
    private static readonly int StatCount = CountStats();

    private readonly float[] baseValues = new float[StatCount];
    private readonly List<StatModifier> modifiers = new List<StatModifier>();

    /// <summary>Raised after a stat's base value or modifiers change.</summary>
    public event Action<StatType> Changed;

    public float GetBase(StatType stat)
    {
        return baseValues[(int)stat];
    }

    public void SetBase(StatType stat, float value)
    {
        baseValues[(int)stat] = value;
        Changed?.Invoke(stat);
    }

    /// <summary>The stat's current value with every modifier applied.</summary>
    public float Get(StatType stat)
    {
        float flat = 0f;
        float percent = 0f;
        for (int i = 0; i < modifiers.Count; i++)
        {
            StatModifier modifier = modifiers[i];
            if (modifier.Stat != stat)
            {
                continue;
            }

            if (modifier.Op == StatModifierOp.Flat)
            {
                flat += modifier.Value;
            }
            else
            {
                percent += modifier.Value;
            }
        }

        return (baseValues[(int)stat] + flat) * Math.Max(0f, 1f + percent);
    }

    public void AddModifier(StatModifier modifier)
    {
        modifiers.Add(modifier);
        Changed?.Invoke(modifier.Stat);
    }

    /// <summary>Removes every modifier that came from the source. Returns how many were removed.</summary>
    public int RemoveModifiersFrom(object source)
    {
        int removed = 0;
        List<StatType> changed = null;
        for (int i = modifiers.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(modifiers[i].Source, source))
            {
                continue;
            }

            changed = changed ?? new List<StatType>();
            if (!changed.Contains(modifiers[i].Stat))
            {
                changed.Add(modifiers[i].Stat);
            }

            modifiers.RemoveAt(i);
            removed++;
        }

        if (changed != null)
        {
            foreach (StatType stat in changed)
            {
                Changed?.Invoke(stat);
            }
        }

        return removed;
    }

    private static int CountStats()
    {
        int highest = 0;
        foreach (int value in Enum.GetValues(typeof(StatType)))
        {
            highest = Math.Max(highest, value);
        }

        return highest + 1;
    }
}
