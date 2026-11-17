using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Lost (Resources/LostOneSettings, Systems Editor › World › The Lost): settlers the mist took and forgot so completely that nothing
/// is left of them but a hunger for the flame, and so for its Keeper. Each character has a few of them (LostOneRoster), named, each
/// growing when it kills the player and scarred when it flees, until the player lays it to rest. This holds when they come, how they
/// hunt, how they grow, what they are called and what is said of them; the rules are LostOneRules', the raid's part RaidLostOnes'.
/// Every list here has its defaults in code, so a fresh asset works; text may use {name}, {past}, {raid} and {trait}. Authored data;
/// never changes while playing.
/// </summary>
[CreateAssetMenu(fileName = "LostOneSettings", menuName = "Safe Haven/Settings/Lost One Settings")]
public sealed class LostOneSettings : GameSettings<LostOneSettings>
{
    /// <summary>One rank of a Lost One: what the journal calls it and how much stronger it is than the first.</summary>
    [Serializable]
    public sealed class Rank
    {
        [Tooltip("What the journal calls this rank.")]
        public string title = "";
        [Tooltip("Multiplies its health, on top of its traits' and the raid's own scaling.")]
        [Min(0.1f)] public float health = 1f;
        [Tooltip("Multiplies its damage, on top of its traits' and the raid's own scaling.")]
        [Min(0.1f)] public float damage = 1f;
        [Tooltip("How many traits (elite affixes) it has at this rank. Rising to a rank gives it what it lacks.")]
        [Min(0)] public int traits = 1;

        public Rank()
        {
        }

        public Rank(string title, float health, float damage, int traits)
        {
            this.title = title;
            this.health = health;
            this.damage = damage;
            this.traits = traits;
        }
    }

    /// <summary>
    /// The bodies the Lost walk in from a rank up: plain cloth at first, leather as they grow, mail at the top. A Lost One keeps the same
    /// place in a grade's list (from its id), so it keeps its face as its armour changes.
    /// </summary>
    [Serializable]
    public sealed class BodyGrade
    {
        [Tooltip("For the designer: what this grade is.")]
        public string name = "";
        [Tooltip("The lowest rank that walks in this grade. The highest grade a rank reaches that has bodies is used.")]
        [Range(1, Tiers.Max)] public int fromRank = 1;
        [Tooltip("Its bodies (Data/Monsters: Lost Cloth, Lost Leather, Lost Mail): each a creature with its prefab, weapon and moves.")]
        public List<MonsterDefinition> bodies = new List<MonsterDefinition>();

        public BodyGrade()
        {
        }

        public BodyGrade(string name, int fromRank)
        {
            this.name = name;
            this.fromRank = fromRank;
        }
    }

    [Header("When they come")]
    [Tooltip("Off: none walks a raid and none rises.")]
    [SerializeField] private bool enabled = true;
    [Tooltip("They rise only once this is met: The Weakening Ward, completed. Empty: no story gate.")]
    [SerializeField] private QuestCondition storyGate;
    [Tooltip("...and once the player has come home from this many raids (the Haven's successful extractions).")]
    [SerializeField, Min(0)] private int minimumExtractions = 3;
    [Tooltip("How many walk the mist at once, at most. New ones rise at a raid's end (a homecoming) to fill the roster.")]
    [SerializeField, Range(1, 8)] private int rosterSize = 3;
    [Tooltip("Raids' ends after one is laid to rest before another rises in its place. 2: a whole homecoming between.")]
    [SerializeField, Min(1)] private int homecomingsBeforeANewOne = 2;
    [Tooltip("Chance that one walks a raid, by the raid's tier (I, II, ...). The last counts for every tier above it.")]
    [SerializeField] private List<float> chanceByTier = new List<float> { 0.2f, 0.25f, 0.3f, 0.35f };
    [Tooltip("Added to that chance for each rank above the first of the one chosen: what has grown hunts more often.")]
    [SerializeField, Range(0f, 0.5f)] private float chancePerRank = 0.05f;
    [Tooltip("The chance is never more than this.")]
    [SerializeField, Range(0f, 1f)] private float mostChance = 0.6f;
    [Tooltip("How much likelier a higher rank is to be the one that walks: weight 1 + this × (rank − 1).")]
    [SerializeField, Min(0f)] private float rankWeight = 0.5f;

    [Header("Bodies")]
    [Tooltip("What it walks in while its rank's grade has no bodies: the Draug (Data/Monsters/Draug).")]
    [SerializeField] private MonsterDefinition fallbackBody;
    [Tooltip("Bodies by rank: cloth, leather, mail. Empty lists fall back to a lower grade's, then to the fallback body.")]
    [SerializeField] private List<BodyGrade> bodyGrades = new List<BodyGrade>
    {
        new BodyGrade("Cloth", 1),
        new BodyGrade("Leather", 3),
        new BodyGrade("Mail", 5),
    };

