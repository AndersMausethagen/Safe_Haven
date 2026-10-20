using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode tests for the state of Safe Haven itself (SettlementState): residents and jobs, the Purification Altar's
/// queue, and the Changed event the scene objects listen to.
/// </summary>
public sealed class SettlementTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object asset in created)
        {
            Object.DestroyImmediate(asset);
        }

        created.Clear();
    }

    [Test]
    public void Residents_MoveInOnlyOnce()
    {
        var settlement = new SettlementState();
        ResidentDefinition ingrid = Create<ResidentDefinition>("ingrid");

        Assert.IsTrue(settlement.AddResident(ingrid));
        Assert.IsFalse(settlement.AddResident(ingrid));
        Assert.AreEqual(1, settlement.Residents.Count);
    }

    [Test]
    public void Jobs_CountOnceAssigned()
    {
        var settlement = new SettlementState();
        ResidentDefinition ingrid = Create<ResidentDefinition>("ingrid");
        JobDefinition alchemist = Create<JobDefinition>("alchemist");
        settlement.AddResident(ingrid);

        Assert.IsFalse(settlement.HasWorker(alchemist));
        settlement.AssignJob(ingrid, alchemist);
        Assert.IsTrue(settlement.HasWorker(alchemist));
    }

    [Test]
    public void Purification_QueueIsCapped()
    {
        var settlement = new SettlementState();

        Assert.AreEqual(3, settlement.QueuePurification(5, 3));
        Assert.AreEqual(0, settlement.QueuePurification(1, 3));
        Assert.AreEqual(3, settlement.PurificationQueued);
    }

    [Test]
    public void Purification_FinishesBatchesAndKeepsLeftoverTime()
    {
        var settlement = new SettlementState();
        settlement.QueuePurification(3, 10);

        Assert.AreEqual(0, settlement.TickPurification(15f, 20f));
        Assert.AreEqual(2, settlement.TickPurification(35f, 20f));

        Assert.AreEqual(1, settlement.PurificationQueued);
        Assert.AreEqual(10f, settlement.PurificationProgress, 0.001f);
    }

    [Test]
    public void Purification_ResetsProgressWhenTheQueueEmpties()
    {
        var settlement = new SettlementState();
        settlement.QueuePurification(1, 10);

        Assert.AreEqual(1, settlement.TickPurification(50f, 20f));

        Assert.AreEqual(0, settlement.PurificationQueued);
        Assert.AreEqual(0f, settlement.PurificationProgress);
        Assert.AreEqual(0, settlement.TickPurification(50f, 20f), "Nothing to purify.");
    }

    [Test]
    public void Changed_IsRaisedOnlyForRealChanges()
    {
        var settlement = new SettlementState();
        int changes = 0;
        settlement.Changed += () => changes++;

        settlement.RepairPortal();
        settlement.RepairPortal();
        settlement.SetBreach(BreachStatus.Pending);
        settlement.SetBreach(BreachStatus.Pending);
        settlement.QueuePurification(1, 5);
        settlement.TickPurification(5f, 20f);

        Assert.AreEqual(3, changes, "Repair, breach and queue each once; an unfinished batch is not a change.");
    }

    private T Create<T>(string name) where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        asset.name = name;
        created.Add(asset);
        return asset;
    }
}
