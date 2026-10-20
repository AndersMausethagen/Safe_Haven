using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Edit Mode tests for the raid rules and the data that carries the player between Safe Haven and raids.</summary>
public sealed class RaidTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
    }

    // ----- Parameters -----

    [Test]
    public void Parameters_WithoutModifiers_UseTheRaidsBaseValues()
    {
        RaidDefinition raid = Create<RaidDefinition>();

        RaidParameters rules = RaidParameters.Calculate(raid, new RaidModifierDefinition[0]);

        // GDD §7.3: 12 minutes.
        Assert.AreEqual(720f, rules.Duration);
        Assert.AreEqual(5f, rules.ExtractionTime);
        Assert.AreEqual(1f, rules.LootMultiplier);
        Assert.AreEqual(1f, rules.EnemyHealthMultiplier);
        Assert.AreEqual(1f, rules.EnemyPowerMultiplier);
    }

    [Test]
    public void Parameters_AddFlatModifiersThenScaleByPercentOnes()
    {
        RaidDefinition raid = Create<RaidDefinition>();
        var effects = new[]
        {
            new RaidStatModifier(RaidStat.Duration, StatModifierOp.Flat, 120f),
            new RaidStatModifier(RaidStat.Duration, StatModifierOp.Percent, 0.25f),
            new RaidStatModifier(RaidStat.Duration, StatModifierOp.Percent, 0.25f),
        };

        RaidParameters rules = RaidParameters.Calculate(raid, effects);

        Assert.AreEqual((720f + 120f) * 1.5f, rules.Duration, 0.001f);
    }

    [Test]
    public void Parameters_AreClampedToTheirMinimums()
    {
        RaidDefinition raid = Create<RaidDefinition>();
        var effects = new[]
        {
            new RaidStatModifier(RaidStat.Duration, StatModifierOp.Flat, -10000f),
            new RaidStatModifier(RaidStat.ExtractionTime, StatModifierOp.Percent, -2f),
            new RaidStatModifier(RaidStat.LootQuantity, StatModifierOp.Flat, -5f),
            new RaidStatModifier(RaidStat.EnemyHealth, StatModifierOp.Flat, -5f),
        };

        RaidParameters rules = RaidParameters.Calculate(raid, effects);

        Assert.AreEqual(RaidParameters.MinimumDuration, rules.Duration);
        Assert.AreEqual(RaidParameters.MinimumExtractionTime, rules.ExtractionTime);
        Assert.AreEqual(0f, rules.LootMultiplier);
        Assert.AreEqual(RaidParameters.MinimumEnemyHealth, rules.EnemyHealthMultiplier);
    }

    [Test]
    public void Parameters_PortalSparkSecondsExtendTheDuration()
    {
        RaidDefinition raid = Create<RaidDefinition>();
        RaidModifierDefinition[] none = new RaidModifierDefinition[0];

        Assert.AreEqual(720f + 360f, RaidParameters.Calculate(raid, none, 360f).Duration, 0.001f);
        Assert.AreEqual(720f, RaidParameters.Calculate(raid, none, 0f).Duration, 0.001f);
    }

    [Test]
    public void SparkCost_IsOnePortalSparkPerSpark()
    {
        ResourceDefinition spark = SettlementSettings.Current.PortalSpark;
        Assert.IsNotNull(spark, "Resources/SettlementSettings needs its Portal Spark resource.");

        List<ResourceAmount> cost = RaidLauncher.SparkCost(2);

        Assert.AreEqual(1, cost.Count);
        Assert.AreEqual(spark, cost[0].Resource);
        Assert.AreEqual(2, cost[0].Amount);
        Assert.IsEmpty(RaidLauncher.SparkCost(0));
    }

    [Test]
    public void Parameters_CombineEveryPickedModifierDefinition()
    {
        RaidDefinition raid = Create<RaidDefinition>();
        RaidModifierDefinition hourglass = CreateModifier(new RaidStatModifier(RaidStat.Duration, StatModifierOp.Flat, 120f));
        RaidModifierDefinition sigil = CreateModifier(
            new RaidStatModifier(RaidStat.LootQuantity, StatModifierOp.Flat, 1f),
            new RaidStatModifier(RaidStat.EnemyHealth, StatModifierOp.Flat, 0.5f));

        RaidParameters rules = RaidParameters.Calculate(raid, new[] { hourglass, sigil, null });

        Assert.AreEqual(840f, rules.Duration);
        Assert.AreEqual(2f, rules.LootMultiplier);
        Assert.AreEqual(1.5f, rules.EnemyHealthMultiplier);
        Assert.AreEqual("+2:00 raid time", hourglass.DescribeEffects());
        Assert.AreEqual("+100% cache loot, +50% enemy health", sigil.DescribeEffects());
    }

    [TestCase(1, 1.5f, 0.4f, 2)]
    [TestCase(1, 1.5f, 0.6f, 1)]
    [TestCase(4, 1f, 0.99f, 4)]
    [TestCase(2, 1.5f, 0.99f, 3)]
    [TestCase(3, 0f, 0f, 0)]
    public void ScaleAmount_RoundsTheFractionByTheRoll(int amount, float multiplier, float roll, int expected)
    {
        Assert.AreEqual(expected, RaidParameters.ScaleAmount(amount, multiplier, roll));
    }

    [TestCase(600f, "10:00")]
    [TestCase(59.2f, "1:00")]
    [TestCase(9f, "0:09")]
    [TestCase(0f, "0:00")]
    public void FormatTime_ShowsMinutesAndSecondsRoundedUp(float seconds, string expected)
    {
        Assert.AreEqual(expected, RaidStats.FormatTime(seconds));
    }

    // ----- Session -----

    [Test]
    public void Session_EndsAsTimeExpired_OnTheTickThatRunsOutOfTime()
    {
        RaidSession session = CreateSession();

        Assert.IsFalse(session.Tick(719f));
        Assert.AreEqual(1f, session.TimeRemaining, 0.001f);
        Assert.IsTrue(session.Tick(5f));
        Assert.AreEqual(RaidOutcome.TimeExpired, session.Outcome);
        Assert.AreEqual(0f, session.TimeRemaining);
        Assert.IsFalse(session.Tick(1f), "A finished raid doesn't tick.");
    }

    [Test]
    public void Session_KeepsOnlyTheFirstOutcome()
    {
        RaidSession session = CreateSession();

        Assert.IsTrue(session.End(RaidOutcome.Extracted));
        Assert.IsFalse(session.End(RaidOutcome.Died));
        Assert.IsFalse(session.Tick(10000f));
        Assert.AreEqual(RaidOutcome.Extracted, session.Outcome);
    }

    // ----- Player snapshot -----

    [Test]
    public void Snapshot_RestoresSlotsResourcesAndKnowledgeExactly()
    {
        ItemDefinition potion = Create<ItemDefinition>();
        ItemDefinition ingot = Create<ItemDefinition>();
        ResourceDefinition wood = Create<ResourceDefinition>();
        ResourceDefinition ore = Create<ResourceDefinition>();
        RecipeDefinition recipe = Create<RecipeDefinition>();

        var bag = new ItemContainer(4, 2);
        bag.SetSlot(5, new ItemStack(potion, 3));
        bag.SetSlot(1, new ItemStack(ingot, 7));
        var storage = new ResourceStorage(20);
        storage.Add(wood, 15);
        storage.MarkDiscovered(ore);
        PlayerSnapshot snapshot = PlayerSnapshot.Capture(bag, storage, new[] { recipe });

        var restoredBag = new ItemContainer(4, 2);
        restoredBag.Add(ingot, 1);
        var restoredStorage = new ResourceStorage(10);
        restoredStorage.Add(ore, 4);
        List<ItemAmount> unplaced = snapshot.ApplyTo(restoredBag, restoredStorage, null);

        Assert.IsEmpty(unplaced);
        Assert.AreEqual(potion, restoredBag.GetSlot(5).Item);
        Assert.AreEqual(3, restoredBag.GetSlot(5).Count);
        Assert.AreEqual(7, restoredBag.GetSlot(1).Count);
        Assert.AreEqual(7, restoredBag.Count(ingot), "What the bag held before is replaced, not added to.");
        Assert.AreEqual(15, restoredStorage.GetAmount(wood), "Restoring ignores capacity, so nothing carried is lost.");
        Assert.AreEqual(0, restoredStorage.GetAmount(ore));
        Assert.IsTrue(restoredStorage.IsDiscovered(ore));
        CollectionAssert.AreEqual(new[] { recipe }, snapshot.KnownRecipes);
    }

    [Test]
    public void Snapshot_PlacesStacksFromMissingSlotsWhereverTheyFit()
    {
        ItemDefinition potion = Create<ItemDefinition>();
        var bigBag = new ItemContainer(4, 4);
        bigBag.SetSlot(15, new ItemStack(potion, 2));
        PlayerSnapshot snapshot = PlayerSnapshot.Capture(bigBag, null, null);

        var smallBag = new ItemContainer(2, 1);
        List<ItemAmount> unplaced = snapshot.ApplyTo(smallBag, null, null);

        Assert.IsEmpty(unplaced);
        Assert.AreEqual(2, smallBag.Count(potion));
    }

    [Test]
    public void Snapshot_WithoutBelongings_KeepsOnlyKnowledge()
    {
        ItemDefinition potion = Create<ItemDefinition>();
        ResourceDefinition wood = Create<ResourceDefinition>();
        RecipeDefinition recipe = Create<RecipeDefinition>();
        var bag = new ItemContainer(2, 2);
        bag.Add(potion, 2);
        var storage = new ResourceStorage();
        storage.Add(wood, 3);

        PlayerSnapshot stripped = PlayerSnapshot.Capture(bag, storage, new[] { recipe }).WithoutBelongings();

        Assert.IsFalse(stripped.HasBelongings);
        CollectionAssert.Contains(stripped.Discovered, wood);
        CollectionAssert.Contains(stripped.KnownRecipes, recipe);
    }

    // ----- Report -----

    [Test]
    public void Report_ListsLootAndWhatWasUsedUp()
    {
        ItemDefinition potion = Create<ItemDefinition>();
        ItemDefinition ingot = Create<ItemDefinition>();
        ResourceDefinition wood = Create<ResourceDefinition>();
        var bag = new ItemContainer(4, 2);
        bag.Add(potion, 2);
        PlayerSnapshot before = PlayerSnapshot.Capture(bag, new ResourceStorage(), null);
        bag.Remove(potion, 1);
        bag.Add(ingot, 3);
        var storage = new ResourceStorage();
        storage.Add(wood, 5);
        PlayerSnapshot after = PlayerSnapshot.Capture(bag, storage, null);
        RaidSession session = CreateSession();
        session.End(RaidOutcome.Extracted);

        RaidReport report = RaidReport.Create(session, before, after);

        Assert.IsTrue(report.Extracted);
        Assert.AreEqual(1, report.ItemsGained.Count);
        Assert.AreEqual(ingot, report.ItemsGained[0].Item);
        Assert.AreEqual(3, report.ItemsGained[0].Amount);
        Assert.AreEqual(1, report.ItemsLost.Count);
        Assert.AreEqual(potion, report.ItemsLost[0].Item);
        Assert.AreEqual(1, report.ItemsLost[0].Amount);
        Assert.AreEqual(5, report.ResourcesGained[0].Amount);
    }

    [Test]
    public void Report_AfterAFailure_ListsEverythingCarriedAsLost()
    {
        ItemDefinition axe = Create<ItemDefinition>();
        var bag = new ItemContainer(2, 2);
        bag.Add(axe, 1);
        PlayerSnapshot loadout = PlayerSnapshot.Capture(bag, null, null);
        RaidSession session = CreateSession();
        session.End(RaidOutcome.Died);

        RaidReport report = RaidReport.Create(session, loadout, loadout.WithoutBelongings());

        Assert.IsFalse(report.Extracted);
        Assert.IsEmpty(report.ItemsGained);
        Assert.AreEqual(axe, report.ItemsLost[0].Item);
    }

    // ----- Stash transfers -----

    [Test]
    public void TransferTo_MovesWhatFitsAndLeavesTheRest()
    {
        ItemDefinition ingot = Create<ItemDefinition>();
        ItemDefinition other = Create<ItemDefinition>();
        var bag = new ItemContainer(2, 1);
        bag.SetSlot(0, new ItemStack(ingot, 10));
        var stash = new ItemContainer(1, 1);
        stash.SetSlot(0, new ItemStack(ingot, 95));

        Assert.AreEqual(4, bag.TransferTo(0, stash));
        Assert.AreEqual(6, bag.GetSlot(0).Count);
        Assert.AreEqual(99, stash.GetSlot(0).Count);

        var full = new ItemContainer(1, 1);
        full.Add(other, 1);
        Assert.AreEqual(0, bag.TransferTo(0, full));
        Assert.AreEqual(6, bag.GetSlot(0).Count);
    }

    [Test]
    public void TransferAllTo_MovesResourcesUpToTheTargetsCapacity()
    {
        ResourceDefinition wood = Create<ResourceDefinition>();
        ResourceDefinition stone = Create<ResourceDefinition>();
        var stash = new ResourceStorage(100);
        stash.Add(wood, 15);
        stash.Add(stone, 3);
        var carried = new ResourceStorage(10);

        int moved = stash.TransferAllTo(carried);

        Assert.AreEqual(13, moved);
        Assert.AreEqual(10, carried.GetAmount(wood));
        Assert.AreEqual(3, carried.GetAmount(stone));
        Assert.AreEqual(5, stash.GetAmount(wood));
        Assert.AreEqual(0, stash.GetAmount(stone));
    }

    // ----- Helpers -----

    private T Create<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = typeof(T).Name + created.Count;
        created.Add(asset);
        return asset;
    }

    private RaidModifierDefinition CreateModifier(params RaidStatModifier[] effects)
    {
        RaidModifierDefinition modifier = Create<RaidModifierDefinition>();
        var data = new SerializedObject(modifier);
        SerializedProperty list = data.FindProperty("effects");
        list.arraySize = effects.Length;
        for (int i = 0; i < effects.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("stat").intValue = (int)effects[i].Stat;
            entry.FindPropertyRelative("op").intValue = (int)effects[i].Op;
            entry.FindPropertyRelative("value").floatValue = effects[i].Value;
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        return modifier;
    }

    private RaidSession CreateSession()
    {
        RaidDefinition raid = Create<RaidDefinition>();
        RaidModifierDefinition[] none = new RaidModifierDefinition[0];
        return new RaidSession(raid, none, RaidParameters.Calculate(raid, none), null, "SampleScene");
    }
}
