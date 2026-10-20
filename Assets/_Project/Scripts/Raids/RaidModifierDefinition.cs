using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// A rule of a raid that modifiers can change. Values are serialized as numbers, so add new stats at the end and never
/// renumber. A new stat also needs a base value in RaidDefinition and a system that reads it from RaidParameters.
/// </summary>
public enum RaidStat
{
    /// <summary>Seconds until the raid collapses. Read by RaidSession.</summary>
    Duration = 0,
    /// <summary>Seconds an extraction takes. Read by ExtractionPoint.</summary>
    ExtractionTime = 1,
    /// <summary>Multiplier on what supply caches hold; 1 is normal. Read by LootCache.</summary>
    LootQuantity = 2,
    /// <summary>Multiplier on enemies' health; 1 is normal. Applied by RaidDirector.</summary>
    EnemyHealth = 3,
    /// <summary>Multiplier on enemies' physical and spell power; 1 is normal. Applied by RaidDirector.</summary>
    EnemyPower = 4,
}

/// <summary>One change a modifier makes to a raid stat, worked out like combat stats: (base + flat) x (1 + percent).</summary>
[Serializable]
public struct RaidStatModifier
{
    [SerializeField] private RaidStat stat;
    [Tooltip("Flat adds the value: seconds, or 0.5 for +50% on a multiplier. Percent scales the total: 0.25 is +25%.")]
    [SerializeField] private StatModifierOp op;
    [SerializeField] private float value;

    public RaidStatModifier(RaidStat stat, StatModifierOp op, float value)
    {
        this.stat = stat;
        this.op = op;
        this.value = value;
    }

    public RaidStat Stat => stat;
    public StatModifierOp Op => op;
    public float Value => value;
}

/// <summary>How raid stats are named and written for the player.</summary>
public static class RaidStats
{
    public static bool IsMultiplier(RaidStat stat) => stat != RaidStat.Duration && stat != RaidStat.ExtractionTime;

    public static string Label(RaidStat stat)
    {
        switch (stat)
        {
            case RaidStat.Duration:
                return "raid time";
            case RaidStat.ExtractionTime:
                return "extraction time";
            case RaidStat.LootQuantity:
                return "cache loot";
            case RaidStat.EnemyHealth:
                return "enemy health";
            case RaidStat.EnemyPower:
                return "enemy power";
            default:
                return stat.ToString();
        }
    }

    /// <summary>"+2:00 raid time", "+50% cache loot", "-2s extraction time".</summary>
    public static string Describe(RaidStatModifier modifier)
    {
        string sign = modifier.Value < 0f ? "-" : "+";
        float size = Mathf.Abs(modifier.Value);
        string amount;
        if (modifier.Op == StatModifierOp.Percent || IsMultiplier(modifier.Stat))
        {
            amount = $"{Mathf.RoundToInt(size * 100f)}%";
        }
        else if (modifier.Stat == RaidStat.Duration)
        {
            amount = FormatTime(size);
        }
        else
        {
            amount = $"{size:0.#}s";
        }

        return $"{sign}{amount} {Label(modifier.Stat)}";
    }

    /// <summary>Minutes and seconds, "9:05". Rounded up so the clock only shows 0:00 when time is really up.</summary>
    public static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds - 0.001f));
        return $"{total / 60}:{total % 60:00}";
    }
}

/// <summary>
/// Authored data for something that changes a raid's rules: more time, richer caches, tougher enemies. Raid modifier
/// items (see ItemDefinition) point at one; the player picks them at the raid portal and they are used up when the raid
/// starts. Other sources, such as Safe Haven upgrades, can reuse the same definitions later.
/// </summary>
[CreateAssetMenu(fileName = "NewRaidModifier", menuName = "Safe Haven/Raids/Raid Modifier")]
public sealed class RaidModifierDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for lookups and saves.")]
    [SerializeField] private string id;
    [Tooltip("Shown in the UI. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;

    [Header("Effects")]
    [Tooltip("What this modifier changes. Several modifiers on the same stat add together.")]
    [SerializeField] private List<RaidStatModifier> effects = new List<RaidStatModifier>();

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public IReadOnlyList<RaidStatModifier> Effects => effects;

    /// <summary>Every effect in words: "+2:00 raid time, +50% cache loot".</summary>
    public string DescribeEffects()
    {
        var builder = new StringBuilder();
        foreach (RaidStatModifier effect in effects)
        {
            builder.Append(builder.Length > 0 ? ", " : "").Append(RaidStats.Describe(effect));
        }

        return builder.Length > 0 ? builder.ToString() : "no effect";
    }
}
