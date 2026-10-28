using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StoryTests
{
    private GameObject holder;

    [TearDown]
    public void TearDown()
    {
        if (holder != null)
        {
            Object.DestroyImmediate(holder);
        }
    }

    [Test]
    public void EveryNodeKindCanBeMadeAndNamed()
    {
        Type[] kinds = TypeCache.GetTypesDerivedFrom<StoryNode>().Where(t => !t.IsAbstract).ToArray();
        Assert.GreaterOrEqual(kinds.Length, 20);
        foreach (Type kind in kinds)
        {
            var node = (StoryNode)Activator.CreateInstance(kind);
            Assert.IsFalse(string.IsNullOrWhiteSpace(node.Title), kind.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(node.Category), kind.Name);
            Assert.IsTrue(kind.IsSerializable, $"{kind.Name} needs [Serializable] to be saved in a graph");
            Assert.GreaterOrEqual(node.OutputCount, 1);
        }

        Assert.IsTrue(kinds.Select(k => (StoryNode)Activator.CreateInstance(k)).Count(n => n.IsTrigger) >= 5, "several ways to start a story");
    }

    [Test]
    public void LinksFollowTheirNodesAndNeverLeadIntoATrigger()
    {
        holder = new GameObject("Story");
        var script = holder.AddComponent<StoryScript>();
        var start = new WhenSceneStarts();
        var line = new SayNode { line = "Hello." };
        var choice = new ChoiceNode { line = "Well?", answers = new System.Collections.Generic.List<string> { "Yes.", "No.", "Maybe." } };
        script.AddNode(start);
        script.AddNode(line);
        script.AddNode(choice);
        script.Connect(start, 0, line);
        script.Connect(line, 0, choice);
        script.Connect(choice, 2, line);
        script.Connect(line, 0, start);
        script.Connect(line, 0, choice);

        Assert.AreEqual(3, script.Links.Count, "no link into a trigger, no duplicates");
        Assert.AreEqual(3, choice.OutputCount);
        Assert.AreEqual("Maybe.", choice.OutputName(2));
        script.RemoveNode(line);
        Assert.IsEmpty(script.Links, "removing a node removes its links");
        Assert.AreSame(choice, script.Find(choice.id));
    }

    [Test]
    public void AStoryWithoutATriggerOrWithMissingPartsSaysSo()
    {
        holder = new GameObject("Story");
        var script = holder.AddComponent<StoryScript>();
        script.AddNode(new SayNode());
        Assert.IsTrue(script.FindProblems().Any(p => p.Contains("No trigger")));
        Assert.IsTrue(script.FindProblems().Any(p => p.Contains("write the line")));
        script.AddNode(new WhenUsed());
        Assert.IsTrue(script.FindProblems().Any(p => p.Contains("choose the object to use")));
    }

    [Test]
    public void FiredTriggersAreSavedAndRestored()
    {
        holder = new GameObject("Story");
        var script = holder.AddComponent<StoryScript>();
        var trigger = new WhenQuestEvent();
        script.AddNode(trigger);
        script.RestoreState(trigger.id + ",other");
        Assert.IsTrue(script.HasFired(trigger));
        Assert.AreEqual(2, script.CaptureState().Split(',').Length);
        script.RestoreState("");
        Assert.IsFalse(script.HasFired(trigger));
    }

    [Test]
    public void TheDemoTellsBothStories()
    {
        string scene = System.IO.File.ReadAllText("Assets/_Project/Scenes/Demo/StoryDemo.unity");
        StringAssert.Contains("Story - At least it's not raining", scene);
        StringAssert.Contains("Story - The Waking Stone", scene);
        StringAssert.Contains("WitherGroundNode", scene);
        StringAssert.Contains("FocusCameraNode", scene);
    }

    [Test]
    public void CheckNodesLeaveThroughWhatHasHappened()
    {
        var quest = ScriptableObject.CreateInstance<QuestDefinition>();
        var gameEvent = ScriptableObject.CreateInstance<GameEventDefinition>();
        try
        {
            var check = new CheckQuestNode { quest = quest };
            Assert.AreEqual(3, check.OutputCount);
            Assert.AreEqual(0, Run(check), "a quest never started");

            var happened = new HasHappenedNode { questEvent = gameEvent };
            var data = new SerializedObject(gameEvent);
            data.FindProperty("id").stringValue = "test.story_check_" + Guid.NewGuid().ToString("N");
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.AreEqual(1, Run(happened), "No: it hasn't happened");
            GameSession.Quests.Handle(new GameEvent(GameEventKind.Custom, gameEvent));
            Assert.AreEqual(0, Run(happened), "Yes once it was raised");
            Assert.AreEqual("Yes", happened.OutputName(0));
        }
        finally
        {
            Object.DestroyImmediate(quest);
            Object.DestroyImmediate(gameEvent);
        }
    }

    [Test]
    public void TheIntroMomentsAreCompleteStories()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Prologue_MistyWoods.unity");
        try
        {
            StoryScript[] stories = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StoryScript>(true)).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "Story - Bow or Axe", "Story - Eirik's Waterskin", "Story - Troll Ambush", "Story - The Old Troll" },
                stories.Select(s => s.name).ToArray());
            foreach (StoryScript story in stories)
            {
                CollectionAssert.IsEmpty(story.FindProblems(), story.name);
                Assert.NotNull(story.GetComponent<SaveableEntity>(), $"{story.name} remembers what has fired");
            }

            ChoiceNode choice = stories.Single(s => s.name == "Story - Bow or Axe").Nodes.OfType<ChoiceNode>().Single();
            Assert.IsTrue(choice.mustAnswer, "Nobody leaves without a weapon.");
            Assert.AreEqual(2, choice.answers.Count);
            Assert.IsTrue(stories.Single(s => s.name == "Story - Bow or Axe").Nodes.OfType<GiveItemsNode>().All(g => g.equip));

            PrologueCompanion[] companions = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrologueCompanion>()).ToArray();
            Assert.AreEqual(3, companions.Length);
            foreach (PrologueCompanion companion in companions)
            {
                Assert.AreEqual(3, new SerializedObject(companion).FindProperty("campTopics").arraySize, companion.name);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static int Run(StoryNode node)
    {
        var context = new StoryContext(null);
        System.Collections.IEnumerator steps = node.Run(context);
        while (steps.MoveNext())
        {
        }

        return context.Output;
    }
}
