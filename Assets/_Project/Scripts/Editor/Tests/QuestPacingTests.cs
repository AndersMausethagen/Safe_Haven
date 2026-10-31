using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>When story beats may come (StoryPacing), quests waiting their turn, and the quest designer seeing stories.</summary>
public sealed class QuestPacingTests
{
    private readonly List<Object> created = new List<Object>();
    private StoryPacingSettings rules;
    private GameObject host;
    private QuestManager manager;

    [SetUp]
    public void SetUp()
    {
        rules = Make<StoryPacingSettings>();
    }

    [TearDown]
    public void TearDown()
    {
        if (manager != null)
        {
            // Edit Mode doesn't call OnDestroy, so the test unsubscribes the manager itself.
            typeof(QuestManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
            Object.DestroyImmediate(host);
            manager = null;
            GameSession.StartNewGame();
        }

        foreach (Object value in created)
        {
            Object.DestroyImmediate(value);
        }

        created.Clear();
    }

    private T Make<T>() where T : ScriptableObject
    {
        var value = ScriptableObject.CreateInstance<T>();
        created.Add(value);
        return value;
    }

    private QuestDefinition Quest(string id, QuestStart start, float delay = 0f, GameEventDefinition startEvent = null, params QuestDefinition[] after)
    {
        var quest = Make<QuestDefinition>();
        quest.name = id;
        var data = new SerializedObject(quest);
        data.FindProperty("start").enumValueIndex = (int)start;
        data.FindProperty("startDelay").floatValue = delay;
        data.FindProperty("startEvent").objectReferenceValue = startEvent;
        SerializedProperty prerequisites = data.FindProperty("prerequisites");
        prerequisites.arraySize = after.Length;
        for (int i = 0; i < after.Length; i++)
        {
            prerequisites.GetArrayElementAtIndex(i).objectReferenceValue = after[i];
        }

        SerializedProperty objectives = data.FindProperty("objectives");
        objectives.arraySize = 1;
        objectives.GetArrayElementAtIndex(0).FindPropertyRelative("type").enumValueIndex = (int)ObjectiveType.QuestEvent;
        objectives.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 1;
        data.ApplyModifiedPropertiesWithoutUndo();
        return quest;
    }

    private void Run(params QuestDefinition[] quests)
    {
        var database = Make<ContentDatabase>();
        var data = new SerializedObject(database);
        SerializedProperty list = data.FindProperty("quests");
        list.arraySize = quests.Length;
        for (int i = 0; i < quests.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        GameSession.StartNewGame();
        host = new GameObject("Quest Manager");
        manager = host.AddComponent<QuestManager>();
        manager.Initialise(database);
    }

    [Test]
    public void AQuest_WaitsForTheGapAfterTheLastOne()
    {
        var pacing = new StoryPacing();
        pacing.QuestCompleted(10f);
        float ready = pacing.ReadyAt(0f, 10f, rules);
        Assert.AreEqual(10f + rules.BetweenQuestsSeconds, ready, 1e-4f);
        Assert.IsFalse(pacing.CanStart(ready, 11f, rules));
        Assert.IsTrue(pacing.CanStart(ready, ready, rules));
    }

    [Test]
    public void AQuest_NotFollowingAnother_NeedNotWait()
    {
        var pacing = new StoryPacing();
        float ready = pacing.ReadyAt(0f, 100f, rules);
        Assert.AreEqual(100f, ready, "walking into a zone can start one on the spot");
        Assert.IsTrue(pacing.CanStart(ready, 100f, rules));
    }

    [Test]
    public void AQuestsOwnDelay_ReplacesTheGap()
    {
        var pacing = new StoryPacing();
        pacing.QuestCompleted(10f);
        Assert.AreEqual(18f, pacing.ReadyAt(8f, 10f, rules), 1e-4f);
    }

    [Test]
    public void AFight_HoldsQuests_AndLeavesAPauseAfterIt()
    {
        var pacing = new StoryPacing();
        pacing.Tick(0f, true, false);
        Assert.IsFalse(pacing.CanStart(0f, 1f, rules), "not in a fight");
        Assert.IsFalse(pacing.IsCatchingBreath(1f, rules), "a line said during a fight stays a subtitle");

        pacing.Tick(20f, false, false);
        Assert.IsTrue(pacing.IsCatchingBreath(20f, rules), "the last blow has just landed");
        Assert.IsFalse(pacing.CanStart(0f, 21f, rules));
        Assert.IsFalse(pacing.IsCatchingBreath(20f + rules.AfterFightSeconds, rules));
        Assert.IsTrue(pacing.CanStart(0f, 20f + rules.AfterFightSeconds, rules));
    }

    [Test]
    public void SomeoneTalking_HoldsQuests_ForABitLonger()
    {
        var pacing = new StoryPacing();
        pacing.Tick(0f, false, true);
        Assert.IsFalse(pacing.CanStart(0f, 1f, rules));
        pacing.Tick(5f, false, false);
        Assert.IsFalse(pacing.CanStart(0f, 5f, rules), "right on the end of the line");
        Assert.IsTrue(pacing.CanStart(0f, 5f + rules.AfterDialogueSeconds, rules));
    }

    [Test]
    public void AnEndlessFight_CannotStopTheStory()
    {
        var pacing = new StoryPacing();
        pacing.Tick(0f, true, false);
        Assert.IsFalse(pacing.CanStart(10f, 10f + rules.LongestHoldSeconds - 1f, rules));
        Assert.IsTrue(pacing.CanStart(10f, 10f + rules.LongestHoldSeconds, rules));
    }

    [Test]
    public void TheNextQuestOfAChain_WaitsThenStarts()
    {
        var first = Quest("first", QuestStart.ByQuestGiver);
        var second = Quest("second", QuestStart.Automatically, after: first);
        Run(first, second);
        manager.Log.ForceComplete(first);
        Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(second), "not on the heels of the first");
        Assert.IsTrue(manager.IsWaiting(second));

        manager.StartWaitingQuests(Time.time + StoryPacingSettings.Current.BetweenQuestsSeconds * 0.5f);
        Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(second));

        manager.StartWaitingQuests(Time.time + StoryPacingSettings.Current.BetweenQuestsSeconds);
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(second));
        Assert.IsFalse(manager.IsWaiting(second));
    }

    [Test]
    public void AGiverStartsAQuest_AtOnce_WhateverIsWaiting()
    {
        var first = Quest("first", QuestStart.ByQuestGiver);
        var second = Quest("second", QuestStart.ByQuestGiver, after: first);
        Run(first, second);
        manager.Log.ForceComplete(first);
        Assert.IsTrue(manager.TryStartQuest(second, out _), "the player asked");
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(second));
    }

    [Test]
    public void AGameSavedWhileAQuestWaited_FindsItAgainOnArriving()
    {
        var raised = Make<GameEventDefinition>();
        raised.name = "home again";
        var quest = Quest("search", QuestStart.OnQuestEvent, startEvent: raised);
        Run(quest);
        manager.Log.RestoreHappened(raised.Id);
        Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(quest), "as loaded: the event is saved, the quest isn't");

        manager.StartQuestsWhoseEventHappened();
        manager.StartWaitingQuests(Time.time + 1000f);
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(quest), "a homecoming quest must not be lost to a save in the gap");
    }

    [Test]
    public void ADelayOnAQuestThatSomeoneStarts_IsCalledOut()
    {
        Assert.IsTrue(Quest("given", QuestStart.ByQuestGiver, 5f).FindProblems().Any(p => p.Contains("Start Delay")));
        Assert.IsFalse(Quest("timed", QuestStart.Automatically, 5f).FindProblems().Any(p => p.Contains("Start Delay")));
    }

    [Test]
    public void TheWolves_LeaveRoomBeforeTheOldTrollComes()
    {
        var something = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Prologue/Quests/ReachPortal.asset");
        Assert.AreEqual(QuestStart.Automatically, something.Start);
        Assert.GreaterOrEqual(something.StartDelay, StoryPacingSettings.Current.AfterFightSeconds);
    }

    [Test]
    public void TheSettingsAsset_Exists()
    {
        Assert.IsNotNull(Resources.Load<StoryPacingSettings>(StoryPacingSettings.ResourceName));
    }

    [Test]
    public void SaidLines_WaitOutTheFightUnlessToldNot()
    {
        Assert.IsTrue(new SayNode().afterFightPause);
        Assert.IsTrue(new ChoiceNode().afterFightPause);
    }

    [Test]
    public void TheIndex_TellsWhatEachStoryNodeDoes()
    {
        const string quest = "11111111111111111111111111111111";
        const string raised = "22222222222222222222222222222222";
        const string heard = "33333333333333333333333333333333";
        string script = AssetDatabase.AssetPathToGUID("Assets/_Project/Scripts/Story/StoryScript.cs");
        string scene = "%YAML 1.1\n--- !u!1 &100\nGameObject:\n  m_Name: The Waking Stone\n" +
            "--- !u!114 &200\nMonoBehaviour:\n  m_GameObject: {fileID: 100}\n" +
            $"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n" +
            "  references:\n    version: 2\n    RefIds:\n" +
            "    - rid: 1\n      type: {class: StartQuestNode, ns: , asm: SafeHaven}\n      data:\n" +
            $"        quest: {{fileID: 11400000, guid: {quest}, type: 2}}\n" +
            "    - rid: 2\n      type: {class: RaiseQuestEventNode, ns: , asm: SafeHaven}\n      data:\n" +
            $"        questEvent: {{fileID: 11400000, guid: {raised}, type: 2}}\n" +
            "    - rid: 3\n      type: {class: WhenQuestEvent, ns: , asm: SafeHaven}\n      data:\n" +
            $"        questEvent: {{fileID: 11400000, guid: {heard}, type: 2}}\n";
        var scripts = new Dictionary<string, System.Type> { [script] = typeof(StoryScript) };
        List<QuestSceneUse> uses = QuestSceneIndex.Parse(scene, "Assets/Test.unity", scripts, new HashSet<string> { quest, raised, heard });

        Assert.AreEqual(QuestSceneRole.Starts, uses.Single(u => u.TargetGuid == quest).Role);
        Assert.AreEqual(QuestSceneRole.Raises, uses.Single(u => u.TargetGuid == raised).Role);
        Assert.AreEqual(QuestSceneRole.Listens, uses.Single(u => u.TargetGuid == heard).Role, "waiting for an event doesn't raise it");
        Assert.AreEqual("The Waking Stone", uses[0].ObjectName);
    }

    [Test]
    public void AQuestAStoryStarts_NeedsNoGiver()
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/StoryDemo/TheWakingStone.asset");
        QuestSceneIndex.Invalidate();
        Assert.IsTrue(QuestSceneIndex.IsStartedByStory(quest));
        Assert.IsFalse(QuestSection.FindSceneProblems(quest, new[] { quest }).Any(p => p.Contains("Quest Giver")));
    }
}
