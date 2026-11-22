using UnityEngine;

/// <summary>
/// The fields' tuning (Fields; the Keeper's view, phase 5): how long a watering keeps a crop growing, how a dry plot slows (never
/// dies), what rain and a Waterskin do, and how far a Farmer goes for water and tends round their own plot. Resources/FarmSettings.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Farm Settings", fileName = "FarmSettings")]
public sealed class FarmSettings : GameSettings<FarmSettings>
{
    [Header("Water")]
    [Tooltip("Seconds of growing one watering gives a plot (a day is 24 minutes). It drains only while something grows.")]
    [SerializeField, Min(10f)] private float wateredSeconds = 480f;
    [Tooltip("How fast a crop grows in dry soil, of its watered pace: slower, never dead.")]
    [SerializeField, Range(0f, 1f)] private float drySpeed = 0.35f;
    [Tooltip("Rain at least this strong (0-1) waters every plot at home.")]
    [SerializeField, Range(0.05f, 1f)] private float rainWaters = 0.3f;
    [Tooltip("How much of a full water container (a Waterskin) watering one plot by hand takes.")]
    [SerializeField, Range(0.05f, 1f)] private float waterPerPlot = 0.5f;

    [Header("The Farmer")]
    [Tooltip("A Farmer fetches water once a growing plot has less than this share of a watering left.")]
    [SerializeField, Range(0f, 1f)] private float waterWhenBelow = 0.35f;
    [Tooltip("Metres round their own plot within which a Farmer waters every plot on one trip.")]
    [SerializeField, Min(1f)] private float tendReach = 30f;
    [Tooltip("Metres a Farmer goes for water at most (a well, a stream, a mere).")]
    [SerializeField, Min(5f)] private float waterReach = 120f;
    [Tooltip("Shown in the Farmer's hand on the way back with water.")]
    [SerializeField] private ItemDefinition waterCarried;

    [Header("Look")]
    [Tooltip("A crop's size when just sown, of its grown size.")]
    [SerializeField, Range(0.05f, 1f)] private float sownScale = 0.2f;
    [Tooltip("How much darker watered soil looks.")]
    [SerializeField, Range(0f, 0.8f)] private float wetDarkening = 0.35f;

    public float WateredSeconds => wateredSeconds;
    public float DrySpeed => drySpeed;
    public float RainWaters => rainWaters;
    public float WaterPerPlot => waterPerPlot;
    public float WaterWhenBelow => waterWhenBelow;
    public float TendReach => tendReach;
    public float WaterReach => waterReach;
    public ItemDefinition WaterCarried => waterCarried;
    public float SownScale => sownScale;
    public float WetDarkening => wetDarkening;
}
