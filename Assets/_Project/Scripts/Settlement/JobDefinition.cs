using System.Collections.Generic;
using UnityEngine;

/// <summary>Something a worker keeps making from the stash, on their own, until there is plenty.</summary>
[System.Serializable]
public sealed class WorkOrder
{
    public RecipeDefinition recipe;
    [Tooltip("They stop once the stash holds this many of what it makes.")]
    [Min(1)] public int stockTarget = 30;
    [Tooltip("They leave this much of each thing it costs in the stash, so the player can still build.")]
    [Min(0)] public int keepInput = 10;
}

/// <summary>
/// A job a resident can hold in Safe Haven (GDD §15.3), such as Alchemist. Giving the job teaches the player what the
/// resident knows (buildings and recipes), and systems that need a worker look for a resident with their job: the
/// Purification Altar runs while someone is the Alchemist (see SettlementSettings.PurifierJob).
/// <para>
/// Extending: a new job is a new asset. A job with a new effect gets that effect from the system that needs it (it asks
/// SettlementState.HasWorker), so this class stays plain data.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "NewJob", menuName = "Safe Haven/Settlement/Job")]
public sealed class JobDefinition : ScriptableObject
{
    public EmoteDefinition WorkGesture => workGesture;
    public ProficiencyDefinition Teaches => teaches;
    public ItemDefinition WorkTool => workTool;
    public float WorkToolScale => workToolScale;
    public SoundDefinition WorkSound => workSound;
    public float WorkSoundDelay => workSoundDelay;
    public Vector2 WorkSoundEvery => workSoundEvery;

    [Tooltip("Stable unique identifier, used for saves.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [Tooltip("What the job does, shown when assigning it.")]
    [SerializeField, TextArea(2, 4)] private string description;
    [Tooltip("Said by the resident when given the job.")]
    [SerializeField, TextArea] private string acceptLine;
    [Tooltip("Buildings the player learns when someone takes the job.")]
    [SerializeField] private List<BuildingDefinition> teachesBuildings = new List<BuildingDefinition>();
    [Tooltip("Recipes the player learns when someone takes the job.")]
    [SerializeField] private List<RecipeDefinition> teachesRecipes = new List<RecipeDefinition>();
    [Tooltip("Where someone with this job works: they stand by a placed building of this kind when there is one.")]
    [SerializeField] private BuildingDefinition workplace;
    [Tooltip("What they do with their hands there: forging, reading. Empty: they just stand by it.")]
    [SerializeField] private EmoteDefinition workGesture;
    [Tooltip("What they hold while they work. Empty: bare hands.")]
    [SerializeField] private ItemDefinition workTool;
    [Tooltip("Times the size the tool has as an item: a warhammer is rather less at the anvil.")]
    [SerializeField, Min(0.05f)] private float workToolScale = 1f;
    [Tooltip("What their work sounds like. Empty: nothing.")]
    [SerializeField] private SoundDefinition workSound;
    [Tooltip("Seconds into each work gesture that the sound lands: when the hammer comes down.")]
    [SerializeField, Min(0f)] private float workSoundDelay = 0.4f;
    [Tooltip("With a gesture that is held rather than repeated, how often the sound comes, in seconds.")]
    [SerializeField] private Vector2 workSoundEvery = new Vector2(1.5f, 4f);
    [Tooltip("What they keep making on their own at their workplace, first that can be made first, while the player is away too.")]
    [SerializeField] private List<WorkOrder> workOrders = new List<WorkOrder>();
    [Tooltip("The skill they can give the player a lesson in, once between raids. Empty for none.")]
    [SerializeField] private ProficiencyDefinition teaches;
    [Tooltip("Raised when someone takes the job, for quests such as \"Find work for the traveler\".")]
    [SerializeField, RaisedByGame] private GameEventDefinition assignedEvent;

    [Header("What they do for the haven")]
    [Tooltip("More rumours in the raid briefing: what is out there this run.")]
    [SerializeField, Min(0)] private int extraRumours;
    [Tooltip("Share off the cost of repairing gear.")]
    [SerializeField, Range(0f, 1f)] private float gearRepairDiscount;
    [Tooltip("Share off the cost of mending buildings after a breach.")]
    [SerializeField, Range(0f, 1f)] private float buildingRepairDiscount;
    [Tooltip("They stand guard in a ward breach, fighting like this instead of running for the portal. Empty: they shelter with the rest.")]
    [SerializeField] private CompanionDefinition defendsAs;
    [Tooltip("Share more health for the ward during a breach.")]
    [SerializeField, Min(0f)] private float wardHealth;
    [Tooltip("Seconds added to the clock of every raid.")]
    [SerializeField, Min(0f)] private float raidSeconds;
    [Tooltip("Carves enchantments at the altar past the player's own Runecraft, by this many levels at each worker rank (Novice, " +
             "Apprentice, Journeyman, Expert, Master): Liv the Rune Scholar (Docs/EnchantingResearch.md #6). Empty: doesn't carve.")]
    [SerializeField] private int[] carvesPast = new int[0];

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public string AcceptLine => acceptLine;
    public IReadOnlyList<BuildingDefinition> TeachesBuildings => teachesBuildings;
    public IReadOnlyList<RecipeDefinition> TeachesRecipes => teachesRecipes;
    public BuildingDefinition Workplace => workplace;
    public IReadOnlyList<WorkOrder> WorkOrders => workOrders;
    public GameEventDefinition AssignedEvent => assignedEvent;
    public int ExtraRumours => extraRumours;
    public float GearRepairDiscount => gearRepairDiscount;
    public float BuildingRepairDiscount => buildingRepairDiscount;
    public float WardHealth => wardHealth;
    public CompanionDefinition DefendsAs => defendsAs;
    public float RaidSeconds => raidSeconds;
    /// <summary>True for a job that carves enchantments at the altar (Liv's).</summary>
    public bool Carves => carvesPast != null && carvesPast.Length > 0;

    /// <summary>Runecraft levels past the player's own this job carves at worker rank <paramref name="rankIndex"/> (0 = Novice).</summary>
    public int CarvesPastAt(int rankIndex) => !Carves ? 0 : carvesPast[Mathf.Clamp(rankIndex, 0, carvesPast.Length - 1)];

    /// <summary>What the job does for the haven, in a line for the player. Empty when it does none of these.</summary>
    public string DescribeBonus()
    {
        var parts = new List<string>();
        if (extraRumours > 0) parts.Add($"{extraRumours} more rumour{(extraRumours == 1 ? "" : "s")} before each raid");
        if (gearRepairDiscount > 0f) parts.Add($"gear repairs cost {Mathf.RoundToInt(gearRepairDiscount * 100f)}% less");
        if (buildingRepairDiscount > 0f) parts.Add($"mending buildings costs {Mathf.RoundToInt(buildingRepairDiscount * 100f)}% less");
        if (defendsAs != null) parts.Add("stands guard in a breach");
        if (wardHealth > 0f) parts.Add($"the ward holds {Mathf.RoundToInt(wardHealth * 100f)}% longer in a breach");
        if (raidSeconds > 0f) parts.Add($"{raidSeconds:0} more seconds in every raid");
        return string.Join(", ", parts);
    }
}
