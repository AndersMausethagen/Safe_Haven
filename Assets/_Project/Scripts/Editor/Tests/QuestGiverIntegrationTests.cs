using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Standard giver selection, confirmation and lifecycle ownership, including the saved story scene.</summary>
public sealed class QuestGiverIntegrationTests
{
    const string ScenePath = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
    readonly List<Object> created = new List<Object>();
    QuestManager manager;

    static void Edit(Object target, Action<SerializedObject> edit)
    {
        using (var data = new SerializedObject(target))
        {
            edit(data);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    T Create<T>() where T : ScriptableObject
    {
        var result = ScriptableObject.CreateInstance<T>();
        created.Add(result);
        return result;
    }

    QuestDefinition Quest(QuestStart start = QuestStart.ByQuestGiver, QuestDefinition prerequisite = null)
    {
        var quest = Create<QuestDefinition>();
        Edit(quest, data => {
            data.FindProperty("start").enumValueIndex = (int)start;
            data.FindProperty("objectives").arraySize = 1;
            if (prerequisite != null)
            {
                var prerequisites = data.FindProperty("prerequisites");
                prerequisites.arraySize = 1;
                prerequisites.GetArrayElementAtIndex(0).objectReferenceValue = prerequisite;
            }
        });
        return quest;
    }

    QuestManager Manager(params QuestDefinition[] quests)
    {
        GameSession.StartNewGame();
        var database = Create<ContentDatabase>();
        Edit(database, data => {
            var list = data.FindProperty("quests");
            list.arraySize = quests.Length;
            for (int i = 0; i < quests.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = quests[i];
        });
        var host = new GameObject("Quest giver test manager");
        created.Add(host);
        manager = host.AddComponent<QuestManager>();
        manager.Initialise(database);
        _ = manager.Log;
        return manager;
    }

    QuestGiver Giver(params QuestDefinition[] quests) => Giver(true, quests);

    QuestGiver Giver(bool talks, params QuestDefinition[] quests)
    {
        var host = new GameObject("Standard giver");
        created.Add(host);
        if (talks) host.AddComponent<Talkable>();
        var giver = host.AddComponent<QuestGiver>();
        Edit(giver, data => {
            data.FindProperty("showMarker").boolValue = false;
            var offers = data.FindProperty("offers");
            offers.arraySize = quests.Length;
            for (int i = 0; i < quests.Length; i++)
            {
                var offer = offers.GetArrayElementAtIndex(i);
                offer.FindPropertyRelative("quest").objectReferenceValue = quests[i];
                offer.FindPropertyRelative("text").stringValue = "Authored briefing " + i;
                offer.FindPropertyRelative("acceptText").stringValue = "Accept " + i;
            }
        });
        // Edit Mode doesn't dispatch normal MonoBehaviour lifecycle callbacks.
        typeof(QuestGiver).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(giver, null);
        return giver;
    }

    [TearDown]
    public void Cleanup()
    {
        if (manager != null)
            typeof(QuestManager).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(manager, null);
        foreach (var giver in created.OfType<GameObject>().SelectMany(g => g.GetComponents<QuestGiver>()))
            typeof(QuestGiver).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(giver, null);
        for (int i = created.Count - 1; i >= 0; i--) Object.DestroyImmediate(created[i]);
        created.Clear();
        GameSession.StartNewGame();
    }

    [Test]
    public void Confirmation_DoesNotStartUntilChosen_AndStaleChoiceCannotAcceptNextOffer()
    {
        var first = Quest();
        var next = Quest(prerequisite: first);
        Manager(first, next);
        var giver = Giver(first, next);
        var page = giver.BuildDialoguePage();
        Assert.AreEqual("Authored briefing 0", page.Body);
        Assert.AreEqual("Accept 0", page.Actions.Single().Label);
        Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(first));
        page.Actions.Single().Chosen();
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(first));
        Assert.IsEmpty(giver.BuildDialoguePage().Actions);
        manager.Log.ForceComplete(first);
        page.Actions.Single().Chosen();
        Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(next), "A stale choice must not start a new offer.");
        giver.BuildDialoguePage().Actions.Single().Chosen();
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(next));
    }

    [Test]
    public void Talkable_GathersTheGiversLineAndAnswers_AndFollowsUpWithItsOwnLine()
    {
        var quest = Quest();
        Manager(quest);
        var giver = Giver(quest);
        var talkable = giver.GetComponent<Talkable>();
        var page = talkable.BuildPage();
        Assert.AreEqual("Authored briefing 0", page.Body);
        Assert.AreEqual("Accept 0", page.Actions.Single().Label);
        page = talkable.BuildPage("Anything else?");
        Assert.AreEqual("Anything else?", page.Body, "After an answer the giver doesn't repeat the briefing.");
        Assert.AreEqual(1, page.Actions.Count);
        Assert.IsTrue(giver.CanInteract(null, out _), "The conversation stays open; the giver only adds to it.");
        Assert.AreEqual(talkable.SpeakerName, ((IInteractionName)talkable).InteractionName);
    }

    [Test]
    public void OrderedOffers_DoNotSkipLockedEarlierOffers_OrSkipRequiredTurnIn()
    {
        var prerequisite = Quest();
        var locked = Quest(prerequisite: prerequisite);
        var available = Quest();
        Edit(locked, d => d.FindProperty("turnInToGiver").boolValue = true);
        Manager(prerequisite, locked, available);
        var giver = Giver(locked, available);
        Assert.IsEmpty(giver.BuildDialoguePage().Actions);
        Assert.AreSame(locked, giver.Quest);
        manager.Log.ForceComplete(prerequisite);
        giver.BuildDialoguePage().Actions.Single().Chosen();
        manager.Log.AddProgress(locked, 0, 1000);
        Assert.AreEqual(QuestStatus.ReadyToTurnIn, manager.Log.StatusOf(locked));
        Assert.AreSame(locked, giver.Quest);
        giver.BuildDialoguePage().Actions.Single().Chosen();
        Assert.AreEqual(QuestStatus.Completed, manager.Log.StatusOf(locked));
        Assert.AreSame(available, giver.Quest);
    }

    [Test]
    public void ConnectedOffers_KeepTheReturnObjectiveUntilTheNextBriefingIsAccepted()
    {
        var first = Quest();
        var next = Quest(prerequisite: first);
        Edit(first, d => d.FindProperty("turnInToGiver").boolValue = true);
        Manager(first, next);
        var giver = Giver(first, next);
        giver.BuildDialoguePage().Actions.Single().Chosen();
        manager.Log.AddProgress(first, 0, 1000);
        var page = giver.BuildDialoguePage();
        Assert.AreEqual("Authored briefing 1", page.Body);
        Assert.AreEqual("Accept 1", page.Actions.Single().Label);
        Assert.AreEqual(QuestStatus.ReadyToTurnIn, manager.Log.StatusOf(first), "Reading or leaving must not drop the current quest.");
        Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(next));
        page.Actions.Single().Chosen();
        Assert.AreEqual(QuestStatus.Completed, manager.Log.StatusOf(first));
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(next));
        page.Actions.Single().Chosen();
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(next), "A stale handoff is harmless.");
    }

    [Test]
    public void Tracker_ShowsAvailableInstructions_AndRefreshesWhenATimerIsRegisteredAfterQuestStart()
    {
        var first = Quest();
        var next = Quest(prerequisite: first);
        Edit(next, d => {
            d.FindProperty("availableObjective").stringValue = "Speak to the smith about the next step.";
            d.FindProperty("start").enumValueIndex = (int)QuestStart.Automatically;
            d.FindProperty("startDelay").floatValue = 60f;
        });
        Manager(first, next);
        var root = new GameObject("Tracker test", typeof(RectTransform));
        root.SetActive(false);
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        var label = panel.AddComponent<TMPro.TextMeshProUGUI>();
        var tracker = root.AddComponent<QuestTrackerView>();
        Edit(tracker, d => {
            d.FindProperty("label").objectReferenceValue = label;
            d.FindProperty("panel").objectReferenceValue = panel;
        });
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        try
        {
            typeof(QuestTrackerView).GetMethod("OnEnable", flags).Invoke(tracker, null);
            Assert.IsFalse(panel.activeSelf);
            manager.Log.ForceComplete(first);
            StringAssert.Contains("Speak to the smith", label.text);
            Assert.IsTrue(panel.activeSelf);
            Assert.IsTrue(manager.IsWaiting(next));
            Assert.IsTrue((bool)typeof(QuestTrackerView).GetField("timed", flags).GetValue(tracker),
                "Queueing a paced quest must notify the tracker so conversation guidance keeps refreshing.");
            manager.Log.TryStart(next);
            StringAssert.DoesNotContain("Speak to the smith", label.text);
            QuestTimers.Set(next, 35f);
            StringAssert.Contains("0:35", label.text, "The timer is set after the quest's Changed event.");
            QuestTimers.Clear(next);
            StringAssert.DoesNotContain("0:35", label.text);
        }
        finally
        {
            typeof(QuestTrackerView).GetMethod("OnDisable", flags).Invoke(tracker, null);
            QuestTimers.Clear(next);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void LegacySingleGiver_StillStartsImmediately_AndTurnsInNormally()
    {
        var quest = Quest();
        Edit(quest, d => d.FindProperty("turnInToGiver").boolValue = true);
        Manager(quest);
        var giver = Giver(false);
        Edit(giver, data => data.FindProperty("quest").objectReferenceValue = quest);
        giver.BuildDialoguePage(); // refresh after test authoring
        giver.Interact();
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(quest));
        manager.Log.AddProgress(quest, 0, 1000);
        giver.Interact();
        Assert.AreEqual(QuestStatus.Completed, manager.Log.StatusOf(quest));
    }

    [Test]
    public void LifecycleEvents_AreRaisedOnceByManager_NotByLogOrGiver()
    {
        var quest = Quest();
        var started = Create<GameEventDefinition>();
        var finished = Create<GameEventDefinition>();
        Edit(quest, data => {
            data.FindProperty("announceStart").boolValue = false;
            data.FindProperty("announceCompletion").boolValue = false;
            data.FindProperty("onStarted").arraySize = 1;
            data.FindProperty("onStarted").GetArrayElementAtIndex(0).objectReferenceValue = started;
            data.FindProperty("onCompleted").arraySize = 1;
            data.FindProperty("onCompleted").GetArrayElementAtIndex(0).objectReferenceValue = finished;
        });
        var independent = new QuestLog();
        independent.TryStart(quest);
        independent.ForceComplete(quest);
        Assert.IsFalse(independent.HasHappened(started));
        Assert.IsFalse(independent.HasHappened(finished));
        Manager(quest);
        var giver = Giver(quest);
        var raised = new List<GameEvent>();
        GameEvents.Raised += raised.Add;
        try
        {
            var choose = giver.BuildDialoguePage().Actions.Single().Chosen;
            choose();
            choose();
            manager.Log.ForceComplete(quest);
            manager.Log.ForceComplete(quest);
            Assert.AreEqual(1, raised.Count(e => e.Kind == GameEventKind.QuestStarted && e.Subject == quest));
            Assert.AreEqual(1, raised.Count(e => e.Kind == GameEventKind.Custom && e.Subject == started));
            Assert.AreEqual(1, raised.Count(e => e.Kind == GameEventKind.Custom && e.Subject == finished));
            Assert.IsTrue(manager.Log.HasHappened(started));
            Assert.IsTrue(manager.Log.HasHappened(finished));
        }
        finally { GameEvents.Raised -= raised.Add; }
    }

    [Test]
    public void SavedLeader_HasRealStandardOffers_PreservesText_AndRunsEmergencyChaptersAutomatically()
    {
        var scene = EditorSceneManager.OpenPreviewScene(ScenePath);
        try
        {
            var director = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrologueDirector>(true)).Single();
            var definition = director.Definition;
            var leader = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrologueCompanion>(true))
                .Single(c => c.Resident.DisplayName == "Sigrid");
            var giver = leader.GetComponent<QuestGiver>();
            Assert.NotNull(giver, "Saved scene, not an indirect director binding, must contain the giver.");
            var briefings = definition.Chapters.Where(c => c.Quest.Start == QuestStart.ByQuestGiver).ToArray();
            CollectionAssert.AreEqual(definition.Chapters.Where(c => c.Quest.Start == QuestStart.ByQuestGiver || c.Quest.TurnInToGiver)
                .Select(c => c.Quest), giver.Offers.Select(o => o.Quest));
            var acceptedOffers = giver.Offers.Where(o => o.Quest.Start == QuestStart.ByQuestGiver).ToArray();
            CollectionAssert.AreEqual(briefings.Select(c => c.ArrivalText), acceptedOffers.Select(o => o.Text));
            CollectionAssert.AreEqual(new[] { "Tell me.", "I'm ready. Let's go.", "I will find us shelter.", "I will gather the supplies.",
                "I will build the campfire.", "Let us catch our breath.", "I will try the waystones." },
                acceptedOffers.Select(o => o.AcceptText));
            var talkable = leader.GetComponent<Talkable>();
            Assert.NotNull(talkable, "The leader's offers are taken in a conversation.");
            Assert.AreEqual(definition.DialogueCameraDistance, new SerializedObject(talkable).FindProperty("cameraDistance").floatValue);
            var components = leader.GetComponents<IConversationTopics>().ToList();
            Assert.Less(components.IndexOf(giver), components.IndexOf(leader), "The quest offer speaks before the camp chatter.");
            Assert.IsTrue(giver.Offers.All(o => !string.IsNullOrWhiteSpace(o.WaitingText)));
            string before = EditorJsonUtility.ToJson(definition);
            Manager(definition.Chapters.Select(c => c.Quest).ToArray());
            foreach (var chapter in definition.Chapters)
            {
                if (chapter.Quest.Start == QuestStart.ByQuestGiver)
                {
                    Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(chapter.Quest));
                    var page = giver.BuildDialoguePage();
                    Assert.AreEqual(chapter.ArrivalText, page.Body);
                    Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(chapter.Quest), "Reading isn't acceptance.");
                    page.Actions.Single().Chosen();
                }
                else
                {
                    Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(chapter.Quest), "It waits a moment after the last chapter.");
                    manager.StartWaitingQuests(Time.time + 1000f);
                }

                Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(chapter.Quest), chapter.Quest.name);
                foreach (var goal in chapter.Quest.Objectives)
                    GameEvents.Raise(goal.DeedKind.HasValue ? new GameEvent(goal.DeedKind.Value, goal.DeedTarget, goal.Amount) :
                        new GameEvent(goal.Type == ObjectiveType.QuestEvent ? GameEventKind.Custom :
                        goal.Type == ObjectiveType.BuildStructure ? GameEventKind.Built :
                        goal.Type == ObjectiveType.CraftItem ? GameEventKind.Crafted : GameEventKind.ItemObtained,
                        goal.Type == ObjectiveType.QuestEvent ? (Object)goal.QuestEvent :
                        goal.Type == ObjectiveType.BuildStructure ? (Object)goal.Building : goal.Item, goal.Amount));
                Assert.AreEqual(chapter.Quest.TurnInToGiver ? QuestStatus.ReadyToTurnIn : QuestStatus.Completed,
                    manager.Log.StatusOf(chapter.Quest), chapter.Quest.name);
            }
            Assert.IsTrue(manager.Log.HasHappened(definition.FinishedEvent));
            Assert.AreEqual(before, EditorJsonUtility.ToJson(definition), "Shared authored data must stay immutable.");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [TestCase("  - quest:")]
    [TestCase("  - text: Hello\n    quest:")]
    public void SavedIndex_RecognizesStandardOrderedOffersWithoutAnyStoryComponents(string member)
    {
        const string script = "11111111111111111111111111111111";
        const string quest = "22222222222222222222222222222222";
        string text = "\n--- !u!1 &100\nGameObject:\n  m_Name: Ordinary giver\n" +
            "\n--- !u!114 &101\nMonoBehaviour:\n  m_GameObject: {fileID: 100}\n" +
            $"  m_Script: {{fileID: 11500000, guid: {script}, type: 3}}\n" +
            $"  offers:\n{member} {{fileID: 11400000, guid: {quest}, type: 2}}\n";
        var uses = QuestSceneIndex.Parse(text, "Test.unity",
            new Dictionary<string, Type> { [script] = typeof(QuestGiver) }, new[] { quest });
        Assert.AreEqual(QuestSceneRole.Gives, uses.Single().Role);
        Assert.AreEqual("Ordinary giver", uses.Single().ObjectName);
    }

    [Test]
    public void HomecomingQuest_StartsThroughItsAuthoredEvent_NotAGiverBypass()
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(
            "Assets/_Project/Data/Quests/Awakening.asset");
        Assert.AreEqual(QuestStart.OnQuestEvent, quest.Start);
        Assert.NotNull(quest.StartEvent);
        Manager(quest);
        foreach (var prerequisite in quest.Prerequisites) manager.Log.ForceComplete(prerequisite);
        Assert.AreEqual(QuestStatus.NotStarted, manager.Log.StatusOf(quest));
        GameEvents.Raise(quest.StartEvent);
        manager.StartWaitingQuests(Time.time + 1000f);
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(quest));
        Assert.IsTrue(manager.Log.HasHappened(quest.StartEvent));
        GameEvents.Raise(quest.StartEvent);
        Assert.AreEqual(QuestStatus.Active, manager.Log.StatusOf(quest));
    }
}
