using NUnit.Framework;
using UnityEngine;

/// <summary>
/// LeaseWatch (Docs/BugPatterns.md, second round): what is held and never let go is told once, when it is held far too long or by
/// something gone, and a hold let go is forgotten.
/// </summary>
public sealed class LeaseWatchTests
{
    [Test]
    public void AHoldFarTooLong_IsToldOnce_AndOneLetGoIsForgotten()
    {
        var watch = new LeaseWatch();
        var creature = new object();
        var hop = new object();

        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 0f);
        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 1f);
        Assert.AreEqual(0, Found(watch), "a hop's moment is no leak");

        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 25f);
        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 30f);
        Assert.AreEqual(1, Found(watch), "held 25 s: told, and once");

        watch.Look(31f); // the game holds nothing of it: let go, and forgotten
        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 32f);
        Assert.AreEqual(0, Found(watch), "held again, it counts from its own start");
    }

    [Test]
    public void AHoldByOneGone_IsToldAtOnce_WhateverItsKind()
    {
        var watch = new LeaseWatch();
        var window = new GameObject("A window, closed and destroyed");
        Object.DestroyImmediate(window);
        watch.Judge(LeaseWatch.Kind.GameplayKeysOff, typeof(GameInput), new object[] { window }, 0f);
        Assert.AreEqual(1, Found(watch), "the keys held off by something gone: they never come back");
    }

    [Test]
    public void AWindowOpenLong_IsNoLeak()
    {
        var watch = new LeaseWatch();
        var window = new GameObject("The journal, open");
        try
        {
            watch.Judge(LeaseWatch.Kind.GameplayKeysOff, typeof(GameInput), new object[] { window }, 0f);
            watch.Judge(LeaseWatch.Kind.GameplayKeysOff, typeof(GameInput), new object[] { window }, 900f);
            Assert.AreEqual(0, Found(watch), "reading the journal for a quarter of an hour is allowed");
        }
        finally
        {
            Object.DestroyImmediate(window);
        }
    }

    // What Judge has found so far of invulnerability and keys held off, read without Look (which looks at the running game, where
    // other tests may have left things).
    private static int Found(LeaseWatch watch) =>
        ((System.Collections.Generic.List<string>)typeof(LeaseWatch).GetField("found", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .GetValue(watch)).FindAll(leak => leak.Contains(" on Object ") || leak.Contains(" on GameInput ")).Count;
}
