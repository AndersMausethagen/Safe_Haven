using UnityEngine;

/// <summary>How far from the raid's entry (the player start) a spawn group may fill its points.</summary>
public enum EntryBand
{
    Any = 0,
    Near = 1,
    Middle = 2,
    Far = 3,
}

/// <summary>
/// Raid-wide rules applied while filling spawn groups, made from the raid's definition (RaidDefinition.SpawnRules):
/// how many enemies compared to what the groups say, and where the near, middle and far bands around the entry lie.
/// </summary>
public readonly struct SpawnRules
{
    public const float DefaultNearDistance = 60f;
    public const float DefaultFarDistance = 110f;

    /// <summary>Normal density, no entry known (so every band accepts every point).</summary>
    public static SpawnRules Default => new SpawnRules(1f, null, DefaultNearDistance, DefaultFarDistance);

    public SpawnRules(float enemyDensity, Vector3? entry, float nearDistance, float farDistance, float areaScale = 1f)
    {
        AreaScale = Mathf.Max(0.01f, areaScale);
        EnemyDensity = Mathf.Max(0f, enemyDensity);
        Entry = entry;
        NearDistance = Mathf.Max(0f, nearDistance);
        FarDistance = Mathf.Max(NearDistance, farDistance);
    }

    /// <summary>How many times more ground this map has than the raid's normal one, and so how much more of what fills it.</summary>
    public float AreaScale { get; }
    /// <summary>Multiplier on the counts of groups that spawn enemies. 1 is as authored.</summary>
    public float EnemyDensity { get; }
    /// <summary>Where the player enters the raid; null ignores the bands.</summary>
    public Vector3? Entry { get; }
    /// <summary>Points closer than this (flat metres) are near the entry.</summary>
    public float NearDistance { get; }
    /// <summary>Points farther than this are far; between Near and Far is the middle.</summary>
    public float FarDistance { get; }

    public SpawnRules WithEntry(Vector3? entry) => new SpawnRules(EnemyDensity, entry, NearDistance, FarDistance, AreaScale);

    /// <summary>The band a point lies in, or Any when the entry is unknown.</summary>
    public EntryBand BandOf(Vector3 point)
    {
        if (!Entry.HasValue)
        {
            return EntryBand.Any;
        }

        Vector3 offset = point - Entry.Value;
        float distance = new Vector2(offset.x, offset.z).magnitude;
        return distance < NearDistance ? EntryBand.Near : distance > FarDistance ? EntryBand.Far : EntryBand.Middle;
    }

    public bool Accepts(Vector3 point, EntryBand band) => band == EntryBand.Any || !Entry.HasValue || BandOf(point) == band;

    /// <summary>A count scaled by the enemy density, rounded half up.</summary>
    public int Scale(int count) => Mathf.FloorToInt(count * EnemyDensity + 0.5f);
}
