using System.Collections.Generic;

/// <summary>
/// Holds the player's feet while something asks (a window the player studies, such as a horse's): the movement keys do
/// nothing until every asker lets go. Askers that are destroyed let go by themselves.
/// </summary>
public static class PlayerHold
{
    private static readonly HashSet<object> Holding = new HashSet<object>();

    public static bool Active
    {
        get
        {
            Holding.RemoveWhere(asker => asker is UnityEngine.Object thing && thing == null);
            return Holding.Count > 0;
        }
    }

    public static void Set(object asker, bool hold)
    {
        if (asker == null)
        {
            return;
        }

        if (hold)
        {
            Holding.Add(asker);
        }
        else
        {
            Holding.Remove(asker);
        }
    }

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Holding.Clear();
}
