using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What a home is like, and what a good one does (Docs/BuildingSystemResearch.md §3.4.2).</summary>
public enum HomeFeatureKind
{
    /// <summary>One of the listed buildings stands within reach of the door.</summary>
    Near,
    /// <summary>A light (a building's VisibilitySource) reaches the door, one of the listed buildings stands near, or a
    /// paved path passes it: a lit or paved lane.</summary>
    Lane,
    /// <summary>Someone who lives here works within reach.</summary>
    Work,
}

/// <summary>
/// Homes and the life round them (Docs/BuildingSystemResearch.md §3.4, the user's answers of 2026-11-15): what makes a home
/// good (its surroundings, its tier, the comforts put in it), what a good home gives (each kind capped once: work speed,
/// Well Rested comfort, renown), what each resident likes, and the Carpenter and Mason finishing scaffolds while the player
/// raids. Everything is positive: nothing near a home makes it worse. Lives at Resources/HomeLifeSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Home Life Settings", fileName = "HomeLifeSettings")]
public sealed class HomeLifeSettings : GameSettings<HomeLifeSettings>
{
    [Serializable]
    public sealed class Feature
    {
        public string name = "Warm";
        [Tooltip("Kit icon (Resources/UIStyle icons).")]
        public string icon = "Flame";
        public HomeFeatureKind kind;
        [Tooltip("What counts (Near and Lane).")]
        public List<BuildingDefinition> near = new List<BuildingDefinition>();
        [Tooltip("Metres from the door.")]
        [Min(0f)] public float within = 12f;
        [Tooltip("Lane: metres a paved path may pass from the door.")]
        [Min(0f)] public float pathWithin = 4f;
        [Tooltip("A house comfort that counts as this too (a kitchen hearth is warm). Optional.")]
        public string orComfort;
        [Tooltip("What to build to have it, for the readout.")]
        public string hint;
        [Min(0)] public int points = 1;
    }

    [Serializable]
    public sealed class Comfort
    {
        public string id = "beds";
        public string name = "Proper Beds";
        public string icon = "Bed";
        [TextArea(1, 3)] public string description;
        public List<ItemAmount> cost = new List<ItemAmount>();
        [Min(0)] public int points = 1;
    }

    [Serializable]
    public sealed class Level
    {
        public string name = "Plain";
        [Tooltip("Home quality needed.")]
        [Min(0)] public int quality;
        [Tooltip("How much faster those who live here work, at their own station.")]
        [Range(0f, 1f)] public float workSpeed;
        [Tooltip("Comfort for Well Rested. Only the best home counts.")]
        [Min(0)] public int comfort;
        [Tooltip("Renown. Only the best home counts.")]
        [Min(0)] public int renown;
    }

    [Serializable]
    public sealed class Liking
    {
        public ResidentDefinition resident;
        [Tooltip("The feature's name they care for.")]
        public string likes;
        [Tooltip("What they say of it, shown in their window.")]
        public string line;
        [Tooltip("What they say the first time their home has it (HomeWishes), aloud where they are.")]
        public string thanks;
    }

    [Header("What makes a home")]
    [SerializeField] private List<Feature> features = new List<Feature>();
    [Tooltip("Points for each tier above the first (a Thatched Hut).")]
    [SerializeField, Min(0)] private int pointsPerTier = 1;
    [Tooltip("What can be put in a house, bought from a resident's window.")]
    [SerializeField] private List<Comfort> comforts = new List<Comfort>();

    [Header("What a good home gives")]
    [Tooltip("Lowest first: the best one a home's quality reaches is its level.")]
    [SerializeField] private List<Level> levels = new List<Level>
    {
        new Level { name = "Bare", quality = 0 },
        new Level { name = "Plain", quality = 2, workSpeed = 0.05f, comfort = 1, renown = 2 },
        new Level { name = "Snug", quality = 4, workSpeed = 0.1f, comfort = 2, renown = 4 },
        new Level { name = "Fine", quality = 6, workSpeed = 0.15f, comfort = 3, renown = 6 },
        new Level { name = "Grand", quality = 8, workSpeed = 0.2f, comfort = 4, renown = 8 },
    };
    [Tooltip("What each one cares for in a home. Having it makes them content: they work this much faster.")]
    [SerializeField] private List<Liking> likings = new List<Liking>();
    [SerializeField, Range(0f, 0.5f)] private float contentWorkSpeed = 0.05f;

    [Header("Moving house")]
    [Tooltip("Metres nearer their work a free bed must be before someone leaves the house they live in for it (homes are " +
             "sticky: a nearer Cottage built later doesn't take them from the house the player furnished). A house that is " +
             "gone, or the player's choice, moves them whatever this is.")]
    [SerializeField, Min(0f)] private float moveHomeFor = 15f;

    [Header("Paths")]
    [Tooltip("The player's speed on a path is only raised at home, in the ward.")]
    [SerializeField] private bool pathsOnlyAtHome = true;
    [Tooltip("Worn footpaths: how much a step wears the ground, and how much it grows back each second.")]
    [SerializeField, Min(0f)] private float wearPerStep = 0.04f;
    [SerializeField, Min(0f)] private float wearRegrowth = 0.0006f;

    [Header("Building while you are away")]
    [Tooltip("Jobs whose residents finish scaffolds while the player is on a raid.")]
    [SerializeField] private List<JobDefinition> builders = new List<JobDefinition>();
    [Tooltip("Seconds of build work each of them does per second the player is away.")]
    [SerializeField, Min(0f)] private float builderWorkPerSecond = 0.5f;

    public IReadOnlyList<Feature> Features => features;
    public int PointsPerTier => pointsPerTier;
    public IReadOnlyList<Comfort> Comforts => comforts;
    public IReadOnlyList<Level> Levels => levels;
    public IReadOnlyList<Liking> Likings => likings;
    public float ContentWorkSpeed => contentWorkSpeed;
    public float MoveHomeFor => moveHomeFor;
    public bool PathsOnlyAtHome => pathsOnlyAtHome;
    public float WearPerStep => wearPerStep;
    public float WearRegrowth => wearRegrowth;
    public IReadOnlyList<JobDefinition> Builders => builders;
    public float BuilderWorkPerSecond => builderWorkPerSecond;

    /// <summary>The level a home of this quality reaches (the best whose quality it has).</summary>
    public Level LevelFor(int quality)
    {
        Level best = levels.Count > 0 ? levels[0] : new Level { name = "Bare" };
        foreach (Level level in levels)
        {
            if (quality >= level.quality && level.quality >= best.quality)
            {
                best = level;
            }
        }

        return best;
    }

    public Comfort FindComfort(string id) => comforts.Find(one => one.id == id);

    public Liking LikingOf(ResidentDefinition resident) => likings.Find(one => one.resident == resident);

    /// <summary>Sets what is found by name (tests and editor tools).</summary>
    public void Configure(List<Feature> homeFeatures, List<Comfort> houseComforts, List<Liking> residentLikings, List<JobDefinition> builderJobs)
    {
        features = homeFeatures ?? new List<Feature>();
        comforts = houseComforts ?? new List<Comfort>();
        likings = residentLikings ?? new List<Liking>();
        builders = builderJobs ?? new List<JobDefinition>();
    }
}
