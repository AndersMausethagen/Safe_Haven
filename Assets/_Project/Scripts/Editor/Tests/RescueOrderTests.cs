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
        Assert.IsFalse(tor.CanComeUp(day, settlement, null), "Bjarne isn't home yet");
        settlement.AddResident(Person("Bjarne"));
        Assert.IsTrue(tor.CanComeUp(day, settlement, null));
        settlement.AddResident(Person("Tor"));
        Assert.IsFalse(tor.CanComeUp(day, settlement, null), "nobody is sought who has come home");
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
}
