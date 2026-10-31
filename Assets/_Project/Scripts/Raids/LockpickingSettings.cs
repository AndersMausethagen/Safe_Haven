using UnityEngine;

/// <summary>
/// Rules of picking a locked cache (Resources/LockpickingSettings, Systems Editor › World › Lockpicking): how the bar feels,
/// how many slips a lock forgives and how far each press and the opening lid are heard. How many pins a cache has is set
/// on the cache (LootCache). Read by PlayerLockpicking.
/// </summary>
[CreateAssetMenu(fileName = "LockpickingSettings", menuName = "Safe Haven/Settings/Lockpicking Settings")]
public sealed class LockpickingSettings : GameSettings<LockpickingSettings>
{
    [Header("Bar")]
    [Tooltip("Seconds for the marker to go up and back down.")]
    [SerializeField, Range(0.3f, 4f)] private float cycleSeconds = 1.3f;
    [Tooltip("Width of the yellow zone on the first pin, as a share of the bar.")]
    [SerializeField, Range(0.05f, 1f)] private float normalZoneWidth = 0.34f;
    [Tooltip("Width of the green zone on the first pin.")]
    [SerializeField, Range(0.01f, 0.5f)] private float criticalZoneWidth = 0.1f;
    [Tooltip("Share of both widths lost with each pin set, so the last pin is the hardest.")]
    [SerializeField, Range(0f, 0.3f)] private float narrowingPerPin = 0.12f;
    [Tooltip("How far the zones may move from the middle of the bar each press, so it can't be done by rhythm alone.")]
    [SerializeField, Range(0f, 0.4f)] private float zoneJitter = 0.25f;

    [Header("Slips")]
    [Tooltip("Slips after which the lock gives way by force: the cache opens at once, as loud as Forced Noise.")]
    [SerializeField, Range(1, 6)] private int slipsAllowed = 3;
    [Tooltip("Taking damage stops the picking; the lock must be started again.")]
    [SerializeField] private bool interruptOnHit = true;
    [Tooltip("Seconds per pin with auto-gather on (Options › Accessibility): pins set themselves, never in the green zone.")]
    [SerializeField, Range(0.2f, 3f)] private float autoPinSeconds = 1f;

    [Header("Noise (radius in metres; monsters inside hear it)")]
    [Tooltip("A pin set in the green zone.")]
    [SerializeField, Min(0f)] private float criticalNoise = 1.5f;
    [Tooltip("A pin set in the yellow zone.")]
    [SerializeField, Min(0f)] private float normalNoise = 5f;
    [Tooltip("A slip rattling the lock.")]
    [SerializeField, Min(0f)] private float slipNoise = 12f;
    [Tooltip("The lock giving way by force, instead of the opening lid.")]
    [SerializeField, Min(0f)] private float forcedNoise = 20f;
    [Tooltip("The lid coming up on a picked lock, however well it was done.")]
    [SerializeField, Min(0f)] private float openNoise = 7f;

    [Header("Sounds (empty: none)")]
    [SerializeField] private SoundDefinition pinSound;
    [SerializeField] private SoundDefinition slipSound;
    [SerializeField] private SoundDefinition forcedSound;
    [SerializeField] private SoundDefinition openSound;

    public float CycleSeconds => cycleSeconds;
    public float NormalZoneWidth => normalZoneWidth;
    public float CriticalZoneWidth => criticalZoneWidth;
    public float NarrowingPerPin => narrowingPerPin;
    public float ZoneJitter => zoneJitter;
    public int SlipsAllowed => slipsAllowed;
    public bool InterruptOnHit => interruptOnHit;
    public float AutoPinSeconds => autoPinSeconds;
    public float ForcedNoise => forcedNoise;
    public float OpenNoise => openNoise;
    public SoundDefinition ForcedSound => forcedSound;
    public SoundDefinition OpenSound => openSound;

    /// <summary>How far a press with this result is heard.</summary>
    public float NoiseOf(SwingResult result)
    {
        return result == SwingResult.Critical ? criticalNoise : result == SwingResult.Normal ? normalNoise : slipNoise;
    }

    public SoundDefinition SoundOf(SwingResult result) => result == SwingResult.Glancing ? slipSound : pinSound;
}
