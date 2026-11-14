using UnityEngine;

/// <summary>
/// How followers (companions, and people freed in a raid) look after themselves and are looked after: the bandages they
/// carry and tear from cloth, when they use one, and how the player bandages them with a healing item from the bag
/// (WoundTending). Also how a freed traveller keeps clear of a fight (RescuableTraveler). Lives at
/// Resources/AllyCareSettings (the user, 2026-11-14: "follower NPC's should avoid enemy attacks and act smart, give them
/// the option to create and use bandages or potion, or allow us to bandage them up, or both").
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Ally Care Settings", fileName = "AllyCareSettings")]
public sealed class AllyCareSettings : GameSettings<AllyCareSettings>
{
    [Header("Bandaging themselves")]
    [Tooltip("Bandages a follower starts with.")]
    [SerializeField, Min(0)] private int bandagesCarried = 2;
    [Tooltip("The most they carry: they tear a new one from cloth now and then, up to this.")]
    [SerializeField, Min(0)] private int mostBandages = 2;
    [Tooltip("Seconds to tear one more bandage from cloth.")]
    [SerializeField, Min(1f)] private float makeOneEvery = 75f;
    [Tooltip("They bandage themselves below this share of their health...")]
    [SerializeField, Range(0.05f, 0.95f)] private float bandageBelow = 0.45f;
    [Tooltip("...and only with no enemy within this many metres of them.")]
    [SerializeField, Min(0f)] private float safeDistance = 6f;
    [Tooltip("Seconds it takes; a hit stops it, bandage kept.")]
    [SerializeField, Min(0.2f)] private float bandageSeconds = 2.5f;
    [Tooltip("Share of their health one of their own bandages gives back.")]
    [SerializeField, Range(0.05f, 1f)] private float bandageHeals = 0.35f;

    [Header("The player bandaging them")]
    [Tooltip("Seconds the player works on them.")]
    [SerializeField, Min(0.2f)] private float tendSeconds = 1.5f;
    [Tooltip("The Bandage prompt shows below this share of their health.")]
    [SerializeField, Range(0.1f, 1f)] private float tendBelow = 0.9f;
    [Tooltip("A healing item gives back the same share of their health as of the player's, times this.")]
    [SerializeField, Min(0.1f)] private float tendStrength = 1.5f;

    [Header("A freed traveller in a fight")]
    [Tooltip("Enemies within this many metres of the player or the traveller make it a fight.")]
    [SerializeField, Min(1f)] private float fightRadius = 16f;
    [Tooltip("Metres behind the player, away from the enemies, the traveller keeps to in a fight: out of the blows aimed at the player.")]
    [SerializeField, Min(1f)] private float keepClear = 6.5f;
    [Tooltip("Chance a traveller sees a blow coming at them in time to step out of it.")]
    [SerializeField, Range(0f, 1f)] private float readsAttacks = 0.8f;
    [Tooltip("Metres they step aside from a blow.")]
    [SerializeField, Min(0.5f)] private float sidestep = 3f;

    public int BandagesCarried => bandagesCarried;
    public int MostBandages => Mathf.Max(mostBandages, bandagesCarried);
    public float MakeOneEvery => makeOneEvery;
    public float BandageBelow => bandageBelow;
    public float SafeDistance => safeDistance;
    public float BandageSeconds => bandageSeconds;
    public float BandageHeals => bandageHeals;
    public float TendSeconds => tendSeconds;
    public float TendBelow => tendBelow;
    public float TendStrength => tendStrength;
    public float FightRadius => fightRadius;
    public float KeepClear => keepClear;
    public float ReadsAttacks => readsAttacks;
    public float Sidestep => sidestep;
}
