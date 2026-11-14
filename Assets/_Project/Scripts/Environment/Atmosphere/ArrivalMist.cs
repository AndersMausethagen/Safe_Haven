using UnityEngine;

/// <summary>
/// Mist that rolls round a spot and thins away: set down around the player as a raid's first-visit cinematic fades into
/// the game (RaidDefinition › Arrival Effect). Drives the <see cref="MistVolume"/> on the same object from thick to
/// nothing over its life, then removes itself. Game time, so it waits through a pause.
/// </summary>
[RequireComponent(typeof(MistVolume))]
public sealed class ArrivalMist : MonoBehaviour
{
    [Tooltip("Seconds it takes to come in (it starts already part-way, as the screen is still dark).")]
    [SerializeField, Min(0f)] private float rollIn = 1.5f;
    [Tooltip("How thick it is when the picture appears, as a share of the volume's density.")]
    [SerializeField, Range(0f, 1f)] private float startAt = 0.6f;
    [Tooltip("Seconds it stays at its thickest.")]
    [SerializeField, Min(0f)] private float hold = 6f;
    [Tooltip("Seconds it takes to thin away to nothing.")]
    [SerializeField, Min(0.1f)] private float thinOut = 14f;
    [Tooltip("Metres below the spot the mist starts: the volume rises from its base, and ground is rarely flat.")]
    [SerializeField, Min(0f)] private float sink = 1.5f;

    private MistVolume volume;
    private float age;

    /// <summary>How thick the mist is <paramref name="seconds"/> after it was set down, 0..1.</summary>
    public static float Thickness(float seconds, float rollIn, float startAt, float hold, float thinOut)
    {
        if (seconds < rollIn)
        {
            return Mathf.Lerp(startAt, 1f, Mathf.SmoothStep(0f, 1f, seconds / Mathf.Max(0.01f, rollIn)));
        }

        float thinning = (seconds - rollIn - hold) / Mathf.Max(0.1f, thinOut);
        return thinning <= 0f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(thinning));
    }

    public float Lifetime => rollIn + hold + thinOut;

    private void Awake()
    {
        volume = GetComponent<MistVolume>();
        volume.DensityScale = startAt;
        transform.position += Vector3.down * sink;
    }

    private void Update()
    {
        age += Time.deltaTime;
        volume.DensityScale = Thickness(age, rollIn, startAt, hold, thinOut);
        if (age >= Lifetime)
        {
            Destroy(gameObject);
        }
    }
}
