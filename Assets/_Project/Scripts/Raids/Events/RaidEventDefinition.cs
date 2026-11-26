using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One thing that can happen during a raid (GDD §9.3): a warning the player gets, a place it happens at (a site prefab
/// with RaidEventSite, away from the player) or nothing but what it does at once (a horn in the forest), and the choices
/// offered there, each with a price and outcomes the player can understand. A raid's Raid Events system draws from a pool
/// of these. To add one: a new asset in the Content Editor (Raids › Raid Events) and a line in the pool; new kinds of
/// outcome are a new RaidEventOutcome class. Never changes while playing.
/// </summary>
[CreateAssetMenu(fileName = "RaidEvent", menuName = "Safe Haven/Raids/Raid Event")]
public sealed class RaidEventDefinition : ScriptableObject, IDisplayName
{
    [Header("Identity")]
    [Tooltip("Stable unique identifier.")]
    [SerializeField] private string id;
    [Tooltip("Shown on the HUD, in menus and in the raid report.")]
    [SerializeField] private string displayName;

    [Header("When")]
    [Tooltip("Relative chance against the other events the raid could pick now.")]
    [SerializeField, Min(0f)] private float weight = 1f;
    [Tooltip("Not before this many seconds into the raid.")]
    [SerializeField, Min(0f)] private float earliestSeconds = 30f;
    [Tooltip("Not with less than this many seconds left: the player must have time to reach it.")]
    [SerializeField, Min(0f)] private float latestSecondsLeft = 60f;
    [Tooltip("Not before this danger stage: 0 is any time, 1 from the raid's first stage on.")]
    [SerializeField, Min(0)] private int minimumStage;

    [Header("Warning")]
    [Tooltip("Told to the player when it begins: a sound, a sight, a rumour. It should say what is at stake, not how it ends.")]
    [SerializeField, TextArea] private string warning;
    [SerializeField] private SoundDefinition warningSound;

    [Header("Place")]
    [Tooltip("What appears in the world, with RaidEventSite on its root. Empty: it happens around the player at once.")]
    [SerializeField] private GameObject sitePrefab;
    [Tooltip("How far from the player the site appears, out of sight.")]
    [SerializeField] private Vector2 siteDistance = new Vector2(35f, 70f);
    [Tooltip("The site stands on a shore, dry but with water a few steps off (a ferryman's raft). Event rooms are skipped for it.")]
    [SerializeField] private bool nearWater;
    [Tooltip("What the player sees on arriving, shown above the choices.")]
    [SerializeField, TextArea(2, 5)] private string siteText;
    [Tooltip("A painting of the scene, above its words and choices (LocalSpawn: Tools/LocalSpawn/event_art.json). Optional.")]
    [SerializeField] private Sprite artwork;
    [Tooltip("What is at stake, told with the warning (Docs/RaidEventsPlan_2026-11-26.md E1), so the walk is a choice: 0 none, 1 some, 2 much.")]
    [SerializeField] private EventStakes stakes;

    [Header("What happens")]
    [Tooltip("Carried out when it begins, at the site if it has one.")]
    [SerializeReference] private List<RaidEventOutcome> onStart = new List<RaidEventOutcome>();
    [Tooltip("What the player can do at the site. None: walking there is all there is (what is there rewards it).")]
    [SerializeField] private List<RaidEventChoice> choices = new List<RaidEventChoice>();
    [Header("What it remembers (Docs/RaidEventsPlan_2026-11-26.md E4-E6)")]
    [Tooltip("Comes only once the journal holds this mark (a choice made before, Set Mark): a follow-up. Empty: always.")]
    [SerializeField] private string needsMark = "";
    [Tooltip("Never comes once the journal holds this mark (the follow-up already met).")]
    [SerializeField] private string blockedByMark = "";
    [Tooltip("Once in a save, ever: a legend. Marked in the journal as it begins.")]
    [SerializeField] private bool oncePerSave;
    [Tooltip("More likely while the player is in these states: a full bag, an angry forest, a fall last raid, the night.")]
    [SerializeField] private List<EventStateWeight> moreLikelyWhen = new List<EventStateWeight>();
    [Tooltip("More likely once the journal holds these marks (the bell told where the Lindorm sleeps), without needing them.")]
    [SerializeField] private List<EventMarkWeight> moreLikelyWithMark = new List<EventMarkWeight>();
    [Tooltip("A legend's page in the journal's Strange Things, once met (Once Per Save). Empty: the site's words.")]
    [SerializeField, TextArea(3, 8)] private string legendPage = "";
    [Tooltip("A Story Event raised when any choice is taken here: how a quest counts what was done at the site, whichever way it went " +
             "(Act II's set pieces: the door in the snow, the wrong door at the Black Pool). Empty: none.")]
    [RaisedByGame, SerializeField] private GameEventDefinition raisesWhenChosen;

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public float Weight => weight;
    public string Warning => warning;
    public SoundDefinition WarningSound => warningSound;
    public GameObject SitePrefab => sitePrefab;
    public Vector2 SiteDistance => siteDistance;
    public string SiteText => siteText;
    public Sprite Artwork => artwork;
    public EventStakes Stakes => stakes;
    public IReadOnlyList<RaidEventOutcome> OnStart => onStart;
    public IReadOnlyList<RaidEventChoice> Choices => choices;
    public GameEventDefinition RaisesWhenChosen => raisesWhenChosen;
    public bool OncePerSave => oncePerSave;
    public string NeedsMark => needsMark;
    public bool NearWater => nearWater;
    /// <summary>What the journal's Strange Things says of it, once met.</summary>
    public string LegendPage => string.IsNullOrWhiteSpace(legendPage) ? siteText : legendPage;

    /// <summary>The journal mark a legend leaves once the player reaches it.</summary>
    public string OnceMark => "event:once:" + Id;

    /// <summary>
    /// True when what the save remembers lets it come: its needed mark held, its blocking one not, a legend not yet met. The needed
    /// mark is asked of <paramref name="hadAtStart"/> when given (the save as the raid began), so a follow-up comes raids later, not
    /// minutes after the choice that set its mark; the blocking and once marks are asked of <paramref name="hasMark"/> (the save now).
    /// </summary>
    public bool AllowedBy(System.Func<string, bool> hasMark, System.Func<string, bool> hadAtStart = null)
    {
        if (hasMark == null)
        {
            return string.IsNullOrEmpty(needsMark);
        }

        return (string.IsNullOrEmpty(needsMark) || (hadAtStart ?? hasMark)(needsMark))
            && (string.IsNullOrEmpty(blockedByMark) || !hasMark(blockedByMark))
            && (!oncePerSave || !hasMark(OnceMark));
    }

    /// <summary>Its weight times every state's that holds now and every mark's the save holds (1 when none).</summary>
    public float WeightIn(ICollection<RaidEventState> states, System.Func<string, bool> hasMark = null)
    {
        float scale = 1f;
        if (states != null)
        {
            foreach (EventStateWeight rule in moreLikelyWhen)
            {
                if (rule != null && states.Contains(rule.state))
                {
                    scale *= Mathf.Max(0f, rule.times);
                }
            }
        }

        if (hasMark != null)
        {
            foreach (EventMarkWeight rule in moreLikelyWithMark)
            {
                if (rule != null && !string.IsNullOrEmpty(rule.mark) && hasMark(rule.mark))
                {
                    scale *= Mathf.Max(0f, rule.times);
                }
            }
        }

        return weight * scale;
    }

    /// <summary>Whether it may begin now.</summary>
    public bool CanHappen(float elapsed, float secondsLeft, int stage)
    {
        return weight > 0f && elapsed >= earliestSeconds && secondsLeft >= latestSecondsLeft && stage >= minimumStage;
    }

    /// <summary>Whether it can still begin later in a raid with <paramref name="secondsLeft"/> to go: time and stage only
    /// grow, so only its last moment can have passed.</summary>
    public bool CanStillHappen(float secondsLeft) => weight > 0f && secondsLeft >= latestSecondsLeft;

#if UNITY_EDITOR
    /// <summary>Sets when it can happen (tests).</summary>
    public void ConfigureTiming(float chance, float earliest, float latestLeft, int stage)
    {
        weight = chance;
        earliestSeconds = earliest;
        latestSecondsLeft = latestLeft;
        minimumStage = stage;
    }
#endif
}

/// <summary>A state of the player or the run an event can answer (Docs/RaidEventsPlan_2026-11-26.md E5).</summary>
public enum RaidEventState
{
    BagMostlyFull = 0,
    ForestAngry = 1,
    FellLastRaid = 2,
    Night = 3,
}

/// <summary>How many times likelier an event is while a state holds.</summary>
[Serializable]
public sealed class EventStateWeight
{
    public RaidEventState state;
    [Min(0f)] public float times = 3f;
}

/// <summary>How many times likelier an event is once the journal holds a mark.</summary>
[Serializable]
public sealed class EventMarkWeight
{
    public string mark;
    [Min(0f)] public float times = 4f;
}

/// <summary>What an event puts at stake, each 0 (none) to 2 (much): told with its warning, in words.</summary>
[Serializable]
public struct EventStakes
{
    [Range(0, 2)] public int loot;
    [Range(0, 2)] public int danger;
    [Range(0, 2)] public int time;

    public EventStakes(int loot, int danger, int time)
    {
        this.loot = loot;
        this.danger = danger;
        this.time = time;
    }

    /// <summary>True when nothing has been set.</summary>
    public bool IsEmpty => loot == 0 && danger == 0 && time == 0;

    /// <summary>The words for the warning: "rich · risky", in the UI's colours. Pure, for tests.</summary>
    public string Words(string lootHex, string dangerHex, string timeHex)
    {
        var parts = new List<string>(3);
        if (loot > 0) parts.Add($"<color={lootHex}>{(loot >= 2 ? "rich" : "some loot")}</color>");
        if (danger > 0) parts.Add($"<color={dangerHex}>{(danger >= 2 ? "deadly" : "risky")}</color>");
        if (time > 0) parts.Add($"<color={timeHex}>{(time >= 2 ? "long" : "takes time")}</color>");
        return string.Join("  ·  ", parts);
    }
}

/// <summary>One thing the player can do at an event's site: what it says, what it costs and what comes of it.</summary>
[Serializable]
public sealed class RaidEventChoice
{
    [Tooltip("The button, in the player's words: \"Search the wagon\".")]
    public string label;
    [Tooltip("The price or the risk, plainly (GDD §9.3: \"a reward with an understandable price\").")]
    public string detail;
    [Tooltip("Items paid from the bag. Without them the choice can't be taken.")]
    public List<ItemAmount> cost = new List<ItemAmount>();
    [Tooltip("What else it needs: a skill, a thing carried, someone along, the hour (Docs/RaidEventsPlan_2026-11-26.md E2). Unmet, the " +
             "choice still shows, greyed, with the reason.")]
    [SerializeReference] public List<ChoiceRequirement> requires = new List<ChoiceRequirement>();
    [Tooltip("Not shown at all until its needs are met (a troll who knows you, a barrow you sealed): a choice the player's past opens, " +
             "not one to aim for.")]
    public bool hiddenUntilMet;

    /// <summary>The first need the player doesn't meet, or null when every one is met.</summary>
    public ChoiceRequirement Unmet(GameObject player, RaidSession session)
    {
        if (requires == null)
        {
            return null;
        }

        foreach (ChoiceRequirement requirement in requires)
        {
            if (requirement != null && !requirement.IsMet(player, session))
            {
                return requirement;
            }
        }

        return null;
    }
    [Tooltip("Told to the player after choosing.")]
    [TextArea] public string result;
    [SerializeReference] public List<RaidEventOutcome> outcomes = new List<RaidEventOutcome>();
}
