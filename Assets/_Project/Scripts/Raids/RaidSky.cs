using System.Collections;
using UnityEngine;

/// <summary>
/// Sets a raid scene's sky to the hour the run was rolled (RaidSession.TimeOfDay, chosen from the raid's Time of Day
/// list and shown in the briefing) and stops the clock, so a night raid stays night. On the raid's "Sky and Weather"
/// rig. Without a rolled hour (a test raid, a raid with no times of day) the rig's own start hour stands. The weather's
/// course follows the run's seed, so friends on one run (co-op) see the same rain and fog.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidSky : MonoBehaviour, ISceneSetup
{
    [Tooltip("The scene's day/night cycle (found in children when empty).")]
    [SerializeField] private DayNightCycle cycle;
    [Tooltip("The scene's weather (found in children when empty).")]
    [SerializeField] private WeatherSystem weather;

    public int SetupOrder => -90;

    public IEnumerator Setup()
    {
        Apply();
        yield break;
    }

    private void Start()
    {
        // Covers scenes started straight from the editor, which run setup before Start as well; applying twice is harmless.
        Apply();
    }

    private void Apply()
    {
        RaidSession run = GameSession.ActiveRaid;
        if (weather == null)
        {
            weather = GetComponentInChildren<WeatherSystem>();
        }

        if (run != null && weather != null)
        {
            // The raid's own climate, and the weather the briefing foretold for this run (RaidWeather): every game on the run the same.
            if (run.Definition != null && run.Definition.Climate != null)
            {
                weather.SetClimate(run.Definition.Climate);
            }

            weather.SetRandomSeed(RaidLayout.SeedFor(run.Seed, "Weather"));
            WeatherDefinition opening = RaidWeather.Opening(run.Definition, run.Seed);
            if (opening != null)
            {
                weather.StartIn(opening);
            }
        }

        if (cycle == null)
        {
            cycle = GetComponentInChildren<DayNightCycle>();
        }

        if (cycle == null)
        {
            return;
        }

        cycle.ClockRuns = false;
        RaidTimeOfDay timeOfDay = run?.TimeOfDay;
        if (timeOfDay != null)
        {
            cycle.Hour = timeOfDay.hour;
        }
    }
}
