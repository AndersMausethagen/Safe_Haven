using UnityEngine;

/// <summary>One kind of weather (Clear, Breezy, Mist, Rain, Storm...): how windy, wet, cloudy and foggy it is, its
/// lightning, and how long it lasts when a <see cref="ClimateDefinition"/> picks it. Read-only at runtime;
/// <see cref="WeatherSystem"/> blends between them.</summary>
[CreateAssetMenu(menuName = "Safe Haven/Environment/Weather", fileName = "Weather")]
public sealed class WeatherDefinition : ScriptableObject
{
    [Tooltip("Shown to players or in tools.")]
    [SerializeField] private string displayName = "Clear";
    [Header("Wind (multiplies the scene's Moss Wind)")]
    [SerializeField, Range(0f, 4f)] private float wind = 1f;
    [Tooltip("Extra gustiness on top of the Moss Wind's own.")]
    [SerializeField, Range(0f, 1f)] private float gusts;
    [Header("Sky")]
    [Tooltip("0 = none, 1 = downpour. Drives the rain particles, rain rings on water and part of the gloomy look.")]
    [SerializeField, Range(0f, 1f)] private float rain;
    [Tooltip("Dims the sun and softens shadows; drives the Overcast volume.")]
    [SerializeField, Range(0f, 1f)] private float clouds;
    [Tooltip("Multiplies the time of day's fog density.")]
    [SerializeField, Range(0f, 8f)] private float fog = 1f;
    [Tooltip("Independent ground-cloud amount (Terrain Mist), not a multiplier of global fog. Zero permits fog without mist; one gives the terrain's full authored mist density.")]
    [SerializeField, Range(0f, 1f)] private float mist;
    [Tooltip("Fog colour mixed in by its alpha (grey rain, white mist).")]
    [SerializeField] private Color fogTint = new Color(0.6f, 0.65f, 0.7f, 0f);
    [SerializeField, Min(0f)] private float lightningPerMinute;
    [Header("Duration when picked by a climate")]
    [SerializeField, Min(0.1f)] private float minMinutes = 4f;
    [SerializeField, Min(0.1f)] private float maxMinutes = 8f;

    public string DisplayName => displayName;
    public float MinMinutes => minMinutes;
    public float MaxMinutes => Mathf.Max(minMinutes, maxMinutes);

    public WeatherState State => new WeatherState
    {
        Wind = wind, Gusts = gusts, Rain = rain, Clouds = clouds, Fog = fog, Mist = mist, FogTint = fogTint, LightningPerMinute = lightningPerMinute
    };

    /// <summary>Sets every value at once (setup tools and tests).</summary>
    public void Configure(string name, float windScale, float gustiness, float rainAmount, float cloudCover, float fogScale,
        Color tint, float lightning, float shortest, float longest, float mistAmount = 0f)
    {
        displayName = name;
        wind = windScale;
        gusts = gustiness;
        rain = rainAmount;
        clouds = cloudCover;
        fog = fogScale;
        mist = Mathf.Clamp01(mistAmount);
        fogTint = tint;
        lightningPerMinute = lightning;
        minMinutes = shortest;
        maxMinutes = longest;
    }
}

/// <summary>Weather values that blend smoothly from one weather to the next.</summary>
public struct WeatherState
{
    public float Wind, Gusts, Rain, Clouds, Fog, Mist, LightningPerMinute;
    public Color FogTint;

    public static WeatherState Clear => new WeatherState { Wind = 1f, Fog = 1f, FogTint = new Color(0.6f, 0.65f, 0.7f, 0f) };

    public static WeatherState Lerp(WeatherState a, WeatherState b, float t)
    {
        t = Mathf.Clamp01(t);
        return new WeatherState
        {
            Wind = Mathf.Lerp(a.Wind, b.Wind, t),
            Gusts = Mathf.Lerp(a.Gusts, b.Gusts, t),
            Rain = Mathf.Lerp(a.Rain, b.Rain, t),
            Clouds = Mathf.Lerp(a.Clouds, b.Clouds, t),
            Fog = Mathf.Lerp(a.Fog, b.Fog, t),
            Mist = Mathf.Lerp(a.Mist, b.Mist, t),
            LightningPerMinute = Mathf.Lerp(a.LightningPerMinute, b.LightningPerMinute, t),
            FogTint = Color.Lerp(a.FogTint, b.FogTint, t)
        };
    }
}
