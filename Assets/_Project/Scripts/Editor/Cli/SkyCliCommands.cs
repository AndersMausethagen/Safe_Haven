using System.Globalization;
using System.Linq;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Day/night and weather for the Unity CLI (`unity command sh_sky -- --hour 21 --weather Rain`), the command-line twin
/// of the Sky Controller window. Works in Play mode and as a preview in edit mode.
/// </summary>
public static class SkyCliCommands
{
    private const string Tag = "safehaven";

    [CliCommand("sh_sky", "Show or set the time of day, clock speed and weather. Negative values leave a setting alone.", Tags = new[] { Tag })]
    public static string Sky(float hour = -1f, string weather = "", float transition = 0f, float speed = -1f, bool lightning = false)
    {
        var cycle = Object.FindFirstObjectByType<DayNightCycle>();
        var system = Object.FindFirstObjectByType<WeatherSystem>();
        if (cycle == null && system == null)
        {
            return "This scene has no day/night cycle or weather (Safe Haven > World > Sky and Weather > Add Day, Night and Weather to Scene).";
        }

        if (system != null && !string.IsNullOrEmpty(weather))
        {
            WeatherDefinition definition = AssetDatabase.FindAssets("t:" + nameof(WeatherDefinition))
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeatherDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .FirstOrDefault(w => w != null && (string.Equals(w.name, weather, System.StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(w.DisplayName, weather, System.StringComparison.OrdinalIgnoreCase)));
            if (definition == null)
            {
                return $"No weather called '{weather}'.";
            }

            system.SetWeather(definition, Application.isPlaying ? Mathf.Max(0f, transition) : 0f);
        }

        if (cycle != null && hour >= 0f)
        {
            cycle.Hour = hour;
        }
        else if (cycle != null)
        {
            cycle.Hour = cycle.Hour; // re-light with the new weather
        }

        if (cycle != null && speed >= 0f && Application.isPlaying)
        {
            cycle.ClockRuns = speed > 0f;
            cycle.ClockSpeed = Mathf.Max(speed, 1f);
        }

        if (system != null && lightning)
        {
            system.Strike();
        }

        string time = cycle != null
            ? $"{Mathf.FloorToInt(cycle.Hour):00}:{Mathf.FloorToInt(Mathf.Repeat(cycle.Hour, 1f) * 60f):00} ({(cycle.IsNight ? "night" : "day")}, clock {(cycle.ClockRuns ? "×" + cycle.ClockSpeed.ToString("0", CultureInfo.InvariantCulture) : "paused")})"
            : "no clock";
        string sky = system != null && system.Weather != null
            ? $"{system.Weather.DisplayName} (rain {system.Current.Rain:0.00}, clouds {system.Current.Clouds:0.00}, wind ×{system.Current.Wind:0.0})"
            : "no weather";
        return time + " | " + sky;
    }
}
