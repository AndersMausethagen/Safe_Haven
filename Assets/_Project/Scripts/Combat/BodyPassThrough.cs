using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bodies the player passes through for a moment (a dodge, a weapon art's dash, the lock of a finishing blow), counted per pair, so one
/// of them giving a body back doesn't make it solid while another still passes through it (a dodge out of a dash's wind-up kept snagging
/// on the bodies it should go through).
/// </summary>
public static class BodyPassThrough
{
    private static readonly Dictionary<(Collider, Collider), int> Counts = new Dictionary<(Collider, Collider), int>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Counts.Clear();

    /// <summary>Lets <paramref name="mover"/> pass through <paramref name="body"/> until as many <see cref="Restore"/> calls.</summary>
    public static void Ignore(Collider mover, Collider body)
    {
        if (mover == null || body == null)
        {
            return;
        }

        var key = (mover, body);
        Counts.TryGetValue(key, out int count);
        Counts[key] = count + 1;
        if (count == 0)
        {
            Physics.IgnoreCollision(mover, body, true);
        }
    }

    /// <summary>One user no longer passes through: solid again once none does.</summary>
    public static void Restore(Collider mover, Collider body)
    {
        var key = (mover, body);
        if (!Counts.TryGetValue(key, out int count))
        {
            return;
        }

        if (count > 1)
        {
            Counts[key] = count - 1;
            return;
        }

        Counts.Remove(key);
        if (mover != null && body != null)
        {
            Physics.IgnoreCollision(mover, body, false);
        }
    }
}
