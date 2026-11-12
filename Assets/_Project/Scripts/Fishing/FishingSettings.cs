using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where a fish bites: in rivers, in still water (ponds, tarns), or anywhere.</summary>
public enum FishWater { Anywhere = 0, Flowing = 1, Still = 2 }

/// <summary>
/// Fishing (PlayerFishing): how far and how fast the line is cast, how long a bite takes, the fight with a hooked fish
/// (FishFight: how it runs and rests, how fast the reel brings it in, how long the line holds at full strain or slack,
/// how soon it tires), which fish bite where, which bait works how well, and the Nøkken who sometimes rises from still
/// water instead of a fish. Lives at Resources/FishingSettings; built-in defaults apply when the asset is missing (then
/// nothing bites).
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Fishing Settings", fileName = "FishingSettings")]
public sealed class FishingSettings : GameSettings<FishingSettings>
{
    [Serializable]
    public sealed class Fish
    {
        public ItemDefinition item;
        [Tooltip("Relative chance against the other fish that can bite there.")]
        [Min(0f)] public float weight = 1f;
        public FishWater water = FishWater.Anywhere;
        [Tooltip("0: a lazy fish that hardly runs; 1: a fighter that runs hard and often.")]
        [Range(0f, 1f)] public float fight = 0.3f;
        [Tooltip("Kilograms of a typical one (the middle of this range is one of middling length, FishSizes); a longer fish weighs more by the cube of its length.")]
        public Vector2 size = new Vector2(0.3f, 1.2f);
        [Tooltip("How long this kind runs against the others: 1 is a fish of 33 cm in the middle of the curve, 0.75 a perch's, 1.5 a pike's (FishSizes: 10-200 cm whatever the kind).")]
        [Min(0.2f)] public float lengthScale = 1f;
        [Tooltip("Its colour when it hangs from the line at the landing.")]
        public Color colour = new Color(0.55f, 0.6f, 0.5f, 1f);
    }

    /// <summary>The fight with a hooked fish (FishFight). Seconds and metres; pairs run from a lazy fish to a fighter.</summary>
    [Serializable]
    public sealed class FightTuning
    {
        [Tooltip("Metres a second the reel brings in a resting fish, and a spent one.")]
        [Min(0.1f)] public float reelSpeed = 1.6f;
        [Min(0.1f)] public float spentReelSpeed = 3f;
        [Tooltip("Metres a second a running fish takes at its hardest pull while the reel isn't turning.")]
        [Min(0f)] public float runTake = 2.4f;
        [Tooltip("Seconds of steady pressure to tire a fish (lazy to fighter), for one of middling size.")]
        public Vector2 tireSeconds = new Vector2(9f, 22f);
        [Tooltip("Seconds a fish rests between runs, warns before one (the float jerks), and runs.")]
        public Vector2 restSeconds = new Vector2(2.8f, 1.5f);
        public Vector2 warnSeconds = new Vector2(0.8f, 0.5f);
        public Vector2 runSeconds = new Vector2(1.1f, 2.4f);
        [Tooltip("How hard a fresh fish pulls on a run (1 is enough to strain a turning reel).")]
        public Vector2 runPull = new Vector2(0.55f, 1f);
        [Tooltip("Share of runs that go straight out, where the rod's side doesn't help.")]
        [Range(0f, 1f)] public float straightRuns = 0.2f;
        [Tooltip("Seconds the line holds at full strain before it snaps, and slack before a fresh fish shakes the hook.")]
        [Min(0.1f)] public float strainSeconds = 1.3f;
        [Min(0.1f)] public float slackSeconds = 5f;
        [Tooltip("Rod held against a run: its pull drops by this share and it tires this many times faster.")]
        [Range(0f, 0.9f)] public float sideGrip = 0.45f;
        [Min(1f)] public float sideTire = 2.2f;
        [Tooltip("Rod held with the run: its pull grows by this factor and it tires at this share of the speed.")]
        [Min(1f)] public float wrongSidePull = 1.25f;
        [Range(0.1f, 1f)] public float wrongSideTire = 0.6f;
        [Tooltip("Furthest a run swings off the line of the cast, in degrees.")]
        [Range(5f, 70f)] public float maxAngle = 40f;
        [Tooltip("Metres of line at which a tired fish is landed where the water comes right up to the angler (it is landed sooner, tired or not, where it is brought up onto the shore), and the most line there is.")]
        [Min(0.5f)] public float landDistance = 1f;
        [Min(5f)] public float maxLine = 28f;
        [Tooltip("A fish is tired enough to land once its stamina is down to this share.")]
        [Range(0f, 0.6f)] public float landStamina = 0.2f;
    }

