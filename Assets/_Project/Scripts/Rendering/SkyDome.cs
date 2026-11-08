using UnityEngine;

/// <summary>
/// Puts the game's sky (RenderingSettings › Sky Material, Shaders/Sky.shader) on every scene that keeps time (has a
/// DayNightCycle) and tells it, each frame, where the sun and moon stand, the moon's phase, the light of the hour and
/// lightning. Clouds come from CloudLayer. Works on a copy, so the material asset never changes in play. Made by
/// WorldRendering.
/// </summary>
[DefaultExecutionOrder(95)] // after DayNightCycle and WeatherSystem
public sealed class SkyDome : MonoBehaviour
{
    private static readonly int SunId = Shader.PropertyToID("_SkySunDirection");
    private static readonly int MoonId = Shader.PropertyToID("_SkyMoonDirection");
    private static readonly int SunColourId = Shader.PropertyToID("_SkySunColour");
    private static readonly int StateId = Shader.PropertyToID("_SkyState");

    private RenderingSettings settings;
    private Material sky;
    private DayNightCycle cycle;

    public void Initialise(RenderingSettings rules)
    {
        settings = rules;
        if (settings.SkyMaterial != null)
        {
            sky = new Material(settings.SkyMaterial) { name = settings.SkyMaterial.name + " (play)", hideFlags = HideFlags.DontSave };
        }
    }

    /// <summary>Takes over this scene's sky if it keeps time; a scene without a clock (a menu) keeps its own.</summary>
    public void Bind()
    {
        cycle = FindAnyObjectByType<DayNightCycle>();
        if (sky != null && cycle != null)
        {
            RenderSettings.skybox = sky;
        }
    }

    /// <summary>The moon's phase on a day: 0 new, 0.5 full. A whole cycle takes RenderingSettings › Moon Cycle Days.</summary>
    public static float MoonPhase(float days, float cycleDays, float atStart) =>
        Mathf.Repeat(atStart + days / Mathf.Max(1f, cycleDays), 1f);

    private void LateUpdate()
    {
        if (cycle == null || cycle.Profile == null)
        {
            return;
        }

        float hour = cycle.Hour;
        cycle.Profile.SkyAt(hour, out Vector3 sun, out Vector3 moon, out Color sunColour);
        float days = (GameSession.Settlement != null ? GameSession.Settlement.DaysPassed : 0) + hour / 24f;
        float phase = MoonPhase(days, settings.MoonCycleDays, settings.MoonPhaseAtStart);
        DayNightState state = cycle.State;
        float flash = WeatherSystem.Active != null ? WeatherSystem.Active.LightningFlash : 0f;
        float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.2f, sun.y));

        Shader.SetGlobalVector(SunId, new Vector4(sun.x, sun.y, sun.z, 0f));
        Shader.SetGlobalVector(MoonId, new Vector4(moon.x, moon.y, moon.z, phase));
        Shader.SetGlobalColor(SunColourId, sunColour); // converted to linear by Unity
        Shader.SetGlobalVector(StateId, new Vector4(daylight, state.Night, state.GoldenHour, flash));
    }

    private void OnDestroy()
    {
        if (sky != null)
        {
            Destroy(sky);
        }
    }
}
