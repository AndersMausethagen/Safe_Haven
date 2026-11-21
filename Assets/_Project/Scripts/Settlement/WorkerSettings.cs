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
    [Tooltip("Metres from a workplace or chest in which a free cart or wheelbarrow is taken along.")]
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

    public float UnattendedSpeed => unattendedSpeed;
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
