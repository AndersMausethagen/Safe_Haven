using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Residents' work and carrying (WorkerSettings, WorkerCarry, WorkerProduction; the user, 2026-11-21): a station runs on its own and
/// faster with its worker at it, a worker fetches only what their hands (Strength, gear, a cart, a horse) carry, finished work goes back
/// on the trip, and what was fetched and hauled is saved.
/// </summary>
public sealed class WorkerCarryTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in made)
        {
            Object.DestroyImmediate(thing);
        }

        made.Clear();
    }

    private ItemDefinition Item(string name, float weight)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        item.name = name;
        var so = new SerializedObject(item);
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("id").stringValue = name;
        so.ApplyModifiedPropertiesWithoutUndo();
        made.Add(item);
        return item;
    }

    private WorkerSettings Settings()
    {
        var settings = ScriptableObject.CreateInstance<WorkerSettings>();
        made.Add(settings);
        return settings;
    }

    // ----- Strength and limits -----

    [Test]
    public void ByHand_StartsSmall_AndGrowsWithHaulingRaidsAndGear()
    {
        WorkerSettings settings = Settings();
        var record = new ResidentRecord(null);
        CarryLimits fresh = WorkerCarry.ByHand(record, settings);
        Assert.AreEqual(settings.CarryWeight, fresh.Weight, "a new worker carries the base weight");
        Assert.AreEqual(settings.CarrySlots, fresh.Slots);
        Assert.LessOrEqual(fresh.Weight, 30f, "the user's 20-30");
        Assert.LessOrEqual(fresh.Slots, 2, "the user's 1-2 slots");

        record.HaulDone = 1000f;
        record.RaidsAlong = 4;
        CarryLimits trained = WorkerCarry.ByHand(record, settings);
        Assert.Greater(trained.Weight, fresh.Weight, "hauling and raids make them stronger");
        Assert.Greater(trained.Slots, fresh.Slots, "and at Strength 2 they carry another kind");

        ItemDefinition frame = Item("Carrying Frame", 4f);
        var so = new SerializedObject(frame);
        so.FindProperty("workerCarryWeight").floatValue = 20f;
        so.FindProperty("workerCarrySlots").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        Assert.IsTrue(frame.IsWorkerGear);
        record.WorkGear = frame;
        CarryLimits geared = WorkerCarry.ByHand(record, settings);
        Assert.AreEqual(trained.Weight + 20f, geared.Weight, 1e-3f);
        Assert.AreEqual(trained.Slots + 1, geared.Slots);
    }

    [Test]
    public void ACartAndAHorse_CarryMore()
    {
        WorkerSettings settings = Settings();
        var hands = new CarryLimits(25f, 1);
        CarryLimits cart = WorkerCarry.WithHelp(hands, 200f, false, settings);
        Assert.AreEqual(200f, cart.Weight);
        Assert.AreEqual(settings.CartSlots, cart.Slots);
        CarryLimits horse = WorkerCarry.WithHelp(hands, 0f, true, settings);
        Assert.AreEqual(25f * settings.HorseCarry, horse.Weight, 1e-3f);
        Assert.AreEqual(2, horse.Slots);
        Assert.AreEqual(hands.Weight, WorkerCarry.WithHelp(hands, 0f, false, settings).Weight, "no help, no more");
    }

    // ----- What a trip carries -----

    [Test]
    public void PlanFetch_BringsAsManyCraftsAsFit_WithinWeightAndKinds()
    {
        ItemDefinition wood = Item("Wood", 1f);
        ItemDefinition ore = Item("Ore", 2f);
        var costs = new List<ItemAmount> { new ItemAmount(wood, 4) };
        // 25 weight, 1 kind: 6 crafts of 4 wood would be 24 weight; 5 asked for fit (20 weight).
        List<ItemAmount> load = WorkerCarry.PlanFetch(costs, 5, _ => 0, _ => 100, new CarryLimits(25f, 1));
        Assert.AreEqual(1, load.Count);
        Assert.AreEqual(20, load[0].Amount, "five crafts' worth");
        // Only 2 crafts fit in 10 weight.
        load = WorkerCarry.PlanFetch(costs, 5, _ => 0, _ => 100, new CarryLimits(10f, 1));
        Assert.AreEqual(8, load[0].Amount);
        // Two kinds with one slot: not one craft fits, so a start on the first kind only.
        var two = new List<ItemAmount> { new ItemAmount(ore, 2), new ItemAmount(wood, 1) };
        load = WorkerCarry.PlanFetch(two, 3, _ => 0, _ => 100, new CarryLimits(25f, 1));
        Assert.AreEqual(1, load.Count, "one kind at a time by hand");
        Assert.AreSame(ore, load[0].Item);
        // Two slots: three crafts of both.
        load = WorkerCarry.PlanFetch(two, 3, _ => 0, _ => 100, new CarryLimits(25f, 2));
        Assert.AreEqual(2, load.Count);
        Assert.AreEqual(15f, WorkerCarry.WeightOf(load), 1e-3f);
    }

    [Test]
    public void PlanFetch_CountsWhatWasFetched_AndNeverMoreThanTheStashHolds()
    {
        ItemDefinition wood = Item("Wood", 1f);
        var costs = new List<ItemAmount> { new ItemAmount(wood, 4) };
        List<ItemAmount> load = WorkerCarry.PlanFetch(costs, 2, _ => 3, _ => 100, new CarryLimits(25f, 1));
        Assert.AreEqual(5, load[0].Amount, "two crafts want 8; 3 lie fetched already");
        load = WorkerCarry.PlanFetch(costs, 5, _ => 0, _ => 6, new CarryLimits(25f, 1));
        Assert.AreEqual(4, load[0].Amount, "one craft when the stash holds 6");
        Assert.AreEqual(0, WorkerCarry.PlanFetch(costs, 1, _ => 4, _ => 100, new CarryLimits(25f, 1)).Count, "nothing wanted");
        Assert.AreEqual(12, WorkerCarry.CanTake(wood, 30, new CarryLimits(12.5f, 1)));
    }

    [Test]
    public void AwayTrips_TakeTheWalkThereAndBack()
    {
        WorkerSettings settings = Settings();
        float near = WorkerCarry.AwayTripSeconds(10f, settings);
        float far = WorkerCarry.AwayTripSeconds(40f, settings);
        Assert.Greater(far, near, "a chest further off means fewer trips");
        Assert.AreEqual(40f * 2f / settings.AwayWalkSpeed + settings.TripHandling, far, 1e-3f);
    }

    // ----- The station -----

    [Test]
    public void AStation_RunsOnItsOwn_AndFasterWithItsWorkerAtIt()
    {
        var queue = new StationQueue("bench", CraftingStationType.Anvil, 10, 5) { SpeedMultiplier = 0.6f, WorkerSpeed = 1f };
        Assert.AreEqual(0.6f, queue.Speed, 1e-4f, "nobody there");
        queue.WorkerHere = true;
        Assert.AreEqual(1.6f, queue.Speed, 1e-4f, "work adds up");

        var away = new WorkshopState();
        away.Add(queue);
        queue.WorkerHere = false;
        away.AwayFromHome = true;
        away.Tick(0.01f, null);
        Assert.IsTrue(queue.WorkerHere, "away, a worker counts as at their station");
    }

    [Test]
    public void Supplies_PayOrdersOnlyWhenEnough_AndAreSaved()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ItemDefinition wood = null;
        foreach (ItemDefinition item in content.Items)
        {
            if (item != null && item.Weight > 0f && item.Stackable)
            {
                wood = item;
                break;
            }
        }

        Assume.That(wood != null, "a stackable item in the content");
        var queue = new StationQueue("bench", CraftingStationType.Anvil, 10, 5);
        queue.AddSupply(wood, 5);
        Assert.IsFalse(queue.TakeSupply(wood, 6), "too few: nothing taken");
        Assert.AreEqual(5, queue.Supplied(wood));
        Assert.IsTrue(queue.TakeSupply(wood, 2));
        Assert.AreEqual(3, queue.Supplied(wood));

        var settlement = new SettlementState();
        settlement.Workshops.Add(queue);
        SettlementState back = SaveMapper.SettlementFrom(
            JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(settlement))), content);
        Assert.AreEqual(3, back.Workshops.Find("bench").Supplied(wood), "what was fetched survives a save");
    }

    [Test]
    public void AWorkersCraft_NeverStartsShort_AndPaysFromWhatWasFetchedFirst()
    {
        ItemDefinition wood = Item("Wood", 1f);
        ItemDefinition plank = Item("Plank", 0.5f);
        var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
        made.Add(recipe);
        var rso = new SerializedObject(recipe);
        rso.FindProperty("output").objectReferenceValue = plank;
        rso.FindProperty("outputAmount").intValue = 2;
        rso.FindProperty("craftSeconds").floatValue = 5f;
        SerializedProperty costs = rso.FindProperty("itemCost");
        costs.arraySize = 1;
        costs.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = wood;
        costs.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 4;
        rso.ApplyModifiedPropertiesWithoutUndo();
        var job = ScriptableObject.CreateInstance<JobDefinition>();
        made.Add(job);
        var jso = new SerializedObject(job);
        SerializedProperty orders = jso.FindProperty("workOrders");
        orders.arraySize = 1;
        orders.GetArrayElementAtIndex(0).FindPropertyRelative("recipe").objectReferenceValue = recipe;
        orders.GetArrayElementAtIndex(0).FindPropertyRelative("stockTarget").intValue = 100;
        orders.GetArrayElementAtIndex(0).FindPropertyRelative("keepInput").intValue = 0;
        jso.ApplyModifiedPropertiesWithoutUndo();

        var stash = new StashStorage();
        var queue = new StationQueue("lodge", CraftingStationType.Sawbench, 10, 5);
        Assume.That(stash.Items.Add(wood, 3) == 3, "the stash takes items without a chest in a test");
        Assert.IsFalse(WorkerProduction.StartNext(job, queue, stash), "3 of 4: no craft");
        queue.AddSupply(wood, 2);
        Assert.IsTrue(WorkerProduction.StartNext(job, queue, stash), "2 fetched and 3 stored: one craft");
        Assert.AreEqual(0, queue.Supplied(wood), "what was fetched is used first");
        Assert.AreEqual(1, stash.Items.Count(wood), "the rest from the stash");
        Assert.IsFalse(WorkerProduction.StartNext(job, queue, stash), "1 left: no free craft");
        Assert.AreEqual(1, queue.QueuedCount);
    }

    [Test]
    public void HaulingAndGear_AreSavedWithTheResident()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        ResidentDefinition someone = null;
        foreach (ResidentDefinition resident in content.Residents)
        {
            someone = resident;
            break;
        }

        ItemDefinition gear = null;
        foreach (ItemDefinition item in content.Items)
        {
            if (item != null)
            {
                gear = item;
                break;
            }
        }

        Assume.That(someone != null && gear != null);
        var settlement = new SettlementState();
        settlement.AddResident(someone);
        settlement.Find(someone).HaulDone = 812.5f;
        settlement.Find(someone).WorkGear = gear;
        SettlementState back = SaveMapper.SettlementFrom(
            JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(settlement))), content);
        Assert.AreEqual(812.5f, back.Find(someone).HaulDone, 1e-3f);
        Assert.AreSame(gear, back.Find(someone).WorkGear);
    }
}
