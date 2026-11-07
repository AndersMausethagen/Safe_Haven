using UnityEngine;

/// <summary>
/// Where a raid's creatures are made when not in this game: co-op, a friend on the host's run sees the host's creatures,
/// so what this player wakes (an extraction beacon's alarm, an event's creatures) is asked of the host's game, which
/// makes them there and sends them after this player. Set on <see cref="RaidDirector.RemoteCreatures"/> by the co-op
/// side; null when this game makes its own.
/// </summary>
public interface IRaidCreatureSource
{
    /// <summary>Asks for a waiting spawn group to be filled where the creatures are made; false when it can't be asked.</summary>
    bool Wake(SpawnGroup group);

    /// <summary>Asks for one creature (or pack) of <paramref name="prefab"/> to be made where the creatures are made.</summary>
    void Spawn(GameObject prefab, Vector3 position, float yaw, bool hunt);
}
