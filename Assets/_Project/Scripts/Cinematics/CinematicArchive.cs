using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which cinematics have been seen on this computer, by any character: the main menu offers those to watch again
/// (CinematicsScreen). Kept in PlayerPrefs like the volume, not in a save, because the menu has no character. A
/// cinematic counts as seen once it starts (skipped or not), and a save that has reached one counts it too when its
/// scene starts, so saves from before this record fill it in.
/// </summary>
public static class CinematicArchive
{
    private const string Key = "Cinematics.Seen";
    private const char Separator = '|';

    private static HashSet<string> seen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => seen = null;

    public static bool HasSeen(CinematicDefinition cinematic) => cinematic != null && Seen.Contains(cinematic.name);

    /// <summary>Marks a cinematic as seen on this computer.</summary>
    public static void Remember(CinematicDefinition cinematic)
    {
        if (cinematic == null || !Seen.Add(cinematic.name))
        {
            return;
        }

        PlayerPrefs.SetString(Key, string.Join(Separator.ToString(), seen));
        PlayerPrefs.Save();
    }

    /// <summary>Marks every cinematic this character has reached, as the journal judges it.</summary>
    public static void RememberReached(JournalSettings settings, QuestLog log, CharacterProgress progress)
    {
        if (settings == null || log == null || progress == null)
        {
            return;
        }

        foreach (JournalCinematic entry in settings.Cinematics)
        {
            if (JournalContent.IsUnlocked(entry, log, progress))
            {
                Remember(entry.cinematic);
            }
        }
    }

    /// <summary>Forgets the record (tests).</summary>
    public static void Forget()
    {
        seen = null;
        PlayerPrefs.DeleteKey(Key);
    }

    private static HashSet<string> Seen =>
        seen ??= new HashSet<string>(PlayerPrefs.GetString(Key, "").Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries));
}
