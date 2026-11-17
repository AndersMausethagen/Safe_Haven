using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

/// <summary>What a large raid is, on a raid with generated maps: a wider map with as much on every square metre, and more time to cross it.</summary>
[System.Serializable]
public sealed class RaidSizeOption
{
    [Tooltip("How many times wider the map is. 2 is four times the ground, and four times what is on it.")]
    [Min(1f)] public float widthScale = 2f;
    [Tooltip("Seconds added to the clock: the same danger stages and exit windows, with more calm before them.")]
    [Min(0f)] public float extraSeconds = 750f;
    [Tooltip("Exits only a large map has, by name. Each copies one of the raid's own (its hours, what it costs), without its alarm.")]
    public string[] extraExits = new string[0];
}

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
    [Tooltip("What the place is, in the UI's words, lower case: \"the forest stirs\" on the gathering-noise meter, \"The forest\" over the " +
             "briefing's mood. Each raid its own: forest, valley, fen, mountain.")]
    [SerializeField] private string land = "forest";
    [Tooltip("How dangerous and rewarding the raid is, shown to the player.")]
    [SerializeField, Range(1, Tiers.Max)] private int tier = 1;

    [Header("Scene")]
    [Tooltip("Name of the scene loaded for this raid. It must be listed in Build Settings.")]
    [SerializeField] private string sceneName;
    [Tooltip("Shown in the raid planner but can't be entered yet: a destination for later (GDD §21.5 \"a harder expedition\").")]
    [SerializeField] private bool locked;
    [Tooltip("Locked until this boss has been beaten. Empty: it stays locked.")]
    [SerializeField] private BossDefinition unlockedBy;
    [Tooltip("What choosing a large raid does here. A raid without generated maps has no large version.")]
    [SerializeField] private RaidSizeOption largeRaid = new RaidSizeOption();
    [Tooltip("Why a locked destination can't be entered, shown in the planner.")]
    [SerializeField, TextArea] private string lockedText = "The way there is not yet open.";
    [Tooltip("Set: this raid is a boss encounter, one boss in a small hand-built arena beyond a gate of mist, with no way out but " +
             "winning or dying (Docs/BossEncounterResearch_2026-11-17.md). Empty: an expedition.")]
    [SerializeField] private BossEncounterDefinition encounter;

    [Header("Briefing (GDD §7.5)")]
    [Tooltip("Enemies the player is told about before entering.")]
    [SerializeField] private MonsterDefinition[] knownEnemies = new MonsterDefinition[0];
    [Tooltip("Materials and other items the player can expect to find.")]
    [SerializeField] private ItemDefinition[] likelyItems = new ItemDefinition[0];
    [Tooltip("Essences the player can expect to find.")]
    [SerializeField] private ResourceDefinition[] likelyResources = new ResourceDefinition[0];

    [Header("Loot")]
    [Tooltip("What only this raid holds (Data/Loot/Raids): rolled on top of each cache's own table whenever a cache is searched here, " +
             "so the same chest gives different things in different places, as Valheim's biomes and ARC Raiders' maps do.")]
    [SerializeField] private LootTable regionalLoot;
    [Tooltip("What killing an elite here gives on top of the creature's drop (Data/Loot/Raids), rows limited by tier. Empty: each " +
             "tier's own Elite loot list.")]
    [SerializeField] private LootTable eliteLoot;

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

    [Header("Difficulty Tiers")]
    [Tooltip("The tiers this raid can be entered at, lowest first, picked at the portal. Empty: it plays at its one Tier above, with nothing scaled.")]
    [SerializeField] private List<RaidDifficulty> difficulties = new List<RaidDifficulty>();

    [Tooltip("What can make a creature an elite in this raid. Each tier says how many elites and how many affixes each.")]
    [SerializeField] private List<EliteAffixDefinition> eliteAffixes = new List<EliteAffixDefinition>();

    [Header("Time of Day")]
    [Tooltip("The hours this raid can take place at, rolled from each run's seed (shown in the briefing). Empty: the " +
             "scene's own hour. Night is darker for the player and the monsters alike, and should pay more.")]
    [SerializeField] private List<RaidTimeOfDay> timesOfDay = new List<RaidTimeOfDay>();

    [Header("Look")]
    [Tooltip("This raid's colour grade over its scene's volumes: the filmic curve and how much darker and greyer than home.")]
    [SerializeField] private RaidGrade grade = new RaidGrade();
    [Tooltip("This raid's ground light, worked out while a generated map loads: the canopy's and the land's shade, light thrown " +
             "back from sunlit ground. Off keeps the look from before it.")]
    [SerializeField] private RaidGroundLight groundLight = new RaidGroundLight();

    [Header("Map")]
    [Tooltip("The rules this raid's map is generated by each run. Empty: it plays the scene's hand-painted map, turned.")]
    [SerializeField] private RaidLayoutRules layoutRules;

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
    [Tooltip("Optional: the extracted artwork moving, a short loop that ends on its first frame (LocalSpawn video). " +
             "It fades in over the still once it plays.")]
    [SerializeField] private VideoClip extractedMotion;
    [Tooltip("Optional: the failed artwork moving, a loop that ends on its first frame.")]
    [SerializeField] private VideoClip failedMotion;
    [Tooltip("Optional: the sound of the extracted artwork's place, looped under the report (ambience channel).")]
    [SerializeField] private AudioClip extractedSound;
    [Tooltip("Optional: the sound of the failed artwork's place, looped under the report.")]
    [SerializeField] private AudioClip failedSound;

    [Tooltip("A painting of the place, shown in the raid planner when it is chosen (LocalSpawn: Tools/LocalSpawn/raid_art.json).")]
    [SerializeField] private Sprite placeArtwork;

    [Header("First Visit")]
    [Tooltip("Played the first time a character enters this raid, as the loading screen lifts and before the clock starts. " +
             "Once per save; not in co-op. Empty: none.")]
    [SerializeField] private CinematicDefinition firstVisitCinematic;
    [Tooltip("Seconds after that cinematic in which no creature notices the player, so the arrival can be taken in. " +
             "A creature the player strikes still fights back.")]
    [SerializeField, Min(0f)] private float calmAfterCinematic = 8f;
    [Tooltip("Optional: set down around the player as the cinematic fades into the game (fog rolling round them).")]
    [SerializeField] private GameObject arrivalEffect;
    [Tooltip("Optional: heard as the cinematic fades into the game.")]
    [SerializeField] private SoundDefinition arrivalSound;

    /// <summary>The cinematic of a character's first visit, or null.</summary>
    public CinematicDefinition FirstVisitCinematic => firstVisitCinematic;
    public float CalmAfterCinematic => calmAfterCinematic;
    public GameObject ArrivalEffect => arrivalEffect;
    public SoundDefinition ArrivalSound => arrivalSound;

    /// <summary>This raid's own results artwork for the outcome, or null to use the default.</summary>
    public Sprite ResultArtwork(bool extracted) => extracted ? extractedArtwork : failedArtwork;

    /// <summary>The results artwork moving (a loop), or null.</summary>
    public VideoClip ResultMotion(bool extracted) => extracted ? extractedMotion : failedMotion;

    /// <summary>The sound of the results artwork's place (a loop), or null.</summary>
    public AudioClip ResultSound(bool extracted) => extracted ? extractedSound : failedSound;

    /// <summary>A painting of the place for the raid planner, or null.</summary>
    public Sprite PlaceArtwork => placeArtwork;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Land => string.IsNullOrWhiteSpace(land) ? "forest" : land;
    public string Description => description;
    public int Tier => tier;
    public string SceneName => sceneName;
    public bool IsLocked => (locked && (unlockedBy == null || !GameSession.Here.IsBossDefeated(unlockedBy.Id)))
                            || (encounter != null && !encounter.IsOpenBy(GameSession.Quests));
    /// <summary>The boss encounter this raid is, or null for an expedition.</summary>
    public BossEncounterDefinition Encounter => encounter;
    public bool IsEncounter => encounter != null;
    public string LockedText => lockedText;
    public RaidSizeOption LargeRaid => largeRaid;
    public bool CanBeLarge => layoutRules != null && largeRaid != null && largeRaid.widthScale > 1f;
    public MonsterDefinition[] KnownEnemies => knownEnemies;
    public ItemDefinition[] LikelyItems => likelyItems;
    public LootTable RegionalLoot => regionalLoot;
    public LootTable EliteLoot => eliteLoot;
    public ResourceDefinition[] LikelyResources => likelyResources;
    /// <summary>Danger stages, the earliest (most time left) first.</summary>
    public IReadOnlyList<RaidStage> Stages => stages;
    public IReadOnlyList<RaidTimeOfDay> TimesOfDay => timesOfDay;
    public RaidLayoutRules LayoutRules => layoutRules;
    public RaidGrade Grade => grade;
    public RaidGroundLight GroundLight => groundLight;
    public IReadOnlyList<RaidDifficulty> Difficulties => difficulties;
    public IReadOnlyList<EliteAffixDefinition> EliteAffixes => eliteAffixes;

    /// <summary>The lowest difficulty, what a raid is entered at unless the player picks another; null for a raid without tiers.</summary>
    public RaidDifficulty LowestDifficulty => difficulties.Count > 0 ? difficulties[0] : null;

    /// <summary>
    /// Whether the briefing should name a creature at a tier: false when the spawn lists hold it, but not at this tier. Creatures no list
    /// holds (placed in the scene, or by an event) can be met at any tier.
    /// </summary>
    public bool CanMeet(MonsterDefinition monster, int tier)
    {
        bool listed = false;
        foreach (RaidSpawnList list in spawnLists)
        {
            foreach (SpawnTable.Entry entry in list.entries)
            {
                if (entry.prefab == null)
                {
                    continue;
                }

                foreach (MonsterAI creature in entry.prefab.GetComponentsInChildren<MonsterAI>(true))
                {
                    if (creature.Definition == monster)
                    {
                        listed = true;
                        if (entry.AppearsAt(tier))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return !listed;
    }

    /// <summary>The highest tier the player's gear is ready for, so the portal starts on a sensible choice; the lowest if none is.</summary>
    public RaidDifficulty DifficultyFor(int gearScore)
    {
        RaidDifficulty best = LowestDifficulty;
        foreach (RaidDifficulty difficulty in difficulties)
        {
            if (gearScore >= difficulty.recommendedGearScore && RaidLauncher.CheckWard(difficulty) == null)
            {
                best = difficulty;
            }
        }

        return best;
    }

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

    /// <summary>How many creatures this run places and keeps roaming: the raid's and the global density, times the hour's and the
    /// tier's (both say they multiply how many the raid places or spawns). Camps (GetSpawnRules) and roamers (RoamingSpawner) share it.</summary>
    public float SessionEnemyDensity =>
        EffectiveEnemyDensity * (GameSession.ActiveRaid?.TimeOfDay?.enemyDensity ?? 1f) * (GameSession.ActiveRaid?.Difficulty?.enemyDensity ?? 1f);

    /// <summary>The rules spawn lists and groups fill by in this raid, measured from the given entry (null ignores bands).</summary>
    public SpawnRules GetSpawnRules(Vector3? entry)
    {
        RaidGlobalSettings global = RaidGlobalSettings.Current;
        float density = SessionEnemyDensity;
        float area = GameSession.ActiveRaid != null ? GameSession.ActiveRaid.AreaScale : 1f;
        return overrideBands ? new SpawnRules(density, entry, nearDistance, farDistance, area)
            : new SpawnRules(density, entry, global.NearDistance, global.FarDistance, area);
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
