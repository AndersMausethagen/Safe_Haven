using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class PauseTests
{
    private readonly object ownerA = new object();
    private readonly object ownerB = new object();
    private readonly List<Func<bool>> registered = new List<Func<bool>>();

    [TearDown]
    public void TearDown()
    {
        GamePause.Release(ownerA);
        GamePause.Release(ownerB);
        GamePause.FreezesTime = true;
        Time.timeScale = 1f;
        foreach (Func<bool> handler in registered)
        {
            BackHandlers.Unregister(handler);
        }
    }

    [Test]
    public void PauseHoldsUntilEveryOwnerReleases()
    {
        GamePause.Request(ownerA);
        GamePause.Request(ownerB);
        Assert.IsTrue(GamePause.IsPaused);
        Assert.AreEqual(0f, Time.timeScale);

        GamePause.Release(ownerA);
        Assert.IsTrue(GamePause.IsPaused, "One owner must not unpause another.");

        GamePause.Release(ownerB);
        Assert.IsFalse(GamePause.IsPaused);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void ChangedFiresOnlyOnTransitions()
    {
        var events = new List<bool>();
        Action<bool> listener = events.Add;
        GamePause.Changed += listener;
        try
        {
            GamePause.Request(ownerA);
            GamePause.Request(ownerA);
            GamePause.Request(ownerB);
            GamePause.Release(ownerA);
            GamePause.Release(ownerB);
        }
        finally
        {
            GamePause.Changed -= listener;
        }

        CollectionAssert.AreEqual(new[] { true, false }, events);
    }

    [Test]
    public void PauseWithoutFreezingTimeKeepsWorldRunning()
    {
        GamePause.FreezesTime = false;
        GamePause.Request(ownerA);

        Assert.IsTrue(GamePause.IsPaused);
        Assert.IsFalse(GamePause.IsTimeFrozen);
        Assert.AreEqual(1f, Time.timeScale);
    }

    [Test]
    public void BackHandlersRunHighestPriorityFirstAndStopWhenUsed()
    {
        var calls = new List<string>();
        Register(BackHandlers.InteractionPriority, () => { calls.Add("interaction"); return true; });
        Register(BackHandlers.BuildPlacementPriority, () => { calls.Add("build"); return false; });
        Register(BackHandlers.WindowsPriority, () => { calls.Add("windows"); return true; });

        Assert.IsTrue(BackHandlers.TryHandle());
        CollectionAssert.AreEqual(new[] { "build", "windows" }, calls);
    }

    [Test]
    public void UnregisteredBackHandlerIsNotCalled()
    {
        bool called = false;
        Func<bool> handler = () => { called = true; return true; };
        Register(BackHandlers.WindowsPriority, handler);
        BackHandlers.Unregister(handler);

        BackHandlers.TryHandle();
        Assert.IsFalse(called);
    }

    private void Register(int priority, Func<bool> handler)
    {
        registered.Add(handler);
        BackHandlers.Register(priority, handler);
    }
}
