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

    /// <summary>Current shake strength, 0-1.</summary>
    public static float Trauma => trauma;

    /// <summary>Adds shake. Amounts add up to at most 1.</summary>
    public static void Add(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        Decay();
        trauma = Mathf.Clamp01(trauma + amount * GameOptions.ScreenShake.Value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        trauma = 0f;
        lastDecayTime = 0f;
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
