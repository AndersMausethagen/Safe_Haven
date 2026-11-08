using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What kind of item this is. Values are serialized as numbers, so add new kinds at the end and never renumber.
/// </summary>
public enum ItemCategory
{
    Equipment = 0,
    Consumable = 1,
    Material = 2,
    Quest = 3,
    Tool = 4,
    /// <summary>Exists to be carried home: relics, coins, trophies. Worth its value, has no other use yet.</summary>
    Valuable = 5,
}

/// <summary>
/// Authored data for an item: identity, how it looks in the UI and in the world, how it stacks, and what it does when it
/// is a tool, a weapon, a consumable, a backpack, a blueprint or a raid modifier. Inventories, crafting and pickups read
/// this; nothing here changes while playing.
/// <para>
/// Items are what the bag holds (GDD §12.1/§12.6): materials (wood, ore, planks, ingots, berries), equipment, weapons,
/// tools, consumables and unique finds. Every item weighs something against the bag's carry budget; only essences are
/// weightless ResourceDefinitions shown as bars. Gear – armor, weapons, tools and
/// backpacks – is equipped in the character screen's gear slots (see PlayerEquipment) and has durability: it is deployed
/// equipment, kept on death with durability damage (see DurabilitySettings).
/// </para>
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Safe Haven/Items/Item Definition")]
public sealed class ItemDefinition : ScriptableObject, IStackDefinition
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier, used for lookups and saves.")]
    [SerializeField] private string id;
    [Tooltip("Shown in the UI. Falls back to the asset name when empty.")]
    [SerializeField] private string displayName;
    [SerializeField, TextArea] private string description;
    [SerializeField] private ItemCategory category;
    [Tooltip("Progression tier, shown on the item. Usually the tier of the materials it is made from.")]
    [SerializeField, Range(Tiers.Min, Tiers.Max)] private int tier = 1;

    [Header("Loot")]
    [Tooltip("How rare and exciting the item is: colors it in the UI and picks its pickup sound (see Resources/RaritySettings).")]
    [SerializeField] private ItemRarity rarity = ItemRarity.Common;
    [Tooltip("What one of these is worth. Adds up to the haul value shown after a raid.")]
    [SerializeField, Min(0)] private int value = 1;
    [Tooltip("This piece's gear score, for what the rules get wrong (a torch or a fishing rod is no weapon). 0: worked out from its tier, slot and rarity.")]
    [SerializeField, Min(0)] private int gearScoreOverride;

    [Header("Appearance")]
    [Tooltip("Picture shown in inventories and tooltips.")]
    [SerializeField] private Sprite icon;
    [Tooltip("Generated prefab used when the item lies in the world.")]
    [SerializeField] private GameObject prefab;

    [Header("Held In Hand")]
    [Tooltip("Model shown in the player's right hand when this is the equipped weapon or the tool being used. Empty: " +
             "the world prefab above.")]
    [SerializeField] private GameObject heldModel;
    [Tooltip("Where the held model sits in the hand's grip (metres): zero for models made for the grip; tune it for " +
             "others in Safe Haven > Content > Held Item Grips.")]
    [SerializeField] private Vector3 heldPosition;
    [SerializeField] private Vector3 heldRotation;
    [SerializeField, Min(0.01f)] private float heldScale = 1f;

    [Header("Stacking")]
    [Tooltip("Whether several of this item share one inventory slot.")]
    [SerializeField] private bool stackable = true;
    [Tooltip("How many fit in one slot when stackable.")]
    [SerializeField, Min(1)] private int maximumStack = 99;

    [Header("Carrying")]
    [Tooltip("Weight of one item against the bag's carry budget (GDD §12.4). 0 means it weighs nothing.")]
    [SerializeField, Min(0f)] private float weight = 1f;

    [Header("Tool")]
    [Tooltip("The kind of gathering tool this is. None for items that aren't tools.")]
    [SerializeField] private ToolType toolType;
    [Tooltip("The highest resource tier this tool can gather. A Tier II pickaxe mines Iron Ore.")]
    [SerializeField, Range(0, Tiers.Max)] private int toolTier;

    [Header("Weapon")]
    [Tooltip("Makes this item a weapon the player can equip (right-click in the bag). Empty for other items.")]
    [SerializeField] private WeaponDefinition weapon;
    [Tooltip("Makes this item ammunition a bow shoots from the bag. Empty for other items.")]
    [SerializeField] private ArrowDefinition arrow;

    [Header("Consumable")]
    [Tooltip("Health restored when the item is used (right-click in the inventory).")]
    [SerializeField, Min(0f)] private float healAmount;
    [Tooltip("Hunger restored when the item is eaten (GDD §11).")]
    [SerializeField, Min(0f)] private float hungerRestore;
    [Tooltip("Thirst restored when the item is drunk.")]
    [SerializeField, Min(0f)] private float thirstRestore;
    [Tooltip("A water container (a waterskin): the water it holds when full. Each one keeps its own level; drinking from " +
             "it restores thirst from what is in it and keeps the container, and water sources fill it. 0 for anything else.")]
    [SerializeField, Min(0f)] private float waterCapacity;
    [Tooltip("Bonuses for Boon Seconds after it is eaten or drunk (GDD §11.4): Strength, Max Stamina, Move Speed, Gather Yield...")]
    [SerializeField] private List<StatBonus> boonBonuses = new List<StatBonus>();
    [Tooltip("Seconds its bonuses last.")]
    [SerializeField, Min(0f)] private float boonSeconds = 300f;
    [Tooltip("A tonic: its bonuses take no meal slot.")]
    [SerializeField] private bool tonic;
    [Tooltip("Status effects it takes away: an antidote cures Poison.")]
    [SerializeField] private List<StatusEffectDefinition> cures = new List<StatusEffectDefinition>();
    [Tooltip("A repair kit: share of full durability every worn piece gets back. 0.3 means 30%.")]
    [SerializeField, Range(0f, 1f)] private float gearRepair;
    [Tooltip("A field kit: what it sets down where the player stands, in a raid only (a camp, a way out).")]
    [SerializeField] private GameObject placesInRaid;

    [Header("Backpack")]
    [Tooltip("Extra carry weight for the bag while this backpack is worn. 0 means it isn't a backpack.")]
    [SerializeField, Min(0)] private int carryCapacityBonus;
    [Tooltip("Extra bag slots unlocked while this backpack is in the bag. Only the best backpack counts.")]
    [SerializeField, Min(0)] private int bagSlotBonus;
    [Tooltip("A pack that rattles: how many metres further every step carries while it is worn, half that while sneaking.")]
    [SerializeField, Min(0f)] private float carryNoise;
    [Tooltip("Speed while it is worn, as a share of normal. 1 for an ordinary pack.")]
    [SerializeField, Range(0.3f, 1f)] private float carrySpeedScale = 1f;
    [Tooltip("Heard with each step, for a pack that rattles.")]
    [SerializeField] private SoundDefinition carrySound;

    [Header("Gear")]
    [Tooltip("Makes this item armor or jewelry worn in that slot of the character screen (C). None for other items. " +
             "Weapons, backpacks and tools find their own slots.")]
    [SerializeField] private ArmorSlot armorSlot;
    [Tooltip("What it looks like on the character while worn. Optional: without one it can't be seen.")]
    [SerializeField] private GearLookDefinition wornLook;
    [Tooltip("Stats added while this item is equipped and not broken: Armor, Max Health, Strength, resistances...")]
    [SerializeField] private List<StatBonus> statBonuses = new List<StatBonus>();

    [Header("Blueprint")]
    [Tooltip("Buildings learned when the player reads this item (right-click). Reading uses it up.")]
    [SerializeField] private List<BuildingDefinition> teachesBuildings = new List<BuildingDefinition>();
    [Tooltip("Recipes learned when the player reads this item.")]
    [SerializeField] private List<RecipeDefinition> teachesRecipes = new List<RecipeDefinition>();
    [Tooltip("Experience gained when the player reads this item (a rune book). Reading uses it up. 0: none.")]
    [SerializeField, Min(0)] private int experience;
    [Tooltip("What it teaches can only be taken in at home, so it has to survive the way back: plans found in a raid, a swarm of bees.")]
    [SerializeField] private bool learnedAtHome;

    [Header("Transport")]
    [Tooltip("Makes this item a transport's kit: using it sets the cart or boat down. Empty for other items.")]
    [SerializeField] private TransportDefinition transport;

    [Header("Lantern")]
    [Tooltip("Makes this item a light worn on the belt (the Light slot): how it shines and how long it lasts. Empty for other items.")]
    [SerializeField] private LanternDefinition lantern;

    [Header("Raid")]
    [Tooltip("Makes this item a raid modifier: picked at the raid portal and used up when the raid starts. Empty for other items.")]
    [SerializeField] private RaidModifierDefinition raidModifier;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Description => description;
    public ItemCategory Category => category;
    public int Tier => tier;
    public ItemRarity Rarity => rarity;
    public int Value => value;
    public int GearScoreOverride => gearScoreOverride;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;
    /// <summary>Model shown in the hand (the held model, else the world prefab); null when the item has none.</summary>
    public GameObject HeldModel => heldModel != null ? heldModel : prefab;
    /// <summary>The held model's place in the hand's grip.</summary>
    public Pose HeldPose => new Pose(heldPosition, Quaternion.Euler(heldRotation));
    public float HeldScale => heldScale;
    /// <summary>Items with durability never stack, so every one keeps its own wear.</summary>
    public bool Stackable => stackable && !KeepsCondition;
    public int MaximumStack => Stackable ? Mathf.Max(1, maximumStack) : 1;
    int IStackDefinition.StackSize => MaximumStack;
    Color? IStackDefinition.SlotTint => rarity != ItemRarity.Common ? global::Rarity.ColorOf(rarity) : (Color?)null;
    public float Weight => weight;
    public ToolType ToolType => toolType;
    public int ToolTier => toolType == ToolType.None ? 0 : toolTier;
    public bool IsTool => toolType != ToolType.None && toolTier > 0;
    public WeaponDefinition Weapon => weapon;
    public bool IsWeapon => weapon != null;
    public ArrowDefinition Arrow => arrow;
    public bool IsArrow => arrow != null;
    public float HealAmount => healAmount;
    public float HungerRestore => hungerRestore;
    public float ThirstRestore => thirstRestore;
    public float WaterCapacity => waterCapacity;
    public bool HoldsWater => waterCapacity > 0f;
    public LanternDefinition Lantern => lantern;
    public bool IsLantern => lantern != null;

    /// <summary>Each stack keeps its own condition: durability for gear, the water level for a water container, the light
    /// left in a lantern.</summary>
    public bool KeepsCondition => HasDurability || HoldsWater || IsLantern;
    public IReadOnlyList<StatBonus> BoonBonuses => boonBonuses;
    public float BoonSeconds => boonSeconds;
    public bool IsTonic => tonic;
    public IReadOnlyList<StatusEffectDefinition> Cures => cures;
    /// <summary>True when eating it gives timed bonuses.</summary>
    public bool HasBoon => boonBonuses.Count > 0 && boonSeconds > 0f;
    /// <summary>True for consumables: something restores health, hunger or thirst, gives a boon or cures when used.</summary>
    public bool IsConsumable => healAmount > 0f || hungerRestore > 0f || thirstRestore > 0f || waterCapacity > 0f || HasBoon || cures.Count > 0 || gearRepair > 0f || placesInRaid != null;
    public float GearRepair => gearRepair;
    public GameObject PlacesInRaid => placesInRaid;
    /// <summary>True when right-clicking does something: eat, drink, heal, equip or read.</summary>
    public bool IsUsable => IsConsumable || IsEquippable || IsBlueprint || experience > 0 || transport != null;
    public TransportDefinition Transport => transport;
    public int CarryCapacityBonus => carryCapacityBonus;
    public float CarryNoise => carryNoise;
    public float CarrySpeedScale => carrySpeedScale;
    public SoundDefinition CarrySound => carrySound;
    public int BagSlotBonus => bagSlotBonus;
    public bool IsBackpack => carryCapacityBonus > 0 || bagSlotBonus > 0;
    public ArmorSlot ArmorSlot => armorSlot;
    public bool IsArmor => armorSlot != ArmorSlot.None;
    public GearLookDefinition WornLook => wornLook;
    /// <summary>True for anything that goes into a gear slot: armor, jewelry, weapons, backpacks and tools.</summary>
    public bool IsEquippable => IsArmor || IsWeapon || IsBackpack || IsTool || IsLantern;
    public IReadOnlyList<StatBonus> StatBonuses => statBonuses;
    public IReadOnlyList<BuildingDefinition> TeachesBuildings => teachesBuildings;
    public IReadOnlyList<RecipeDefinition> TeachesRecipes => teachesRecipes;
    public bool IsBlueprint => teachesBuildings.Count > 0 || teachesRecipes.Count > 0;
    public bool IsLearnedAtHome => learnedAtHome;
    /// <summary>Experience for reading it (a rune book).</summary>
    public int Experience => experience;
    /// <summary>Gear wears: tools, weapons, backpacks and armor are deployed equipment that survives death with damage.</summary>
    public bool HasDurability => IsEquippable;
    public RaidModifierDefinition RaidModifier => raidModifier;
    public bool IsRaidModifier => raidModifier != null;
}
