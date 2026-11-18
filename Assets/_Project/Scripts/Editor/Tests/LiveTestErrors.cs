using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The errors, asserts and exceptions logged while a Play-mode test runs, from any thread (Docs/BugPatterns.md, sixth round). The
/// main thread's Application.logMessageReceived never hears what the renderer logs from its own threads, so a scene smoke test
/// listening there passed with the GPU Resident Drawer's errors in the console. Each message heard is told with where the test was.
/// What is not the game's to fix is listed below with the reason, never filtered by a weaker rule.
/// </summary>
public sealed class LiveTestErrors : IDisposable
{
    /// <summary>The start of a message that is not the game's while a scene changes (SceneTransition.IsBusy), and why.</summary>
    public static readonly Dictionary<string, string> NotTheGames = new Dictionary<string, string>
    {
        ["A BatchDrawCommand was submitted with an invalid Batch, Mesh, or Material ID"] =
            "the GPU Resident Drawer's own check while a scene unloads behind the loading screen (a raid's creatures' skins, the Moss " +
            "Marsh's lily pads; measured 2026-11-18: never at a kill, a corpse's end or a scene load of its own, only as a raid's scene " +
            "goes): nothing is drawn wrong in a frame anyone sees, and only the Editor checks it",
    };

    private readonly List<string> heard = new List<string>();
    private readonly object guard = new object();
    private readonly Func<string> where;

    /// <summary>Listens from now until disposed; <paramref name="where"/> says where the test is, for each message.</summary>
    public LiveTestErrors(Func<string> where)
    {
        this.where = where;
        Application.logMessageReceivedThreaded += Heard;
    }

    /// <summary>Every distinct problem heard so far.</summary>
    public List<string> All
    {
        get
        {
            lock (guard)
            {
                return heard.Distinct().ToList();
            }
        }
    }

    public void Dispose() => Application.logMessageReceivedThreaded -= Heard;

    private void Heard(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
        {
            return;
        }

        // Only while a scene changes, as its reason says: the same message mid-raid (a creature's mesh or material gone while drawn) is
        // the game's, and is told (the review of 2026-11-18).
        if (SceneTransition.IsBusy && NotTheGames.Keys.Any(start => message.StartsWith(start, StringComparison.Ordinal)))
        {
            return;
        }

        string at = where != null ? where() : "";
        lock (guard)
        {
            heard.Add($"[{at}] {type}: {message}\n{string.Join("\n", (stack ?? "").Split('\n').Take(6))}");
        }
    }
}
