using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// An empty house for someone lost (EmptyHouses; the Keeper's view, the people): the house is kept with their bed while the forest has
/// them, saved, and theirs again when they are found.
/// </summary>
public sealed class EmptyHousesTests
{
    [Test]
    public void TheHouseOfSomeoneLost_KeepsTheirBed_AndIsTheirsAgainWhenFound()
    {
        var tor = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Tor.asset");
        var carpenter = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Carpenter.asset");
        Assume.That(tor != null && carpenter != null);
        var home = new SettlementState();
        home.AddResident(tor, carpenter);
        home.Find(tor).Home = "hut-7";
        Assert.IsNull(EmptyHouses.WaitingFor(home, "hut-7"), "nobody lost yet");

        home.LoseResident(tor, "Misty Woods");
        Assert.AreEqual(tor, EmptyHouses.WaitingFor(home, "hut-7").Resident, "the hut waits for them");
        Assert.AreEqual(1, EmptyHouses.BedsKept(home, "hut-7"), "their bed is kept");
        Assert.AreEqual(0, EmptyHouses.BedsKept(home, "hut-8"));
        StringAssert.Contains("Misty Woods", EmptyHouses.Tell(EmptyHouses.WaitingFor(home, "hut-7")));

        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(home))), content);
        Assert.AreEqual(1, EmptyHouses.BedsKept(back, "hut-7"), "kept by a save");

        Assert.IsTrue(back.FindAgain(tor));
        Assert.AreEqual("hut-7", back.Find(tor).Home, "found again, they come home to it");
        Assert.AreEqual(0, EmptyHouses.BedsKept(back, "hut-7"));
    }

    [Test]
    public void AHouse_IsKeptOnlyForSoManyRaids()
    {
        var tor = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Tor.asset");
        var carpenter = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Carpenter.asset");
        int keep = SettlementSettings.Current.KeepHouseRaids;
        Assume.That(tor != null && carpenter != null && keep > 0);
        var home = new SettlementState();
        home.AddResident(tor, carpenter);
        home.Find(tor).Home = "hut-7";
        home.LoseResident(tor, "Misty Woods");
        home.Restore(true, 0, keep - 1, BreachStatus.Dormant, 0, 0f);
        Assert.AreEqual(1, EmptyHouses.BedsKept(home, "hut-7"), "still kept a raid before the end");
        home.Restore(true, 0, keep, BreachStatus.Dormant, 0, 0f);
        Assert.AreEqual(0, EmptyHouses.BedsKept(home, "hut-7"), "then the bed goes to whoever needs it");
        Assert.IsNull(EmptyHouses.WaitingFor(home, "hut-7"));
    }
}
