using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class HavenManagementTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in created)
        {
            Object.DestroyImmediate(thing);
        }

        created.Clear();
    }

    [Test]
    public void Stores_AddUpEachKind_AcrossStashCargoAndStations()
    {
        ItemDefinition plank = Item("Plank", ItemCategory.Material);
        var stash = new StashStorage();
        stash.Items.Add(plank, 5);
        var cart = new ItemContainer(4, 1);
        cart.Add(plank, 3);
        var workshops = new WorkshopState();
        var queue = new StationQueue("saw", CraftingStationType.None, 10, 5);
        queue.Restore(new List<StationQueue.Job>(), 0f, plank, 2);
        workshops.Add(queue);

        List<HavenShelf> shelves = HavenStores.Read(stash, workshops, new[] { ("Cart", cart) });

        HavenEntry entry = shelves.SelectMany(shelf => shelf.Entries).Single();
        Assert.AreEqual(plank, entry.Key);
        Assert.AreEqual("10", entry.Badge);
        StringAssert.Contains("Stash 5", entry.Line);
        StringAssert.Contains("Cart 3", entry.Line);
    }

    [Test]
    public void Stores_ShelveItemsByWhatTheyAreFor_AndEssencesApart()
    {
        var stash = new StashStorage();
        stash.Items.Add(Item("Plank", ItemCategory.Material), 1);
        stash.Items.Add(Item("Stew", ItemCategory.Consumable), 1);
        var essence = ScriptableObject.CreateInstance<ResourceDefinition>();
        created.Add(essence);
        stash.Resources.Add(essence, 7);

        List<HavenShelf> shelves = HavenStores.Read(stash, new WorkshopState(), new (string, ItemContainer)[0]);

        Assert.AreEqual(1, shelves.Find(shelf => shelf.Title == "Materials").Entries.Count);
        Assert.AreEqual(1, shelves.Find(shelf => shelf.Title == "Supplies").Entries.Count);
        Assert.AreEqual("7", shelves.Find(shelf => shelf.Title == "Essences").Entries.Single().Badge);
        Assert.IsEmpty(shelves.Find(shelf => shelf.Title == "Gear").Entries);
    }

    [Test]
    public void Stores_LeaveOutWhatThePlayerCarries_AndCountSlotsAChestNoLongerOpens()
    {
        ItemDefinition plank = Item("Plank", ItemCategory.Material);
        var stash = new StashStorage();
        stash.Items.Add(plank, 4);
        stash.Items.SetUnlockedSlots(0);

        List<HavenShelf> shelves = HavenStores.Read(stash, new WorkshopState(), new (string, ItemContainer)[0]);

        Assert.AreEqual("4", shelves.SelectMany(shelf => shelf.Entries).Single().Badge);
    }

    [Test]
    public void People_AreHomeOrAway_AndSomeoneWithoutWorkWantsAttention()
    {
        JobDefinition smith = Job("Smith");
        ResidentDefinition bjarne = Person("Bjarne", smith);
        ResidentDefinition tor = Person("Tor", Job("Carpenter"));
        var settlement = new SettlementState();
        settlement.AddResident(bjarne);

        List<HavenShelf> shelves = HavenPeople.Read(new[] { bjarne, tor }, settlement);

        HavenEntry home = shelves.Find(shelf => shelf.Title == HavenPeople.HomeShelf).Entries.Single();
        HavenEntry away = shelves.Find(shelf => shelf.Title == HavenPeople.AwayShelf).Entries.Single();
        Assert.AreEqual(bjarne, home.Key);
        Assert.AreEqual(HavenEntryState.Attention, home.State);
        Assert.IsTrue(home.Actions.Any(action => action.Label == "Work as Smith"));
        Assert.AreEqual(tor, away.Key);
        Assert.AreEqual(HavenEntryState.Locked, away.State);
        Assert.IsEmpty(away.Actions);
    }

    [Test]
    public void People_WithAJob_ShowIt_AndAreNotOfferedItAgain()
    {
        JobDefinition smith = Job("Smith");
        ResidentDefinition bjarne = Person("Bjarne", smith);
        var settlement = new SettlementState();
        settlement.AddResident(bjarne, smith);

        HavenEntry home = HavenPeople.Read(new[] { bjarne }, settlement)[0].Entries.Single();

        Assert.AreEqual("Smith", home.Line);
        Assert.AreEqual(HavenEntryState.Good, home.State);
        Assert.IsFalse(home.Actions.Any(action => action.Label.StartsWith("Work as")));
    }

    [Test]
    public void GivingAJob_ThroughTheWindowsRoute_RecordsItInTheSettlement()
    {
        JobDefinition smith = Job("Smith");
        ResidentDefinition bjarne = Person("Bjarne", smith);
        GameSession.Settlement.AddResident(bjarne);

        ResidentNpc.GiveJob(bjarne, smith, null);

        Assert.AreEqual(smith, GameSession.Settlement.Find(bjarne).Job);
        GameSession.StartNewGame();
    }

    [Test]
    public void AnEntry_WithoutAnyTeacher_SaysSo()
    {
        var building = ScriptableObject.CreateInstance<BuildingDefinition>();
        created.Add(building);

        Assert.AreEqual("Nobody here knows how yet.", HavenBuildings.HowToUnlock(building, null, null));
    }

    [Test]
    public void EveryBuildingThatStartsLocked_SaysHowToUnlockIt()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingDefinition"))
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (building.ShowInBuildMenu && !building.KnownFromStart)
            {
                Assert.AreNotEqual("Nobody here knows how yet.", HavenBuildings.HowToUnlock(building, null, content), building.name);
            }
        }
    }

    [Test]
    public void GoingToAPerson_ChoosesASpotBetweenThemAndThePlayer()
    {
        var target = new GameObject("Target");
        created.Add(target);

        Vector3 spot = HavenTravel.SpotBeside(target.transform, new Vector3(10f, 0f, 0f));

        Assert.Greater(spot.x, 0f);
        Assert.Less(spot.x, 10f);
    }

    private ItemDefinition Item(string name, ItemCategory category)
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        created.Add(item);
        item.name = name;
        var serialized = new SerializedObject(item);
        serialized.FindProperty("displayName").stringValue = name;
        serialized.FindProperty("category").enumValueIndex = (int)category;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return item;
    }

    private JobDefinition Job(string name)
    {
        var job = ScriptableObject.CreateInstance<JobDefinition>();
        created.Add(job);
        job.name = name;
        return job;
    }

    private ResidentDefinition Person(string name, params JobDefinition[] jobs)
    {
        var person = ScriptableObject.CreateInstance<ResidentDefinition>();
        created.Add(person);
        person.name = name;
        var serialized = new SerializedObject(person);
        SerializedProperty list = serialized.FindProperty("jobs");
        list.arraySize = jobs.Length;
        for (int i = 0; i < jobs.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = jobs[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        return person;
    }
}
