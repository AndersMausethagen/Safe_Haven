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

    /// <summary>
    /// The nearest player still up to <paramref name="at"/> (this game's own unless down or dead, and friends' bodies), as something to
    /// strike, and how far (squared, in full 3D); null and infinity for none. A boss waking and a creature choosing whom to go for ask this:
    /// the boss kept its own copy, which took the host even when they lay downed, and its Engage was then refused (review 2026-11-27).
    /// </summary>
    public static IDamageable NearestUp(Player local, Vector3 at, out float sqrDistance)
    {
        IDamageable nearest = null;
        sqrDistance = float.PositiveInfinity;
        if (local != null && local.Health != null && local.Health.IsAlive && !CoopDowned.IsDown)
        {
            nearest = local.Health;
            sqrDistance = (local.transform.position - at).sqrMagnitude;
        }

        foreach (FriendHitbox friend in FriendHitbox.Active)
        {
            if (friend == null || !friend.IsAlive)
            {
                continue;
            }

            float distance = (friend.transform.position - at).sqrMagnitude;
            if (distance < sqrDistance)
            {
                sqrDistance = distance;
                nearest = friend;
            }
        }

        return nearest;
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
