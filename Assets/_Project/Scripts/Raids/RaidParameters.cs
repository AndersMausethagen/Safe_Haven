using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rules one raid run is played with: the raid's base values with every chosen modifier applied, each stat worked
/// out as (base + flat modifiers) x (1 + percent modifiers), the same way combat stats are. Calculated once when the raid
/// starts and read-only afterwards, so every system in the raid sees the same numbers.
/// </summary>
public sealed class RaidParameters
{
    public const float MinimumDuration = 30f;
    public const float MinimumExtractionTime = 0.5f;
    public const float MinimumEnemyHealth = 0.1f;

    private static readonly int StatCount = CountStats();

    private readonly float[] values;

    private RaidParameters(float[] values)
    {
        this.values = values;
    }

    public float Duration => Get(RaidStat.Duration);
    public float ExtractionTime => Get(RaidStat.ExtractionTime);
    public float LootMultiplier => Get(RaidStat.LootQuantity);
    public float EnemyHealthMultiplier => Get(RaidStat.EnemyHealth);
    public float EnemyPowerMultiplier => Get(RaidStat.EnemyPower);

    public float Get(RaidStat stat)
    {
        int index = (int)stat;
        return index >= 0 && index < values.Length ? values[index] : 0f;
    }

    public static RaidParameters Calculate(RaidDefinition raid, IEnumerable<RaidModifierDefinition> modifiers)
    {
        return Calculate(raid, modifiers, 0f);
    }

    /// <summary>The rules with the modifiers applied, plus extra seconds bought with Portal Sparks (GDD §7.3).</summary>
    public static RaidParameters Calculate(RaidDefinition raid, IEnumerable<RaidModifierDefinition> modifiers, float extraSeconds,
        RaidTimeOfDay timeOfDay = null, RaidDifficulty difficulty = null, float extractionCut = 0f)
    {
        var effects = new List<RaidStatModifier>();
        if (extractionCut > 0f)
        {
            effects.Add(new RaidStatModifier(RaidStat.ExtractionTime, StatModifierOp.Percent, -extractionCut));
        }

        if (modifiers != null)
        {
            foreach (RaidModifierDefinition modifier in modifiers)
            {
                if (modifier != null)
                {
                    effects.AddRange(modifier.Effects);
                }
            }
        }

        if (extraSeconds > 0f)
        {
            effects.Add(new RaidStatModifier(RaidStat.Duration, StatModifierOp.Flat, extraSeconds));
        }

        // Accessibility › Raid Length: more time on the clock, shown in the briefing's rules as the rest are.
        if (GameOptions.RaidLengthShare > 1f)
        {
            effects.Add(new RaidStatModifier(RaidStat.Duration, StatModifierOp.Percent, GameOptions.RaidLengthShare - 1f));
        }

        // The hour's reward works like a modifier, so it shows in the briefing's rules.
        if (timeOfDay != null && !Mathf.Approximately(timeOfDay.lootMultiplier, 1f))
        {
            effects.Add(new RaidStatModifier(RaidStat.LootQuantity, StatModifierOp.Percent, timeOfDay.lootMultiplier - 1f));
        }

        // So does the tier's.
        if (difficulty != null)
        {
            effects.Add(new RaidStatModifier(RaidStat.EnemyHealth, StatModifierOp.Percent, difficulty.enemyHealth - 1f));
            effects.Add(new RaidStatModifier(RaidStat.EnemyPower, StatModifierOp.Percent, difficulty.enemyPower - 1f));
            effects.Add(new RaidStatModifier(RaidStat.LootQuantity, StatModifierOp.Percent, difficulty.loot - 1f));
        }

        return Calculate(raid, effects);
    }

    public static RaidParameters Calculate(RaidDefinition raid, IEnumerable<RaidStatModifier> effects)
    {
        if (raid == null)
        {
            throw new ArgumentNullException(nameof(raid));
        }

        var flat = new float[StatCount];
        var percent = new float[StatCount];
        if (effects != null)
        {
            foreach (RaidStatModifier effect in effects)
            {
                int index = (int)effect.Stat;
                if (index < 0 || index >= StatCount)
                {
                    continue;
                }

                if (effect.Op == StatModifierOp.Flat)
                {
                    flat[index] += effect.Value;
                }
                else
                {
                    percent[index] += effect.Value;
                }
            }
        }

        var values = new float[StatCount];
        for (int i = 0; i < StatCount; i++)
        {
            var stat = (RaidStat)i;
            values[i] = Clamp(stat, (raid.GetBase(stat) + flat[i]) * Mathf.Max(0f, 1f + percent[i]));
        }

        return new RaidParameters(values);
    }

    /// <summary>
    /// Scales a quantity by a multiplier. The fraction is rounded up or down at random, so x1.5 on a single item gives two
    /// half of the time. roll is a random value from 0 to 1.
    /// </summary>
    public static int ScaleAmount(int amount, float multiplier, float roll)
    {
        if (amount <= 0 || multiplier <= 0f)
        {
            return 0;
        }

        float scaled = amount * multiplier;
        int whole = Mathf.FloorToInt(scaled);
        return whole + (roll < scaled - whole ? 1 : 0);
    }

    private static float Clamp(RaidStat stat, float value)
    {
        switch (stat)
        {
            case RaidStat.Duration:
                return Mathf.Max(MinimumDuration, value);
            case RaidStat.ExtractionTime:
                return Mathf.Max(MinimumExtractionTime, value);
            case RaidStat.EnemyHealth:
                return Mathf.Max(MinimumEnemyHealth, value);
            default:
                return Mathf.Max(0f, value);
        }
    }

    private static int CountStats()
    {
        int highest = 0;
        foreach (int value in Enum.GetValues(typeof(RaidStat)))
        {
            highest = Math.Max(highest, value);
        }

        return highest + 1;
    }
}
