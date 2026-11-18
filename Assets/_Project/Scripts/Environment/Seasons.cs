using UnityEngine;

/// <summary>
/// The year turning at home: each dawn counts a day (SettlementState.DaysPassed, saved), and the day and the hour place it
/// in its SeasonCycle. The look goes to the shaders as _SafeHavenSeason (autumn, leaf fall, frost): Tree Wind turns and thins
/// broadleaf crowns and frosts every leaf, the grass yellows and frosts. A snowing season (winter) turns the weather's rain
/// to snow and keeps a thin layer lying (WeatherSystem.SetSnowfall). A season with its own climate sets the weather's.
/// <para>Where it belongs: once in the Haven scene, beside the Day Night Cycle.</para>
/// </summary>
public sealed class Seasons : MonoBehaviour
{
    private static readonly int SeasonId = Shader.PropertyToID("_SafeHavenSeason");

    [SerializeField] private SeasonCycle cycle;
    [Tooltip("Which day of the year a new game begins on (a few days into summer).")]
    [SerializeField, Min(0f)] private float startDay = 2f;

    private float lastHour = -1f;
    private ClimateDefinition climateSet;

    public SeasonCycle Cycle => cycle;

    /// <summary>The season now, or null without a cycle.</summary>
    public SeasonCycle.Season Current { get; private set; }

    /// <summary>The name of the season at home now, or null where there are no seasons (raids, no cycle).</summary>
    public static string Today { get; private set; }

    /// <summary>Metres the rivers run higher now (the thaw; 0 where there are no seasons).</summary>
    public static float RiverRise { get; private set; }

    /// <summary>The share of a summer's fireflies out now (1 where there are no seasons).</summary>
    public static float Fireflies { get; private set; } = 1f;

    /// <summary>The scene's seasons, if it has them.</summary>
    public static Seasons Active { get; private set; }

    /// <summary>The day of the year now (whole days and the fraction since dawn), from the saved days at home.</summary>
    public float Day => startDay + GameSession.Here.DaysPassed + Mathf.Repeat(AtmosphereConditions.Hour - 6f, 24f) / 24f;

    private void OnEnable()
    {
        Active = this;
        if (cycle != null)
        {
            cycle.At(Day, out SeasonCycle.Season season, out _, out _);
            Current = season;
            Today = season != null ? season.name : null;
        }
    }

    public void Configure(SeasonCycle year, float firstDay)
    {
        cycle = year;
        startDay = firstDay;
    }

    private void OnDisable()
    {
        if (Active == this)
        {
            Active = null;
            Today = null;
            Fireflies = 1f;
            RiverRise = 0f;
        }

        Shader.SetGlobalVector(SeasonId, Vector4.zero);
        if (WeatherSystem.Active != null)
        {
            WeatherSystem.Active.SetSnowfall(false, 0f);
        }
    }

    /// <summary>
    /// How much of the ground is frosted (0-1), from the season's frost and the hour: a hard winter whitens it all day, a
    /// light frost only lies in patches, thickest in the morning (with the mist) and thinned by the afternoon sun.
    /// </summary>
    public static float GroundFrost(float frost, float hour, float morning)
    {
        if (frost <= 0f)
        {
            return 0f;
        }

        float afternoon = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 13f, hour)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(16f, 19f, hour)));
        return Mathf.Clamp01(frost * 1.35f + frost * morning) * (1f - 0.5f * afternoon * (1f - frost));
    }

    private void Update()
    {
        float hour = AtmosphereConditions.Hour;
        // Dawn coming round: the hour passing 6 going forward (not a jump back from a loaded or skipped time).
        // In a friend's Haven the day is theirs: it comes with their settlement (co-op).
        if (lastHour >= 0f && lastHour < 6f && hour >= 6f && hour - lastHour < 2f && !HavenVisit.IsVisiting)
        {
            GameSession.Home.PassDay();
            global::Territory.Turn(true);
        }

        lastHour = hour;
        if (cycle == null)
        {
            return;
        }

        float day = startDay + GameSession.Here.DaysPassed + Mathf.Repeat(hour - 6f, 24f) / 24f;
        Vector3 look = cycle.LookAt(day);
        Shader.SetGlobalVector(SeasonId, new Vector4(look.x, look.y, look.z, GroundFrost(look.z, hour, AtmosphereConditions.Morning)));
        cycle.At(day, out SeasonCycle.Season season, out _, out _);
        Current = season;
        Today = season != null ? season.name : null;
        Fireflies = cycle.FirefliesAt(day);
        RiverRise = cycle.RiverRiseAt(day);
        WeatherSystem weather = WeatherSystem.Active;
        if (weather != null)
        {
            cycle.SnowAt(day, out bool snows, out float lying);
            weather.SetSnowfall(snows, lying);
        }

        if (weather != null && season != null && season.climate != null && season.climate != climateSet)
        {
            climateSet = season.climate;
            weather.SetClimate(season.climate);
        }
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Active = null;
        Fireflies = 1f;
        RiverRise = 0f;
        Today = null;
    }
}
