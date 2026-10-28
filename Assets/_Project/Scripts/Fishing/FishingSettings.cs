using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Where a fish bites: in rivers, in still water (ponds, tarns), or anywhere.</summary>
public enum FishWater { Anywhere = 0, Flowing = 1, Still = 2 }

/// <summary>
/// Fishing (PlayerFishing): how far and how fast the line is cast, how long a bite takes, the reeling game (how fast the
/// fish darts, how the catch zone rises and falls, how quickly the catch fills and drains), which fish bite where, which
/// bait works how well, and the Nøkken who sometimes rises from still water instead of a fish. Lives at
/// Resources/FishingSettings; built-in defaults apply when the asset is missing (then nothing bites).
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
        [Tooltip("0: a lazy fish that hardly moves; 1: one that darts all over the bar.")]
        [Range(0f, 1f)] public float fight = 0.3f;
        [Tooltip("Kilograms, shown when caught.")]
        public Vector2 size = new Vector2(0.3f, 1.2f);
    }

    [Serializable]
    public sealed class Bait
    {
        public ItemDefinition item;
        [Tooltip("How much sooner fish bite with it: 1 is normal, 1.5 is a third less waiting.")]
        [Min(0.1f)] public float lure = 1f;
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

    [Header("Reeling")]
    [Tooltip("Share of the bar the catch zone covers.")]
    [SerializeField, Range(0.1f, 0.6f)] private float zoneSize = 0.28f;
    [Tooltip("How fast the zone climbs while attack is held, and falls when let go (bar heights per second²).")]
    [SerializeField, Min(0.1f)] private float zoneLift = 2.6f;
    [SerializeField, Min(0.1f)] private float zoneFall = 2.2f;
    [Tooltip("Catch gained per second while the fish is in the zone, and lost while it isn't.")]
    [SerializeField, Min(0.01f)] private float catchFill = 0.32f;
    [SerializeField, Min(0.01f)] private float catchDrain = 0.15f;
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
    [SerializeField] private GameObject nokkenModel;
    [SerializeField] private SoundDefinition nokkenScream;
    [SerializeField] private SoundDefinition nokkenSplash;
    [Tooltip("Raised the first time the player meets the Nøkken (quests, stories).")]
    [SerializeField, RaisedByGame] private GameEventDefinition metNokken;

    [Header("Sound")]
    [SerializeField] private SoundDefinition castSound;
    [SerializeField] private SoundDefinition biteSound;
    [SerializeField] private SoundDefinition catchSound;

    public float MinimumCast => minimumCast;
    public float MaximumCast => Mathf.Max(minimumCast, maximumCast);
    public float ChargeSeconds => chargeSeconds;
    public Vector2 BiteWait => biteWait;
    public float StrikeWindow => strikeWindow;
    public float ZoneSize => zoneSize;
    public float ZoneLift => zoneLift;
    public float ZoneFall => zoneFall;
    public float CatchFill => catchFill;
    public float CatchDrain => catchDrain;
    public IReadOnlyList<Fish> FishList => fish;
    public IReadOnlyList<Bait> BaitList => bait;
    public float NokkenChance => nokkenChance;
    public int NokkenPresses => nokkenPresses;
    public float NokkenSeconds => nokkenSeconds;
    public float NokkenDamagePerSecond => nokkenDamagePerSecond;
    public float NokkenWinsDamage => nokkenWinsDamage;
    public GameObject NokkenModel => nokkenModel;
    public SoundDefinition NokkenScream => nokkenScream;
    public SoundDefinition NokkenSplash => nokkenSplash;
    public GameEventDefinition MetNokken => metNokken;
    public SoundDefinition CastSound => castSound;
    public SoundDefinition BiteSound => biteSound;
    public SoundDefinition CatchSound => catchSound;

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
