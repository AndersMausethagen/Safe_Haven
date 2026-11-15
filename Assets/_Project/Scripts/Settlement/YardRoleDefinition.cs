using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a fenced yard is for, from what stands in it (Docs/BuildingSystemResearch.md §3.4.4, like a RimWorld room's role
/// from its furniture): a Home Yard is a house and a hearth or bench inside one ring; a Feast Green a fire, benches and a
/// table. A yard takes the role whose needs it all meets with the most of what the role looks for; a tie goes to the role
/// listed first (YardSettings › Roles). What a role gives counts once per kind of role, however many yards have it, and is
/// never a penalty.
/// </summary>
[CreateAssetMenu(fileName = "NewYardRole", menuName = "Safe Haven/Buildings/Yard Role")]
public sealed class YardRoleDefinition : ScriptableObject
{
    [Serializable]
    public sealed class Need
    {
        [Tooltip("Any of these counts.")]
        public List<BuildingDefinition> anyOf = new List<BuildingDefinition>();
        [Min(1)] public int count = 1;
        [Tooltip("How the readout names it: \"a house\", \"two benches\".")]
        public string words;
    }

    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [Tooltip("Kit icon (Resources/UIStyle icons).")]
    [SerializeField] private string icon = "Fence";
    [Tooltip("The overlay's tint for yards of this role.")]
    [SerializeField] private Color colour = new Color(0.9f, 0.75f, 0.35f, 1f);

    [Header("What makes it")]
    [SerializeField] private List<Need> needs = new List<Need>();
    [Tooltip("Square metres of ground it needs at least (a paddock wants room).")]
    [SerializeField, Min(0f)] private float minArea;

    [Header("What it gives (once per kind of role)")]
    [Tooltip("Homes inside get this much more quality.")]
    [SerializeField, Min(0)] private int homeQuality;
    [Tooltip("Stations inside work this much faster.")]
    [SerializeField, Range(0f, 1f)] private float stationSpeed;
    [Tooltip("Station extensions inside count for a station inside without the reach rule, and it gets this many more slots.")]
    [SerializeField, Min(0)] private int extensionSlots;
    [Tooltip("Comfort for Well Rested.")]
    [SerializeField, Min(0)] private int comfort;
    [Tooltip("Renown.")]
    [SerializeField, Min(0)] private int renown;
    [Tooltip("The residents gather here of an evening, at its fire, rather than at the fire nearest home.")]
    [SerializeField] private bool gatheringPlace;
    [Tooltip("Horses graze here by day when they aren't in the stable or at work.")]
    [SerializeField] private bool pasture;

    [Header("What it looks like")]
    [Tooltip("Small things set about the yard by its role (washing lines, firewood, hay): looks only, no colliders.")]
    [SerializeField] private List<GameObject> clutter = new List<GameObject>();
    [SerializeField, Range(0, 8)] private int clutterCount = 3;

    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public string Icon => icon;
    public Color Colour => colour;
    public IReadOnlyList<Need> Needs => needs;
    public float MinArea => minArea;
    public int HomeQuality => homeQuality;
    public float StationSpeed => stationSpeed;
    public int ExtensionSlots => extensionSlots;
    public int Comfort => comfort;
    public int Renown => renown;
    public bool GatheringPlace => gatheringPlace;
    public bool Pasture => pasture;
    public IReadOnlyList<GameObject> Clutter => clutter;
    public int ClutterCount => clutterCount;

    /// <summary>How well what stands in a yard fits this role: -1 when a need isn't met, else how many of what it looks
    /// for are there.</summary>
    public int Score(IReadOnlyList<BuildingDefinition> inside, float area)
    {
        if (area < minArea || needs.Count == 0)
        {
            return -1;
        }

        int score = 0;
        foreach (Need need in needs)
        {
            int found = 0;
            foreach (BuildingDefinition kind in inside)
            {
                found += kind != null && need.anyOf.Contains(kind) ? 1 : 0;
            }

            if (found < need.count)
            {
                return -1;
            }

            score += found;
        }

        return score;
    }

    /// <summary>What it still lacks, in words ("a bench or a hearth"), or null when its needs are met.</summary>
    public string Missing(IReadOnlyList<BuildingDefinition> inside, float area)
    {
        foreach (Need need in needs)
        {
            int found = 0;
            foreach (BuildingDefinition kind in inside)
            {
                found += kind != null && need.anyOf.Contains(kind) ? 1 : 0;
            }

            if (found < need.count)
            {
                return need.words;
            }
        }

        return area < minArea ? $"{minArea:0} m² of ground" : null;
    }

    /// <summary>Sets it up from code (tests and editor tools).</summary>
    public void Configure(string title, List<Need> roleNeeds, float area, int homes = 0, int comfortGiven = 0, int renownGiven = 0, bool gathers = false)
    {
        displayName = title;
        needs = roleNeeds ?? new List<Need>();
        minArea = area;
        homeQuality = homes;
        comfort = comfortGiven;
        renown = renownGiven;
        gatheringPlace = gathers;
    }
}
