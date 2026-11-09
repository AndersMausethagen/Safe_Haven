using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Developer tool for the open scene's day/night cycle and weather (Safe Haven > Environment > Sky
/// Controller): drag the hour, jump to dawn / noon / golden hour / night, speed up or pause the clock, switch weather
/// (instantly or blended), stop the climate changing it, and strike lightning. Works in Play mode and, as a preview, in
/// edit mode (where nothing is saved unless you press Make Start Hour / Make Start Weather).</summary>
public sealed class SkyControllerWindow : EditorWindow
{
    private static readonly (string Label, float Hour)[] Moments =
    {
        ("Dawn", 6.5f), ("Morning", 9f), ("Noon", 13f), ("Golden hour", 19.3f), ("Dusk", 20.6f), ("Night", 23.5f)
    };

    private static readonly float[] Speeds = { 0f, 1f, 10f, 60f, 240f };

    private float transition = 5f;
    private Vector2 scroll;
    private double lastRepaint;
    private List<WeatherDefinition> projectWeathers;

    [MenuItem("Safe Haven/World/Sky and Weather/Sky Controller")]
    public static void Open() => GetWindow<SkyControllerWindow>("Sky Controller");

    // Searching the project is slow (tens of ms), so the weather list is found once and again only when assets change.
    private void OnEnable() => projectWeathers = null;

    private void OnProjectChange() => projectWeathers = null;

    private void Update()
    {
        // Follow the clock and the weather blend while playing (twice a second is enough for a readout).
        if (Application.isPlaying && EditorApplication.timeSinceStartup - lastRepaint > 0.5)
        {
            lastRepaint = EditorApplication.timeSinceStartup;
            Repaint();
        }
    }

    private void OnGUI()
    {
        var cycle = FindAnyObjectByType<DayNightCycle>();
        var weather = FindAnyObjectByType<WeatherSystem>();
        if (cycle == null && weather == null)
        {
            EditorGUILayout.HelpBox("This scene has no day/night or weather. Add them with Safe Haven > Environment > " +
                                    "Weather > Add Day, Night and Weather to Scene.", MessageType.Info);
            return;
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Edit mode: changes are a preview and are not saved (except the Make Start buttons).",
                                    MessageType.None);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (cycle != null)
        {
            DrawClock(cycle);
        }

        if (weather != null)
        {
            EditorGUILayout.Space();
            DrawWeather(weather, cycle);
        }

        EditorGUILayout.EndScrollView();
    }

    private static void DrawClock(DayNightCycle cycle)
    {
        EditorGUILayout.LabelField("Time of day", EditorStyles.boldLabel);
        DayNightState state = cycle.State;
        EditorGUI.BeginChangeCheck();
        float hour = EditorGUILayout.Slider($"Hour ({Clock(cycle.Hour)})", cycle.Hour, 0f, 24f);
        if (EditorGUI.EndChangeCheck())
        {
            cycle.Hour = hour;
            SceneView.RepaintAll();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (var (label, at) in Moments)
            {
                if (GUILayout.Button(label))
                {
                    cycle.Hour = at;
                    SceneView.RepaintAll();
                }
            }
        }

        EditorGUILayout.LabelField(" ", (state.IsMoon ? "Moon" : "Sun") +
            $" {state.LightIntensity:0.00}, daylight {state.Daylight:0.00}, night look {state.Night:0.00}, golden hour {state.GoldenHour:0.00}");

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel("Clock speed");
            foreach (float speed in Speeds)
            {
                bool on = speed == 0f ? !cycle.ClockRuns : cycle.ClockRuns && Mathf.Approximately(cycle.ClockSpeed, speed);
                if (GUILayout.Toggle(on, speed == 0f ? "Pause" : $"×{speed:0}", EditorStyles.miniButton) && !on)
                {
                    cycle.ClockRuns = speed > 0f;
                    cycle.ClockSpeed = Mathf.Max(speed, 1f);
                }
            }
        }

