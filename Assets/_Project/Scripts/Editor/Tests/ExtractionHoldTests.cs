using NUnit.Framework;

/// <summary>Holding an extraction point: only inside and uncontested fills it, nothing but going dark resets it.</summary>
public sealed class ExtractionHoldTests
{
    [Test]
    public void Unlit_NeverFills()
    {
        var hold = new ExtractionHold();
        Assert.AreEqual(ExtractionHoldState.Unlit, hold.Tick(5f, true, false));
        Assert.AreEqual(0f, hold.Held);
    }

    [Test]
    public void FillsWhileInside_AtHalfSpeedWhenContested()
    {
        var hold = new ExtractionHold();
        hold.Light(30f);
        Assert.AreEqual(ExtractionHoldState.Holding, hold.Tick(10f, true, false));
        Assert.AreEqual(ExtractionHoldState.Waiting, hold.Tick(10f, false, false), "outside the circle");
        Assert.AreEqual(10f, hold.Held, 1e-4f, "waiting pauses, it doesn't reset");
        Assert.AreEqual(ExtractionHoldState.Contested, hold.Tick(10f, true, true, 0.5f), "a creature in the circle");
        Assert.AreEqual(15f, hold.Held, 1e-4f, "a contest slows it to the contested rate");
        Assert.AreEqual(ExtractionHoldState.Done, hold.Tick(40f, true, true, 0.5f), "it still finishes while contested");
    }

    [Test]
    public void LeavingWhileContested_IsWaiting()
    {
        var hold = new ExtractionHold();
        hold.Light(30f);
        Assert.AreEqual(ExtractionHoldState.Waiting, hold.Tick(1f, false, true));
    }

    [Test]
    public void ReachesDoneOnce_AndStays()
    {
        var hold = new ExtractionHold();
        hold.Light(30f);
        hold.Tick(25f, true, false);
        Assert.AreEqual(ExtractionHoldState.Done, hold.Tick(10f, true, false));
        Assert.AreEqual(1f, hold.Fraction, 1e-4f, "never past full");
        Assert.AreEqual(ExtractionHoldState.Done, hold.Tick(1f, false, true), "done stays done");
    }

    [Test]
    public void Dousing_LosesWhatWasHeld()
    {
        var hold = new ExtractionHold();
        hold.Light(30f);
        hold.Tick(12f, true, false);
        hold.Douse();
        Assert.IsFalse(hold.IsLit);
        Assert.AreEqual(0f, hold.Held);
        Assert.AreEqual(ExtractionHoldState.Unlit, hold.Tick(5f, true, false));
    }
}
