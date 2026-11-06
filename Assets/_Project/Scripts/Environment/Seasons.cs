using UnityEngine;

/// <summary>
/// The year turning at home: each dawn counts a day (SettlementState.DaysPassed, saved), and the day and the hour place it
/// in its SeasonCycle. The look goes to the shaders as _SafeHavenSeason (autumn, leaf fall, frost): Tree Wind turns and thins
/// broadleaf crowns and frosts every leaf, the grass yellows and frosts. A season with its own climate sets the weather's.
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

    /// <summary>The season now, or null without a cycle.</summary>
    public SeasonCycle.Season Current { get; private set; }

    public void Configure(SeasonCycle year, float firstDay)
    {
        cycle = year;
        startDay = firstDay;
    }

    private void OnDisable() => Shader.SetGlobalVector(SeasonId, Vector4.zero);

    private void Update()
    {
        float hour = AtmosphereConditions.Hour;
        // Dawn coming round: the hour passing 6 going forward (not a jump back from a loaded or skipped time).
        if (lastHour >= 0f && lastHour < 6f && hour >= 6f && hour - lastHour < 2f)
        {
            GameSession.Settlement.PassDay();
        }

        lastHour = hour;
        if (cycle == null)
        {
            return;
        }

        float day = startDay + GameSession.Settlement.DaysPassed + Mathf.Repeat(hour - 6f, 24f) / 24f;
        Vector3 look = cycle.LookAt(day);
        Shader.SetGlobalVector(SeasonId, new Vector4(look.x, look.y, look.z, 0f));
        cycle.At(day, out SeasonCycle.Season season, out _, out _);
        Current = season;
        WeatherSystem weather = WeatherSystem.Active;
        if (weather != null && season != null && season.climate != null && season.climate != climateSet)
        {
            climateSet = season.climate;
            weather.SetClimate(season.climate);
        }
    }
}
