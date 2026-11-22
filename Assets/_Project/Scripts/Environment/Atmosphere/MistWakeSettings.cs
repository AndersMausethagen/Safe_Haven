using UnityEngine;

/// <summary>
/// How the drifting mist (Terrain Mist, Mist Volumes; not the base fog) answers what moves through it (MistWakes; the user, 2026-11-22,
/// after an Ocarina of Time style ground fog: "it reacts to all monsters, npcs and even trees when we fell them").
/// </summary>
[CreateAssetMenu(fileName = "MistWakeSettings", menuName = "Safe Haven/Settings/Mist Wakes")]
public sealed class MistWakeSettings : GameSettings<MistWakeSettings>
{
    [Tooltip("Shaders/MistWakeSim.shader, referenced so builds include it.")]
    [SerializeField] private Shader simulation;

    [Header("The window round the player")]
    [Tooltip("Metres across the stirred square that follows the player. Beyond it the mist drifts untouched.")]
    [SerializeField, Min(8f)] private float size = 48f;
    [Tooltip("Texels across it (48 m over 192: a quarter metre each).")]
    [SerializeField, Range(64, 512)] private int resolution = 192;

    [Header("How the mist answers")]
    [Tooltip("How fully a body parts the mist where it stands (0-1).")]
    [SerializeField, Range(0f, 1f)] private float parting = 0.8f;
    [Tooltip("How much of the flow's carrying the mist's pattern shows (1: all of it): it curls round a body rather than just thinning.")]
    [SerializeField, Range(0f, 2f)] private float swirl = 0.7f;
    [Tooltip("The wake reaches this high over the ground; the mist above drifts on untouched.")]
    [SerializeField, Min(0.5f)] private float reach = 3f;
    [Tooltip("How fast a gap fills back in (per second; 0.3 is a gap two thirds closed after about 3.5 s).")]
    [SerializeField, Range(0.05f, 5f)] private float fillRate = 0.3f;
    [Tooltip("How fast a swirl settles (per second).")]
    [SerializeField, Range(0.05f, 5f)] private float settleRate = 0.45f;
    [Tooltip("How fast the stirred air comes to rest (per second): lower, and the swirls turn on longer.")]
    [SerializeField, Range(0.05f, 5f)] private float velocityDamping = 0.9f;
    [Tooltip("How hard a moving body stirs the mist (Stable Fluids: the push it gives the air).")]
    [SerializeField, Range(0f, 4f)] private float stir = 1f;
    [Tooltip("Pressure steps a frame: more, and the flow curls round a body more truly (each is one small pass).")]
    [SerializeField, Range(4, 60)] private int pressureSteps = 20;

    [Header("Who stirs it")]
    [Tooltip("A body's stamp is this many times its own radius.")]
    [SerializeField, Min(0.5f)] private float bodyScale = 2.2f;
    [Tooltip("How much a body standing still parts the mist; moving adds up to all of it at Full Speed.")]
    [SerializeField, Range(0f, 1f)] private float stillPresence = 0.35f;
    [SerializeField, Min(0.5f)] private float fullSpeed = 6f;
    [Tooltip("A felled tree's trunk parts this wide as it sweeps down (metres, for a full-sized tree).")]
    [SerializeField, Min(0.5f)] private float treeRadius = 2.4f;
    [Tooltip("And as it lands: how much wider its burst is.")]
    [SerializeField, Min(1f)] private float treeLandingSpread = 1.6f;
    [Tooltip("The most stamps in a frame: the player first, then blows and falling trees, then everyone else.")]
    [SerializeField, Range(8, 64)] private int mostStamps = 64;

    [Header("Blows")]
    [Tooltip("How fast a blow throws the mist the way it goes (metres a second, at the heart of its gust).")]
    [SerializeField, Min(0f)] private float strikeSpeed = 7f;
    [Tooltip("How long a blow's gust goes on, fading (seconds).")]
    [SerializeField, Range(0.02f, 1f)] private float strikeSeconds = 0.2f;
    [Tooltip("How much a blow's gust parts the mist as it throws it (0-1).")]
    [SerializeField, Range(0f, 1f)] private float strikeStrength = 0.45f;
    [Tooltip("The farthest a gust reaches from the one striking (a long spear's reach, a troll's sweep), metres.")]
    [SerializeField, Min(0.5f)] private float strikeMostReach = 4f;
    [Tooltip("Metres round a swinging blade's tip that its path stirs the mist.")]
    [SerializeField, Min(0.1f)] private float bladeRadius = 0.6f;
    [Tooltip("How much a blade at full speed parts the mist along its path (0-1).")]
    [SerializeField, Range(0f, 1f)] private float bladeStrength = 0.4f;

    [Header("What stands in its way")]
    [Tooltip("How much a tree, a rock or a wall parts the mist where it stands; the creep carries that gap on into its lee (0-1).")]
    [SerializeField, Range(0f, 1f)] private float obstacleParting = 0.7f;
    [Tooltip("Only what rises at least this high out of the ground stands in the mist's way (not a floor, a path, a rug).")]
    [SerializeField, Min(0f)] private float lowestObstacle = 0.3f;
    [Tooltip("The narrowest an obstacle counts as (metres from its middle): a thin post still bends the creep.")]
    [SerializeField, Min(0.05f)] private float smallestObstacle = 0.3f;
    [Tooltip("Seconds between two looks for what stands in the square (it is found again sooner when the player has moved far).")]
    [SerializeField, Min(0.05f)] private float obstacleGap = 0.4f;
    [Tooltip("Metres the player moves before the square is looked over again.")]
    [SerializeField, Min(0.5f)] private float obstacleRegatherDistance = 3f;
    [Tooltip("Metres above and below the player searched for obstacles (hillsides in the square).")]
    [SerializeField, Min(1f)] private float obstacleSearchHeight = 12f;

    public Shader Simulation => simulation;
    public float Size => size;
    public int Resolution => resolution;
    public float Parting => parting;
    public float Swirl => swirl;
    public float Reach => reach;
    public float FillRate => fillRate;
    public float SettleRate => settleRate;
    public float VelocityDamping => velocityDamping;
    public float Stir => stir;
    public int PressureSteps => pressureSteps;
    public float BodyScale => bodyScale;
    public float StillPresence => stillPresence;
    public float FullSpeed => fullSpeed;
    public float TreeRadius => treeRadius;
    public float TreeLandingSpread => treeLandingSpread;
    public int MostStamps => mostStamps;
    public float StrikeSpeed => strikeSpeed;
    public float StrikeSeconds => strikeSeconds;
    public float StrikeStrength => strikeStrength;
    public float StrikeMostReach => strikeMostReach;
    public float BladeRadius => bladeRadius;
    public float BladeStrength => bladeStrength;
    public float ObstacleParting => obstacleParting;
    public float LowestObstacle => lowestObstacle;
    public float SmallestObstacle => smallestObstacle;
    public float ObstacleGap => obstacleGap;
    public float ObstacleRegatherDistance => obstacleRegatherDistance;
    public float ObstacleSearchHeight => obstacleSearchHeight;

    /// <summary>How much a body moving at <paramref name="speed"/> parts the mist (0-1).</summary>
    public float PresenceAt(float speed) => Mathf.Lerp(stillPresence, 1f, Mathf.Clamp01(speed / Mathf.Max(0.1f, fullSpeed)));
}
