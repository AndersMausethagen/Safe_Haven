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
        // Named, so what Look adds from the running game (other tests' leftovers) is told apart from this test's own.
        var creature = new LeaseKey("a test's creature");
        var hop = new object();

        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 0f);
        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 1f);
        Assert.AreEqual(0, watch.Found.Count, "a hop's moment is no leak");

        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 25f);
        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 30f);
        Assert.AreEqual(1, watch.Found.Count, "held 25 s: told, and once");

        watch.Look(31f); // the game holds nothing of it: let go, and forgotten
        watch.Judge(LeaseWatch.Kind.Invulnerable, creature, new[] { hop }, 32f);
        Assert.AreEqual(0, Of(watch, creature), "held again, it counts from its own start");
    }

    [Test]
    public void AHoldByOneGone_IsToldAtOnce_WhateverItsKind()
    {
        var watch = new LeaseWatch();
        var window = new GameObject("A window, closed and destroyed");
        Object.DestroyImmediate(window);
        watch.Judge(LeaseWatch.Kind.GameplayKeysOff, typeof(GameInput), new object[] { window }, 0f);
        Assert.AreEqual(1, watch.Found.Count, "the keys held off by something gone: they never come back");
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
            Assert.AreEqual(0, watch.Found.Count, "reading the journal for a quarter of an hour is allowed");
        }
        finally
        {
            Object.DestroyImmediate(window);
        }
    }

    [Test]
    public void AHolder_IsToldByWhatItIs_NotOnlyWhereItIs()
    {
        // The playtests' Stuck rows read "held by Object" (80) and "held by PlayerArmature" (31): nothing to go on.
        var watch = new LeaseWatch();
        var player = new GameObject("PlayerArmature");
        try
        {
            Component part = player.AddComponent<BoxCollider>();
            var key = new LeaseKey("a finishing blow");
            var body = new object();
            watch.Judge(LeaseWatch.Kind.Invulnerable, body, new object[] { part, key }, 0f);
            watch.Judge(LeaseWatch.Kind.Invulnerable, body, new object[] { part, key }, 30f);
            var found = new System.Collections.Generic.List<string>(watch.Found);
            Assert.IsTrue(found.Exists(leak => leak.Contains("held by PlayerArmature's BoxCollider")), string.Join("\n", found));
            Assert.IsTrue(found.Exists(leak => leak.Contains("held by a finishing blow")), string.Join("\n", found));
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    // What this test's own holder was found holding (Look also reads the running game, where other tests may have left things).
    private static int Of(LeaseWatch watch, LeaseKey holder) =>
        System.Linq.Enumerable.Count(watch.Found, leak => leak.Contains(" on " + holder + " "));
}
