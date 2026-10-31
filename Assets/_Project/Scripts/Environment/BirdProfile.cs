using UnityEngine;

/// <summary>
/// The birds of one place: crows in the Misty Woods, small songbirds at home. What they look like, how many come, how easily
/// they startle and how they sound. Read-only at runtime; <see cref="Birds"/> flies them.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Environment/Bird Profile", fileName = "Birds")]
public sealed class BirdProfile : ScriptableObject
{
    [Header("Look")]
    [Tooltip("One is picked for each bird.")]
    [SerializeField] private Color[] colours = { new Color(0.55f, 0.42f, 0.3f) };
    [Tooltip("Metres across the sprite, smallest and largest. A touch bigger than life, or they vanish on screen.")]
    [SerializeField] private Vector2 size = new Vector2(0.45f, 0.6f);

    [Header("How many")]
    [Tooltip("Flocks about at once by day.")]
    [SerializeField, Min(0)] private int mostFlocks = 3;
    [Tooltip("Birds in a flock, fewest and most.")]
    [SerializeField] private Vector2Int flockSize = new Vector2Int(3, 7);
    [Tooltip("Seconds between flocks arriving, shortest and longest.")]
    [SerializeField] private Vector2 arrivalSeconds = new Vector2(6f, 16f);
    [Tooltip("Share of them still about in full dark. Songbirds roost; a few crows don't.")]
    [SerializeField, Range(0f, 1f)] private float nightShare;
    [Tooltip("Share of them that shelter in a downpour.")]
    [SerializeField, Range(0f, 1f)] private float rainShyness = 0.8f;
    [Tooltip("Chance a new arrival only passes over, high up, and never lands.")]
    [SerializeField, Range(0f, 1f)] private float flyoverChance = 0.25f;

    [Header("Where")]
    [Tooltip("Metres from the player that they land, nearest and furthest. Nearest should be beyond Startle Distance, or they land only to leave.")]
    [SerializeField] private Vector2 landingDistance = new Vector2(11f, 20f);
    [Tooltip("Metres around the middle of the flock that its birds land in.")]
    [SerializeField, Min(0.2f)] private float flockSpread = 1.8f;
    [Tooltip("Metres from the player past which a settled flock is quietly dropped, out of sight.")]
    [SerializeField, Min(10f)] private float leaveDistance = 45f;

    [Header("Startling")]
    [Tooltip("Share of how far the player can be heard (footsteps, gear, a fight) that a flock reacts at. So sneaking gets close, running doesn't.")]
    [SerializeField, Min(0f)] private float hearing = 0.9f;
    [Tooltip("Metres they let anyone come, however quiet, and the most a sound can startle them from.")]
    [SerializeField] private Vector2 startleDistance = new Vector2(1.6f, 9f);

    [Header("Flight")]
    [Tooltip("Metres a second, slowest and fastest.")]
    [SerializeField] private Vector2 flySpeed = new Vector2(6f, 9f);
    [Tooltip("Metres a second they climb on taking off.")]
    [SerializeField, Min(0f)] private float climb = 3.5f;
    [Tooltip("Wing beats a second.")]
    [SerializeField, Min(0.5f)] private float flapRate = 9f;
    [Tooltip("Share of flight spent gliding with wings held out. Crows do; small birds hardly at all.")]
    [SerializeField, Range(0f, 1f)] private float glide = 0.1f;
    [Tooltip("Seconds they are seen fleeing before they fade out.")]
    [SerializeField, Min(0.5f)] private float fleeSeconds = 3.5f;

    [Header("Sound")]
    [Tooltip("Played once when a flock goes up.")]
    [SerializeField] private SoundDefinition takeOff;
    [Tooltip("Played now and then by a flock on the ground, and as a crow goes up.")]
    [SerializeField] private SoundDefinition call;
    [Tooltip("Seconds between calls from a settled flock, shortest and longest.")]
    [SerializeField] private Vector2 callSeconds = new Vector2(4f, 12f);
    [Tooltip("Whether it calls as it takes off: an alarm.")]
    [SerializeField] private bool callsOnTakeOff;

    public int FlockSizeMin => flockSize.x;
    public int FlockSizeMax => Mathf.Max(flockSize.x, flockSize.y);
    public float FlockSpread => flockSpread;
    public Vector2 LandingDistance => landingDistance;
    public float LeaveDistance => leaveDistance;
    public float Climb => climb;
    public float FlapRate => flapRate;
    public float Glide => glide;
    public float FleeSeconds => fleeSeconds;
    public float FlyoverChance => flyoverChance;
    public SoundDefinition TakeOff => takeOff;
    public SoundDefinition Call => call;
    public bool CallsOnTakeOff => callsOnTakeOff;

    public float RandomSize => Random.Range(size.x, Mathf.Max(size.x, size.y));
    public float RandomSpeed => Random.Range(flySpeed.x, Mathf.Max(flySpeed.x, flySpeed.y));
    public float RandomArrival => Random.Range(arrivalSeconds.x, Mathf.Max(arrivalSeconds.x, arrivalSeconds.y));
    public float RandomCall => Random.Range(callSeconds.x, Mathf.Max(callSeconds.x, callSeconds.y));
    public Color RandomColour => colours.Length > 0 ? colours[Random.Range(0, colours.Length)] : Color.gray;

    /// <summary>How many flocks stay about in this dark and rain, each 0..1.</summary>
    public int FlocksFor(float night, float rain) =>
        Mathf.RoundToInt(mostFlocks * Mathf.Lerp(1f, nightShare, Mathf.Clamp01(night)) * (1f - rainShyness * Mathf.Clamp01(rain)));

    /// <summary>How near the player can come before a flock goes up, when they can be heard this many metres away.</summary>
    public float StartleDistance(float heardFrom) => Mathf.Clamp(heardFrom * hearing, startleDistance.x, Mathf.Max(startleDistance.x, startleDistance.y));
}
