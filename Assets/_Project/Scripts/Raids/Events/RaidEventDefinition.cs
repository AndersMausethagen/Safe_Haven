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
    [Tooltip("What the player sees on arriving, shown above the choices.")]
    [SerializeField, TextArea(2, 5)] private string siteText;

    [Header("What happens")]
    [Tooltip("Carried out when it begins, at the site if it has one.")]
    [SerializeReference] private List<RaidEventOutcome> onStart = new List<RaidEventOutcome>();
    [Tooltip("What the player can do at the site. None: walking there is all there is (what is there rewards it).")]
    [SerializeField] private List<RaidEventChoice> choices = new List<RaidEventChoice>();

    public string Id => string.IsNullOrWhiteSpace(id) ? name : id;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public float Weight => weight;
    public string Warning => warning;
    public SoundDefinition WarningSound => warningSound;
    public GameObject SitePrefab => sitePrefab;
    public Vector2 SiteDistance => siteDistance;
    public string SiteText => siteText;
    public IReadOnlyList<RaidEventOutcome> OnStart => onStart;
    public IReadOnlyList<RaidEventChoice> Choices => choices;

    /// <summary>Whether it may begin now.</summary>
    public bool CanHappen(float elapsed, float secondsLeft, int stage)
    {
        return weight > 0f && elapsed >= earliestSeconds && secondsLeft >= latestSecondsLeft && stage >= minimumStage;
    }

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
    [Tooltip("Told to the player after choosing.")]
    [TextArea] public string result;
    [SerializeReference] public List<RaidEventOutcome> outcomes = new List<RaidEventOutcome>();
}