    [Serializable]
    public sealed class Bait
    {
        public ItemDefinition item;
        [Tooltip("How much sooner fish bite with it: 1 is normal, 1.5 is a third less waiting.")]
        [Min(0.1f)] public float lure = 1f;
        [Tooltip("How much easier a big fish is on it: it bites more readily and its extra fight is cut by this share (0 none, 0.5 halved).")]
        [Range(0f, 0.8f)] public float ease;
    }

    [Header("Casting")]
    [SerializeField, Min(1f)] private float minimumCast = 3f;
    [SerializeField, Min(1f)] private float maximumCast = 12f;
    [Tooltip("Seconds of holding attack to cast the furthest.")]
    [SerializeField, Min(0.1f)] private float chargeSeconds = 1.2f;

    [Header("Biting")]
    [Tooltip("Seconds until a fish bites, at random between these (divided by the bait's lure).")]
    [SerializeField] private Vector2 biteWait = new Vector2(5f, 14f);
    [Tooltip("Seconds to strike (press attack) once the float goes under.")]
    [SerializeField, Min(0.2f)] private float strikeWindow = 1.1f;

    [Header("The fight")]
    [SerializeField] private FightTuning fightTuning = new FightTuning();
    [Tooltip("A fish this heavy or more (kg) is lifted out with both hands and held up; a lighter one is swung out on the line.")]
    [SerializeField, Min(0f)] private float haulKilograms = 2.5f;
    [Tooltip("The water thrown up when a fish runs, jumps or is lifted out (a particle effect that plays once).")]
    [SerializeField] private GameObject splashEffect;
    [Tooltip("What the landed fish is drawn with (its shape is made in code; Fish › Colour tints it).")]
    [SerializeField] private Material fishMaterial;

    [Header("Animation (shares of the clips)")]
    [Tooltip("Where the throw (Fish Cast) stops at the top of the backswing while a cast is being charged.")]
    [SerializeField, Range(0f, 1f)] private float castHoldPoint = 0.27f;
    [Tooltip("Where in the throw the rod flicks forward and the float leaves the tip.")]
    [SerializeField, Range(0f, 1f)] private float castReleasePoint = 0.4f;
    [Tooltip("Where in a landing (Fish Catch, Fish Haul Begin) the rod is high and the fish clear of the water.")]
    [SerializeField, Range(0f, 1f)] private float landLiftPoint = 0.4f;
    [Tooltip("Seconds a heavy fish is held up before it is put away.")]
    [SerializeField, Min(0f)] private float haulHoldSeconds = 1.6f;
    [SerializeField] private List<Fish> fish = new List<Fish>();
    [SerializeField] private List<Bait> bait = new List<Bait>();

    [Header("Nøkken")]
    [Tooltip("Chance that a bite in still water is the Nøkken instead of a fish.")]
    [SerializeField, Range(0f, 1f)] private float nokkenChance = 0.06f;
    [Tooltip("Presses of Space to break free.")]
    [SerializeField, Min(1)] private int nokkenPresses = 14;
    [Tooltip("Seconds before the Nøkken has dragged the player under.")]
    [SerializeField, Min(1f)] private float nokkenSeconds = 7f;
    [Tooltip("Health lost each second in its grip, and the share of maximum health lost if it wins.")]
    [SerializeField, Min(0f)] private float nokkenDamagePerSecond = 3f;
    [SerializeField, Range(0f, 1f)] private float nokkenWinsDamage = 0.35f;
    [Tooltip("Experience for breaking free of the Nøkken the first time, and each time after.")]
    [SerializeField, Min(0)] private int nokkenFirstEscapeExperience = 80;
    [SerializeField, Min(0)] private int nokkenEscapeExperience = 30;
    [SerializeField] private GameObject nokkenModel;
    [SerializeField] private SoundDefinition nokkenScream;
    [SerializeField] private SoundDefinition nokkenSplash;
    [Tooltip("The fright as it takes the player: a high, sudden stinger, heard before anything else.")]
    [SerializeField] private SoundDefinition nokkenStartle;
    [Tooltip("Seconds after the stinger before the battle music comes in; it then plays for as long as the Nøkken holds on.")]
    [SerializeField, Min(0f)] private float nokkenMusicDelay = 0.6f;
    [Tooltip("Camera shake as it takes the player (0-1, 1 the strongest), and the shake that keeps on while it drags them.")]
    [SerializeField, Range(0f, 1f)] private float nokkenGrabShake = 1f;
    [SerializeField, Range(0f, 1f)] private float nokkenHoldShake = 0.5f;
    [Tooltip("Raised the first time the player meets the Nøkken (quests, stories).")]
    [SerializeField, RaisedByGame] private GameEventDefinition metNokken;

