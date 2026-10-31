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

    [Header("Placement")]
    [Tooltip("Meters of open ground kept clear on the entrance side so the building can be reached and used. Nothing may be built there. 0 = no yard.")]
    [SerializeField, Min(0f)] private float yardDepth;
    [Tooltip("Which side of the model is the entrance, in the model's own axes (Front is its local +Z).")]
    [SerializeField] private YardSide yardSide = YardSide.Front;
    [Tooltip("Built in lines by dragging, joining the ends of others like it: walls, fences, palisades.")]
    [SerializeField] private bool placeInLines;

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
    public float YardDepth => yardDepth;
    public YardSide YardSide => yardSide;
    public bool PlaceInLines => placeInLines;
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
