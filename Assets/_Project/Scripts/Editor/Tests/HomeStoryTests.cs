using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>The quest line at home: why the haven is built, and the links between its quests that are easy to break by editing data.</summary>
public sealed class HomeStoryTests
{
    private static QuestDefinition Quest(string file) => AssetDatabase.LoadAssetAtPath<QuestDefinition>($"Assets/_Project/Data/Quests/{file}.asset");

    [Test]
    public void TheSearchForIngrid_StartsWhenThePortalWakes()
    {
        QuestDefinition quest = Quest("AStrangerInTheMist");

        Assert.AreEqual(QuestStart.Automatically, quest.Start);
        CollectionAssert.Contains(quest.Prerequisites, Quest("AwakeningThePortal"));
        Assert.AreEqual(2, quest.Objectives.Count, "find her, then bring her home");
        Assert.IsTrue(quest.ObjectivesInOrder);
        Assert.IsTrue(quest.Objectives.All(objective => objective.CountAlreadyOwned), "a rescue before the quest starts still counts");
    }

    [Test]
    public void TheHamletRank_IsWhatTheQuestWaitsFor()
    {
        int hamlet = HavenSettings.Current.Ranks.ToList().FindIndex(rank => rank.name == "Hamlet");
        QuestObjective objective = Quest("ALightTheyCanSee").Objectives[0];

        Assert.AreEqual(ObjectiveType.ReachHavenRank, objective.Type);
        Assert.AreEqual(hamlet, objective.Rank);
        Assert.IsTrue(objective.Matches(new GameEvent(GameEventKind.HavenRankReached, null, hamlet)), "reaching the Hamlet counts");
        Assert.IsFalse(objective.Matches(new GameEvent(GameEventKind.HavenRankReached, null, hamlet - 1)), "the rank below doesn't");
        Assert.IsTrue(objective.CountAlreadyOwned, "a haven can be a Hamlet before the quest starts");
    }

    [Test]
    public void TheHavenPage_CountsTheWholeExpedition()
    {
        var expedition = HavenSettings.Current.Expedition;

        Assert.IsNotEmpty(expedition);
        Assert.IsFalse(expedition.Contains(null));
        Assert.AreEqual(expedition.Count, expedition.Select(person => person.DisplayName).Distinct().Count(), "each one counted once");
    }

    // Docs/QuestRedesign.md (2026-11-10): the door opens after a few short steps, where nine quests used to stand before it, and
    // the fire is the only thing built on the way. Which quests those steps are is the data's: they are found by walking back from
    // the first raid's prerequisites.
    [Test]
    public void OnlyTheFire_IsBuiltBeforeTheFirstRaid()
    {
        var before = new HashSet<QuestDefinition>();
        var waiting = new Stack<QuestDefinition>(Quest("ThroughThePortal").Prerequisites);
        while (waiting.Count > 0)
        {
            QuestDefinition quest = waiting.Pop();
            if (quest != null && before.Add(quest))
            {
                foreach (QuestDefinition earlier in quest.Prerequisites)
                {
                    waiting.Push(earlier);
                }
            }
        }

        Assert.IsNotEmpty(before, "the first raid waits for the steps before it");
        string[] built = before.SelectMany(quest => quest.Objectives).Where(o => o.Type == ObjectiveType.BuildStructure)
            .Select(o => o.Building.name).Distinct().ToArray();
        CollectionAssert.AreEqual(new[] { "Campfire" }, built, "the fire is the only thing built before the first raid");
    }

    [Test]
    public void TheFirstRaid_FindsTheOneTheSearchIsFor()
    {
        // Finding Ingrid is her rescue quest's alone (the quest flow review's A10: Into the Misty Woods asked it twice); that quest is
        // under way before the first raid, so the first raid's find counts for it.
        Assert.IsFalse(Quest("ThroughThePortal").Objectives.Any(o => o.Type == ObjectiveType.RescuePerson), "the first raid is the extraction");
        QuestDefinition search = Quest("AStrangerInTheMist");
        QuestObjective find = search.Objectives.First(o => o.Type == ObjectiveType.RescuePerson);
        Assert.IsNotNull(find.Person);
        Assert.IsFalse(search.Prerequisites.Contains(Quest("ThroughThePortal")), "the search begins before the first raid, not after it");
    }

