using UnityEngine;

/// <summary>Rain around the camera: falling streaks from a box above the ground the camera looks at, and small splashes
/// on whatever each drop hits (a roof, a rock, the ground; RainDrips adds the drips off eaves and leaves), both scaled by the intensity <see cref="WeatherSystem"/> sets. The streaks slant with the scene's
/// Moss Wind. Also sets _SafeHavenRain (0..1) for shaders (rain rings on water). In a snowing season the same weather
/// falls as drifting flakes instead (no streaks, splashes or rings).</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RainEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem drops;
    [SerializeField] private ParticleSystem splashes;
    [Tooltip("Snowflakes, falling in place of the rain in a snowing season (optional).")]
    [SerializeField] private ParticleSystem flakes;
    [Tooltip("Flakes per second in a heavy snowfall.")]
    [SerializeField, Min(0f)] private float flakesPerSecond = 900f;
    [Tooltip("Sideways drift (m/s) the wind gives the flakes at the Moss Wind's usual strength (they drift more than rain).")]
    [SerializeField, Min(0f)] private float flakeDrift = 1.6f;
    [Tooltip("Streaks per second in a downpour.")]
    [SerializeField, Min(0f)] private float dropsPerSecond = 3000f;
    [Tooltip("Splashes per second in a downpour.")]
    [SerializeField, Min(0f)] private float splashesPerSecond = 500f;
    [Tooltip("Height above the ground the rain falls from (metres).")]
    [SerializeField, Min(1f)] private float height = 12f;
    [Tooltip("Sideways speed (m/s) the wind gives the rain at the Moss Wind's usual strength.")]
    [SerializeField, Min(0f)] private float windSlant = 3f;
    [Header("Read from above (Docs/LookAndFeelResearch_2026-11-18.md V3)")]
    [Tooltip("The share of the splashes that land within Near Reach of the player, where the eye is: from 20 m up the streaks barely " +
             "read, the ground boiling with splashes does.")]
    [SerializeField, Range(0f, 1f)] private float nearShare = 0.5f;
    [SerializeField, Min(1f)] private float nearReach = 6f;
    [Tooltip("Streaks falling close in front of the camera, a thin sheet of rain between it and the ground (a copy of the drops, " +
             "fainter and shorter), per second in a downpour. 0: none.")]
    [SerializeField, Min(0f)] private float sheetPerSecond = 220f;
    [Tooltip("Metres in front of the camera the sheet falls, and how wide, tall and deep it is.")]
    [SerializeField, Min(0.5f)] private float sheetDistance = 5f;
    [SerializeField] private Vector3 sheetSize = new Vector3(9f, 6f, 5f);

    private static readonly int RainId = Shader.PropertyToID("_SafeHavenRain");
    private static readonly int WindId = Shader.PropertyToID("_SafeHavenWind");
    private float intensity;
    private bool asSnow;
    private float splashRate;
    private bool playSetUp;
    private RainDrips drips;
    private float splashCarry;
    private float splashReach;
    private ParticleSystem.EmitParams splashAt;
    private ParticleSystem sheet;

    public float Intensity => intensity;

    /// <summary>Whether it falls as snow now.</summary>
    public bool AsSnow => asSnow;

    /// <summary>0 = dry, 1 = downpour (or a heavy snowfall, with <paramref name="snow"/>).</summary>
    public void SetRain(float amount, bool snow = false)
    {
        intensity = Mathf.Clamp01(amount);
        asSnow = snow && flakes != null;
        float rainfall = asSnow ? 0f : intensity;
        Shader.SetGlobalFloat(RainId, rainfall);
        SetRate(drops, rainfall * dropsPerSecond);
        // Splashes are placed one by one in play (LateUpdate), on whatever each drop hits; the box only in the editor.
        splashRate = rainfall * splashesPerSecond;
        if (Application.isPlaying)
        {
            SetUpPlay();
        }

        SetRate(splashes, Application.isPlaying ? 0f : splashRate);
        if (Application.isPlaying && splashes != null && splashRate > 0f && !splashes.isPlaying)
        {
            splashes.Play();
        }
        SetRate(flakes, asSnow ? intensity * flakesPerSecond : 0f);
        SetRate(sheet, rainfall * sheetPerSecond);
    }

    public void Configure(ParticleSystem snowflakes, float perSecond, float drift)
    {
        flakes = snowflakes;
        flakesPerSecond = perSecond;
        flakeDrift = drift;
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(RainId, 0f);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        SetUpPlay();
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 ground = GroundInView(camera);
        if (drops != null)
        {
            drops.transform.position = ground + Vector3.up * height;
            Vector4 wind = Shader.GetGlobalVector(WindId);
            Vector2 slant = new Vector2(wind.x, wind.y) * (windSlant * wind.z / 0.045f);
            ParticleSystem.VelocityOverLifetimeModule velocity = drops.velocityOverLifetime;
            velocity.x = slant.x;
            velocity.z = slant.y;
        }

        if (splashes != null && splashRate > 0f && !asSnow)
        {
            Splash(ground);
        }

        if (sheet != null)
        {
            // Just in front of the lens, turned with it: the sheet is seen falling across the view whatever the camera does.
            Transform view = camera.transform;
            sheet.transform.SetPositionAndRotation(view.position + view.forward * sheetDistance, Quaternion.Euler(0f, view.eulerAngles.y, 0f));
        }

        if (flakes != null && asSnow)
        {
            flakes.transform.position = ground + Vector3.up * height;
            Vector4 wind = Shader.GetGlobalVector(WindId);
            Vector2 drift = new Vector2(wind.x, wind.y) * (flakeDrift * wind.z / 0.045f);
            ParticleSystem.VelocityOverLifetimeModule velocity = flakes.velocityOverLifetime;
            velocity.x = drift.x;
            velocity.z = drift.y;
        }
    }

    // In play only, once: ExecuteAlways runs Awake in the editor, and entering Play may not run it again.
    private void SetUpPlay()
    {
        if (playSetUp && drips != null)
        {
            return;
        }

        playSetUp = true;
        if (splashes != null)
        {
            ParticleSystem.MainModule main = splashes.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            splashReach = Mathf.Max(splashes.shape.scale.x, splashes.shape.scale.z) * 0.5f;
            ParticleSystem.EmissionModule emission = splashes.emission;
            emission.rateOverTime = 0f;
        }

        if (sheet == null && drops != null && sheetPerSecond > 0f)
        {
            sheet = MakeSheet(drops);
            SetRate(sheet, (asSnow ? 0f : intensity) * sheetPerSecond);
        }

        drips = GetComponent<RainDrips>();
        if (drips == null)
        {
            drips = gameObject.AddComponent<RainDrips>();
            drips.Initialise(this, drops != null ? drops.GetComponent<ParticleSystemRenderer>() : null);
        }
    }

    // The sheet: a copy of the drops, a few metres in front of the camera, fainter, thinner and short-lived, in its own space so it
    // stays with the lens.
    private ParticleSystem MakeSheet(ParticleSystem source)
    {
        ParticleSystem made = Instantiate(source, transform);
        made.name = "Rain Sheet";
        made.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = made.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        main.startSizeMultiplier = source.main.startSizeMultiplier * 0.6f;
        Color colour = source.main.startColor.color;
        colour.a *= 0.35f;
        main.startColor = colour;
        main.maxParticles = Mathf.CeilToInt(sheetPerSecond * 0.6f) + 16;
        ParticleSystem.ShapeModule shape = made.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0f, sheetSize.y * 0.5f, 0f);
        shape.scale = sheetSize;
        shape.rotation = Vector3.zero;
        // No splashes or collisions of its own: the ground's are the splashes'.
        ParticleSystem.CollisionModule collision = made.collision;
        collision.enabled = false;
        ParticleSystem.SubEmittersModule subs = made.subEmitters;
        subs.enabled = false;
        return made;
    }

    /// <summary>Where a splash lands, as an offset from the view's middle: a share within <paramref name="near"/> metres of the player
    /// (who stands near the middle), the rest anywhere within <paramref name="reach"/>. Pure but for its random numbers.</summary>
    public static Vector2 SplashOffset(Vector2 player, float share, float near, float reach)
    {
        return Random.value < share ? player + Random.insideUnitCircle * near : Random.insideUnitCircle * reach;
    }

    // Each drop's splash where it really lands: on a roof, a rock, a cart or the ground, never under a roof or on water
    // (which rings instead). Half of them near the player, where the eye is.
    private void Splash(Vector3 ground)
    {
        splashCarry += splashRate * Time.deltaTime;
        int count = Mathf.Min((int)splashCarry, 24);
        splashCarry -= (int)splashCarry;
        float reach = splashReach > 0f ? splashReach : 18f;
        Player player = Player.Current;
        Vector2 nearPlayer = player != null ? new Vector2(player.transform.position.x - ground.x, player.transform.position.z - ground.z) : Vector2.zero;
        for (int i = 0; i < count; i++)
        {
            Vector2 offset = SplashOffset(nearPlayer, player != null ? nearShare : 0f, nearReach, reach);
            var from = new Vector3(ground.x + offset.x, ground.y + 30f, ground.z + offset.y);
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f, SplashLayers, QueryTriggerInteraction.Ignore))
            {
                SplashAt(hit.point, hit.normal);
            }
        }
    }

    /// <summary>Layers a splash can land on: not the UI, not Ignore Raycast.</summary>
    public const int SplashLayers = ~((1 << 2) | (1 << 5));

    /// <summary>One splash at a point (a drip landing, a drop): skipped where water stands over it.</summary>
    public void SplashAt(Vector3 point, Vector3 normal)
    {
        if (splashes == null || (WaterSurface.TryFind(point, 0.5f, out float water) && water > point.y - 0.05f))
        {
            return;
        }

        splashAt.position = point + normal * 0.03f;
        splashAt.velocity = normal * Random.Range(0.5f, 1.2f) + (Vector3)(Random.insideUnitCircle * 0.3f);
        splashAt.applyShapeToPosition = false;
        splashes.Emit(splashAt, 1);
    }

    private static void SetRate(ParticleSystem system, float rate)
    {
        if (system == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = rate;
        if (Application.isPlaying && rate > 0f && !system.isPlaying)
        {
            system.Play();
        }
    }

    /// <summary>The point on the ground in the middle of the camera's view.</summary>
    public static Vector3 GroundInView(Camera camera)
    {
        Transform view = camera.transform;
        Terrain terrain = Terrains.Under(view.position);
        if (terrain != null && terrain.terrainData == null)
        {
            terrain = null;
        }

        float groundY = terrain != null ? terrain.transform.position.y + terrain.SampleHeight(view.position) : 0f;
        Vector3 forward = view.forward;
        Vector3 point = forward.y < -0.05f ? view.position + forward * ((view.position.y - groundY) / -forward.y) : view.position;
        if (terrain != null)
        {
            point.y = terrain.transform.position.y + terrain.SampleHeight(point);
        }

        return point;
    }
}
