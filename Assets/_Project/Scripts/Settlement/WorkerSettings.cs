using UnityEngine;

/// <summary>
/// How residents work and carry (the user, 2026-11-21: "They have to get resources from storage, but they have reduced carry capacity
/// and slots... We can train and upgrade the NPCs with equipment or by having them with us in raids or doing other activities. This
/// will also make wheelbarrows, horses and carts useful"; Palworld's work speed, Docs/ColonyModeResearch_2026-11-21.md). A station
/// runs at Unattended Speed (plus its upgrades) on its own, and its worker adds their work speed while they stand at it; they fetch
/// the inputs of their work orders from a chest within their carry (weight and kinds), and take finished work back on the same trip.
/// Carrying more comes from hauling (Strength), going along on raids, worker gear (ItemDefinition › Worker Gear), a cart taken along
/// or a horse working at the station. Lives at Resources/WorkerSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Worker Settings", fileName = "WorkerSettings")]
public sealed class WorkerSettings : GameSettings<WorkerSettings>
{
    [Header("Work speed")]
    [Tooltip("How fast a station works with nobody at it (before its upgrades, yard and extensions): 1 is a recipe's Craft Seconds.")]
    [SerializeField, Min(0.05f)] private float unattendedSpeed = 0.6f;
    [Tooltip("What a worker standing at their station adds, before their rank, bed, home and gear (Palworld's work speed).")]
    [SerializeField, Min(0f)] private float workerSpeed = 1f;
    [Tooltip("Stations that run at Unattended Speed with nobody at them (their recipes take half as long again); the rest (the farm, " +
             "the hive, the campfire, the portal, the dock) run at 1 on their own, as before.")]
    [SerializeField] private CraftingStationType[] workedStations =
    {
        CraftingStationType.Workbench, CraftingStationType.Anvil, CraftingStationType.AlchemyTable, CraftingStationType.Loom,
        CraftingStationType.Sawbench, CraftingStationType.Kitchen, CraftingStationType.Tannery, CraftingStationType.Wainwright,
    };
    [Tooltip("What the player adds standing at a station (within Player Reach): their own hands at the bench.")]
    [SerializeField, Min(0f)] private float playerSpeed = 1f;
    [Tooltip("Metres from a station within which the player counts as working at it.")]
    [SerializeField, Min(1f)] private float playerReach = 6f;

    [Header("Carrying")]
    [Tooltip("Weight a resident carries by hand (the player's bag holds 150).")]
    [SerializeField, Min(1f)] private float carryWeight = 25f;
    [Tooltip("Kinds of thing a resident carries at once by hand.")]
    [SerializeField, Min(1)] private int carrySlots = 1;
    [Tooltip("Strength: weight hauled (and raids gone along on, below) for each level, cumulative.")]
    [SerializeField] private float[] strengthAt = { 300f, 900f, 1800f, 3200f, 5000f };
    [Tooltip("Weight each Strength level adds.")]
    [SerializeField, Min(0f)] private float weightPerStrength = 5f;
    [Tooltip("Strength levels at which they carry one more kind.")]
    [SerializeField] private int[] slotAtStrength = { 2, 4 };
    [Tooltip("What a raid gone along on counts as, in weight hauled, for Strength.")]
    [SerializeField, Min(0f)] private float strengthPerRaid = 250f;

    [Header("Help with carrying")]
    [Tooltip("Metres from a workplace in which a free handcart is taken along (wheelbarrows are pushed, by the player only).")]
    [SerializeField, Min(0f)] private float cartReach = 18f;
    [Tooltip("Kinds a cart holds when a resident takes it (its weight is the cart's own Capacity).")]
    [SerializeField, Min(1)] private int cartSlots = 4;
    [Tooltip("A horse working at the station (HorseJobs) carries the packs: the worker's weight times this, and one more kind.")]
    [SerializeField, Min(1f)] private float horseCarry = 2f;

