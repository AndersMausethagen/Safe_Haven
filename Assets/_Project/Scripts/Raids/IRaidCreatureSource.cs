using UnityEngine;

/// <summary>
/// Where creatures are made when not in this game: co-op, a friend on the host's run (or in the host's Haven) sees the
/// host's creatures, so what this player wakes in a raid (an extraction beacon's alarm, an event's creatures) is asked of
/// the host's game, which makes them there and sends them after this player. Set on <see cref="CreatureSource.Remote"/>
/// by the co-op side.
/// </summary>
public interface IRaidCreatureSource
{
    /// <summary>Asks for a waiting spawn group to be filled where the creatures are made; false when it can't be asked.</summary>
    bool Wake(SpawnGroup group);

    /// <summary>Asks for one creature (or pack) of <paramref name="prefab"/> to be made where the creatures are made.</summary>
    void Spawn(GameObject prefab, Vector3 position, float yaw, bool hunt);
}

/// <summary>
/// Whether this game makes its own creatures. <see cref="Remote"/> is set while another game makes them (co-op: on the
/// host's run, or in the host's Haven): spawn groups, roaming spawns, set-piece guards and mob zones then make none, and
/// a raid's alarms and events ask the remote source. Null when this game makes its own.
/// </summary>
public static class CreatureSource
{
    public static IRaidCreatureSource Remote { get; set; }
}
