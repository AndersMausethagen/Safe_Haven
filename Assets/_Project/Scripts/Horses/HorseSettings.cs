using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The rules horses share (Resources/HorseSettings): which kinds roam (the common wild horse round the Haven, the rare toxic
/// horse of the bog), what each temperament does, the whistle, how bond grows and what it gives, breeding, and the first
/// horse every Haven is promised. A kind's own numbers are on its HorseDefinition.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Horses/Horse Settings", fileName = "HorseSettings")]
public sealed class HorseSettings : GameSettings<HorseSettings>
{
    [Tooltip("The wild horse met round the Haven, tamed with a lasso and barley.")]
    [SerializeField] private HorseDefinition wildHorse;
    [Tooltip("Other kinds that roam, each where and as often as its definition says (the bog's toxic horse).")]
    [SerializeField] private List<HorseDefinition> specialHorses = new List<HorseDefinition>();

    [Header("Temperament")]
    [SerializeField] private List<HorseTemperamentRules> temperaments = new List<HorseTemperamentRules>();

    [Header("The first horse")]
    [Tooltip("If the player has no horse by this in-game day, a calm one comes to graze near the Haven, and the journal says so.")]
    [SerializeField, Min(1)] private int firstHorseByDay = 3;
    [Tooltip("Metres from the Haven's heart the first horse grazes: near enough to find, out of the yard.")]
    [SerializeField] private Vector2 firstHorseRing = new Vector2(45f, 70f);
    [Tooltip("Who tells the player of it (a resident's name), and what they say.")]
    [SerializeField] private string firstHorseTeller = "Sigrid";
    [SerializeField, TextArea] private string firstHorseLine = "There's a horse grazing out past the yard. A quiet one. Take a lasso and some barley, and keep low.";

    [Header("Whistle")]
    [SerializeField] private SoundDefinition whistle;
    [Tooltip("Metres a horse hears its owner's whistle, at no bond and at full bond.")]
    [SerializeField] private Vector2 whistleReach = new Vector2(45f, 160f);
    [Tooltip("Bond a horse needs before it comes at a whistle.")]
    [SerializeField, Range(0f, 100f)] private float whistleBond = 15f;

    [Header("Bond (0-100)")]
    [SerializeField, Min(0f)] private float bondPerFeed = 4f;
    [Tooltip("Grooming, once each in-game day.")]
    [SerializeField, Min(0f)] private float bondPerGroom = 8f;
    [Tooltip("Bond for every kilometre ridden.")]
    [SerializeField, Min(0f)] private float bondPerKilometre = 6f;
    [Tooltip("Lost each in-game day with no feeding, grooming or riding.")]
    [SerializeField, Min(0f)] private float bondLostPerIdleDay = 2f;
    [Tooltip("Bond it starts with once tamed.")]
    [SerializeField, Range(0f, 100f)] private float bondWhenTamed = 10f;
    [Tooltip("At full bond: this much more stamina, and runs this much shorter when hurt.")]
    [SerializeField, Range(0f, 1f)] private float staminaAtFullBond = 0.2f;
    [SerializeField, Range(0f, 1f)] private float calmerAtFullBond = 0.7f;
    [Tooltip("Below this bond, a hit makes it throw its rider now and then.")]
    [SerializeField, Range(0f, 100f)] private float throwsBelowBond = 20f;

    [Header("Experience (Horsemanship)")]
    [SerializeField, Min(0f)] private float experiencePerTame = 40f;
    [SerializeField, Min(0f)] private float experiencePerFeed = 2f;
    [SerializeField, Min(0f)] private float experiencePerGroom = 5f;
    [SerializeField, Min(0f)] private float experiencePerKilometre = 12f;
    [SerializeField, Min(0f)] private float experiencePerMountedHit = 1.5f;
    [SerializeField, Min(0f)] private float experiencePerFoal = 30f;

    [Header("The struggle and the breaking ride")]
    [Tooltip("Leaning against the horse's pull (movement keys away from it) holds as this many taps a second, on the player's stamina.")]
    [SerializeField, Min(0f)] private float leanTapsPerSecond = 3f;
    [Tooltip("Player stamina a second spent leaning on the rope.")]
    [SerializeField, Min(0f)] private float leanStaminaPerSecond = 9f;
    [Tooltip("Metres a second the horse drags a player who neither leans nor taps, at full strain on the rope.")]
    [SerializeField, Min(0f)] private float dragSpeed = 2.6f;
    [Tooltip("Screen shake while it fights the rope (at full strain), and the jolt when it rears or turns to pull another way.")]
    [SerializeField, Range(0f, 1f)] private float ropeShake = 0.15f;
    [SerializeField, Range(0f, 1f)] private float ropeJolt = 0.26f;
    [Tooltip("On the breaking ride, seconds between bucks and veers, and how much balance one costs if not steered against.")]
    [SerializeField] private Vector2 buckSeconds = new Vector2(2.5f, 5f);
    [SerializeField, Range(0f, 1f)] private float buckBalance = 0.28f;
    [Tooltip("Balance a second won back by steering against the veer.")]
    [SerializeField, Min(0f)] private float steadyPerSecond = 0.35f;

    [Header("Ground")]
    [Tooltip("How fast a horse goes over each ground (the terrain layer under it, by a word in its name, first match wins): " +
             "quicker on paths, slower in mud and bog, a little slower on rock and forest floor.")]
    [SerializeField] private List<GroundSpeed> grounds = new List<GroundSpeed>();
    [Tooltip("Uphill: the speed left at the steepest climb it takes (a 1 in 1 grade).")]
    [SerializeField, Range(0.2f, 1f)] private float steepClimb = 0.55f;

    [Header("Jumping")]
    [Tooltip("At a trot or faster it jumps what is in its way, up to this high (metres), and gaps and streams up to this far.")]
    [SerializeField, Min(0.2f)] private float jumpHeight = 1.3f;
    [SerializeField] private Vector2 jumpLength = new Vector2(3.5f, 7f);

    [Header("Following")]
    [Tooltip("Metres behind its leader a following horse keeps, and how far a led one (a pack horse behind a rider) may fall back.")]
    [SerializeField, Min(1f)] private float followDistance = 3.5f;
    [SerializeField, Min(2f)] private float leadReach = 8f;

    [Header("Traits from use")]
    [Tooltip("Metres on rough ground (rock, forest floor, a steep climb) before it is sure-footed, metres swum before it is a swimmer, " +
             "metres ridden in all before it is enduring, and blows taken under a rider before it is brave.")]
    [SerializeField, Min(1f)] private float sureFootedMetres = 2000f;
    [SerializeField, Min(1f)] private float swimmerMetres = 300f;
    [SerializeField, Min(1f)] private float enduringMetres = 20000f;
    [SerializeField, Min(1)] private int braveHits = 10;

    [Header("Getting on")]
    [Tooltip("Metres from the horse the player may stand and still get on (it turns its side to them).")]
    [SerializeField, Min(0.5f)] private float mountReach = 2.4f;
    [Tooltip("The rider's path onto a horse from its left, from the pack's Rider_Mount_Left clip (BuildHorses measures it): " +
             "each point the body's offset from where it ends in the saddle (x right, y up, z forward, in the rider's own size) and, in w, " +
             "its turn from the horse's facing. From the right it is mirrored, and getting off runs it backwards.")]
    [SerializeField] private Vector4[] mountPath = System.Array.Empty<Vector4>();
    [SerializeField, Min(0.1f)] private float mountSeconds = 1.67f;

    [Header("Stable")]
    [Tooltip("Share of its health a stabled horse gets back each in-game hour.")]
    [SerializeField, Range(0f, 1f)] private float stableHealPerHour = 0.08f;

    [Header("Swimming")]
    [Tooltip("Metres a second it swims, the stamina a second that costs, and how deep water must be before it swims.")]
    [SerializeField, Min(0.1f)] private float swimSpeed = 2.6f;
    [SerializeField, Min(0f)] private float swimCost = 3f;
    [SerializeField, Min(0.2f)] private float swimDepth = 1.25f;

    [Header("Breeding")]
    [Tooltip("Bond both need, in the same stable.")]
    [SerializeField, Range(0f, 100f)] private float breedingBond = 50f;
    [Tooltip("In-game days from pairing to the foal, and days before the mare foals again.")]
    [SerializeField, Min(0.1f)] private float daysToFoal = 2f;
    [SerializeField, Min(0f)] private float daysBetweenFoals = 5f;
    [Tooltip("In-game days a foal takes to grow up, and its size when born.")]
    [SerializeField, Min(0.1f)] private float daysToGrow = 3f;
    [SerializeField, Range(0.3f, 0.9f)] private float foalSize = 0.55f;
    [Tooltip("How far a foal's numbers may stray from its parents' average, either way (0.08 = 8%).")]
    [SerializeField, Range(0f, 0.3f)] private float foalSpread = 0.08f;

    public HorseDefinition WildHorse => wildHorse;
    public IReadOnlyList<HorseDefinition> SpecialHorses => specialHorses;
    public int FirstHorseByDay => firstHorseByDay;
    public Vector2 FirstHorseRing => firstHorseRing;
    public string FirstHorseTeller => firstHorseTeller;
    public string FirstHorseLine => firstHorseLine;
    public float LeanTapsPerSecond => leanTapsPerSecond;
    public float LeanStaminaPerSecond => leanStaminaPerSecond;
    public float DragSpeed => dragSpeed;
    public float RopeShake => ropeShake;
    public float RopeJolt => ropeJolt;
    public float MountReach => mountReach;
    public float SureFootedMetres => sureFootedMetres;
    public float SwimmerMetres => swimmerMetres;
    public float EnduringMetres => enduringMetres;
    public int BraveHits => braveHits;
    public float SteepClimb => steepClimb;
    public float JumpHeight => jumpHeight;
    public Vector2 JumpLength => jumpLength;
    public float FollowDistance => followDistance;
    public float LeadReach => leadReach;

    /// <summary>How fast a horse goes over a ground (a terrain layer's name), 1 for one not listed.</summary>
    public float GroundSpeedOf(string ground, out bool soft)
    {
        soft = false;
        if (!string.IsNullOrEmpty(ground))
        {
            foreach (GroundSpeed kind in grounds)
            {
                if (!string.IsNullOrEmpty(kind.word) && ground.IndexOf(kind.word, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    soft = kind.soft;
                    return kind.speed;
                }
            }
        }

        return 1f;
    }
    public float MountSeconds => mountSeconds;

    /// <summary>The rider's offset from the saddle (xyz, in their own size) and turn (w, degrees) a share of the way through
    /// getting on, from the given side. Without a measured path: a straight climb from beside the horse.</summary>
    public Vector4 MountPoint(float t, bool fromRight)
    {
        Vector4 point;
        if (mountPath == null || mountPath.Length < 2)
        {
            point = Vector4.Lerp(new Vector4(-0.55f, -0.66f, -0.1f, 90f), Vector4.zero, Mathf.SmoothStep(0f, 1f, t));
        }
        else
        {
            float at = Mathf.Clamp01(t) * (mountPath.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(at), mountPath.Length - 2);
            point = Vector4.Lerp(mountPath[i], mountPath[i + 1], at - i);
        }

        return fromRight ? new Vector4(-point.x, point.y, point.z, -point.w) : point;
    }
    public Vector2 BuckSeconds => buckSeconds;
    public float BuckBalance => buckBalance;
    public float SteadyPerSecond => steadyPerSecond;
    public float StableHealPerHour => stableHealPerHour;
    public float SwimSpeed => swimSpeed;
    public float SwimCost => swimCost;
    public float SwimDepth => swimDepth;
    public SoundDefinition Whistle => whistle;
    public Vector2 WhistleReach => whistleReach;
    public float WhistleBond => whistleBond;
    public float BondPerFeed => bondPerFeed;
    public float BondPerGroom => bondPerGroom;
    public float BondPerKilometre => bondPerKilometre;
    public float BondLostPerIdleDay => bondLostPerIdleDay;
    public float BondWhenTamed => bondWhenTamed;
    public float StaminaAtFullBond => staminaAtFullBond;
    public float CalmerAtFullBond => calmerAtFullBond;
    public float ThrowsBelowBond => throwsBelowBond;
    public float BreedingBond => breedingBond;
    public float DaysToFoal => daysToFoal;
    public float DaysBetweenFoals => daysBetweenFoals;
    public float DaysToGrow => daysToGrow;
    public float FoalSize => foalSize;
    public float FoalSpread => foalSpread;

    public float ExperienceFor(HorseWork work)
    {
        switch (work)
        {
            case HorseWork.Tame: return experiencePerTame;
            case HorseWork.Feed: return experiencePerFeed;
            case HorseWork.Groom: return experiencePerGroom;
            case HorseWork.Kilometre: return experiencePerKilometre;
            case HorseWork.MountedHit: return experiencePerMountedHit;
            case HorseWork.Foal: return experiencePerFoal;
            default: return 0f;
        }
    }

    /// <summary>The kind a horse's definition belongs to, by its id (asset name), for one brought back from a save or a raid.</summary>
    public HorseDefinition KindNamed(string name)
    {
        foreach (HorseDefinition kind in AllKinds())
        {
            if (kind.name == name)
            {
                return kind;
            }
        }

        return null;
    }

    /// <summary>Every kind that roams: the wild horse first.</summary>
    public IEnumerable<HorseDefinition> AllKinds()
    {
        if (wildHorse != null)
        {
            yield return wildHorse;
        }

        foreach (HorseDefinition kind in specialHorses)
        {
            if (kind != null && kind != wildHorse)
            {
                yield return kind;
            }
        }
    }

    public HorseTemperamentRules Temperament(HorseTemperament temperament)
    {
        foreach (HorseTemperamentRules rules in temperaments)
        {
            if (rules.temperament == temperament)
            {
                return rules;
            }
        }

        return HorseTemperamentRules.Neutral(temperament);
    }

    public HorseTemperament RollTemperament(System.Random random)
    {
        float total = 0f;
        foreach (HorseTemperamentRules rules in temperaments)
        {
            total += rules.weight;
        }

        if (total <= 0f)
        {
            return HorseTemperament.Calm;
        }

        float roll = (float)random.NextDouble() * total;
        foreach (HorseTemperamentRules rules in temperaments)
        {
            roll -= rules.weight;
            if (roll <= 0f)
            {
                return rules.temperament;
            }
        }

        return temperaments[temperaments.Count - 1].temperament;
    }
}

/// <summary>A kind of ground a horse goes faster or slower over (HorseSettings › Grounds).</summary>
[System.Serializable]
public struct GroundSpeed
{
    [Tooltip("A word in the terrain layer's name (Path, Mud, Bog, Rock...).")]
    public string word;
    [Range(0.3f, 1.5f)] public float speed;
    [Tooltip("Mud and bog: a bog-strider doesn't slow on it.")]
    public bool soft;
}
