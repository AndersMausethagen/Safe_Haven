using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The tutorials: pages of the journal's Mechanics tab that open the first time a thing is tried, wait behind a notice and show with their
/// painting (Tutorials, JournalNotices.Tried, TutorialWindow, TutorialNoticeHud).
/// </summary>
public sealed class TutorialTests
{
    private static JournalSettings Settings => AssetDatabase.LoadAssetAtPath<JournalSettings>("Assets/_Project/Resources/JournalSettings.asset");

    [SetUp]
    public void FreshGame()
    {
        GameSession.StartNewGame();
    }

    [TearDown]
    public void Forget()
    {
        GameSession.StartNewGame();
    }

    [Test]
    public void EveryMechanicsPage_SaysWhatItIs_AndHowItWorks()
    {
        Assert.Greater(Settings.Mechanics.Count, 20);
        foreach (JournalPage page in Settings.Mechanics)
        {
            Assert.IsNotEmpty(page.summary, page.title);
            Assert.IsNotEmpty(page.body, page.title);
        }
    }

    [Test]
    public void EveryMechanicsPage_HasItsPainting()
    {
        foreach (JournalPage page in Settings.Mechanics)
        {
            Assert.IsNotNull(page.art, $"{page.title} has no picture: paint it (Tools/LocalSpawn/generate_ui_art.py tutorials) and run BuildJournal.Apply.");
        }
    }

    [TestCase("woodcutting", "Woodcutting")]
    [TestCase("mining", "Mining")]
    [TestCase("foraging", "Foraging")]
    [TestCase("fishing", "Fishing")]
    [TestCase("archery", "Archery")]
    [TestCase("sneaking", "Sneaking")]
    [TestCase("melee", "Swings and Chains")]
    [TestCase("runecraft", "Abilities")]
    [TestCase("construction", "Building")]
    public void TryingASkill_OpensItsTutorial_Once(string skillId, string title)
    {
        ProficiencyDefinition skill = LevelingSettings.Current.FindSkill(skillId);
        Assert.IsNotNull(skill, skillId);
        JournalPage page = Settings.Mechanics.First(p => p.title == title);
        Assert.AreEqual(skill, page.unlockedBySkill, $"{title} opens when {skillId} is first tried.");
        Assert.IsFalse(JournalContent.IsUnlocked(page, GameSession.Quests, GameSession.Progress), "Hidden before it is tried.");

        JournalNotices.Tried(skill);
        Assert.IsTrue(JournalContent.IsUnlocked(page, GameSession.Quests, GameSession.Progress));
        CollectionAssert.Contains(Tutorials.Pending, page, "It waits as a tutorial.");
        int waiting = Tutorials.Pending.Count;

        JournalNotices.Tried(skill);
        Assert.AreEqual(waiting, Tutorials.Pending.Count, "Trying it again opens nothing new.");
    }

    [Test]
    public void ASeenTutorial_NoLongerWaits_AndIsReadInTheJournal()
    {
        JournalNotices.Tried(LevelingSettings.Current.FindSkill("fishing"));
        JournalPage page = Tutorials.Pending.First(p => p.title == "Fishing");
        Tutorials.MarkSeen(page);
        CollectionAssert.DoesNotContain(Tutorials.Pending, page);
        Assert.IsTrue(GameSession.Progress.Journal.HasRead(Tutorials.ReadKeyOf(page)));
        Assert.IsNotNull(Tutorials.Latest, "The tutorial key can still show one again.");
    }

    [Test]
    public void ANewGame_ForgetsWhatWaited()
    {
        JournalNotices.Tried(LevelingSettings.Current.FindSkill("mining"));
        Assert.IsNotEmpty(Tutorials.Pending);
        GameSession.StartNewGame();
        Assert.IsEmpty(Tutorials.Pending);
    }

    [Test]
    public void WhatWasTried_SurvivesASave()
    {
        var progress = new CharacterProgress();
        progress.Journal.NoteTried("fishing");
        var saved = new ProgressSaveData();
        const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Static;
        typeof(SaveMapper).GetMethod("WriteProgress", Hidden).Invoke(null, new object[] { progress, saved });
        ProgressSaveData read = JsonUtility.FromJson<ProgressSaveData>(JsonUtility.ToJson(saved));
        var loaded = (CharacterProgress)typeof(SaveMapper).GetMethod("ReadProgress", Hidden).Invoke(null, new object[] { read });
        Assert.IsTrue(loaded.Journal.HasTried("fishing"));
        Assert.IsFalse(loaded.Journal.NoteTried("fishing"), "Already tried.");
    }

    [Test]
    public void AnOldSave_WithExperience_HasItsPagesOpen()
    {
        JournalPage page = Settings.Mechanics.First(p => p.title == "Mining");
        var progress = new CharacterProgress();
        Assert.IsFalse(JournalContent.IsUnlocked(page, new QuestLog(), progress));
        progress.AddExperience(page.unlockedBySkill, 5, LevelingSettings.Current);
        Assert.IsTrue(JournalContent.IsUnlocked(page, new QuestLog(), progress), "A skill trained before tutorials existed counts as tried.");
    }

    [Test]
    public void TheTutorialHasAKey_ThatCanBeRebound()
    {
        Assert.IsNotNull(GameInput.Tutorial, "The action is in the Input Actions asset.");
        Assert.IsTrue(GameInput.Tutorial.bindings.Any(b => b.path == "<Keyboard>/t"));
        Assert.IsTrue(InputRebinds.Rows.Any(r => r.ActionPath == "Player/Tutorial"), "It is listed under Options, Controls.");
    }

    [Test]
    public void TheWindowAndNotice_ShowWhatWaits()
    {
        TutorialWindow window = TutorialWindow.Create();
        TutorialNoticeHud notice = TutorialNoticeHud.Create();
        try
        {
            JournalNotices.Tried(LevelingSettings.Current.FindSkill("woodcutting"));
            Assert.IsNotEmpty(Tutorials.Pending);
            Assert.IsNotNull(notice);
            JournalPage first = Tutorials.Pending[0];
            TutorialWindow.ShowPending();
            Assert.AreEqual(first, window.Shown, "The window shows the first that waits.");
            Assert.IsTrue(GameSession.Progress.Journal.HasRead(Tutorials.ReadKeyOf(first)), "Shown is read.");
            CollectionAssert.DoesNotContain(Tutorials.Pending, first);
        }
        finally
        {
            Object.DestroyImmediate(window.gameObject);
            Object.DestroyImmediate(notice.gameObject);
        }
    }
}
