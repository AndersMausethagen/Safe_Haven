using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Taking a building down gives back what it cost (BuildingDemolition, the user 2026-11-20), and refuses when it would lose something.</summary>
public sealed class BuildingDemolitionTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in created)
        {
            if (thing != null)
            {
                Object.DestroyImmediate(thing);
            }
        }

        created.Clear();
        GameSession.StartNewGame();
    }

    private static BuildingDefinition Building(string name) =>
        AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/" + name + ".asset");

    // A placed building of the kind, made inactive so nothing of it runs.
    private BuildingObject Placed(BuildingDefinition definition)
    {
        var go = new GameObject(definition.name);
        go.SetActive(false);
        created.Add(go);
        BuildingObject building = go.AddComponent<BuildingObject>();
        var data = new SerializedObject(building);
        data.FindProperty("definition").objectReferenceValue = definition;
        data.ApplyModifiedPropertiesWithoutUndo();
        return building;
    }

    [Test]
    public void TheRefundIsWhatTheBuildingCost()
    {
        int checkedBuildings = 0;
        foreach (string name in new[] { "StorageShed", "SmallChest", "ConstructionTable" })
        {
            BuildingDefinition definition = Building(name);
            Assert.IsNotNull(definition, name);
            var resources = new List<ResourceAmount>();
            var items = new List<ItemAmount>();
            BuildingDemolition.Refund(Placed(definition), resources, items);
            foreach (ItemAmount cost in definition.ConstructionItems.Where(c => c.Item != null && c.Amount > 0))
            {
                Assert.AreEqual(definition.ConstructionItems.Where(c => c.Item == cost.Item).Sum(c => c.Amount),
                    items.Where(i => i.Item == cost.Item).Sum(i => i.Amount), $"{name}: {cost.Item.name} comes back");
            }

            Assert.AreEqual(definition.ConstructionItems.Where(c => c.Item != null).Sum(c => c.Amount), items.Sum(i => i.Amount), $"{name}: nothing extra");
            checkedBuildings++;
        }

        Assert.AreEqual(3, checkedBuildings);
    }

    [Test]
    public void ADamagedBuilding_KeepsBackWhatMendingItWouldCost()
    {
        // Taking down and putting up again was a free repair (the review of 2026-11-21): what comes back plus what mending the damage
        // costs is what it cost.
        BuildingDefinition shed = Building("StorageShed");
        Assert.Greater(BuildingRepairs.CurrentShare, 0f, "mending costs something");
        var resources = new List<ResourceAmount>();
        var items = new List<ItemAmount>();
        BuildingDemolition.Refund(shed, 0.5f, 1, false, resources, items);
        int kept = 0;
        foreach (ItemAmount cost in shed.ConstructionItems.Where(c => c.Item != null && c.Amount > 0))
        {
            int back = items.Where(i => i.Item == cost.Item).Sum(i => i.Amount);
            int mend = BuildingRepairs.Share(cost.Amount, 0.5f, BuildingRepairs.CurrentShare);
            Assert.AreEqual(cost.Amount, back + mend, $"{cost.Item.name}: back and mending make up the cost");
            kept += mend;
        }

        Assert.Greater(kept, 0, "some of it is kept back");
        BuildingDemolition.Refund(shed, 0f, 1, false, resources, items);
        Assert.AreEqual(shed.ConstructionItems.Where(c => c.Item != null).Sum(c => c.Amount), items.Sum(i => i.Amount), "unhurt: all of it");
    }

    [Test]
    public void WithFreeCosts_NothingComesBack()
    {
        BuildingDefinition shed = Building("StorageShed");
        var resources = new List<ResourceAmount>();
        var items = new List<ItemAmount>();
        try
        {
            Cheats.SetFreeCosts(true);
            BuildingDemolition.Refund(shed, 0f, 1, false, resources, items);
            Assert.IsEmpty(items);
            Assert.IsEmpty(resources);
        }
        finally
        {
            Cheats.SetFreeCosts(false);
        }

        BuildingDemolition.Refund(shed, 0f, 1, false, resources, items);
        Assert.IsNotEmpty(items, "paid for: it comes back");
    }

    [Test]
    public void PuttingUpAgainWhatWasTakenDown_IsNotCountedTwice_AndIsSaved()
    {
        // Build, take down, build again counted twice towards "build N" (the review of 2026-11-21).
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        QuestDefinition quest = content.Quests.First(q => q != null && q.Objectives.Any(o => o.Type == ObjectiveType.BuildStructure && o.Building != null && o.Amount >= 3));
        int step = quest.Objectives.ToList().FindIndex(o => o.Type == ObjectiveType.BuildStructure && o.Building != null && o.Amount >= 3);
        BuildingDefinition building = quest.Objectives[step].Building;
        // Every other step done, so this one is open whatever the order.
        int[] counts = quest.Objectives.Select(o => o.Amount).ToArray();
        counts[step] = 0;
        var log = new QuestLog();
        log.Restore(quest, QuestStatus.Active, counts);

        log.Handle(new GameEvent(GameEventKind.Built, building, 1));
        Assert.AreEqual(1, log.Get(quest).Counts[step]);
        log.Handle(new GameEvent(GameEventKind.TakenDown, building, 1));
        log.Handle(new GameEvent(GameEventKind.Built, building, 1));
        Assert.AreEqual(1, log.Get(quest).Counts[step], "the same piece put up again");

        log.Handle(new GameEvent(GameEventKind.TakenDown, building, 1));
        SaveData data = SaveMapper.ToSaveData(null, new StashStorage(), "SafeHaven", null, System.DateTime.UtcNow, null, log, new SettlementState());
        data = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));
        QuestLog loaded = SaveMapper.FromSaveData(data, content).Quests;
        Assert.AreEqual(1, loaded.Get(quest).Counts[step]);
        loaded.Handle(new GameEvent(GameEventKind.Built, building, 2));
        Assert.AreEqual(2, loaded.Get(quest).Counts[step], "a line of two after a load: one again, one new");
    }
}
