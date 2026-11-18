using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Life in a place's air between fights (Docs/LookAndFeelResearch_2026-11-18.md V2, V4), from its <see cref="AmbientLifeProfile"/>:
/// each swarm a particle system kept round what the camera looks at (set down over the ground, over open water or round a light,
/// alone or gathered in columns, drifting with the wind and wandering), debris a gust carries across the view from upwind, and now and
/// then a big bird's shadow sliding over the ground (a shadow-only mesh flying high, flapping). Fewer by night or in rain as the
/// profile says. Shader: Shaders/AmbientMote.shader.
/// <para>Where it belongs: one per raid scene (and arena), with the biome's profile. Purely visual; nothing is saved.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class AmbientLife : MonoBehaviour
{
    private const int MostPerFrame = 6;
    private const float WindEvery = 1f;
    private static readonly int WindId = Shader.PropertyToID("_SafeHavenWind");

    [SerializeField] private AmbientLifeProfile profile;

    private sealed class Live
    {
        public AmbientLifeProfile.Swarm swarm;
        public ParticleSystem system;
        public readonly List<Vector3> columns = new List<Vector3>();
        public float nextGust;
        public float columnsAt;
    }

    private sealed class Bird
    {
        public Transform body;
        public Vector3 from;
        public Vector3 heading;
        public float speed;
        public float travelled;
        public float length;
        public float flap;
    }

    private readonly List<Live> live = new List<Live>();
    private readonly List<Bird> birds = new List<Bird>();
    private float nextWind;
    private float nextBirds;
    private Mesh birdMesh;

    public AmbientLifeProfile Profile => profile;

    public void Configure(AmbientLifeProfile with) => profile = with;

    private void Start()
    {
        if (profile == null)
        {
            enabled = false;
            return;
        }

        foreach (AmbientLifeProfile.Swarm swarm in profile.Swarms)
        {
            if (swarm != null && swarm.material != null)
            {
                live.Add(new Live { swarm = swarm, system = MakeSystem(swarm), nextGust = Time.time + Random.Range(1f, 4f) });
            }
        }

        nextBirds = Time.time + Random.Range(6f, 20f);
    }

    private ParticleSystem MakeSystem(AmbientLifeProfile.Swarm swarm)
    {
        var holder = new GameObject(swarm.name);
        holder.transform.SetParent(transform, false);
        ParticleSystem system = holder.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(swarm.life.x, swarm.life.y);
        main.startSize = new ParticleSystem.MinMaxCurve(swarm.size.x, swarm.size.y);
        main.startColor = new ParticleSystem.MinMaxGradient(swarm.colour, swarm.colour2);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = Mathf.Max(swarm.byDay, swarm.byNight, swarm.gusts ? swarm.perGust * 3 : 0) + MostPerFrame;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;
        if (swarm.wander > 0f)
        {
            ParticleSystem.NoiseModule noise = system.noise;
            noise.enabled = true;
            // Mostly across: a dragonfly darting down through the water or a mote sinking into the ground reads wrong.
            noise.separateAxes = true;
            noise.strengthX = swarm.wander;
            noise.strengthY = swarm.wander * 0.15f;
            noise.strengthZ = swarm.wander;
            noise.frequency = swarm.wanderFrequency;
            noise.scrollSpeed = swarm.wanderFrequency * 0.5f;
            noise.octaveCount = 1;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        if (swarm.spin > 0f)
        {
            ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-swarm.spin * Mathf.Deg2Rad, swarm.spin * Mathf.Deg2Rad);
        }

        // Fade in and out over each one's life, so none pops.
        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        fade.color = new ParticleSystem.MinMaxGradient(gradient);
        ParticleSystem.VelocityOverLifetimeModule drift = system.velocityOverLifetime;
        drift.enabled = !swarm.gusts && swarm.withWind > 0f;
        drift.space = ParticleSystemSimulationSpace.World;
        var renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = swarm.material;
        renderer.renderMode = swarm.lieFlat ? ParticleSystemRenderMode.HorizontalBillboard : ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        system.Play();
        return system;
    }

    private void Update()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 centre = RainEffect.GroundInView(camera);
        Vector4 wind = Shader.GetGlobalVector(WindId);
        var windDir = new Vector2(wind.x, wind.y);
        windDir = windDir.sqrMagnitude > 1e-4f ? windDir.normalized : new Vector2(0.6f, 0.8f);
        float windSpeed = Mathf.Max(wind.w, 0.5f);
        bool windNow = Time.time >= nextWind;
        if (windNow)
        {
            nextWind = Time.time + WindEvery;
        }

        float night = AtmosphereConditions.Night, rain = AtmosphereConditions.WeatherRain;
        foreach (Live swarm in live)
        {
            if (swarm.system == null)
            {
                continue;
            }

            if (windNow && !swarm.swarm.gusts && swarm.swarm.withWind > 0f)
            {
                ParticleSystem.VelocityOverLifetimeModule drift = swarm.system.velocityOverLifetime;
                float drifting = windSpeed * swarm.swarm.withWind;
                drift.x = windDir.x * drifting;
                drift.y = 0f;
                drift.z = windDir.y * drifting;
            }

            if (swarm.swarm.gusts)
            {
                Gust(swarm, centre, windDir, windSpeed, rain);
            }
            else
            {
                Keep(swarm, centre, night, rain);
            }
        }

        UpdateBirds(centre, windDir, night, rain);
    }

    // Keeps a swarm at its count round the view.
    private void Keep(Live swarm, Vector3 centre, float night, float rain)
    {
        int missing = AmbientLifeProfile.CountFor(swarm.swarm, night, rain) - swarm.system.particleCount;
        if (missing <= 0)
        {
            return;
        }

        if (swarm.swarm.columns > 0)
        {
            RefreshColumns(swarm, centre);
            if (swarm.columns.Count == 0)
            {
                return;
            }
        }

        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < Mathf.Min(missing, MostPerFrame); i++)
        {
            Vector3 at;
            if (swarm.swarm.columns > 0)
            {
                Vector3 column = swarm.columns[Random.Range(0, swarm.columns.Count)];
                Vector2 round = Random.insideUnitCircle * swarm.swarm.columnRadius;
                at = column + new Vector3(round.x, Random.Range(swarm.swarm.height.x, swarm.swarm.height.y), round.y);
            }
            else if (!TryPlace(swarm.swarm, centre, out at))
            {
                continue;
            }

            emit.position = at;
            swarm.system.Emit(emit, 1);
        }
    }

    // Columns stand where the swarm's place is, re-chosen when the view has gone on or every half minute.
    private void RefreshColumns(Live swarm, Vector3 centre)
    {
        float reach = swarm.swarm.radius;
        // Columns the view has left behind go (a loop: a RemoveAll lambda would allocate its closure each frame).
        for (int i = swarm.columns.Count - 1; i >= 0; i--)
        {
            Vector3 c = swarm.columns[i];
            if (new Vector2(c.x - centre.x, c.z - centre.z).sqrMagnitude > reach * reach * 1.4f)
            {
                swarm.columns.RemoveAt(i);
            }
        }
        if (Time.time > swarm.columnsAt)
        {
            swarm.columnsAt = Time.time + 30f;
            if (swarm.columns.Count > 0)
            {
                swarm.columns.RemoveAt(0);
            }
        }

        for (int tries = 0; swarm.columns.Count < swarm.swarm.columns && tries < 4; tries++)
        {
            if (TryPlace(swarm.swarm, centre, out Vector3 at, false))
            {
                swarm.columns.Add(at);
            }
        }
    }

    // A point for one of a swarm, by its place: over the ground, over open water, or round a light. False when none was found.
    private static bool TryPlace(AmbientLifeProfile.Swarm swarm, Vector3 centre, out Vector3 at, bool lifted = true)
    {
        float up = lifted ? Random.Range(swarm.height.x, swarm.height.y) : 0f;
        switch (swarm.place)
        {
            case AmbientLifeProfile.Place.NearLights:
                if (CatchableFireflies.TryLightNear(centre, swarm.radius, out Vector3 light))
                {
                    at = light + Random.insideUnitSphere * 0.9f + Vector3.up * (up - (swarm.height.x + swarm.height.y) * 0.5f);
                    return true;
                }

                at = default;
                return false;
            case AmbientLifeProfile.Place.OverWater:
                for (int i = 0; i < 4; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * swarm.radius;
                    var probe = new Vector3(centre.x + offset.x, centre.y, centre.z + offset.y);
                    if (OpenWaterAt(probe, out Vector3 water))
                    {
                        at = water + Vector3.up * up;
                        return true;
                    }
                }

                at = default;
                return false;
            default:
                Vector2 round = Random.insideUnitCircle * swarm.radius;
                var point = new Vector3(centre.x + round.x, 0f, centre.z + round.y);
                at = new Vector3(point.x, Fireflies.GroundUnder(point, centre.y) + up, point.z);
                return true;
        }
    }

    /// <summary>Open water at a point: a surface whose water shows there (not under the ground, not frozen), within a metre of it.</summary>
    public static bool OpenWaterAt(Vector3 point, out Vector3 water)
    {
        water = default;
        Terrain terrain = Terrains.Under(point);
        foreach (WaterSurface surface in WaterSurface.Surfaces)
        {
            if (surface == null || surface.IsFrozen)
            {
                continue;
            }

            Vector3 near = surface.NearestPoint(point);
            if (new Vector2(near.x - point.x, near.z - point.z).sqrMagnitude < 1f && !PlacedAmbience.Buried(terrain, near))
            {
                water = near;
                return true;
            }
        }

        return false;
    }

    // A gust's worth, from upwind across the view at the speed the grass's waves roll (Grass.shader: about five metres a second
    // at the wind's speed), low over the ground, as one event across the frame.
    private void Gust(Live swarm, Vector3 centre, Vector2 windDir, float windSpeed, float rain)
    {
        if (Time.time < swarm.nextGust)
        {
            return;
        }

        swarm.nextGust = Time.time + Random.Range(swarm.swarm.gustEvery.x, swarm.swarm.gustEvery.y) * Mathf.Lerp(1f, 1.6f, rain);
        float speed = Mathf.Clamp(windSpeed * 4f, 3f, 9f);
        var along = new Vector3(windDir.x, 0f, windDir.y);
        var side = new Vector3(-windDir.y, 0f, windDir.x);
        float reach = swarm.swarm.radius;
        float life = reach * 2f / speed;
        Vector3 front = centre - along * reach * 0.9f + side * Random.Range(-reach * 0.4f, reach * 0.4f);
        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false, startLifetime = life };
        for (int i = 0; i < swarm.swarm.perGust; i++)
        {
            Vector3 point = front + side * Random.Range(-reach * 0.5f, reach * 0.5f) - along * Random.Range(0f, 3f);
            emit.position = new Vector3(point.x, Fireflies.GroundUnder(point, centre.y) + Random.Range(swarm.swarm.height.x, swarm.swarm.height.y), point.z);
            emit.velocity = along * speed * Random.Range(0.8f, 1.15f) + Vector3.up * Random.Range(0f, 0.5f);
            swarm.system.Emit(emit, 1);
        }
    }

    // Now and then by day a bird (or two) flies over, too high to see: only its shadow slides over the ground, flapping.
    private void UpdateBirds(Vector3 centre, Vector2 windDir, float night, float rain)
    {
        AmbientLifeProfile.Birds settings = profile.BirdShadows;
        for (int i = birds.Count - 1; i >= 0; i--)
        {
            Bird bird = birds[i];
            bird.travelled += bird.speed * Time.deltaTime;
            if (bird.travelled > bird.length || bird.body == null)
            {
                if (bird.body != null)
                {
                    Destroy(bird.body.gameObject);
                }

                birds.RemoveAt(i);
                continue;
            }

            bird.flap += Time.deltaTime * 5.5f;
            // Glides between bouts of flapping.
            float flapping = Mathf.Sin(bird.flap) * Mathf.Clamp01(Mathf.Sin(bird.flap * 0.17f) * 2f);
            bird.body.position = bird.from + bird.heading * bird.travelled;
            bird.body.localScale = new Vector3(settings.span * (0.82f + 0.18f * flapping), 1f, settings.span);
        }

        Light sun = RenderSettings.sun;
        if (!settings.enabled || settings.shadowMaterial == null || night > 0.5f || rain > 0.6f || Time.time < nextBirds || sun == null)
        {
            return;
        }

        nextBirds = Time.time + Random.Range(settings.every.x, settings.every.y);
        Vector3 light = sun.transform.forward;
        if (light.y > -0.25f)
        {
            return; // the sun too low: the shadow would fall far off and smeared
        }
        int flock = Random.Range(1, settings.mostInFlock + 1);
        float angle = Random.Range(0f, Mathf.PI * 2f);
        var heading = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        var side = new Vector3(-heading.z, 0f, heading.x);
        float speed = Random.Range(settings.speed.x, settings.speed.y);
        float altitude = Random.Range(settings.altitude.x, settings.altitude.y);
        // Aimed so its shadow, not the bird, crosses the view: the sun throws it altitude / tan(elevation) off (fifty metres at 15:00).
        var thrown = new Vector3(light.x, 0f, light.z) * (altitude / -light.y);
        Vector3 start = centre - thrown - heading * 70f + side * Random.Range(-6f, 6f);
        for (int i = 0; i < flock; i++)
        {
            var body = new GameObject("Bird Shadow");
            body.transform.SetParent(transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = BirdMesh();
            var renderer = body.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = settings.shadowMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
            renderer.receiveShadows = false;
            Vector3 from = start + side * (i * 3.5f) - heading * (i * 2.5f);
            // Its height over the view's own ground (not over a valley wall it starts above), so the shadow lands where it was aimed.
            from.y = centre.y + altitude + i * 1.5f;
            body.transform.SetPositionAndRotation(from, Quaternion.LookRotation(heading, Vector3.up));
            birds.Add(new Bird { body = body.transform, from = from, heading = heading, speed = speed * Random.Range(0.95f, 1.05f), length = 140f, flap = Random.value * 6f });
        }
    }

    // A bird seen from below, one metre across (scaled by the span): long narrow wings bent at the wrist, a head ahead and a fanned tail,
    // as a raven's or an eagle's shadow reads from above; the wings' tips spread like fingers.
    private Mesh BirdMesh()
    {
        if (birdMesh != null)
        {
            return birdMesh;
        }

        var vertices = new[]
        {
            new Vector3(0f, 0f, 0.4f),                                       // 0 head
            new Vector3(-0.05f, 0f, 0.2f), new Vector3(0.05f, 0f, 0.2f),     // 1, 2 neck
            new Vector3(-0.27f, 0f, 0.13f), new Vector3(0.27f, 0f, 0.13f),   // 3, 4 wrists, leading edge
            new Vector3(-0.5f, 0f, 0.02f), new Vector3(0.5f, 0f, 0.02f),     // 5, 6 tips
            new Vector3(-0.44f, 0f, -0.08f), new Vector3(0.44f, 0f, -0.08f), // 7, 8 tips, trailing (the spread fingers)
            new Vector3(-0.25f, 0f, -0.07f), new Vector3(0.25f, 0f, -0.07f), // 9, 10 wrists, trailing
            new Vector3(-0.06f, 0f, -0.12f), new Vector3(0.06f, 0f, -0.12f), // 11, 12 wing roots, trailing
            new Vector3(-0.1f, 0f, -0.4f), new Vector3(0.1f, 0f, -0.4f),     // 13, 14 tail
        };
        var up = new[]
        {
            0, 1, 2,
            1, 11, 12, 1, 12, 2,
            1, 3, 9, 1, 9, 11, 3, 5, 7, 3, 7, 9,
            2, 10, 4, 2, 12, 10, 4, 8, 6, 4, 10, 8,
            11, 13, 14, 11, 14, 12,
        };
        var triangles = new int[up.Length * 2];
        for (int i = 0; i < up.Length; i += 3)
        {
            triangles[i] = up[i];
            triangles[i + 1] = up[i + 1];
            triangles[i + 2] = up[i + 2];
            // And wound the other way, so it casts from either side.
            triangles[up.Length + i] = up[i];
            triangles[up.Length + i + 1] = up[i + 2];
            triangles[up.Length + i + 2] = up[i + 1];
        }

        birdMesh = new Mesh { name = "Bird Shadow", vertices = vertices, triangles = triangles, hideFlags = HideFlags.DontSave };
        // Normals straight up (both windings share the vertices: averaged, they would cancel out and the shadow bias would break).
        var normals = new Vector3[vertices.Length];
        for (int i = 0; i < normals.Length; i++)
        {
            normals[i] = Vector3.up;
        }

        birdMesh.normals = normals;
        birdMesh.RecalculateBounds();
        return birdMesh;
    }

    private void OnDestroy()
    {
        if (birdMesh != null)
        {
            Destroy(birdMesh);
        }
    }
}
