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
using Object = UnityEngine.Object;

/// <summary>
/// The way out of a game in Play mode (Docs/BugPatterns.md, sixth round): a raid left from the pause menu's "Quit to main menu", then a
/// new game, then the save that quit wrote loaded. In the Editor every test starts the program's statics afresh, so a game left by the
/// menu never handed anything on to the next one there; a player does it in one sitting. What the game left kept into the next was the
/// bug: a quest clock, a save waiting for home that was then written into the next game and the builders' names. Each is checked here.
/// Fails on any error or exception on the way, those from the renderer's own threads too (LiveTestErrors). About three minutes.
/// </summary>
public sealed class QuitToMenuLiveTests
{
    private const string Haven = "SafeHaven";
    private const string MainMenu = "MainMenu";
    private const string RaidPath = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string AQuestPath = "Assets/_Project/Data/Quests";

    private LiveTestErrors problems;
    private string folder;
    private string where = "starting";

    [UnityTest, Timeout(900000)]
    public IEnumerator ARaidLeftByTheMenu_EndsAsAFall_AndTheNextGameStartsClean()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(RaidPath);
        Assert.IsNotNull(raid, RaidPath);
        QuestDefinition timed = AnyQuest();

        LogAssert.ignoreFailingMessages = true;
        yield return new EnterPlayMode();
        problems = new LiveTestErrors(() => where);
        folder = Path.Combine(Path.GetTempPath(), "SafeHavenQuitToMenu_" + Guid.NewGuid().ToString("N"));
        SaveGameService.Storage = new SaveStorage(folder);

        where = "a new game";
        Assert.IsTrue(SaveGameService.TryStartNewGame(Haven, out string problem), "new game: " + problem);
        yield return Until(() => In(Haven), 120f, "the Haven never loaded with a player");
        Assert.IsTrue(SaveGameService.TrySaveAsNew(out problem), "the first save: " + problem);
        string slot = SaveGameService.CurrentSlot;
        Cheats.SetInvincible(true);

        where = raid.name;
        Assert.IsTrue(Cheats.StartRaid(raid, out string launched), "the raid: " + launched);
        yield return Until(() => CinematicScreen.IsPlaying || Running(), 180f, "the raid never loaded");
        if (CinematicScreen.IsPlaying)
        {
            yield return Wait(1f);
            CinematicScreen.Stop();
        }

        yield return Until(Running, 90f, "the raid never started");
        RaidDirector.Current.Session.AddTime(900f);
        yield return Wait(2f);
        // A timed moment under way as the player leaves (the prologue's hold is one), and someone who built at home meanwhile.
        QuestTimers.Set(timed, 60f);
        var builders = (List<ResidentDefinition>)typeof(ResidentBuilders).GetField("worked", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        builders.Add(AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Settlement/Ingrid.asset"));

        where = raid.name + " (quit to the menu)";
        var pause = Object.FindAnyObjectByType<PauseMenu>(FindObjectsInactive.Include);
        Assert.IsNotNull(pause, "the raid has a pause menu");
        typeof(PauseMenu).GetMethod("QuitToMainMenu", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pause, null);
        yield return Until(() => SceneManager.GetActiveScene().name == MainMenu && !SceneTransition.IsBusy, 120f, "never reached the main menu");
        yield return Wait(1f);

        Assert.IsTrue(SaveGameService.TryReadSave(slot, out SaveData left, out problem), "the save the quit wrote: " + problem);
        Assert.IsTrue(string.IsNullOrEmpty(left.raidUnderWay), "the raid ended as a fall and was saved so: it is not a raid cut short");
        Assert.AreEqual(Haven, left.scene, "the quit leaves the journey at home, at the portal");
        Assert.IsTrue(SaveGameService.IsSaveWaitingForHome, "the raid's save for home waits (the menu has no player to save)");

        // A new game from the menu: nothing of the game left comes with it.
        where = "a new game after the quit";
        string[] savesBefore = Directory.GetFiles(folder);
        Assert.IsTrue(SaveGameService.TryStartNewGame(Haven, out problem), "the next new game: " + problem);
        Assert.IsFalse(SaveGameService.IsSaveWaitingForHome, "the left game's save for home is forgotten with it");
        yield return Until(() => In(Haven), 120f, "the next new game never loaded");
        yield return Wait(3f);
        Assert.IsFalse(QuestTimers.TryGet(timed, out _), "the left game's quest clock is forgotten");
        CollectionAssert.IsEmpty(builders, "the left game's builders aren't named in the next game's message home");
        CollectionAssert.AreEquivalent(savesBefore, Directory.GetFiles(folder),
            "nothing of the left game was saved in the new one (its save for home waited through the menu and wrote the new game)");

        // And the left journey loads, home at the portal.
        where = "loading the journey the quit saved";
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        Assert.IsTrue(SaveGameService.TryLoad(slot, content, Haven, out problem), "load: " + problem);
        yield return Until(() => In(Haven), 120f, "the saved journey never loaded");
        yield return Wait(2f);
        Assert.IsNull(GameSession.ActiveRaid, "no raid under way after loading");
        Assert.AreEqual(slot, SaveGameService.CurrentSlot, "the loaded journey goes on in its own save");

        List<string> heard = problems.All;
        CollectionAssert.IsEmpty(heard, "errors and exceptions on the way:\n" + string.Join("\n\n", heard));
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

        SaveGameService.Storage = new SaveStorage();
        GameSession.StartNewGame();
        LogAssert.ignoreFailingMessages = false;
        if (folder != null && Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
    }

    private static QuestDefinition AnyQuest()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition", new[] { AQuestPath }))
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (quest != null)
            {
                return quest;
            }
        }

        Assert.Fail("no quest to time");
        return null;
    }

    private static bool In(string scene) =>
        SceneManager.GetActiveScene().name == scene && Player.Current != null && !SceneTransition.IsBusy && GameSession.ActiveRaid == null;

    private static bool Running() =>
        RaidDirector.Current != null && RaidDirector.Current.IsRunning && Player.Current != null && !SceneTransition.IsBusy && !CinematicScreen.IsPlaying;

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
