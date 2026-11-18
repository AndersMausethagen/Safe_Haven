using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Every game scene entered once in Play mode, and nothing in any of them throws or logs an error (Docs/BugPatterns.md, third
/// round): the prologue as a new game starts it, the Haven, then each raid and arena from the Haven, a fight started in each, and
/// home through the results screen. The other way to find many bugs at once: a scene's systems meeting each other, which no test
/// of one system sees. GameLoopLiveTests covers the Misty Woods and the save made after it; this covers the rest. Saves go to a
/// folder of the test's own. Takes several minutes (a scene load each).
/// </summary>
public sealed class SceneSmokeLiveTests
{
    private const string Haven = "SafeHaven";
    private const string Prologue = "Prologue_MistyWoods";

    private static readonly string[] Raids =
    {
        "Assets/_Project/Data/Raids/TrollValley.asset",
        "Assets/_Project/Data/Raids/MossMarsh.asset",
        "Assets/_Project/Data/Raids/BrukamsBridge.asset",
        "Assets/_Project/Data/Raids/GuardiansHollow.asset",
    };

    private readonly List<string> problems = new List<string>();
    private string folder;
    private string where = "";

    [UnityTest, Timeout(1800000)]
    public IEnumerator EveryScene_RunsWithoutAnErrorOrAnException()
    {
        LogAssert.ignoreFailingMessages = true;
        yield return new EnterPlayMode();
        Application.logMessageReceived += Heard;
        folder = Path.Combine(Path.GetTempPath(), "SafeHavenSceneSmoke_" + Guid.NewGuid().ToString("N"));
        SaveGameService.Storage = new SaveStorage(folder);

        // The prologue, as the main menu's New Game starts it.
        where = Prologue;
        Assert.IsTrue(SaveGameService.TryStartNewGame(Prologue, out string problem), "the prologue: " + problem);
        yield return Until(() => In(Prologue), 180f, "the prologue never loaded with a player");
        yield return Settle();

        // The Haven, then each raid from it and home again.
        where = Haven;
        Assert.IsTrue(SaveGameService.TryStartNewGame(Haven, out problem), "the Haven: " + problem);
        yield return Until(() => In(Haven), 180f, "the Haven never loaded with a player");
        Cheats.SetInvincible(true);
        // Each raid is reached by the one before it (Troll Valley lies beyond the Misty Woods): all of them open here.
        Cheats.SetNoRequirements(true);
        yield return Settle();
        foreach (string path in Raids)
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(path);
            Assert.IsNotNull(raid, path);
            where = raid.name;
            Assert.IsTrue(Cheats.StartRaid(raid, out string launched), raid.name + ": " + launched);
            yield return Until(() => CinematicScreen.IsPlaying || Running(), 240f, raid.name + " never loaded");
            if (CinematicScreen.IsPlaying)
            {
                yield return Wait(1f);
                CinematicScreen.Stop();
            }

            yield return Until(Running, 90f, raid.name + " never started");
            RaidDirector.Current.Session.AddTime(900f);
            Cheats.SetInvincible(true);
            yield return Settle();
            Cheats.KillNearby();
            yield return Wait(2f);

            // Home: extracted where the raid has a way out, lost where it has none (an arena).
            if (Cheats.ExtractFromRaid() != "Extracted.")
            {
                Cheats.FailRaid();
            }

            where = raid.name + " (the results)";
            yield return Until(() => Object.FindAnyObjectByType<RaidResultsScreen>() != null && !SceneTransition.IsBusy, 90f, raid.name + ": no results screen");
            yield return Wait(1f);
            var screen = Object.FindAnyObjectByType<RaidResultsScreen>();
            var button = (Button)typeof(RaidResultsScreen).GetField("continueButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
            button.onClick.Invoke();
            where = Haven + " (after " + raid.name + ")";
            yield return Until(() => In(Haven) && GameSession.ActiveRaid == null, 180f, "never home from " + raid.name);
            Cheats.SetInvincible(true);
            yield return Wait(3f);
        }

        CollectionAssert.IsEmpty(problems, "errors and exceptions on the way:\n" + string.Join("\n\n", problems.Distinct()));
    }

    [UnityTearDown]
    public IEnumerator PutThingsBack()
    {
        Application.logMessageReceived -= Heard;
        if (Application.isPlaying)
        {
            Cheats.SetInvincible(false);
            Cheats.SetNoRequirements(false);
            yield return new ExitPlayMode();
        }

        SaveGameService.Storage = new SaveStorage();
        GameSession.StartNewGame();
        LogAssert.ignoreFailingMessages = false;
        if (folder != null && Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    private void Heard(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        {
            problems.Add($"[{where}] {type}: {message}\n{string.Join("\n", stack.Split('\n').Take(6))}");
        }
    }

    private static bool In(string scene) => SceneManager.GetActiveScene().name == scene && Player.Current != null && !SceneTransition.IsBusy;

    private static bool Running() =>
        RaidDirector.Current != null && RaidDirector.Current.IsRunning && Player.Current != null && !SceneTransition.IsBusy && !CinematicScreen.IsPlaying;

    // A scene left to run for a few seconds: its first ticks, its late setups, a day's first weather.
    private static IEnumerator Settle()
    {
        for (int i = 0; i < 30; i++)
        {
            yield return null;
        }

        yield return Wait(6f);
    }

    private static IEnumerator Until(Func<bool> done, float seconds, string failure)
    {
        float waited = 0f;
        while (!done() && waited < seconds)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        Assert.IsTrue(done(), failure + $" ({seconds} s)");
    }

    private static IEnumerator Wait(float seconds)
    {
        for (float waited = 0f; waited < seconds; waited += Time.unscaledDeltaTime)
        {
            yield return null;
        }
    }
}
