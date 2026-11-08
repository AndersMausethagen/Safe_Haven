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
        string[] quests = { "Awakening", "SettlingIn", "FirstCamp", "SafeKeeping", "FirstWeapon", "FoodAndWater", "RootboundStones", "AwakeningThePortal", "ThroughThePortal", "AStrangerInTheMist", "WoodcuttersLodge", "RaiseTheWalls", "AGrowingHome", "TheWeakeningWard", "ALightTheyCanSee" };
        foreach (string file in quests)
        {
            Assert.IsNotEmpty(Quest(file).Description, file);
            Assert.IsEmpty(Quest(file).FindProblems(), file);
        }
    }

    // Playtest 2026-11-08: the opening teaches before it builds. Tools, a workshop, a chest (what stays safe at home), a weapon,
    // food, then the Life Orbs and the portal, and the first raid; the lodge and the walls only after it.
    [Test]
    public void TheFirstDays_TeachTheBasics_BeforeThePortal()
    {
        string[] order = { "Awakening", "SettlingIn", "PrimitiveTools", "FirstCamp", "SafeKeeping", "FirstWeapon", "FoodAndWater", "RootboundStones", "AwakeningThePortal", "ThroughThePortal" };
        for (int i = 1; i < order.Length; i++)
        {
            QuestDefinition quest = Quest(order[i]);
            Assert.NotNull(quest, order[i]);
            Assert.AreEqual(QuestStart.Automatically, quest.Start, order[i]);
            CollectionAssert.AreEqual(new[] { Quest(order[i - 1]) }, quest.Prerequisites, order[i] + " follows " + order[i - 1]);
            Assert.AreEqual("The First Days", quest.Chain, order[i]);
        }

        Assert.IsTrue(Quest("PrimitiveTools").Objectives.All(o => o.Type == ObjectiveType.CraftItem), "the tools are made, not found");
        string[] built = order.SelectMany(file => Quest(file).Objectives).Where(o => o.Type == ObjectiveType.BuildStructure).Select(o => o.Building.name).ToArray();
        CollectionAssert.AreEquivalent(new[] { "Campfire", "ConstructionTable", "SmallChest" }, built, "only what the opening teaches is built before the first raid");
    }

    [Test]
    public void TheChestAndTheWeapon_CanBeMadeWithWhatCameBefore()
    {
        BuildingDefinition chest = Quest("SafeKeeping").Objectives.Single().Building;
        Assert.AreEqual("SmallChest", chest.name);
        Assert.IsTrue(chest.KnownFromStart);
        Assert.IsTrue(chest.Requires.All(r => r.building.name == "ConstructionTable" && r.tier <= 1), "the workshop of A Light in the Wood is enough");

        QuestObjective weapon = Quest("FirstWeapon").Objectives.Single();
        Assert.AreEqual(ObjectiveType.CraftItem, weapon.Type);
        Assert.IsTrue(weapon.Item.IsWeapon);
        Assert.IsTrue(weapon.CountAlreadyOwned, "a club made earlier counts");
        Assert.IsFalse(Quest("FoodAndWater").Objectives.Any(o => o.Item == weapon.Item), "the weapon is asked for once");
    }

    [Test]
    public void TheBuildingQuests_ComeAfterTheFirstRaid()
    {
        CollectionAssert.AreEqual(new[] { Quest("ThroughThePortal") }, Quest("WoodcuttersLodge").Prerequisites);
        CollectionAssert.AreEqual(new[] { Quest("WoodcuttersLodge") }, Quest("RaiseTheWalls").Prerequisites);
        CollectionAssert.AreEqual(new[] { Quest("WoodcuttersLodge") }, Quest("AGrowingHome").Prerequisites, "its smithy and shed need the lodge");
        Assert.AreEqual(4, Quest("RaiseTheWalls").Objectives.Single().Amount, "four lengths of palisade");
        Assert.IsFalse(Quest("AGrowingHome").Objectives.Any(o => o.Building != null && o.Building.name == "Sawbench"), "the lodge has its own quest");
    }

    [Test]
    public void TheyAreStillOutThere_SaysHowThePortalWakes()
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Prologue/Quests/TheyAreStillOutThere.asset");

        StringAssert.Contains("three Life Orbs", quest.Description);
        StringAssert.Contains("The First Days", quest.Description);
        Assert.IsTrue(quest.ObjectivesInOrder);
        StringAssert.Contains("Life Orbs", quest.Objectives.Last().Describe());
    }

    // Playtest 2026-11-08: Hild turned up with the Homestead rank, unexplained. Everyone comes home from a raid.
    [Test]
    public void NoRank_BringsSomeone()
    {
        Assert.IsTrue(HavenSettings.Current.Ranks.All(rank => rank.settler == null));
    }

    [Test]
    public void TheNewQuests_AreInTheContentDatabase()
    {
        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        CollectionAssert.Contains(database.Quests, Quest("SafeKeeping"), "or it never starts");
        CollectionAssert.Contains(database.Quests, Quest("FirstWeapon"), "or it never starts");
    }
}
