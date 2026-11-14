using UnityEngine;

/// <summary>
/// A fire that is lit at dusk and put out at dawn: a building's wall torch. Its Lit child (the flame and its light) shows
/// while the Haven is dark (NightSettings.Darkness), each torch at its own moment, so they come on one by one as if
/// someone went round lighting them, not all in one frame. A building still going up or lying in ruin keeps its torches
/// out. Unlit it costs nothing: no light, no flame, no fog slot (AtmosphereLight follows its light).
/// </summary>
[DisallowMultipleComponent]
public sealed class NightFlame : MonoBehaviour
{
    [Tooltip("The flame and its light, shown while lit.")]
    [SerializeField] private GameObject lit;
    [Tooltip("Darkness (0 day, 1 night) at which the first torches are lit. Each goes out a little below its own moment, " +
             "so a wavering dusk doesn't blink it.")]
    [SerializeField, Range(0f, 1f)] private float lightAt = 0.4f;
    [Tooltip("How much more darkness the last torch waits for, at random: they come on one by one.")]
    [SerializeField, Range(0f, 0.4f)] private float spread = 0.2f;

    private const float Hysteresis = 0.06f;
    private const float CheckSeconds = 0.5f;
    private float threshold;
    private float nextCheck;
    private Construction building;

    public bool IsLit => lit != null && lit.activeSelf;

    public void Configure(GameObject flame) => lit = flame;

    private void Awake()
    {
        building = GetComponentInParent<Construction>();
        threshold = Mathf.Clamp01(lightAt + Random.value * spread);
        nextCheck = Time.time + Random.value * CheckSeconds;
        Show(Wanted(false));
    }

    private void Update()
    {
        if (Time.time < nextCheck)
        {
            return;
        }

        nextCheck = Time.time + CheckSeconds;
        Show(Wanted(IsLit));
    }

    private bool Wanted(bool now)
    {
        if (building != null && !building.IsStanding)
        {
            return false;
        }

        float dark = NightSettings.Darkness;
        return now ? dark >= threshold - Hysteresis : dark >= threshold;
    }

    private void Show(bool on)
    {
        if (lit != null && lit.activeSelf != on)
        {
            lit.SetActive(on);
        }
    }
}
