using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One upgrade of a building (level 2, 3, ...): what it costs, how long it is hammered, and what it brings: renown for
/// Safe Haven, a faster and bigger crafting station, more storage. Each level also adds visible dressing to the
/// building (Construction).
/// </summary>
[Serializable]
public sealed class BuildingLevel
{
    [Tooltip("Shown before the building's name, e.g. \"Sturdy\" Sawbench.")]
    public string title = "Sturdy";
    [TextArea(1, 3)] public string description;
    public List<ItemAmount> cost = new List<ItemAmount>();
    [Tooltip("Essence the level also takes: Purified Essence for the great tiers (GDD §13.5). Most levels need none.")]
    public List<ResourceAmount> essence = new List<ResourceAmount>();
    [Tooltip("Seconds of hammering at the scaffold.")]
    [Min(0f)] public float work = 6f;
    [Tooltip("Renown added to Safe Haven on top of the building's own.")]
    [Min(0)] public int renown = 4;
    [Tooltip("Share faster a crafting station works. 0.25 means 25% faster. Levels add up.")]
    [Min(0f)] public float craftSpeed;
    [Min(0)] public int extraQueue;
    [Min(0)] public int extraOutput;
    [Tooltip("Extra stash slots for storage (chests, sheds).")]
    [Min(0)] public int extraStorage;

    [Header("What it does for raids")]
    [Tooltip("Seconds added to the clock of every raid. The best building of a kind counts, not each one.")]
    [Min(0f)] public float raidSeconds;
    [Tooltip("More rumours in the raid briefing. The best building of a kind counts.")]
    [Min(0)] public int extraRumours;
    [Tooltip("How many of a raid's coming events the briefing names beforehand. The best building of a kind counts.")]
    [Min(0)] public int foretoldEvents;
    [Tooltip("More comfort: a longer Well Rested before a raid. The best building of a kind counts.")]
    [Min(0)] public int comfort;
    [Tooltip("Extra logs from each section of a tree cut at home.")]
    [Min(0)] public int extraLogs;
    [Tooltip("Recipes the player learns when this level is finished: Watch Post levels teach the raid runes.")]
    public List<RecipeDefinition> teachesRecipes = new List<RecipeDefinition>();

    [Header("What it does for the haven")]
    [Tooltip("What must stand before this level can be ordered, besides the level below it.")]
    public List<BuildingRequirement> requires = new List<BuildingRequirement>();
    [Tooltip("Ward breaches held off first: a won defence teaches the defences their next step (the design review's 8.7, " +
             "2026-11-14). 0 = none.")]
    [Min(0)] public int breachesWon;
    [Tooltip("More beds. A resident with a bed works faster and adds comfort; nobody needs one to live or work here.")]
    [Min(0)] public int beds;
    [Tooltip("Share more Purified Essence from each batch at the altar. 0.25 means 25% more.")]
    [Min(0f)] public float purifyYield;
    [Tooltip("More meals the player can have in them at once.")]
    [Min(0)] public int extraMealSlots;
    [Tooltip("More game roaming outside the light at home.")]
    [Min(0)] public int extraGame;
    [Tooltip("Metres more light around it, pushing the night back.")]
    [Min(0f)] public float extraLight;

    /// <summary>"12 Oak Plank, 8 Purified Essence".</summary>
    public string CostText
    {
        get
        {
            string items = AmountText.Describe(cost);
            string magic = AmountText.Describe(essence);
            return items.Length > 0 && magic.Length > 0 ? items + ", " + magic : items + magic;
        }
    }
}

/// <summary>A building that must stand, at a tier at least this high, before another can be built.</summary>
[Serializable]
public struct BuildingRequirement
{
    public BuildingDefinition building;
    [Range(1, Tiers.Max)] public int tier;

    public BuildingRequirement(BuildingDefinition building, int tier)
    {
        this.building = building;
        this.tier = tier;
    }
}

