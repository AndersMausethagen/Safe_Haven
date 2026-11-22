using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The fields (Fields, FarmSettings; the Keeper's view, phase 5): a crop grows at its pace while watered and slower dry (never dead),
/// the soil dries only while something grows, rain and a Waterskin water it, a Farmer at home keeps the plots watered while the player
/// is away, and the water is saved with the plot.
/// </summary>
public sealed class FarmingTests
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

    private RecipeDefinition Crop(float seconds)
    {
        var seed = ScriptableObject.CreateInstance<ItemDefinition>();
        seed.name = "Seed";
        made.Add(seed);
        var recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
        made.Add(recipe);
        var so = new SerializedObject(recipe);
        so.FindProperty("output").objectReferenceValue = seed;
        so.FindProperty("outputAmount").intValue = 3;
        so.FindProperty("craftSeconds").floatValue = seconds;
        so.ApplyModifiedPropertiesWithoutUndo();
        return recipe;
    }

    [Test]
    public void ACrop_GrowsAtItsPaceWatered_AndSlowerDry_AndTheSoilDriesOnlyWhileItGrows()
    {
        FarmSettings settings = FarmSettings.Current;
        Assume.That(settings.DrySpeed > 0f && settings.DrySpeed < 1f);
        var plot = new StationQueue("plot", CraftingStationType.Farm, 10, 3);
        Assert.AreEqual(0f, plot.Water, "a new plot is dry");
        Fields.Fill(plot, settings);
        Assert.AreEqual(settings.WateredSeconds, plot.Water, 1e-3f);
        plot.Tick(30f, null);
        Assert.AreEqual(settings.WateredSeconds, plot.Water, 1e-3f, "empty, it keeps its water");

        plot.Enqueue(Crop(1000f));
        Assert.AreEqual(1f, plot.Speed, 1e-3f, "watered: its own pace");
        plot.Tick(10f, null);
        Assert.AreEqual(settings.WateredSeconds - 10f, plot.Water, 1e-3f, "growing, it drinks");
        Assert.AreEqual(10f, plot.Progress, 1e-3f);

        plot.Water = 0f;
        Assert.AreEqual(settings.DrySpeed, plot.Speed, 1e-3f, "dry: slower");
        plot.Tick(10f, null);
        Assert.AreEqual(10f + 10f * settings.DrySpeed, plot.Progress, 1e-3f, "slower, never stopped");

        var bench = new StationQueue("bench", CraftingStationType.Sawbench, 10, 3);
        Assert.AreEqual(1f, bench.Speed, 1e-3f, "only fields need water");
    }

    [Test]
    public void RainAndAWaterskin_WaterAPlot_AndAWaterskinIsSpent()
    {
        FarmSettings settings = FarmSettings.Current;
        var plot = new StationQueue("plot", CraftingStationType.Farm, 10, 3);
        plot.Enqueue(Crop(1000f));
        var workshops = new WorkshopState();
        workshops.Add(plot);
        Assert.AreEqual(0, Fields.Rain(workshops, settings.RainWaters * 0.5f, settings), "a drizzle waters nothing");
        Assert.AreEqual(1, Fields.Rain(workshops, settings.RainWaters, settings));
        Assert.AreEqual(settings.WateredSeconds, plot.Water, 1e-3f);

        var skin = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Waterskin.asset");
        Assert.IsNotNull(skin, "the Waterskin moved: update the path");
        Assert.IsTrue(skin.HoldsWater);
        var bag = new ItemContainer(4, 1);
        bag.Add(skin, 1);
        Assert.IsFalse(Fields.TryWaterFrom(bag, plot, settings, out string why), "a watered plot needs nothing");
        Assert.IsNull(why);

        plot.Water = 0f;
        Assert.IsTrue(Fields.TryWaterFrom(bag, plot, settings, out _));
        Assert.AreEqual(1f - settings.WaterPerPlot, bag.GetSlot(0).Durability, 1e-3f, "part of the skin is poured out");
        Assert.AreEqual(settings.WateredSeconds, plot.Water, 1e-3f);

        bag.SetDurability(0, 0f);
        plot.Water = 0f;
        Assert.IsFalse(Fields.TryWaterFrom(bag, plot, settings, out why), "an empty skin waters nothing");
        Assert.IsNotNull(why, "and says where water is");
    }

    [Test]
    public void AwayAFarmerAtHome_KeepsThePlotsWatered_ButNotTheOneAlong()
    {
        FarmSettings settings = FarmSettings.Current;
        var farmer = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Farmer.asset");
        Assert.IsNotNull(farmer, "the Farmer moved: update the path");
        ResidentDefinition someone = null;
        foreach (ResidentDefinition resident in Resources.Load<ContentDatabase>(ContentDatabase.ResourceName).Residents)
        {
            foreach (JobDefinition job in resident.Jobs)
            {
                someone = job == farmer ? resident : someone;
            }
        }

        Assert.IsNotNull(someone, "somebody can be the Farmer");
        var home = new SettlementState();
        home.AddResident(someone, farmer);
        StationQueue plot = home.Workshops.GetOrCreate("plot", CraftingStationType.Farm, 10, 3);
        plot.Enqueue(Crop(1000f));
        Assert.IsTrue(Fields.Farms(home.Find(someone)));

        Assert.IsTrue(Fields.WorkAway(home, null, settings));
        Assert.AreEqual(settings.WateredSeconds, plot.Water, 1e-3f, "the Farmer waters it");

        plot.Water = 0f;
        Assert.IsFalse(Fields.WorkAway(home, someone, settings), "the Farmer along on the raid waters nothing at home");
        Assert.AreEqual(0f, plot.Water);
    }

    [Test]
    public void APlotsWater_IsSaved()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var home = new SettlementState();
        StationQueue plot = home.Workshops.GetOrCreate("plot-1", CraftingStationType.Farm, 10, 3);
        plot.Water = 123f;
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(home))), content);
        StationQueue again = back.Workshops.Find("plot-1");
        Assert.IsNotNull(again, "an empty plot with water is kept");
        Assert.AreEqual(123f, again.Water, 1e-3f);
    }
}
