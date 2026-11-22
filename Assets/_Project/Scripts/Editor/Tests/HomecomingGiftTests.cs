using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The homecoming's gift (HomecomingSettings, AwayGifts; the Keeper's view, Docs/ColonyModeResearch_2026-11-21.md §4.5): one good
/// thing from someone at home whose job it is, by chance, into the chests; never from someone away on the raid.
/// </summary>
public sealed class HomecomingGiftTests
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

    private T Make<T>() where T : ScriptableObject
    {
        var thing = ScriptableObject.CreateInstance<T>();
        made.Add(thing);
        return thing;
    }

    private HomecomingSettings Settings(JobDefinition job, ItemDefinition item, float chance)
    {
        var settings = Make<HomecomingSettings>();
        var so = new SerializedObject(settings);
        so.FindProperty("chance").floatValue = chance;
        SerializedProperty gifts = so.FindProperty("gifts");
        gifts.arraySize = 1;
        SerializedProperty gift = gifts.GetArrayElementAtIndex(0);
        gift.FindPropertyRelative("job").objectReferenceValue = job;
        gift.FindPropertyRelative("item").objectReferenceValue = item;
        gift.FindPropertyRelative("amount").intValue = 3;
        gift.FindPropertyRelative("line").stringValue = "{name} split some planks.";
        gift.FindPropertyRelative("weight").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return settings;
    }

    [Test]
    public void AGift_ComesFromSomeoneAtHomeWithTheJob_IntoTheChests()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var job = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Carpenter.asset");
        var tor = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Tor.asset");
        var plank = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/OakPlank.asset");
        Assert.IsTrue(content != null && job != null && tor != null && plank != null, "the gift's assets have moved: update the paths");
        HomecomingSettings settings = Settings(job, plank, 0.5f);

        var home = new SettlementState();
        var stash = new StashStorage();
        Assert.IsNull(AwayGifts.Draw(settings, home, stash, 0f, 0f), "nobody with the job at home: no gift");
        home.AddResident(tor, job);
        Assert.IsNull(AwayGifts.Draw(settings, home, stash, 0.9f, 0f), "the roll past the chance: no gift");
        Assert.IsNull(AwayGifts.Draw(settings, home, stash, 0f, 0f, away: tor), "not from someone away on the raid");

        string line = AwayGifts.Draw(settings, home, stash, 0.1f, 0.5f);
        StringAssert.Contains(tor.DisplayName, line, "the line names who gave it");
        StringAssert.Contains("+3", line);
        Assert.AreEqual(3, stash.Items.Count(plank), "the gift is in the chests");
    }

    [Test]
    public void TheShippedGifts_EachHaveAJobAnItemAndAName()
    {
        HomecomingSettings shipped = Resources.Load<HomecomingSettings>(HomecomingSettings.ResourceName);
        Assert.IsNotNull(shipped, "Resources/HomecomingSettings");
        Assert.Greater(shipped.Gifts.Count, 4);
        foreach (HomecomingSettings.Gift gift in shipped.Gifts)
        {
            Assert.IsNotNull(gift.job, gift.line);
            Assert.IsNotNull(gift.item, gift.line);
            StringAssert.Contains("{name}", gift.line, "a gift line names who gave it");
        }
    }
}
