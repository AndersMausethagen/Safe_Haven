using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// A rescued resident's quest line starts at the rescue, and their workplace is learned only once its first quest has started
/// (ResidentQuests, the user 2026-11-20); a building a later quest introduces waits for that quest (BuildingGate, the approved quest
/// flow review). Checked on every shipped resident that has a quest line.
/// </summary>
public sealed class ResidentQuestsTests
{
    private GameObject player;

    [TearDown]
    public void TearDown()
    {
        if (player != null)
        {
            Object.DestroyImmediate(player);
        }

        GameSession.StartNewGame();
    }

    private static T Asset<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>("Assets/_Project/" + path + ".asset");
    private static ContentDatabase Content => Asset<ContentDatabase>("Resources/ContentDatabase");

    private static List<ResidentDefinition> WithQuestLines() =>
        Content.Residents.Where(person => person != null && ResidentQuests.FirstQuest(person) != null).ToList();

    // What their jobs teach that a new game doesn't already know.
    private static List<BuildingDefinition> Workplaces(ResidentDefinition person) =>
        person.Jobs.Where(job => job != null).SelectMany(job => job.TeachesBuildings).Where(b => b != null && !b.KnownFromStart).Distinct().ToList();

    private PlayerCrafting NewPlayer()
    {
        if (player != null)
        {
            Object.DestroyImmediate(player);
        }

        player = new GameObject("Player");
        player.SetActive(false);
        PlayerCrafting crafting = player.AddComponent<PlayerCrafting>();
        var data = new SerializedObject(crafting);
        data.FindProperty("recipeBook").objectReferenceValue = Asset<RecipeBook>("Data/Crafting/RecipeBook");
        data.ApplyModifiedPropertiesWithoutUndo();
        return crafting;
    }

    [Test]
    public void AWorkplace_IsLearnedOnlyOnceTheFirstQuestStarts()
    {
        List<ResidentDefinition> people = WithQuestLines();
        Assert.Greater(people.Count, 0, "at least one resident has a quest line (Ingrid's)");
        int checkedPlaces = 0;
        foreach (ResidentDefinition person in people)
        {
            GameSession.StartNewGame();
            PlayerCrafting crafting = NewPlayer();
            QuestDefinition first = ResidentQuests.FirstQuest(person);
            List<BuildingDefinition> places = Workplaces(person);

            ResidentNpc.TeachWhatTheyKnow(person, player, false);
            foreach (BuildingDefinition place in places)
            {
                Assert.IsFalse(crafting.IsKnown(place), $"{person.name}'s {place.name} waits for {first.name} to start");
            }

            Assert.IsTrue(ResidentQuests.StartFirst(person, player, false), $"{first.name} starts at the rescue, prerequisites aside");
            Assert.AreEqual(QuestStatus.Active, GameSession.Quests.StatusOf(first));
            foreach (BuildingDefinition place in places)
            {
                // What the first quest introduces (or nothing does) comes now; what a later quest of theirs introduces waits for it.
                bool now = BuildingGate.IntroducedBy(place).Count == 0 || BuildingGate.IntroducedBy(place).Contains(first);
                Assert.AreEqual(now, crafting.IsKnown(place), $"{person.name}'s {place.name} " + (now ? "is learned with " + first.name : "waits for its own quest"));
                checkedPlaces++;
            }

            Assert.IsFalse(ResidentQuests.StartFirst(person, player, false), "a quest under way isn't started twice");
        }

        Assert.Greater(checkedPlaces, 0, "a workplace was checked");
    }

    [Test]
    public void SomeoneHomeWhoseLineNeverStarted_StartsItWhenThePlayerArrives()
    {
        ResidentDefinition person = WithQuestLines().FirstOrDefault();
        Assert.IsNotNull(person);
        GameSession.Home.AddResident(person);
        NewPlayer();
        Assert.AreEqual(QuestStatus.NotStarted, GameSession.Quests.StatusOf(ResidentQuests.FirstQuest(person)));

        ResidentNpc.TeachEveryoneHome(player);
        Assert.AreEqual(QuestStatus.Active, GameSession.Quests.StatusOf(ResidentQuests.FirstQuest(person)));
    }

