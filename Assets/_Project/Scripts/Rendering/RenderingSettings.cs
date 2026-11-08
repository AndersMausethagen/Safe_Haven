using UnityEngine;

/// <summary>
/// The numbers of the world's runtime rendering (WorldRendering): the reflection probe that follows the player, the sky,
/// the clouds and their shadows, sky occlusion and decals. Lives at Resources/RenderingSettings; built-in defaults apply when it is missing.
/// </summary>
[CreateAssetMenu(menuName = "Safe Haven/Settings/Rendering Settings", fileName = "RenderingSettings")]
public sealed class RenderingSettings : GameSettings<RenderingSettings>
{
    [Header("Reflections")]
    [Tooltip("A realtime reflection probe that follows the player, so metal, water and wet stone reflect what is round them " +
             "instead of only the sky.")]
    [SerializeField] private bool reflections = true;
    [Tooltip("Cube face size in pixels.")]
    [SerializeField] private int reflectionResolution = 128;
    [Tooltip("Metres above the ground it is taken from: above the player's head, so they don't fill it.")]
    [SerializeField, Min(0.5f)] private float reflectionHeight = 4f;
    [Tooltip("Rendered again once the player has moved this far...")]
    [SerializeField, Min(1f)] private float reflectionMove = 8f;
    [Tooltip("...or the hour has moved on this much...")]
    [SerializeField, Min(0.01f)] private float reflectionHours = 0.25f;
    [Tooltip("...and never more often than this, in seconds (each render is spread over nine frames, a face a frame).")]
    [SerializeField, Min(0.1f)] private float reflectionInterval = 1f;
    [Tooltip("...or this many seconds have passed, so drifting clouds and turning weather reach the reflections.")]
    [SerializeField, Min(1f)] private float reflectionSkySeconds = 6f;
    [Tooltip("What it sees.")]
    [SerializeField] private LayerMask reflectionLayers = ~(1 << 5);

    [Header("Sky")]
    [Tooltip("The sky every scene with a clock gets (Shaders/Sky.shader): gradient, sun, moon, stars and the clouds.")]
    [SerializeField] private Material skyMaterial;
    [Tooltip("Days the moon takes from new to new.")]
    [SerializeField, Min(1f)] private float moonCycleDays = 12f;
    [Tooltip("Its phase on the first day (0 new, 0.5 full).")]
    [SerializeField, Range(0f, 1f)] private float moonPhaseAtStart = 0.42f;

    [Header("Cloud shadows")]
    [Tooltip("Soft shadows of passing clouds on the sunlit ground.")]
    [SerializeField] private bool cloudShadows = true;
    [SerializeField] private bool cloudShadowsInRaids = true;
    [Tooltip("Metres one tile of the cloud pattern covers.")]
    [SerializeField, Min(20f)] private float cloudSize = 180f;
    [Tooltip("Metres a second they drift at the wind's usual strength.")]
    [SerializeField, Min(0f)] private float cloudSpeed = 2.5f;
    [Tooltip("Share of the ground under cloud on a clear day, and under a sky that is half cloud.")]
    [SerializeField] private Vector2 cloudCover = new Vector2(0.28f, 0.6f);
    [Tooltip("How dark a cloud's shadow is in full sun (0 none, 1 black).")]
    [SerializeField, Range(0f, 1f)] private float cloudDarkness = 0.55f;
    [Tooltip("How soft the shadows' edges are.")]
    [SerializeField, Range(0.02f, 0.5f)] private float cloudSoftness = 0.18f;
    [Header("Sky occlusion")]
    [Tooltip("Dim the sky's light and reflections under roofs and canopies, and keep rain and snow out from under them " +
             "(SkyOcclusionMap: a top-down height map round the player).")]
    [SerializeField] private bool skyOcclusion = true;
    [Tooltip("How much cover dims the light in the raids (1 as at home). Raids are nearly all canopy and dark: at full " +
             "strength the player vanished on the forest floor at night.")]
    [SerializeField, Range(0f, 1f)] private float skyOcclusionInRaids = 0.35f;
    [Tooltip("Metres the map covers, centred on the player.")]
    [SerializeField, Min(20f)] private float skyOcclusionArea = 160f;
    [SerializeField] private int skyOcclusionResolution = 512;
    [Tooltip("Seconds between redraws: buildings put up, people moving.")]
    [SerializeField, Min(0.2f)] private float skyOcclusionInterval = 1.5f;
    [SerializeField] private LayerMask skyOcclusionLayers = ~(1 << 5);
    [Tooltip("Index of the Sky Occlusion Renderer in the pipeline's renderer list (set by AgentScripts/BuildRendering.cs); -1 off.")]
    [SerializeField] private int skyOcclusionRendererIndex = -1;

