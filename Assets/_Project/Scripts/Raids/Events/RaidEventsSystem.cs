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
