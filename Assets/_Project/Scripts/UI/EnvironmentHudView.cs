using TMPro;
using UnityEngine;

/// <summary>Read-only clock and weather display, wired to the scene's existing sky rig.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class EnvironmentHudView : MonoBehaviour
{
    [SerializeField] private DayNightCycle cycle;
    [SerializeField] private WeatherSystem weather;
    [SerializeField] private EnvironmentTimelineGraphic timeline;
    [SerializeField] private WeatherIconGraphic weatherIcon;
    [SerializeField] private TMP_Text timeLabel;
    [SerializeField] private TMP_Text weatherLabel;
    [Tooltip("Night, dawn, day, dusk, night, in timeline order.")]
    [SerializeField] private TMP_Text[] phaseLabels;

    private int lastMinute = -1;
    private string lastPhase;
    private WeatherDefinition lastWeather;
    private bool lastTransition;
    private float lastSunrise = -1f;
    private float lastSunset = -1f;

    public DayNightCycle Cycle => cycle;
    public WeatherSystem Weather => weather;

    private void Start()
    {
        if (cycle == null || cycle.Profile == null || weather == null || timeline == null ||
            weatherIcon == null || timeLabel == null || weatherLabel == null ||
            phaseLabels == null || phaseLabels.Length != 5 ||
            System.Array.Exists(phaseLabels, label => label == null))
        {
            Debug.LogError("Environment HUD needs a scene clock, profile, weather and all display references.", this);
            enabled = false;
            return;
        }

        ApplyTypography();
        Refresh();
    }

    private void LateUpdate() => Refresh();

    private void ApplyTypography()
    {
        timeLabel.fontSize = Mathf.Max(timeLabel.fontSize, 18f);
        weatherLabel.fontSize = Mathf.Max(weatherLabel.fontSize, 17f);
        foreach (TMP_Text label in phaseLabels)
        {
            label.fontSize = Mathf.Max(label.fontSize, 15f);
            Vector2 size = label.rectTransform.sizeDelta;
            label.rectTransform.sizeDelta = new Vector2(Mathf.Max(size.x, 68f), Mathf.Max(size.y, 36f));
        }
    }

    public void Refresh()
    {
        if (cycle == null || cycle.Profile == null || weather == null)
        {
            return;
        }

        float sunrise = cycle.Profile.Sunrise;
        float sunset = cycle.Profile.Sunset;
        float hour = cycle.Hour;
        ShowTimeline(HudRules.ShowsEverything || TimeMatters(hour, sunrise, sunset) || WorldMapHud.IsOpen);
        timeline.SetTime(hour, sunrise, sunset);
        if (sunrise != lastSunrise || sunset != lastSunset)
        {
            lastSunrise = sunrise;
            lastSunset = sunset;
            for (int i = 0; i < phaseLabels.Length; i++)
            {
                float start = Boundary(i, sunrise, sunset);
                float end = Boundary(i + 1, sunrise, sunset);
                RectTransform rect = phaseLabels[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2((start + end) / 48f, 0f);
                phaseLabels[i].text = SegmentName(i) + "\n" + FormatHour(start);
            }
        }

        int minute = Mathf.FloorToInt(Mathf.Repeat(hour, 24f) * 60f);
        string phase = PhaseAt(hour, sunrise, sunset);
        if (minute != lastMinute || phase != lastPhase)
        {
            lastMinute = minute;
            lastPhase = phase;
            timeLabel.text = phase.ToUpperInvariant() + "  " + FormatHour(hour);
        }

        WeatherDefinition current = weather.Weather;
        bool transitioning = weather.Blend < 1f;
        if (current != lastWeather || transitioning != lastTransition || string.IsNullOrEmpty(weatherLabel.text))
        {
            lastWeather = current;
            lastTransition = transitioning;
            weatherLabel.text = current != null
                ? (transitioning ? "To " + current.DisplayName : current.DisplayName)
                : "No weather";
        }

        // During a blend, describe the conditions actually visible, not the destination forecast.
        weatherIcon.SetWeather(weather.Current, cycle.IsNight);
    }

    // The day's timeline and its five phase labels: in a Contextual HUD only while the time is about to matter (HudRules). All day
    // it spread across the top of the screen, a time behind the portal (the HUD review of 2026-11-18); the clock and the weather stay.
    private bool timelineShown = true;

    private void ShowTimeline(bool shown)
    {
        if (shown == timelineShown)
        {
            return;
        }

        timelineShown = shown;
        timeline.gameObject.SetActive(shown);
        foreach (TMP_Text label in phaseLabels)
        {
            label.gameObject.SetActive(shown);
        }
    }

    /// <summary>Two hours before nightfall until it falls, and an hour and a half before dawn: when the time is worth planning by.</summary>
    public static bool TimeMatters(float hour, float sunrise, float sunset)
    {
        hour = Mathf.Repeat(hour, 24f);
        return (hour >= sunset - 2f && hour < sunset) || (hour >= sunrise - 1.5f && hour < sunrise);
    }

    public static string FormatHour(float hour)
    {
        int minutes = Mathf.FloorToInt(Mathf.Repeat(hour, 24f) * 60f);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }

    public static float Boundary(int index, float sunrise, float sunset)
    {
        float twilight = Mathf.Min(2f, Mathf.Max(0f, sunset - sunrise) * 0.25f);
        switch (index)
        {
            case 0: return 0f;
            case 1: return sunrise;
            case 2: return sunrise + twilight;
            case 3: return sunset - twilight;
            case 4: return sunset;
            default: return 24f;
        }
    }

    public static string SegmentName(int index)
    {
        switch (index)
        {
            case 1: return "Dawn";
            case 2: return "Day";
            case 3: return "Dusk";
            default: return "Night";
        }
    }

    public static string PhaseAt(float hour, float sunrise, float sunset)
    {
        hour = Mathf.Repeat(hour, 24f);
        for (int i = 0; i < 5; i++)
        {
            if (hour < Boundary(i + 1, sunrise, sunset))
            {
                return SegmentName(i);
            }
        }

        return "Night";
    }
}
