using UnityEngine;

/// <summary>The shape a blow presses into the ground.</summary>
public enum GroundMarkShape
{
    /// <summary>No mark.</summary>
    None,
    /// <summary>A round pit with a raised lip: a stomp, a slam, a boulder coming down.</summary>
    Crater,
    /// <summary>A long gouge along the blow: a charge ploughing the ground.</summary>
    Furrow,
    /// <summary>An arc of grass cut short in front of the swing, with no pit: a heavy slash mowing.</summary>
    Mow,
}

/// <summary>
/// What a heavy blow does to the world around it (2026-11-14, the user: "make all attacks affect the environment"): a burst
/// of wind through the grass, dust and clods thrown up, a mark pressed into the ground for a while, a decal on it, loose
/// things thrown and trees shaken, and anyone standing in it stumbling. One asset per kind of blow (a heavy slash, a troll's
/// stomp, a boulder landing), named on the AttackDefinitions that use it, and the player's from Ground Impact Settings.
/// Sizes are for a body of Reference Height; a bigger or smaller creature scales them.
/// </summary>
[CreateAssetMenu(fileName = "NewGroundImpact", menuName = "Safe Haven/Combat/Ground Impact")]
public sealed class GroundImpactDefinition : ScriptableObject
{
    [Tooltip("The body size these numbers are for. A creature twice as tall scales radius, depth and dust by up to twice.")]
    [SerializeField, Min(0.3f)] private float referenceHeight = 2f;

    [Header("Wind through the grass")]
    [Tooltip("Metres the burst reaches.")]
    [SerializeField, Min(0f)] private float gustRadius = 4f;
    [Tooltip("How hard it pushes the grass over, in metres at the tip.")]
    [SerializeField, Range(0f, 1.5f)] private float gustStrength = 0.35f;
    [Tooltip("How fast the wave rolls out, metres per second.")]
    [SerializeField, Min(1f)] private float gustSpeed = 14f;
    [Tooltip("Width of the blow in degrees: 360 rolls out all round (a stomp), less blows only ahead along the swing.")]
    [SerializeField, Range(10f, 360f)] private float gustArc = 360f;

    [Header("Dust and debris")]
    [Tooltip("Dust thrown up (particles at full size). 0: none.")]
    [SerializeField, Min(0)] private int dust = 14;
    [Tooltip("Clods and pebbles thrown up, falling back.")]
    [SerializeField, Min(0)] private int debris = 8;
    [Tooltip("Grass bits torn up or cut (only where grass grows).")]
    [SerializeField, Min(0)] private int clippings = 10;
    [Tooltip("Metres the dust spreads to.")]
    [SerializeField, Min(0.1f)] private float dustRadius = 2f;

    [Header("Mark on the ground")]
    [SerializeField] private GroundMarkShape mark = GroundMarkShape.None;
    [Tooltip("Metres across the middle of the mark (a furrow's width; a mow's reach).")]
    [SerializeField, Min(0.1f)] private float markRadius = 1.2f;
    [Tooltip("Metres the ground sinks at the middle. Shown where the terrain is shaped finely enough (Terrain Detail).")]
    [SerializeField, Range(0f, 0.4f)] private float markDepth = 0.12f;
    [Tooltip("Metres the lip round it rises.")]
    [SerializeField, Range(0f, 0.2f)] private float rimHeight = 0.04f;
    [Tooltip("Furrow: metres long.")]
    [SerializeField, Min(0.1f)] private float furrowLength = 2.5f;
    [Tooltip("How flat the grass lies in it (0-1), or for a mow how short it is cut.")]
    [SerializeField, Range(0f, 1f)] private float grassFlatten = 0.8f;
    [Tooltip("Seconds before the ground has healed.")]
    [SerializeField, Min(1f)] private float markSeconds = 120f;
    [Tooltip("Painted over the mark: cracked earth, a scorch. Empty: none.")]
    [SerializeField] private Material decal;
    [Tooltip("Metres across the decal (before scaling).")]
    [SerializeField, Min(0.2f)] private float decalSize = 3f;

    [Header("Force")]
    [Tooltip("Shove given to loose things in reach (the dead, broken pieces, props), as an explosion's force.")]
    [SerializeField, Min(0f)] private float push;
    [Tooltip("Metres it reaches loose things and trees.")]
    [SerializeField, Min(0f)] private float pushRadius = 4f;
    [Tooltip("Degrees trees in reach are shaken over (0: none). Shaken trees drop leaves.")]
    [SerializeField, Range(0f, 30f)] private float treeShake;
    [Tooltip("Camera shake for the player near it (0-1), less with distance.")]
    [SerializeField, Range(0f, 1f)] private float shake;

    [Header("Noise and element")]
    [Tooltip("Metres the noise carries: monsters inside hear it and come, game bolts, birds take flight (WorldNoise).")]
    [SerializeField, Min(0f)] private float noise;
    [Tooltip("Fire sets webs, gas and dry grass alight where it lands; frost freezes pools and puts fires out (GroundHazard).")]
    [SerializeField] private HazardElement element = HazardElement.None;

    [Header("Footing")]
    [Tooltip("Metres from the middle within which the player stumbles, hit or not (0: never).")]
    [SerializeField, Min(0f)] private float stumbleRadius;
    [Tooltip("Seconds the stumble lasts.")]
    [SerializeField, Min(0f)] private float stumbleSeconds = 0.5f;

    [Header("Sound")]
    [Tooltip("The thud and patter of it. Empty: none (the blow's own hit sound still plays).")]
    [SerializeField] private SoundDefinition sound;

    public float ReferenceHeight => referenceHeight;
    public float GustRadius => gustRadius;
    public float GustStrength => gustStrength;
    public float GustSpeed => gustSpeed;
    public float GustArc => gustArc;
    public int Dust => dust;
    public int Debris => debris;
    public int Clippings => clippings;
    public float DustRadius => dustRadius;
    public GroundMarkShape Mark => mark;
    public float MarkRadius => markRadius;
    public float MarkDepth => markDepth;
    public float RimHeight => rimHeight;
    public float FurrowLength => furrowLength;
    public float GrassFlatten => grassFlatten;
    public float MarkSeconds => markSeconds;
    public Material Decal => decal;
    public float DecalSize => decalSize;
    public float Push => push;
    public float PushRadius => pushRadius;
    public float TreeShake => treeShake;
    public float Shake => shake;
    public float Noise => noise;
    public HazardElement Element => element;
    public float StumbleRadius => stumbleRadius;
    public float StumbleSeconds => stumbleSeconds;
    public SoundDefinition Sound => sound;
}
