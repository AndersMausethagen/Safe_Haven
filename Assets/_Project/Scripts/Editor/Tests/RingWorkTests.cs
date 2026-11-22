using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Felling rings (RingOrder, RingWork; the Keeper's view, Docs/ColonyModeResearch_2026-11-21.md §4.6-4.7): who fells, what a ring holds,
/// how long a tree takes, felling while the player is away (capped by the trees last seen), and rings kept by a save.
/// </summary>
public sealed class RingWorkTests
{
    [Test]
    public void ARing_HoldsWhatIsInsideIt_AcrossTheGround()
    {
        var ring = new RingOrder(new Vector3(10f, 2f, 10f), 5f);
        Assert.IsTrue(ring.Contains(new Vector3(13f, 40f, 13f)), "height doesn't matter");
        Assert.IsFalse(ring.Contains(new Vector3(16f, 2f, 10f)));
        Assert.AreEqual(1f, new RingOrder(Vector3.zero, 0f).Radius, "never smaller than a metre");
    }

    [Test]
    public void OnlyTheWoodcutter_Fells_AndAFasterOneTakesLess()
    {
        WorkerSettings settings = WorkerSettings.Current;
        var carpenter = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Carpenter.asset");
        var cook = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Cook.asset");
        Assume.That(carpenter != null && cook != null);
        CollectionAssert.Contains(settings.Fellers, carpenter, "the shipped settings make the Carpenter the woodcutter");
        Assert.IsTrue(RingWork.Fells(new ResidentRecord(null, carpenter), settings));
        Assert.IsFalse(RingWork.Fells(new ResidentRecord(null, cook), settings));
        var paused = new ResidentRecord(null, carpenter) { WorkPaused = true };
        Assert.IsFalse(RingWork.Fells(paused, settings), "paused work fells nothing");
        Assert.Greater(RingWork.SecondsPerTree(1f, settings), RingWork.SecondsPerTree(2f, settings));
        Assert.IsNotNull(settings.Sapling, "the forester plants something");
    }

    [Test]
    public void AwayTheRingIsFelled_UpToTheTreesLastSeen_AndTheLogsComeHome()
    {
        WorkerSettings settings = WorkerSettings.Current;
        var carpenter = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/_Project/Data/Settlement/Carpenter.asset");
        var tor = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Tor.asset");
        Assume.That(carpenter != null && tor != null);
        var home = new SettlementState();
        var stash = new StashStorage();
        home.AddResident(tor, carpenter);
        var ring = new RingOrder(Vector3.zero, 10f) { StandingSeen = 3 };
        home.AddRing(ring);

        // Away they fell for a share of the time (working hours), as at home.
        float treeAway = RingWork.SecondsPerTree(1f, settings) / settings.AwayFellingShare;
        Assume.That(settings.AwayFellingShare > 0f);
        RingWork.WorkAway(home, stash, treeAway * 2.5f, null, settings);
        Assert.AreEqual(2, ring.PendingFells, "two trees in two and a half trees' time");
        RingWork.WorkAway(home, stash, treeAway * 10f, null, settings);
        Assert.AreEqual(3, ring.PendingFells, "never more than stood in it");
        int logs = 0;
        foreach (ItemAmount log in RingWork.Logs(settings.AverageTreeHeight, settings, () => 0.5f))
        {
            logs += stash.Items.Count(log.Item);
        }

        Assert.Greater(logs, 0, "the logs are in the chests");
        TreeChoppingSettings rules = TreeChoppingSettings.Current;
        int expected = rules.AllLogs(settings.FellerAxeTier, rules.SizeOf(settings.AverageTreeHeight));
        int one = 0;
        foreach (ItemAmount log in RingWork.Logs(settings.AverageTreeHeight, settings, () => 0.5f))
        {
            one += log.Amount;
        }

        Assert.AreEqual(expected, one, "a ring's tree gives what the player's does worked to the end (its size, not its metres)");
        Assert.Less(one, 12, "a tree of average height gives a handful of logs, not a cartload");

        var along = new RingOrder(Vector3.zero, 10f) { StandingSeen = 5 };
        var other = new SettlementState();
        other.AddResident(tor, carpenter);
        other.AddRing(along);
        RingWork.WorkAway(other, new StashStorage(), 1000f, tor, settings);
        Assert.AreEqual(0, along.PendingFells, "the woodcutter along on the raid fells nothing at home");
    }

    [Test]
    public void Rings_AreSaved()
    {
        var content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        var home = new SettlementState();
        home.AddRing(new RingOrder(new Vector3(4f, 1f, -7f), 12f) { PendingFells = 2, StandingSeen = 9 });
        SettlementState back = SaveMapper.SettlementFrom(JsonUtility.FromJson<SettlementSaveData>(JsonUtility.ToJson(SaveMapper.ToData(home))), content);
        Assert.AreEqual(1, back.Rings.Count);
        Assert.AreEqual(12f, back.Rings[0].Radius, 1e-3f);
        Assert.AreEqual(-7f, back.Rings[0].Centre.z, 1e-3f);
        Assert.AreEqual(2, back.Rings[0].PendingFells);
        Assert.AreEqual(9, back.Rings[0].StandingSeen);
    }
}