/// <summary>
/// Authored data for a kind of building: identity, how it looks in menus, its combat stats and what it costs to build.
/// Buildings with Show In Build Menu are placed by the player through the build menu (GDD §14.1); the cost is paid when
/// placed. Buildings that aren't known from the start are learned from blueprints or residents (PlayerCrafting keeps
/// what is known). Each placed building reads this; its current health lives on its own Damageable. Placement rules (a
/// yard kept clear in front of the entrance, building walls in lines) are read by BuildPlacer.
/// </summary>
[CreateAssetMenu(fileName = "NewBuilding", menuName = "Safe Haven/Buildings/Building Definition")]
public sealed class BuildingDefinition : ScriptableObject, ICombatSounds
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for lookups and saves.")]
    [SerializeField] private string id;
    [Tooltip("Shown in the UI. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [Tooltip("Picture for build menus.")]
    [SerializeField] private Sprite icon;

    [Header("World")]
    [Tooltip("Player buildings are attacked by Enemy monsters; Enemy buildings by the player.")]
    [SerializeField] private Faction faction = Faction.Player;
    [Tooltip("Health, armor and resistances.")]
    [SerializeField] private CombatStatsDefinition combatStats;
    [Tooltip("Generated building prefab placed in the world.")]
    [SerializeField] private GameObject prefab;

    [Header("Construction")]
    [Tooltip("Resources it costs to build. At home they come from what the player carries first, then from storage.")]
    [SerializeField] private List<ResourceAmount> constructionCost = new List<ResourceAmount>();
    [Tooltip("Items it costs to build. Usually empty: materials are resources.")]
    [SerializeField] private List<ItemAmount> constructionItems = new List<ItemAmount>();
    [Tooltip("Listed in the build menu so the player can place it.")]
    [SerializeField] private bool showInBuildMenu = true;
    [Tooltip("Known from the start. Otherwise a blueprint or a resident has to teach it.")]
    [SerializeField] private bool knownFromStart = true;
    [Tooltip("Buildings with lower values are listed first in the build menu.")]
    [SerializeField] private int sortOrder;
    [Tooltip("What must stand first. Each building opens the next, so the base grows one new thing at a time.")]
    [SerializeField] private List<BuildingRequirement> requires = new List<BuildingRequirement>();
    [Tooltip("The Haven rank needed before it can be built (HavenSettings: 0 is the first). What only a known settlement can draw.")]
    [SerializeField, Min(0)] private int requiredRank;

    [Header("Placement")]
    [Tooltip("The size its model is shown at (1 = as modelled, life-size). The Haven's big buildings are half size (the " +
             "user, 2026-11-14: too big and hard to read top-down); a stable or a yard that horses and carts use is less " +
             "small. Applied to the prefab by AgentScripts/BuildBuildingScale.cs: its collider, navigation, smoke, " +
             "lights and points all follow. People's ground (the yard) keeps its size.")]
    [SerializeField, Range(0.25f, 1.5f)] private float modelScale = 1f;
    [Tooltip("Its height on top of that, as a share (1 = in proportion). The halved buildings stand a little taller than " +
             "half (the user, 2026-11-14: \"they are a bit low now\"), for steeper roofs and doors that fit the people.")]
    [SerializeField, Range(0.5f, 2f)] private float modelHeight = 1f;
    [Tooltip("Meters of open ground kept clear on the entrance side so the building can be reached and used. Nothing may be built there. 0 = no yard.")]
    [SerializeField, Min(0f)] private float yardDepth;
    [Tooltip("Which side of the model is the entrance, in the model's own axes (Front is its local +Z).")]
    [SerializeField] private YardSide yardSide = YardSide.Front;
    [Tooltip("Built in lines by dragging, joining the ends of others like it: walls, fences, palisades.")]
    [SerializeField] private bool placeInLines;
    [Tooltip("Shown under the build menu's Decorations: benches, woodpiles, flower beds, posts. Things that make the Haven " +
             "a home (comfort and renown) and do no work.")]
    [SerializeField] private bool decoration;

    [Header("Crafting")]
    [Tooltip("Makes the building a crafting station: using it opens its queue of recipes for this station.")]
    [SerializeField] private CraftingStationType station;
    [Tooltip("How many finished things the station's output slot holds before it stops and waits to be emptied.")]
    [SerializeField, Min(1)] private int outputCapacity = CraftingStation.DefaultOutputCapacity;
    [Tooltip("How many crafts can wait in the station's queue, including the one being made.")]
    [SerializeField, Min(1)] private int queueLength = CraftingStation.DefaultQueueLength;

    [Header("Building Up")]
    [Tooltip("Seconds of hammering to put it up (hold E at its scaffold). 0: it stands at once (campfires, torches, walls).")]
    [SerializeField, Min(0f)] private float buildWork;
    [Tooltip("Renown it brings Safe Haven once built (HavenRenown). Each more of the same kind brings half as much.")]
    [SerializeField, Min(0)] private int renown = 5;
    [Tooltip("Upgrades, in order: level 2, level 3, ...")]
    [SerializeField] private List<BuildingLevel> upgrades = new List<BuildingLevel>();
    [Tooltip("Comfort it gives once built: a longer Well Rested before a raid. Each kind of building counts once.")]
    [SerializeField, Min(0)] private int comfort;
    [Tooltip("Beds it gives. A resident with a bed works faster and adds comfort; nobody needs one to live or work here.")]
    [SerializeField, Min(0)] private int beds;
    [Tooltip("Recipes the player learns when it is first built.")]
    [SerializeField] private List<RecipeDefinition> teachesRecipes = new List<RecipeDefinition>();

    [Header("Sound")]
    [Tooltip("Plays when it is destroyed. Empty uses the structure default from Audio Settings.")]
    [SerializeField] private SoundDefinition deathSound;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;
    public Faction Faction => faction;
    public CombatStatsDefinition CombatStats => combatStats;
    public IReadOnlyList<ResourceAmount> ConstructionCost => constructionCost;
    public IReadOnlyList<ItemAmount> ConstructionItems => constructionItems;
    public bool ShowInBuildMenu => showInBuildMenu;
    public bool KnownFromStart => knownFromStart;
    public int SortOrder => sortOrder;
    public IReadOnlyList<BuildingRequirement> Requires => requires;
    public int Comfort => comfort;
    public int Beds => beds;
    public int RequiredRank => requiredRank;
    public IReadOnlyList<RecipeDefinition> TeachesRecipes => teachesRecipes;
    public float YardDepth => yardDepth;
    public float ModelScale => modelScale;
    public float ModelHeight => modelHeight;
    /// <summary>The scale its model is shown at, per axis: ModelScale across, ModelScale × ModelHeight up.</summary>
    public Vector3 ModelSize => new Vector3(modelScale, modelScale * modelHeight, modelScale);
    public YardSide YardSide => yardSide;
    public bool PlaceInLines => placeInLines;
    public bool Decoration => decoration;
    public CraftingStationType Station => station;
    public int OutputCapacity => Mathf.Max(1, outputCapacity);
    public int QueueLength => Mathf.Max(1, queueLength);
    public SoundDefinition DeathSound => deathSound;
    SoundDefinition ICombatSounds.HurtSound => null;
    public float BuildWork => buildWork;
    public int Renown => renown;
    public IReadOnlyList<BuildingLevel> Upgrades => upgrades;
    /// <summary>The highest level: 1 plus the upgrades.</summary>
    public int MaxLevel => 1 + upgrades.Count;

    /// <summary>The upgrade that reaches <paramref name="level"/> (2 or more), or null.</summary>
    public BuildingLevel UpgradeTo(int level) => level >= 2 && level - 2 < upgrades.Count ? upgrades[level - 2] : null;

    /// <summary>Sets up how it is built and upgraded (editor tools and tests).</summary>
    public void ConfigureBuildingUp(float work, int renownValue, IEnumerable<BuildingLevel> levels)
    {
        buildWork = Mathf.Max(0f, work);
        renown = Mathf.Max(0, renownValue);
        upgrades = new List<BuildingLevel>(levels);
    }
}
