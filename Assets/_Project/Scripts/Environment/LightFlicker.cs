using UnityEngine;

/// <summary>
/// Makes a fire's light breathe: intensity and range drift around their authored values on smooth noise, each light out
/// of step with the others; the colour slips from its warm yellow towards a deeper orange as it dims, and the light
/// shifts a little in the flames, so shadows move. Optionally embers rise from it now and then (Resources/Fire Sparks):
/// glowing, trailing, a few lighting what they pass; and with them the air above shimmers in the heat (Resources/Heat Haze).
/// The flames themselves are the Fire shader (Safe Haven/Fire). Put it on the Light of a torch or campfire.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
[OnAFriendsCopy(CopyRule.Same, "a flickering light")]
public sealed class LightFlicker : MonoBehaviour
{
    [Tooltip("How far intensity strays from its authored value (share).")]
    [SerializeField, Range(0f, 1f)] private float intensityVariation = 0.25f;
    [Tooltip("How far range strays from its authored value (share).")]
    [SerializeField, Range(0f, 0.5f)] private float rangeVariation = 0.06f;
    [Tooltip("Flickers per second, roughly.")]
    [SerializeField, Min(0.1f)] private float speed = 4f;
    [Tooltip("The colour it sinks towards as it dims (its own colour is the bright one). Alpha: how far.")]
    [SerializeField] private Color dimColour = new Color(1f, 0.5f, 0.18f, 0.35f);
    [Tooltip("Metres the light wanders in the flames (shadows move with it). 0: still.")]
    [SerializeField, Range(0f, 0.2f)] private float wander = 0.03f;
    [Tooltip("Sparks rising from the fire.")]
    [SerializeField] private bool sparks;
    [Tooltip("Sparks per second.")]
    [SerializeField, Min(0f)] private float sparksPerSecond = 3f;

    // A fire this far from the camera holds still at its authored light: its flicker can't be told from there, and many
    // torches each animating their light every frame add up. Looked at a few times a second, out of step with the others.
    private const float FlickerDistance = 60f;
    private const float DistanceCheckSeconds = 0.5f;

    private Light fire;
    private float nextDistanceCheck;
    private bool far;
    private float baseIntensity;
    private float baseRange;
    private Color baseColour;
    private Vector3 basePosition;
    private float seed;
    private ParticleSystem rising;
    private static Material sparkMaterial;
    private static bool sparkLooked;
    private static Material hazeMaterial;
    private static bool hazeLooked;

    public void Configure(Color dim, float wanders, bool withSparks, float perSecond)
    {
        dimColour = dim;
        wander = wanders;
        sparks = withSparks;
        sparksPerSecond = perSecond;
    }

