using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>The quest line at home: why the haven is built, and the links between its quests that are easy to break by editing data.</summary>
public sealed class HomeStoryTests
{
    private static QuestDefinition Quest(string file) => AssetDatabase.LoadAssetAtPath<QuestDefinition>($"Assets/_Project/Data/Quests/{file}.asset");

    [Test]
    public void TheSearchForIngrid_StartsWhenThePortalWakes()
    {
        QuestDefinition quest = Quest("AStrangerInTheMist");

        Assert.AreEqual(QuestStart.Automatically, quest.Start);
        CollectionAssert.Contains(quest.Prerequisites, Quest("AwakeningThePortal"));
        Assert.AreEqual(2, quest.Objectives.Count, "find her, then bring her home");
        Assert.IsTrue(quest.ObjectivesInOrder);
        Assert.IsTrue(quest.Objectives.All(objective => objective.CountAlreadyOwned), "a rescue before the quest starts still counts");
    }

    [Test]
    public void ALightTheyCanSee_ComesAfterTheFirstDefenceAndTheNewBuildings()
    {
        QuestDefinition quest = Quest("ALightTheyCanSee");

        CollectionAssert.AreEquivalent(new[] { Quest("TheWeakeningWard"), Quest("AGrowingHome") }, quest.Prerequisites);
        Assert.IsEmpty(quest.FindProblems());
        CollectionAssert.Contains(AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset").Quests, quest, "or it never starts");
    }

    [Test]
    public void TheHamletRank_RaisesWhatTheQuestWaitsFor()
    {
        HavenSettings.Rank hamlet = HavenSettings.Current.Ranks.First(rank => rank.name == "Hamlet");

        Assert.IsNotNull(hamlet.reachedEvent);
        Assert.AreEqual(hamlet.reachedEvent, Quest("ALightTheyCanSee").Objectives[0].QuestEvent);
        Assert.IsTrue(Quest("ALightTheyCanSee").Objectives[0].CountAlreadyOwned, "a haven can be a Hamlet before the quest starts");
    }

    [Test]
    public void TheHavenPage_CountsTheWholeExpedition()
    {
        var expedition = HavenSettings.Current.Expedition;

        Assert.AreEqual(7, expedition.Count);
        Assert.IsFalse(expedition.Contains(null));
        Assert.AreEqual(7, expedition.Select(person => person.DisplayName).Distinct().Count());
    }

    [Test]
    public void EveryHomeQuest_SaysWhy()
    {
        string[] quests = { "Awakening", "SettlingIn", "FirstCamp", "FoodAndWater", "RootboundStones", "AwakeningThePortal", "ThroughThePortal", "AStrangerInTheMist", "AGrowingHome", "TheWeakeningWard", "ALightTheyCanSee" };
        foreach (string file in quests)
        {
            Assert.IsNotEmpty(Quest(file).Description, file);
            Assert.IsEmpty(Quest(file).FindProblems(), file);
        }
    }
}