    [Header("In the raid")]
    [Tooltip("Nearest it appears to where the player arrives, in metres (on the NavMesh, out of sight, with a way to the player).")]
    [SerializeField, Min(10f)] private float nearestToStart = 60f;
    [Tooltip("Furthest it appears from where the player arrives.")]
    [SerializeField, Min(10f)] private float furthestFromStart = 120f;
    [Tooltip("Seconds into the raid before it catches the player's scent, least and most: then it is said, and it starts after them.")]
    [SerializeField] private Vector2 scentAfter = new Vector2(40f, 90f);
    [Tooltip("Seconds between its turns towards where the player is, while it isn't already on them.")]
    [SerializeField, Min(1f)] private float steerEvery = 8f;
    [Tooltip("Metres off the player's spot it aims each turn: it follows a scent, not a sight.")]
    [SerializeField, Min(0f)] private float scentError = 14f;
    [Tooltip("Its pace while it follows the scent, as a share of its own (patrols walk, hunts run).")]
    [SerializeField, Range(0.2f, 1.5f)] private float huntPace = 0.85f;
    [Tooltip("Share of its health under which it flees into the mist, once a raid, while below the highest rank. 0: never.")]
    [SerializeField, Range(0f, 0.9f)] private float fleeBelow = 0.3f;
    [Tooltip("Seconds it runs before the mist takes it. It can't be hurt meanwhile.")]
    [SerializeField, Min(0.5f)] private float fleeSeconds = 3f;
    [Tooltip("How far away from the player it runs, in metres.")]
    [SerializeField, Min(5f)] private float fleeDistance = 25f;
    [Tooltip("Left where it vanishes into the mist (should end itself). Optional.")]
    [SerializeField] private GameObject vanishEffect;
    [Tooltip("Heard where it vanishes. Optional.")]
    [SerializeField] private SoundDefinition vanishSound;
    [Tooltip("Heard by the player when it catches their scent. Optional.")]
    [SerializeField] private SoundDefinition scentSound;
    [Tooltip("Its name on the label over it.")]
    [SerializeField] private Color labelColour = new Color(0.78f, 0.86f, 0.95f);

    [Header("Growing")]
    [Tooltip("Rank 1 first. Killing the player raises it a rank, to the last.")]
    [SerializeField] private List<Rank> ranks = new List<Rank>
    {
        new Rank("Newly Lost", 1.2f, 1.1f, 1),
        new Rank("Hungering", 1.3f, 1.15f, 2),
        new Rank("Hunting", 1.4f, 1.2f, 3),
        new Rank("Dreaded", 1.5f, 1.25f, 4),
        new Rank("Bane of the Flame", 1.6f, 1.3f, 5),
    };
    [Tooltip("What it can have: elite affixes (Data/Raids/Elites), picked by weight from those usable at its rank as a tier.")]
    [SerializeField] private List<EliteAffixDefinition> traits = new List<EliteAffixDefinition>();
    [Tooltip("Health share each escape adds when it comes back (its scars).")]
    [SerializeField, Range(0f, 1f)] private float scarPerEscape = 0.1f;
    [Tooltip("The most health its scars add, all told.")]
    [SerializeField, Range(0f, 2f)] private float mostScar = 0.5f;
    [Tooltip("How many stacks of what it took from the player it keeps: the most valuable. It drops them when laid to rest.")]
    [SerializeField, Range(0, 16)] private int carriedCap = 4;

    [Header("Laid to rest (on top of its elite loot)")]
    [Tooltip("Cursed Essence for laying one to rest, at rank 1.")]
    [SerializeField, Min(0)] private int essenceAtRest = 10;
    [Tooltip("More Cursed Essence for each rank above the first.")]
    [SerializeField, Min(0)] private int essencePerRank = 5;
    [Tooltip("Insight for laying one to rest, at rank 1 (scaled as every Insight reward is).")]
    [SerializeField, Min(0)] private int insightAtRest = 60;
    [Tooltip("More Insight for each rank above the first.")]
    [SerializeField, Min(0)] private int insightPerRank = 30;

