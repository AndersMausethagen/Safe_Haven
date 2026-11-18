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

    // The builders give triggers the ids saved games know after linking them (StoryBuilder.Finish). Setting the id alone
    // once left 27 stories stopped at their trigger: Eirik never gave the hatchet, and Sigrid never opened the prologue.
    [Test]
    public void BuilderGivesTriggersTheirIdsAndKeepsTheirLinks()
    {
        holder = new GameObject("Stories");
        StoryScript story = StoryBuilder.Fresh(holder.transform, "Story - Test");
        var start = new WhenSceneStarts();
        var wait = new WaitNode();
        StoryBuilder.Link(story, (start, 0, wait));
        StoryBuilder.Finish();

        Assert.AreEqual(StoryBuilder.StableId("trigger:Story - Test#0"), start.id);
        Assert.AreEqual(start.id, story.Links.Single().from);
        Assert.AreEqual(wait.id, story.Links.Single().to);
        Assert.IsEmpty(story.FindProblems());
    }

    [Test]
    public void EveryStoryInTheGameFollowsItsLinks()
    {
        int checkedStories = 0;
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            var scene = EditorSceneManager.OpenPreviewScene(entry.path);
            try
            {
                foreach (StoryScript story in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StoryScript>(true)))
                {
                    checkedStories++;
                    var ids = story.Nodes.Where(n => n != null).Select(n => n.id).ToHashSet();
                    Assert.IsTrue(story.Links.All(l => ids.Contains(l.from) && ids.Contains(l.to)), $"{entry.path}: {story.name} has a link to or from a node it doesn't have");
                    Assert.IsTrue(story.Nodes.Where(n => n != null && n.IsTrigger).All(t => story.Links.Any(l => l.from == t.id)), $"{entry.path}: {story.name} has a trigger that leads nowhere");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        Assert.Greater(checkedStories, 30, "the prologue's and the Haven's stories");
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
        // What is missing is reported; in the Story window's own words, which this doesn't pin.
        script.AddNode(new SayNode());
        Assert.IsNotNull(new SayNode().FindProblem(), "a line with nothing written");
        Assert.GreaterOrEqual(script.FindProblems().Count(), 2, "no trigger, and an empty line");
        script.AddNode(new WhenUsed());
        Assert.IsNotNull(new WhenUsed().FindProblem(), "a trigger with no object to use");
        Assert.IsNotEmpty(script.FindProblems(), "still something to fix");
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
    public void AVisitor_DoesntPlayTheHostsHavenStories_ButKeepsThemForHome()
    {
        holder = new GameObject("Story");
        var script = holder.AddComponent<StoryScript>();
        var trigger = new WhenQuestEvent();
        script.AddNode(trigger);
        var visiting = typeof(HavenVisit).GetProperty(nameof(HavenVisit.IsVisiting));
        visiting.SetValue(null, true);
        try
        {
            script.Fire(trigger);
            Assert.IsFalse(script.HasFired(trigger), "Not in a friend's Haven, and not spent there either");
        }
        finally
        {
            visiting.SetValue(null, false);
        }

        script.Fire(trigger);
        Assert.IsTrue(script.HasFired(trigger), "At home it plays");
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
    public void ScriptedMovement_ScalesAnimationAndRestoresItWhenStopped()
    {
        holder = new GameObject("Scripted walker");
        var animator = holder.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/_Project/Animation/Controllers/MonsterHumanoid.controller");
        animator.Rebind();
        animator.speed = 0.8f;
        var destination = new GameObject("Destination").transform;
        destination.SetParent(holder.transform);
        destination.position = Vector3.forward * 10f;
        var node = new MoveObjectNode
        {
            target = holder.transform, destination = destination, seconds = 12f,
            animationSpeed = 0.5f, waitUntilArrived = true,
        };
        var outer = node.Run(new StoryContext(null));
        Assert.IsTrue(outer.MoveNext());
        var moving = (System.Collections.IEnumerator)outer.Current;
        try
        {
            Assert.IsTrue(moving.MoveNext());
            Assert.AreEqual(0.4f, animator.speed, 0.001f);
        }
        finally { ((IDisposable)moving).Dispose(); }
        Assert.AreEqual(0.8f, animator.speed, 0.001f);
        Assert.AreEqual(0f, animator.GetFloat("Speed"));
        node.animationSpeed = 0f;
        StringAssert.Contains("positive", node.FindProblem());
    }

    [Test]
    public void Footfalls_CountRealMovementEvenWhenAiIsDisabled_AndIgnoreWarps()
    {
        holder = new GameObject("Scripted heavy creature");
        var footfalls = holder.AddComponent<CreatureFootfalls>();
        holder.GetComponent<MonsterAI>().enabled = false;
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        typeof(CreatureFootfalls).GetMethod("Awake", flags).Invoke(footfalls, null);
        typeof(CreatureFootfalls).GetMethod("OnEnable", flags).Invoke(footfalls, null);
        var update = typeof(CreatureFootfalls).GetMethod("Update", flags);
        var travelled = typeof(CreatureFootfalls).GetField("travelled", flags);
        holder.transform.position += Vector3.forward;
        update.Invoke(footfalls, null);
        Assert.AreEqual(1f, (float)travelled.GetValue(footfalls), 0.001f);
        holder.transform.position += Vector3.forward * 1.5f;
        update.Invoke(footfalls, null);
        Assert.AreEqual(0.1f, (float)travelled.GetValue(footfalls), 0.001f, "A stride produces its footfall and keeps the remainder.");
        holder.transform.position += Vector3.forward * 100f;
        update.Invoke(footfalls, null);
        Assert.AreEqual(0f, (float)travelled.GetValue(footfalls), "A teleport must not become a stomp.");
    }

    [Test]
    public void TheIntroMomentsAreCompleteStories()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Prologue_MistyWoods.unity");
        try
        {
            StoryScript[] stories = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StoryScript>(true)).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "Story - Choose a Weapon", "Story - Eirik's Waterskin", "Story - Troll Ambush", "Story - The Old Troll" },
                stories.Select(s => s.name).ToArray());
            foreach (StoryScript story in stories)
            {
                CollectionAssert.IsEmpty(story.FindProblems(), story.name);
                Assert.NotNull(story.GetComponent<SaveableEntity>(), $"{story.name} remembers what has fired");
            }

            ChoiceNode choice = stories.Single(s => s.name == "Story - Choose a Weapon").Nodes.OfType<ChoiceNode>().Single();
            Assert.IsTrue(choice.mustAnswer, "Nobody goes into the forest without a weapon.");
            Assert.AreEqual(5, choice.answers.Count, "Sword, axe, spear, club and bow.");
            Assert.IsTrue(stories.Single(s => s.name == "Story - Choose a Weapon").Nodes.OfType<GiveItemsNode>().All(g => g.equip));

            PrologueCompanion[] companions = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PrologueCompanion>()).ToArray();
            Assert.AreEqual(3, companions.Length);
            foreach (PrologueCompanion companion in companions)
            {
                Assert.GreaterOrEqual(new SerializedObject(companion).FindProperty("campTopics").arraySize, 3, companion.name);
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
