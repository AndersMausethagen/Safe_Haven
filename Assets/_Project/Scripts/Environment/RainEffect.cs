using UnityEngine;

/// <summary>Rain around the camera: falling streaks from a box above the ground the camera looks at, and small splashes
/// on that ground, both scaled by the intensity <see cref="WeatherSystem"/> sets. The streaks slant with the scene's
/// Moss Wind. Also sets _SafeHavenRain (0..1) for shaders (rain rings on water).</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class RainEffect : MonoBehaviour
{
    [SerializeField] private ParticleSystem drops;
    [SerializeField] private ParticleSystem splashes;
    [Tooltip("Streaks per second in a downpour.")]
    [SerializeField, Min(0f)] private float dropsPerSecond = 3000f;
    [Tooltip("Splashes per second in a downpour.")]
    [SerializeField, Min(0f)] private float splashesPerSecond = 500f;
    [Tooltip("Height above the ground the rain falls from (metres).")]
    [SerializeField, Min(1f)] private float height = 12f;
    [Tooltip("Sideways speed (m/s) the wind gives the rain at the Moss Wind's usual strength.")]
    [SerializeField, Min(0f)] private float windSlant = 3f;

    private static readonly int RainId = Shader.PropertyToID("_SafeHavenRain");
    private static readonly int WindId = Shader.PropertyToID("_SafeHavenWind");
    private float intensity;

    public float Intensity => intensity;

    /// <summary>0 = dry, 1 = downpour.</summary>
    public void SetRain(float amount)
    {
        intensity = Mathf.Clamp01(amount);
        Shader.SetGlobalFloat(RainId, intensity);
        SetRate(drops, intensity * dropsPerSecond);
        SetRate(splashes, intensity * splashesPerSecond);
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

        if (splashes != null)
        {
            splashes.transform.position = ground + Vector3.up * 0.05f;
        }
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
        Terrain terrain = Terrain.activeTerrain;
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
