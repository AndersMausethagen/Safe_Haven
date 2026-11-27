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
        GamePause.TimeIsShared = false;
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
    public void HitStopLeavesSharedTimeAlone()
    {
        // Hit-stop on for the test whatever this machine's options say, and the player's choice put back after.
        float hitStopBefore = GameOptions.HitStop.Value;
        GameOptions.HitStop.Value = 1f;
        // Not initialised: that would subscribe to game-wide events, and edit mode never calls OnDestroy to let go.
        var feedback = new GameObject("feedback").AddComponent<CombatFeedback>();
        try
        {
            Assert.IsTrue(GameOptions.HitStop.On);
            // Hosting friends in co-op: the world they fight in runs on this game's time.
            GamePause.TimeIsShared = true;
            feedback.SlowTime(0.05f, 1f);
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsFalse(feedback.IsHitStopped);

            GamePause.TimeIsShared = false;
            feedback.SlowTime(0.05f, 1f);
            Assert.AreEqual(0.05f, Time.timeScale, 1e-4f, "alone, a strong hit still stops time");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(feedback.gameObject);
            GameOptions.HitStop.Value = hitStopBefore;
        }
    }

    [Test]
    public void AStopInsideASlow_StopsOnlyForItself_ThenTheSlowGoesOn()
    {
        float hitStopBefore = GameOptions.HitStop.Value;
        GameOptions.HitStop.Value = 1f;
        var feedback = new GameObject("feedback").AddComponent<CombatFeedback>();
        try
        {
            float now = Time.unscaledTime;
            // An ultimate's slow beat, then the player is struck: a hit stop inside it.
            feedback.SlowTime(0.3f, 0.45f);
            feedback.SlowTime(0.05f, 0.07f);
            Assert.AreEqual(0.05f, Time.timeScale, 1e-4f, "the stop stops time");

            feedback.EndStopsDueBy(now + 0.1f);
            Assert.AreEqual(0.3f, Time.timeScale, 1e-4f, "once the stop is over the slow goes on: not a freeze for the rest of it");
            Assert.IsTrue(feedback.IsHitStopped);

            feedback.EndStopsDueBy(now + 0.1f + 0.45f);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "and it ends");

            // A gentler, longer slow asked during a stop follows the stop.
            feedback.SlowTime(0.05f, 0.07f);
            feedback.SlowTime(0.3f, 0.45f);
            Assert.AreEqual(0.05f, Time.timeScale, 1e-4f);
            feedback.EndStopsDueBy(Time.unscaledTime + 0.1f);
            Assert.AreEqual(0.3f, Time.timeScale, 1e-4f, "the slow comes after the stop");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(feedback.gameObject);
            GameOptions.HitStop.Value = hitStopBefore;
        }
    }

    [Test]
    public void AnEasedSlow_ComesBackToFullSpeedAsItRuns()
    {
        float hitStopBefore = GameOptions.HitStop.Value;
        GameOptions.HitStop.Value = 1f;
        var feedback = new GameObject("feedback").AddComponent<CombatFeedback>();
        try
        {
            float now = Time.unscaledTime;
            // A milestone's held breath (ExperienceHud), through the one owner of slow time.
            feedback.SlowTime(0.3f, 0.9f, easeOut: true);
            Assert.AreEqual(0.3f, Time.timeScale, 1e-4f);
            feedback.EndStopsDueBy(now + 0.6f);
            Assert.Greater(Time.timeScale, 0.3f, "easing back");
            Assert.Less(Time.timeScale, 1f);
            feedback.EndStopsDueBy(now + 1f);
            Assert.AreEqual(1f, Time.timeScale, 1e-4f, "and full speed once it is over");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(feedback.gameObject);
            GameOptions.HitStop.Value = hitStopBefore;
        }
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
