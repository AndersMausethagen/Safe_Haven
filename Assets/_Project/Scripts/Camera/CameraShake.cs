using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Trauma-based camera shake for the Cinemachine camera. Anything adds trauma (0-1) through <see cref="Add"/>; the shake
/// grows with trauma squared, so small hits stay subtle while big ones kick, and trauma fades over time. It runs on
/// real time, so it still moves during a hit stop. Strength, wobble speed and fade come from the CombatFeel settings.
/// <para>
/// Where it belongs: Camera. CombatFeedback adds it to the scene's Cinemachine camera when a scene starts, so no setup is
/// needed; other systems (explosions, a boss landing) can call CameraShake.Add too.
/// </para>
/// </summary>
[AddComponentMenu("Safe Haven/Camera Shake")]
public sealed class CameraShake : CinemachineExtension
{
    private static float trauma;
    private static float lastDecayTime;
    // The kick (Docs/CombatFeelReview_2026-11-19.md §15): a push of the frame along a blow, in metres, and when it began (real time).
    private static Vector3 kick;
    private static float kickAt = float.NegativeInfinity;
    private const float KickRise = 0.03f;
    private const float KickReturn = 0.12f;

    /// <summary>Pushes the frame along <paramref name="worldDirection"/> by <paramref name="metres"/>: up in 30 ms, sprung back over
    /// 120 ms, on real time so it plays through the hit stop. A push rather than a rattle, for a big blow's direction (Vlambeer's
    /// screen kick). Under the Screen Shake slider; a new kick replaces one still under way.</summary>
    public static void Kick(Vector3 worldDirection, float metres)
    {
        worldDirection.y = 0f;
        if (metres <= 0f || worldDirection.sqrMagnitude < 1e-6f)
        {
            return;
        }

        kick = worldDirection.normalized * (metres * GameOptions.ScreenShake.Value);
        kickAt = Time.unscaledTime;
    }

    /// <summary>The share of a kick's push at <paramref name="age"/> seconds: rising to 1 over the rise, eased back to 0 over the return.</summary>
    public static float KickShare(float age)
    {
        if (age < 0f || age >= KickRise + KickReturn)
        {
            return 0f;
        }

        if (age < KickRise)
        {
            return age / KickRise;
        }

        float back = (age - KickRise) / KickReturn;
        return 1f - back * back * (3f - 2f * back);
    }

    /// <summary>Current shake strength, 0-1.</summary>
    public static float Trauma
    {
        get
        {
            // Faded to now: the camera reading it may not be the one that fades it (an in-game cinematic's).
            Decay();
            return trauma;
        }
    }

    /// <summary>Adds shake. Amounts add up to at most 1.</summary>
    public static void Add(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        Decay();
        trauma = Mathf.Clamp01(trauma + amount * GameOptions.ScreenShake.Value);
        // Felt in the hands too, on its own option (Controller Vibration), whatever Screen Shake is set to.
        ControllerRumble.FromShake(amount);
    }

    /// <summary>Keeps the shake at least this strong (0-1) while called each frame: a rumble that lasts, such as being
    /// dragged by the Nøkken, rather than a single blow.</summary>
    public static void AtLeast(float amount)
    {
        Decay();
        trauma = Mathf.Max(trauma, Mathf.Clamp01(amount * GameOptions.ScreenShake.Value));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        trauma = 0f;
        lastDecayTime = 0f;
        kick = Vector3.zero;
        kickAt = float.NegativeInfinity;
    }

    private static void Decay()
    {
        float now = Time.unscaledTime;
        float elapsed = Mathf.Max(0f, now - lastDecayTime);
        lastDecayTime = now;
        trauma = Mathf.Max(0f, trauma - CombatFeelSettings.Current.ShakeRecovery * elapsed);
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || !Application.isPlaying)
        {
            return;
        }

        Decay();
        float kicked = KickShare(Time.unscaledTime - kickAt);
        if (kicked > 0f)
        {
            // Along the blow on the ground, which from above is along the screen.
            state.PositionCorrection += kick * kicked;
        }

        if (trauma <= 0f)
        {
            return;
        }

        CombatFeelSettings feel = CombatFeelSettings.Current;
        float strength = trauma * trauma;
        float time = Time.unscaledTime * feel.ShakeFrequency;
        // Perlin noise on three separate tracks gives a smooth, non-repeating wobble on each axis.
        var offset = new Vector3(Noise(time, 0f), Noise(time, 10f), Noise(time, 20f)) * (feel.ShakeMaxOffset * strength);
        float roll = Noise(time, 30f) * feel.ShakeMaxRoll * strength;

        state.PositionCorrection += state.GetCorrectedOrientation() * offset;
        state.OrientationCorrection *= Quaternion.Euler(0f, 0f, roll);
    }

    private static float Noise(float time, float track)
    {
        return Mathf.PerlinNoise(time, track) * 2f - 1f;
    }
}
