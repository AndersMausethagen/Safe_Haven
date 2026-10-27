using UnityEngine;

/// <summary>
/// Makes a fire's light breathe: intensity and range drift around their authored values on smooth noise, each light out
/// of step with the others. Visual only. Put it on the Light of a torch or campfire.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class LightFlicker : MonoBehaviour
{
    [Tooltip("How far intensity strays from its authored value (share).")]
    [SerializeField, Range(0f, 1f)] private float intensityVariation = 0.25f;
    [Tooltip("How far range strays from its authored value (share).")]
    [SerializeField, Range(0f, 0.5f)] private float rangeVariation = 0.06f;
    [Tooltip("Flickers per second, roughly.")]
    [SerializeField, Min(0.1f)] private float speed = 4f;

    private Light fire;
    private float baseIntensity;
    private float baseRange;
    private float seed;

    private void Awake()
    {
        fire = GetComponent<Light>();
        baseIntensity = fire.intensity;
        baseRange = fire.range;
        seed = Random.value * 100f;
    }

    private void OnDisable()
    {
        if (fire != null)
        {
            fire.intensity = baseIntensity;
            fire.range = baseRange;
        }
    }

    private void Update()
    {
        float t = Time.time * speed + seed;
        // Two octaves: a slow swell and a quick flutter.
        float noise = Mathf.PerlinNoise(t, seed) * 0.7f + Mathf.PerlinNoise(t * 3.1f, seed + 17f) * 0.3f;
        float wave = noise * 2f - 1f;
        fire.intensity = baseIntensity * (1f + wave * intensityVariation);
        fire.range = baseRange * (1f + wave * rangeVariation);
    }
}
