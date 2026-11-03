using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tuning for Safe Haven as a place (GDD §6, §7.3, §13.5): repairing the portal, Portal Spark time extensions, the Heart
/// Tree's ward and its upgrades, and the Purification Altar. Read by RaidPortal, RaidPreparationWindow, HomePortal,
/// BuildPlacer and SettlementSystem. Edit it on the Settlement page of the Systems Editor.
/// </summary>
[CreateAssetMenu(fileName = "SettlementSettings", menuName = "Safe Haven/Settings/Settlement Settings")]
public sealed class SettlementSettings : GameSettings<SettlementSettings>
{
    [System.Serializable]
    public sealed class WardLevel
    {
        [Tooltip("What strengthening the ward to this level costs.")]
        public List<ResourceAmount> cost = new List<ResourceAmount>();
        [Tooltip("Meters added to the buildable radius at this level.")]
        [Min(0f)] public float extraRadius = 6f;
        [Tooltip("Seconds added to every raid: a stronger Portal holds the way home open for longer.")]
        [Min(0f)] public float raidSeconds = 90f;
        [Tooltip("What it opens, for the player: \"Opens tier II raids.\" Which tier a level opens is set on the raid's difficulties.")]
        public string opens;
    }

    /// <summary>One improvement of the portal: what it does for the moment things go wrong out there.</summary>
    [System.Serializable]
    public sealed class PortalLevel
    {
        public string title = "Steadied";
        [TextArea(1, 3)] public string description;
        public List<ResourceAmount> cost = new List<ResourceAmount>();
        public List<ItemAmount> items = new List<ItemAmount>();
        [Tooltip("More slots in the safe pocket, whose contents survive a lost raid.")]
        [Min(0)] public int extraPocketSlots;
        [Tooltip("Share off the time an extraction takes. Levels add up.")]
        [Range(0f, 0.9f)] public float extractionCut;
    }

    [Header("Daily Life")]
    [Tooltip("The hour residents get up.")]
    [SerializeField, Range(0f, 24f)] private float residentWakeHour = 6.5f;
    [Tooltip("The hour they go to their workplace. Before that, and with no workplace, they are at the fire.")]
    [SerializeField, Range(0f, 24f)] private float residentWorkFromHour = 8f;
    [SerializeField, Range(0f, 24f)] private float residentMealFromHour = 12f;
    [SerializeField, Range(0f, 24f)] private float residentMealUntilHour = 13f;
    [SerializeField, Range(0f, 24f)] private float residentWorkUntilHour = 18f;
    [Tooltip("The hour they go to their own spot to sleep.")]
    [SerializeField, Range(0f, 24f)] private float residentSleepFromHour = 22f;
    [Tooltip("Where they eat and sit of an evening. Without one built: the portal.")]
    [SerializeField] private BuildingDefinition residentGatheringPlace;
    [SerializeField, Min(0.1f)] private float residentWalkSpeed = 1.6f;
    [Tooltip("Their speed when they run for the portal.")]
    [SerializeField, Min(0.1f)] private float residentRunSpeed = 3.8f;
    [Tooltip("They run for the portal when something hunting is this close, in metres, or while a breach is on.")]
    [SerializeField, Min(0f)] private float residentDangerRadius = 9f;
    [SerializeField] private EmoteDefinition residentMealGesture;
    [SerializeField] private EmoteDefinition residentSleepGesture;
    [Tooltip("What they do at the fire, one after another at random.")]
    [SerializeField] private EmoteDefinition[] residentFireGestures = new EmoteDefinition[0];
    [Tooltip("Where a resident's right hand holds a work tool, in the hand's own space.")]
    [SerializeField] private Vector3 residentToolGripPosition;
    [SerializeField] private Vector3 residentToolGripRotation;

    public ResidentHours ResidentHours => new ResidentHours(residentWakeHour, residentWorkFromHour, residentMealFromHour, residentMealUntilHour, residentWorkUntilHour, residentSleepFromHour);
    public BuildingDefinition ResidentGatheringPlace => residentGatheringPlace;
    public float ResidentWalkSpeed => residentWalkSpeed;
    public float ResidentRunSpeed => residentRunSpeed;
    public float ResidentDangerRadius => residentDangerRadius;
    public Companion RaidCompanionPrefab => raidCompanionPrefab;
    public FallenCairn FallenCairn => fallenCairn;
    public string[] GriefLines => griefLines;
    public int GriefRaids => griefRaids;
    public EmoteDefinition ResidentMealGesture => residentMealGesture;
    public EmoteDefinition ResidentSleepGesture => residentSleepGesture;
    public EmoteDefinition[] ResidentFireGestures => residentFireGestures;
    public Pose ResidentToolGrip => new Pose(residentToolGripPosition, Quaternion.Euler(residentToolGripRotation));

