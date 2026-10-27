using UnityEngine;

/// <summary>
/// What the atmosphere is like right now, shared between the systems that make it and those that feel it: the weather's
/// fog factor (DayNightCycle writes it from the WeatherSystem; 1 without weather) and the fog's density near the ground
/// as AtmosphereFeature last drew it (0 without fog). Monster sight shrinks in thick fog through
/// <see cref="SightScale"/>. Runtime state only, reset every play session.
/// </summary>
public static class AtmosphereConditions
{
    /// <summary>The current weather's fog multiplier (Clear 1, Mist more). Set by DayNightCycle.</summary>
    public static float WeatherFog { get; set; } = 1f;

    /// <summary>Volumetric fog density (extinction per metre) at its base, weather included, as last drawn.</summary>
    public static float FogDensity { get; set; }

    /// <summary>How much of their sight range monsters keep in the current fog (PerceptionSettings › Fog).</summary>
    public static float SightScale(PerceptionSettings rules) =>
        PerceptionRules.FogSightScale(FogDensity, rules.DenseFog, rules.SightInDenseFog);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        WeatherFog = 1f;
        FogDensity = 0f;
    }
}
