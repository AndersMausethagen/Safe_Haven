using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Edit Mode tests for hunger and thirst (GDD §11): the meters, the settings' helper numbers and eating.</summary>
public sealed class SurvivalTests
{
    // The Haven's lakes and rivers gave no waterskin for a week after its one Drinking Spot was removed (found 2026-11-14).
    [Test]
    public void OpenWater_HandsOutAWaterContainer()
    {
        SurvivalSettings settings = SurvivalSettings.Current;
        Assert.NotNull(settings.OpenWaterFillItem, "Survival Settings › Open Water Fill Item");
        Assert.IsTrue(settings.OpenWaterFillItem.HoldsWater, "it holds water");
        Assert.Greater(settings.OpenWaterFillUpTo, 0);
    }

    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in created)
        {
            Object.DestroyImmediate(thing);
        }

        created.Clear();
    }

    [Test]
    public void Meter_StartsAtTheGivenShareOfItsMaximum()
    {
        var meter = new NeedMeter(100f, 0.7f);

        Assert.AreEqual(70f, meter.Current, 0.001f);
        Assert.AreEqual(0.7f, meter.Fraction, 0.001f);
    }

    [Test]
    public void Meter_DrainStopsAtZero()
    {
        var meter = new NeedMeter(100f, 0.5f);

        Assert.IsTrue(meter.Drain(80f));
        Assert.AreEqual(0f, meter.Current);
        Assert.IsTrue(meter.IsEmpty);
        Assert.IsFalse(meter.Drain(1f), "An empty meter can't drain further.");
    }

    [Test]
    public void Meter_RestoreStopsAtTheMaximumAndReportsWhatItGained()
    {
        var meter = new NeedMeter(100f, 0.9f);

        Assert.AreEqual(10f, meter.Restore(25f), 0.001f);
        Assert.IsTrue(meter.IsFull);
        Assert.AreEqual(0f, meter.Restore(5f));
    }

    [Test]
    public void Meter_IgnoresNegativeAmountsAndClampsSetValues()
    {
        var meter = new NeedMeter(100f, 0.5f);

        Assert.IsFalse(meter.Drain(-5f));
        Assert.AreEqual(0f, meter.Restore(-5f));
        meter.Set(500f);
        Assert.AreEqual(100f, meter.Current);
        meter.Set(-3f);
        Assert.AreEqual(0f, meter.Current);
    }

    [Test]
    public void Settings_MinutesToEmptyIncludeTheRaidMultiplier()
    {
        Assert.AreEqual(40f, SurvivalSettings.MinutesToEmpty(100f, 2.5f), 0.001f);
        Assert.AreEqual(16f, SurvivalSettings.MinutesToEmpty(100f, 2.5f, 2.5f), 0.001f);
        Assert.IsTrue(float.IsPositiveInfinity(SurvivalSettings.MinutesToEmpty(100f, 0f)));
    }

    [Test]
    public void Eating_RestoresBothMeters()
    {
        PlayerNeeds needs = CreateNeeds();
        needs.Restore(20f, 30f);

        Assert.IsTrue(needs.TryConsume("Stewed Berries", 25f, 10f));

        Assert.AreEqual(45f, needs.Hunger.Current, 0.001f);
        Assert.AreEqual(40f, needs.Thirst.Current, 0.001f);
    }

    [Test]
    public void Eating_IsRefusedWhenItWouldNotHelp()
    {
        PlayerNeeds needs = CreateNeeds();
        needs.Restore(needs.Hunger.Max, 10f);

        Assert.IsFalse(needs.TryConsume("Berries", 10f, 0f), "Food isn't wasted on a full stomach.");
        Assert.IsTrue(needs.TryConsume("Water", 0f, 10f), "Drinking still helps while thirsty.");
    }

    // Playtest 2026-11-05: one waterskin holds up to 100 water, drunk from and refilled at any water.
    [Test]
    public void Waterskin_HoldsAHundredWater_EachOneItsOwn()
    {
        ItemDefinition skin = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Waterskin.asset");
        Assert.IsTrue(skin.HoldsWater);
        Assert.AreEqual(100f, skin.WaterCapacity);
        Assert.IsFalse(skin.Stackable, "each skin keeps its own level");
        Assert.IsTrue(skin.IsUsable, "right-click drinks from it");

        var full = new ItemStack(skin, 1);
        Assert.AreEqual(100f, full.Water, 0.01f, "a new skin comes full");
        ItemStack sipped = full.WithWater(40f);
        Assert.AreEqual(40f, sipped.Water, 0.01f);
        var reloaded = new ItemStack(skin, 1, sipped.Durability);
        Assert.AreEqual(40f, reloaded.Water, 0.01f, "the level is kept the way saves and moves keep a stack");
    }

    [Test]
    public void WaterSources_RefillEverySkinInTheBag()
    {
        ItemDefinition skin = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Waterskin.asset");
        var bag = new ItemContainer(4, 1);
        bag.AddStack(new ItemStack(skin, 1).WithWater(10f));
        bag.AddStack(new ItemStack(skin, 1).WithWater(100f));
        ItemDefinition sticks = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(UnityEditor.AssetDatabase.FindAssets("Sticks t:ItemDefinition")[0]));
        bag.AddStack(new ItemStack(sticks, 3));

        Assert.AreEqual(1, WaterSource.Refill(bag), "only the one that wasn't full");
        Assert.AreEqual(100f, bag.GetSlot(0).Water, 0.01f);
        Assert.AreEqual(100f, bag.GetSlot(1).Water, 0.01f);
        Assert.AreEqual(3, bag.Count(sticks), "other things are left alone");
    }

    private PlayerNeeds CreateNeeds()
    {
        var player = new GameObject("Player");
        created.Add(player);
        return player.AddComponent<PlayerNeeds>();
    }
}
