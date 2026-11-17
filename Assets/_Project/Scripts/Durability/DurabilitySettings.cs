using UnityEngine;

/// <summary>
/// Death and repair rules for deployed equipment (GDD §8.3, §13.4, design decisions of the first-90-minutes pass):
/// tools, weapons and backpacks the player carried through the portal are kept when a raid fails, but lose durability.
/// Gear at 0% is broken: kept, but useless until repaired. Use wears gear in one way only: a glancing blow (a missed
/// timing press) in the gathering minigame chips the axe or pickaxe.
/// Repairing at the repair station costs a share of the item's recipe, scaled by how damaged it is. Read by
/// PlayerSnapshot.AfterFailedRaid, PlayerGathering and Repairs.
/// </summary>
[CreateAssetMenu(fileName = "DurabilitySettings", menuName = "Safe Haven/Settings/Durability Settings")]
public sealed class DurabilitySettings : GameSettings<DurabilitySettings>
{
    [Header("Failed raids")]
    [Tooltip("Durability each piece of deployed equipment loses when a raid fails (0.25 = 25%).")]
    [SerializeField, Range(0f, 1f)] private float lossOnFailedRaid = 0.25f;

    [Header("Use")]
    [Tooltip("Durability a gathering tool loses on each glancing blow in the timing minigame (0.02 = 2%).")]
    [SerializeField, Range(0f, 0.25f)] private float wearPerGlancingBlow = 0.02f;

    [Header("Repairs")]
    [Tooltip("Where gear is repaired.")]
    [SerializeField] private CraftingStationType repairStation = CraftingStationType.Workbench;
    [Tooltip("Share of the item's recipe a full repair (from broken) costs. 0.5 = half the recipe.")]
    [SerializeField, Min(0f)] private float fullRepairCostShare = 0.5f;
    [Tooltip("Every material in a repair costs at least this many.")]
    [SerializeField, Min(0)] private int minimumCostPerMaterial = 1;
    [Tooltip("Breaking gear down at the Workshop (Salvage): the share of its recipe's materials it gives back in full condition, " +
             "less as it wears. Junk finds give what their item's Breaks Into says instead.")]
    [SerializeField, Range(0f, 1f)] private float salvageShare = 0.5f;

    public float LossOnFailedRaid => lossOnFailedRaid;
    public float WearPerGlancingBlow => wearPerGlancingBlow;
    public CraftingStationType RepairStation => repairStation;
    public float FullRepairCostShare => fullRepairCostShare;
    public int MinimumCostPerMaterial => minimumCostPerMaterial;
    public float SalvageShare => salvageShare;

    /// <summary>How many failed raids a new item survives before it breaks, for the Systems Editor.</summary>
    public int FailedRaidsUntilBroken => lossOnFailedRaid > 0f ? Mathf.CeilToInt(1f / lossOnFailedRaid - 0.0001f) : int.MaxValue;
}
