using System;
using UnityEngine;

/// <summary>
/// Sounds monsters can hear. <see cref="Emit"/> first raises <see cref="Emitted"/> (CreatureSleep wakes sleepers inside
/// the radius) and then lets every awake monster hear it (MonsterAI.Hear: suspicious ones come to look). The radius is
/// how far the sound carries before a monster's own hearing multiplier and any cover in between. What the player does
/// goes through PlayerNoise, which fills the HUD's meter; call Emit directly for sounds of the world (a beacon, an arrow
/// striking far away).
/// </summary>
public static class WorldNoise
{
    /// <summary>Raised for every sound, with its position and radius.</summary>
    public static event Action<Vector3, float> Emitted;

    public static void Emit(Vector3 position, float radius)
    {
        if (radius <= 0f)
        {
            return;
        }

        Emitted?.Invoke(position, radius);
        var monsters = MonsterAI.Active;
        for (int i = monsters.Count - 1; i >= 0; i--)
        {
            monsters[i].Hear(position, radius);
        }
    }

    // Entering play mode doesn't reload scripts in this project, so the event is cleared here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Emitted = null;
    }
}
