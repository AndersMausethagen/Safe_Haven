using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The base stats of a kind of combatant: the player, a wolf, a wooden wall. Authored in the Inspector and never changed
/// at runtime; each Combatant copies it into its own StatSheet, where gear and buffs modify it.
/// A stat that has no row counts as 0.
/// </summary>
[CreateAssetMenu(fileName = "CombatStats", menuName = "Safe Haven/Combat/Combat Stats")]
public sealed class CombatStatsDefinition : ScriptableObject
{
    [SerializeField] private List<StatValue> stats = new List<StatValue>();

    public IReadOnlyList<StatValue> Stats => stats;

    public float GetValue(StatType stat)
    {
        foreach (StatValue entry in stats)
        {
            if (entry.stat == stat)
            {
                return entry.value;
            }
        }

        return 0f;
    }

    /// <summary>Writes every base value into a StatSheet. If a stat is listed twice, the first row wins.</summary>
    public void ApplyTo(StatSheet sheet)
    {
        var applied = new HashSet<StatType>();
        foreach (StatValue entry in stats)
        {
            if (applied.Add(entry.stat))
            {
                sheet.SetBase(entry.stat, entry.value);
            }
        }
    }

    // A new asset starts with a row for every stat so none are forgotten.
    private void Reset()
    {
        stats.Clear();
        foreach (StatType stat in Enum.GetValues(typeof(StatType)))
        {
            float value = 0f;
            if (stat == StatType.MaxHealth)
            {
                value = 100f;
            }
            else if (stat == StatType.PhysicalCritDamage || stat == StatType.SpellCritDamage)
            {
                value = 0.5f;
            }

            stats.Add(new StatValue(stat, value));
        }
    }

    private void OnValidate()
    {
        var seen = new HashSet<StatType>();
        foreach (StatValue entry in stats)
        {
            if (!seen.Add(entry.stat))
            {
                Debug.LogWarning($"{name} lists {entry.stat} more than once. Only the first row is used.", this);
            }
        }
    }
}

[Serializable]
public struct StatValue
{
    public StatType stat;
    public float value;

    public StatValue(StatType stat, float value)
    {
        this.stat = stat;
        this.value = value;
    }
}
