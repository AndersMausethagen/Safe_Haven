using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The kinds of quest (Docs/QuestRedesign.md): the story is few and gold, rescues and the haven have kinds of their own,
/// nothing optional stands in the story's way, and what the player wore in the prologue comes home.
/// </summary>
public sealed class QuestCategoryTests
{
    private static IReadOnlyList<QuestDefinition> Quests =>
        AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset").Quests;

    private static T Asset<T>(string name) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}"))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && asset.name == name) return asset;
        }

        Assert.Fail($"No {typeof(T).Name} named {name}");
        return null;
    }

    [Test]
    public void EveryQuest_IsSound()
    {
        foreach (QuestDefinition quest in Quests)
        {
            Assert.NotNull(quest, "the content database lists a quest that no longer exists");
            Assert.IsEmpty(quest.FindProblems(), quest.name);
            Assert.IsFalse(quest.Prerequisites.Contains(null), quest.name + " waits for a quest that no longer exists");
        }
    }

    // The rule that opens the door early: tools, food, the workshop, tales and errands help, and never gate.
    [Test]
    public void NothingOptional_StandsBeforeTheStoryOrARescue()
    {
        foreach (QuestDefinition quest in Quests.Where(q => q.Rank == QuestRank.MainStory || q.Rank == QuestRank.Rescue))
        {
            var seen = new HashSet<QuestDefinition>();
            var open = new Stack<QuestDefinition>(quest.Prerequisites);
            while (open.Count > 0)
            {
                QuestDefinition before = open.Pop();
                if (!seen.Add(before)) continue;
                Assert.IsTrue(before.Rank == QuestRank.MainStory || before.Rank == QuestRank.Rescue,
                    $"{quest.name} waits for {before.name}, which is a {QuestMarkerSettings.RankName(before.Rank).ToLowerInvariant()}");
                foreach (QuestDefinition earlier in before.Prerequisites) open.Push(earlier);
            }
        }
    }

    [Test]
    public void AtHome_OnlyTheStoryIsGold()
    {
        string[] gold = Quests.Where(q => q.Rank == QuestRank.MainStory && !q.Chain.StartsWith("Prologue")).Select(q => q.name).ToArray();
        CollectionAssert.AreEquivalent(new[] { "Awakening", "FirstCamp", "RootboundStones", "AwakeningThePortal", "ThroughThePortal", "TheWeakeningWard" }, gold);
        Assert.AreEqual(11, Quests.Count(q => q.Rank == QuestRank.Rescue), "one rescue for each of the lost expedition");
        Assert.IsTrue(Quests.Where(q => q.Chain == "Hands at Home").All(q => q.Rank == QuestRank.Haven), "work at home is a Haven quest");
    }

    [Test]
    public void Kinds_HaveAnOrderAndAName()
    {
        QuestRank[] order = { QuestRank.MainStory, QuestRank.Rescue, QuestRank.Haven, QuestRank.SideQuest, QuestRank.Optional };
        CollectionAssert.AreEqual(order, ((QuestRank[])System.Enum.GetValues(typeof(QuestRank))).OrderBy(QuestMarkerSettings.RankOrder).ToArray());
        Assert.AreEqual(order.Length, order.Select(QuestMarkerSettings.RankName).Distinct().Count());

        QuestDefinition story = Asset<QuestDefinition>("FirstCamp"), rescue = Asset<QuestDefinition>("BringTorHome"),
            haven = Asset<QuestDefinition>("SafeKeeping"), errand = Asset<QuestDefinition>("KnowYourEnemy");
        var tracker = new List<QuestDefinition> { errand, haven, rescue, story };
        tracker.Sort(QuestTrackerView.ByRankThenName);
        CollectionAssert.AreEqual(new[] { story, rescue, haven, errand }, tracker, "the story on top, errands last");

        Assert.AreEqual("New quest: " + story.DisplayName, QuestMarkerSettings.NewQuestText(story));
        Assert.AreEqual("New rescue quest: " + rescue.DisplayName, QuestMarkerSettings.NewQuestText(rescue));
        Assert.AreEqual("New haven quest: " + haven.DisplayName, QuestMarkerSettings.NewQuestText(haven));
        Assert.AreEqual("New errand: " + errand.DisplayName, QuestMarkerSettings.NewQuestText(errand));
    }

    // The prologue ends by the rule of a failed raid: what was worn comes through, what was carried does not.
    [Test]
    public void WhatWasWorn_IsTheGearAndTheArrows_AndNothingElse()
    {
        ItemDefinition club = Asset<ItemDefinition>("KnottedClub"), arrows = Asset<ItemDefinition>("CrudeStoneArrow"), sticks = Asset<ItemDefinition>("Sticks");
        Assert.IsTrue(arrows.IsArrow);
        var bag = new ItemContainer(4, 2);
        bag.SetSlot(0, new ItemStack(sticks, 6));
        bag.SetSlot(3, new ItemStack(arrows, 12));
        var gear = new ItemContainer(GearSlots.Count, 1);
        gear.SetSlot((int)GearSlot.MainHand, new ItemStack(club, 1, 0.6f));
        var carried = new ResourceStorage(100);
        PlayerSnapshot atTheStones = PlayerSnapshot.Capture(bag, carried, null, null, null, new PlayerSnapshot.Condition(20f, 30f, -1), gear);

        PlayerSnapshot worn = atTheStones.WhatWasWorn();

        Assert.IsTrue(worn.HasGear);
        Assert.AreEqual(1, worn.Gear.Count);
        Assert.AreEqual((int)GearSlot.MainHand, worn.Gear[0].Index);
        Assert.AreEqual(club, worn.Gear[0].Stack.Item);
        Assert.AreEqual(0.6f, worn.Gear[0].Stack.Durability, 0.001f, "no wear for the way home");
        Assert.AreEqual(1, worn.Slots.Count, "of what was carried only the arrows come through");
        Assert.AreEqual(arrows, worn.Slots[0].Stack.Item);
        Assert.AreEqual(12, worn.Slots[0].Stack.Count);
        Assert.IsFalse(worn.State.HasNeeds, "hunger and thirst are the Haven's own");
        Assert.IsEmpty(worn.KnownRecipes);
        Assert.IsTrue(AssetDatabase.LoadAssetAtPath<PrologueDefinition>("Assets/_Project/Data/Prologue/ForestPrologue.asset").WornGearComesHome);
    }
}
