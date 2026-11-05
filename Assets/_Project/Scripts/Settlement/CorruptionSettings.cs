using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How the Misty Woods' corruption creeps into the Haven at night (CorruptionNights). Rare at first and more likely, and
/// larger, as the Haven grows (its rank): a small wave that walls, guards and the player can mostly hold, unlike a ward
/// breach. Lives in Resources.
/// </summary>
[CreateAssetMenu(fileName = "CorruptionSettings", menuName = "Safe Haven/Settings/Corruption Settings")]
public sealed class CorruptionSettings : GameSettings<CorruptionSettings>
{
    [Header("When")]
    [Tooltip("No corruption before the Haven reaches this rank, so the first days are left to building.")]
    [SerializeField, Min(0)] private int fromRank = 1;
    [Tooltip("Chance on a night at that first rank (0.17: about one night in six).")]
    [SerializeField, Range(0f, 1f)] private float chance = 0.17f;
    [Tooltip("Chance added per Haven rank above the first.")]
    [SerializeField, Range(0f, 0.3f)] private float chancePerRank = 0.03f;
    [SerializeField, Range(0f, 1f)] private float mostChance = 0.45f;

    [Header("The wave")]
    [Tooltip("What comes. One is picked at random for each creature.")]
    [SerializeField] private List<GameObject> creatures = new List<GameObject>();
    [SerializeField, Min(1)] private int count = 3;
    [Tooltip("Haven ranks for each extra creature.")]
    [SerializeField, Min(1)] private int ranksPerExtra = 2;
    [SerializeField, Min(1)] private int mostCount = 8;
    [Tooltip("Metres from the Haven's portal where they come out of the dark.")]
    [SerializeField] private Vector2 distance = new Vector2(45f, 60f);
    [Tooltip("Degrees either side of the night's one direction: they come as one band, from the woods.")]
    [SerializeField, Range(0f, 180f)] private float spread = 35f;
    [Tooltip("Seconds after nightfall before they come, so the warning is heard first.")]
    [SerializeField, Min(0f)] private float delay = 20f;
    [Tooltip("Seconds the survivors have at dawn to slink off before they are removed.")]
    [SerializeField, Min(1f)] private float dawnSeconds = 20f;

    [Header("Warning")]
    [Tooltip("Heard when the corruption begins, from the side it comes from (a howl in the woods).")]
    [SerializeField] private SoundDefinition warningSound;
    [Tooltip("The weather the corruption brings (the game's own fog and terrain mist), until the band is beaten or dawn; then the weather before it returns. Empty: the weather is left alone.")]
    [SerializeField] private WeatherDefinition weather;
    [Tooltip("Seconds the fog takes to roll in.")]
    [SerializeField, Min(0f)] private float weatherSeconds = 10f;

    [Header("Holding it off")]
    [Tooltip("Experience for beating the whole band before dawn.")]
    [SerializeField, Min(0)] private int experience = 40;
    [Tooltip("Extra experience per creature in the band.")]
    [SerializeField, Min(0)] private int experiencePerCreature = 10;

    [Header("Words")]
    [SerializeField] private string warning = "The mist thickens at the forest edge. Something from the Misty Woods is coming for the Haven.";
    [SerializeField] private string cleared = "The corruption is beaten back. The night is quiet again.";

    public IReadOnlyList<GameObject> Creatures => creatures;
    public Vector2 Distance => distance;
    public float Spread => spread;
    public float Delay => delay;
    public float DawnSeconds => dawnSeconds;
    public string Warning => warning;
    public string Cleared => cleared;
    public SoundDefinition WarningSound => warningSound;
    public WeatherDefinition Weather => weather;
    public float WeatherSeconds => weatherSeconds;
    public int ExperienceFor(int creatures) => experience + experiencePerCreature * Mathf.Max(0, creatures);

    /// <summary>The chance of a corrupted night at a Haven rank (0 below the first rank).</summary>
    public float ChanceAt(int rank) => rank < fromRank ? 0f : Mathf.Min(mostChance, chance + chancePerRank * (rank - fromRank));

    /// <summary>How many come at a Haven rank.</summary>
    public int CountAt(int rank) => Mathf.Min(mostCount, count + Mathf.Max(0, rank - fromRank) / ranksPerExtra);
}
