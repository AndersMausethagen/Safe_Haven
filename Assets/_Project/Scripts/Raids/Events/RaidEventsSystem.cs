using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>A raid's events: the pool they are drawn from, how often they come and the budget (GDD §9.4).</summary>
[Serializable]
public sealed class RaidEventSettings
{
    [Tooltip("What can happen in this raid. Each happens at most once a raid.")]
    public List<RaidEventDefinition> pool = new List<RaidEventDefinition>();
    [Tooltip("Seconds into the raid before the first one.")]
    [Min(0f)] public float firstDelay = 45f;
    [Tooltip("Seconds between events, on average.")]
    [Min(5f)] public float interval = 110f;
    [Tooltip("Seconds the interval can be shorter or longer.")]
    [Min(0f)] public float intervalSpread = 35f;
    [Tooltip("The most events in one raid.")]
    [Min(0)] public int maxPerRaid = 4;
    [Tooltip("No new event while this many sites still wait for the player, so they don't pile up.")]
    [Min(1)] public int maxOpenSites = 2;
    [Tooltip("What kind of day it is out there (Docs/RaidEventsPlan_2026-11-26.md E5): at most one a run, rolled from its seed and named " +
             "in the briefing, making some events likelier.")]
    public List<RaidEventCondition> conditions = new List<RaidEventCondition>();

    /// <summary>The run's condition, or null: one roll from the seed against each condition's chance in turn. Pure, for tests.</summary>
    public RaidEventCondition ConditionFor(int seed)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return null;
        }

        double roll = new System.Random(RaidLayout.SeedFor(seed, "Event Condition")).NextDouble();
        double reached = 0d;
        foreach (RaidEventCondition condition in conditions)
        {
            if (condition == null)
            {
                continue;
            }

            reached += Mathf.Clamp01(condition.chance);
            if (roll < reached)
            {
                return condition;
            }
        }

        return null;
    }

    /// <summary>The events settings of a raid, or null when it has no Raid Events system.</summary>
    public static RaidEventSettings Of(RaidDefinition raid)
    {
        if (raid == null)
        {
            return null;
        }

        foreach (RaidSystem system in raid.Systems)
        {
            if (system is RaidEventsSystem events && events.Enabled)
            {
                return events.Settings;
            }
        }

        return null;
    }
}

/// <summary>A kind of day a raid can have, told in the briefing: "The trolls are cooking today", and some events likelier.</summary>
[Serializable]
public sealed class RaidEventCondition
{
    public string name = "The trolls are cooking today";
    [TextArea(1, 3)] public string briefing = "";
    [Tooltip("The chance a run has this condition (the conditions' chances are taken in turn, together at most 1).")]
    [Range(0f, 1f)] public float chance = 0.2f;
    [Tooltip("The events it makes likelier.")]
    public List<RaidEventDefinition> events = new List<RaidEventDefinition>();
    [Tooltip("How many times likelier.")]
    [Min(1f)] public float times = 3f;

    /// <summary>How many times likelier it makes an event: <see cref="times"/> for its own, 1 for the rest.</summary>
    public float Boosts(RaidEventDefinition definition) => events != null && events.Contains(definition) ? times : 1f;
}

/// <summary>Things happen during the raid: warnings, places to reach and choices to make (RaidEventDirector).</summary>
[Serializable]
public sealed class RaidEventsSystem : RaidSystem
{
    [SerializeField] private RaidEventSettings settings = new RaidEventSettings();

    public RaidEventSettings Settings => settings;
    public override string DisplayName => "Raid Events";
    public override string Description => "Random events during the raid, each with a warning and often a place to reach and a choice to make.";

    public override void Install(RaidDirector director)
    {
        if (!director.TryGetComponent(out RaidEventDirector events))
        {
            events = director.gameObject.AddComponent<RaidEventDirector>();
        }

        events.Configure(settings);
    }
}