        if (cycle.Profile != null)
        {
            float minutes = cycle.Profile.DayLengthMinutes / Mathf.Max(cycle.ClockSpeed, 1f);
            EditorGUILayout.LabelField(" ", cycle.ClockRuns ? $"A day takes {minutes:0.#} real minutes" : "Clock paused");
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Make This the Start Hour"))
            {
                var serialized = new SerializedObject(cycle);
                serialized.FindProperty("startHour").floatValue = cycle.Hour;
                serialized.ApplyModifiedProperties();
            }
        }
    }

    private void DrawWeather(WeatherSystem weather, DayNightCycle cycle)
    {
        EditorGUILayout.LabelField("Weather", EditorStyles.boldLabel);
        WeatherState now = weather.Current;
        string arriving = weather.Blend < 1f ? $" (blending {weather.Blend:P0})" : "";
        EditorGUILayout.LabelField("Now", (weather.Weather != null ? weather.Weather.DisplayName : "none") + arriving);
        EditorGUILayout.LabelField(" ", $"wind ×{now.Wind:0.0}, rain {now.Rain:0.00}, clouds {now.Clouds:0.00}, fog ×{now.Fog:0.0}, mist {now.Mist:0.00}");
        if (Application.isPlaying && weather.ChangesByItself && weather.Climate != null && !float.IsInfinity(weather.TimeLeft))
        {
            EditorGUILayout.LabelField(" ", $"Next change in {weather.TimeLeft / 60f:0.0} min");
        }

        transition = EditorGUILayout.Slider("Blend over (seconds)", transition, 0f, 60f);
        EditorGUILayout.HelpBox("Mist combinations: Breezy + Mist and Storm + Mist keep the base weather's wind, rain and lightning, with full ground mist. Wind drives mist drift and rolling waves; Fog remains stationary. Pause climate changes in Play mode to hold a test.", MessageType.Info);
        foreach (WeatherDefinition[] row in AllWeathers(weather, ref projectWeathers).Select((w, i) => (w, i)).GroupBy(p => p.i / 3).Select(g => g.Select(p => p.w).ToArray()))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (WeatherDefinition definition in row)
                {
                    bool current = weather.Weather == definition;
                    if (GUILayout.Toggle(current, definition.DisplayName, EditorStyles.miniButton) && !current)
                    {
                        weather.SetWeather(definition, Application.isPlaying ? transition : 0f);
                        Refresh(cycle);
                    }
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                weather.ChangesByItself = EditorGUILayout.ToggleLeft("Climate changes the weather", weather.ChangesByItself);
                if (GUILayout.Button("Lightning"))
                {
                    weather.Strike();
                }
            }
        }

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Make This the Start Weather"))
            {
                var serialized = new SerializedObject(weather);
                serialized.FindProperty("startWeather").objectReferenceValue = weather.Weather;
                serialized.ApplyModifiedProperties();
            }
        }
    }

    /// <summary>The climate's weathers first, then any other weather assets in the project (searched once, cached).</summary>
    private static List<WeatherDefinition> AllWeathers(WeatherSystem weather, ref List<WeatherDefinition> project)
    {
        if (project == null)
        {
            project = AssetDatabase.FindAssets("t:" + nameof(WeatherDefinition))
                .Select(guid => AssetDatabase.LoadAssetAtPath<WeatherDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(w => w != null).ToList();
        }

        var list = new List<WeatherDefinition>();
        if (weather.Climate != null)
        {
            list.AddRange(weather.Climate.Weathers.Select(e => e.weather).Where(w => w != null));
        }

        list.AddRange(project.Where(w => w != null && !list.Contains(w)));
        return list;
    }

    private static void Refresh(DayNightCycle cycle)
    {
        if (cycle != null)
        {
            cycle.Hour = cycle.Hour; // re-light with the new weather now (edit mode has no frame loop)
        }

        SceneView.RepaintAll();
    }

    private static string Clock(float hour)
    {
        int minutes = Mathf.FloorToInt(Mathf.Repeat(hour, 24f) * 60f);
        return $"{minutes / 60:00}:{minutes % 60:00}";
    }
}
