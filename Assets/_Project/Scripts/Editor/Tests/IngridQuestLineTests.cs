using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ingrid's quests at home (the user, 2026-11-19): she gives The Alchemist once she is home, takes it back when the altar stands, and
/// teaches the altar after it, one quest at a time (ResidentDefinition › Quest Line, run by a QuestGiver on her).
/// </summary>
public sealed class IngridQuestLineTests
{
    private const string Quests = "Assets/_Project/Data/Quests/";

    private static ResidentDefinition Ingrid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Settlement/Ingrid.asset");
    private static QuestDefinition Quest(string name) => AssetDatabase.LoadAssetAtPath<QuestDefinition>(Quests + name + ".asset");

    [Test]
    public void HerLine_IsTheAlchemist_ThenTheAltarsThreeLessons_EachGivenAndHandedInByHer()
    {
        // The approved quest flow (Docs/QuestFlowReview_2026-11-20.md §5.3): the Cauldron's draughts come third.
        string[] expected = { "TheAlchemist", "CleansingTheCurse", "ADraughtForTheRoad", "WhatTheSteelRemembers", "MarksOfTheOldWays" };
        CollectionAssert.AreEqual(expected, Ingrid.QuestLine.Select(offer => offer.Quest != null ? offer.Quest.name : null).ToArray());
        QuestDefinition before = null;
        foreach (QuestOffer offer in Ingrid.QuestLine)
        {
            QuestDefinition quest = offer.Quest;
            Assert.AreEqual(QuestStart.ByQuestGiver, quest.Start, quest.name + ": she gives it");
            Assert.IsTrue(quest.TurnInToGiver, quest.name + ": handed back to her");
            Assert.IsFalse(string.IsNullOrWhiteSpace(offer.Text), quest.name + ": she says what she wants");
            Assert.IsEmpty(quest.FindProblems(), quest.name);
            if (before != null)
            {
                CollectionAssert.Contains(quest.Prerequisites.ToArray(), before, quest.name + ": after the one before");
            }

            before = quest;
        }
    }

    [Test]
    public void AQuestLog_OffersEachInTurn_AsTheOneBeforeIsHandedIn()
    {
        var log = new QuestLog();
        foreach (QuestDefinition prerequisite in Quest("TheAlchemist").Prerequisites)
        {
            log.ForceComplete(prerequisite);
        }

        foreach (QuestOffer offer in Ingrid.QuestLine)
        {
            QuestOffer now = QuestGiver.SelectOffer(Ingrid.QuestLine, log);
            Assert.AreSame(offer.Quest, now.Quest, "she offers " + offer.Quest.name + " next");
            Assert.AreEqual(QuestGiverState.Available, QuestGiver.StateFor(now.Quest, log), now.Quest.name);
            Assert.IsTrue(log.TryStart(now.Quest), now.Quest.name);
            log.ForceComplete(now.Quest);
        }
    }

    [Test]
    public void TheAltarsWork_CountsForItsQuests()
    {
        var log = new QuestLog();
        QuestDefinition cleanse = Quest("CleansingTheCurse"), empower = Quest("WhatTheSteelRemembers");
        log.ForceComplete(Quest("TheAlchemist"));
        Assert.IsTrue(log.TryStart(cleanse));
        ResourceDefinition purified = cleanse.Objectives[0].Resource;
        Assert.IsNotNull(purified, "purifying counts the Purified Essence made");
        log.Handle(new GameEvent(GameEventKind.Crafted, purified, cleanse.Objectives[0].Amount));
        Assert.AreEqual(QuestStatus.ReadyToTurnIn, log.StatusOf(cleanse), "the altar's batches count as made");

        log.ForceComplete(cleanse);
        log.ForceComplete(Quest("ADraughtForTheRoad"));
        Assert.IsTrue(log.TryStart(empower));
        var sword = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Weapons/CopperSword.asset");
        log.Handle(new GameEvent(GameEventKind.Repaired, sword, 1));
        Assert.AreEqual(QuestStatus.Active, log.StatusOf(empower), "only empowering counts");
        log.Handle(new GameEvent(GameEventKind.Empowered, sword, 1));
        Assert.AreEqual(QuestStatus.ReadyToTurnIn, log.StatusOf(empower));
    }
}
