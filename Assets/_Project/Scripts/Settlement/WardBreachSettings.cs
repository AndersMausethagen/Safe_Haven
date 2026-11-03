using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How ward breaches follow one another (GDD §6.6–6.9): the breaches in order, how many raids lie between them, what a lost
/// breach costs and what repairs take. The last breach in the list comes back, stronger each time, once they run out.
/// </summary>
[CreateAssetMenu(fileName = "WardBreachSettings", menuName = "Safe Haven/Settings/Ward Breaches")]
public sealed class WardBreachSettings : GameSettings<WardBreachSettings>
{
    [Header("Pacing")]
    [Tooltip("The breaches in the order they come. The first is the scripted, forgiving one.")]
    [SerializeField] private List<WardBreachDefinition> breaches = new List<WardBreachDefinition>();
    [Tooltip("Successful raids between one breach being beaten back and the next warning (GDD §6.6: every three to five).")]
    [SerializeField, Min(1)] private int raidsBetween = 3;
    [Tooltip("Successful raids a warned breach can be put off. After that the ward gives way at the next nightfall at home.")]
    [SerializeField, Min(0)] private int raidsPutOff = 2;
    [Tooltip("Seconds between the ward giving way at nightfall and the first attackers arriving.")]
    [SerializeField, Min(0f)] private float nightfallWarning = 20f;
    [Tooltip("How much stronger the last breach is each time it comes back, as a share.")]
    [SerializeField, Min(0f)] private float strongerEachRepeat = 0.2f;

    [Header("The defense")]
    [Tooltip("Where attackers set out from, in metres beyond the edge of the ward.")]
    [SerializeField, Min(1f)] private float laneDistance = 16f;
    [Tooltip("The portal's ward in a fight: what the attackers wear down. It needs a Damageable that isn't destroyed on death.")]
    [SerializeField] private Damageable wardAnchor;
    [Tooltip("Share more health the ward has in a fight for each ward level.")]
    [SerializeField, Min(0f)] private float wardHealthPerLevel = 0.15f;
    [Tooltip("Metres the player can lead attackers before they give up and go back to the clearing.")]
    [SerializeField, Min(1f)] private float chaseLimit = 18f;
    [Tooltip("Seconds after which any attackers left slink away and the breach counts as held, so none can stay stuck forever.")]
    [SerializeField, Min(30f)] private float longestDefense = 300f;

    [Header("Recovery (GDD §6.9: bounded and repairable)")]
    [Tooltip("What fixing a building from broken costs, as a share of building it. Less damage costs less.")]
    [SerializeField, Range(0f, 1f)] private float repairShare = 0.3f;
    [Tooltip("Successful raids a strained ward takes to recover after a lost breach.")]
    [SerializeField, Min(0)] private int strainRaids = 2;
    [Tooltip("Metres the ward draws in while strained.")]
    [SerializeField, Min(0f)] private float strainRadius = 4f;

    public IReadOnlyList<WardBreachDefinition> Breaches => breaches;
    public int RaidsBetween => raidsBetween;
    public int RaidsPutOff => raidsPutOff;
    public float NightfallWarning => nightfallWarning;
    public float LaneDistance => laneDistance;
    public Damageable WardAnchor => wardAnchor;
    public float WardHealthPerLevel => wardHealthPerLevel;
    public float ChaseLimit => chaseLimit;
    public float LongestDefense => longestDefense;
    public float RepairShare => repairShare;
    public int StrainRaids => strainRaids;
    public float StrainRadius => strainRadius;

    /// <summary>The breach after this many have been beaten back; null when none are set up.</summary>
    public WardBreachDefinition For(int won) =>
        breaches.Count == 0 ? null : breaches[Mathf.Clamp(won, 0, breaches.Count - 1)];

    /// <summary>How much stronger the breach after this many wins is: 1 until the list runs out.</summary>
    public float Strength(int won) => 1f + Mathf.Max(0, won - (breaches.Count - 1)) * strongerEachRepeat;
}
