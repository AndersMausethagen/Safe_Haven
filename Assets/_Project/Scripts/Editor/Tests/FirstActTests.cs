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
        QuestDefinition hunt = Quest("WhatBreathesUnderTheRoots"), road = Quest("TheRoadNorth");
        // And once Bjarne is home: its rumour needs two extractions, so it no longer starts a raid early (2026-11-15).
        CollectionAssert.AreEqual(new[] { Quest("TheWeakeningWard"), Quest("BringBjarneHome") }, hunt.Prerequisites, "after the first breach, when the roots have been seen in the dead");
        CollectionAssert.AreEqual(new[] { hunt }, road.Prerequisites);
        foreach (QuestDefinition quest in new[] { hunt, road })
        {
            Assert.AreEqual(QuestRank.MainStory, quest.Rank, quest.name);
            Assert.AreEqual("The Hearth and the Door", quest.Chain, quest.name);
            Assert.AreEqual(QuestStart.Automatically, quest.Start, quest.name);
            Assert.IsNotEmpty(quest.JournalNotes, quest.name);
            Assert.IsEmpty(quest.FindProblems(), quest.name);
        }

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

        var site = Load<RaidEventDefinition>("Assets/_Project/Data/Raids/Rumours/Events/TheHeartroot.asset");
        RaidEventChoice wake = site.Choices.Single();
        Assert.AreEqual(1, wake.cost.Count);
        Assert.AreSame(ember, wake.cost[0].Item);
        StringAssert.Contains("Hearth Ember", wake.detail, "the price is said plainly");
        StringAssert.Contains("steel", Load<RaidRumourDefinition>("Assets/_Project/Data/Raids/Rumours/TheHeartroot.asset").Briefing, "the briefing hints at it");

        var database = Load<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        CollectionAssert.Contains(database.Items, ember, "or saves can't find it");
        CollectionAssert.Contains(database.Quests, Quest("WhatBreathesUnderTheRoots"));
        CollectionAssert.Contains(database.Quests, Quest("TheRoadNorth"));
    }

    [Test]
    public void ItsLastWord_IsForThePlayer_AndTheTrophyGoesToTheFire()
    {
        var boss = Load<BossDefinition>("Assets/_Project/Data/Monsters/Boss The Heartroot.asset");
        StringAssert.Contains("Hearth-keeper", boss.DefeatLine);
        Assert.IsFalse(boss.HomeLines.Any(l => l.Contains("away from the fire")), "the twist asks for it by the fire");
    }

    [Test]
    public void TheyRemember_TheNightTheDoorBroke()
    {
        StringAssert.Contains("came for the door", Load<ResidentDefinition>("Assets/_Project/Data/Settlement/Ingrid.asset").WelcomeLine);
        StringAssert.Contains("didn't know its name", Load<ResidentDefinition>("Assets/_Project/Data/Settlement/Ingrid.asset").WelcomeLine, "the first sight of what the forgotten become");
        StringAssert.Contains("Bjarne's hammer", Load<ResidentDefinition>("Assets/_Project/Data/Settlement/Ingrid.asset").WelcomeLine, "she still points to the next rescue");
        StringAssert.Contains("roots had moved it", Load<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Bjarne.asset").WelcomeLine);
        StringAssert.Contains("saw", Load<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Bjarne.asset").WelcomeLine, "he points to Tor");
        StringAssert.Contains("breathes", Load<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Tor.asset").WelcomeLine, "he points to the hollow");
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
