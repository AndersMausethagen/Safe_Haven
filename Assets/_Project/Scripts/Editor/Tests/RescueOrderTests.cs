using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Who comes home, and when (Docs/RescueOrder.md): each rescue waits for the one before, answers a problem, and opens the
/// building they work at. Nobody arrives any other way.
/// </summary>
public sealed class RescueOrderTests
{
    private static readonly string[] Order = { "Bjarne", "Tor", "Eirik", "Gunnar", "Asa", "Yrsa", "Runa", "Liv", "Hild", "Sigrid" };

    private static RaidDefinition Woods => AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
    private static RaidRumourDefinition Rumour(string file) => AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>($"Assets/_Project/Data/Raids/Rumours/{file}.asset");
    private static QuestDefinition Quest(string file) => AssetDatabase.LoadAssetAtPath<QuestDefinition>($"Assets/_Project/Data/Quests/{file}.asset");
    private static BuildingDefinition Building(string file) => AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/_Project/Data/Buildings/{file}.asset");
    private static JobDefinition Job(string file) => AssetDatabase.LoadAssetAtPath<JobDefinition>($"Assets/_Project/Data/Settlement/{file}.asset");

    private static ResidentDefinition Person(string name) => AssetDatabase.FindAssets("t:ResidentDefinition " + name)
        .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid))).First(person => person.name == name);

    [Test]
    public void EachRumour_WaitsForThePersonBefore()
    {
        string before = "Ingrid";
        foreach (string name in Order)
        {
            RaidRumourDefinition rumour = Rumour(name);
            Assert.NotNull(rumour, name);
            Assert.AreEqual(name, rumour.Seeks.name);
            Assert.AreEqual(before, rumour.AfterHome != null ? rumour.AfterHome.name : null, name + " waits for " + before);
            Assert.IsTrue(rumour.Urgent, name + " comes up as soon as they can");
            Assert.NotNull(rumour.Site != null ? rumour.Site.SitePrefab : null, name + " has somewhere to be found");
            CollectionAssert.Contains(Woods.GetSystem<RaidRumoursSystem>().Pool, rumour, name + " is out in the Misty Woods");
            Assert.IsNull(new SerializedObject(rumour).FindProperty("needsBuilding").objectReferenceValue, name + ": a rescue opens the workplace, not the other way round");
            before = name;
        }
    }

    [Test]
    public void TheNextPerson_ComesUp_OnlyOnceTheLastIsHome()
    {
        RaidRumourDefinition tor = Rumour("Tor");
        RaidTimeOfDay day = Enumerable.Range(1, 500).Select(seed => RaidTimeOfDay.For(Woods, seed)).First(hour => hour != null && !hour.IsDark);
        var settlement = new SettlementState();
        settlement.RecordExtraction();
        Assert.IsFalse(tor.CanComeUp(day, settlement, null), "Bjarne isn't home yet");
        settlement.AddResident(Person("Bjarne"));
        Assert.IsTrue(tor.CanComeUp(day, settlement, null));
        settlement.AddResident(Person("Tor"));
        Assert.IsFalse(tor.CanComeUp(day, settlement, null), "nobody is sought who has come home");
    }

    [Test]
    public void EveryWorkplace_IsOpenedByWhoWorksThere()
    {
        (string job, string building)[] taught =
        {
            ("Alchemist", "PurificationAltar"), ("Alchemist", "Cauldron"), ("Smith", "Forge"), ("Carpenter", "Sawbench"), ("Carpenter", "WainwrightsYard"),
            ("Warden", "WatchPost"), ("Hunter", "TanningRack"), ("Fisher", "Dock"), ("Farmer", "FarmPlot"), ("Weaver", "Loom"), ("Cook", "Kitchen"),
            ("Leader", "HavenBanner"), ("Mason", "Cottage"), ("Mason", "StoneWall"),
        };
        foreach ((string job, string building) in taught)
        {
            CollectionAssert.Contains(Job(job).TeachesBuildings, Building(building), $"{job} teaches {building}");
            Assert.IsFalse(Building(building).KnownFromStart, building + " waits for whoever works there");
        }

        Assert.IsTrue(Building("ConstructionTable").KnownFromStart && Building("SmallChest").KnownFromStart, "the opening's own buildings stay open");
    }

    [Test]
    public void TheSmithy_AndTheCauldron_NeedOnlyTheWorkshop_AndTheYardNeedsLodgeII()
    {
        Assert.AreEqual(new[] { ("ConstructionTable", 1) }, Building("Forge").Requires.Select(r => (r.building.name, r.tier)).ToArray(), "Bjarne comes before Tor");
        Assert.AreEqual(new[] { ("ConstructionTable", 1) }, Building("Cauldron").Requires.Select(r => (r.building.name, r.tier)).ToArray(), "Ingrid heals from the start");
        Assert.AreEqual(new[] { ("Sawbench", 2) }, Building("WainwrightsYard").Requires.Select(r => (r.building.name, r.tier)).ToArray());
    }

    [Test]
    public void ARescue_TeachesTheirWorkplaces()
    {
        var player = new GameObject("Player");
        try
        {
            PlayerCrafting crafting = player.AddComponent<PlayerCrafting>();
            Assert.IsFalse(crafting.IsKnown(Building("Forge")));
            ResidentNpc.TeachWhatTheyKnow(Person("Bjarne"), player);
            Assert.IsTrue(crafting.IsKnown(Building("Forge")));
            ResidentNpc.TeachWhatTheyKnow(Person("Gunnar"), player);
            Assert.IsTrue(crafting.IsKnown(Building("TanningRack")) && crafting.IsKnown(Building("Dock")), "every job they can do");
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void EachRescueQuest_FollowsTheLast_AndTheWorkFollowsTheRescue()
    {
        QuestDefinition before = Quest("AStrangerInTheMist");
        foreach (string name in Order)
        {
            QuestDefinition rescue = Quest("Bring" + name + "Home");
            Assert.NotNull(rescue, name);
            CollectionAssert.AreEqual(new[] { before }, rescue.Prerequisites, rescue.name);
            Assert.IsTrue(rescue.Objectives.All(o => o.Type == ObjectiveType.RescuePerson && o.Person == Person(name) && o.CountAlreadyOwned), rescue.name);
            Assert.IsNotEmpty(rescue.Description, rescue.name + " says what problem they answer");
            Assert.IsEmpty(rescue.FindProblems(), rescue.name);
            before = rescue;
        }

        foreach ((string work, string rescue) in new[] { ("TheSmith", "Bjarne"), ("WoodcuttersLodge", "Tor"), ("TheWarden", "Eirik"), ("TheHunter", "Gunnar"), ("TheFarmer", "Asa"),
                     ("TheCook", "Yrsa"), ("TheTracker", "Runa"), ("TheRuneScholar", "Liv"), ("TheMason", "Hild"), ("TheLeader", "Sigrid") })
        {
            QuestDefinition quest = Quest(work);
            CollectionAssert.AreEqual(new[] { Quest("Bring" + rescue + "Home") }, quest.Prerequisites, work);
            Assert.AreEqual(ObjectiveType.AssignJob, quest.Objectives[0].Type, work);
            Assert.IsEmpty(quest.FindProblems(), work);
        }
    }

    [Test]
    public void TheNewQuests_AreInTheContentDatabase()
    {
        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        foreach (string name in Order)
        {
            CollectionAssert.Contains(database.Quests, Quest("Bring" + name + "Home"), name);
        }

        CollectionAssert.Contains(database.Quests, Quest("HeavyLoads"));
    }
}
