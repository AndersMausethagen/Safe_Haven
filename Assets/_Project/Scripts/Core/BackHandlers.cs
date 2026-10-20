using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Decides what Escape (back) does, so one key press never triggers several systems at once. The Escape menu is the only
/// listener for the key: it asks the handlers here from highest to lowest priority, and the first one that returns true
/// has used the press. Only when nobody used it does the Escape menu open.
/// <para>
/// To make something cancellable with Escape, register a handler in OnEnable and unregister it in OnDisable. The
/// handler returns false when it has nothing to cancel.
/// </para>
/// </summary>
public static class BackHandlers
{
    /// <summary>Placing or moving a building.</summary>
    public const int BuildPlacementPriority = 300;
    /// <summary>Open windows (bag, character, stash, menus...).</summary>
    public const int WindowsPriority = 200;
    /// <summary>A timed interaction in the world, such as gathering.</summary>
    public const int InteractionPriority = 100;

    private struct Entry
    {
        public int Priority;
        public Func<bool> Handler;
    }

    private static readonly List<Entry> entries = new List<Entry>();

    public static void Register(int priority, Func<bool> handler)
    {
        if (handler == null)
        {
            return;
        }

        Unregister(handler);
        int index = entries.FindIndex(entry => entry.Priority < priority);
        entries.Insert(index < 0 ? entries.Count : index, new Entry { Priority = priority, Handler = handler });
    }

    public static void Unregister(Func<bool> handler)
    {
        entries.RemoveAll(entry => entry.Handler == handler);
    }

    /// <summary>Gives the press to the handlers in priority order. Returns true if one of them used it.</summary>
    public static bool TryHandle()
    {
        // Copied because a handler may unregister itself (e.g. a window that closes and disables).
        Entry[] snapshot = entries.ToArray();
        foreach (Entry entry in snapshot)
        {
            try
            {
                if (entry.Handler())
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        entries.Clear();
    }
}
