using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>How dangerous a region of the Haven's land is, for the player to judge where to go.</summary>
public enum RegionDanger
{
    Calm = 0,
    Wary = 1,
    Dangerous = 2,
    Deadly = 3,
}

/// <summary>What a held region asks of the Haven, now and then: its task (TerritoryState.TaskOf).</summary>
public enum RegionTaskKind
{
    /// <summary>Bring down creatures or game in it.</summary>
    Hunt = 0,
    /// <summary>Gather there: fell, mine or forage.</summary>
    Gather = 1,
    /// <summary>Land fish from its water.</summary>
    Fish = 2,
}

/// <summary>
/// The Haven's Reach: claiming the regions round home (WorldAtlas) at their boundary stones, holding them against the forest,
/// and what holding them gives. Every number here is a design dial; Docs/Territory.md explains them. Lives at
/// Resources/TerritorySettings, written by AgentScripts/BuildTerritory.cs.
/// </summary>
[CreateAssetMenu(fileName = "TerritorySettings", menuName = "Safe Haven/Settings/Territory")]
public sealed class TerritorySettings : GameSettings<TerritorySettings>
{
    /// <summary>What a region is like: written for the player (the Lands tab, its stone, the title on entering it).</summary>
    [Serializable]
    public sealed class RegionInfo
    {
        [Tooltip("The region's name in the atlas.")]
        public string region;
        [TextArea(2, 4)] public string description;
        public RegionDanger danger = RegionDanger.Wary;
        [Tooltip("What can be found there, in a few words.")]
        public string gives;
        public RegionTaskKind task = RegionTaskKind.Hunt;
        [Min(1)] public int taskCount = 6;
        [Tooltip("What it sends home each dawn while it is Secure.")]
        public List<ItemAmount> tithe = new List<ItemAmount>();
    }

    [Header("Claiming")]
    [Tooltip("The boundary stones, spawned where the atlas says (TerritorySpawner).")]
    [SerializeField] private GameObject stonePrefab;
    [Tooltip("Offered at a stone to claim its region (after the first) or to strengthen the hold.")]
    [SerializeField] private ItemDefinition pactRune;
    [Tooltip("The first stone lit needs no rune: the hearth's own.")]
    [SerializeField] private bool firstClaimFree = true;
    [Tooltip("Places in a region that must be found before its stone answers.")]
    [SerializeField, Min(0)] private int placesToClaim = 1;
    [Tooltip("Hold a region starts at when claimed.")]
    [SerializeField, Range(1f, 100f)] private float claimHold = 40f;
    [Tooltip("Hold a Pact Rune adds.")]
    [SerializeField, Range(0f, 100f)] private float runeHold = 25f;
    [Tooltip("Hold finding one of its places adds, while it is claimed.")]
    [SerializeField, Range(0f, 50f)] private float findHold = 5f;
    [Tooltip("Below this a held region is Slipping.")]
    [SerializeField, Range(0f, 100f)] private float slippingBelow = 25f;
    [Tooltip("From here a held region is Secure.")]
    [SerializeField, Range(0f, 100f)] private float secureFrom = 70f;

    [Header("Erosion (each turn: a dawn at home, or a raid ended), only under blight")]
    [Tooltip("What a turn wears off a held region as soon as the woods round home are blighted at all (at a quarter of " +
             "the blight it is all there). Without blight a hold stands still (design review 8.14, 2026-11-14: the home " +
             "mustn't become upkeep that punishes going out).")]
    [SerializeField, Min(0f)] private float erosion = 1f;
    [Tooltip("More erosion at full blight.")]
    [SerializeField, Min(0f)] private float erosionPerBlight = 4f;

    [Header("The forest pushes back")]
    [Tooltip("Turns after the first claim before the forest pushes at all.")]
    [SerializeField, Min(0)] private int firstPushAfter = 3;
    [Tooltip("Turns after a push ends before the next can come.")]
    [SerializeField, Min(0)] private int restAfterPush = 3;
    [SerializeField, Range(0f, 1f)] private float pushChance = 0.2f;
    [SerializeField, Range(0f, 1f)] private float pushChancePerBlight = 0.4f;
    [SerializeField, Range(0f, 1f)] private float mostPushChance = 0.6f;
    [Tooltip("Turns from the warning to the push.")]
    [SerializeField, Min(1)] private int warningTurns = 2;
    [Tooltip("Hold a push takes if nothing is done.")]
    [SerializeField, Min(0f)] private float pushStrength = 30f;
    [SerializeField, Min(0f)] private float pushStrengthPerBlight = 25f;
    [Tooltip("A push on a Secure region is this much of the rest.")]
    [SerializeField, Range(0f, 1f)] private float secureShare = 0.6f;
    [Tooltip("Share of the push each rot's heart burned on a raid takes off it (two at a half stop it).")]
    [SerializeField, Range(0f, 1f)] private float heartShare = 0.5f;
    [Tooltip("Blight spread when the forest takes a region back.")]
    [SerializeField, Range(0f, 1f)] private float lostBlight = 0.05f;

    [Header("Standing watch at a stone")]
    [Tooltip("What comes for the stone; empty uses the corruption's (CorruptionSettings).")]
    [SerializeField] private List<GameObject> defenceCreatures = new List<GameObject>();
    [SerializeField, Min(1)] private int defenceCount = 3;
    [Tooltip("More creatures for each step of the region's danger.")]
    [SerializeField, Min(0)] private int defencePerDanger = 1;
    [SerializeField] private Vector2 defenceDistance = new Vector2(16f, 26f);
    [Tooltip("Metres from the stone the player must stay within.")]
    [SerializeField, Min(5f)] private float defenceLeash = 40f;
    [Tooltip("Seconds away before the watch is lost.")]
    [SerializeField, Min(1f)] private float defenceAway = 12f;
    [SerializeField, Min(10f)] private float defenceSeconds = 240f;
    [Tooltip("Hold a held watch adds.")]
    [SerializeField, Range(0f, 100f)] private float defenceHold = 15f;
    [SerializeField, Min(0)] private int defenceInsight = 60;

