using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("Raised when someone takes the job, for quests such as \"Find work for the traveler\".")]
    [SerializeField, RaisedByGame] private GameEventDefinition assignedEvent;

    [Header("What they do for the haven")]
    [Tooltip("More rumours in the raid briefing: what is out there this run.")]
    [SerializeField, Min(0)] private int extraRumours;
    [Tooltip("Share off the cost of repairing gear.")]
    [SerializeField, Range(0f, 1f)] private float gearRepairDiscount;
    [Tooltip("Share off the cost of mending buildings after a breach.")]
    [SerializeField, Range(0f, 1f)] private float buildingRepairDiscount;
    [Tooltip("Share more health for the ward during a breach.")]
    [SerializeField, Min(0f)] private float wardHealth;
    [Tooltip("Seconds added to the clock of every raid.")]
    [SerializeField, Min(0f)] private float raidSeconds;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public string AcceptLine => acceptLine;
    public IReadOnlyList<BuildingDefinition> TeachesBuildings => teachesBuildings;
    public IReadOnlyList<RecipeDefinition> TeachesRecipes => teachesRecipes;
    public BuildingDefinition Workplace => workplace;
    public GameEventDefinition AssignedEvent => assignedEvent;
    public int ExtraRumours => extraRumours;
    public float GearRepairDiscount => gearRepairDiscount;
    public float BuildingRepairDiscount => buildingRepairDiscount;
    public float WardHealth => wardHealth;
    public float RaidSeconds => raidSeconds;

    /// <summary>What the job does for the haven, in a line for the player. Empty when it does none of these.</summary>
    public string DescribeBonus()
    {
        var parts = new List<string>();
        if (extraRumours > 0) parts.Add($"{extraRumours} more rumour{(extraRumours == 1 ? "" : "s")} before each raid");
        if (gearRepairDiscount > 0f) parts.Add($"gear repairs cost {Mathf.RoundToInt(gearRepairDiscount * 100f)}% less");
        if (buildingRepairDiscount > 0f) parts.Add($"mending buildings costs {Mathf.RoundToInt(buildingRepairDiscount * 100f)}% less");
        if (wardHealth > 0f) parts.Add($"the ward holds {Mathf.RoundToInt(wardHealth * 100f)}% longer in a breach");
        if (raidSeconds > 0f) parts.Add($"{raidSeconds:0} more seconds in every raid");
        return string.Join(", ", parts);
    }
}