    /// <summary>The flicker at a moment: -1 dim to 1 bright, a slow swell and a quick flutter.</summary>
    public static float Wave(float t, float seed) =>
        (Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 3.1f, seed + 17f) * 0.3f) * 2f - 1f;

    private void Awake()
    {
        fire = GetComponent<Light>();
        baseIntensity = fire.intensity;
        baseRange = fire.range;
        baseColour = fire.color;
        basePosition = transform.localPosition;
        seed = Random.value * 100f;
        nextDistanceCheck = Time.time + Random.value * DistanceCheckSeconds;
        // Only a light on its own child wanders: never the torch, the arrow or the building it belongs to.
        if (transform.parent == null || GetComponent<Renderer>() != null || GetComponent<Collider>() != null || GetComponent<Rigidbody>() != null)
        {
            wander = 0f;
        }
    }

    private void Start()
    {
        if (sparks && Application.isPlaying)
        {
            rising = Sparks();
            Haze();
        }
    }

    private void OnDisable()
    {
        Settle();
        far = false;
    }

    private void Settle()
    {
        if (fire != null)
        {
            fire.intensity = baseIntensity;
            fire.range = baseRange;
            fire.color = baseColour;
            transform.localPosition = basePosition;
        }
    }

    private void Update()
    {
        if (Time.time >= nextDistanceCheck)
        {
            nextDistanceCheck = Time.time + DistanceCheckSeconds;
            Camera view = Camera.main;
            bool nowFar = view != null && (view.transform.position - transform.position).sqrMagnitude > FlickerDistance * FlickerDistance;
            if (nowFar && !far)
            {
                Settle();
            }

            far = nowFar;
        }

        if (far)
        {
            return;
        }

        float t = Time.time * speed + seed;
        float wave = Wave(t, seed);
        fire.intensity = baseIntensity * (1f + wave * intensityVariation);
        fire.range = baseRange * (1f + wave * rangeVariation);
        fire.color = Color.Lerp(baseColour, new Color(dimColour.r, dimColour.g, dimColour.b), Mathf.Clamp01(-wave) * dimColour.a);
        if (wander > 0f)
        {
            transform.localPosition = basePosition + new Vector3(Mathf.PerlinNoise(t * 0.7f, seed + 3f) - 0.5f,
                Mathf.PerlinNoise(t * 0.9f, seed + 5f) - 0.5f, Mathf.PerlinNoise(t * 0.8f, seed + 9f) - 0.5f) * (wander * 2f);
        }
    }

    // A few bright motes drifting up and winking out, made in code so every fire can have them.
    private ParticleSystem Sparks()
    {
        if (!sparkLooked)
        {
            sparkLooked = true;
            sparkMaterial = Resources.Load<Material>("Fire Sparks");
        }

        if (sparkMaterial == null)
        {
            return null;
        }

        var go = new GameObject("Sparks");
        go.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
        go.transform.localPosition = basePosition;
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.09f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.45f), new Color(1f, 0.55f, 0.2f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 24;
        main.gravityModifier = -0.05f;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = sparksPerSecond;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        ParticleSystem.NoiseModule drift = system.noise;
        drift.enabled = true;
        drift.strength = 0.5f;
        drift.frequency = 1.2f;
        drift.quality = ParticleSystemNoiseQuality.Low;
        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        // Embers streak a little as they rise, and one in a few carries a small light over the ground and walls round it.
        ParticleSystem.TrailModule trails = system.trails;
        trails.enabled = true;
        trails.ratio = 0.6f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
        trails.minVertexDistance = 0.02f;
        trails.dieWithParticles = true;
        var lightGo = new GameObject("Ember Light");
        lightGo.transform.SetParent(go.transform, false);
        var emberLight = lightGo.AddComponent<Light>();
        emberLight.type = LightType.Point;
        emberLight.range = 1.4f;
        emberLight.intensity = 1.2f;
        emberLight.color = new Color(1f, 0.55f, 0.2f);
        emberLight.shadows = LightShadows.None;
        lightGo.SetActive(false);
        ParticleSystem.LightsModule lights = system.lights;
        lights.enabled = true;
        lights.light = emberLight;
        lights.ratio = 0.15f;
        lights.maxLights = 3;
        lights.useParticleColor = true;
        lights.alphaAffectsIntensity = true;
        var drawn = go.GetComponent<ParticleSystemRenderer>();
        drawn.sharedMaterial = sparkMaterial;
        drawn.trailMaterial = sparkMaterial;
        drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        drawn.receiveShadows = false;
        system.Play();
        return system;
    }

    // The air above the flames shimmering: a camera-facing quad that redraws what is behind it through a rising ripple.
    private void Haze()
    {
        if (!hazeLooked)
        {
            hazeLooked = true;
            hazeMaterial = Resources.Load<Material>("Heat Haze");
        }

        if (hazeMaterial == null)
        {
            return;
        }

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(quad.GetComponent<Collider>());
        quad.name = "Heat Haze";
        quad.transform.SetParent(transform.parent != null ? transform.parent : transform, false);
        float width = Mathf.Clamp(baseRange * 0.14f, 0.35f, 2f);
        float height = width * 1.8f;
        quad.transform.localPosition = basePosition + Vector3.up * (height * 0.4f);
        quad.transform.localScale = new Vector3(width, height, 1f);
        var drawn = quad.GetComponent<MeshRenderer>();
        drawn.sharedMaterial = hazeMaterial;
        drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        drawn.receiveShadows = false;
    }
}
