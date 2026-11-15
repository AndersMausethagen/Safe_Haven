using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Small things around the raid loop: what the stash takes in one press, and who hunts the player on a first raid.</summary>
public sealed class HavenLoopTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object one in made)
        {
            Object.DestroyImmediate(one);
        }

        made.Clear();
    }

    private ItemDefinition Item(ItemCategory category, int maxStack = 50)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        var so = new SerializedObject(item);
        so.FindProperty("category").intValue = (int)category;
        so.FindProperty("stackable").boolValue = true;
        so.FindProperty("maximumStack").intValue = maxStack;
        so.ApplyModifiedPropertiesWithoutUndo();
        made.Add(item);
        return item;
    }

    [Test]
    public void StoringMaterialsTakesOnlyMaterials()
    {
        var stash = new StashStorage();
        var bag = new ItemContainer(10, 2);
        ItemDefinition ore = Item(ItemCategory.Material);
        ItemDefinition stew = Item(ItemCategory.Consumable);
        ItemDefinition pick = Item(ItemCategory.Tool);
        bag.Add(ore, 30);
        bag.Add(stew, 2);
        bag.Add(pick, 1);

        Assert.AreEqual(30, stash.StoreMaterials(bag));
        Assert.AreEqual(0, bag.Count(ore));
        Assert.AreEqual(30, stash.Items.Count(ore));
        Assert.AreEqual(2, bag.Count(stew), "Food stays for the raid");
        Assert.AreEqual(1, bag.Count(pick), "So do tools");
    }

    [Test]
    public void WhatDoesntFitStaysInTheBag()
    {
        var stash = new StashStorage();
        ItemDefinition filler = Item(ItemCategory.Material, 1);
        stash.Items.Add(filler, stash.Items.SlotCount);
        var bag = new ItemContainer(4, 1);
        ItemDefinition ore = Item(ItemCategory.Material);
        bag.Add(ore, 10);

        Assert.AreEqual(0, stash.StoreMaterials(bag));
        Assert.AreEqual(10, bag.Count(ore), "Nothing is lost");
    }

    [TestCase(1f, 0f, 0f, "a way off, east")]
    [TestCase(0f, 0f, 1f, "a way off, north")]
    [TestCase(-1f, 0f, -1f, "a way off, south-west")]
    public void ARumourPointsRoughly(float x, float y, float z, string said)
    {
        Assert.AreEqual(said, RumourWay.Describe(new Vector3(x, y, z).normalized * 40f));
    }

    [Test]
    public void ARumourSaysHowFarInWords()
    {
        Assert.AreEqual("close by", RumourWay.Describe(Vector3.forward * 10f));
        StringAssert.StartsWith("far off", RumourWay.Describe(Vector3.forward * 120f));
    }

    [Test]
    public void TheForecastIsTheSameInTheBriefingAndTheRaid()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        CollectionAssert.AreEqual(RaidForecast.For(raid, 7, 2), RaidForecast.For(raid, 7, 2));
        Assert.AreEqual(2, RaidForecast.For(raid, 7, 2).Count);
        Assert.AreEqual(0, RaidForecast.For(raid, 7, 0).Count, "No Watch Post, no forecast");
        Assert.AreEqual(2, new HashSet<RaidEventDefinition>(RaidForecast.For(raid, 7, 2)).Count, "Not the same thing twice");
    }

    [Test]
    public void TheWatchPostSeesFurtherAsItGrows()
    {
        var post = AssetDatabase.LoadAssetAtPath<BuildingDefinition>("Assets/_Project/Data/Buildings/WatchPost.asset");
        Assert.AreEqual(1, post.Upgrades[0].foretoldEvents);
    }

    [TestCase("Stirring Prowlers")]
    [TestCase("Hunters")]
    [TestCase("Collapse Hunters")]
    [TestCase("East Beacon Alarm")]
    public void AFirstRaidIsHuntedByMoreThanWolves(string listName)
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        RaidSpawnList list = System.Linq.Enumerable.First(raid.SpawnLists, one => one.name == listName);
        Assert.IsNotNull(list, listName);
        var kinds = new HashSet<GameObject>();
        foreach (SpawnTable.Entry entry in list.EntriesFor(1))
        {
            kinds.Add(entry.prefab);
        }

        Assert.GreaterOrEqual(kinds.Count, 2, listName);
    }
}
