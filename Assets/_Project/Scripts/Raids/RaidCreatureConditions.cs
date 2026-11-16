using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What raid conditions a creature rule answers to.</summary>
public enum CreatureCondition
{
    /// <summary>The raid's time of day by name (Day, Dusk, Night).</summary>
    TimeOfDay,
    /// <summary>The season at home by name (Summer, Autumn, Late Autumn, Winter, Spring); a rule's name may be part of it.</summary>
    Season,
    /// <summary>It is raining (the raid's weather now, Rain at least the threshold).</summary>
    Rain,
    /// <summary>Thick fog or mist (Fog above the threshold, or Mist at least it, scaled 0-1).</summary>
    Fog,
    /// <summary>Snow is falling.</summary>
    Snow,
}

/// <summary>
/// The forest changes with the hour, the weather and the season (the user, 2026-11-14: "certain enemies can become more
/// plentiful, some may disappear and some new might appear"). Rules scale a creature's chance in every creature spawn
/// pick in a raid (0 = gone), and additions join creature-only picks while their condition holds. Applied in
/// SpawnTable.Pick, so raid-start groups follow the raid's first weather and roaming spawns follow it as it turns. No raid:
/// no change. Lives at Resources/RaidCreatureConditions.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Raids/Raid Creature Conditions", fileName = "RaidCreatureConditions")]
public sealed class RaidCreatureConditions : GameSettings<RaidCreatureConditions>
{
    [Serializable]
    public sealed class Rule
    {
        [Tooltip("The creature it changes: a prefab carrying one (a pack counts by its first creature).")]
        public GameObject creature;
        public CreatureCondition when;
        [Tooltip("Time of day or season name; part of a name matches (Autumn matches Late Autumn). Unused for weather.")]
        public string named;
        [Tooltip("Its chance is multiplied by this while the condition holds: 2 = twice as common, 0 = gone.")]
        [Min(0f)] public float chance = 1f;
    }

    [Serializable]
    public sealed class Addition
    {
        [Tooltip("A creature that joins creature picks only while the condition holds.")]
        public GameObject creature;
        public CreatureCondition when;
        public string named;
        [Tooltip("Its weight beside the table's own (most entries weigh 1).")]
        [Min(0f)] public float weight = 0.4f;
        [Tooltip("From this raid tier on (0: any).")]
        [Range(0, Tiers.Max)] public int fromTier;
    }

    [Tooltip("The year at home (the Haven's Seasons › Cycle), to know the season while away.")]
    [SerializeField] private SeasonCycle seasons;
    [Tooltip("The day of the year a new game begins on: the Haven's Seasons › Start Day.")]
    [SerializeField, Min(0f)] private float yearStartDay = 2f;
    [Tooltip("Rain and mist (0-1) count from this much; fog from 1 + 4 x this (clear air is 1).")]
    [SerializeField, Range(0.05f, 1f)] private float weatherThreshold = 0.35f;
    [SerializeField] private List<Rule> rules = new List<Rule>();
    [SerializeField] private List<Addition> additions = new List<Addition>();

    private static readonly Dictionary<GameObject, MonsterDefinition> creatureOf = new Dictionary<GameObject, MonsterDefinition>();

    // Entering Play doesn't reload scripts: a prefab edited since the last session is read afresh.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => creatureOf.Clear();

    public IReadOnlyList<Rule> Rules => rules;
    public IReadOnlyList<Addition> Additions => additions;

    /// <summary>The conditions in force, or null outside a raid (spawning then goes by the tables alone).</summary>
    public static RaidCreatureConditions Now => GameSession.ActiveRaid != null ? Current : null;

    /// <summary>True while the condition holds in the raid now.</summary>
    public bool Holds(CreatureCondition when, string named)
    {
        switch (when)
        {
            case CreatureCondition.TimeOfDay:
                string hour = GameSession.ActiveRaid?.TimeOfDay?.DisplayName;
                return !string.IsNullOrWhiteSpace(named) && hour != null && hour.IndexOf(named.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            case CreatureCondition.Season:
                string season = SeasonNow();
                return !string.IsNullOrWhiteSpace(named) && season != null && season.IndexOf(named.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
            case CreatureCondition.Rain:
                return WeatherSystem.Active != null && WeatherSystem.Active.Current.Rain >= weatherThreshold && !WeatherSystem.Active.Snowing;
            case CreatureCondition.Fog:
                return WeatherSystem.Active != null && (WeatherSystem.Active.Current.Mist >= weatherThreshold
                                                        || WeatherSystem.Active.Current.Fog >= 1f + 4f * weatherThreshold);
            case CreatureCondition.Snow:
                return WeatherSystem.Active != null && WeatherSystem.Active.Snowing;
            default:
                return false;
        }
    }

    /// <summary>The season at home now, worked out from the days passed (Seasons runs only in the Haven).</summary>
    public string SeasonNow()
    {
        if (Seasons.Today != null)
        {
            return Seasons.Today;
        }

        if (seasons == null)
        {
            return null;
        }

        seasons.At(yearStartDay + GameSession.Here.DaysPassed, out SeasonCycle.Season season, out _, out _);
        return season?.name;
    }

    /// <summary>What a prefab's chance is multiplied by now (1 when no rule touches it).</summary>
    public float ChanceFor(GameObject prefab)
    {
        if (prefab == null || rules.Count == 0)
        {
            return 1f;
        }

        MonsterDefinition creature = CreatureIn(prefab);
        float chance = 1f;
        foreach (Rule rule in rules)
        {
            if (rule.creature != null && (rule.creature == prefab || (creature != null && CreatureIn(rule.creature) == creature))
                && Holds(rule.when, rule.named))
            {
                chance *= rule.chance;
            }
        }

        return chance;
    }

    /// <summary>The additions whose condition holds at this tier.</summary>
    public void AddedNow(int tier, List<Addition> into)
    {
        into.Clear();
        foreach (Addition addition in additions)
        {
            if (addition.creature != null && addition.weight > 0f && (addition.fromTier <= 0 || tier >= addition.fromTier)
                && Holds(addition.when, addition.named))
            {
                into.Add(addition);
            }
        }
    }

    /// <summary>The creature a prefab carries (its first), cached; null for anything else.</summary>
    public static MonsterDefinition CreatureIn(GameObject prefab)
    {
        if (prefab == null)
        {
            return null;
        }

        if (!creatureOf.TryGetValue(prefab, out MonsterDefinition creature))
        {
            MonsterAI ai = prefab.GetComponentInChildren<MonsterAI>(true);
            creature = ai != null ? ai.Definition : null;
            creatureOf[prefab] = creature;
        }

        return creature;
    }
}
