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
    public void TakingDown_TakesBackWhatAQuestUnderWayCounted_AndNothingElse()
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

        // Old pieces taken down before anything counted take nothing from it (a wall ring redrawn didn't eat a later wall quest).
        log.Handle(new GameEvent(GameEventKind.TakenDown, building, 5));
        Assert.AreEqual(0, log.Get(quest).Counts[step]);
        log.Handle(new GameEvent(GameEventKind.Built, building, 1));
        Assert.AreEqual(1, log.Get(quest).Counts[step], "a new piece counts");

        log.Handle(new GameEvent(GameEventKind.TakenDown, building, 1));
        Assert.AreEqual(0, log.Get(quest).Counts[step], "taken down: it no longer counts");
        log.Handle(new GameEvent(GameEventKind.Built, building, 1));
        Assert.AreEqual(1, log.Get(quest).Counts[step], "the same piece put up again is one, not two");
    }

    [Test]
    public void ALoad_KeepsTheSavedHealth_AndABrokenThingStaysBroken_WhenItsMostHealthGrows()
    {
        // Iron Bands and the Hag's Knot raise a building's most health (Construction); a load healed it, and a broken one was whole again
        // (the review of 2026-11-21).
        var body = new GameObject("Banded");
        created.Add(body);
        Combatant fighter = body.AddComponent<Combatant>();
        Damageable health = body.AddComponent<Damageable>();
        Invoke(fighter, "Awake");
        fighter.Stats.SetBase(StatType.MaxHealth, 100f);
        Invoke(health, "Awake");
        Invoke(health, "OnEnable");
        float most = health.MaxHealth;
        Assert.Greater(most, 10f);

        health.RestoreState(JsonUtility.ToJson(new HealthState { health = most * 1.2f }));
        fighter.Stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, 0.5f, this));
        Assert.AreEqual(most * 1.2f, health.CurrentHealth, 0.01f, "the saved health, not a share of the base most it was clamped to");

        fighter.Stats.RemoveModifiersFrom(this);
        health.HoldFloor(this, 1f);
        health.ApplyDamage(new DamageInfo(null, null, most * 10f, DamageType.Holy, false, Vector3.zero, Vector3.up));
        Assert.IsTrue(health.IsBroken, "worn to its floor");
        // The restore's frame is over once a blow has landed in a later one; move the frame on by hand for the test.
        typeof(Damageable).GetField("restoredFrame", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(health, -1);
        fighter.Stats.AddModifier(new StatModifier(StatType.MaxHealth, StatModifierOp.Percent, 0.5f, this));
        Assert.IsTrue(health.IsBroken, "more most health doesn't lift it off its floor");
    }

    [System.Serializable]
    private struct HealthState
    {
        public float health;
    }

    private static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(target, null);
}
