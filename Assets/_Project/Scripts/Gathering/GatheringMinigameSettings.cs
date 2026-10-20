using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How one swinging tool feels in the gathering minigame: how fast the marker runs, how wide the zones are and how loud
/// its swings are. A heavy axe is slow and loud; a sickle is quick, tight and quiet. Swings per node are set on the node.
/// </summary>
[Serializable]
public sealed class GatheringToolProfile
{
    [SerializeField] private ToolType tool = ToolType.Axe;
    [Tooltip("Seconds for the marker to go up and back down at node swing speed 1. A whole cycle without a press is a glancing blow.")]
    [SerializeField, Range(0.3f, 4f)] private float cycleSeconds = 1.4f;
    [Tooltip("Width of the yellow (normal) zone, as a share of the bar.")]
    [SerializeField, Range(0.05f, 1f)] private float normalZoneWidth = 0.35f;
    [Tooltip("Width of the critical zone with a tool of exactly the node's required tier.")]
    [SerializeField, Range(0.01f, 0.5f)] private float criticalZoneWidth = 0.08f;
    [Tooltip("Scales this tool's noise radius and the forest annoyance of each swing. 1 = as set below/on the raid.")]
    [SerializeField, Range(0f, 2f)] private float loudness = 1f;

    public GatheringToolProfile()
    {
    }

    public GatheringToolProfile(ToolType tool, float cycleSeconds, float normalZoneWidth, float criticalZoneWidth, float loudness)
    {
        this.tool = tool;
        this.cycleSeconds = cycleSeconds;
        this.normalZoneWidth = normalZoneWidth;
        this.criticalZoneWidth = criticalZoneWidth;
        this.loudness = loudness;
    }

    public ToolType Tool => tool;
    public float CycleSeconds => cycleSeconds;
    public float NormalZoneWidth => normalZoneWidth;
    public float CriticalZoneWidth => criticalZoneWidth;
    public float Loudness => loudness;
}

/// <summary>
/// Rules of the gathering timing minigame (axe, pickaxe and sickle): a marker runs up and down a bar and each press is a swing.
/// A press in the critical (centre) zone gives one extra of the node's main yield and is quiet; the yellow zone is a
/// normal swing; a miss or no press in a whole cycle is a glancing blow that wears the tool and is loud. In raids each
/// swing makes noise that wakes and draws nearby creatures (GatheringNoise) and annoys the forest (ForestAnnoyanceSystem).
/// How many swings a node takes is set per node (GatheringNodeDefinition.Swings); how each tool's bar feels is set by
/// its tool profile. Read by PlayerGathering.
/// </summary>
[CreateAssetMenu(fileName = "GatheringMinigameSettings", menuName = "Safe Haven/Settings/Gathering Minigame Settings")]
public sealed class GatheringMinigameSettings : GameSettings<GatheringMinigameSettings>
{
    [Tooltip("Turns the minigame on. Off: every node gathers by holding interact for its gather time.")]
    [SerializeField] private bool enabled = true;
    [Tooltip("Tools whose nodes use the minigame, and how each one's bar feels. Other nodes keep the timed gather.")]
    [SerializeField] private List<GatheringToolProfile> profiles = new List<GatheringToolProfile>
    {
        new GatheringToolProfile(ToolType.Axe, 1.4f, 0.35f, 0.08f, 1f),
        new GatheringToolProfile(ToolType.Pickaxe, 1.6f, 0.3f, 0.07f, 1.1f),
        new GatheringToolProfile(ToolType.Sickle, 0.7f, 0.25f, 0.07f, 0.35f),
    };

    [Header("Bar")]
    [Tooltip("Critical width added per tool tier above the node's required tier.")]
    [SerializeField, Range(0f, 0.2f)] private float criticalWidthPerTier = 0.03f;
    [Tooltip("Critical width added per critical swing in a row (the combo). Any other result resets it.")]
    [SerializeField, Range(0f, 0.2f)] private float criticalWidthPerCombo = 0.02f;
    [Tooltip("Criticals in a row that still widen the zone.")]
    [SerializeField, Range(0, 10)] private int maxCombo = 3;
    [Tooltip("How far the zones may move from the middle of the bar each swing, so it can't be done by rhythm alone.")]
    [SerializeField, Range(0f, 0.4f)] private float zoneJitter = 0.15f;

