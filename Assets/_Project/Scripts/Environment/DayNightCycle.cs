using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Runs the scene's clock and lights it from a <see cref="DayNightProfile"/> (see Docs/Weather.md): the
/// directional light is the sun by day and the moon by night; ambient light, fog, sky reflections and the Night and
/// Golden Hour volumes follow the hour. A <see cref="WeatherSystem"/>, when set, dims the sun under clouds, thickens
/// and tints the fog, and flashes lightning. In the editor, drag Start Hour to preview any time of day.
/// <para>
/// Saved (ISaveable; needs a SaveableEntity on the same object in a persistent scene): the hour is kept when the player
/// leaves for a raid or saves. Coming back in the same play session, the clock has moved on by the real time spent away
/// (a raid takes part of a day), so the Haven doesn't freeze at the hour you left. A loaded game resumes at the saved hour.
/// </para></summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DayNightCycle : MonoBehaviour, ISaveable, ISharedState
{
    // Identifies this run of the game, so a restore can tell "away on a raid" (same run: time passed) from "loaded from
    // disk" (another run: no way to know how long it was). With the game's world: a save made earlier in the same run and
    // loaded again is a new world, and must not count the real time since it was made as time away.
    private static readonly string Process = System.Guid.NewGuid().ToString("N");
    /// <summary>This run of the game with this world (see above): what else counts real time away by (ResourceNode's regrowth).</summary>
    public static string Run => Process + "." + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(GameSession.World);

    [SerializeField] private DayNightProfile profile;
    [Tooltip("The scene's directional light: the sun by day, the moon by night.")]
    [SerializeField] private Light sun;
    [Tooltip("Hour the scene starts at. In the editor, drag it to preview any time of day.")]
    [SerializeField, Range(0f, 24f)] private float startHour = 9f;
    [Tooltip("Off: time stands still (a cutscene, a raid at a fixed hour).")]
    [SerializeField] private bool clockRuns = true;
    [SerializeField] private Volume nightVolume;
    [SerializeField] private Volume goldenHourVolume;
    [Tooltip("Optional: clouds dim the sun, weather thickens the fog, storms flash lightning.")]
    [SerializeField] private WeatherSystem weather;

    private static readonly Color LightningColour = new Color(0.8f, 0.86f, 1f);
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int NightId = Shader.PropertyToID("_SafeHavenNight");
    private float hour;
    private Material skybox;
    private DayNightProfile blendedProfile;
    private float blendAmount;
    private float goingFrom;
    private float goingTo;
    private float goingFor;
    private float goingElapsed = -1f;
    private float fadeTo;
    private float fadeFor;
    private float fadeElapsed = -1f;

    /// <summary>The time of day in hours (0..24). Setting it jumps the clock (sleeping, a scripted dawn).</summary>
    public float Hour
    {
        get => hour;
        set
        {
            goingElapsed = -1f;
            fadeElapsed = -1f;
            hour = Mathf.Repeat(value, 24f);
            Apply();
        }
    }

    /// <summary>Moves the clock forward to <paramref name="target"/> over <paramref name="seconds"/> (at once when 0). The sun goes
    /// the long way round if it has to: from morning to night by way of the afternoon, never backwards.</summary>
    public void GoToHour(float target, float seconds)
    {
        if (seconds <= 0f)
        {
            Hour = target;
            return;
        }

        fadeElapsed = -1f;
        goingFrom = hour;
        goingTo = ForwardTo(hour, target);
        goingFor = seconds;
        goingElapsed = 0f;
    }

    /// <summary>Changes straight to the look of <paramref name="target"/> over <paramref name="seconds"/>, rather than running the
    /// clock there: shadows don't race over the ground. The hour changes when it is done.</summary>
    public void FadeToHour(float target, float seconds)
    {
        if (seconds <= 0f)
        {
            Hour = target;
            return;
        }

        goingElapsed = -1f;
        fadeTo = Mathf.Repeat(target, 24f);
        fadeFor = seconds;
        fadeElapsed = 0f;
    }

    /// <summary>The same hour as <paramref name="target"/>, on or after <paramref name="from"/>, for moving forward between them.</summary>
    public static float ForwardTo(float from, float target)
    {
        target = Mathf.Repeat(target, 24f);
        return target < from ? target + 24f : target;
    }

    /// <summary>Lights the scene part-way to a second profile (SceneLook). Empty, or 0: its own profile alone.</summary>
    public void BlendProfile(DayNightProfile other, float amount)
    {
        blendedProfile = other;
        blendAmount = Mathf.Clamp01(amount);
    }

    public bool ClockRuns
    {
        get => clockRuns;
        set => clockRuns = value;
    }

    /// <summary>How much faster than the profile's day length the clock runs (developer tools; not saved).</summary>
    public float ClockSpeed { get; set; } = 1f;

    public DayNightProfile Profile => profile;

    /// <summary>The look applied last (sun or moon, daylight, ambient...).</summary>
    public DayNightState State { get; private set; }

    public bool IsNight => State.IsMoon;

    public string SaveKey => "clock";

    // Each player's own in co-op.
    bool ISharedState.IsShared => false;

    int ISharedState.SharedMark => 0;

    public string CaptureState() =>
        string.Join("|", (fadeElapsed >= 0f ? fadeTo : hour).ToString("R", CultureInfo.InvariantCulture), Run,
            Time.realtimeSinceStartupAsDouble.ToString("R", CultureInfo.InvariantCulture));

    public void RestoreState(string state)
    {
        string[] parts = string.IsNullOrEmpty(state) ? new string[0] : state.Split('|');
        if (parts.Length == 0 || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float saved))
        {
            return;
        }

        float away = 0f;
        if (parts.Length >= 3 && parts[1] == Run && clockRuns && profile != null
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double then))
        {
            away = (float)(Time.realtimeSinceStartupAsDouble - then) * 24f / (profile.DayLengthMinutes * 60f);
        }

        Hour = saved + Mathf.Max(0f, away);
        PassDawnsAway(saved, saved + Mathf.Max(0f, away));
    }

    // Seasons counts a day each time it sees the clock pass 6:00, but the hours away on a raid are added here at once, out of its
    // sight: a raid over a dawn lost that day (the day count went back, saplings shrank). Each dawn the time away went past is
    // passed here instead, where Seasons keeps the days (in a friend's Haven the days are theirs).
    private static void PassDawnsAway(float from, float to)
    {
        int dawns = Mathf.FloorToInt((to - 6f) / 24f) - Mathf.FloorToInt((from - 6f) / 24f);
        if (dawns <= 0 || HavenVisit.IsVisiting || GameSession.Here == null || FindAnyObjectByType<Seasons>() == null)
        {
            return;
        }

        for (int i = 0; i < Mathf.Min(dawns, 30); i++)
        {
            GameSession.Home.PassDay();
            global::Territory.Turn(true);
        }
    }

    private void OnEnable()
    {
        hour = startHour;
        Apply();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && isActiveAndEnabled)
        {
            hour = startHour;
            Apply();
        }
    }

    private void OnDisable()
    {
        Shader.SetGlobalFloat(NightId, 0f);
        AtmosphereConditions.GoldenHour = 0f;
        AtmosphereConditions.Night = 0f;
    }

    private void OnDestroy()
    {
        if (skybox != null)
        {
            Destroy(skybox);
        }
    }

    private void LateUpdate()
    {
        if (Application.isPlaying && fadeElapsed >= 0f)
        {
            fadeElapsed += Time.deltaTime;
            if (fadeElapsed >= fadeFor)
            {
                hour = fadeTo;
                fadeElapsed = -1f;
            }
        }
        else if (Application.isPlaying && goingElapsed >= 0f)
        {
            goingElapsed += Time.deltaTime;
            float along = Mathf.Clamp01(goingElapsed / goingFor);
            hour = Mathf.Repeat(Mathf.Lerp(goingFrom, goingTo, along), 24f);
            if (along >= 1f)
            {
                goingElapsed = -1f;
            }
        }
        else if (Application.isPlaying && clockRuns && profile != null)
        {
            hour = Mathf.Repeat(hour + Time.deltaTime * ClockSpeed * 24f / (profile.DayLengthMinutes * 60f), 24f);
        }

        Apply(); // after WeatherSystem.Update, so clouds and lightning are this frame's
    }

    private void Apply()
    {
        if (profile == null || sun == null)
        {
            return;
        }

        DayNightState state = LookAt(hour);
        if (fadeElapsed >= 0f)
        {
            state = DayNightState.Crossfade(state, LookAt(fadeTo), Mathf.SmoothStep(0f, 1f, fadeElapsed / fadeFor));
        }

        State = state;
        bool hasWeather = weather != null && weather.isActiveAndEnabled;
        WeatherState sky = hasWeather ? weather.Current : WeatherState.Clear;
        float flash = hasWeather ? weather.LightningFlash * (0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 30f, 0f)) : 0f;

        sun.transform.rotation = state.LightRotation;
        sun.useColorTemperature = false;
        sun.color = Color.Lerp(state.LightColour, LightningColour, flash);
        sun.intensity = state.LightIntensity * (1f - 0.75f * sky.Clouds) + flash * 3f;
        sun.shadowStrength = Mathf.Lerp(state.ShadowStrength, Mathf.Min(state.ShadowStrength, 0.35f), sky.Clouds);
        RenderSettings.sun = sun;

        // Clouds grey the ambient light a little; lightning lights everything up.
        float grey = sky.Clouds * 0.45f;
        Color flashAmbient = LightningColour * (flash * 0.8f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = Overcast(state.AmbientSky, grey) + flashAmbient;
        RenderSettings.ambientEquatorColor = Overcast(state.AmbientEquator, grey) + flashAmbient;
        RenderSettings.ambientGroundColor = Overcast(state.AmbientGround, grey) + flashAmbient * 0.5f;
        // Shaders light from the ambient probe, which Unity only rebuilds when lighting is baked or updated; setting
        // the colours alone would leave the scene in its baked (daytime) ambient light.
        RenderSettings.ambientProbe = Trilight(RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor,
                                               RenderSettings.ambientGroundColor);

        // Weather tints the fog towards its own colour, kept as bright as the hour's fog (dark at night).
        Color tint = sky.FogTint * (Brightness(state.FogColour) / Mathf.Max(Brightness(sky.FogTint), 0.05f));
        RenderSettings.fogColor = Color.Lerp(state.FogColour, tint, sky.FogTint.a) + flashAmbient * 0.3f;
        RenderSettings.fogDensity = state.FogDensity * sky.Fog * AtmosphereConditions.SceneFogScale;
        AtmosphereConditions.GoldenHour = state.GoldenHour * (1f - sky.Clouds);
        AtmosphereConditions.Night = state.Night;
        AtmosphereConditions.Hour = Hour;
        // Wind and rain stir the morning mist away; a still clear dawn keeps it.
        AtmosphereConditions.Morning = AtmosphereConditions.MorningAt(Hour) * (1f - 0.6f * AtmosphereConditions.WeatherRain);
        RenderSettings.reflectionIntensity = state.Reflection * (1f - 0.4f * sky.Clouds);

        if (nightVolume != null)
        {
            nightVolume.weight = state.Night;
        }

        // Glowing plants: at night, and faintly under heavy cloud.
        Shader.SetGlobalFloat(NightId, Mathf.Max(state.Night, sky.Clouds * 0.25f));

        if (goldenHourVolume != null)
        {
            goldenHourVolume.weight = state.GoldenHour * (1f - sky.Clouds);
        }

        // The skybox is seldom seen from above, but the sky behind distant hills should not glow at night. Only in
        // Play mode, on a copy, so the scene's skybox material is never changed.
        if (Application.isPlaying && RenderSettings.skybox != null && RenderSettings.skybox.HasProperty(ExposureId))
        {
            if (skybox == null)
            {
                skybox = new Material(RenderSettings.skybox) { name = RenderSettings.skybox.name + " (day/night)" };
                RenderSettings.skybox = skybox;
            }

            skybox.SetFloat(ExposureId, Mathf.Lerp(0.12f, 1.3f, state.Daylight) * (1f - 0.5f * sky.Clouds));
        }
    }

    private DayNightState LookAt(float atHour)
    {
        DayNightState state = profile.Evaluate(atHour);
        return blendedProfile != null && blendAmount > 0f ? DayNightState.Lerp(state, blendedProfile.Evaluate(atHour), blendAmount) : state;
    }

    /// <summary>Ambient probe for a sky / horizon / ground gradient, as Unity builds it for Trilight ambient: the
    /// gradient sampled in evenly spread directions, each added as a small directional light.</summary>
    public static SphericalHarmonicsL2 Trilight(Color sky, Color equator, Color ground)
    {
        Vector3[] directions = SampleDirections();
        var probe = new SphericalHarmonicsL2();
        float weight = SampleWeight();
        foreach (Vector3 direction in directions)
        {
            Color colour = direction.y >= 0f ? Color.Lerp(equator, sky, direction.y) : Color.Lerp(equator, ground, -direction.y);
            probe.AddDirectionalLight(direction, colour, weight);
        }

        return probe;
    }

    private static Vector3[] sampleDirections;
    private static float sampleWeight;

    /// <summary>Per-sample light strength that makes a uniform white sky give exactly white ambient (measured once,
    /// rather than relying on how AddDirectionalLight scales its lobe).</summary>
    private static float SampleWeight()
    {
        if (sampleWeight <= 0f)
        {
            var white = new SphericalHarmonicsL2();
            foreach (Vector3 direction in SampleDirections())
            {
                white.AddDirectionalLight(direction, Color.white, 1f);
            }

            var result = new Color[1];
            white.Evaluate(new[] { Vector3.up }, result);
            sampleWeight = 1f / Mathf.Max(result[0].r, 1e-6f);
        }

        return sampleWeight;
    }

    private static Vector3[] SampleDirections()
    {
        if (sampleDirections == null)
        {
            // Fibonacci sphere: evenly spread points.
            const int count = 96;
            sampleDirections = new Vector3[count];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < count; i++)
            {
                float y = 1f - (i + 0.5f) / count * 2f;
                float radius = Mathf.Sqrt(1f - y * y);
                sampleDirections[i] = new Vector3(Mathf.Cos(golden * i) * radius, y, Mathf.Sin(golden * i) * radius);
            }
        }

        return sampleDirections;
    }

    private static Color Overcast(Color colour, float amount) =>
        Color.Lerp(colour, Color.white * (Brightness(colour) * 0.9f), amount);

    private static float Brightness(Color colour) => colour.r * 0.3f + colour.g * 0.59f + colour.b * 0.11f;
}