    [Header("Going Along on Raids")]
    [Tooltip("The body of someone who goes along on a raid: collider, agent, Combatant, Damageable, Companion and Talkable. Their model and how they fight come from their ResidentDefinition.")]
    [SerializeField] private Companion raidCompanionPrefab;
    [Tooltip("Raised near the portal for each person who didn't come home.")]
    [SerializeField] private FallenCairn fallenCairn;
    [Tooltip("What those at home say about someone lost, with {0} for the name. One is kept per person, so they stay in character.")]
    [SerializeField, TextArea] private string[] griefLines = new string[0];
    [Tooltip("For how many raids after a loss they still speak of it.")]
    [SerializeField, Min(0)] private int griefRaids = 2;

    [Header("Portal")]
    [Tooltip("What repairing the ruined portal costs (GDD §14.3: Life Orbs from the clearing).")]
    [SerializeField] private List<ResourceAmount> portalRepairCost = new List<ResourceAmount>();
    [Tooltip("The resource spent to extend a raid (GDD §7.3).")]
    [SerializeField] private ResourceDefinition portalSpark;
    [Tooltip("Seconds each Portal Spark adds to a raid.")]
    [SerializeField, Min(0f)] private float secondsPerSpark = 180f;
    [Tooltip("Most Portal Sparks one raid can use.")]
    [SerializeField, Min(0)] private int maxSparksPerRaid = 2;

    [Tooltip("Improvements made at the portal, in order.")]
    [SerializeField] private List<PortalLevel> portalLevels = new List<PortalLevel>();

    [Header("Portal ward")]
    [Tooltip("Radius around the portal, in meters, where the player may build before any upgrade.")]
    [SerializeField, Min(1f)] private float baseWardRadius = 16f;
    [Tooltip("Upgrades bought at the portal, in order. Each grows the buildable area (GDD §6.4).")]
    [SerializeField] private List<WardLevel> wardLevels = new List<WardLevel>();

    [Header("Purification (GDD §13.5)")]
    [SerializeField] private ResourceDefinition cursedEssence;
    [SerializeField] private ResourceDefinition purifiedEssence;
    [Tooltip("Cursed Essence used per batch.")]
    [SerializeField, Min(1)] private int cursedPerBatch = 2;
    [Tooltip("Purified Essence made per batch.")]
    [SerializeField, Min(1)] private int purifiedPerBatch = 1;
    [Tooltip("Seconds one batch takes.")]
    [SerializeField, Min(0.1f)] private float secondsPerBatch = 20f;
    [Tooltip("Batches that can wait in the altar's queue.")]
    [SerializeField, Min(1)] private int maxQueuedBatches = 50;
    [Tooltip("The job a resident must hold for the altar to work.")]
    [SerializeField] private JobDefinition purifierJob;

    public IReadOnlyList<ResourceAmount> PortalRepairCost => portalRepairCost;
    public ResourceDefinition PortalSpark => portalSpark;
    public float SecondsPerSpark => secondsPerSpark;
    public int MaxSparksPerRaid => maxSparksPerRaid;
    public float BaseWardRadius => baseWardRadius;
    public IReadOnlyList<WardLevel> WardLevels => wardLevels;
    public IReadOnlyList<PortalLevel> PortalLevels => portalLevels;

    /// <summary>Safe pocket slots the portal's improvements add, at a level.</summary>
    public int ExtraPocketSlotsAt(int level)
    {
        int slots = 0;
        for (int i = 0; i < level && i < portalLevels.Count; i++)
        {
            slots += portalLevels[i].extraPocketSlots;
        }

        return slots;
    }

    /// <summary>Share off the extraction time the portal's improvements add up to, at a level.</summary>
    public float ExtractionCutAt(int level)
    {
        float cut = 0f;
        for (int i = 0; i < level && i < portalLevels.Count; i++)
        {
            cut += portalLevels[i].extractionCut;
        }

        return cut;
    }
    public ResourceDefinition CursedEssence => cursedEssence;
    public ResourceDefinition PurifiedEssence => purifiedEssence;
    public int CursedPerBatch => cursedPerBatch;
    public int PurifiedPerBatch => purifiedPerBatch;
    public float SecondsPerBatch => secondsPerBatch;
    public int MaxQueuedBatches => maxQueuedBatches;
    public JobDefinition PurifierJob => purifierJob;

    /// <summary>The buildable radius at a ward level (0 = not upgraded).</summary>
    public float WardRadiusAt(int level)
    {
        float radius = baseWardRadius;
        for (int i = 0; i < level && i < wardLevels.Count; i++)
        {
            radius += wardLevels[i].extraRadius;
        }

        return radius;
    }
}
