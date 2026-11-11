using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Quests in co-op, decided in one place (QuestObjective.CountsInAFriendsHaven, a Quest Event's Home Only), so a new quest
/// needs no multiplayer check: what a visitor does with their own hands counts anywhere, what belongs to a Haven only in
/// their own; a friend's people offer them nothing.
/// </summary>
public sealed class QuestCoopTests
{
    private static void Visiting(bool visiting) => typeof(HavenVisit).GetProperty(nameof(HavenVisit.IsVisiting)).SetValue(null, visiting);

    [Test]
    public void EveryObjectiveType_HasACoopRule()
    {
        CollectionAssert.AreEquivalent(Enum.GetValues(typeof(ObjectiveType)).Cast<ObjectiveType>(), QuestObjective.TypesWithACoopRule,
            "a new objective type says whether it counts in a friend's Haven (QuestObjective.InAFriendsHaven)");
    }

    [Test]
    public void InAFriendsHaven_OwnHandsCount_TheHavensThingsDont()
    {
        var club = AssetDatabase.FindAssets("t:ItemDefinition").Select(g => AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(g))).First(i => i != null);
        var hut = AssetDatabase.FindAssets("t:BuildingDefinition").Select(g => AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(g))).First();
        QuestObjective craft = QuestObjective.Create(ObjectiveType.CraftItem, 1, club);
        QuestObjective build = QuestObjective.Create(ObjectiveType.BuildStructure, 1, hut);
        var crafted = new GameEvent(GameEventKind.Crafted, club);
        var built = new GameEvent(GameEventKind.Built, hut);
        Assert.AreEqual(1, craft.Progress(crafted));
        Assert.AreEqual(1, build.Progress(built));
        try
        {
            Visiting(true);
            Assert.AreEqual(1, craft.Progress(crafted), "what the player makes with their own hands counts anywhere");
            Assert.AreEqual(0, build.Progress(built), "what is built in a friend's Haven stands in theirs: it isn't this player's quest done");
        }
        finally
        {
            Visiting(false);
            HavenVisit.Forget();
        }
    }

    [Test]
    public void AHomeOnlyEvent_InAFriendsHaven_IsntRemembered_NorCounted()
    {
        var lit = AssetDatabase.LoadAssetAtPath<GameEventDefinition>("Assets/_Project/Data/Quests/Events/LandStoneLit.asset");
        var drank = AssetDatabase.LoadAssetAtPath<GameEventDefinition>("Assets/_Project/Data/Quests/Events/DrankAtWater.asset");
        Assert.IsTrue(lit.HomeOnly, "a boundary stone is a Haven's (FixQuestAudit)");
        Assert.IsFalse(drank.HomeOnly, "any water will do");
        var log = new QuestLog();
        try
        {
            Visiting(true);
            log.Handle(new GameEvent(GameEventKind.Custom, lit));
            log.Handle(new GameEvent(GameEventKind.Custom, drank));
            Assert.IsFalse(log.HasHappened(lit), "or it would count once home");
            Assert.IsTrue(log.HasHappened(drank));
            Assert.AreEqual(0, QuestObjective.Create(ObjectiveType.QuestEvent, 1, lit).Progress(new GameEvent(GameEventKind.Custom, lit)));
        }
        finally
        {
            Visiting(false);
            HavenVisit.Forget();
        }

        log.Handle(new GameEvent(GameEventKind.Custom, lit));
        Assert.IsTrue(log.HasHappened(lit), "at home it counts");
    }

    [Test]
    public void AFriendsPeople_OfferAVisitorNothing()
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>("Assets/_Project/Data/Quests/SmithsRequest.asset");
        var log = new QuestLog();
        Assert.AreEqual(QuestGiverState.Available, QuestGiver.StateFor(quest, log), "the notice board offers it at home");
        try
        {
            Visiting(true);
            Assert.AreEqual(QuestGiverState.None, QuestGiver.StateFor(quest, log), "a friend's board asks things of the friend");
        }
        finally
        {
            Visiting(false);
            HavenVisit.Forget();
        }
    }
}
