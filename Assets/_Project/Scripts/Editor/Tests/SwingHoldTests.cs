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

    [Test]
    public void ALunge_EasesToAStop_CoveringTheSameGround()
    {
        float sum = 0f;
        const int steps = 1000;
        for (int i = 0; i < steps; i++)
        {
            sum += PlayerMovement.LungeShape((i + 0.5f) / steps) / steps;
        }

        Assert.AreEqual(1f, sum, 0.01f, "the same distance as a flat push");
        Assert.AreEqual(0f, PlayerMovement.LungeShape(1f), 1e-4f, "nothing left at its end: no dead stop");
        Assert.Greater(PlayerMovement.LungeShape(0f), PlayerMovement.LungeShape(0.5f), "quick first, easing off");
    }

    [Test]
    public void AnEasedLunge_CoversItsWayExactly_AtAnyFrameRate()
    {
        foreach (float fps in new[] { 24f, 30f, 60f, 144f })
        {
            // 0.12 s, as a charged blow's step: frames that don't divide it evenly, the last one past its end.
            const float seconds = 0.12f;
            float dt = 1f / fps, covered = 0f, firstFrame = 0f;
            for (float t = dt; t - dt < seconds; t += dt)
            {
                float part = PlayerMovement.EasedLungeCovered((t - dt) / seconds, t / seconds);
                if (firstFrame == 0f)
                {
                    firstFrame = part;
                }

                covered += part;
            }

            Assert.AreEqual(1f, covered, 1e-4f, $"at {fps} fps it goes exactly its distance (a sample a frame overshot by a fifth at 30)");
            Assert.Greater(firstFrame, dt / seconds, $"at {fps} fps the first frame moves more than a flat push would: quick, then easing");
        }
    }
}
