using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a raid's end plays (Docs/RaidLoopResearch_2026-11-22.md and the user's answers): what staying longer is worth and what it costs.
/// <list type="bullet">
/// <item>Overtime: when the clock runs out the way home doesn't close; a portal opens at the centre of every extraction stone (step
/// through, no hold), and the player may stay. Each Mist Step Seconds the mist grips harder (Mist Step Penalties, on top of the last
/// stage's), and after Overtime Seconds the portals close: whoever is still out is taken (the user: "Instead of automatically
/// extracting when the timer goes out, a portal... activates when the time runs out... we can choose to stay").</item>
/// <item>Veins: a stage with Veins sprouts a cluster of its tier's ore (Vein Node By Tier) away from the exits, guarded, marked gold
/// on the map: moved from the start layout, not added (the user's answer 3), and the events "that sprout up new nodes with monsters
/// guarding" the user asked for in place of nodes regrowing in a raid.</item>
/// <item>Time away: parties, outposts and homecoming gifts pay less for a quick raid, rising to the full yield at Full From Seconds
/// (the user's answer 4: "reduce reward for quick raids. less than 4 min, then gradually increase up to 9 min").</item>
/// </list>
/// </summary>
[CreateAssetMenu(fileName = "RaidPacingSettings", menuName = "Safe Haven/Settings/Raid Pacing")]
public sealed class RaidPacingSettings : GameSettings<RaidPacingSettings>
{
    [Header("Overtime")]
    [Tooltip("Seconds the portals stay open once the clock has run out. 0: the old rule (out of time is taken, unless in an open circle).")]
    [SerializeField, Min(0f)] private float overtimeSeconds = 180f;
    [Tooltip("Each this many seconds of overtime the mist grips harder (one more Mist Step).")]
    [SerializeField, Min(5f)] private float mistStepSeconds = 30f;
    [Tooltip("What each step of overtime adds to the last stage's Mist-touched (Percent: -0.08 = 8% less).")]
    [SerializeField] private List<StatBonus> mistStepPenalties = new List<StatBonus>();
    [Tooltip("The portal shown at the centre of each extraction stone in overtime (a look: the raid portal's Active Look).")]
    [SerializeField] private GameObject portalLook;
    [Tooltip("Its size at the stone, against the Haven's portal.")]
    [SerializeField, Range(0.1f, 2f)] private float portalScale = 0.8f;
    [Tooltip("Raised this far above the stone's centre, to stand on its top.")]
    [SerializeField, Range(0f, 2f)] private float portalLift = 0.25f;

    [Header("The stage's gather bonus (RaidStage › Gather Bonus)")]
    [Tooltip("What staying costs the tools: each node gathered while a stage's bonus is up wears the tool by this times the bonus (0.08 " +
             "at +30% = 2.4% a node). Glancing blows wear it as before. The user, 2026-11-22: \"mining empowered nodes can increase tear " +
             "on used equipment\".")]
    [SerializeField, Range(0f, 0.5f)] private float toolWearPerBonus = 0.08f;

    [Header("Veins (a stage's sprouting ore)")]
    [Tooltip("The ore node a stage's veins sprout, by the raid's tier (I first; the last holds beyond).")]
    [SerializeField] private List<GameObject> veinNodeByTier = new List<GameObject>();
    [SerializeField, Min(1)] private int veinsEach = 2;
    [Tooltip("How much more a vein's node gives than one in the start layout.")]
    [SerializeField, Min(1f)] private float veinYield = 1.5f;
    [Tooltip("Creatures of the raid's tier set to guard a vein (in the game that keeps the raid).")]
    [SerializeField, Min(0)] private int veinGuards = 2;

    [Header("Time away (parties, outposts, homecoming gifts)")]
    [Tooltip("A raid this short or shorter pays Least Share of what they bring.")]
    [SerializeField, Min(0f)] private float reducedBelowSeconds = 240f;
    [Tooltip("From this long they bring all of it, as before.")]
    [SerializeField, Min(1f)] private float fullFromSeconds = 540f;
    [SerializeField, Range(0f, 1f)] private float leastShare = 0.5f;

    public float OvertimeSeconds => overtimeSeconds;
    public float MistStepSeconds => mistStepSeconds;
    public IReadOnlyList<StatBonus> MistStepPenalties => mistStepPenalties;
    public GameObject PortalLook => portalLook;
    public float PortalScale => portalScale;
    public float PortalLift => portalLift;
    public int VeinsEach => veinsEach;
    public float VeinYield => veinYield;
    public int VeinGuards => veinGuards;

    /// <summary>The vein node at <paramref name="tier"/>, or null.</summary>
    public GameObject VeinNodeAt(int tier) => veinNodeByTier.Count == 0 ? null : veinNodeByTier[Mathf.Clamp(tier - 1, 0, veinNodeByTier.Count - 1)];

    /// <summary>The share of their yield parties, outposts and gifts bring for a raid of <paramref name="seconds"/>.</summary>
    public float TimeAwayShare(float seconds)
    {
        if (seconds <= reducedBelowSeconds)
        {
            return leastShare;
        }

        return Mathf.Lerp(leastShare, 1f, Mathf.InverseLerp(reducedBelowSeconds, Mathf.Max(reducedBelowSeconds + 1f, fullFromSeconds), seconds));
    }

    /// <summary>Overtime steps reached after <paramref name="overtime"/> seconds: 1 as it begins.</summary>
    public int MistStepsAt(float overtime) => overtime <= 0f ? 0 : 1 + Mathf.FloorToInt(overtime / Mathf.Max(5f, mistStepSeconds));

    /// <summary>A tool's wear for gathering one node while the stage's bonus is <paramref name="gatherBonus"/> (0.3 = +30%).</summary>
    public float ToolWearFor(float gatherBonus) => gatherBonus <= 0f ? 0f : toolWearPerBonus * gatherBonus;
}
