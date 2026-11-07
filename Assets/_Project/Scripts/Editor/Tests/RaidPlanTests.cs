using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>RaidPlan, the choices made at the portal before a raid, without the planner window.</summary>
public sealed class RaidPlanTests
{
    private const string MistyWoodsPath = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string TrollValleyPath = "Assets/_Project/Data/Raids/TrollValley.asset";

    // What the plan asked to have laid out ahead, instead of building the maps.
    private readonly List<(RaidDefinition raid, int seed, bool large)> laidOut = new List<(RaidDefinition, int, bool)>();
    private ItemDefinition rune;
    private GameObject portalHost;
    private RaidDefinition mistyWoods;
    private RaidDefinition trollValley;

    [SetUp]
    public void SetUp()
    {
        GameSession.StartNewGame();
        rune = ScriptableObject.CreateInstance<ItemDefinition>();
        mistyWoods = AssetDatabase.LoadAssetAtPath<RaidDefinition>(MistyWoodsPath);
        trollValley = AssetDatabase.LoadAssetAtPath<RaidDefinition>(TrollValleyPath);
        laidOut.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(rune);
        if (portalHost != null)
        {
            Object.DestroyImmediate(portalHost);
        }

        GameSession.StartNewGame();
    }

    private RaidPlan NewPlan() => new RaidPlan((raid, seed, large) => laidOut.Add((raid, seed, large)));

    private RaidPortal Portal(params RaidDefinition[] destinations)
    {
        portalHost = new GameObject("Portal");
        var portal = portalHost.AddComponent<RaidPortal>();
        var so = new SerializedObject(portal);
        so.FindProperty("raid").objectReferenceValue = destinations[0];
        SerializedProperty others = so.FindProperty("otherDestinations");
        others.arraySize = destinations.Length - 1;
        for (int i = 1; i < destinations.Length; i++)
        {
            others.GetArrayElementAtIndex(i - 1).objectReferenceValue = destinations[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return portal;
    }

    [Test]
    public void EachRuneKindIsUsedAtMostOnce()
    {
        RaidPlan plan = NewPlan();
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
        RaidPlan plan = NewPlan();
        plan.Toggle(rune);
        plan.ForgetMissing(new List<ItemDefinition>());
        Assert.IsFalse(plan.IsPicked(rune));
    }

    [Test]
    public void SparksStayWithinTheLimit()
    {
        RaidPlan plan = NewPlan();
        plan.StepSparks(-1);
        Assert.AreEqual(0, plan.Sparks);
        plan.StepSparks(1000);
        Assert.AreEqual(SettlementSettings.Current.MaxSparksPerRaid, plan.Sparks);
    }

    [Test]
    public void NoPortalMeansNoDestinationAndNoLaunch()
    {
        RaidPlan plan = NewPlan();
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
        RaidPlan plan = NewPlan();
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

    [Test]
    public void APlan_StartsOnTheFirstDestination_AndLaysItOutAhead()
    {
        RaidPlan plan = NewPlan();
        plan.Begin(Portal(mistyWoods, trollValley), null);
        Assert.AreSame(mistyWoods, plan.Destination);
        Assert.AreEqual(0, plan.DestinationIndex);
        Assert.AreEqual(1, laidOut.Count);
        Assert.AreEqual((mistyWoods, plan.Seed, plan.Large), laidOut[0], "The run the briefing describes is the one laid out");
    }

    [Test]
    public void ChangingTheSize_LaysTheRunOutAgain()
    {
        Assume.That(mistyWoods.CanBeLarge, "Misty Woods offers a large map");
        RaidPlan plan = NewPlan();
        plan.Begin(Portal(mistyWoods), null);
        bool wasLarge = plan.Large;
        plan.ToggleSize();
        Assert.AreEqual(!wasLarge, plan.Large);
        Assert.AreEqual((mistyWoods, plan.Seed, !wasLarge), laidOut[laidOut.Count - 1], "Same seed, the other size");
    }

    [Test]
    public void TheTier_IsWhatTheGearIsReadyFor_UntilOneIsPicked()
    {
        RaidPlan plan = NewPlan();
        plan.Begin(Portal(mistyWoods, trollValley), null);
        plan.Settle();
        Assert.AreSame(mistyWoods.DifficultyFor(0), plan.Difficulty, "No gear: the tier for none");

        RaidDifficulty highest = mistyWoods.Difficulties[mistyWoods.Difficulties.Count - 1];
        plan.PickTier(highest);
        plan.Settle();
        Assert.AreSame(highest, plan.Difficulty, "A picked tier stays picked, even when the gear isn't ready (the launch says why)");

        plan.PickDestination(1);
        Assert.AreSame(trollValley, plan.Destination);
        Assert.IsNull(plan.Difficulty, "Another place starts on its own tier");
        Assert.AreSame(trollValley, laidOut[laidOut.Count - 1].raid, "and its own run is laid out ahead");
    }

    [Test]
    public void APlaceStillLocked_IsReadAboutButTakesNoSparks()
    {
        Assume.That(!Cheats.NoRequirements, "The cheat menu opens locked places");
        Assume.That(SettlementSettings.Current.PortalSpark != null && SettlementSettings.Current.MaxSparksPerRaid > 0, "Sparks are offered");
        RaidPlan plan = NewPlan();
        plan.Begin(Portal(mistyWoods, trollValley), null);
        plan.StepSparks(1);
        plan.Settle();
        Assert.IsTrue(plan.SparksUsable);
        Assert.AreEqual(1, plan.Sparks, "An open place keeps the Sparks put on it");

        plan.PickDestination(1);
        plan.Settle();
        Assert.IsTrue(plan.IsClosed, "Troll Valley waits for its boss");
        Assert.IsFalse(plan.SparksUsable);
        Assert.AreEqual(0, plan.Sparks);
    }

    [Test]
    public void ALargeMap_AddsItsExtraTimeToTheRules()
    {
        Assume.That(mistyWoods.CanBeLarge, "Misty Woods offers a large map");
        RaidPlan plan = NewPlan();
        plan.Begin(Portal(mistyWoods), null);
        plan.Settle();
        if (plan.Large)
        {
            plan.ToggleSize();
        }

        RaidTimeOfDay hour = plan.TimeOfDay;
        float normal = plan.Rules(hour).Duration;
        plan.ToggleSize();
        Assert.AreEqual(normal + mistyWoods.LargeRaid.extraSeconds, plan.Rules(hour).Duration, 0.01f);
    }
}
