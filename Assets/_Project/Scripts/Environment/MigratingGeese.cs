using UnityEngine;

/// <summary>
/// Geese going over in their seasons: now and then by day a skein of them flies high across the view in a V, calling,
/// with their shadows sweeping over the ground: south in autumn, north again in spring. Drawn with the scene's birds'
/// picture sheet (the Birds' particle material), bigger and paler, flapping slow.
/// <para>Where it belongs: once in the Haven scene (set up by AgentScripts/BuildSeasonalLife.cs).</para>
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class MigratingGeese : MonoBehaviour
{
    private const int Pictures = 8;
    private static readonly Color32 ShadowColour = new Color32(0, 0, 0, 40);

    [Tooltip("Seasons they fly over in, by the Season Cycle's names, and which way (south: autumn, north: spring).")]
    [SerializeField] private string[] southIn = { "Autumn", "Late Autumn" };
    [SerializeField] private string[] northIn = { "Spring" };
    [SerializeField] private Vector2 every = new Vector2(90f, 240f);
    [SerializeField] private Vector2 birds = new Vector2(7, 13);
    [SerializeField, Min(5f)] private float height = 24f;
    [SerializeField, Min(1f)] private float speed = 10f;
    [SerializeField, Min(0.2f)] private float size = 1.4f;
    [SerializeField] private Color colour = new Color(0.78f, 0.74f, 0.66f, 1f);
    [SerializeField] private SoundDefinition call;

    private ParticleSystem system;
    private ParticleSystem.Particle[] particles;
    private Vector3[] offsets;
    private int count;
    private Vector3 centre;
    private Vector3 heading;
    private float travelled;
    private float span;
    private float nextSkein;
    private float nextCall;
    private bool flying;

    public bool Flying => flying;

    public void Configure(SoundDefinition calling) => call = calling;

    private void Awake()
    {
        system = GetComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.simulationSpeed = 0f;
        main.maxParticles = 32;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = 4;
        sheet.numTilesY = 2;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.cycleCount = 1;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
        particles = new ParticleSystem.Particle[32];
        offsets = new Vector3[16];
        system.Play();
        nextSkein = Time.time + Random.Range(20f, every.y);
    }

    private void Start()
    {
        // The same picture sheet as the scene's other birds.
        var renderer = GetComponent<ParticleSystemRenderer>();
        if (renderer.sharedMaterial == null && Birds.Current != null && Birds.Current.TryGetComponent(out ParticleSystemRenderer birds))
        {
            renderer.sharedMaterial = birds.sharedMaterial;
        }
    }

    /// <summary>Where each goose flies in the V, from the leader (0), alternating sides, metres back and out.</summary>
    public static Vector3 PlaceInSkein(int index, Vector3 heading)
    {
        if (index == 0)
        {
            return Vector3.zero;
        }

        int rank = (index + 1) / 2;
        float side = index % 2 == 0 ? 1f : -1f;
        Vector3 across = Vector3.Cross(Vector3.up, heading);
        return -heading * (rank * 2.2f) + across * (side * rank * 1.9f);
    }

    private void Update()
    {
        Camera view = Camera.main;
        if (view == null)
        {
            return;
        }

        if (!flying && Time.time >= nextSkein)
        {
            nextSkein = Time.time + Random.Range(every.x, every.y);
            TryStart(view);
        }

        if (!flying)
        {
            return;
        }

        travelled += speed * Time.deltaTime;
        if (travelled > span)
        {
            flying = false;
            system.SetParticles(particles, 0);
            return;
        }

        if (call != null && Time.time >= nextCall)
        {
            nextCall = Time.time + Random.Range(2.5f, 6f);
            AudioManager.Play(call, Position(0));
        }

        Draw(view);
    }

    private void TryStart(Camera view)
    {
        string season = Seasons.Today;
        bool south = Has(southIn, season), north = Has(northIn, season);
        if ((!south && !north) || AtmosphereConditions.Night > 0.4f || Random.value > 0.6f)
        {
            return;
        }

        // Across the ground the camera looks at, from well out of sight to well past it.
        Vector3 look = RainEffect.GroundInView(view);
        float angle = (south ? 180f : 0f) + Random.Range(-35f, 35f);
        heading = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        span = 160f;
        centre = look - heading * (span * 0.5f) + Vector3.up * height + Vector3.Cross(Vector3.up, heading) * Random.Range(-20f, 20f);
        travelled = 0f;
        count = Mathf.Clamp(Mathf.RoundToInt(Random.Range(birds.x, birds.y)), 3, offsets.Length);
        for (int i = 0; i < count; i++)
        {
            offsets[i] = PlaceInSkein(i, heading) + Random.insideUnitSphere * 0.4f;
        }

        flying = true;
        nextCall = Time.time + 1f;
    }

    private Vector3 Position(int index) => centre + heading * travelled + offsets[index];

    private void Draw(Camera view)
    {
        Vector3 right = view.transform.right;
        Vector3 up = view.transform.up;
        float angle = Mathf.Atan2(Vector3.Dot(heading, right), Vector3.Dot(heading, up)) * Mathf.Rad2Deg;
        float fade = Mathf.Clamp01(Mathf.Min(travelled, span - travelled) / 20f);
        int drawn = 0;
        for (int i = 0; i < count; i++)
        {
            Vector3 at = Position(i);
            float flap = Time.time * 1.6f + i * 0.37f;
            int picture = Bird.FirstFlapFrame + Mathf.FloorToInt(Mathf.Repeat(flap, 1f) * Bird.FlapFrames) % Bird.FlapFrames;
            Color tint = colour * Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(AtmosphereConditions.Night));
            tint.a = fade;
            particles[drawn++] = Particle(at, size, tint, angle, picture);
            Terrain terrain = Terrains.Under(at);
            if (terrain != null)
            {
                Vector3 ground = at;
                ground.y = terrain.transform.position.y + terrain.SampleHeight(ground) + 0.05f;
                Color32 shadow = ShadowColour;
                shadow.a = (byte)(shadow.a * fade);
                particles[drawn++] = Particle(ground, size * 0.8f, shadow, angle, picture);
            }
        }

        system.SetParticles(particles, drawn);
    }

    private static bool Has(string[] names, string season)
    {
        if (string.IsNullOrEmpty(season) || names == null)
        {
            return false;
        }

        foreach (string name in names)
        {
            if (string.Equals(name, season, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ParticleSystem.Particle Particle(Vector3 position, float size, Color32 colour, float angle, int picture) => new ParticleSystem.Particle
    {
        position = position,
        startSize = size,
        startColor = colour,
        rotation = angle,
        startLifetime = 1f,
        remainingLifetime = 1f - (picture + 0.5f) / Pictures,
    };
}
