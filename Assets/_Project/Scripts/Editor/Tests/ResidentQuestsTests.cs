using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// A rescued resident's quest line starts at the rescue, and their workplace is learned only once its first quest has started
/// (ResidentQuests, the user 2026-11-20). Checked on every shipped resident that has a quest line.
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
                Assert.IsTrue(crafting.IsKnown(place), $"{person.name}'s {place.name} is learned with {first.name}");
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

    [Test]
    public void SomeoneWithoutAQuestLine_TeachesAtOnce()
    {
        ResidentDefinition person = Content.Residents.FirstOrDefault(p => p != null && ResidentQuests.FirstQuest(p) == null && Workplaces(p).Count > 0);
        Assert.IsNotNull(person, "a resident without a quest line who teaches a workplace");
        PlayerCrafting crafting = NewPlayer();
        ResidentNpc.TeachWhatTheyKnow(person, player, false);
        Assert.IsTrue(Workplaces(person).All(crafting.IsKnown));
    }
}
