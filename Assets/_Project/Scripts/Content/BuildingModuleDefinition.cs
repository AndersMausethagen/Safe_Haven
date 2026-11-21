using System.Collections.Generic;
using UnityEngine;

/// <summary>Where a module may be fitted, besides the buildings it lists.</summary>
public enum ModuleFits
{
    /// <summary>Only the buildings in its list.</summary>
    Listed = 0,
    /// <summary>Any crafting station (and its list).</summary>
    AnyStation = 1,
    /// <summary>Any building the player builds (and its list).</summary>
    AnyBuilding = 2,
}

/// <summary>
/// A modular improvement fitted to a building from its Upgrade page (the user, 2026-11-21: "adding a storage unit will increase the number
/// of output slots by +1"; Docs/BuildingUpgradesResearch_2026-11-21.md). Authored data: what it fits, the building tier it needs, what it
/// costs (rare raid parts included), and what it adds (the same BuildingBonus a tier adds). A building has one slot per tier, at most five,
/// shared with the extensions placed beside a station (StationExtensions); each module once per building, one of a Group. Fitting and
/// removing are BuildingModules'; what is fitted is the building's (Construction). Read-only while playing.
/// </summary>
[CreateAssetMenu(fileName = "NewModule", menuName = "Safe Haven/Buildings/Building Module")]
public sealed class BuildingModuleDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for saves.")]
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private Sprite icon;

    [Header("Where it fits")]
    [SerializeField] private ModuleFits fits = ModuleFits.Listed;
    [Tooltip("The buildings it fits besides what Fits says.")]
    [SerializeField] private List<BuildingDefinition> buildings = new List<BuildingDefinition>();
    [Tooltip("The building's tier it needs.")]
    [SerializeField, Range(1, 5)] private int tier = 1;
    [Tooltip("Only one module of a group per building (\"fire\": the Bellows or the Bloomery Hearth). Empty: no group.")]
    [SerializeField] private string group;

    [Header("What it does")]
    [Tooltip("Counts only while someone works at the building (a resident's job).")]
    [SerializeField] private bool staffed;
    [SerializeField] private BuildingBonus bonus = new BuildingBonus();

    [Header("What it costs")]
    [SerializeField] private List<ItemAmount> cost = new List<ItemAmount>();
    [SerializeField] private List<ResourceAmount> essence = new List<ResourceAmount>();
    [Tooltip("Seconds of hammering at the building's scaffold to fit it (a spare too).")]
    [SerializeField, Min(0.5f)] private float work = 5f;
    [Tooltip("Known from the start. Otherwise a quest teaches it as it starts (QuestDefinition › Unlocks Modules).")]
    [SerializeField] private bool knownFromStart;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int Tier => Mathf.Max(1, tier);
    public string Group => group;
    public bool Staffed => staffed;
    public BuildingBonus Bonus => bonus;
    public IReadOnlyList<ItemAmount> Cost => cost;
    public IReadOnlyList<ResourceAmount> Essence => essence;
    public float Work => Mathf.Max(0.5f, work);
    public bool KnownFromStart => knownFromStart;

    /// <summary>True when it may be fitted to this kind of building (tier aside).</summary>
    public bool Fits(BuildingDefinition building)
    {
        if (building == null)
        {
            return false;
        }

        return buildings.Contains(building)
            || (fits == ModuleFits.AnyStation && building.Station != CraftingStationType.None && building.ShowInBuildMenu)
            || (fits == ModuleFits.AnyBuilding && building.ShowInBuildMenu);
    }

    /// <summary>What it adds, in words: "+1 output slot, +5 room".</summary>
    public string Effect()
    {
        var parts = new List<string>();
        if (bonus.extraOutputSlots > 0) parts.Add($"+{bonus.extraOutputSlots} output slot{(bonus.extraOutputSlots == 1 ? "" : "s")}");
        if (bonus.extraOutput > 0) parts.Add($"+{bonus.extraOutput} room");
        if (bonus.extraQueue > 0) parts.Add($"+{bonus.extraQueue} queue");
        if (bonus.craftSpeed > 0f) parts.Add($"+{bonus.craftSpeed * 100f:0}% speed");
        if (bonus.extraChance > 0f) parts.Add($"{bonus.extraChance * 100f:0}% chance of one more");
        if (bonus.extraStorage > 0) parts.Add($"+{bonus.extraStorage} stash slots");
        if (bonus.comfort > 0) parts.Add($"+{bonus.comfort} comfort");
        if (bonus.beds > 0) parts.Add($"+{bonus.beds} bed{(bonus.beds == 1 ? "" : "s")}");
        if (bonus.extraLight > 0f) parts.Add($"+{bonus.extraLight:0} m light");
        if (bonus.purifyYield > 0f) parts.Add($"+{bonus.purifyYield * 100f:0}% Purified Essence");
        if (bonus.renown > 0) parts.Add($"+{bonus.renown} renown");
        if (bonus.extraGame > 0) parts.Add($"+{bonus.extraGame} game at home");
        if (bonus.extraLogs > 0) parts.Add($"+{bonus.extraLogs} log{(bonus.extraLogs == 1 ? "" : "s")} a cut at home");
        if (bonus.foretoldEvents > 0) parts.Add($"+{bonus.foretoldEvents} foretold event{(bonus.foretoldEvents == 1 ? "" : "s")}");
        if (bonus.healthShare > 0f) parts.Add($"+{bonus.healthShare * 100f:0}% health");
        if (bonus.fightShare > 0f) parts.Add($"shots {bonus.fightShare * 100f:0}% harder");
        if (bonus.turnBackShare > 0f) parts.Add($"{bonus.turnBackShare * 100f:0}% of each blow comes back on the striker");
        if (bonus.fineChance > 0f) parts.Add($"gear Fine {bonus.fineChance * 100f:0}% more often");
        // What it opens at the station: a thriftier way to make something, or something new (RecipeDefinition › Needs Module).
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        foreach (RecipeDefinition recipe in content != null ? content.Recipes : (IReadOnlyList<RecipeDefinition>)System.Array.Empty<RecipeDefinition>())
        {
            if (recipe == null || recipe.NeedsModule != this)
            {
                continue;
            }

            parts.Add(recipe.Replaces != null
                ? $"{recipe.OutputAmount} {recipe.DisplayName} from {AmountText.Describe(recipe.ItemCost)} (was {AmountText.Describe(recipe.Replaces.ItemCost)})"
                : $"makes {recipe.DisplayName} ×{recipe.OutputAmount} from {AmountText.Describe(recipe.ItemCost)}");
        }

        string text = string.Join(", ", parts);
        return staffed && text.Length > 0 ? "While someone works here: " + text : text;
    }

    /// <summary>The buildings it fits, in words: "the Workshop", "the Hut or the Cottage", "any crafting station".</summary>
    public string FitsText()
    {
        if (fits == ModuleFits.AnyStation)
        {
            return "any crafting station";
        }

        if (fits == ModuleFits.AnyBuilding)
        {
            return "any building";
        }

        var names = new List<string>();
        foreach (BuildingDefinition building in buildings)
        {
            if (building != null)
            {
                names.Add("the " + building.DisplayName);
            }
        }

        return names.Count == 0 ? "nothing" : names.Count <= 3 ? string.Join(" or ", names) : "most buildings";
    }
}
