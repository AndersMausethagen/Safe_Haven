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

    /// <summary>True for a copy made at runtime by a module's One Fewer (RecipeVariants), not a recipe asset.</summary>
    public bool IsThriftierCopy { get; private set; }

    /// <summary>The id a thriftier copy of <paramref name="plain"/> by <paramref name="module"/> goes by: the plain one's and the module's,
    /// so a queued craft saved with it is found again (ContentDatabase.FindRecipe) and a cancel gives back exactly what it took.</summary>
    public static string ThriftierId(RecipeDefinition plain, BuildingModuleDefinition module) => plain.Id + "+" + module.Id;

    /// <summary>
    /// <paramref name="cost"/> with one fewer of each of <paramref name="fewer"/> it needs more than one of (the Herb Rack, the user
    /// 2026-11-22: "reduce the amount of herbs needed in tonics by 1, as long as the required herb is more than 1"). Pure.
    /// </summary>
    public static List<ItemAmount> Fewer(IReadOnlyList<ItemAmount> cost, IReadOnlyList<ItemDefinition> fewer)
    {
        var less = new List<ItemAmount>(cost.Count);
        foreach (ItemAmount one in cost)
        {
            bool cut = one.Item != null && one.Amount > 1 && Contains(fewer, one.Item);
            less.Add(cut ? new ItemAmount(one.Item, one.Amount - 1) : one);
        }

        return less;
    }

    /// <summary>True when <paramref name="module"/>'s One Fewer makes this recipe cheaper.</summary>
    public bool CheaperWith(BuildingModuleDefinition module)
    {
        if (module == null || module.OneFewer.Count == 0)
        {
            return false;
        }

        foreach (ItemAmount one in itemCost)
        {
            if (one.Item != null && one.Amount > 1 && Contains(module.OneFewer, one.Item))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Makes <paramref name="copy"/> (made with CreateInstance) the thriftier copy of <paramref name="plain"/> at a station with
    /// <paramref name="module"/>: the same in every way but its item cost, its id, the module it needs and the recipe it replaces.</summary>
    internal static void MakeThriftierCopy(RecipeDefinition copy, RecipeDefinition plain, BuildingModuleDefinition module)
    {
        copy.name = plain.name;
        copy.id = ThriftierId(plain, module);
        copy.displayName = plain.displayName;
        copy.output = plain.output;
        copy.outputResource = plain.outputResource;
        copy.outputAmount = plain.outputAmount;
        copy.resourceCost = new List<ResourceAmount>(plain.resourceCost);
        copy.itemCost = Fewer(plain.itemCost, module.OneFewer);
        copy.station = plain.station;
        copy.craftSeconds = plain.craftSeconds;
        copy.knownFromStart = plain.knownFromStart;
        copy.minimumStationTier = plain.minimumStationTier;
        copy.requiredSkill = plain.requiredSkill;
        copy.requiredSkillLevel = plain.requiredSkillLevel;
        copy.sortOrder = plain.sortOrder;
        copy.needsModule = module;
        copy.replaces = plain;
        copy.IsThriftierCopy = true;
    }

    private static bool Contains(IReadOnlyList<ItemDefinition> items, ItemDefinition item)
    {
        foreach (ItemDefinition one in items)
        {
            if (one == item)
            {
                return true;
            }
        }

        return false;
    }
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