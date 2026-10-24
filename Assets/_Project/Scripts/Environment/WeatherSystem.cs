using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>The scene's weather (see Docs/Weather.md). Blends from one <see cref="WeatherDefinition"/> to the next and
/// applies the result: wind strength and gusts (the scene's <see cref="MossWind"/>), rain (<see cref="RainEffect"/>),
/// the gloomy Overcast volume, and lightning flashes that <see cref="DayNightCycle"/> shows. With a
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
    [SerializeField] private MossWind wind;
    [SerializeField] private RainEffect rain;
    [Tooltip("Global volume with the overcast / rain look; its weight follows clouds and rain.")]
    [SerializeField] private Volume overcastVolume;

    private WeatherDefinition weather;
    private WeatherState from = WeatherState.Clear;
    private WeatherState current = WeatherState.Clear;
    private float blend = 1f;
    private float transition;
    private float timeLeft;
    private float flash;
    private System.Random random = new System.Random();

    /// <summary>The weather being blended to (or that has arrived).</summary>
    public WeatherDefinition Weather => weather;

    /// <summary>The blended weather right now.</summary>
    public WeatherState Current => current;

    /// <summary>Lightning brightness, 1 at a strike and fading within a fraction of a second.</summary>
    public float LightningFlash => flash;

    public ClimateDefinition Climate => climate;

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
    }

    public event Action<WeatherDefinition> Changed;

    private void OnEnable()
    {
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

        flash = Mathf.Max(0f, flash - deltaTime * 5f);
        if (current.LightningPerMinute > 0f && random.NextDouble() < current.LightningPerMinute / 60f * deltaTime)
        {
            flash = 1f;
        }

        Apply();
    }

    /// <summary>Makes lightning predictable in tests.</summary>
    public void SetRandomSeed(int seed) => random = new System.Random(seed);

    private void Begin()
    {
        weather = startWeather != null ? startWeather : climate != null ? climate.Start : null;
        current = from = weather != null ? weather.State : WeatherState.Clear;
        blend = 1f;
        flash = 0f;
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
        if (wind != null)
        {
            wind.SetWeather(current.Wind, current.Gusts);
        }

        if (rain != null)
        {
            rain.SetRain(current.Rain);
        }

        if (overcastVolume != null)
        {
            overcastVolume.weight = Mathf.Clamp01(Mathf.Max(current.Clouds * 0.75f, current.Rain));
        }
    }
}
