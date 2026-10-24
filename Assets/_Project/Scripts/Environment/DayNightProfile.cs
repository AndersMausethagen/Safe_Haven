using UnityEngine;

/// <summary>How a scene's day looks (see Docs/Weather.md): day length, when the sun rises and sets, the sun's and
/// moon's path, colour and strength, and the ambient light, fog, reflections and post-processing weights over the 24
/// hours. Read by <see cref="DayNightCycle"/>; shared by scenes and never changed at runtime.
/// Gradients and curves run over the clock (gradient 0..1 = 0..24 h, curves in hours), so keep their dusk and dawn
/// keys near <c>sunrise</c> / <c>sunset</c> when moving those.</summary>
[CreateAssetMenu(menuName = "Safe Haven/Environment/Day Night Profile", fileName = "Day Night Profile")]
public sealed class DayNightProfile : ScriptableObject
{
    [Header("Time")]
    [Tooltip("Real minutes for a whole day (24 game hours).")]
    [SerializeField, Min(0.1f)] private float dayLengthMinutes = 24f;
    [SerializeField, Range(0f, 24f)] private float sunrise = 6f;
    [SerializeField, Range(0f, 24f)] private float sunset = 20f;

    [Header("Sun")]
    [Tooltip("Compass direction (degrees) the light shines towards at noon. The sun swings 80° either side of it " +
             "over the day, so pick a direction that casts readable shadows from the game camera.")]
    [SerializeField] private float noonYaw = 320f;
    [SerializeField, Range(5f, 90f)] private float noonElevation = 55f;
    [Tooltip("Lowest angle the light comes from while the sun is up (it still fades out at sunrise and sunset). A real " +
             "low sun barely lights flat ground; keeping the light a little higher keeps golden hour warm and readable.")]
    [SerializeField, Range(0f, 30f)] private float lowestLightAngle = 12f;
    [SerializeField, Min(0f)] private float sunIntensity = 2.3f;
    [Tooltip("Sun strength by its height (0 = horizon, 1 = noon), times Sun Intensity.")]
    [SerializeField] private AnimationCurve sunIntensityByHeight = new AnimationCurve(
        new Keyframe(0f, 1.15f), new Keyframe(0.3f, 1f), new Keyframe(1f, 1f)); // low sun still lights flat ground
    [SerializeField] private Gradient sunColour = Clock(
        // Warm gold rather than deep orange at the ends of the day: green grass under orange light turns muddy.
        (6f, new Color(1f, 0.58f, 0.38f)), (7.2f, new Color(1f, 0.8f, 0.62f)), (9f, new Color(1f, 0.89f, 0.78f)),
        (17f, new Color(1f, 0.89f, 0.78f)), (18.8f, new Color(1f, 0.8f, 0.6f)), (20f, new Color(1f, 0.62f, 0.4f)));

    [Header("Moon (the same light at night)")]
    [SerializeField] private Color moonColour = new Color(0.7f, 0.78f, 1f);
    [SerializeField, Min(0f)] private float moonIntensity = 1.6f;
    [SerializeField, Range(5f, 90f)] private float moonElevation = 60f;

    [Header("Ambient light, fog and reflections over the day")]
    [SerializeField] private Gradient ambientSky = Clock(
        (0f, new Color(0.28f, 0.32f, 0.5f)), (5f, new Color(0.3f, 0.33f, 0.5f)), (6.5f, new Color(0.8f, 0.68f, 0.66f)),
        (9f, new Color(0.62f, 0.68f, 0.78f)), (17f, new Color(0.62f, 0.68f, 0.78f)), (19.5f, new Color(0.88f, 0.7f, 0.6f)),
        (21f, new Color(0.32f, 0.34f, 0.52f)), (24f, new Color(0.28f, 0.32f, 0.5f)));
    [SerializeField] private Gradient ambientEquator = Clock(
        (0f, new Color(0.18f, 0.2f, 0.3f)), (5f, new Color(0.2f, 0.21f, 0.31f)), (6.5f, new Color(0.7f, 0.56f, 0.48f)),
        (9f, new Color(0.52f, 0.49f, 0.44f)), (17f, new Color(0.52f, 0.49f, 0.44f)), (19.5f, new Color(0.78f, 0.58f, 0.44f)),
        (21f, new Color(0.22f, 0.22f, 0.34f)), (24f, new Color(0.18f, 0.2f, 0.3f)));
    [SerializeField] private Gradient ambientGround = Clock(
        (0f, new Color(0.1f, 0.1f, 0.14f)), (6.5f, new Color(0.36f, 0.29f, 0.24f)), (9f, new Color(0.3f, 0.26f, 0.22f)),
        (17f, new Color(0.3f, 0.26f, 0.22f)), (19.5f, new Color(0.4f, 0.3f, 0.23f)), (21f, new Color(0.12f, 0.12f, 0.16f)),
        (24f, new Color(0.1f, 0.1f, 0.14f)));
    [SerializeField] private Gradient fogColour = Clock(
        (0f, new Color(0.07f, 0.09f, 0.15f)), (5f, new Color(0.12f, 0.14f, 0.2f)), (6.5f, new Color(0.8f, 0.62f, 0.52f)),
        (9f, new Color(0.8f, 0.75f, 0.68f)), (17f, new Color(0.8f, 0.75f, 0.68f)), (19.5f, new Color(0.85f, 0.6f, 0.45f)),
        (21f, new Color(0.14f, 0.14f, 0.22f)), (24f, new Color(0.07f, 0.09f, 0.15f)));
    [Tooltip("Fog density (exponential squared) by hour: thicker as morning mist.")]
    [SerializeField] private AnimationCurve fogDensity = Hours((0f, 0.008f), (5f, 0.01f), (6.5f, 0.012f), (9f, 0.006f),
                                                                (17f, 0.006f), (20f, 0.008f), (24f, 0.008f));
    [Tooltip("Sky reflection strength by hour (water and shiny surfaces stop reflecting a bright sky at night).")]
    [SerializeField] private AnimationCurve reflection = Hours((0f, 0.15f), (6f, 0.5f), (9f, 1f), (18f, 1f), (21f, 0.2f), (24f, 0.15f));