    [Header("Float and Line")]
    [Tooltip("What bobs on the water: a model without colliders, about 15 cm across. Empty: a plain sphere.")]
    [SerializeField] private GameObject floatModel;
    [Tooltip("The fishing line's material. It is tinted by Line Colour.")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color lineColour = new Color(0.9f, 0.9f, 0.85f, 0.8f);
    [SerializeField, Min(0.001f)] private float lineWidth = 0.012f;

    [Header("Sound")]
    [SerializeField] private SoundDefinition castSound;
    [SerializeField] private SoundDefinition biteSound;
    [SerializeField] private SoundDefinition catchSound;
    [Tooltip("The reel turning (played over and over while reeling), the line creaking near its limit, and it snapping.")]
    [SerializeField] private SoundDefinition reelSound;
    [SerializeField] private SoundDefinition strainSound;
    [SerializeField] private SoundDefinition snapSound;
    [Tooltip("A fish thrashing at the surface on a run, and coming out of the water.")]
    [SerializeField] private SoundDefinition splashSound;

    public float MinimumCast => minimumCast;
    public float MaximumCast => Mathf.Max(minimumCast, maximumCast);
    public float ChargeSeconds => chargeSeconds;
    public Vector2 BiteWait => biteWait;
    public float StrikeWindow => strikeWindow;
    public FightTuning Fight => fightTuning ??= new FightTuning();
    public float HaulKilograms => haulKilograms;
    public GameObject SplashEffect => splashEffect;
    public Material FishMaterial => fishMaterial;
    public float CastHoldPoint => castHoldPoint;
    public float CastReleasePoint => Mathf.Max(castHoldPoint, castReleasePoint);
    public float LandLiftPoint => landLiftPoint;
    public float HaulHoldSeconds => haulHoldSeconds;
    public IReadOnlyList<Fish> FishList => fish;
    public IReadOnlyList<Bait> BaitList => bait;
    public float NokkenChance => nokkenChance;
    public int NokkenPresses => nokkenPresses;
    public float NokkenSeconds => nokkenSeconds;
    public float NokkenDamagePerSecond => nokkenDamagePerSecond;
    public float NokkenWinsDamage => nokkenWinsDamage;
    public int NokkenEscapeExperience(bool first) => first ? nokkenFirstEscapeExperience : nokkenEscapeExperience;
    public GameObject NokkenModel => nokkenModel;
    public SoundDefinition NokkenScream => nokkenScream;
    public SoundDefinition NokkenSplash => nokkenSplash;
    public SoundDefinition NokkenStartle => nokkenStartle;
    public float NokkenMusicDelay => nokkenMusicDelay;
    public float NokkenGrabShake => nokkenGrabShake;
    public float NokkenHoldShake => nokkenHoldShake;
    public GameEventDefinition MetNokken => metNokken;
    public GameObject FloatModel => floatModel;
    public Material LineMaterial => lineMaterial;
    public Color LineColour => lineColour;
    public float LineWidth => lineWidth;
    public SoundDefinition CastSound => castSound;
    public SoundDefinition BiteSound => biteSound;
    public SoundDefinition CatchSound => catchSound;
    public SoundDefinition ReelSound => reelSound;
    public SoundDefinition StrainSound => strainSound;
    public SoundDefinition SnapSound => snapSound;
    public SoundDefinition SplashSound => splashSound;

    /// <summary>A fish that bites in this water, weighted at random (null when none can).</summary>
    public Fish PickFish(bool still, float roll)
    {
        float total = 0f;
        foreach (Fish each in fish)
        {
            if (Bites(each, still)) total += each.weight;
        }

        float at = roll * total;
        foreach (Fish each in fish)
        {
            if (!Bites(each, still)) continue;
            at -= each.weight;
            if (at <= 0f) return each;
        }

        return null;
    }

    private static bool Bites(Fish each, bool still) =>
        each.item != null && each.weight > 0f && (each.water == FishWater.Anywhere || each.water == (still ? FishWater.Still : FishWater.Flowing));

    /// <summary>Sets fishing up (tests and editor tools).</summary>
    public void Configure(IEnumerable<Fish> fishes, IEnumerable<Bait> baits, float nokken)
    {
        fish = new List<Fish>(fishes);
        bait = new List<Bait>(baits);
        nokkenChance = Mathf.Clamp01(nokken);
    }
}
