using UnityEngine;

/// <summary>
/// A lived-in building: smoke rising from its roof while it stands, drifting with the wind, and a warm light in it at night.
/// The GDD's hearth is what the lost see of home, and the Haven's ranks speak of "smoke from more than one chimney"; this is
/// that smoke. A building under construction or in ruins gives none. Smoke and light run only while the building is near
/// enough to be on screen, so a whole village costs nothing when the player is in the woods.
/// <para>Where it belongs: on a building prefab (Cottage, Hut, Kitchen, Smithy, Sauna...), set up by BuildLivingWorld.</para>
/// </summary>
[DisallowMultipleComponent]
public sealed class HearthSmoke : MonoBehaviour
{
    private static readonly int WindId = Shader.PropertyToID("_SafeHavenWind");

    [Tooltip("Where the smoke leaves the roof, from the building's root.")]
    [SerializeField] private Vector3 chimney = new Vector3(0.8f, 3.4f, 0.4f);
    [Tooltip("Puffs a second, and how high they rise before they are gone.")]
    [SerializeField, Min(0.1f)] private float puffs = 2.4f;
    [SerializeField, Min(1f)] private float rise = 9f;
    [SerializeField] private Material smoke;
    [Tooltip("The warm light seen in it at night (0 range: none), from the building's root.")]
    [SerializeField] private Vector3 lightAt = new Vector3(0f, 1.2f, 1.6f);
    [SerializeField, Min(0f)] private float lightRange = 6f;
    [SerializeField, Min(0f)] private float lightIntensity = 1.6f;
    [SerializeField] private Color lightColour = new Color(1f, 0.62f, 0.3f);
    [Tooltip("Beyond this many metres from the camera, no smoke is made and the light is off.")]
    [SerializeField, Min(10f)] private float drawnWithin = 70f;
    [Tooltip("Things about the place that say someone lives here (a washing line, tools by the door): shown only while it stands.")]
    [SerializeField] private GameObject livedIn;

    private Construction construction;
    private ParticleSystem system;
    private Light glow;
    private float flicker;

    public void Configure(Vector3 smokeFrom, Vector3 lightFrom, float range, Material material, GameObject props = null)
    {
        livedIn = props;
        chimney = smokeFrom;
        lightAt = lightFrom;
        lightRange = range;
        smoke = material;
    }

    private void Awake()
    {
        construction = GetComponent<Construction>();
        if (smoke != null)
        {
            system = MakeSmoke();
        }

        if (lightRange > 0f)
        {
            var holder = new GameObject("Hearth Light");
            holder.transform.SetParent(transform, false);
            holder.transform.localPosition = lightAt;
            glow = holder.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = lightRange;
            glow.color = lightColour;
            glow.shadows = LightShadows.None;
            glow.intensity = 0f;
            glow.enabled = false;
        }

        flicker = Random.value * 10f;
    }

    private ParticleSystem MakeSmoke()
    {
        var holder = new GameObject("Hearth Smoke");
        holder.transform.SetParent(transform, false);
        holder.transform.localPosition = chimney;
        var made = holder.AddComponent<ParticleSystem>();
        made.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = made.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(rise / 1.1f, rise / 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.1f, 1.7f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.maxParticles = Mathf.CeilToInt(puffs * rise * 1.6f);
        ParticleSystem.EmissionModule emission = made.emission;
        emission.rateOverTime = puffs;
        ParticleSystem.ShapeModule shape = made.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 6f;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        // Each puff swells and thins as it climbs.
        ParticleSystem.SizeOverLifetimeModule size = made.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 3.6f)));
        ParticleSystem.ColorOverLifetimeModule colour = made.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.1f), new GradientAlphaKey(0.35f, 0.55f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        ParticleSystem.RotationOverLifetimeModule spin = made.rotationOverLifetime;
        spin.enabled = true;
        spin.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        ParticleSystem.ForceOverLifetimeModule force = made.forceOverLifetime;
        force.enabled = true;
        force.space = ParticleSystemSimulationSpace.World;
        var drawn = holder.GetComponent<ParticleSystemRenderer>();
        drawn.sharedMaterial = smoke;
        drawn.renderMode = ParticleSystemRenderMode.Billboard;
        drawn.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        drawn.receiveShadows = false;
        drawn.sortingFudge = 2f;
        return made;
    }

    private void Update()
    {
        bool standing = construction == null || (construction.IsStanding && !construction.IsBuilding);
        if (livedIn != null && livedIn.activeSelf != standing)
        {
            livedIn.SetActive(standing);
        }

        Camera view = Camera.main;
        bool near = view != null && (view.transform.position - transform.position).sqrMagnitude < drawnWithin * drawnWithin;
        if (system != null)
        {
            bool smoking = standing && near;
            if (smoking && !system.isEmitting)
            {
                system.Play();
            }
            else if (!smoking && system.isEmitting)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (smoking)
            {
                Steer();
            }
        }

        if (glow != null)
        {
            // Lit from dusk, a little brighter and redder as the fire burns down, flickering as fire does.
            float dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, NightSettings.Darkness));
            float wanted = standing && near ? dark : 0f;
            flicker += Time.deltaTime * 7f;
            float flame = 0.86f + 0.14f * Mathf.PerlinNoise(flicker, 0.37f);
            glow.intensity = Mathf.MoveTowards(glow.intensity, wanted * lightIntensity * flame, Time.deltaTime * 2f);
            glow.enabled = glow.intensity > 0.01f;
        }
    }

    // The smoke leans with the world's wind and takes the sky's colour: pale grey by day, dark and blue at night.
    private void Steer()
    {
        Vector4 wind = Shader.GetGlobalVector(WindId);
        var direction = new Vector2(wind.x, wind.y);
        if (direction.sqrMagnitude < 1e-4f)
        {
            direction = new Vector2(0.7f, 0.25f);
        }

        direction.Normalize();
        float strength = 0.35f + 0.6f * Mathf.Clamp01(AtmosphereConditions.WeatherWind);
        ParticleSystem.ForceOverLifetimeModule force = system.forceOverLifetime;
        force.x = new ParticleSystem.MinMaxCurve(direction.x * strength * 0.7f, direction.x * strength * 1.3f);
        force.y = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        force.z = new ParticleSystem.MinMaxCurve(direction.y * strength * 0.7f, direction.y * strength * 1.3f);
        ParticleSystem.MainModule main = system.main;
        float night = Mathf.Clamp01(NightSettings.Darkness);
        main.startColor = Color.Lerp(new Color(0.78f, 0.77f, 0.75f), new Color(0.24f, 0.27f, 0.33f), night);
    }
}
