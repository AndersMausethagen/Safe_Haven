using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// The shake of an in-game cinematic's camera. It takes the game's shake (<see cref="CameraShake.Trauma"/>: a giant's
/// footfalls, a roar) but plays it as a filmed camera would feel it: a slow sway of the view, a few times a second, and
/// hardly any sideways jolt. The game's own shake is a quick 22 Hz judder meant for the high camera, and close up it made
/// the shots look jittery (the user, 2026-11-12).
/// </summary>
[AddComponentMenu("")]
public sealed class CinematicShake : CinemachineExtension
{
    private const float Frequency = 4.5f;
    private const float MaxDegrees = 1.6f;
    private const float MaxRoll = 1.2f;
    private const float MaxOffset = 0.05f;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || !Application.isPlaying)
        {
            return;
        }

        float trauma = CameraShake.Trauma;
        if (trauma <= 0f)
        {
            return;
        }

        float strength = trauma * trauma;
        float time = Time.unscaledTime * Frequency;
        var turn = Quaternion.Euler(Noise(time, 0f) * MaxDegrees * strength, Noise(time, 10f) * MaxDegrees * strength, Noise(time, 20f) * MaxRoll * strength);
        state.OrientationCorrection *= turn;
        state.PositionCorrection += state.GetCorrectedOrientation() * new Vector3(Noise(time, 30f), Noise(time, 40f), 0f) * (MaxOffset * strength);
    }

    private static float Noise(float time, float track) => Mathf.PerlinNoise(time, track) * 2f - 1f;
}
