using System;
using UnityEngine;

/// <summary>
/// How a raid adds roaming creatures around the player on top of its seeded camps (RoamingSpawner): when they start,
/// how many may be out at once and in total, and how far from the player they appear and are cleaned up. The number
/// out at once grows with each danger stage and each extraction alarm, and scales with the raid's enemy density.
/// </summary>
[Serializable]
public sealed class RoamingSpawnSettings
{
    [Tooltip("What roams. Empty turns roaming spawns off for this raid.")]
    [SerializeField] private SpawnTable table;
    [Tooltip("Seconds into the raid before the first roamer may appear, so the start belongs to the seeded layout.")]
    [SerializeField, Min(0f)] private float firstDelay = 60f;
    [Tooltip("Seconds between roaming spawns.")]
    [SerializeField, Min(1f)] private float interval = 20f;
    [Tooltip("Roaming creatures allowed out at once before any danger stage or alarm.")]
    [SerializeField, Min(0)] private int baseAlive = 2;
    [Tooltip("Extra roaming creatures allowed out at once for each danger stage that has begun.")]
    [SerializeField, Min(0)] private int perStage = 1;
    [Tooltip("Extra roaming creatures allowed out at once for each extraction alarm raised.")]
    [SerializeField, Min(0)] private int perAlarm = 2;
    [Tooltip("Never more roaming creatures out at once than this, whatever the stage, alarms and density.")]
    [SerializeField, Min(0)] private int maxAlive = 8;
    [Tooltip("Most roaming creatures one raid can spawn, so they can't be farmed for loot. Ones cleaned up far from " +
             "the player don't count.")]
    [SerializeField, Min(0)] private int totalLimit = 12;
    [Tooltip("Closest a roamer appears to the player (metres). It also has to be out of sight.")]
    [SerializeField, Min(5f)] private float minDistance = 35f;
    [Tooltip("Farthest a roamer appears from the player (metres).")]
    [SerializeField, Min(5f)] private float maxDistance = 60f;
    [Tooltip("Roamers that are not fighting and are this far from the player, out of sight, are removed and refunded.")]
    [SerializeField, Min(10f)] private float despawnDistance = 90f;
    [Tooltip("A roamer never appears this close to another living creature, so it doesn't land in a camp.")]
    [SerializeField, Min(0f)] private float campClearance = 20f;
    [Tooltip("Roamers walk to a random spot this close to the player, then look around there.")]
    [SerializeField, Min(0f)] private float huntSpread = 15f;

    public SpawnTable Table => table;
    public bool Enabled => table != null && totalLimit > 0 && maxAlive > 0;
    public float FirstDelay => firstDelay;
    public float Interval => interval;
    public int TotalLimit => totalLimit;
    public float MinDistance => Mathf.Min(minDistance, maxDistance);
    public float MaxDistance => Mathf.Max(minDistance, maxDistance);
    public float DespawnDistance => Mathf.Max(despawnDistance, MaxDistance + 5f);
    public float CampClearance => campClearance;
    public float HuntSpread => huntSpread;

    /// <summary>Roaming creatures allowed out at once after the given stages and alarms, scaled by enemy density.</summary>
    public int AliveCap(int stage, int alarms, float density)
    {
        int wanted = baseAlive + perStage * Mathf.Max(0, stage) + perAlarm * Mathf.Max(0, alarms);
        return Mathf.Clamp(Mathf.RoundToInt(wanted * Mathf.Max(0f, density)), 0, maxAlive);
    }

    /// <summary>Copies the settings with a different table and limits; for tests and editor tools.</summary>
    public static RoamingSpawnSettings Create(SpawnTable table, int baseAlive, int perStage, int perAlarm, int maxAlive, int totalLimit)
    {
        return new RoamingSpawnSettings
        {
            table = table,
            baseAlive = baseAlive,
            perStage = perStage,
            perAlarm = perAlarm,
            maxAlive = maxAlive,
            totalLimit = totalLimit,
        };
    }
}
