using UnityEngine;

/// <summary>
/// Fireflies around what the camera looks at: a few by day, many at night, fewer in the rain (FireflyProfile has the
/// numbers and the colour). Some gather round the fires, lamps and the player's lantern near the view. Each is set down at
/// its own height over the ground, so they follow hills that a fixed emitter box wouldn't; after that they wander and
/// blink by themselves and go out when their life ends. The few that can be caught are flown by
/// <see cref="CatchableFireflies"/> (added here when the profile names what catching one gives).
/// <para>
/// Where it belongs: the Fireflies prefab in a scene, with the place's profile. Purely visual.
/// </para>
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ParticleSystem))]
public sealed class Fireflies : MonoBehaviour
{
    private const int MostPerFrame = 6;

    [SerializeField] private FireflyProfile profile;

    private ParticleSystem system;

    private void Awake()
    {
        system = GetComponent<ParticleSystem>();
        if (profile == null)
        {
            enabled = false;
            return;
        }

        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(profile.Life.x, profile.Life.y);
        main.startSize = new ParticleSystem.MinMaxCurve(profile.Size.x, profile.Size.y);
        main.startColor = profile.Colour;
        main.maxParticles = profile.MostAtOnce + MostPerFrame;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = profile.Wander;
        noise.frequency = 0.22f;
        noise.scrollSpeed = 0.18f;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;
        ParticleSystem.ColorOverLifetimeModule blink = system.colorOverLifetime;
        blink.enabled = true;
        blink.color = BlinkOverLife();
        system.Play();
        if (profile.Catchable != null)
        {
            gameObject.AddComponent<CatchableFireflies>().Initialise(profile);
        }
    }

    // Alpha is how lit it is: three slow pulses, dark between them, so a swarm twinkles instead of just glowing.
    private static ParticleSystem.MinMaxGradient BlinkOverLife()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[]
            {
                new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.08f, 0.32f), new GradientAlphaKey(1f, 0.48f),
                new GradientAlphaKey(0.08f, 0.64f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f),
            });
        return new ParticleSystem.MinMaxGradient(gradient);
    }

    private void Update()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        int wanted = Mathf.RoundToInt(profile.CountFor(AtmosphereConditions.Night, AtmosphereConditions.WeatherRain) * Seasons.Fireflies);
        int missing = wanted - system.particleCount;
        if (missing <= 0)
        {
            return;
        }

        Vector3 center = RainEffect.GroundInView(camera);
        var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        for (int i = 0; i < Mathf.Min(missing, MostPerFrame); i++)
        {
            Vector3 point;
            if (Random.value < profile.LightShare && CatchableFireflies.TryLightNear(center, profile.Radius, out Vector3 light))
            {
                Vector2 round = Random.insideUnitCircle.normalized * Random.Range(profile.LightDistance.x, profile.LightDistance.y);
                point = light + new Vector3(round.x, 0f, round.y);
            }
            else
            {
                Vector2 offset = Random.insideUnitCircle * profile.Radius;
                point = center + new Vector3(offset.x, 0f, offset.y);
            }

            emit.position = new Vector3(point.x, GroundHeight(point, center.y) + Random.Range(profile.Height.x, profile.Height.y), point.z);
            system.Emit(emit, 1);
        }
    }

    /// <summary>The ground's height under a point (the terrain's), or the fallback off any terrain.</summary>
    public static float GroundUnder(Vector3 point, float fallback) => GroundHeight(point, fallback);

    private static float GroundHeight(Vector3 point, float fallback)
    {
        Terrain terrain = Terrains.Under(point);
        return terrain != null && terrain.terrainData != null ? terrain.transform.position.y + terrain.SampleHeight(point) : fallback;
    }
}