    [Header("Decals")]
    [Tooltip("Short-lived decals out at once (blood, footprints); the oldest is reused after that.")]
    [SerializeField, Min(8)] private int decalPoolSize = 128;
    [Tooltip("Blood on the ground, tinted per creature from Blood Atlas (2x2 splats).")]
    [SerializeField] private Material bloodDecal;
    [Tooltip("The blood splats, readable: tinted into a copy for each creature's blood colour.")]
    [SerializeField] private Texture2D bloodAtlas;
    [Tooltip("Boot prints in wet ground (left and right side by side).")]
    [SerializeField] private Material footprintDecal;
    [Tooltip("Prints are left once the ground is this wet (WeatherSystem's wetness, 0-1).")]
    [SerializeField, Range(0f, 1f)] private float footprintWetness = 0.15f;
    [SerializeField, Min(1f)] private float footprintSeconds = 40f;
    [Tooltip("Worn, trampled ground round every building, its yard included.")]
    [SerializeField] private Material trampledDecal;
    [Tooltip("Metres the worn ground reaches past a building's footprint.")]
    [SerializeField, Min(0f)] private float trampledMargin = 1.6f;
    [Tooltip("Ash and char round a campfire.")]
    [SerializeField] private Material scorchDecal;
    [Tooltip("Damp, dirt and moss creeping up the foot of a building's walls.")]
    [SerializeField] private Material grimeDecal;
    [SerializeField, Min(0.1f)] private float grimeHeight = 0.7f;
    [Tooltip("Shaders/CloudCookie.shader, referenced so builds include it.")]
    [SerializeField] private Shader cloudShader;

    public bool Reflections => reflections;
    public int ReflectionResolution => Mathf.ClosestPowerOfTwo(Mathf.Clamp(reflectionResolution, 16, 1024));
    public float ReflectionHeight => reflectionHeight;
    public float ReflectionMove => reflectionMove;
    public float ReflectionHours => reflectionHours;
    public float ReflectionInterval => reflectionInterval;
    public float ReflectionSkySeconds => reflectionSkySeconds;
    public LayerMask ReflectionLayers => reflectionLayers;
    public Material SkyMaterial => skyMaterial;
    public float MoonCycleDays => moonCycleDays;
    public float MoonPhaseAtStart => moonPhaseAtStart;
    public bool CloudShadows => cloudShadows;
    public bool CloudShadowsInRaids => cloudShadowsInRaids;
    public float CloudSize => cloudSize;
    public float CloudSpeed => cloudSpeed;
    public Vector2 CloudCover => cloudCover;
    public float CloudDarkness => cloudDarkness;
    public float CloudSoftness => cloudSoftness;
    public bool SkyOcclusion => skyOcclusion;
    public float SkyOcclusionInRaids => skyOcclusionInRaids;
    public float SkyOcclusionArea => skyOcclusionArea;
    public int SkyOcclusionResolution => skyOcclusionResolution;
    public float SkyOcclusionInterval => skyOcclusionInterval;
    public LayerMask SkyOcclusionLayers => skyOcclusionLayers;
    public int SkyOcclusionRendererIndex => skyOcclusionRendererIndex;
    public int DecalPoolSize => decalPoolSize;
    public Material BloodDecal => bloodDecal;
    public Texture2D BloodAtlas => bloodAtlas;
    public Material FootprintDecal => footprintDecal;
    public float FootprintWetness => footprintWetness;
    public float FootprintSeconds => footprintSeconds;
    public Material TrampledDecal => trampledDecal;
    public float TrampledMargin => trampledMargin;
    public Material ScorchDecal => scorchDecal;
    public Material GrimeDecal => grimeDecal;
    public float GrimeHeight => grimeHeight;
    public Shader CloudShader => cloudShader != null ? cloudShader : Shader.Find("Hidden/Safe Haven/Cloud Cookie");
}
