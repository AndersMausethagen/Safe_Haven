using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// The game's loop played through once in Play mode (Docs/BugPatterns.md, second round, fix 7): a new game in the Haven, into
/// the Misty Woods, creatures killed, extracted, the results screen's Continue pressed, home again, and the save made at home
/// counts the raid. Each part has tests of its own; this is the one that catches the seams between them (a scene that never
/// loads, a save written before the raid's end is counted, an exception on the way home). Saves go to a folder of the test's
/// own: the player's saves are left as they were, and checked so. Takes about two minutes (loading the Haven and a raid).
/// </summary>
public sealed class GameLoopLiveTests
{
    private const string Haven = "SafeHaven";
    private const string RaidPath = "Assets/_Project/Data/Raids/MistyWoods.asset";

    // Errors and exceptions from any thread (LiveTestErrors): it heard only the main thread's exceptions, so an error logged on the way
    // (or the renderer's) passed (the test audit of 2026-11-18).
    private LiveTestErrors problems;
    private string where = "a new game";
    private string folder;
    // Every data asset as it was before Play (DataAssets), put back after whatever the loop wrote.
    private Dictionary<ScriptableObject, string> dataBefore;

    [UnityTest, Timeout(600000)]
    public IEnumerator ANewGame_GoesOnARaid_AndComesHomeWithIt()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(RaidPath);
        Assert.IsNotNull(raid, RaidPath);
        Dictionary<string, DateTime> playersSaves = PlayersSaves();
        dataBefore = DataAssets();

        // Entered from the runner's own empty scene: opening the Haven in the Editor rebuilds its scatter (scene churn).
        LogAssert.ignoreFailingMessages = true;
        yield return new EnterPlayMode();
        problems = new LiveTestErrors(() => where);
        folder = Path.Combine(Path.GetTempPath(), "SafeHavenGameLoop_" + Guid.NewGuid().ToString("N"));
        SaveGameService.Storage = new SaveStorage(folder);

        // A new game, as the main menu starts one.
        Assert.IsTrue(SaveGameService.TryStartNewGame(Haven, out string problem), "new game: " + problem);
        yield return Until(() => AtHome(), 120f, "the Haven never loaded with a player");
        Assert.IsTrue(SaveGameService.TrySaveAsNew(out problem), "the first save: " + problem);
        string slot = SaveGameService.CurrentSlot;
        int extractions = GameSession.Home.SuccessfulExtractions;
        Cheats.SetInvincible(true);

        // Into the Misty Woods.
        where = raid.name;
        Assert.IsTrue(Cheats.StartRaid(raid, out string launched), "the raid: " + launched);
        // A new character's first raid opens with its cinematic (The Misty Woods): seen to play, then ended as a player skips it.
        // Left to play out, the raid waited on it and the test on the clock (failed once right after an Editor restart).
        yield return Until(() => CinematicScreen.IsPlaying || (RaidDirector.Current != null && RaidDirector.Current.IsRunning), 180f, "the raid never loaded");
        Assert.IsTrue(CinematicScreen.IsPlaying, "a first raid opens with its cinematic");
        yield return Wait(2f);
        CinematicScreen.Stop();
        yield return Until(() => RaidDirector.Current != null && RaidDirector.Current.IsRunning && Player.Current != null && !SceneTransition.IsBusy,
            60f, "the raid never started after its cinematic");
        RaidDirector.Current.Session.AddTime(900f);
        Assert.AreSame(raid, GameSession.ActiveRaid.Definition);
        for (int i = 0; i < 30; i++) yield return null;
        Cheats.KillNearby();
        yield return Wait(1f);