    // The user, 2026-11-21: Ingrid's rescue completed itself on extraction and taught her building; it must be handed in to her at
    // home, and her building waits for that. Checked on every shipped quest that brings someone home.
    [Test]
    public void ARescue_IsHandedInToThePersonAtHome_AndTheirLineWaitsForIt()
    {
        List<QuestDefinition> rescues = Content.Quests.Where(q => q != null && q.BroughtHome != null && ResidentQuests.FirstQuest(q.BroughtHome) != null).ToList();
        Assert.GreaterOrEqual(rescues.Count, 5, "the Bring ... Home quests are found (Ingrid's, Bjarne's, Liv's ...)");
        int checkedPlaces = 0;
        foreach (QuestDefinition rescue in rescues)
        {
            GameSession.StartNewGame();
            PlayerCrafting crafting = NewPlayer();
            ResidentDefinition person = rescue.BroughtHome;
            QuestDefinition first = ResidentQuests.FirstQuest(person);
            Assert.IsTrue(rescue.TurnInToGiver, rescue.name + " is handed in");
            StringAssert.Contains(person.DisplayName, rescue.TurnInTo, rescue.name + ": the tracker says who to return to");

            Assert.IsTrue(GameSession.Quests.TryStart(rescue, prerequisitesAside: true), rescue.name);
            GameSession.Home.AddResident(person);
            for (int i = 0; i < rescue.Objectives.Count; i++)
            {
                GameSession.Quests.AddProgress(rescue, i, rescue.Objectives[i].Amount);
            }

            Assert.AreEqual(QuestStatus.ReadyToTurnIn, GameSession.Quests.StatusOf(rescue), rescue.name + " waits at the end, not completed on extraction");
            Assert.AreEqual(rescue, ResidentQuests.RescueToHandIn(person));
            Assert.IsFalse(ResidentQuests.StartFirst(person, player, false), $"{first.name} waits for the hand-in");
            ResidentNpc.TeachEveryoneHome(player);
            Assert.AreEqual(QuestStatus.NotStarted, GameSession.Quests.StatusOf(first), $"arriving home doesn't start {first.name} either");
            List<BuildingDefinition> places = Workplaces(person).Where(p => BuildingGate.IntroducedBy(p).Contains(first)).ToList();
            foreach (BuildingDefinition place in places)
            {
                Assert.IsFalse(crafting.IsKnown(place), $"{place.name} waits until {person.name} is spoken to");
            }

            Assert.IsTrue(ResidentQuests.HandInRescue(person, player), rescue.name + " is handed in to " + person.name);
            Assert.AreEqual(QuestStatus.Completed, GameSession.Quests.StatusOf(rescue));
            Assert.AreEqual(QuestStatus.Active, GameSession.Quests.StatusOf(first), $"{first.name} starts with the hand-in");
            Assert.IsNull(ResidentQuests.RescueToHandIn(person));
            Assert.IsFalse(ResidentQuests.HandInRescue(person, player), "handed in once");
            foreach (BuildingDefinition place in places)
            {
                Assert.IsTrue(crafting.IsKnown(place), $"{place.name} is learned with the hand-in");
                checkedPlaces++;
            }
        }

        Assert.Greater(checkedPlaces, 0, "a workplace was checked (the Purification Altar)");
    }

    // Every rescued person starts a quest chain (the user, 2026-11-20): a line of two or more, the first introducing their workplace.
    [Test]
    public void EveryoneRescued_HasAQuestChain()
    {
        int people = 0;
        foreach (ResidentDefinition person in Content.Residents.Where(p => p != null && p.Jobs.Length > 0))
        {
            Assert.GreaterOrEqual(person.QuestLine.Count(offer => offer?.Quest != null), 2, person.name + " has a chain of their own");
            Assert.IsTrue(person.QuestLine.All(offer => offer?.Quest != null && !string.IsNullOrWhiteSpace(offer.Text)), person.name + " says what each quest wants");
            people++;
        }

        Assert.Greater(people, 5);
    }

    [Test]
    public void ALaterTrade_IsLearnedWithItsQuest_NotWithTheJob()
    {
        int checkedPlaces = 0;
        foreach (ResidentDefinition person in WithQuestLines())
        {
            foreach (BuildingDefinition place in Workplaces(person))
            {
                QuestDefinition introducing = BuildingGate.IntroducedBy(place).FirstOrDefault();
                if (introducing == null || introducing == ResidentQuests.FirstQuest(person))
                {
                    continue;
                }

                GameSession.StartNewGame();
                PlayerCrafting crafting = NewPlayer();
                ResidentQuests.StartFirst(person, player, false);
                Assert.IsFalse(crafting.IsKnown(place), $"{place.name} waits for {introducing.name}");
                Assert.IsTrue(GameSession.Quests.TryStart(introducing, prerequisitesAside: true));
                ResidentNpc.TeachWhatTheyKnow(person, player, false);
                Assert.IsTrue(crafting.IsKnown(place), $"{place.name} comes with {introducing.name}");
                checkedPlaces++;
            }
        }

        Assert.Greater(checkedPlaces, 0, "a second trade was checked (the Cauldron, the Dock, the Yard)");
    }

    [Test]
    public void AJob_IsOfferedOnlyOnceTheQuestThatTeachesItsWorkplaceHasBegun()
    {
        // Yrsa could be a Fisher long before Gunnar's line taught the Dock (the review of 2026-11-21).
        int checkedJobs = 0;
        foreach (ResidentDefinition person in Content.Residents.Where(r => r != null))
        {
            foreach (JobDefinition job in person.Jobs.Where(j => j != null && j.Workplace != null))
            {
                QuestDefinition introducing = BuildingGate.IntroducedBy(job.Workplace).FirstOrDefault();
                if (introducing == null)
                {
                    continue;
                }

                GameSession.StartNewGame();
                Assert.IsFalse(GameSession.Here.JobsOpenTo(person).Contains(job), $"{person.name}: {job.name} waits for {introducing.name}");
                Assert.IsTrue(GameSession.Quests.TryStart(introducing, prerequisitesAside: true));
                Assert.IsTrue(GameSession.Here.JobsOpenTo(person).Contains(job), $"{person.name}: {job.name} once {introducing.name} has begun");
                checkedJobs++;
            }
        }

        Assert.Greater(checkedJobs, 0, "the Fisher at the Dock was checked");
    }
}
