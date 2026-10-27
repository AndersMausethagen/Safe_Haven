using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored data for one raid destination: its name, the scene it plays in and its base rules (time limit, extraction
/// time, loot and enemy strength). Raid portals point at one. Modifiers change these values for a single run through
/// RaidParameters; the definition itself never changes while playing.
/// </summary>
[CreateAssetMenu(fileName = "NewRaid", menuName = "Safe Haven/Raids/Raid")]
public sealed class RaidDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for lookups and saves.")]
    [SerializeField] private string id;
    [Tooltip("Shown in the UI. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [Tooltip("How dangerous and rewarding the raid is, shown to the player.")]
    [SerializeField, Range(1, Tiers.Max)] private int tier = 1;

    [Header("Scene")]
    [Tooltip("Name of the scene loaded for this raid. It must be listed in Build Settings.")]
    [SerializeField] private string sceneName;
    [Tooltip("Shown in the raid planner but can't be entered yet: a destination for later (GDD §21.5 \"a harder expedition\").")]
    [SerializeField] private bool locked;
    [Tooltip("Why a locked destination can't be entered, shown in the planner.")]
    [SerializeField, TextArea] private string lockedText = "The way there is not yet open.";

    [Header("Briefing (GDD §7.5)")]
    [Tooltip("Enemies the player is told about before entering.")]
    [SerializeField] private MonsterDefinition[] knownEnemies = new MonsterDefinition[0];
    [Tooltip("Materials and other items the player can expect to find.")]
    [SerializeField] private ItemDefinition[] likelyItems = new ItemDefinition[0];
    [Tooltip("Essences the player can expect to find.")]
    [SerializeField] private ResourceDefinition[] likelyResources = new ResourceDefinition[0];

    [Header("Base Rules")]
    [Tooltip("Seconds the player has to extract before the raid collapses (GDD §7.3: 12 minutes). Portal Sparks add more.")]
    [SerializeField, Min(RaidParameters.MinimumDuration)] private float durationSeconds = 720f;
    [Tooltip("Seconds the player must hold a lit extraction point's circle to leave (time outside it or contested doesn't count).")]
    [SerializeField, Min(RaidParameters.MinimumExtractionTime)] private float extractionSeconds = 5f;
    [Tooltip("Multiplier on what supply caches hold. 1 is normal.")]
    [SerializeField, Min(0f)] private float lootMultiplier = 1f;
    [Tooltip("Multiplier on enemies' health. 1 is normal.")]
    [SerializeField, Min(0.1f)] private float enemyHealthMultiplier = 1f;
    [Tooltip("Multiplier on enemies' physical and spell power. 1 is normal.")]
    [SerializeField, Min(0f)] private float enemyPowerMultiplier = 1f;

    [Header("Time of Day")]
    [Tooltip("The hours this raid can take place at, rolled from each run's seed (shown in the briefing). Empty: the " +
             "scene's own hour. Night is darker for the player and the monsters alike, and should pay more.")]
    [SerializeField] private List<RaidTimeOfDay> timesOfDay = new List<RaidTimeOfDay>();

    [Header("Spawns")]
    [Tooltip("Multiplier on how many creatures every enemy spawn list places (its min and max count), on top of the " +
             "global density. 1 is as authored. A list never fills more points than it has.")]
    [SerializeField, Range(0f, 4f)] private float enemyDensity = 1f;
    [Tooltip("Use this raid's own Near/Far distances instead of the global raid rules.")]
    [SerializeField] private bool overrideBands;
    [Tooltip("Spawn points closer than this to the player start (flat metres) count as Near for a list's Entry Band.")]
    [SerializeField, Min(0f)] private float nearDistance = SpawnRules.DefaultNearDistance;
    [Tooltip("Spawn points farther than this from the player start count as Far; between Near and Far is Middle.")]
    [SerializeField, Min(0f)] private float farDistance = SpawnRules.DefaultFarDistance;

    [Tooltip("What spawns where: each list fills points of the scene's Spawn Groups (named) from its entries.")]
    [SerializeField] private List<RaidSpawnList> spawnLists = new List<RaidSpawnList>();

    [Header("Systems")]
    [Tooltip("Systems only this raid runs, such as roaming spawns. Edited in the Systems Editor (Raids > Raid Systems).")]
    [SerializeReference] private List<RaidSystem> systems = new List<RaidSystem>();

    [Header("Roaming (legacy)")]
    [Tooltip("Moved to the Roaming Spawns raid system; kept only until old assets are migrated.")]
    [SerializeField, HideInInspector] private RoamingSpawnSettings roaming = new RoamingSpawnSettings();
    [Header("Escalation (GDD §7.3)")]
    [Tooltip("Danger stages as the clock runs down, in order. Each can strengthen enemies and wake spawn groups set to " +
             "its number (1 is the first).")]
    [SerializeField] private List<RaidStage> stages = new List<RaidStage>();

    [Header("Results Screen")]
    [Tooltip("Optional artwork behind the report after getting out alive. Empty uses the results screen's default.")]
    [SerializeField] private Sprite extractedArtwork;
    [Tooltip("Optional artwork behind the report after dying or the raid collapsing. Empty uses the default.")]
    [SerializeField] private Sprite failedArtwork;

    /// <summary>This raid's own results artwork for the outcome, or null to use the default.</summary>
    public Sprite ResultArtwork(bool extracted) => extracted ? extractedArtwork : failedArtwork;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public int Tier => tier;
    public string SceneName => sceneName;
    public bool IsLocked => locked;
    public string LockedText => lockedText;
    public MonsterDefinition[] KnownEnemies => knownEnemies;
    public ItemDefinition[] LikelyItems => likelyItems;
    public ResourceDefinition[] LikelyResources => likelyResources;
    /// <summary>Danger stages, the earliest (most time left) first.</summary>
    public IReadOnlyList<RaidStage> Stages => stages;
    public IReadOnlyList<RaidTimeOfDay> TimesOfDay => timesOfDay;

    public float EnemyDensity => enemyDensity;
    /// <summary>This raid's enemy density times the global one (RaidGlobalSettings).</summary>
    public float EffectiveEnemyDensity => enemyDensity * RaidGlobalSettings.Current.GlobalEnemyDensity;
    public bool OverridesBands => overrideBands;
    public IReadOnlyList<RaidSpawnList> SpawnLists => spawnLists;
    public IReadOnlyList<RaidSystem> Systems => systems;
#if UNITY_EDITOR
    /// <summary>The editable lists, for the raid editors (record Undo before changing them).</summary>
    public List<RaidSpawnList> EditorSpawnLists => spawnLists;
    public List<RaidSystem> EditorSystems => systems;
#endif
    /// <summary>Old roaming settings from before raid systems; only the migration reads it.</summary>
    public RoamingSpawnSettings LegacyRoaming => roaming;

    /// <summary>The raid's first system of the type, or null when it doesn't run one.</summary>
    public T GetSystem<T>() where T : RaidSystem
    {
        foreach (RaidSystem system in systems)
        {
            if (system is T match)
            {
                return match;
            }
        }

        return null;
    }

    /// <summary>The rules spawn lists and groups fill by in this raid, measured from the given entry (null ignores bands).</summary>
    public SpawnRules GetSpawnRules(Vector3? entry)
    {
        RaidGlobalSettings global = RaidGlobalSettings.Current;
        return overrideBands ? new SpawnRules(EffectiveEnemyDensity, entry, nearDistance, farDistance)
            : new SpawnRules(EffectiveEnemyDensity, entry, global.NearDistance, global.FarDistance);
    }

    private void OnValidate()
    {
        RaidStages.Sort(stages);
        farDistance = Mathf.Max(nearDistance, farDistance);
    }

    /// <summary>The value a stat starts from before modifiers.</summary>
    public float GetBase(RaidStat stat)
    {
        switch (stat)
        {
            case RaidStat.Duration:
                return durationSeconds;
            case RaidStat.ExtractionTime:
                return extractionSeconds;
            case RaidStat.LootQuantity:
                return lootMultiplier;
            case RaidStat.EnemyHealth:
                return enemyHealthMultiplier;
            case RaidStat.EnemyPower:
                return enemyPowerMultiplier;
            default:
                return 0f;
        }
    }
}