    [Header("Trips")]
    [Tooltip("Metres a chest may be from a workplace for its worker to fetch from it; further, the work waits.")]
    [SerializeField, Min(5f)] private float chestReach = 120f;
    [Tooltip("Seconds of a trip while the player is away, besides the walk there and back (loading, unloading).")]
    [SerializeField, Min(0f)] private float tripHandling = 6f;
    [Tooltip("A worker's walk while away, in metres a second (their walk at home is Settlement Settings › Resident Walk Speed).")]
    [SerializeField, Min(0.1f)] private float awayWalkSpeed = 1.6f;
    [Tooltip("Metres between a station and its chest assumed while away when none was measured at home.")]
    [SerializeField, Min(1f)] private float assumedTripDistance = 30f;

    [Header("Felling rings")]
    [Tooltip("Jobs whose holders fell the rings drawn in the Keeper's view (the Woodcutter's Lodge's).")]
    [SerializeField] private JobDefinition[] fellers = System.Array.Empty<JobDefinition>();
    [Tooltip("Seconds of chopping a tree takes a feller at work speed 1.")]
    [SerializeField, Min(1f)] private float chopSeconds = 12f;
    [Tooltip("Seconds a tree's logs take to the chests and back, while the player is away.")]
    [SerializeField, Min(0f)] private float fellWalkSeconds = 20f;
    [Tooltip("The axe tier a feller cuts with (a tree's logs: TreeChoppingSettings).")]
    [SerializeField, Range(1, 5)] private int fellerAxeTier = 1;
    [Tooltip("Metres tall a tree felled while away is taken to be (its logs).")]
    [SerializeField, Min(1f)] private float averageTreeHeight = 9f;
    [Tooltip("The sapling planted where a felled tree stood (the forester); empty plants none.")]
    [SerializeField] private SaplingDefinition sapling;
    [Tooltip("Rings that may stand at once.")]
    [SerializeField, Min(1)] private int mostRings = 3;
    [Tooltip("A new ring's radius in metres (the wheel changes it while drawing).")]
    [SerializeField, Min(3f)] private float ringRadius = 10f;
    [Tooltip("The share of the time away a woodcutter fells (working hours only, and their bench first, as at home).")]
    [SerializeField, Range(0f, 1f)] private float awayFellingShare = 0.4f;
    [Tooltip("What a woodcutter holds while felling (shown in hand; any axe).")]
    [SerializeField] private ItemDefinition fellingTool;

    [Header("Quarry rings")]
    [Tooltip("Jobs whose holders break the stone in the quarry rings drawn in the Keeper's view (the Mason's).")]
    [SerializeField] private JobDefinition[] quarriers = System.Array.Empty<JobDefinition>();
    [Tooltip("Seconds of breaking a rock takes a quarrier at work speed 1.")]
    [SerializeField, Min(1f)] private float quarrySeconds = 14f;
    [Tooltip("The pickaxe tier a quarrier breaks with: rocks needing more are left for the player.")]
    [SerializeField, Range(1, 5)] private int quarryPickTier = 1;
    [Tooltip("What a quarrier breaks (stone, not ore): empty, any rock a pickaxe of their tier gathers.")]
    [SerializeField] private System.Collections.Generic.List<GatheringNodeDefinition> quarried = new System.Collections.Generic.List<GatheringNodeDefinition>();
    [Tooltip("What a rock broken while the player is away gives (an average one), straight to the chests.")]
    [SerializeField] private System.Collections.Generic.List<ItemAmount> awayRockYield = new System.Collections.Generic.List<ItemAmount>();
    [Tooltip("The share of the time away a quarrier breaks stone (working hours only, and scaffolds and their bench first).")]
    [SerializeField, Range(0f, 1f)] private float awayQuarryShare = 0.4f;
    [Tooltip("What a quarrier holds while breaking stone (shown in hand; any pickaxe).")]
    [SerializeField] private ItemDefinition quarryTool;

