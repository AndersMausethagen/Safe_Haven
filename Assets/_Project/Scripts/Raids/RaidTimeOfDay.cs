using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One hour a raid can take place at (RaidDefinition › Time of Day): its name in the briefing, the clock hour, how
/// often it comes up and what it pays. Which one a run gets is rolled from the run's seed (<see cref="For"/>), before
/// the portal opens, so the briefing can show it. Night raids are darker (the night veil; monsters see less, the player
/// too) and pay more.
/// </summary>
[Serializable]
public sealed class RaidTimeOfDay
{
    [Tooltip("Shown in the briefing, e.g. Day, Dusk, Night.")]
    public string name = "Day";
    [Tooltip("Hour of the day the raid is played at (0–24). The clock stands still during the raid.")]
    [Range(0f, 24f)] public float hour = 12f;
    [Tooltip("How often it comes up, against the others' chances.")]
    [Min(0f)] public float chance = 1f;
    [Tooltip("Multiplies what supply caches hold, like a raid modifier (1 = normal).")]
    [Min(0f)] public float lootMultiplier = 1f;
    [Tooltip("One line for the briefing: what this hour means.")]
    [TextArea(1, 3)] public string briefing;

    public string DisplayName => string.IsNullOrWhiteSpace(name) ? $"{hour:00}:00" : name;

    /// <summary>The hour a run with this seed gets, or null when the raid has no times of day (the scene's own hour).</summary>
    public static RaidTimeOfDay For(RaidDefinition raid, int seed)
    {
        if (raid == null)
        {
            return null;
        }

        IReadOnlyList<RaidTimeOfDay> times = raid.TimesOfDay;
        int index = Pick(times, RaidLayout.SeedFor(seed, "TimeOfDay"));
        return index >= 0 ? times[index] : null;
    }

    /// <summary>Weighted pick by chance from a seed; -1 when nothing can be picked. Same seed, same pick.</summary>
    public static int Pick(IReadOnlyList<RaidTimeOfDay> times, int seed)
    {
        if (times == null)
        {
            return -1;
        }

        float total = 0f;
        foreach (RaidTimeOfDay time in times)
        {
            total += time != null ? Mathf.Max(0f, time.chance) : 0f;
        }

        if (total <= 0f)
        {
            return -1;
        }

        float roll = (float)new System.Random(seed).NextDouble() * total;
        for (int i = 0; i < times.Count; i++)
        {
            float chance = times[i] != null ? Mathf.Max(0f, times[i].chance) : 0f;
            if (chance <= 0f)
            {
                continue;
            }

            if (roll < chance)
            {
                return i;
            }

            roll -= chance;
        }

        for (int i = times.Count - 1; i >= 0; i--)
        {
            if (times[i] != null && times[i].chance > 0f)
            {
                return i;
            }
        }

        return -1;
    }
}
