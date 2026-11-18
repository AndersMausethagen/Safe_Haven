using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The first act's story (Docs/QuestRedesign.md, step 3): the hunt for the Heartroot, which wakes only for an ember from the
/// player's own fire, the twist at the portal, and the moments at home that lead there.
/// </summary>
public sealed class FirstActTests
{
    private const string Quests = "Assets/_Project/Data/Quests/";
    private static QuestDefinition Quest(string file) => AssetDatabase.LoadAssetAtPath<QuestDefinition>(Quests + file + ".asset");
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    private static ItemDefinition Ember => Load<ItemDefinition>("Assets/_Project/Data/Items/HearthEmber.asset");

    [Test]
    public void TheAct_EndsAtTheHeartroot_AndGoesOnNorth()
    {
        // What the hunt and the road north ask for and open. Their order among the other quests, and that they can start at all,
        // are QuestAudit's and QuestReachability's (QuestAuditTests).
        QuestDefinition hunt = Quest("WhatBreathesUnderTheRoots"), road = Quest("TheRoadNorth");
        var heartroot = Load<MonsterDefinition>("Assets/_Project/Data/Monsters/TheHeartroot.asset");
        QuestObjective kill = hunt.Objectives.Single(o => o.Type == ObjectiveType.KillMonster);
        Assert.AreSame(heartroot, new SerializedObject(hunt).FindProperty("objectives").GetArrayElementAtIndex(hunt.Objectives.ToList().IndexOf(kill)).FindPropertyRelative("monster").objectReferenceValue);
        Assert.IsTrue(kill.CountAlreadyOwned, "a Heartroot beaten before the quest counts: it won't come again");
        Assert.AreSame(Ember, hunt.Objectives.Single(o => o.Type == ObjectiveType.CraftItem).Item);
        // The conversation's end raises it (FixQuestAudit): a Talk To the portal counted every talk before the quest.
        QuestObjective told = road.Objectives.Single();
        Assert.AreEqual(ObjectiveType.QuestEvent, told.Type, "the twist is told at the portal");
        Assert.AreSame(Load<GameEventDefinition>(Quests + "Events/RoadNorthHeard.asset"), told.QuestEvent);
        Assert.IsFalse(told.CountAlreadyOwned, "only this conversation counts");
        Assert.AreSame(new SerializedObject(Load<RaidDefinition>("Assets/_Project/Data/Raids/TrollValley.asset")).FindProperty("unlockedBy").objectReferenceValue, heartroot.Prefab.GetComponent<BossEncounter>().Definition,
            "the road north is Troll Valley, which the Heartroot's fall opens");
    }

