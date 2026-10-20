using UnityEngine;

/// <summary>
/// Rules every raid shares, edited in the Systems Editor (Raids > Global Rules): how far creatures sleep, the default
/// entry bands spawn lists measure from, a global multiplier on enemy counts and a ceiling on creatures alive at once.
/// A raid can override the bands on its Rules tab. Lives in Resources/RaidGlobalSettings; read only while playing.
/// </summary>
[CreateAssetMenu(fileName = "RaidGlobalSettings", menuName = "Safe Haven/Settings/Raid Global Settings")]
public sealed class RaidGlobalSettings : GameSettings<RaidGlobalSettings>
{
    [Header("Sleeping creatures")]
    [Tooltip("Idle creatures further than this from the player (ignoring height) go to sleep to save performance.")]
    [SerializeField, Min(0f)] private float sleepDistance = 85f;
    [Tooltip("Sleeping creatures closer than this wake up. Keep it below the sleep distance so creatures don't flicker.")]
    [SerializeField, Min(0f)] private float wakeDistance = 70f;

    [Header("Entry bands (default for every raid)")]
    [Tooltip("Spawn points closer than this to the player start (flat metres) count as Near.")]
    [SerializeField, Min(0f)] private float nearDistance = SpawnRules.DefaultNearDistance;
    [Tooltip("Spawn points farther than this from the player start count as Far; between Near and Far is Middle.")]
    [SerializeField, Min(0f)] private float farDistance = SpawnRules.DefaultFarDistance;

    [Header("Enemies")]
    [Tooltip("Multiplies every raid's enemy density: a quick way to make all raids busier or calmer. 1 is as authored.")]
    [SerializeField, Range(0f, 4f)] private float globalEnemyDensity = 1f;
    [Tooltip("Roaming spawns pause while this many creatures are alive in the raid. 0 means no limit.")]
    [SerializeField, Min(0)] private int maxCreaturesAlive;

    public float SleepDistance => sleepDistance;
    public float WakeDistance => Mathf.Min(wakeDistance, sleepDistance);
    public float NearDistance => nearDistance;
    public float FarDistance => Mathf.Max(nearDistance, farDistance);
    public float GlobalEnemyDensity => globalEnemyDensity;
    public int MaxCreaturesAlive => maxCreaturesAlive;

    private void OnValidate()
    {
        farDistance = Mathf.Max(nearDistance, farDistance);
        wakeDistance = Mathf.Min(wakeDistance, sleepDistance);
    }
}