    [Header("Post-processing weights by hour (the Night and Golden Hour volumes)")]
    [SerializeField] private AnimationCurve night = Hours((0f, 1f), (5f, 1f), (7f, 0f), (19f, 0f), (21.5f, 1f), (24f, 1f));
    [SerializeField] private AnimationCurve goldenHour = Hours((5f, 0f), (6.5f, 1f), (8.5f, 0f), (17.5f, 0f), (19.5f, 1f), (21f, 0f));

    public float DayLengthMinutes => dayLengthMinutes;
    public float Sunrise => sunrise;
    public float Sunset => sunset;

    /// <summary>Everything the scene's light, ambient, fog and volumes should be at <paramref name="hour"/>.</summary>
    public DayNightState Evaluate(float hour)
    {
        hour = Mathf.Repeat(hour, 24f);
        float day = Mathf.Max(sunset - sunrise, 0.1f);
        float sinceSunrise = Mathf.Repeat(hour - sunrise, 24f);
        var state = new DayNightState { Hour = hour };
        if (sinceSunrise < day)
        {
            float fraction = sinceSunrise / day;
            float height = Mathf.Sin(Mathf.PI * fraction);
            float elevation = noonElevation * height; // the real sun: fades the light at the horizon
            float lightAngle = Mathf.Lerp(Mathf.Min(lowestLightAngle, noonElevation), noonElevation, height);
            state.LightRotation = Quaternion.Euler(lightAngle, noonYaw + Mathf.Lerp(-80f, 80f, fraction), 0f);
            state.LightColour = sunColour.Evaluate(hour / 24f);
            state.LightIntensity = sunIntensity * sunIntensityByHeight.Evaluate(height) * Horizon(elevation);
            state.Daylight = Mathf.Clamp01(elevation / 15f);
        }
        else
        {
            float fraction = (sinceSunrise - day) / (24f - day);
            float elevation = moonElevation * Mathf.Sin(Mathf.PI * fraction);
            state.LightRotation = Quaternion.Euler(elevation, noonYaw + Mathf.Lerp(60f, -60f, fraction), 0f);
            state.LightColour = moonColour;
            state.LightIntensity = moonIntensity * Horizon(elevation);
            state.IsMoon = true;
        }

        float t = hour / 24f;
        state.AmbientSky = ambientSky.Evaluate(t);
        state.AmbientEquator = ambientEquator.Evaluate(t);
        state.AmbientGround = ambientGround.Evaluate(t);
        state.FogColour = fogColour.Evaluate(t);
        state.FogDensity = Mathf.Max(0f, fogDensity.Evaluate(hour));
        state.Reflection = Mathf.Clamp01(reflection.Evaluate(hour));
        state.Night = Mathf.Clamp01(night.Evaluate(hour));
        state.GoldenHour = Mathf.Clamp01(goldenHour.Evaluate(hour));
        return state;
    }

    /// <summary>Fades a light in over its first 6° above the horizon.</summary>
    private static float Horizon(float elevation) => Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elevation / 6f));

    private static Gradient Clock(params (float Hour, Color Colour)[] keys)
    {
        var colours = new GradientColorKey[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            colours[i] = new GradientColorKey(keys[i].Colour, keys[i].Hour / 24f);
        }

        var gradient = new Gradient();
        gradient.SetKeys(colours, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    private static AnimationCurve Hours(params (float Hour, float Value)[] keys)
    {
        // Flat tangents: eases between keys and never overshoots, so a plateau (the whole afternoon) stays level.
        var curve = new AnimationCurve();
        foreach (var (hour, value) in keys)
        {
            curve.AddKey(new Keyframe(hour, value, 0f, 0f));
        }

        return curve;
    }
}

/// <summary>The scene's look at one moment of the day (<see cref="DayNightProfile.Evaluate"/>).</summary>
public struct DayNightState
{
    public float Hour;
    public Quaternion LightRotation;
    public Color LightColour;
    public float LightIntensity;
    public bool IsMoon;
    /// <summary>0 at night, 1 once the sun is 15° up.</summary>
    public float Daylight;
    public Color AmbientSky, AmbientEquator, AmbientGround, FogColour;
    public float FogDensity, Reflection, Night, GoldenHour;
}
