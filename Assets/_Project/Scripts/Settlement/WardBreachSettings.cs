using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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

    [Header("Numbers and mood (the user, 2026-11-22: breaches felt underwhelming)")]
    [Tooltip("Each wave fields its roster times this (the roster comes round again to fill it).")]
    [SerializeField, Min(0.5f)] private float enemyCountScale = 1.6f;
    [Tooltip("And this much more for each of the Haven's people who fight (0.12 = +12% each): everyone turns out now (DefenceSettings).")]
    [SerializeField, Min(0f)] private float enemiesPerDefender = 0.12f;
    [Tooltip("The most one wave fields, however it is scaled.")]
    [SerializeField, Min(1)] private int mostPerWave = 24;
    [Tooltip("The look laid over the Haven while a breach is under way (a Volume Profile): darker, colder, the edges closing in.")]
    [SerializeField] private VolumeProfile breachLook;
    [Tooltip("Seconds the look takes to come and go.")]
    [SerializeField, Min(0.1f)] private float lookFadeSeconds = 2.5f;

    public VolumeProfile BreachLook => breachLook;
    public float LookFadeSeconds => lookFadeSeconds;

    /// <summary>How many of a wave's <paramref name="roster"/> come with <paramref name="defenders"/> of the Haven's people fighting, and the
    /// breach's source weakened by <paramref name="weakening"/> (0-1). At least one, at most Most Per Wave.</summary>
    public int Fielded(int roster, int defenders, float weakening) =>
        roster <= 0 ? 0 : Mathf.Clamp(Mathf.RoundToInt(roster * (enemyCountScale + enemiesPerDefender * Mathf.Max(0, defenders)) * (1f - Mathf.Clamp01(weakening))), 1, mostPerWave);

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
