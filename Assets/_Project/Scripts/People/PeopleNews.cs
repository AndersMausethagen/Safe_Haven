using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// News of the people (ranks, inspired mornings, marks, friends made; Docs/PeopleSystemReview_2026-11-23.md §2.10): at home it is told one
/// at a time, Pace Seconds apart, so a busy morning doesn't flood the screen; away (a raid, a friend's Haven) it is kept for the homecoming
/// card (Homecoming.Compose), a few lines at most. The moments the player sees for themselves (a scene overheard, a gift) speak at once
/// elsewhere and don't come through here.
/// </summary>
public static class PeopleNews
{
    private const float PaceSeconds = 8f;
    private const int MostWaiting = 12;
    private static readonly List<(string text, MessageKind kind)> waiting = new List<(string, MessageKind)>();
    private static readonly List<string> away = new List<string>();
    private static float nextAt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => ForgetGame();

    /// <summary>Forgets news of the game being played (a new game or a load: GameSession.Reset).</summary>
    public static void ForgetGame()
    {
        waiting.Clear();
        away.Clear();
        nextAt = 0f;
    }

    /// <summary>A piece of news: at home, told now or as soon as the last has had its moment; away, kept for the homecoming.</summary>
    public static void Tell(string text, MessageKind kind = MessageKind.Info)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (!Place.InOwnHaven)
        {
            // What was still waiting its turn at home goes with it, for the homecoming.
            foreach ((string text, MessageKind kind) left in waiting)
            {
                if (!away.Contains(left.text))
                {
                    away.Add(left.text);
                }
            }

            waiting.Clear();
            if (!away.Contains(text))
            {
                away.Add(text);
            }

            if (away.Count > MostWaiting)
            {
                away.RemoveAt(0);
            }

            return;
        }

        if (Time.unscaledTime >= nextAt && waiting.Count == 0)
        {
            Post(text, kind);
            return;
        }

        waiting.Add((text, kind));
        if (waiting.Count > MostWaiting)
        {
            waiting.RemoveAt(0);
        }
    }

    /// <summary>At home: the next waiting piece, once the last has had its moment (SettlementSystem.Update).</summary>
    public static void Tick()
    {
        if (waiting.Count > 0 && Time.unscaledTime >= nextAt && Place.InOwnHaven)
        {
            (string text, MessageKind kind) = waiting[0];
            waiting.RemoveAt(0);
            Post(text, kind);
        }
    }

    /// <summary>What happened among the people while the player was away, for the homecoming card (at most <paramref name="most"/>
    /// lines; the rest summed up), and forgotten once taken.</summary>
    public static List<string> TakeAway(int most)
    {
        var lines = new List<string>();
        int shown = Mathf.Min(most, away.Count);
        for (int i = 0; i < shown; i++)
        {
            lines.Add(away[away.Count - shown + i]);
        }

        if (away.Count > shown && lines.Count > 0)
        {
            lines[lines.Count - 1] += $" (and {away.Count - shown} more among your people)";
        }

        away.Clear();
        return lines;
    }

    private static void Post(string text, MessageKind kind)
    {
        GameMessages.Post(text, kind);
        nextAt = Time.unscaledTime + PaceSeconds;
    }
}
