using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Rumble on the controller in use. Whatever shakes the camera shakes the pad too (CameraShake.Add calls
/// <see cref="FromShake"/>: hits, hurts, kills, shots, falling trees, heavy footfalls), fading quickly; a few moments have
/// a pulse of their own (a parry's sharp buzz, a fish's tug). Only while the player is on a controller, scaled by the
/// Vibration option, and the motors stop while the game is paused, out of focus or on quitting. Strengths are in
/// ControllerSettings.
/// </summary>
public sealed class ControllerRumble : MonoBehaviour, IGameSystem
{
    private static ControllerRumble current;

    private float shakeLow, shakeHigh;
    private float pulseLow, pulseHigh, pulseUntil;
    private Gamepad driven;
    private bool running;

    public void Initialise()
    {
        current = this;
    }

    public void OnSceneEntered(Scene scene) => Stop();

    /// <summary>A share of a camera shake (0-1), felt in the hands: more in the heavy motor than the light one.</summary>
    public static void FromShake(float amount)
    {
        if (current == null || amount <= 0f)
        {
            return;
        }

        float strength = amount * ControllerSettings.Current.RumblePerShake;
        current.shakeLow = Mathf.Max(current.shakeLow, strength);
        current.shakeHigh = Mathf.Max(current.shakeHigh, strength * 0.6f);
    }

    /// <summary>A pulse of its own: low (heavy) and high (light) motor strengths and seconds, as ControllerSettings keeps them.</summary>
    public static void Pulse(Vector3 lowHighSeconds)
    {
        if (current == null || lowHighSeconds.z <= 0f)
        {
            return;
        }

        current.pulseLow = Mathf.Max(current.pulseLow, lowHighSeconds.x);
        current.pulseHigh = Mathf.Max(current.pulseHigh, lowHighSeconds.y);
        current.pulseUntil = Mathf.Max(current.pulseUntil, Time.unscaledTime + lowHighSeconds.z);
    }

    private void Update()
    {
        ControllerSettings settings = ControllerSettings.Current;
        float fade = settings.RumbleFade * Time.unscaledDeltaTime;
        shakeLow = Mathf.Max(0f, shakeLow - fade);
        shakeHigh = Mathf.Max(0f, shakeHigh - fade * 1.5f);
        if (Time.unscaledTime >= pulseUntil)
        {
            pulseLow = pulseHigh = 0f;
        }

        Gamepad pad = InputDevices.LastPad;
        float scale = GameOptions.Vibration.Value;
        bool felt = InputDevices.UsingGamepad && pad != null && scale > 0f && !GamePause.IsPaused && Application.isFocused;
        float low = felt ? Mathf.Min(settings.RumbleMax, Mathf.Max(shakeLow, pulseLow) * scale) : 0f;
        float high = felt ? Mathf.Min(settings.RumbleMax, Mathf.Max(shakeHigh, pulseHigh) * scale) : 0f;

        if (driven != null && driven != pad)
        {
            driven.ResetHaptics();
            driven = null;
            running = false;
        }

        if (low > 0.01f || high > 0.01f)
        {
            pad.SetMotorSpeeds(low, high);
            driven = pad;
            running = true;
        }
        else if (running)
        {
            Stop();
        }
    }

    private void Stop()
    {
        shakeLow = shakeHigh = pulseLow = pulseHigh = 0f;
        if (driven != null && driven.added)
        {
            driven.ResetHaptics();
        }

        driven = null;
        running = false;
    }

    private void OnDisable() => Stop();

    private void OnApplicationQuit() => Stop();

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        current = null;
    }
}