    [Header("Names")]
    [Tooltip("Given names, Old Norse in feel. A new one takes a name no other of the Lost has had, while one is left.")]
    [SerializeField] private List<string> givenNames = new List<string>
    {
        "Halvard", "Ketil", "Ragnhild", "Torgeir", "Aslaug", "Gudrun", "Eyvind", "Solveig", "Ulf", "Hallgerd", "Orm", "Thora",
        "Grim", "Borghild", "Arnulf", "Helga", "Steinar", "Vemund", "Gyda", "Kolbein", "Svanhild", "Hakon", "Dagny", "Ottar",
    };
    [Tooltip("What the mist made of them, after the name: Halvard Hollow-Eyed.")]
    [SerializeField] private List<string> epithets = new List<string>
    {
        "Hollow-Eyed", "the Unburied", "Ash-Handed", "Rime-Bearded", "Grey-Mouthed", "Long-Fingered", "the Cold", "Barrow-Walker",
        "Bone-Thin", "Black-Tongued", "the Weeping", "Ember-Hungry", "Moss-Skinned", "Crook-Backed", "the Unremembered", "Wolf-Fed",
    };
    [Tooltip("Who they were before the mist, as \"once ...\": it reads after the name (\"Halvard Hollow-Eyed, once the Haven's smith\").")]
    [SerializeField] private List<string> pastLives = new List<string>
    {
        "once the Haven's smith", "once a charcoal-burner on the ridge", "once the miller at the stream", "once a fisher of the black meres",
        "once the one who kept the hearth", "once the herder at the high seter", "once a weaver who sang at the loom", "once the village midwife",
        "once a hunter who never missed", "once the one who rang the dusk bell", "once a tanner by the bog", "once a woodcutter who went out for one more load",
        "once the cooper", "once the beekeeper", "once a carver of the waystones", "once the brewer of the Haven's ale", "once the watch on the north path",
    };

    [Header("Words ({name}, {past}, {raid}, {trait})")]
    [Tooltip("When it catches the player's scent, a while into the raid.")]
    [SerializeField] private List<string> scentLines = new List<string>
    {
        "{name} has caught your scent.",
        "Somewhere in the mist, {name} has caught your scent.",
        "The mist stirs. {name} has caught your scent.",
    };
    [Tooltip("When it first sets on the player in a raid.")]
    [SerializeField] private List<string> sightLines = new List<string>
    {
        "{name}, {past}, has found you. It has not forgotten the flame you carry.",
        "{name} knows you, Keeper of the Flame.",
        "It was {past}. Now it is {name}, and it wants the flame.",
    };
    [Tooltip("When it kills the player.")]
    [SerializeField] private List<string> killLines = new List<string>
    {
        "{name} stands over the Keeper and takes what you carried.",
        "{name} has what you carried now. It will be stronger for it.",
    };
    [Tooltip("When it flees into the mist.")]
    [SerializeField] private List<string> fleeLines = new List<string>
    {
        "{name} draws back into the mist. It will remember this.",
        "{name} melts into the mist, wounded. It will be back.",
    };
    [Tooltip("When it is laid to rest.")]
    [SerializeField] private List<string> restLines = new List<string>
    {
        "{name} is laid to rest. It was {past}, and now it is quiet.",
        "{name} falls and stays down. The mist has let it go.",
    };
    [Tooltip("When the player learns one of its traits as it flees.")]
    [SerializeField] private string learnedLine = "You have seen through {name}: it is {trait}.";
    [Tooltip("In the briefing, when one the player has met walks the raid.")]
    [SerializeField] private string briefingLine = "The mist is restless: {name} walks {raid}.";
    [Tooltip("Added to the briefing line when it carries what it took from the player.")]
    [SerializeField] private string briefingCarriesLine = "It still carries what it took from you.";
    [Tooltip("In the journal, under its name.")]
    [SerializeField, TextArea] private string journalIntro = "It was {past}. The mist took the rest of it, and left only a hunger for the flame you keep.";
    [Tooltip("What the journal says of it once it is laid to rest.")]
    [SerializeField] private string epitaph = "Laid to rest in {raid}. It was {past}, and now it is quiet.";

    private const string NamelessName = "One of the Lost";

