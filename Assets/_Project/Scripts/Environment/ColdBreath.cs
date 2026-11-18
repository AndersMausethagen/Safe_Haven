using UnityEngine;

/// <summary>
/// Breath that shows in the cold: a small pale puff in front of the face every few breaths, quicker and bigger when
/// running, on frosty mornings, in hard frost and wherever snow lies. Only drawn near the camera. Added in code to the
/// player, residents and companions (<see cref="AddTo"/>); the puff's look is Resources/Cold Breath (a soft particle).
/// </summary>
[DisallowMultipleComponent]
public sealed class ColdBreath : MonoBehaviour
{
    private static readonly int SeasonId = Shader.PropertyToID("_SafeHavenSeason");
    private const float DrawnWithin = 40f;
    private static Material material;
    private static bool looked;

    private Transform head;
    private ParticleSystem puffs;
    private Vector3 lastPosition;
    private float nextBreath;

    public static void AddTo(GameObject breather)
    {
        if (breather != null && !breather.TryGetComponent(out ColdBreath _))
        {
            breather.AddComponent<ColdBreath>();
        }
    }

    /// <summary>
    /// How cold it is for breath to show (0 none to 1): lying snow, hard frost, or a light frost in the morning.
    /// </summary>
    public static float Coldness(float snow, float frost, float morning) =>
        Mathf.Clamp01(Mathf.Max(snow * 1.4f, Mathf.Max((frost - 0.1f) * 1.6f, frost * morning * 3f)));

    private void Start()
    {
        Animator animator = GetComponentInChildren<Animator>();
        head = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        lastPosition = transform.position;
        nextBreath = Time.time + Random.Range(0f, 3f);
    }

    private void Update()
    {
        float speed = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 1e-4f);
        lastPosition = transform.position;
        if (Time.time < nextBreath)
        {
            return;
        }

        bool running = speed > 3.5f;
        nextBreath = Time.time + (running ? Random.Range(1.2f, 1.7f) : Random.Range(2.8f, 3.8f));
        Camera camera = Camera.main;
        WeatherSystem weather = WeatherSystem.Active;
        Vector4 season = Shader.GetGlobalVector(SeasonId);
        float cold = Coldness(weather != null ? weather.Snow : 0f, season.z, AtmosphereConditions.Morning);
        if (cold <= 0.05f || camera == null || (camera.transform.position - transform.position).sqrMagnitude > DrawnWithin * DrawnWithin * 4f)
        {
            return;
        }

        if (puffs == null && !Build())
        {
            enabled = false;
            return;
        }

        Vector3 forward = transform.forward;
        Vector3 mouth = head != null ? head.position + forward * 0.14f - Vector3.up * 0.06f : transform.position + Vector3.up * 1.55f + forward * 0.2f;
        var emit = new ParticleSystem.EmitParams
        {
            position = mouth,
            applyShapeToPosition = true,
            velocity = forward * (running ? 0.7f : 0.4f) + Vector3.up * 0.12f,
            startColor = new Color(0.92f, 0.95f, 1f, 0.32f * cold),
            startSize = running ? 0.22f : 0.16f,
            startLifetime = running ? 1.1f : 1.4f,
        };
        puffs.Emit(emit, running ? 5 : 3);
    }

    private bool Build()
    {
        if (!looked)
        {
            looked = true;
            material = Resources.Load<Material>("Cold Breath");
        }

        if (material == null)
        {
            return false;
        }

        var go = new GameObject("Cold Breath");
        go.transform.SetParent(transform, false);
        puffs = go.AddComponent<ParticleSystem>();
        puffs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = puffs.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 40;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        ParticleSystem.EmissionModule emission = puffs.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = puffs.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.03f;
        ParticleSystem.SizeOverLifetimeModule size = puffs.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.6f));
        ParticleSystem.ColorOverLifetimeModule colour = puffs.colorOverLifetime;
        colour.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        colour.color = fade;
        ParticleSystem.VelocityOverLifetimeModule drag = puffs.velocityOverLifetime;
        drag.enabled = true;
        drag.space = ParticleSystemSimulationSpace.World;
        drag.speedModifier = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        puffs.Play();
        return true;
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        looked = false;
    }
}
