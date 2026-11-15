using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Residents live in the houses the player builds (Docs/BuildingSystemResearch.md §3.4.1): the player's choice first, else
/// the nearest free bed to their work; anyone left over sleeps at the home spots by the portal. A house is where their bed
/// is, it is saved, and it reaches a co-op friend with the settlement.
/// </summary>
public sealed class HavenHomeTests
{
    private static HomeRules.House House(string id, float x, int beds) => new HomeRules.House(id, new Vector2(x, 0f), beds);

    private static HomeRules.Person Person(string id, float workX, string chosen = "") => new HomeRules.Person(id, chosen, new Vector2(workX, 0f));

    [Test]
    public void Everyone_SleepsInTheFreeBedNearestTheirWork()
    {
        var houses = new[] { House("far", 100f, 2), House("near", 10f, 2) };
        var people = new[] { Person("smith", 12f), Person("cook", 95f) };
        string[] homes = HomeRules.Assign(houses, people);
        Assert.AreEqual("near", homes[0], "the smith lives by the forge");
        Assert.AreEqual("far", homes[1], "the cook by the kitchen");
    }

    [Test]
    public void TheShortestWalks_AreSettledFirst_NotWhoeverArrivedFirst()
    {
        // One bed by the forge: the smith works beside it, the farmer only a little nearer it than the other house.
        var houses = new[] { House("forge", 0f, 1), House("field", 60f, 1) };
        var people = new[] { Person("farmer", 25f), Person("smith", 1f) };
        string[] homes = HomeRules.Assign(houses, people);
        Assert.AreEqual("forge", homes[1], "the smith has the bed beside the forge");
        Assert.AreEqual("field", homes[0], "the farmer takes the other");
    }

    [Test]
    public void ThePlayersChoice_WinsOverTheNearestBed()
    {
        var houses = new[] { House("near", 0f, 1), House("far", 80f, 1) };
        var people = new[] { Person("smith", 0f), Person("cook", 0f, chosen: "near") };
        string[] homes = HomeRules.Assign(houses, people);
        Assert.AreEqual("near", homes[1], "the cook was put there");
        Assert.AreEqual("far", homes[0], "the smith takes what is left");
    }

    [Test]
    public void ABedPromisedTwice_GoesToWhoeverArrivedFirst_AndTheOtherFindsTheirOwn()
    {
        var houses = new[] { House("hut", 0f, 1), House("cottage", 40f, 2) };
        var people = new[] { Person("a", 0f, chosen: "hut"), Person("b", 0f, chosen: "hut") };
        string[] homes = HomeRules.Assign(houses, people);
        Assert.AreEqual("hut", homes[0]);
        Assert.AreEqual("cottage", homes[1]);
    }

    [Test]
    public void WhenEveryBedIsTaken_TheRest_SleepByThePortal()
    {
        var houses = new[] { House("hut", 0f, 2) };
        var people = new[] { Person("a", 0f), Person("b", 1f), Person("c", 2f) };
        string[] homes = HomeRules.Assign(houses, people);
        Assert.AreEqual(2, homes.Count(home => home == "hut"));
        Assert.AreEqual(1, homes.Count(home => home == null), "one at the home spots, as before houses were lived in");
        Assert.IsNull(HomeRules.Assign(new HomeRules.House[0], people)[0], "no houses: everyone by the portal");
    }

    [Test]
    public void TheSameHousesAndPeople_GiveTheSameHomes()
    {
        // Every machine in co-op, and every load, works the same answer out: ties go by id, never by chance.
        var houses = new[] { House("b", 10f, 1), House("a", -10f, 1) };
        var people = new[] { Person("one", 0f), Person("two", 0f) };
        string[] first = HomeRules.Assign(houses, people);
        for (int i = 0; i < 5; i++)
        {
            CollectionAssert.AreEqual(first, HomeRules.Assign(houses, people));
        }

        Assert.AreEqual("a", first[0], "an equal walk goes to the house with the lower id");
    }

    [Test]
    public void AHouseIsABed_AndItIsSaved_AndAFriendGetsIt()
    {
        ResidentDefinition bjarne = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Bjarne.asset");
        ResidentDefinition other = AssetDatabase.FindAssets("t:ResidentDefinition").Select(guid => AssetDatabase.LoadAssetAtPath<ResidentDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(one => one != null && one != bjarne);
        var settlement = new SettlementState();
        settlement.AddResident(bjarne);
        settlement.AddResident(other);
        settlement.NoteBuildings(new BuildingObject[0]);
        Assert.IsFalse(settlement.HasBed(settlement.Find(bjarne)), "no beds yet");

        ResidentRecord record = settlement.Find(bjarne);
        Assert.IsTrue(settlement.SetHome(record, "spawned-hut", "spawned-smithy", "spawned-smithy"));
        Assert.IsFalse(settlement.SetHome(record, "spawned-hut", "spawned-smithy", "spawned-smithy"), "nothing changed");
        Assert.IsTrue(settlement.HasBed(record), "a house is a bed of their own");
        Assert.IsFalse(settlement.HasBed(settlement.Find(other)), "once homes are known, no house means no bed");
        Assert.AreEqual(1, settlement.Housed);
        settlement.ChooseHome(record, "spawned-cottage");

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(settlement))), content);
        ResidentRecord loaded = back.Find(bjarne);
        Assert.AreEqual("spawned-hut", loaded.Home);
        Assert.AreEqual("spawned-cottage", loaded.HomeChosen);
        Assert.AreEqual("spawned-smithy", loaded.Workplace);
        Assert.AreEqual("spawned-smithy", loaded.WorkQueue);

        // A friend's copy takes the host's settlement in place (CoopTests): the homes come with it.
        var friends = new SettlementState();
        friends.TakeOn(back);
        Assert.AreEqual("spawned-hut", friends.Find(bjarne).Home);
    }

    [Test]
    public void ChoosingAHome_IsAHavenDecision()
    {
        Assert.IsTrue(HavenDecisions.All.ContainsKey("resident.home"), "made where the Haven is kept, so a visitor's choice goes to the host");
    }

    [Test]
    public void TheyWorkTheQueue_OfTheStationNearestTheirHome()
    {
        JobDefinition smith = AssetDatabase.FindAssets("t:JobDefinition").Select(guid => AssetDatabase.LoadAssetAtPath<JobDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
            .First(job => job != null && job.name == "Smith");
        ResidentDefinition bjarne = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Bjarne.asset");
        var settlement = new SettlementState();
        settlement.AddResident(bjarne, smith);
        CraftingStationType anvil = smith.Workplace.Station;
        StationQueue first = settlement.Workshops.GetOrCreate("smithy-a", anvil, 10, 5);
        StationQueue second = settlement.Workshops.GetOrCreate("smithy-b", anvil, 10, 5);
        ResidentRecord record = settlement.Find(bjarne);
        Assert.AreEqual(first, WorkerProduction.QueueOf(settlement, record), "before it is known, the first Smithy (as before)");

        settlement.SetHome(record, "hut", "smithy-b", "smithy-b");
        Assert.AreEqual(second, WorkerProduction.QueueOf(settlement, record), "the second Smithy, by his house");

        settlement.SetHome(record, "hut", "gone", "gone");
        Assert.AreEqual(first, WorkerProduction.QueueOf(settlement, record), "a queue that isn't there: back to the first");
    }
}
