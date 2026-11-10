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
        string[] quests = { "Awakening", "FirstCamp", "PrimitiveTools", "ARoofForTheWork", "SafeKeeping", "FirstWeapon", "FoodAndWater", "RootboundStones", "AwakeningThePortal", "ThroughThePortal", "AStrangerInTheMist", "WoodcuttersLodge", "RaiseTheWalls", "AGrowingHome", "TheWeakeningWard", "ALightTheyCanSee" };
        foreach (string file in quests)
        {
            Assert.IsNotEmpty(Quest(file).Description, file);
            Assert.IsEmpty(Quest(file).FindProblems(), file);
        }
    }

    // Docs/QuestRedesign.md (2026-11-10): the door opens after three short steps, where nine quests used to stand before it.
    // Listen to the portal, light a fire, free the three stones, wake the door, then the first raid.
    [Test]
    public void TheDoor_OpensAfterTheFireAndTheStones()
    {
        string[] order = { "Awakening", "FirstCamp", "RootboundStones", "AwakeningThePortal", "ThroughThePortal" };
        for (int i = 0; i < order.Length; i++)
        {
            QuestDefinition quest = Quest(order[i]);
            Assert.NotNull(quest, order[i]);
            Assert.AreEqual(QuestRank.MainStory, quest.Rank, order[i]);
            Assert.AreEqual("The Hearth and the Door", quest.Chain, order[i]);
            if (i == 0) continue;
            Assert.AreEqual(QuestStart.Automatically, quest.Start, order[i]);
            CollectionAssert.AreEqual(new[] { Quest(order[i - 1]) }, quest.Prerequisites, order[i] + " follows " + order[i - 1]);
        }

        QuestObjective fire = Quest("FirstCamp").Objectives.Single();
        Assert.AreEqual(ObjectiveType.BuildStructure, fire.Type, "the fire is the whole quest: no wood, no stone, no workshop");
        Assert.AreEqual("Campfire", fire.Building.name);
        string[] built = order.SelectMany(file => Quest(file).Objectives).Where(o => o.Type == ObjectiveType.BuildStructure).Select(o => o.Building.name).ToArray();
        CollectionAssert.AreEqual(new[] { "Campfire" }, built, "the fire is the only thing built before the first raid");
        Assert.IsNull(AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/SettlingIn.asset"), "Gifts of the Clearing is retired");
        Assert.IsNull(AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Prologue/Quests/TheyAreStillOutThere.asset"), "They Are Still Out There is retired");
    }

    [Test]
    public void TheFirstRaid_FindsIngrid_AndComesHome()
    {
        QuestDefinition raid = Quest("ThroughThePortal");
        CollectionAssert.AreEqual(new[] { ObjectiveType.RescuePerson, ObjectiveType.CompleteRaids }, raid.Objectives.Select(o => o.Type).ToArray(),
            "no tool has to be made first");
        QuestObjective find = raid.Objectives[0];
        Assert.AreEqual(Quest("AStrangerInTheMist").Objectives[0].Person, find.Person);
        Assert.IsFalse(find.BringHome, "finding her moves the story on; bringing her home is the rescue quest");
        Assert.IsTrue(find.CountAlreadyOwned);
        Assert.IsFalse(raid.ObjectivesInOrder);
        Assert.AreEqual(QuestRank.Rescue, Quest("AStrangerInTheMist").Rank);
    }

    // What used to stand before the portal is optional now, beside the story.
    [Test]
    public void TheFirstDays_AreHavenQuests_BesideTheStory()
    {
        CollectionAssert.AreEqual(new[] { Quest("FirstCamp") }, Quest("PrimitiveTools").Prerequisites, "tools once the fire burns");
        CollectionAssert.AreEqual(new[] { Quest("FirstCamp") }, Quest("FoodAndWater").Prerequisites, "food once the fire burns");
        CollectionAssert.AreEqual(new[] { Quest("AwakeningThePortal") }, Quest("ARoofForTheWork").Prerequisites, "the workshop once the door is awake");
        CollectionAssert.AreEqual(new[] { Quest("ARoofForTheWork") }, Quest("SafeKeeping").Prerequisites);
        CollectionAssert.AreEqual(new[] { Quest("ARoofForTheWork") }, Quest("FirstWeapon").Prerequisites);
        foreach (string file in new[] { "PrimitiveTools", "FoodAndWater", "ARoofForTheWork", "SafeKeeping", "FirstWeapon" })
        {
            Assert.AreEqual(QuestRank.Haven, Quest(file).Rank, file);
            Assert.AreEqual(QuestStart.Automatically, Quest(file).Start, file);
            Assert.AreEqual("The First Days", Quest(file).Chain, file);
        }

        Assert.IsTrue(Quest("PrimitiveTools").Objectives.All(o => o.Type == ObjectiveType.CraftItem), "the tools are made, not found");
        QuestObjective workshop = Quest("ARoofForTheWork").Objectives.Single();
        Assert.AreEqual("ConstructionTable", workshop.Building.name);
        Assert.IsTrue(workshop.CountAlreadyOwned, "a workshop built before the quest counts");
    }

    [Test]
    public void TheChestAndTheWeapon_CanBeMadeWithWhatCameBefore()
    {
        BuildingDefinition chest = Quest("SafeKeeping").Objectives.Single(o => o.Type == ObjectiveType.BuildStructure).Building;
        Assert.AreEqual("SmallChest", chest.name);
        Assert.IsTrue(chest.KnownFromStart);
        Assert.IsTrue(chest.Requires.All(r => r.building.name == "ConstructionTable" && r.tier <= 1), "the workshop of A Roof for the Work is enough");

        QuestObjective weapon = Quest("FirstWeapon").Objectives.Single();
        Assert.AreEqual(ObjectiveType.CraftItem, weapon.Type);
        Assert.IsTrue(weapon.Item.IsWeapon);
        Assert.IsTrue(weapon.CountAlreadyOwned, "a club made earlier counts");
        Assert.IsFalse(Quest("FoodAndWater").Objectives.Any(o => o.Item == weapon.Item), "the weapon is asked for once");
    }

    [Test]
    public void TheBuildingQuests_ComeAfterTor()
    {
        CollectionAssert.AreEqual(new[] { Quest("BringTorHome") }, Quest("WoodcuttersLodge").Prerequisites, "the lodge is his to show");
        CollectionAssert.AreEqual(new[] { Quest("WoodcuttersLodge") }, Quest("RaiseTheWalls").Prerequisites);
        CollectionAssert.AreEqual(new[] { Quest("WoodcuttersLodge") }, Quest("AGrowingHome").Prerequisites, "its smithy and shed need the lodge");
        Assert.AreEqual(4, Quest("RaiseTheWalls").Objectives.Single().Amount, "four lengths of palisade");
        Assert.IsFalse(Quest("AGrowingHome").Objectives.Any(o => o.Building != null && (o.Building.name == "Sawbench" || o.Building.name == "Forge")), "the lodge and the smithy have their own quests");
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
        CollectionAssert.Contains(database.Quests, Quest("ARoofForTheWork"), "or it never starts");
        Assert.IsFalse(database.Quests.Contains(null), "a retired quest leaves no hole");
    }
}
