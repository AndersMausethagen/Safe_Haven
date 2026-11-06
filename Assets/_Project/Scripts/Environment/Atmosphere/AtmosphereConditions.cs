using UnityEngine;

/// <summary>
/// What the atmosphere is like right now, shared between the systems that make it and those that feel it: the weather's
/// fog, wind and independent mist (WeatherSystem writes them; neutral without weather) and the fog's density near the ground
/// as AtmosphereFeature last drew it (0 without fog), and how dark it is. Monster sight shrinks in thick fog and at night through
/// <see cref="SightScale"/>. Runtime state only, reset every play session.
/// </summary>
public static class AtmosphereConditions
{
    /// <summary>The current weather's fog multiplier (Clear 0, Fog more). Set by WeatherSystem.</summary>
    public static float WeatherFog { get; set; } = 1f;
    public static float WeatherWind { get; set; } = 1f;
    public static float WeatherMist { get; set; }
    /// <summary>How hard it rains, 0-1. Set by WeatherSystem; rain hides the player's noise.</summary>
    public static float WeatherRain { get; set; }
    public static float GoldenHour { get; set; }
    /// <summary>The hour of the day (0-24) as the day-night cycle last drew it; 12 without one.</summary>
    public static float Hour { get; set; } = 12f;
    /// <summary>Morning mist, 0 to 1: rising before dawn, thickest about first light, burnt off by mid-morning (MorningAt).</summary>
    public static float Morning { get; set; }

    /// <summary>How much morning mist an hour has: pooling from 3, thickest 5:30 to 7, gone by 10.</summary>
    public static float MorningAt(float hour) =>
        hour < 3f || hour > 10f ? 0f : hour < 5.5f ? Mathf.SmoothStep(0f, 1f, (hour - 3f) / 2.5f) : hour < 7f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (hour - 7f) / 3f);
    public static bool WeatherOnlyAtmosphere { get; set; }

    public static float FogMultiplier(float influence) =>
        WeatherOnlyAtmosphere ? WeatherFog : Mathf.Lerp(1f, WeatherFog, influence);

    /// <summary>Volumetric fog density (extinction per metre) at its base, weather included, as last drawn.</summary>
    public static float FogDensity { get; set; }

    /// <summary>How dark it is: 0 by day, 1 at night (DayNightCycle; 0 without a sky rig).</summary>
    public static float Night { get; set; }

    /// <summary>How much of their sight range monsters keep in the current fog and dark (PerceptionSettings › Fog and Dark).</summary>
    public static float SightScale(PerceptionSettings rules) =>
        PerceptionRules.FogSightScale(FogDensity, rules.DenseFog, rules.SightInDenseFog)
        * Mathf.Lerp(1f, rules.SightAtNight, Mathf.Clamp01(Night));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        WeatherFog = 1f;
        WeatherWind = 1f;
        WeatherMist = 0f;
        WeatherRain = 0f;
        GoldenHour = 0f;
        Hour = 12f;
        Morning = 0f;
        WeatherOnlyAtmosphere = false;
        FogDensity = 0f;
        Night = 0f;
    }
}