        // Out, through the results screen as the player goes, and home.
        where = "home from " + raid.name;
        Assert.AreEqual("Extracted.", Cheats.ExtractFromRaid());
        yield return Until(() => Object.FindAnyObjectByType<RaidResultsScreen>() != null && !SceneTransition.IsBusy, 60f, "no results screen");
        yield return Wait(1f);
        var screen = Object.FindAnyObjectByType<RaidResultsScreen>();
        var continueButton = (Button)typeof(RaidResultsScreen).GetField("continueButton", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(screen);
        continueButton.onClick.Invoke();
        yield return Until(() => AtHome(), 120f, "never home from the raid");
        yield return Wait(2f);

        Assert.IsNull(GameSession.ActiveRaid, "the raid is over");
        Assert.AreEqual(extractions + 1, GameSession.Home.SuccessfulExtractions, "the raid counts at home");
        Assert.AreEqual(slot, SaveGameService.CurrentSlot, "still the same journey");
        Assert.IsTrue(SaveGameService.TryReadSave(out SaveData saved, out problem), "the save: " + problem);
        Assert.AreEqual(extractions + 1, saved.settlement.successfulExtractions, "and the save made at home counts it");
        Assert.AreEqual(Haven, saved.scene, "saved at home");
        List<string> heard = problems.All;
        CollectionAssert.IsEmpty(heard, "nothing threw or logged an error on the way:\n" + string.Join("\n\n", heard));
        CollectionAssert.AreEquivalent(playersSaves, PlayersSaves(), "the player's own saves are untouched");
        CollectionAssert.IsEmpty(ChangedDataAssets(),
            "shared definitions and settings are read-only in play (a write to one is every user's, lives on in the Editor after Play, and " +
            "is never in a build's next run): keep what changes on the object that owns it (Docs/BugPatterns.md, ninth round)");
    }

    // Every ScriptableObject of the game's own (sub-assets too), serialized as it is now. TextMesh Pro's fonts are left out: a dynamic
    // font adds the glyphs it meets to its atlas, which is what it is for.
    private static Dictionary<ScriptableObject, string> DataAssets()
    {
        var all = new Dictionary<ScriptableObject, string>();
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/_Project" }))
        {
            foreach (Object found in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
            {
                if (found is ScriptableObject data && !(data is TMPro.TMP_Asset) && !all.ContainsKey(data))
                {
                    all[data] = EditorJsonUtility.ToJson(data);
                }
            }
        }

        return all;
    }

    private List<string> ChangedDataAssets()
    {
        var changed = new List<string>();
        foreach (KeyValuePair<ScriptableObject, string> before in dataBefore)
        {
            if (before.Key != null && EditorJsonUtility.ToJson(before.Key) != before.Value)
            {
                changed.Add($"{AssetDatabase.GetAssetPath(before.Key)} ({before.Key.GetType().Name} {before.Key.name})");
            }
        }

        return changed;
    }

    [UnityTearDown]
    public IEnumerator PutThingsBack()
    {
        problems?.Dispose();
        problems = null;
        if (Application.isPlaying)
        {
            Cheats.SetInvincible(false);
            yield return new ExitPlayMode();
        }

        // A data asset written in play keeps the write in the Editor: put back as it was, once Play is over.
        if (dataBefore != null)
        {
            foreach (KeyValuePair<ScriptableObject, string> before in dataBefore)
            {
                if (before.Key != null && EditorJsonUtility.ToJson(before.Key) != before.Value)
                {
                    EditorJsonUtility.FromJsonOverwrite(before.Value, before.Key);
                }
            }

            dataBefore = null;
        }

        SaveGameService.Storage = new SaveStorage();
        // The game it played stays in the session after Play: its stash with every chest's block shut (a new Haven has no chest), so
        // a test after it that gave to the stash found no room (CheatTests, in a whole-suite run). A fresh session for whoever is next.
        GameSession.StartNewGame();
        LogAssert.ignoreFailingMessages = false;
        if (folder != null && Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    private static bool AtHome() =>
        SceneManager.GetActiveScene().name == Haven && Player.Current != null && !SceneTransition.IsBusy && GameSession.ActiveRaid == null;

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

    // The real save folder's files and when each was written.
    private static Dictionary<string, DateTime> PlayersSaves()
    {
        string real = new SaveStorage().Directory;
        return Directory.Exists(real) ? Directory.GetFiles(real).ToDictionary(path => path, File.GetLastWriteTimeUtc) : new Dictionary<string, DateTime>();
    }
}
