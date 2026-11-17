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
    [SerializeField, Min(1f)] private float reflectionSkySeconds = 20f;
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
    [Tooltip("Seconds at least between redraws (each costs about 3 ms).")]
    [SerializeField, Min(0.2f)] private float skyOcclusionInterval = 1.5f;
    [Tooltip("Redrawn once the player is this many metres from where it was last drawn (it covers Area round them)...")]
    [SerializeField, Min(1f)] private float skyOcclusionMove = 12f;
    [Tooltip("...or a building has gone up or come down, or this many seconds have passed (felled trees, people).")]
    [SerializeField, Min(1f)] private float skyOcclusionRefresh = 10f;
    [SerializeField] private LayerMask skyOcclusionLayers = ~(1 << 5);
    [Tooltip("Index of the Sky Occlusion Renderer in the pipeline's renderer list (set by AgentScripts/BuildRendering.cs); -1 off.")]
    [SerializeField] private int skyOcclusionRendererIndex = -1;

    [Header("Ground light (generated raids)")]
    [Tooltip("A generated raid map's light is worked out once while its loading screen is up (GroundLightMap; Docs/" +
             "LightingBakeResearch_2026-11-17.md §2.1): what stands overhead is drawn once over the whole map instead of round the " +
             "player every few seconds, and a raid whose Look › Ground Light is on also gets the land's shade and light thrown back " +
             "from sunlit ground. Off: raids use the sky map round the player as before.")]
    [SerializeField] private bool groundLight = true;
    [Tooltip("Metres a texel of the cover drawn over the whole map (2048 texels a side at most; the map round the player was 0.31).")]
    [SerializeField, Range(0.1f, 1f)] private float groundCoverTexel = 0.25f;
    [Tooltip("Metres a cell of the field: the land's shade and the thrown-back light (1024 cells a side at most).")]
    [SerializeField, Range(0.2f, 2f)] private float groundLightCell = 0.4f;
    [Tooltip("Directions the land's shade looks along for higher ground.")]
    [SerializeField, Range(4, 24)] private int reliefDirections = 12;
    [Tooltip("From how near to how far (metres) higher ground shades a spot. Nearer than this is SSAO's.")]
    [SerializeField] private Vector2 reliefReach = new Vector2(1.5f, 30f);
    [Tooltip("Metres the light thrown back from sunlit ground spreads into the shade round it.")]
    [SerializeField, Range(1f, 16f)] private float bounceSpread = 6f;
    [Tooltip("At dusk and night the field keeps the ground's average light as it was before it (under canopy and in hollows darker, " +
             "open ground lighter), so nights never get darker on the whole; lifting by at most this much (1: no lift).")]
    [SerializeField, Range(1f, 2f)] private float nightLiftMost = 1.5f;
    [Tooltip("Seconds after a tree is felled before the cover over it is drawn again (the falling tree has landed).")]
    [SerializeField, Min(0f)] private float felledRedrawDelay = 6f;

    [Header("Characters")]
    [Tooltip("How bright the rim the sky and sun put on a figure's edge is (CharacterShading.hlsl).")]
    [SerializeField, Range(0f, 2f)] private float rimStrength = 0.2f;
    [Tooltip("How thin it is: higher keeps it to the very edge.")]
    [SerializeField, Range(1f, 8f)] private float rimSharpness = 4f;
    [Tooltip("How much stronger at night (1 = twice), so figures still read in the dark.")]
    [SerializeField, Range(0f, 3f)] private float rimAtNight = 0.8f;
    [Tooltip("How much of the sun the edge catches, against the sky's light.")]
    [SerializeField, Range(0f, 2f)] private float rimSunShare = 0.5f;
    [Tooltip("The colour light takes on passing into skin.")]
    [SerializeField] private Color skinScatter = new Color(1f, 0.45f, 0.3f);
    [SerializeField, Range(0f, 2f)] private float skinScatterStrength = 0.4f;

    [Header("Wet characters")]
    [Tooltip("People and creatures out in the rain soak, darken and gloss (CharacterWetness).")]
    [SerializeField] private bool wetCharacters = true;
    [Tooltip("Seconds out in a downpour to soak through (lighter rain takes longer).")]
    [SerializeField, Min(1f)] private float soakSeconds = 30f;
    [Tooltip("Seconds to dry from soaked, out of the rain.")]
    [SerializeField, Min(1f)] private float drySeconds = 150f;
    [Tooltip("...and by a fire (the player).")]
    [SerializeField, Min(1f)] private float fireDrySeconds = 20f;
    [Tooltip("How much soaked cloth, hair and hide darken (polished and metal things less).")]
    [SerializeField, Range(0f, 1f)] private float wetDarkening = 0.5f;
    [Tooltip("How glossy a soaked surface gets at least.")]
    [SerializeField, Range(0f, 1f)] private float wetSmoothness = 0.7f;
    [Tooltip("Metres above a character's feet the look for a roof starts (over the head).")]
    [SerializeField, Min(0.5f)] private float shelterRayStart = 2.2f;

    [Header("Fire shadows")]
    [Tooltip("At dusk and by night the fires and lamps nearest the view cast soft shadows (LightShadowBudget).")]
    [SerializeField] private bool fireShadows = true;
    [Tooltip("How many at once (each point light draws six shadow faces).")]
    [SerializeField, Range(0, 6)] private int shadowedLights = 3;
    [Tooltip("Metres round what the camera looks at that a light may be.")]
    [SerializeField, Min(4f)] private float fireShadowReach = 30f;
    [Tooltip("Lights reaching less than this many metres don't count (small glows: the Rootbound Stones reach 4).")]
    [SerializeField, Min(0f)] private float fireShadowLeastRange = 5f;
    [SerializeField, Range(0f, 1f)] private float fireShadowStrength = 0.85f;
    [Tooltip("They come on once the night is at least this far in (0-1), or in the golden hour.")]
    [SerializeField, Range(0f, 1f)] private float fireShadowsFromNight = 0.15f;
    [Tooltip("Every fire and lamp glows in the volumetric mist (an AtmosphereLight is added where one is missing).")]
    [SerializeField] private bool fireFogGlow = true;
    [SerializeField, Min(0f)] private float fireFogGlowStrength = 0.35f;
    [Tooltip("Metres of mist each pushes back round its flame (0 none).")]
    [SerializeField, Min(0f)] private float fireFogClearing = 0f;

    [Header("Shader warm-up")]
    [Tooltip("The shader variants play uses, recorded by AgentScripts/RecordShaderWarmup.cs; compiled behind the first " +
             "loading screen of a session (ShaderWarmup), in builds.")]
    [SerializeField] private ShaderVariantCollection warmupVariants;
    [Tooltip("Never hold the loading screen longer than this for it.")]
    [SerializeField, Min(1f)] private float warmupMostSeconds = 20f;

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
    public float SkyOcclusionMove => skyOcclusionMove;
    public float SkyOcclusionRefresh => skyOcclusionRefresh;
    public LayerMask SkyOcclusionLayers => skyOcclusionLayers;
    /// <summary>The Sky Occlusion Renderer's place in the pipeline's renderer list, or null when there is none (off).</summary>
    public int? SkyOcclusionRenderer => skyOcclusionRendererIndex >= 0 ? skyOcclusionRendererIndex : (int?)null;
    public bool GroundLight => groundLight;
    public float GroundCoverTexel => groundCoverTexel;
    public float GroundLightCell => groundLightCell;
    public int ReliefDirections => reliefDirections;
    public Vector2 ReliefReach => reliefReach;
    public float BounceSpread => bounceSpread;
    public float NightLiftMost => nightLiftMost;
    public float FelledRedrawDelay => felledRedrawDelay;
    public float RimStrength => rimStrength;
    public float RimSharpness => rimSharpness;
    public float RimAtNight => rimAtNight;
    public float RimSunShare => rimSunShare;
    public Color SkinScatter => skinScatter;
    public float SkinScatterStrength => skinScatterStrength;
    public bool WetCharacters => wetCharacters;
    public float SoakSeconds => soakSeconds;
    public float DrySeconds => drySeconds;
    public float FireDrySeconds => fireDrySeconds;
    public float WetDarkening => wetDarkening;
    public float WetSmoothness => wetSmoothness;
    public float ShelterRayStart => shelterRayStart;
    public bool FireShadows => fireShadows;
    public int ShadowedLights => shadowedLights;
    public float FireShadowReach => fireShadowReach;
    public float FireShadowLeastRange => fireShadowLeastRange;
    public float FireShadowStrength => fireShadowStrength;
    public float FireShadowsFromNight => fireShadowsFromNight;
    public bool FireFogGlow => fireFogGlow;
    public float FireFogGlowStrength => fireFogGlowStrength;
    public float FireFogClearing => fireFogClearing;
    public ShaderVariantCollection WarmupVariants => warmupVariants;
    public float WarmupMostSeconds => warmupMostSeconds;
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
