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
    public static float GoldenHour { get; set; }
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
        GoldenHour = 0f;
        WeatherOnlyAtmosphere = false;
        FogDensity = 0f;
        Night = 0f;
    }
}
