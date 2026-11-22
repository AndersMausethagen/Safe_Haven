using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Forays: residents sent through the portal to gather on their own (Forays; the user, 2026-11-22: "send NPC's on a gathering raid
/// themselves, like Palworld does. With % chance of success, based on their level, equipment, number of NPC's"). Where they can go,
/// what each brings home, and how a party's power is reckoned.
/// </summary>
[CreateAssetMenu(fileName = "ForaySettings", menuName = "Safe Haven/Settings/Forays")]
public sealed class ForaySettings : GameSettings<ForaySettings>
{
    /// <summary>A chance at between <see cref="least"/> and <see cref="most"/> of an item, for a foray's haul.</summary>
    [Serializable]
    public sealed class Loot
    {
        public ItemDefinition item;
        [Min(0)] public int least = 1;
        [Min(0)] public int most = 3;
        [Range(0f, 1f)] public float chance = 1f;
    }

    /// <summary>A place a foray can go: a raid's land at a tier, open once the portal opens that tier.</summary>
    [Serializable]
    public sealed class Destination
    {
        public string id = "misty-woods-1";
        [Tooltip("The raid whose land they go to; its name is shown.")]
        public RaidDefinition raid;
        [Tooltip("The tier: the portal must open it first (PortalTiers), as a raid of that tier would.")]
        [Range(1, 5)] public int tier = 1;
        [Tooltip("Minutes away at exactly the power it asks for (played time, home or raiding): more power is quicker, less slower.")]
        [Min(1f)] public float minutes = 20f;
        [Tooltip("The party's power it asks for: at it, the odds are Chance At Power and the haul is whole.")]
        [Min(1f)] public float power = 30f;
        [Tooltip("What they may bring home, each its own chance.")]
        public List<Loot> loot = new List<Loot>();
    }

    [Header("Where")]
    [SerializeField] private List<Destination> destinations = new List<Destination>();

    [Header("Who goes")]
    [Tooltip("At most this many go on one foray.")]
    [SerializeField, Range(1, 8)] private int mostMembers = 4;
    [Tooltip("Power each one brings, before their rank, their raids and their gear.")]
    [SerializeField, Min(0f)] private float basePower = 10f;
    [Tooltip("More for each worker rank above the first (SettlementSettings › Worker Ranks): their level.")]
    [SerializeField, Min(0f)] private float powerPerRank = 5f;
    [Tooltip("More for each raid they went along on with the player, up to Most Raids Counted: they know the woods.")]
    [SerializeField, Min(0f)] private float powerPerRaid = 1f;
    [SerializeField, Min(0)] private int mostRaidsCounted = 10;
    [Tooltip("More for each tier of the worker gear the player gave them: their equipment.")]
    [SerializeField, Min(0f)] private float powerPerGearTier = 4f;
    [Tooltip("More for one well fed this morning (ResidentMeals).")]
    [SerializeField, Min(0f)] private float wellFedPower = 3f;
    [Tooltip("Food each takes for the road from the chests, in hunger (ResidentMeals' order: cooked first). 0: none.")]
    [SerializeField, Min(0f)] private float roadHunger = 40f;

    [Header("Odds, time and haul (power against what the place asks)")]
    [Tooltip("The odds at exactly the power asked for.")]
    [SerializeField, Range(0f, 1f)] private float chanceAtPower = 0.85f;
    [Tooltip("The odds with no power at all (the line runs from here through Chance At Power), and the most they can reach.")]
    [SerializeField, Range(0f, 1f)] private float leastChance = 0.15f;
    [SerializeField, Range(0f, 1f)] private float mostChance = 0.95f;
    [Tooltip("Shortest and longest share of the minutes: twice the power asked for is quicker, half of it slower (as Palworld's).")]
    [SerializeField, Range(0.1f, 1f)] private float shortest = 0.6f;
    [SerializeField, Range(1f, 3f)] private float longest = 1.5f;
    [Tooltip("The haul is scaled by power over what is asked, between these.")]
    [SerializeField, Range(0.1f, 1f)] private float leastHaul = 0.5f;
    [SerializeField, Range(1f, 3f)] private float mostHaul = 1.5f;
    [Tooltip("A foray that goes wrong brings home this share of its haul, and those who went come home bruised: slower at work until " +
             "the next dawn. Nobody is lost.")]
    [SerializeField, Range(0f, 1f)] private float failedHaul = 0.25f;
    [SerializeField, Range(0f, 1f)] private float bruisedWork = 0.3f;

    public IReadOnlyList<Destination> Destinations => destinations;
    public int MostMembers => mostMembers;
    public float BasePower => basePower;
    public float PowerPerRank => powerPerRank;
    public float PowerPerRaid => powerPerRaid;
    public int MostRaidsCounted => mostRaidsCounted;
    public float PowerPerGearTier => powerPerGearTier;
    public float WellFedPower => wellFedPower;
    public float RoadHunger => roadHunger;
    public float ChanceAtPower => chanceAtPower;
    public float LeastChance => leastChance;
    public float MostChance => mostChance;
    public float Shortest => shortest;
    public float Longest => longest;
    public float LeastHaul => leastHaul;
    public float MostHaul => mostHaul;
    public float FailedHaul => failedHaul;
    public float BruisedWork => bruisedWork;

    public Destination Find(string id) => destinations.Find(d => d != null && d.id == id);
}
