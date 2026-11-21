using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How to make something: an item for the bag (a tool, a weapon, a meal) or a refined material for the resource bars (an
/// Oak Plank, an Iron Ingot). It says what it costs in resources and items, where it is made, how long a station takes,
/// and whether the player knows it from the start or has to learn it (from a blueprint, a resident or a boss).
/// Higher-tier recipes are meant to use some lower-tier materials too, so early resources stay useful.
/// <para>
/// Only a few basic recipes are By Hand (instant, from the bag window). The rest are queued at their station, which works
/// for Craft Seconds per craft and puts the result in its output slot (see StationQueue).
/// </para>
/// <para>
/// Set exactly one of Output (item) and Output Resource. The Content Editor's recipe page flags recipes with neither.
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "NewRecipe", menuName = "Safe Haven/Crafting/Recipe")]
public sealed class RecipeDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for saves and unlocks.")]
    [SerializeField] private string id;
    [Tooltip("Shown in the crafting list. Falls back to the result's name when empty.")]
    [SerializeField] private string displayName;

    [Header("Result")]
    [Tooltip("The item this recipe makes. Leave empty when it makes a resource.")]
    [SerializeField] private ItemDefinition output;
    [Tooltip("The resource this recipe makes, such as Oak Plank. Used when no item is set.")]
    [SerializeField] private ResourceDefinition outputResource;
    [SerializeField, Min(1)] private int outputAmount = 1;

    [Header("Cost")]
    [Tooltip("Resources used up. At home they are taken from what the player carries first, then from storage.")]
    [SerializeField] private List<ResourceAmount> resourceCost = new List<ResourceAmount>();
    [Tooltip("Items used up, taken from the bag (and storage at home).")]
    [SerializeField] private List<ItemAmount> itemCost = new List<ItemAmount>();

    [Header("Requirements")]
    [Tooltip("The crafting station that makes it. By Hand is crafted instantly from the bag window; every other station " +
             "queues it and puts the result in its output slot.")]
    [SerializeField] private CraftingStationType station;
    [Tooltip("Seconds a station works on one craft. Ignored for By Hand recipes, which are instant.")]
    [SerializeField, Min(0.1f)] private float craftSeconds = 5f;
    [Tooltip("Known from the start. Otherwise something has to teach it: a blueprint, a resident or a boss.")]
    [SerializeField] private bool knownFromStart = true;
    [Tooltip("Asks for at least this station tier, for what a tier opens beyond its materials: a better yield, a field kit. 0 goes by the rule alone.")]
    [SerializeField, Range(0, Tiers.Max)] private int minimumStationTier;
    [Tooltip("A craft only a practised hand can make: needs this skill at Required Skill Level as well. Empty for none.")]
    [SerializeField] private ProficiencyDefinition requiredSkill;
    [SerializeField, Range(0, 50)] private int requiredSkillLevel;
    [Tooltip("Recipes with lower values are listed first.")]
    [SerializeField] private int sortOrder;

    [Header("Modules")]
    [Tooltip("A module the station must have fitted to make it (BuildingModules): a new item, or a thriftier way to make one. It is " +
             "shown only once the module is known. Empty: no module needed.")]
    [SerializeField] private BuildingModuleDefinition needsModule;
    [Tooltip("The recipe this one is a thriftier way to make (it needs Needs Module). Where this one can be made, the other is hidden " +
             "and workers make this one instead.")]
    [SerializeField] private RecipeDefinition replaces;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    /// <summary>The module its station must have fitted, or null.</summary>
    public BuildingModuleDefinition NeedsModule => needsModule;
    /// <summary>The recipe this one beats where it can be made (a thrifty variant), or null.</summary>
    public RecipeDefinition Replaces => replaces;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName : OutputName;
    public ItemDefinition Output => output;
    public ProficiencyDefinition RequiredSkill => requiredSkill;
    public int RequiredSkillLevel => requiredSkill != null ? requiredSkillLevel : 0;

    /// <summary>"Needs Armorcrafting 30." while the player's skill is short of it; null when it isn't, or the recipe asks none.</summary>
    public string SkillProblem(CharacterProgress progress)
    {
        if (requiredSkill == null || requiredSkillLevel <= 0 || progress == null || Cheats.NoRequirements)
        {
            return null;
        }

        return progress.LevelOf(requiredSkill, LevelingSettings.Current) >= requiredSkillLevel
            ? null
            : $"Needs {requiredSkill.DisplayName} {requiredSkillLevel}.";
    }
    /// <summary>The resource made, when the recipe makes a resource rather than an item.</summary>
    public ResourceDefinition OutputResource => output == null ? outputResource : null;
    public int OutputAmount => Mathf.Max(1, outputAmount);
    public bool HasOutput => output != null || outputResource != null;
    /// <summary>The result's name, whichever kind it is.</summary>
    public string OutputName => OutputDefinition is IStackDefinition made ? made.DisplayName : name;
    public Sprite OutputIcon => OutputDefinition is IStackDefinition made ? made.Icon : null;
    /// <summary>The definition made, item or resource, for events and lookups.</summary>
    public Object OutputDefinition => output != null ? output : outputResource;
    public IReadOnlyList<ResourceAmount> ResourceCost => resourceCost;
    public IReadOnlyList<ItemAmount> ItemCost => itemCost;
    public CraftingStationType Station => station;
    /// <summary>True for the few basic recipes made instantly from the bag; the rest are queued at their station.</summary>
    public bool IsHandcrafted => station == CraftingStationType.None;
    public float CraftSeconds => Mathf.Max(0.1f, craftSeconds);
    public bool KnownFromStart => knownFromStart;
    public int SortOrder => sortOrder;
    public int Tier => output != null ? output.Tier : outputResource != null ? outputResource.Tier : 1;

    /// <summary>
    /// The tier its station must have. Gear and tools are made at their own tier. What is used up (materials, food, potions)
    /// comes one tier earlier, so a station can refine what its own next upgrade costs: a Tier I Smithy smelts the copper that
    /// raises it to Tier II, where copper gear is made.
    /// </summary>
    public int RequiredStationTier
    {
        get
        {
            bool isGear = output != null && (output.Category == ItemCategory.Equipment || output.Category == ItemCategory.Tool);
            return Mathf.Clamp(Mathf.Max(isGear ? Tier : Tier - 1, minimumStationTier), 1, Tiers.Max);
        }
    }
}