    public bool Enabled => enabled;
    public QuestCondition StoryGate => storyGate;
    public int MinimumExtractions => minimumExtractions;
    public int RosterSize => Mathf.Max(1, rosterSize);
    public int HomecomingsBeforeANewOne => Mathf.Max(1, homecomingsBeforeANewOne);
    public float ChancePerRank => chancePerRank;
    public float MostChance => mostChance;
    public float RankWeight => rankWeight;
    public MonsterDefinition FallbackBody => fallbackBody;
    public IReadOnlyList<BodyGrade> BodyGrades => bodyGrades;
    public float NearestToStart => nearestToStart;
    public float FurthestFromStart => Mathf.Max(nearestToStart + 5f, furthestFromStart);
    public Vector2 ScentAfter => scentAfter;
    public float SteerEvery => steerEvery;
    public float ScentError => scentError;
    public float HuntPace => huntPace;
    public float FleeBelow => fleeBelow;
    public float FleeSeconds => fleeSeconds;
    public float FleeDistance => fleeDistance;
    public GameObject VanishEffect => vanishEffect;
    public SoundDefinition VanishSound => vanishSound;
    public SoundDefinition ScentSound => scentSound;
    public Color LabelColour => labelColour;
    public IReadOnlyList<Rank> Ranks => ranks;
    public IReadOnlyList<EliteAffixDefinition> Traits => traits;
    public float ScarPerEscape => scarPerEscape;
    public float MostScar => mostScar;
    public int CarriedCap => carriedCap;
    public int EssenceAtRest => essenceAtRest;
    public int EssencePerRank => essencePerRank;
    public int InsightAtRest => insightAtRest;
    public int InsightPerRank => insightPerRank;
    public IReadOnlyList<string> GivenNames => givenNames;
    public IReadOnlyList<string> Epithets => epithets;
    public IReadOnlyList<string> PastLives => pastLives;
    public IReadOnlyList<string> ScentLines => scentLines;
    public IReadOnlyList<string> SightLines => sightLines;
    public IReadOnlyList<string> KillLines => killLines;
    public IReadOnlyList<string> FleeLines => fleeLines;
    public IReadOnlyList<string> RestLines => restLines;
    public string LearnedLine => learnedLine;
    public string BriefingLine => briefingLine;
    public string BriefingCarriesLine => briefingCarriesLine;
    public string JournalIntro => journalIntro;
    public string Epitaph => epitaph;
    public string Nameless => NamelessName;

    /// <summary>The highest rank there is (the rank table's length, at least 1).</summary>
    public int MaxRank => Mathf.Max(1, ranks.Count);

    /// <summary>A rank's row, clamped to the table; an empty table counts every rank as plain.</summary>
    public Rank RankAt(int rank) => ranks.Count == 0 ? new Rank("", 1f, 1f, 1) : ranks[Mathf.Clamp(rank, 1, ranks.Count) - 1] ?? new Rank("", 1f, 1f, 1);

    /// <summary>The chance one walks a raid of this tier, the one chosen being of this rank.</summary>
    public float ChanceFor(int tier, int rank)
    {
        float baseChance = chanceByTier.Count == 0 ? 0f : chanceByTier[Mathf.Clamp(tier, 1, chanceByTier.Count) - 1];
        return Mathf.Clamp(baseChance + chancePerRank * (Mathf.Clamp(rank, 1, MaxRank) - 1), 0f, mostChance);
    }

    /// <summary>A trait by its asset's name (as records keep them), or null when it is no longer listed.</summary>
    public EliteAffixDefinition FindTrait(string traitName)
    {
        foreach (EliteAffixDefinition trait in traits)
        {
            if (trait != null && trait.name == traitName)
            {
                return trait;
            }
        }

        return null;
    }

    /// <summary>A record's traits as affixes, in its order; those no longer listed are left out.</summary>
    public List<EliteAffixDefinition> TraitsOf(LostOneRecord record)
    {
        var found = new List<EliteAffixDefinition>();
        foreach (string traitName in record.Traits)
        {
            EliteAffixDefinition trait = FindTrait(traitName);
            if (trait != null)
            {
                found.Add(trait);
            }
        }

        return found;
    }

    /// <summary>True when some body can be walked in: without one, none walks and the briefing says nothing.</summary>
    public bool HasABody
    {
        get
        {
            if (fallbackBody != null && fallbackBody.Prefab != null)
            {
                return true;
            }

            foreach (BodyGrade grade in bodyGrades)
            {
                if (grade != null && grade.bodies.Exists(body => body != null && body.Prefab != null))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The body a Lost One walks in at its rank: from the highest grade its rank reaches that has bodies, always the same one of that list
    /// for the same Lost One (from its id), else the fallback. Null when there is none at all.
    /// </summary>
    public MonsterDefinition BodyFor(LostOneRecord record)
    {
        BodyGrade best = null;
        foreach (BodyGrade grade in bodyGrades)
        {
            if (grade != null && grade.fromRank <= record.Rank && grade.bodies.Exists(body => body != null && body.Prefab != null)
                && (best == null || grade.fromRank >= best.fromRank))
            {
                best = grade;
            }
        }

        if (best == null)
        {
            return fallbackBody != null && fallbackBody.Prefab != null ? fallbackBody : null;
        }

        List<MonsterDefinition> usable = best.bodies.FindAll(body => body != null && body.Prefab != null);
        return usable[RaidLayout.SeedFor(0, "Body:" + record.Id) % usable.Count];
    }
}
