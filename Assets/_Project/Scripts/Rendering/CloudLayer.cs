using UnityEngine;

/// <summary>
/// The clouds' state, one for the whole game: how much of the sky they cover (from the weather), how far they have
/// drifted with the wind, and how closed the sheet is under heavy weather. CloudShadows casts them on the ground and the
/// sky (Sky.shader) draws them overhead from the same numbers and the same pattern (CloudNoise.hlsl), so the two agree.
/// Made by WorldRendering; numbers in RenderingSettings › Cloud shadows.
/// </summary>
[DefaultExecutionOrder(90)] // after WeatherSystem, before CloudShadows and SkyDome
public sealed class CloudLayer : MonoBehaviour
{
    private static readonly int CloudsId = Shader.PropertyToID("_SkyClouds");
    private static readonly int OffsetId = Shader.PropertyToID("_SafeHavenCloudOffset");
    private static readonly int WindId = Shader.PropertyToID("_SafeHavenWind");

    private RenderingSettings settings;
    private Vector2 offset;

    /// <summary>The weather's cloud, 0 clear to 1 all cloud.</summary>
    public float Clouds { get; private set; }

    /// <summary>Share of the sky (and the ground) under cloud while there are still gaps.</summary>
    public float Cover { get; private set; }

    /// <summary>0 broken cloud, 1 a closed sheet: no shapes, so no cloud shadows.</summary>
    public float Overcast { get; private set; }

    /// <summary>The drift in metres, within one tile (RenderingSettings › Cloud Size).</summary>
    public Vector2 Offset => offset;

    public void Initialise(RenderingSettings rules) => settings = rules;

    private void LateUpdate()
    {
        WeatherSystem weather = WeatherSystem.Active;
        Clouds = weather != null ? weather.Current.Clouds : 0f;
        Vector2 range = settings.CloudCover;
        Cover = Mathf.Lerp(range.x, range.y, Mathf.Clamp01(Clouds * 2f));
        Overcast = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.8f, Clouds)); // rain (0.8) and storms close it
        float storm = weather != null ? Mathf.Clamp01(weather.Current.Rain * Clouds) : 0f;

        Vector4 wind = Shader.GetGlobalVector(WindId);
        var direction = new Vector2(wind.x, wind.y);
        if (direction.sqrMagnitude < 1e-4f)
        {
            direction = new Vector2(0.8f, 0.6f);
        }

        float strength = Mathf.Max(0.3f, AtmosphereConditions.WeatherWind);
        offset += direction.normalized * (settings.CloudSpeed * strength * Time.deltaTime);
        offset.x = Mathf.Repeat(offset.x, settings.CloudSize);
        offset.y = Mathf.Repeat(offset.y, settings.CloudSize);

        Shader.SetGlobalVector(CloudsId, new Vector4(Clouds, Cover, Overcast, storm));
        Shader.SetGlobalVector(OffsetId, new Vector4(offset.x / settings.CloudSize, offset.y / settings.CloudSize, 0f, 0f));
    }
}
