using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>RaidPlan, the choices made at the portal before a raid, without the planner window.</summary>
public sealed class RaidPlanTests
{
    private ItemDefinition rune;

    [SetUp]
    public void SetUp()
    {
        GameSession.StartNewGame();
        rune = ScriptableObject.CreateInstance<ItemDefinition>();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(rune);
        GameSession.StartNewGame();
    }

    [Test]
    public void EachRuneKindIsUsedAtMostOnce()
    {
        var plan = new RaidPlan();
        plan.Toggle(rune);
        Assert.IsTrue(plan.IsPicked(rune));
        Assert.AreEqual(1, plan.Picked.Count);
        plan.Toggle(rune);
        Assert.IsFalse(plan.IsPicked(rune), "A second click puts it back");
        Assert.IsFalse(plan.IsPicked(null));
    }

    [Test]
    public void RunesNoLongerInTheBagAreForgotten()
    {
        var plan = new RaidPlan();
        plan.Toggle(rune);
        plan.ForgetMissing(new System.Collections.Generic.List<ItemDefinition>());
        Assert.IsFalse(plan.IsPicked(rune));
    }

    [Test]
    public void SparksStayWithinTheLimit()
    {
        var plan = new RaidPlan();
        plan.StepSparks(-1);
        Assert.AreEqual(0, plan.Sparks);
        plan.StepSparks(1000);
        Assert.AreEqual(SettlementSettings.Current.MaxSparksPerRaid, plan.Sparks);
    }

    [Test]
    public void NoPortalMeansNoDestinationAndNoLaunch()
    {
        var plan = new RaidPlan();
        plan.Settle();
        Assert.IsNull(plan.Destination);
        Assert.IsFalse(plan.SparksUsable);
        Assert.IsFalse(plan.TryLaunch(out _));
    }

    [Test]
    public void CompanionsCycleThroughGoingAloneAndDropOnceGone()
    {
        var sigrid = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Sigrid.asset");
        Assert.IsNotNull(sigrid.RaidCompanion);
        GameSession.Settlement.AddResident(sigrid);
        var plan = new RaidPlan();
        plan.Settle();
        CollectionAssert.AreEqual(new[] { sigrid }, plan.Companions);
        Assert.IsNull(plan.Companion, "Going alone until someone is asked");
        plan.StepCompanion(1);
        Assert.AreSame(sigrid, plan.Companion);
        plan.StepCompanion(1);
        Assert.IsNull(plan.Companion, "Going alone is one of the places in the list");
        plan.StepCompanion(-1);
        Assert.AreSame(sigrid, plan.Companion);

        GameSession.Settlement.LoseResident(sigrid, "The Misty Woods");
        plan.Settle();
        Assert.IsNull(plan.Companion, "Someone lost can't be asked along");
        Assert.AreEqual(0, plan.Companions.Count);
    }
}
