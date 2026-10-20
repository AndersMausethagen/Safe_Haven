using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Edit Mode tests for hunger and thirst (GDD §11): the meters, the settings' helper numbers and eating.</summary>
public sealed class SurvivalTests
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
    public void Meter_StartsAtTheGivenShareOfItsMaximum()
    {
        var meter = new NeedMeter(100f, 0.7f);

        Assert.AreEqual(70f, meter.Current, 0.001f);
        Assert.AreEqual(0.7f, meter.Fraction, 0.001f);
    }

    [Test]
    public void Meter_DrainStopsAtZero()
    {
        var meter = new NeedMeter(100f, 0.5f);

        Assert.IsTrue(meter.Drain(80f));
        Assert.AreEqual(0f, meter.Current);
        Assert.IsTrue(meter.IsEmpty);
        Assert.IsFalse(meter.Drain(1f), "An empty meter can't drain further.");
    }

    [Test]
    public void Meter_RestoreStopsAtTheMaximumAndReportsWhatItGained()
    {
        var meter = new NeedMeter(100f, 0.9f);

        Assert.AreEqual(10f, meter.Restore(25f), 0.001f);
        Assert.IsTrue(meter.IsFull);
        Assert.AreEqual(0f, meter.Restore(5f));
    }

    [Test]
    public void Meter_IgnoresNegativeAmountsAndClampsSetValues()
    {
        var meter = new NeedMeter(100f, 0.5f);

        Assert.IsFalse(meter.Drain(-5f));
        Assert.AreEqual(0f, meter.Restore(-5f));
        meter.Set(500f);
        Assert.AreEqual(100f, meter.Current);
        meter.Set(-3f);
        Assert.AreEqual(0f, meter.Current);
    }

    [Test]
    public void Settings_MinutesToEmptyIncludeTheRaidMultiplier()
    {
        Assert.AreEqual(40f, SurvivalSettings.MinutesToEmpty(100f, 2.5f), 0.001f);
        Assert.AreEqual(16f, SurvivalSettings.MinutesToEmpty(100f, 2.5f, 2.5f), 0.001f);
        Assert.IsTrue(float.IsPositiveInfinity(SurvivalSettings.MinutesToEmpty(100f, 0f)));
    }

    [Test]
    public void Eating_RestoresBothMeters()
    {
        PlayerNeeds needs = CreateNeeds();
        needs.Restore(20f, 30f);

        Assert.IsTrue(needs.TryConsume("Stewed Berries", 25f, 10f));

        Assert.AreEqual(45f, needs.Hunger.Current, 0.001f);
        Assert.AreEqual(40f, needs.Thirst.Current, 0.001f);
    }

    [Test]
    public void Eating_IsRefusedWhenItWouldNotHelp()
    {
        PlayerNeeds needs = CreateNeeds();
        needs.Restore(needs.Hunger.Max, 10f);

        Assert.IsFalse(needs.TryConsume("Berries", 10f, 0f), "Food isn't wasted on a full stomach.");
        Assert.IsTrue(needs.TryConsume("Water", 0f, 10f), "Drinking still helps while thirsty.");
    }

    private PlayerNeeds CreateNeeds()
    {
        var player = new GameObject("Player");
        created.Add(player);
        return player.AddComponent<PlayerNeeds>();
    }
}
