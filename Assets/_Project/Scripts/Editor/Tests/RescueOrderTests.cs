using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Who comes home, and when (Docs/RescueOrder.md): each rescue waits for the one before, answers a problem, and opens the
/// building they work at. Nobody arrives any other way. The order itself is the rumours' data (and Docs/RescueOrder.md's).
/// </summary>
public sealed class RescueOrderTests
{
    private static RaidDefinition Woods => AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
    private static RaidRumourDefinition Rumour(string file) => AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>($"Assets/_Project/Data/Raids/Rumours/{file}.asset");
    private static BuildingDefinition Building(string file) => AssetDatabase.LoadAssetAtPath<BuildingDefinition>($"Assets/_Project/Data/Buildings/{file}.asset");

    private static ResidentDefinition Person(string name) => AssetDatabase.FindAssets("t:ResidentDefinition " + name)
        .Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid))).First(person => person.name == name);

    // One chain from Ingrid: each rescue's rumour waits for the person before, with no gap, fork or loop, and each is urgent, has
    // somewhere to be found and asks for no building (a rescue opens the workplace, not the other way round).
    [Test]
    public void TheRescues_FormOneChain_FromIngrid()
    {
        List<RaidRumourDefinition> rescues = Woods.GetSystem<RaidRumoursSystem>().Pool
            .Where(rumour => rumour != null && rumour.Seeks != null && rumour.AfterHome != null).ToList();
        Assert.IsNotEmpty(rescues, "the rescues' rumours are out in the Misty Woods");
        foreach (IGrouping<ResidentDefinition, RaidRumourDefinition> fork in rescues.GroupBy(rumour => rumour.AfterHome).Where(group => group.Count() > 1))
        {
            Assert.Fail($"{string.Join(" and ", fork.Select(rumour => rumour.Seeks.name))} all wait for {fork.Key.name}: a fork in the order");
        }

        Dictionary<ResidentDefinition, RaidRumourDefinition> next = rescues.ToDictionary(rumour => rumour.AfterHome);
        ResidentDefinition home = Person("Ingrid");
        var sought = new HashSet<ResidentDefinition> { home };
        int steps = 0;
        while (next.TryGetValue(home, out RaidRumourDefinition rumour))
        {
            string name = rumour.Seeks.name;
            Assert.IsTrue(sought.Add(rumour.Seeks), name + " is sought twice: a loop in the order");
            Assert.IsTrue(rumour.Urgent, name + " comes up as soon as they can");
            Assert.NotNull(rumour.Site != null ? rumour.Site.SitePrefab : null, name + " has somewhere to be found");
            Assert.IsNull(new SerializedObject(rumour).FindProperty("needsBuilding").objectReferenceValue, name + ": a rescue opens the workplace, not the other way round");
            home = rumour.Seeks;
            steps++;
        }

        Assert.AreEqual(rescues.Count, steps, "every rescue is on the one chain from Ingrid");
    }

    [Test]
    public void TheNextPerson_ComesUp_OnlyOnceTheLastIsHome()
    {
        RaidRumourDefinition tor = Rumour("Tor");
        RaidTimeOfDay day = Enumerable.Range(1, 500).Select(seed => RaidTimeOfDay.For(Woods, seed)).First(hour => hour != null && !hour.IsDark);
        var settlement = new SettlementState();
        settlement.RecordExtraction();
        // The user (2026-11-21): the next person can be found only once the last one's work is learned. Tor's rumour waits for his
        // rescue quest, which waits for Bjarne's first quest (Bring Tor Home's prerequisites).
        QuestDefinition rescueQuest = new SerializedObject(tor).FindProperty("whileQuest").objectReferenceValue as QuestDefinition;
        Assert.IsNotNull(rescueQuest, "Tor's rumour waits for his rescue quest");
        Assert.IsTrue(rescueQuest.Prerequisites.Any(q => q != null && q.name == "TheSmith"), "which waits for Bjarne's first quest");
        string[] underWay = { rescueQuest.Id };
        Assert.IsFalse(tor.CanComeUp(day, settlement, null, underWay), "Bjarne isn't home yet");
        settlement.AddResident(Person("Bjarne"));
        Assert.IsFalse(tor.CanComeUp(day, settlement, null), "Bjarne is home, but his work isn't learned: Tor's quest hasn't begun");
        Assert.IsTrue(tor.CanComeUp(day, settlement, null, underWay));
        settlement.AddResident(Person("Tor"));
        Assert.IsFalse(tor.CanComeUp(day, settlement, null, underWay), "nobody is sought who has come home");
    }

    // A rescue starts the person's first quest, which opens their workplace (ResidentQuests, BuildingGate). A second trade's workplace
    // (Gunnar's Dock) waits for the quest of his that introduces it.
    [Test]
    public void ARescue_OpensTheWorkplaceOfTheirFirstQuest_AndNoMore()
    {
        GameSession.StartNewGame();
        var player = new GameObject("Player");
        try
        {
            PlayerCrafting crafting = player.AddComponent<PlayerCrafting>();
            Assert.IsFalse(crafting.IsKnown(Building("Forge")));
            ResidentNpc.TeachWhatTheyKnow(Person("Bjarne"), player);
            Assert.IsFalse(crafting.IsKnown(Building("Forge")), "not before his first quest has begun");
            Assert.IsTrue(ResidentQuests.StartFirst(Person("Bjarne"), player, false));
            Assert.IsTrue(crafting.IsKnown(Building("Forge")));
            ResidentQuests.StartFirst(Person("Gunnar"), player, false);
            Assert.IsTrue(crafting.IsKnown(Building("TanningRack")), "his first quest's lodge");
            Assert.IsFalse(crafting.IsKnown(Building("Dock")), "the Dock waits for the quest that introduces it");
        }
        finally
        {
            Object.DestroyImmediate(player);
            GameSession.StartNewGame();
        }
    }
}