    [Test]
    public void TheHeartroot_WakesOnlyForAnEmber()
    {
        ItemDefinition ember = Ember;
        Assert.NotNull(ember);
        RecipeDefinition recipe = Load<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/HearthEmber.asset");
        Assert.AreSame(ember, recipe.Output);
        Assert.IsTrue(recipe.KnownFromStart, "a fire is all it asks");
        Assert.AreEqual(Load<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/StewedBerries.asset").Station, recipe.Station, "made at a campfire");

        // Since Guardian's Hollow (2026-11-17) the ember is the gate's price into the Heartroot's own arena; the stump in the Woods
        // only shows the way (the portal has to learn it first).
        var hollowGate = Load<BossEncounterDefinition>("Assets/_Project/Data/Raids/Encounters/Guardian's Hollow Encounter.asset");
        Assert.AreEqual(1, hollowGate.GateItems.Count);
        Assert.AreSame(ember, hollowGate.GateItems[0].Item);
        StringAssert.Contains(ember.DisplayName, hollowGate.GatePrice, "the price is said plainly");
        Assert.IsEmpty(Load<RaidEventDefinition>("Assets/_Project/Data/Raids/Rumours/Events/TheHeartroot.asset").Choices.Single().cost, "the stump asks nothing");

        var database = Load<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        CollectionAssert.Contains(database.Items, ember, "or saves can't find it");
    }

    [Test]
    public void ABossAlreadyBeaten_Counts()
    {
        var heartroot = Load<MonsterDefinition>("Assets/_Project/Data/Monsters/TheHeartroot.asset");
        string id = heartroot.Prefab.GetComponent<BossEncounter>().Definition.Id;
        QuestObjective kill = Quest("WhatBreathesUnderTheRoots").Objectives.Single(o => o.Type == ObjectiveType.KillMonster);
        bool before = GameSession.Here.IsBossDefeated(id);
        try
        {
            if (!before) Assert.AreEqual(0, kill.Owned(null, new QuestLog()));
            GameSession.Home.MarkBossDefeated(id);
            Assert.AreEqual(1, kill.Owned(null, new QuestLog()));
            QuestObjective wolves = QuestObjective.Create(ObjectiveType.KillMonster, 2, Load<MonsterDefinition>("Assets/_Project/Data/Monsters/ForestBittenWolf.asset"));
            Assert.AreEqual(0, wolves.Owned(null, new QuestLog()), "only a boss counts this way, and only when asked to");
        }
        finally
        {
            if (!before) GameSession.StartNewGame();
        }
    }

    [Test]
    public void SavedHaven_HasTheActsMoments()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/SafeHaven.unity");
        try
        {
            var roots = scene.GetRootGameObjects();
            var stories = roots.SelectMany(r => r.GetComponentsInChildren<StoryScript>(true)).ToArray();
            foreach (string name in new[] { "Story - The Roots Move", "Story - The Stones Remember", "Story - Up Through the Ward", "Story - The Root in Them", "Story - The Road North" })
            {
                StoryScript story = stories.SingleOrDefault(s => s.name == name);
                Assert.NotNull(story, name);
                Assert.IsEmpty(story.FindProblems(), name);
                Assert.NotNull(story.GetComponent<SaveableEntity>(), name);
            }

            Assert.AreEqual(3, stories.Single(s => s.name == "Story - The Stones Remember").Nodes.OfType<WhenUsed>().Select(n => n.target).Distinct().Count(), "each stone remembers");
            Assert.IsTrue(stories.Single(s => s.name == "Story - Up Through the Ward").IsFight);
            StoryScript twist = stories.Single(s => s.name == "Story - The Road North");
            Assert.IsFalse(twist.IsFight);
            Assert.AreEqual(3, twist.Nodes.OfType<HasHappenedNode>().Count(), "a word for each reason the player came");
            Assert.IsTrue(twist.Nodes.OfType<SayNode>().Any(n => n.line.Contains("we broke the pact first")));
            var heard = Load<GameEventDefinition>(Quests + "Events/RoadNorthHeard.asset");
            Assert.AreEqual(1, twist.Nodes.OfType<RaiseQuestEventNode>().Count(n => n.questEvent == heard), "the quest is done once it has been said");
            Assert.IsEmpty(twist.Nodes.OfType<CountAsDoneNode>(), "a Talk To the portal would count every earlier talk");

            HomePortal portal = roots.SelectMany(r => r.GetComponentsInChildren<HomePortal>(true)).Single();
            SerializedProperty whispers = new SerializedObject(portal).FindProperty("questWhispers");
            var moment = Load<GameEventDefinition>(Quests + "Events/RoadNorthMoment.asset");
            bool found = false;
            for (int i = 0; i < whispers.arraySize; i++)
            {
                SerializedProperty whisper = whispers.GetArrayElementAtIndex(i);
                if (whisper.FindPropertyRelative("quest").objectReferenceValue == Quest("TheRoadNorth"))
                {
                    found = true;
                    Assert.AreSame(moment, whisper.FindPropertyRelative("moment").objectReferenceValue, "using the portal starts the conversation");
                }
            }

            Assert.IsTrue(found, "the portal has a line for the road north");
            Assert.AreSame(moment, twist.Nodes.OfType<WhenQuestEvent>().Single().questEvent);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
