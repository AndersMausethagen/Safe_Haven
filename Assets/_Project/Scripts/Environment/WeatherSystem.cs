using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>The scene's weather (see Docs/Weather.md). Blends from one <see cref="WeatherDefinition"/> to the next and
/// applies the result: wind strength and gusts (the scene's <see cref="MossWind"/>), rain (<see cref="RainEffect"/>),
/// the gloomy Overcast volume, how wet the ground is (it soaks and dries slower than the rain comes and goes), and
/// lightning flashes that <see cref="DayNightCycle"/> shows. With a
/// <see cref="ClimateDefinition"/> it changes weather by itself; scripts can force one with <see cref="SetWeather"/>
/// (a storm before a ward breach). The weather is not saved: a loaded game starts with the start weather.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class WeatherSystem : MonoBehaviour
{
    [SerializeField] private ClimateDefinition climate;
    [Tooltip("Weather at start, and what the editor previews. Empty = the climate's start weather.")]
    [SerializeField] private WeatherDefinition startWeather;
    [Tooltip("Off: the weather stays until a script calls SetWeather.")]
    [SerializeField] private bool changesByItself = true;
    [Tooltip("Mist and fog follow the randomly selected weather only. Clear can remove both completely, even at dawn/dusk or in night volumes. Turn off to allow authored constant/golden-hour mist and partial fog weather influence.")]
    [SerializeField] private bool weatherOnlyAtmosphere = true;
    [SerializeField] private MossWind wind;
    [SerializeField] private RainEffect rain;
    [Tooltip("Global volume with the overcast / rain look; its weight follows clouds and rain.")]
    [SerializeField] private Volume overcastVolume;

    private static readonly int WetnessId = Shader.PropertyToID("_SafeHavenWetness");
    private static readonly int SnowId = Shader.PropertyToID("_SafeHavenSnow");
    /// <summary>How much slower snow builds up on the ground than rain soaks it, and how much longer it lingers after a snowfall than it takes to thaw.</summary>
    private const float SnowSettleFactor = 1.5f, SnowLingerFactor = 6f;
    /// <summary>Snow that is no longer the season's thaws this much slower than wet ground dries, and wets the ground as it goes.</summary>
    private const float ThawFactor = 2f, MeltWater = 0.7f;

    private WeatherDefinition weather;
    private WeatherState from = WeatherState.Clear;
    private WeatherState current = WeatherState.Clear;
    private float blend = 1f;
    private float transition;
    private float timeLeft;
    private float flash;
    private float wetness;
    private bool snowing;
    private float lyingSnow;
    private float snow;
    // The climate's picks and how long each weather lasts: a raid seeds it so friends on one run share the weather.
    private System.Random random = new System.Random();
    // Lightning draws every frame, so it keeps its own (frame rates differ between games; the climate's course must not).
    private System.Random lightning = new System.Random();

    /// <summary>The weather being blended to (or that has arrived).</summary>
    public WeatherDefinition Weather => weather;

    /// <summary>The blended weather right now.</summary>
    public WeatherState Current => current;

    /// <summary>How wet the ground is, 0 dry to 1 soaked with full puddles. Lags behind the rain.</summary>
    public float Wetness => wetness;

    /// <summary>Whether rain falls as snow (a snowing season: <see cref="SetSnowfall"/>).</summary>
    public bool Snowing => snowing;

    /// <summary>Snow on the ground, 0 none to 1 a full layer: the season's lying snow, deeper after a snowfall.</summary>
    public float Snow => snow;

    /// <summary>Lightning brightness, 1 at a strike and fading within a fraction of a second.</summary>
    public float LightningFlash => flash;

    public ClimateDefinition Climate => climate;

    /// <summary>The scene's weather, if it has one (the seasons change its climate).</summary>
    public static WeatherSystem Active { get; private set; }

    /// <summary>Changes the climate the next weather is picked from (a season's); the weather now carries on until then.</summary>
    public void SetClimate(ClimateDefinition next) => climate = next;

    /// <summary>Set by the seasons: whether rain falls as snow, and how much snow lies between snowfalls (0-1).</summary>
    public void SetSnowfall(bool falls, float lying)
    {
        snowing = falls;
        lyingSnow = Mathf.Clamp01(lying);
    }

    /// <summary>Whether the climate picks the next weather when one has lasted its time (developer tools can pause it).</summary>
    public bool ChangesByItself
    {
        get => changesByItself;
        set => changesByItself = value;
    }

    /// <summary>0..1 progress of the blend to <see cref="Weather"/>.</summary>
    public float Blend => blend;

    /// <summary>Seconds until the climate picks the next weather.</summary>
    public float TimeLeft => timeLeft;

    /// <summary>A lightning flash now (developer tools, scripted events).</summary>
    public void Strike()
    {
        flash = 1f;
        Struck?.Invoke();
    }

    public event Action<WeatherDefinition> Changed;

    /// <summary>Raised at every lightning strike (thunder follows it).</summary>
    public event Action Struck;

    private void OnEnable()
    {
        Active = this;
        Begin();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying && isActiveAndEnabled)
        {
            Begin();
        }
    }

    private void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
        }

        AtmosphereConditions.WeatherOnlyAtmosphere = false;
        AtmosphereConditions.WeatherFog = 1f;
        AtmosphereConditions.WeatherWind = 1f;
        AtmosphereConditions.WeatherMist = 0f;
        AtmosphereConditions.WeatherRain = 0f;
        Shader.SetGlobalFloat(WetnessId, 0f);
        Shader.SetGlobalFloat(SnowId, 0f);
        if (wind != null)
        {
            wind.SetWeather(1f, 0f);
        }

        if (rain != null)
        {
            rain.SetRain(0f);
        }
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Tick(Time.deltaTime);
        }
    }

    /// <summary>Blends to <paramref name="next"/> over <paramref name="transitionSeconds"/> (the climate's by default);
    /// it then lasts its own duration before the climate picks again.</summary>
    public void SetWeather(WeatherDefinition next, float transitionSeconds = -1f)
    {
        if (next == null)
        {
            return;
        }

        from = current;
        weather = next;
        transition = transitionSeconds >= 0f ? transitionSeconds : climate != null ? climate.TransitionSeconds : 0f;
        blend = transition > 0f ? 0f : 1f;
        timeLeft = Duration(next);
        if (blend >= 1f)
        {
            current = next.State;
        }

        Apply();
        Changed?.Invoke(next);
    }

    /// <summary>The ground's wetness after <paramref name="deltaTime"/> seconds: it rises towards the rain's strength,
    /// as fast as the rain falls, and dries at its own pace.</summary>
    public static float AdvanceWetness(float wetness, float rain, float deltaTime, float soakSeconds, float drySeconds)
    {
        rain = Mathf.Clamp01(rain);
        return wetness < rain
            ? Mathf.Min(rain, wetness + rain * deltaTime / Mathf.Max(soakSeconds, 1e-3f))
            : Mathf.Max(rain, wetness - deltaTime / Mathf.Max(drySeconds, 1e-3f));
    }

    /// <summary>Snow on the ground after <paramref name="deltaTime"/> seconds. While snow falls it builds towards the
    /// snowfall's strength (never below what lies anyway) and lingers after; out of a snowing season it thaws away.</summary>
    public static float AdvanceSnow(float snow, float falling, float lying, bool snowing, float deltaTime, float settleSeconds, float thawSeconds)
    {
        if (!snowing)
        {
            return Mathf.Max(lying, snow - deltaTime / Mathf.Max(thawSeconds, 1e-3f));
        }

        // The season's own layer is simply there (the season's turn already brings it in over days); a snowfall builds on it.
        snow = Mathf.Max(snow, lying);
        float target = Mathf.Max(lying, Mathf.Clamp01(falling));
        return snow < target
            ? Mathf.Min(target, snow + Mathf.Max(falling, 0.1f) * deltaTime / Mathf.Max(settleSeconds, 1e-3f))
            : Mathf.Max(target, snow - deltaTime / Mathf.Max(thawSeconds * SnowLingerFactor, 1e-3f));
    }

    /// <summary>Advances the blend, the climate's timer and lightning by <paramref name="deltaTime"/> seconds.</summary>
    public void Tick(float deltaTime)
    {
        if (weather != null && blend < 1f)
        {
            blend = Mathf.Min(1f, blend + deltaTime / Mathf.Max(transition, 1e-3f));
            current = WeatherState.Lerp(from, weather.State, Mathf.SmoothStep(0f, 1f, blend));
        }

        if (changesByItself && climate != null)
        {
            timeLeft -= deltaTime;
            if (timeLeft <= 0f)
            {
                SetWeather(climate.Pick((float)random.NextDouble()));
            }
        }

        float soak = climate != null ? climate.SoakSeconds : 75f, dry = climate != null ? climate.DrySeconds : 240f;
        float before = snow;
        snow = AdvanceSnow(snow, current.Rain, lyingSnow, snowing, deltaTime, soak * SnowSettleFactor, dry * ThawFactor);
        // Snow doesn't soak the ground; snow thawing out of a snowing season does, a little.
        float soaking = snowing ? 0f : Mathf.Max(current.Rain, before > snow ? before * MeltWater : 0f);
        wetness = AdvanceWetness(wetness, soaking, deltaTime, soak, dry);
        flash = Mathf.Max(0f, flash - deltaTime * 5f);
        if (current.LightningPerMinute > 0f && lightning.NextDouble() < current.LightningPerMinute / 60f * deltaTime)
        {
            Strike();
        }

        Apply();
    }

    /// <summary>Makes the weather's course follow <paramref name="seed"/> (a raid's, so every game on the run has the same
    /// weather; tests); the weather now starts its time over.</summary>
    public void SetRandomSeed(int seed)
    {
        random = new System.Random(seed);
        lightning = new System.Random(unchecked(seed * 31 + 17));
        timeLeft = Duration(weather);
    }

    private void Begin()
    {
        weather = startWeather != null ? startWeather : climate != null ? climate.Start : null;
        current = from = weather != null ? weather.State : WeatherState.Clear;
        blend = 1f;
        flash = 0f;
        wetness = Mathf.Clamp01(current.Rain);
        timeLeft = Duration(weather);
        Apply();
    }

    private float Duration(WeatherDefinition definition)
    {
        if (definition == null)
        {
            return float.PositiveInfinity;
        }

        return Mathf.Lerp(definition.MinMinutes, definition.MaxMinutes, (float)random.NextDouble()) * 60f;
    }

    private void Apply()
    {
        AtmosphereConditions.WeatherOnlyAtmosphere = weatherOnlyAtmosphere;
        AtmosphereConditions.WeatherFog = current.Fog;
        AtmosphereConditions.WeatherWind = Mathf.Max(0f, current.Wind);
        AtmosphereConditions.WeatherMist = Mathf.Clamp01(current.Mist);
        AtmosphereConditions.WeatherRain = Mathf.Clamp01(current.Rain);
        Shader.SetGlobalFloat(WetnessId, wetness);
        Shader.SetGlobalFloat(SnowId, snow);
        if (wind != null)
        {
            wind.SetWeather(current.Wind, current.Gusts);
        }

        if (rain != null)
        {
            rain.SetRain(current.Rain, snowing);
        }

        if (overcastVolume != null)
        {
            overcastVolume.weight = Mathf.Clamp01(Mathf.Max(current.Clouds * 0.75f, current.Rain));
        }
    }
}
