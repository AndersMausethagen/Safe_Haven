using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the players on a raid stand: this game's own and, in co-op, its friends' bodies (FriendHitbox), each while up. A raid's
/// systems that ask "near the player" ask here (Docs/BugPatterns.md, fifth round): asked of the host's own player alone, a guest
/// exploring apart walked past camps put to sleep around them, and a rockfall could close the only way out of the clearing a
/// friend stood in.
/// </summary>
public static class RaidPlayers
{
    /// <summary>The players' positions now (this game's player first), into <paramref name="into"/> (cleared first).</summary>
    public static void Positions(GameObject local, List<Vector3> into)
    {
        into.Clear();
        if (local != null)
        {
            into.Add(local.transform.position);
        }

        foreach (FriendHitbox friend in FriendHitbox.Active)
        {
            if (friend != null && friend.IsAlive)
            {
                into.Add(friend.transform.position);
            }
        }
    }

    /// <summary>The flat (XZ) squared distance from <paramref name="at"/> to the nearest of <paramref name="players"/>; infinity for none.</summary>
    public static float NearestFlatSqr(IReadOnlyList<Vector3> players, Vector3 at)
    {
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < players.Count; i++)
        {
            float dx = players[i].x - at.x;
            float dz = players[i].z - at.z;
            nearest = Mathf.Min(nearest, dx * dx + dz * dz);
        }

        return nearest;
    }
}
