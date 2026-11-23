using NUnit.Framework;
using UnityEngine;

/// <summary>
/// A recovered swing waits on its last pose so the next press still chains (the user, 2026-11-23: "hold the last part of a swing for a
/// bit if no other button is pressed ... rolling or running should immediately cancel the swing"): SwingMotion.HoldsPose.
/// </summary>
public sealed class SwingHoldTests
{
    private const float Hold = 0.4f;

    private static bool Holds(SwingKind kind, float heldFor, Vector3 move = default, bool charged = false, bool attackHeld = false, bool guarding = false,
        bool dodging = false, float hold = Hold) =>
        SwingMotion.HoldsPose(kind, charged, heldFor, hold, attackHeld, move, guarding, dodging);

    [Test]
    public void AChainSwing_WaitsOnItsLastPose_ForTheHoldOnly()
    {
        Assert.IsTrue(Holds(SwingKind.Light, 0f));
        Assert.IsTrue(Holds(SwingKind.Light, 0.39f));
        Assert.IsFalse(Holds(SwingKind.Light, 0.4f), "the wait ends after Swing Hold Seconds");
        Assert.IsTrue(Holds(SwingKind.Rush, 0.1f), "a rush leads into the chain too");
        Assert.IsTrue(Holds(SwingKind.Heavy, 0.1f));
    }

    [Test]
    public void MovingRollingOrGuarding_EndsItAtOnce()
    {
        Assert.IsFalse(Holds(SwingKind.Light, 0.1f, Vector3.forward), "a move (walking or running) ends it");
        Assert.IsTrue(Holds(SwingKind.Light, 0.1f, Vector3.forward * 0.1f), "a stick resting near the middle doesn't");
        Assert.IsFalse(Holds(SwingKind.Light, 0.1f, dodging: true));
        Assert.IsFalse(Holds(SwingKind.Light, 0.1f, guarding: true));
        Assert.IsFalse(Holds(SwingKind.Light, 0.1f, attackHeld: true), "a held button is the heavy's business");
    }

    [Test]
    public void OtherBlows_DontWait()
    {
        Assert.IsFalse(Holds(SwingKind.Heavy, 0.1f, charged: true), "a charged blow is the end of its chain");
        Assert.IsFalse(Holds(SwingKind.Art, 0.1f));
        Assert.IsFalse(Holds(SwingKind.ShieldBash, 0.1f));
        Assert.IsFalse(Holds(SwingKind.Light, 0f, hold: 0f), "a hold of 0 turns it off");
    }
}
