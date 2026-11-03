using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>When a rumour can be true: some things are only out there at some hours.</summary>
public enum RumourHour
{
    Any = 0,
    /// <summary>Only when the raid rolled night or dusk.</summary>
    Dark = 1,
}

/// <summary>
/// Something the portal's briefing says is out there on this run (GDD §7.5), and it is true: the raid's event of the same
/// name stands at a place chosen for it on the map, shown on the HUD from the first moment. It gives a run its reason. A
/// rumour only comes up while it can still matter: nobody is sought who has come home, and no plans for what is already
/// known. To add one: a new asset in the raid's Raid Rumours pool, with a raid event for what is found there.
/// </summary>
[CreateAssetMenu(fileName = "RaidRumour", menuName = "Safe Haven/Raids/Raid Rumour")]
public sealed class RaidRumourDefinition : ScriptableObject
{
    [Tooltip("Stable unique identifier.")]
    [SerializeField] private string id;
    [Tooltip("The line in the briefing: \"A wounded smith was seen near the old mine.\" Says what, not where.")]
    [SerializeField, TextArea] private string briefing;
    [Tooltip("What stands there. Its site, choices and outcomes are the rumour's reward.")]
    [SerializeField] private RaidEventDefinition site;
    [Tooltip("Relative chance against the other rumours that could come up.")]
    [SerializeField, Min(0f)] private float weight = 1f;

    [Header("When it comes up")]
    [SerializeField] private RumourHour hour = RumourHour.Any;
    [Tooltip("Only while this person hasn't come home.")]
    [SerializeField] private ResidentDefinition seeks;
    [Tooltip("Only while the player doesn't yet know everything this item teaches.")]
    [SerializeField] private ItemDefinition teaches;
    [Tooltip("Only while a ward breach is waiting or close at hand.")]
    [SerializeField] private bool breachComing;
    [Tooltip("Not before this many raids have been survived.")]
    [SerializeField, Min(0)] private int afterExtractions;
    [Tooltip("Only once this building stands at home: a craftsman is sought when there is somewhere for them to work.")]
    [SerializeField] private BuildingDefinition needsBuilding;
    [Tooltip("The tier that building must stand at.")]
    [SerializeField, Min(1)] private int needsTier = 1;
    [Tooltip("Comes up ahead of any chosen at random, as soon as it can.")]
    [SerializeField] private bool urgent;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string Briefing => briefing;
    public RaidEventDefinition Site => site;
    public float Weight => weight;
    public bool Urgent => urgent;

    /// <summary>Whether it can come up for this hour and this state of the game.</summary>
    public bool CanComeUp(RaidTimeOfDay timeOfDay, SettlementState settlement, PlayerCrafting crafting)
    {
        if (site == null || weight <= 0f || settlement.SuccessfulExtractions < afterExtractions)
        {
            return false;
        }

        if (hour == RumourHour.Dark && (timeOfDay == null || !timeOfDay.IsDark))
        {
            return false;
        }

        if (seeks != null && settlement.HasResident(seeks))
        {
            return false;
        }

        if (needsBuilding != null && settlement.BuiltTier(needsBuilding) < needsTier)
        {
            return false;
        }

        return (teaches == null || crafting == null || !crafting.KnowsAll(teaches)) && (!breachComing || settlement.BreachIsNear);
    }
}

/// <summary>Picks which rumours a run has. The briefing and the raid ask with the same seed, so they always agree.</summary>
public static class RaidRumours
{
    /// <summary>
    /// The rumours of a run: as many as the raid's Raid Rumours system asks for, plus what a resident's job adds. Empty for a
    /// raid without one.
    /// </summary>
    public static List<RaidRumourDefinition> For(RaidDefinition raid, int seed, SettlementState settlement, PlayerCrafting crafting)
    {
        var picked = new List<RaidRumourDefinition>();
        RaidRumoursSystem system = raid != null ? raid.GetSystem<RaidRumoursSystem>() : null;
        if (system == null || !system.Enabled)
        {
            return picked;
        }

        RaidTimeOfDay hour = RaidTimeOfDay.For(raid, seed);
        var candidates = new List<RaidRumourDefinition>();
        var weights = new List<float>();
        foreach (RaidRumourDefinition rumour in system.Pool)
        {
            if (rumour != null && rumour.CanComeUp(hour, settlement, crafting))
            {
                candidates.Add(rumour);
                weights.Add(rumour.Weight);
            }
        }

        var random = new System.Random(RaidLayout.SeedFor(seed, "Rumours"));
        int wanted = system.Count + settlement.JobBonus(job => job.ExtraRumours) + HavenReadiness.ExtraRumours;
        for (int i = candidates.Count - 1; i >= 0 && picked.Count < wanted; i--)
        {
            if (candidates[i].Urgent)
            {
                picked.Add(candidates[i]);
                candidates.RemoveAt(i);
                weights.RemoveAt(i);
            }
        }

        while (picked.Count < wanted && candidates.Count > 0)
        {
            int index = RaidLayout.PickWeighted(random, weights);
            if (index < 0)
            {
                break;
            }

            picked.Add(candidates[index]);
            candidates.RemoveAt(index);
            weights.RemoveAt(index);
        }

        return picked;
    }
}

/// <summary>A raid's rumours: what the briefing can promise, and how many a run has.</summary>
[Serializable]
public sealed class RaidRumoursSystem : RaidSystem
{
    [SerializeField] private List<RaidRumourDefinition> pool = new List<RaidRumourDefinition>();
    [Tooltip("How many rumours a run has, before a Tracker's help.")]
    [SerializeField, Min(0)] private int count = 2;

    public IReadOnlyList<RaidRumourDefinition> Pool => pool;
    public int Count => count;
    public override string DisplayName => "Raid Rumours";
    public override string Description => "What the briefing says is out there this run, each standing at its own place on the map.";

    public override void Install(RaidDirector director)
    {
        if (!director.TryGetComponent(out RaidEventDirector _))
        {
            director.gameObject.AddComponent<RaidEventDirector>();
        }

        if (!director.TryGetComponent(out RaidRumourDirector _))
        {
            director.gameObject.AddComponent<RaidRumourDirector>();
        }
    }

#if UNITY_EDITOR
    /// <summary>Fills in the pool (builders and tests).</summary>
    public void Configure(IEnumerable<RaidRumourDefinition> rumours, int perRun)
    {
        pool = new List<RaidRumourDefinition>(rumours);
        count = perRun;
    }
#endif
}
