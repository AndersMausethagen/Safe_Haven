using UnityEngine;

/// <summary>
/// A breed that glows in the dark (HorseAbility.NightGlow: the bog's toxic horse): a soft light about it from dusk to
/// dawn, fading in and out with the hour, out when it dies. A light to ride by at night, and a way to find it in the bog.
/// </summary>
[DisallowMultipleComponent]
public sealed class HorseGlow : MonoBehaviour
{
    [SerializeField] private Light glow;
    [SerializeField, Min(0f)] private float intensity = 1.6f;

    private Horse horse;
    private float level;

    private void Awake() => horse = GetComponent<Horse>();

    private void Update()
    {
        if (glow == null)
        {
            return;
        }

        float hour = AtmosphereConditions.Hour;
        // Full from 20:00 to 05:00, easing in from 18:00 and out by 07:00.
        float night = hour >= 20f || hour < 5f ? 1f : hour >= 18f ? Mathf.InverseLerp(18f, 20f, hour) : hour < 7f ? Mathf.InverseLerp(7f, 5f, hour) : 0f;
        float wanted = horse != null && horse.IsAlive ? night : 0f;
        level = Mathf.MoveTowards(level, wanted, Time.deltaTime * 0.5f);
        glow.intensity = intensity * level;
        glow.enabled = level > 0.01f;
    }
}
