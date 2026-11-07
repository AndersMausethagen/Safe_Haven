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
    public void TheHamletRank_IsWhatTheQuestWaitsFor()
    {
        int hamlet = HavenSettings.Current.Ranks.ToList().FindIndex(rank => rank.name == "Hamlet");
        QuestObjective objective = Quest("ALightTheyCanSee").Objectives[0];

        Assert.AreEqual(ObjectiveType.ReachHavenRank, objective.Type);
        Assert.AreEqual(hamlet, objective.Rank);
        Assert.IsTrue(objective.Matches(new GameEvent(GameEventKind.HavenRankReached, null, hamlet)), "reaching the Hamlet counts");
        Assert.IsFalse(objective.Matches(new GameEvent(GameEventKind.HavenRankReached, null, hamlet - 1)), "the rank below doesn't");
        Assert.IsTrue(objective.CountAlreadyOwned, "a haven can be a Hamlet before the quest starts");
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

    // Playtest 2026-11-05: sticks and small rocks, then the stone tools, then the workshop's materials and the workshop,
    // the woodcutter's lodge, then walls, before the rest of the first days.
    [Test]
    public void TheFirstDays_GoFromBareHandsToWalls_OneStepAtATime()
    {
        string[] order = { "Awakening", "SettlingIn", "PrimitiveTools", "FirstCamp", "WoodcuttersLodge", "RaiseTheWalls", "FoodAndWater" };
        for (int i = 1; i < order.Length; i++)
        {
            QuestDefinition quest = Quest(order[i]);
            Assert.NotNull(quest, order[i]);
            Assert.AreEqual(QuestStart.Automatically, quest.Start, order[i]);
            CollectionAssert.AreEqual(new[] { Quest(order[i - 1]) }, quest.Prerequisites, order[i] + " follows " + order[i - 1]);
        }

        Assert.IsTrue(Quest("PrimitiveTools").Objectives.All(o => o.Type == ObjectiveType.CraftItem), "the tools are made, not found");
        Assert.AreEqual(4, Quest("RaiseTheWalls").Objectives.Single().Amount, "four lengths of palisade");
        Assert.IsFalse(Quest("AGrowingHome").Objectives.Any(o => o.Building != null && o.Building.name == "Sawbench"), "the lodge is built earlier now");
    }
}