    [Header("Tasks")]
    [SerializeField, Range(0f, 100f)] private float taskHold = 10f;
    [SerializeField, Min(0)] private int taskInsight = 60;
    [Tooltip("Turns after a task is done before the region asks again.")]
    [SerializeField, Min(0)] private int taskRest = 1;

    [Header("What holding gives")]
    [SerializeField, Min(0)] private int heldRenown = 6;
    [SerializeField, Min(0)] private int secureRenown = 12;
    [Tooltip("Insight for each named place found.")]
    [SerializeField, Min(0)] private int findInsight = 25;

    [Header("The story (the portal's word after the third raid: Docs/Territory.md)")]
    [Tooltip("The quest that opens the stones: until it starts, every stone is cold. Empty: open from the start.")]
    [SerializeField] private QuestDefinition unlockedBy;
    [Tooltip("Optional: the stones stay cold until this has happened too (the portal has told of them), not only once the quest starts.")]
    [SerializeField] private GameEventDefinition unlockedWhen;
    [Tooltip("The objective of that quest (1 is the first) the unlit stones show the way for, as quest waypoints. 0: none.")]
    [SerializeField, Min(0)] private int stonesObjective = 2;
    [Tooltip("Raised when a boundary stone is lit: a region claimed.")]
    [SerializeField, RaisedByGame] private GameEventDefinition stoneLit;
    [Tooltip("Raised when a held region's task is done.")]
    [SerializeField, RaisedByGame] private GameEventDefinition taskDone;
    [Tooltip("Raised when a Pact Rune is offered at a lit stone.")]
    [SerializeField, RaisedByGame] private GameEventDefinition runeOffered;
    [Tooltip("Raised when the forest gathers against a held region (the warning).")]
    [SerializeField, RaisedByGame] private GameEventDefinition forestGathers;
    [Tooltip("Raised when a push is answered: a watch held, or a rot's heart burned.")]
    [SerializeField, RaisedByGame] private GameEventDefinition pushAnswered;
    [Tooltip("Raised when a held region becomes Secure.")]
    [SerializeField, RaisedByGame] private GameEventDefinition landSecure;

    [Header("Regions")]
    [SerializeField] private List<RegionInfo> regions = new List<RegionInfo>();

    public QuestDefinition UnlockedBy => unlockedBy;
    public GameEventDefinition UnlockedWhen => unlockedWhen;
    public int StonesObjective => stonesObjective;
    public GameEventDefinition StoneLit => stoneLit;
    public GameEventDefinition TaskDone => taskDone;
    public GameEventDefinition RuneOffered => runeOffered;
    public GameEventDefinition ForestGathers => forestGathers;
    public GameEventDefinition PushAnswered => pushAnswered;
    public GameEventDefinition LandSecure => landSecure;
    public GameObject StonePrefab => stonePrefab;
    public ItemDefinition PactRune => pactRune;
    public bool FirstClaimFree => firstClaimFree;
    public int PlacesToClaim => placesToClaim;
    public float ClaimHold => claimHold;
    public float RuneHold => runeHold;
    public float FindHold => findHold;
    public float SlippingBelow => slippingBelow;
    public float SecureFrom => secureFrom;
    public float ErosionAt(float blight) => blight <= 0f ? 0f
        : erosion * Mathf.Clamp01(blight * 4f) + erosionPerBlight * Mathf.Clamp01(blight);
    public int FirstPushAfter => firstPushAfter;
    public int RestAfterPush => restAfterPush;
    public float PushChanceAt(float blight) => Mathf.Min(mostPushChance, pushChance + pushChancePerBlight * Mathf.Clamp01(blight));
    public int WarningTurns => warningTurns;
    public float PushStrengthAt(float blight) => pushStrength + pushStrengthPerBlight * Mathf.Clamp01(blight);
    public float SecureShare => secureShare;
    public float HeartShare => heartShare;
    public float LostBlight => lostBlight;
    public IReadOnlyList<GameObject> DefenceCreatures => defenceCreatures.Count > 0 ? defenceCreatures : CorruptionSettings.Current.Creatures;
    public int DefenceCountFor(RegionDanger danger) => defenceCount + defencePerDanger * (int)danger;
    public Vector2 DefenceDistance => defenceDistance;
    public float DefenceLeash => defenceLeash;
    public float DefenceAway => defenceAway;
    public float DefenceSeconds => defenceSeconds;
    public float DefenceHold => defenceHold;
    public int DefenceInsight => defenceInsight;
    public float TaskHold => taskHold;
    public int TaskInsight => taskInsight;
    public int TaskRest => taskRest;
    public int HeldRenown => heldRenown;
    public int SecureRenown => secureRenown;
    public int FindInsight => findInsight;
    public IReadOnlyList<RegionInfo> Regions => regions;

    /// <summary>What is written of a region, or null for one not described (the Haven's own meadows).</summary>
    public RegionInfo Info(string region)
    {
        foreach (RegionInfo info in regions)
        {
            if (info != null && info.region == region)
            {
                return info;
            }
        }

        return null;
    }

    /// <summary>Sets the regions and the parts the builder makes (BuildTerritory).</summary>
    public void Configure(GameObject stone, ItemDefinition rune, List<RegionInfo> described)
    {
        stonePrefab = stone;
        pactRune = rune;
        regions = described;
    }
}