    // The Heartroot left the Woods' expeditions (2026-11-17, Docs/HeartrootEncounterDesign_2026-11-17.md): an expedition finds
    // its hollow and brings the way home, then What Breathes opens the encounter. A Reach Place counts on entering the zone, so
    // the coming home is its own step. A save already on or past What Breathes never gets the find step.
    [Test]
    public void TheBreathingHollow_IsFoundOnAnExpedition_AndBroughtHome()
    {
        QuestDefinition find = Quest("TheBreathingHollow"), wake = Quest("WhatBreathesUnderTheRoots");
        Assert.AreEqual(QuestRank.MainStory, find.Rank);
        CollectionAssert.AreEquivalent(new[] { Quest("TheWeakeningWard"), Quest("BringBjarneHome") }, find.Prerequisites);
        CollectionAssert.AreEqual(new[] { find }, wake.Prerequisites);
        CollectionAssert.AreEqual(new[] { ObjectiveType.ReachPlace, ObjectiveType.CompleteRaids }, find.Objectives.Select(o => o.Type).ToArray());
        Assert.IsTrue(find.ObjectivesInOrder, "the way counts once it is brought home");
        Assert.IsTrue(find.Objectives[0].CountAlreadyOwned, "a find made before the quest counts");

        var woods = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        Assert.AreEqual(1, find.Objectives[1].Progress(new GameEvent(GameEventKind.RaidFinished, woods, 1, null, RaidOutcome.Extracted, 600f)));
        Assert.AreEqual(0, find.Objectives[1].Progress(new GameEvent(GameEventKind.RaidFinished, woods, 1, null, RaidOutcome.Died, 600f)),
            "falling out there brings nothing home");

        var hollow = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/GuardiansHollow.asset");
        Assert.AreEqual(wake, hollow.Encounter.OpensWhen.Quest);
        Assert.AreEqual(QuestGate.Requirement.Started, hollow.Encounter.OpensWhen.Requirement);
        Assert.AreNotEqual(woods.FirstVisitCinematic, hollow.FirstVisitCinematic, "the Woods' first-raid film is not the hollow's");

        var log = new QuestLog();
        foreach (QuestDefinition before in find.Prerequisites) log.Restore(before, QuestStatus.Completed, new int[before.Objectives.Count]);
        log.Restore(wake, QuestStatus.Active, new int[wake.Objectives.Count]);
        Assert.IsFalse(log.TryStart(find), "a save already on What Breathes");
        Assert.IsTrue(hollow.Encounter.IsOpenBy(log), "and its encounter is open");
    }

    [Test]
    public void TheChestAndTheWeapon_CanBeMadeWithWhatCameBefore()
    {
        BuildingDefinition chest = Quest("SafeKeeping").Objectives.Single(o => o.Type == ObjectiveType.BuildStructure).Building;
        Assert.AreEqual("SmallChest", chest.name);
        Assert.IsTrue(chest.KnownFromStart);
        Assert.IsTrue(chest.Requires.All(r => r.building.name == "ConstructionTable" && r.tier <= 1), "the workshop of A Roof for the Work is enough");

        QuestObjective weapon = Quest("FirstWeapon").Objectives.Single();
        Assert.AreEqual(ObjectiveType.CraftItem, weapon.Type);
        Assert.IsTrue(weapon.Item.IsWeapon);
        Assert.IsTrue(weapon.CountAlreadyOwned, "a club made earlier counts");
        Assert.IsFalse(Quest("FoodAndWater").Objectives.Any(o => o.Item == weapon.Item), "the weapon is asked for once");
    }

    // Playtest 2026-11-08: Hild turned up with the Homestead rank, unexplained. Everyone comes home from a raid.
    [Test]
    public void NoRank_BringsSomeone()
    {
        Assert.IsTrue(HavenSettings.Current.Ranks.All(rank => rank.settler == null));
    }

    [Test]
    public void SteadyTheDoor_AsksForThePortalsSecondTier_AndCountsItsRaise()
    {
        // The quest flow review's A15 (approved 2026-11-21): raising the portal is taught by a quest after Ingrid's and the Smithy's.
        QuestDefinition door = Quest("SteadyTheDoor");
        Assert.IsNotNull(door);
        CollectionAssert.AreEquivalent(new[] { Quest("CleansingTheCurse"), Quest("TheSmith") }, door.Prerequisites.ToArray());
        QuestObjective raise = door.Objectives.Single();
        Assert.AreEqual(ObjectiveType.RaisePortal, raise.Type);
        Assert.AreEqual(0, raise.Progress(new GameEvent(GameEventKind.PortalRaised, null, 1)), "tier I is where it starts");
        Assert.AreEqual(1, raise.Progress(new GameEvent(GameEventKind.PortalRaised, null, 2)));
        Assert.AreEqual(0, raise.Progress(new GameEvent(GameEventKind.HavenRankReached, null, 2)), "another event of the same amount isn't a raise");
    }
}