    [Header("Rewards")]
    [Tooltip("Extra of the node's main yield each critical swing gives.")]
    [SerializeField, Range(0, 5)] private int bonusPerCritical = 1;

    [Header("Critical feel")]
    [Tooltip("Seconds the game nearly freezes on a critical swing. The node itself shakes (see Harvest Feedback).")]
    [SerializeField, Range(0f, 0.3f)] private float criticalHitStop = 0.06f;

    [Header("Interruption and accessibility")]
    [Tooltip("Taking damage cancels the gather (nothing is given for it).")]
    [SerializeField] private bool interruptOnHit = true;
    [Tooltip("Accessibility: swings happen by themselves, always as normal swings (no criticals, no glancing blows).")]
    [SerializeField] private bool autoGather;
    [Tooltip("Seconds between swings with auto-gather on.")]
    [SerializeField, Range(0.2f, 3f)] private float autoSwingSeconds = 0.9f;

    [Header("Noise (raids only)")]
    [Tooltip("Metres a critical swing is heard: sleepers inside wake, idle creatures come to look.")]
    [SerializeField, Min(0f)] private float criticalNoiseRadius = 6f;
    [Tooltip("Metres a normal swing is heard.")]
    [SerializeField, Min(0f)] private float normalNoiseRadius = 14f;
    [Tooltip("Metres a glancing blow is heard.")]
    [SerializeField, Min(0f)] private float glancingNoiseRadius = 22f;

    [Header("Default feedback")]
    [Tooltip("Harvest feedback for nodes without their own profile, and the fallback for any sound or effect a node's " +
             "profile leaves empty. Also used by nodes gathered by hand.")]
    [SerializeField] private HarvestFeedbackProfile defaultFeedback;

    public bool Enabled => enabled;
    public IReadOnlyList<GatheringToolProfile> Profiles => profiles;
    public float CriticalWidthPerTier => criticalWidthPerTier;
    public float CriticalWidthPerCombo => criticalWidthPerCombo;
    public int MaxCombo => maxCombo;
    public float ZoneJitter => zoneJitter;
    public int BonusPerCritical => bonusPerCritical;
    public float CriticalHitStop => criticalHitStop;
    public bool InterruptOnHit => interruptOnHit;
    public bool AutoGather => autoGather;
    public float AutoSwingSeconds => autoSwingSeconds;
    public HarvestFeedbackProfile DefaultFeedback => defaultFeedback;

    public bool UsesMinigame(ToolType tool)
    {
        return enabled && Profile(tool) != null;
    }

    /// <summary>The tool's bar profile, or null when the tool doesn't use the minigame.</summary>
    public GatheringToolProfile Profile(ToolType tool)
    {
        if (tool == ToolType.None || profiles == null)
        {
            return null;
        }

        foreach (GatheringToolProfile profile in profiles)
        {
            if (profile != null && profile.Tool == tool)
            {
                return profile;
            }
        }

        return null;
    }

    /// <summary>The critical zone width for a tool tier on a node needing a tier (before combo).</summary>
    public float CriticalWidthFor(GatheringToolProfile profile, int toolTier, int requiredTier)
    {
        return Mathf.Min(profile.CriticalZoneWidth + criticalWidthPerTier * Mathf.Max(0, toolTier - requiredTier), profile.NormalZoneWidth);
    }

    public float NoiseRadius(SwingResult result)
    {
        switch (result)
        {
            case SwingResult.Critical: return criticalNoiseRadius;
            case SwingResult.Normal: return normalNoiseRadius;
            default: return glancingNoiseRadius;
        }
    }
}
