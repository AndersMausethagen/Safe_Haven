using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How the Misty Woods' corruption creeps into the Haven at night (CorruptionNights): a small wave that walls, guards and
/// the player can mostly hold, unlike a ward breach. Since the design review (decisions 8.5 and 8.6, 2026-11-14) it is
/// foretold, never a surprise: when the player comes home from a raid the portal may sense it gathering, says from where,
/// and it comes at the next nightfall. How likely and how large follow the portal's tier and the blight in the woods,
/// never the buildings or renown (the spec's principle 11: don't punish building). Lives in Resources.
/// </summary>
[CreateAssetMenu(fileName = "CorruptionSettings", menuName = "Safe Haven/Settings/Corruption Settings")]
public sealed class CorruptionSettings : GameSettings<CorruptionSettings>
{
    [Header("When: foretold at a homecoming, a raid ahead")]
    [Tooltip("No corruption before the portal reaches this tier (PortalTiers; 2 = Stabilized, the ward's first level): the " +
             "woods take notice once the ward is raised, and the first days are left to building.")]
    [SerializeField, Min(0)] private int fromPortalTier = 2;
    [Tooltip("Chance, on coming home from a successful raid at that first tier, that it gathers for the next night.")]
    [SerializeField, Range(0f, 1f)] private float chance = 0.25f;
    [Tooltip("Chance added per portal tier above the first.")]
    [SerializeField, Range(0f, 0.3f)] private float chancePerTier = 0.05f;
    [Tooltip("Chance added at full blight in the woods round home (it scales with the blight).")]
    [SerializeField, Range(0f, 1f)] private float chancePerBlight = 0.3f;
    [SerializeField, Range(0f, 1f)] private float mostChance = 0.5f;

    [Header("The wave")]
    [Tooltip("What comes. One is picked at random for each creature.")]
    [SerializeField] private List<GameObject> creatures = new List<GameObject>();
    [SerializeField, Min(1)] private int count = 3;
    [Tooltip("Creatures added per portal tier above the first.")]
    [SerializeField, Min(0)] private int countPerTier = 1;
    [Tooltip("Creatures added at full blight (it scales with the blight).")]
    [SerializeField, Min(0)] private int countPerBlight = 3;
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
    [Tooltip("Said on coming home when it gathers for the next night. {0} is where from (\"the north-east\").")]
    [SerializeField] private string foretold = "The portal stirs: corruption gathers in the woods to the {0}. At nightfall it will come for the Haven. Ready the walls and the fire.";
    [SerializeField] private string warning = "The mist thickens at the forest edge. Something from the Misty Woods is coming for the Haven.";
    [SerializeField] private string cleared = "The corruption is beaten back. The night is quiet again.";

    public IReadOnlyList<GameObject> Creatures => creatures;
    public Vector2 Distance => distance;
    public float Spread => spread;
    public float Delay => delay;
    public float DawnSeconds => dawnSeconds;
    public string Warning => warning;
    public string Foretold => foretold;
    public string Cleared => cleared;
    public SoundDefinition WarningSound => warningSound;
    public WeatherDefinition Weather => weather;
    public float WeatherSeconds => weatherSeconds;
    public int ExperienceFor(int creatures) => experience + experiencePerCreature * Mathf.Max(0, creatures);

    /// <summary>The chance, on a homecoming, that it gathers for the next night: by the portal's tier and the blight (0..1),
    /// 0 below the first tier. Never by buildings or renown.</summary>
    public float ChanceAt(int portalTier, float blight) => portalTier < fromPortalTier ? 0f
        : Mathf.Min(mostChance, chance + chancePerTier * (portalTier - fromPortalTier) + chancePerBlight * Mathf.Clamp01(blight));

    /// <summary>How many come: by the portal's tier and the blight.</summary>
    public int CountAt(int portalTier, float blight) => Mathf.Min(mostCount,
        count + countPerTier * Mathf.Max(0, portalTier - fromPortalTier) + Mathf.RoundToInt(countPerBlight * Mathf.Clamp01(blight)));
}
