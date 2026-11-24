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

    // Every Mechanics page a skill opens, in a fresh game each: which skill opens which page is the journal's data.
    [Test]
    public void TryingASkill_OpensItsTutorial_Once()
    {
        // Pages the skill alone opens: one that also waits for a quest, a creature met, someone home, a technique tried (Guard and Parry:
        // the parry) or a find in a raid opens when those are done too (JournalContent.IsUnlocked), which a fresh game hasn't.
        var pages = Settings.Mechanics.Where(p => p.unlockedBySkill != null && p.unlockedByQuest == null && p.unlockedByCreature == null
            && p.unlockedByResident == null && string.IsNullOrEmpty(p.unlockedByTechnique) && !p.foundInRaids).ToList();
        Assert.IsNotEmpty(pages, "pages that open when a skill is first tried");
        foreach (JournalPage page in pages)
        {
            GameSession.StartNewGame();
            ProficiencyDefinition skill = page.unlockedBySkill;
            Assert.IsFalse(JournalContent.IsUnlocked(page, GameSession.Quests, GameSession.Progress), $"{page.title}: hidden before {skill.name} is tried.");

            JournalNotices.Tried(skill);
            Assert.IsTrue(JournalContent.IsUnlocked(page, GameSession.Quests, GameSession.Progress), page.title);
            CollectionAssert.Contains(Tutorials.Pending, page, $"{page.title} waits as a tutorial.");
            int waiting = Tutorials.Pending.Count;

            JournalNotices.Tried(skill);
            Assert.AreEqual(waiting, Tutorials.Pending.Count, $"{page.title}: trying {skill.name} again opens nothing new.");
        }
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
    public void EveryStep_NamesOneKeyThatExists_AndNoPageWritesAKeyOut()
    {
        // A key written out ("Hold Left Ctrl") never followed a rebinding or a controller; a step's key is what the card listens for.
        var writtenOut = new System.Text.RegularExpressions.Regex(@"\b(Left|Right) (Ctrl|Shift|Alt)\b|\b(left|right|middle) mouse button\b|^Space\b",
            System.Text.RegularExpressions.RegexOptions.Multiline);
        int checkedSteps = 0;
        foreach (JournalPage page in Settings.Mechanics)
        {
            Assert.IsFalse(writtenOut.IsMatch(page.body), $"{page.title} writes a key out: use {{key:Action}}");
            foreach (string step in page.steps)
            {
                string token = TutorialLesson.TokenOf(step);
                Assert.IsNotNull(token, $"{page.title}: \"{step}\" has no key");
                Assert.IsNotNull(GameInput.ActionForToken(token), $"{page.title}: {{key:{token}}} names no action");
                Assert.IsNull(GameInput.UnknownKey(step), page.title);
                checkedSteps++;
            }
        }

        Assert.Greater(checkedSteps, 30);
    }

    [Test]
    public void ALesson_IsDoneInOrder_ByItsKeys()
    {
        var page = new JournalPage { title = "A test's page" };
        page.steps.Add("[{key:Guard}] Hold to guard");
        page.steps.Add("[{key:Attack}] Then strike");
        var lesson = new TutorialLesson(page);
        Assert.IsFalse(lesson.Press("Attack"), "the second step's key does nothing yet");
        Assert.IsTrue(lesson.Press("guard"), "a token is matched whatever its case");
        Assert.IsFalse(lesson.Finished);
        Assert.IsTrue(lesson.Press("Attack"));
        Assert.IsTrue(lesson.Finished);
        Assert.IsNull(lesson.CurrentToken);
        Assert.IsFalse(lesson.Press("Attack"), "done is done");
    }

    [Test]
    public void ATutorialWithSteps_IsLearnedOnTheCard_AndThenIsRead()
    {
        TutorialNoticeHud notice = TutorialNoticeHud.Create();
        try
        {
            JournalNotices.Tried(LevelingSettings.Current.FindSkill("woodcutting"));
            Assert.IsNotEmpty(Tutorials.Pending);
            JournalPage page = Tutorials.Pending[Tutorials.Pending.Count - 1];
            Assert.IsTrue(TutorialLesson.HasSteps(page), "woodcutting's page teaches on the card");
            notice.Refresh(); // in Play it hears Tutorials.Changed; an edit-mode test doesn't run OnEnable
            Assert.IsNotNull(notice.Lesson, "the card teaches the page that opened");
            Assert.AreEqual(page, notice.Lesson.Page);

            notice.Pressed("Journal"); // a key the lesson doesn't ask for
            Assert.AreEqual(0, notice.Lesson.Done);
            for (int i = 0; i < page.steps.Count; i++)
            {
                notice.Pressed(TutorialLesson.TokenOf(page.steps[i]));
            }

            Assert.IsTrue(notice.Lesson.Finished);
            Assert.IsTrue(GameSession.Progress.Journal.HasRead(Tutorials.ReadKeyOf(page)), "learned is read");
            CollectionAssert.DoesNotContain(Tutorials.Pending, page);
        }
        finally
        {
            Object.DestroyImmediate(notice.gameObject);
        }
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
