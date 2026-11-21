using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Act II after the Brukam (Docs/Act2Outline_2026-11-21.md): its sites count for their quests, and Sigrid is found in Troll Valley.</summary>
public sealed class SecondActTests
{
    private const string Quests = "Assets/_Project/Data/Quests/";
    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    private static QuestDefinition Quest(string file) => Load<QuestDefinition>(Quests + file + ".asset");
    private static ResidentDefinition Resident(string name) => Load<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/" + name + ".asset");

    private static List<RaidRumourDefinition> PoolOf(string raid)
    {
        var data = new SerializedObject(Load<RaidDefinition>("Assets/_Project/Data/Raids/" + raid + ".asset"));
        SerializedProperty it = data.GetIterator();
        while (it.Next(true))
        {
            if (it.propertyType == SerializedPropertyType.ManagedReference && it.managedReferenceFullTypename.EndsWith("RaidRumoursSystem"))
            {
                SerializedProperty pool = data.FindProperty(it.propertyPath + ".pool");
                return Enumerable.Range(0, pool.arraySize).Select(i => pool.GetArrayElementAtIndex(i).objectReferenceValue as RaidRumourDefinition).ToList();
            }
        }

        return new List<RaidRumourDefinition>();
    }

    // The folded wood's bells ring for everyone home but the one being found, by name.
    [Test]
    public void TheBells_RingForEveryoneHome_ButTheOneFound()
    {
        var home = new List<ResidentRecord> { new ResidentRecord(Resident("Eirik")), new ResidentRecord(Resident("Runa")), new ResidentRecord(Resident("Sigrid")) };
        string words = RingNameBellsOutcome.Words(home, Resident("Sigrid"), "She answers.");
        StringAssert.Contains("Eirik and Runa", words);
        StringAssert.Contains("2 bells", words);
        StringAssert.DoesNotContain("Sigrid", words);
        StringAssert.EndsWith("She answers.", words);
        StringAssert.Contains("no one home", RingNameBellsOutcome.Words(new List<ResidentRecord>(), null, ""));
    }

    // Every Act II quest that asks for a place in a raid has an urgent rumour there while it runs, whose site gives what the step counts.
    [TestCase("TheDoorInTheSnow", "TrollValley")]
    [TestCase("ARedThread", "TrollValley")]
    [TestCase("WhereTheWrongDoorsOpened", "MossMarsh")]
    [TestCase("TheRoomBeneathTheRoad", "TrollValley")]
    [TestCase("BringSigridHome", "TrollValley")]
    public void EveryRaidStep_HasItsSite_OnItsRaid(string quest, string raid)
    {
        QuestDefinition definition = Quest(quest);
        RaidRumourDefinition rumour = PoolOf(raid).FirstOrDefault(r => r != null && r.WhileQuest == definition);
        Assert.IsNotNull(rumour, $"{quest} has a rumour in {raid}");
        Assert.IsTrue(rumour.Urgent, "it is offered while the quest runs");
        RaidEventDefinition site = rumour.Site;
        Assert.IsNotNull(site);
        Assert.IsNotNull(site.SitePrefab, "a site to walk to");
        QuestObjective first = definition.Objectives[0];
        switch (first.Type)
        {
            case ObjectiveType.QuestEvent:
                Assert.AreEqual(first.QuestEvent, site.RaisesWhenChosen, "the site's choice counts the step");
                break;
            case ObjectiveType.CollectItem:
                Assert.IsTrue(site.Choices.SelectMany(c => c.outcomes).OfType<GiveLootOutcome>()
                    .Any(o => o.Loot.Entries.Any(e => e.Item == first.Item)), "the site gives the item");
                break;
            case ObjectiveType.RescuePerson:
                Assert.IsTrue(site.Choices.SelectMany(c => c.outcomes).OfType<RescueResidentOutcome>().Any(), "the site frees the person");
                break;
            default:
                Assert.Fail("an unexpected first step: " + first.Type);
                break;
        }
    }

    [Test]
    public void Sigrid_IsFoundInTrollValley_NotTheMistyWoods()
    {
        var rumour = Load<RaidRumourDefinition>("Assets/_Project/Data/Raids/Rumours/Sigrid.asset");
        Assert.Contains(rumour, PoolOf("TrollValley"));
        CollectionAssert.DoesNotContain(PoolOf("MistyWoods"), rumour);
        CollectionAssert.Contains(Quest("BringSigridHome").Prerequisites.ToList(), Quest("TheRoomBeneathTheRoad"), "found after the room beneath the road");
    }
}
