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
        Sum(stat, out float flat, out float percent);
        return (baseValues[(int)stat] + flat) * Math.Max(0f, 1f + percent);
    }

    /// <summary>
    /// The stat on a base kept outside the sheet (the stamina pool's, or 1 for a share read as 1 + its value, move speed):
    /// (that base + the sheet's + flat) × (1 + percent). With nothing in the sheet's base a Percent modifier only scaled the
    /// Flat ones: Chilled slowed nobody and took no stamina, Rested and Moon Broth gave none, and with a slowing armour Chilled
    /// made the player faster (review 2026-11-16).
    /// </summary>
    public float GetOn(StatType stat, float outsideBase)
    {
        Sum(stat, out float flat, out float percent);
        return (outsideBase + baseValues[(int)stat] + flat) * Math.Max(0f, 1f + percent);
    }

    /// <summary>A chance read as it is (gather yield): a Percent modifier adds its points as a Flat one does ("+10% gather yield"
    /// is ten points more chance), as there is nothing for it to scale.</summary>
    public float GetChance(StatType stat)
    {
        Sum(stat, out float flat, out float percent);
        return baseValues[(int)stat] + flat + percent;
    }

    private void Sum(StatType stat, out float flat, out float percent)
    {
        flat = 0f;
        percent = 0f;
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
