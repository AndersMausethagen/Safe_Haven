using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Blood tints made ahead of the first blow (Docs/QualityLevers_2026-11-24.md §2): a colour asked for is tinted by WarmOne, one a
/// frame, so the hit itself doesn't pay for the pass over the atlas.
/// </summary>
public sealed class BloodTintWarmTests
{
    [SetUp]
    [TearDown]
    public void Forget() => BloodDecals.ForgetForTests();

    [Test]
    public void APreparedColour_IsTintedByTheWarmUp_OneAFrame()
    {
        Assert.IsTrue(RenderingSettings.Current.BloodAtlas != null && RenderingSettings.Current.BloodAtlas.isReadable,
            "the shipped settings draw blood as decals");
        // Colours no creature uses, so another test's tint can't stand in.
        var first = new Color(0.123f, 0.456f, 0.789f, 1f);
        var second = new Color(0.321f, 0.654f, 0.987f, 1f);
        Assert.IsFalse(BloodDecals.IsTinted(first));

        BloodDecals.Prepare(first);
        BloodDecals.Prepare(second);
        BloodDecals.Prepare(first); // asked twice: queued once
        Assert.IsFalse(BloodDecals.IsTinted(first), "asking only queues it");

        BloodDecals.WarmOne();
        Assert.IsTrue(BloodDecals.IsTinted(first));
        Assert.IsFalse(BloodDecals.IsTinted(second), "one colour a frame");

        BloodDecals.WarmOne();
        Assert.IsTrue(BloodDecals.IsTinted(second));
        Assert.AreSame(BloodDecals.MaterialFor(first), BloodDecals.MaterialFor(first), "made once, then shared");
    }
}
