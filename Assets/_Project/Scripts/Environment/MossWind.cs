using UnityEngine;

/// <summary>One shared wind source per scene for Safe Haven vegetation shaders (Terrain Moss, Tree Wind).
/// With a Unity WindZone on the same object, its arrow, Main and Pulse Magnitude drive the wind, so particles
/// with External Forces follow the same wind. Does not move terrain or physics objects.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MossWind : MonoBehaviour
{
    [Tooltip("Optional Unity WindZone (Directional). When set, its arrow, Main and Pulse Magnitude replace Direction, " +
             "the strength scale and Gusts. Found on this object when empty.")]
    [SerializeField] private WindZone windZone;
    [Tooltip("Horizontal direction in degrees: 0 is world +Z, 90 is world +X. Ignored with a WindZone.")]
    [SerializeField, Range(0f, 360f)] private float direction = 35f;
    [Tooltip("Maximum sideways tip displacement in metres before the material's wind multiplier.")]
    [SerializeField, Range(0f, 0.2f)] private float strength = 0.045f;
    [SerializeField, Min(0f)] private float speed = 1.2f;
    [Tooltip("Gust depth for moss. Ignored with a WindZone (Pulse Magnitude is used).")]
    [SerializeField, Range(0f, 1f)] private float gusts = 0.35f;

    private static MossWind active;
    private float weatherStrength = 1f;
    private float weatherGusts;
    private static readonly int WindId = Shader.PropertyToID("_SafeHavenWind");
    private static readonly int GustId = Shader.PropertyToID("_SafeHavenWindGusts");
    private static readonly int TreeId = Shader.PropertyToID("_SafeHavenTreeWind");

    /// <summary>Shader globals for a wind setup: wind = (dir X, dir Z, moss metres, speed), gusts, tree strength.</summary>
    public static (Vector4 wind, float gusts, float tree) Evaluate(float direction, float strength, float speed,
        float gusts, WindZone zone)
    {
        if (zone == null || zone.mode != WindZoneMode.Directional)
        {
            float angle = direction * Mathf.Deg2Rad;
            return (new Vector4(Mathf.Sin(angle), Mathf.Cos(angle), strength, speed), gusts, 1f);
        }

        Vector3 forward = zone.transform.forward;
        Vector2 flat = new Vector2(forward.x, forward.z);
        flat = flat.sqrMagnitude > 1e-6f ? flat.normalized : new Vector2(0f, 1f);
        float main = Mathf.Max(0f, zone.windMain);
        return (new Vector4(flat.x, flat.y, strength * main, speed), Mathf.Clamp01(zone.windPulseMagnitude), main);
    }

    /// <summary>The weather's effect (WeatherSystem): multiplies the wind's strength (moss, trees, grass, water chop) and
    /// speeds it up a little, and adds gustiness. Runtime only, never saved.</summary>
    public void SetWeather(float strength, float gusts)
    {
        weatherStrength = Mathf.Max(0f, strength);
        weatherGusts = Mathf.Clamp01(gusts);
        if (active == this)
        {
            Publish();
        }
    }

    private void Reset()
    {
        windZone = GetComponent<WindZone>();
    }

    private void OnEnable()
    {
        if (active != null && active != this)
        {
#if UNITY_EDITOR
            // Scenes opened read-only by editor tools (e.g. the Sound Editor) are only flagged as preview
            // scenes after OnEnable, so decide one editor tick later.
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall += ReportDuplicate;
                return;
            }
#endif
            ReportDuplicate();
            return;
        }

        active = this;
        Publish();
    }

    private void Update()
    {
        if (active == this)
        {
            Publish();
        }
    }

    private void OnDisable()
    {
        if (active == this)
        {
            Shader.SetGlobalVector(WindId, Vector4.zero);
            Shader.SetGlobalFloat(GustId, 0f);
            Shader.SetGlobalFloat(TreeId, 0f);
            active = null;
        }
    }

    private void ReportDuplicate()
    {
        if (this == null || !isActiveAndEnabled || active == null || active == this)
        {
            return;
        }
#if UNITY_EDITOR
        if (UnityEditor.SceneManagement.EditorSceneManager.IsPreviewSceneObject(this))
        {
            return;
        }
#endif
        Debug.LogError("Only one enabled Moss Wind may drive the shared vegetation wind. Disable the extra source.", this);
    }

    private void Publish()
    {
        if (windZone == null)
        {
            windZone = GetComponent<WindZone>();
        }

        var (wind, gust, tree) = Evaluate(direction, strength, speed, gusts, windZone);
        wind.z *= weatherStrength;
        wind.w *= Mathf.Lerp(1f, weatherStrength, 0.4f);
        gust = Mathf.Clamp01(gust + weatherGusts);
        tree *= weatherStrength;
        Shader.SetGlobalVector(WindId, wind);
        Shader.SetGlobalFloat(GustId, gust);
        Shader.SetGlobalFloat(TreeId, tree);
    }
}