    public System.Collections.Generic.IReadOnlyList<JobDefinition> Quarriers => quarriers;
    public float QuarrySeconds => quarrySeconds;
    public int QuarryPickTier => quarryPickTier;
    public System.Collections.Generic.IReadOnlyList<GatheringNodeDefinition> Quarried => quarried;

    /// <summary>Whether a quarrier may break rocks of <paramref name="definition"/> (Quarried; empty allows any).</summary>
    public bool MayQuarry(GatheringNodeDefinition definition) => quarried.Count == 0 || quarried.Contains(definition);
    public System.Collections.Generic.IReadOnlyList<ItemAmount> AwayRockYield => awayRockYield;
    public float AwayQuarryShare => awayQuarryShare;
    public ItemDefinition QuarryTool => quarryTool;

    public System.Collections.Generic.IReadOnlyList<JobDefinition> Fellers => fellers;
    public float ChopSeconds => chopSeconds;
    public float FellWalkSeconds => fellWalkSeconds;
    public int FellerAxeTier => fellerAxeTier;
    public float AverageTreeHeight => averageTreeHeight;
    public SaplingDefinition Sapling => sapling;
    public int MostRings => mostRings;
    public float RingRadius => ringRadius;
    public float AwayFellingShare => awayFellingShare;
    public ItemDefinition FellingTool => fellingTool;

    public float UnattendedSpeed => unattendedSpeed;
    public float PlayerSpeed => playerSpeed;
    public float PlayerReach => playerReach;

    /// <summary>What a station of <paramref name="type"/> runs at with nobody at it: Unattended Speed for the worked kinds, else 1.</summary>
    public float UnattendedFor(CraftingStationType type) => IsWorked(type) ? unattendedSpeed : 1f;

    /// <summary>What the player standing at a station of <paramref name="type"/> adds: Player Speed at the worked kinds, else nothing
    /// (a farm or a hive grows at its own pace, whoever stands by it).</summary>
    public float PlayerSpeedFor(CraftingStationType type) => IsWorked(type) ? playerSpeed : 0f;

    /// <summary>Whether a station of <paramref name="type"/> is worked by hand (Worked Stations).</summary>
    public bool IsWorked(CraftingStationType type) => workedStations != null && System.Array.IndexOf(workedStations, type) >= 0;
    public float WorkerSpeed => workerSpeed;
    public float CarryWeight => carryWeight;
    public int CarrySlots => carrySlots;
    public float WeightPerStrength => weightPerStrength;
    public float StrengthPerRaid => strengthPerRaid;
    public float CartReach => cartReach;
    public int CartSlots => cartSlots;
    public float HorseCarry => horseCarry;
    public float ChestReach => chestReach;
    public float TripHandling => tripHandling;
    public float AwayWalkSpeed => awayWalkSpeed;
    public float AssumedTripDistance => assumedTripDistance;

    /// <summary>Strength from <paramref name="hauled"/> weight and <paramref name="raids"/> gone along on: 0 to the last level.</summary>
    public int StrengthFor(float hauled, int raids)
    {
        float worth = Mathf.Max(0f, hauled) + Mathf.Max(0, raids) * strengthPerRaid;
        int level = 0;
        while (strengthAt != null && level < strengthAt.Length && worth >= strengthAt[level])
        {
            level++;
        }

        return level;
    }

    /// <summary>Weight hauled for the next Strength level from <paramref name="level"/>, or -1 at the top.</summary>
    public float NextStrengthAt(int level) => strengthAt != null && level >= 0 && level < strengthAt.Length ? strengthAt[level] : -1f;

    /// <summary>Kinds carried by hand at <paramref name="strength"/>.</summary>
    public int SlotsAt(int strength)
    {
        int slots = carrySlots;
        foreach (int at in slotAtStrength ?? System.Array.Empty<int>())
        {
            slots += strength >= at ? 1 : 0;
        }

        return slots;
    }
}
