using UnityEngine;

/// <summary>
/// Game-wide parts of ground impacts (GroundImpacts): the player's heavy blows by kind, the effects every impact uses,
/// how the ground's colour is read, and the caps that keep it cheap. Lives at Resources/GroundImpactSettings.
/// </summary>
[CreateAssetMenu(fileName = "GroundImpactSettings", menuName = "Safe Haven/Settings/Ground Impacts")]
public sealed class GroundImpactSettings : GameSettings<GroundImpactSettings>
{
    [Header("The player's heavy blows (heavy swings and weapon arts)")]
    [SerializeField] private GroundImpactDefinition heavySlash;
    [SerializeField] private GroundImpactDefinition heavyBlunt;
    [SerializeField] private GroundImpactDefinition heavyStab;
    [Tooltip("A two-handed blunt weapon's heavy blow (a maul, a club): a crater of its own.")]
    [SerializeField] private GroundImpactDefinition heavyTwoHandedBlunt;
    [Tooltip("Any other blow of the player's: a sudden wind through the grass ahead, nothing more (no dust, mark or noise).")]
    [SerializeField] private GroundImpactDefinition lightSwing;

    [Header("Effects")]
    [Tooltip("The burst of dust, clods, grass bits and mud (GroundImpactBurst).")]
    [SerializeField] private GameObject burst;
    [Tooltip("Leaves shaken down from trees.")]
    [SerializeField] private GameObject leaves;
    [Tooltip("Where it comes down in open water.")]
    [SerializeField] private GameObject splash;
    [Tooltip("The decal of burnt ground (grass fires).")]
    [SerializeField] private Material scorch;

    [Header("Fire and frost meeting hazards (GroundHazard)")]
    [Tooltip("A grass fire (a GroundHazard with Element Fire and Spreads On Grass).")]
    [SerializeField] private GameObject grassFire;
    [Tooltip("Where a fire is put out (rain, frost).")]
    [SerializeField] private GameObject steam;
    [SerializeField] private SoundDefinition igniteSound;
    [SerializeField] private SoundDefinition gasIgniteSound;
    [SerializeField] private SoundDefinition douseSound;
    [SerializeField] private SoundDefinition freezeSound;
    [Tooltip("A sapling snapped by a blow or a charge.")]
    [SerializeField] private SoundDefinition snapSound;
    [Tooltip("Trees shaken by a heavy blow.")]
    [SerializeField] private SoundDefinition treeShakeSound;

    [Header("Ground colours (the dust takes the colour of what it is thrown from)")]
    [SerializeField] private Color dirt = new Color(0.45f, 0.36f, 0.25f, 1f);
    [SerializeField] private Color grass = new Color(0.36f, 0.42f, 0.2f, 1f);
    [SerializeField] private Color rock = new Color(0.52f, 0.5f, 0.47f, 1f);
    [SerializeField] private Color sand = new Color(0.7f, 0.62f, 0.45f, 1f);
    [SerializeField] private Color snow = new Color(0.9f, 0.93f, 0.97f, 1f);
    [SerializeField] private Color mud = new Color(0.22f, 0.17f, 0.11f, 1f);
    [Tooltip("Rain above this (0-1) turns dust to mud thrown in clots.")]
    [SerializeField, Range(0f, 1f)] private float wetFromRain = 0.35f;

    [Tooltip("Hidden/Safe Haven/Ground Impact Stamp: referenced here so builds keep it.")]
    [SerializeField] private Shader stampShader;

    [Header("Its own side (a giant's stomp among its pack)")]
    [Tooltip("Share of the blow's damage taken by creatures of the attacker's own side inside the stumble ring.")]
    [SerializeField, Range(0f, 1f)] private float friendlyDamage = 0.1f;
    [Tooltip("Metres they are tossed outward.")]
    [SerializeField, Min(0f)] private float friendlyToss = 3f;
    [Tooltip("Seconds they stand dazed after.")]
    [SerializeField, Min(0f)] private float dazeSeconds = 1f;
    [SerializeField] private SoundDefinition dazeSound;

    [Header("Caps")]
    [Tooltip("Wind bursts through the grass at once (the shader's limit is 6).")]
    [SerializeField, Range(1, 6)] private int maxGusts = 6;
    [Tooltip("Decals on the ground at once; the oldest goes first.")]
    [SerializeField, Range(1, 32)] private int maxDecals = 12;
    [Tooltip("Metres across the window of ground marks around the player.")]
    [SerializeField, Range(16f, 96f)] private float markArea = 48f;
    [Tooltip("Texels across that window.")]
    [SerializeField] private int markResolution = 512;
    [Tooltip("Impacts further than this from the camera are skipped altogether.")]
    [SerializeField, Min(10f)] private float maxDistance = 70f;

    public GroundImpactDefinition HeavySlash => heavySlash;
    public GroundImpactDefinition HeavyBlunt => heavyBlunt;
    public GroundImpactDefinition HeavyStab => heavyStab;
    public GroundImpactDefinition HeavyTwoHandedBlunt => heavyTwoHandedBlunt;
    public GroundImpactDefinition LightSwing => lightSwing;
    public Shader StampShader => stampShader != null ? stampShader : Shader.Find("Hidden/Safe Haven/Ground Impact Stamp");
    public GameObject Burst => burst;
    public GameObject Leaves => leaves;
    public GameObject Splash => splash;
    public Material Scorch => scorch;
    public GameObject GrassFire => grassFire;
    public GameObject Steam => steam;
    public SoundDefinition IgniteSound => igniteSound;
    public SoundDefinition GasIgniteSound => gasIgniteSound;
    public SoundDefinition DouseSound => douseSound;
    public SoundDefinition FreezeSound => freezeSound;
    public SoundDefinition SnapSound => snapSound;
    public SoundDefinition TreeShakeSound => treeShakeSound;
    public Color Dirt => dirt;
    public Color Grass => grass;
    public Color Rock => rock;
    public Color Sand => sand;
    public Color Snow => snow;
    public Color Mud => mud;
    public float WetFromRain => wetFromRain;
    public int MaxGusts => maxGusts;
    public int MaxDecals => maxDecals;
    public float MarkArea => markArea;
    public int MarkResolution => Mathf.Clamp(Mathf.ClosestPowerOfTwo(markResolution), 128, 1024);
    public float MaxDistance => maxDistance;
    public float FriendlyDamage => friendlyDamage;
    public float FriendlyToss => friendlyToss;
    public float DazeSeconds => dazeSeconds;
    public SoundDefinition DazeSound => dazeSound;
}
