using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The tutorials waiting to be read: mechanics pages of the journal that opened this session (the first swing at a tree, the first cast,
/// the first quest that needs them), oldest first. TutorialNoticeHud shows that some are waiting; TutorialWindow shows them and takes
/// them off the list once seen. Only what opened this session waits here: an older unread page is still marked in the journal.
/// </summary>
public static class Tutorials
{
    private static readonly List<JournalPage> pending = new List<JournalPage>();
    private static JournalPage latest;

    /// <summary>Raised whenever a tutorial is added or read.</summary>
    public static event Action Changed;

    public static IReadOnlyList<JournalPage> Pending => pending;

    /// <summary>The page that opened last, read or not: what the tutorial key shows when nothing is waiting.</summary>
    public static JournalPage Latest => latest;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        pending.Clear();
        latest = null;
        Changed = null;
    }

    public static string ReadKeyOf(JournalPage page) => "page:" + page.title;

    /// <summary>A mechanics page has just opened.</summary>
    public static void Add(JournalPage page)
    {
        if (page == null || pending.Contains(page))
        {
            return;
        }

        pending.Add(page);
        latest = page;
        Changed?.Invoke();
    }

    /// <summary>The page has been shown: it no longer waits, and the journal counts it as read.</summary>
    public static void MarkSeen(JournalPage page)
    {
        if (page == null)
        {
            return;
        }

        GameSession.Progress.Journal.MarkRead(ReadKeyOf(page));
        if (pending.Remove(page))
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Forgets what waits, for a new or loaded game.</summary>
    public static void Clear()
    {
        if (pending.Count == 0 && latest == null)
        {
            return;
        }

        pending.Clear();
        latest = null;
        Changed?.Invoke();
    }
}
