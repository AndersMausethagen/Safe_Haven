using NUnit.Framework;

/// <summary>
/// FrameTally (Docs/QualityLevers_2026-11-24.md §2): the Perf row's percentiles, worst frame and hitches from a minute of frames.
/// </summary>
public sealed class FrameTallyTests
{
    [Test]
    public void Percentiles_ReadTheFramesAtTheirRank_ToAQuarterMillisecond()
    {
        var tally = new FrameTally();
        // 90 smooth frames, 9 slow ones, one hitch: the median is smooth, the 95th slow, the 99th the hitch's neighbour, the worst the hitch.
        for (int i = 0; i < 90; i++)
        {
            tally.Add(16.6f);
        }

        for (int i = 0; i < 9; i++)
        {
            tally.Add(33.1f);
        }

        tally.Add(180f);

        Assert.AreEqual(100, tally.Frames);
        Assert.AreEqual(16.75f, tally.Percentile(0.5f), 0.001f);
        Assert.AreEqual(33.25f, tally.Percentile(0.95f), 0.001f);
        Assert.AreEqual(33.25f, tally.Percentile(0.99f), 0.001f, "the 99th of 100 frames is the last slow one, not the hitch");
        Assert.AreEqual(180f, tally.Percentile(1f), 0.001f);
        Assert.AreEqual(1, tally.Hitches);
        Assert.AreEqual(180f, tally.WorstMs, 0.001f);
    }

    [Test]
    public void AFrameBeyondTheTop_ReadsAsTheWorstFrame_AndBadFramesAreIgnored()
    {
        var tally = new FrameTally();
        tally.Add(float.NaN);
        tally.Add(-1f);
        Assert.AreEqual(0, tally.Frames);
        Assert.AreEqual(0f, tally.Percentile(0.5f));

        tally.Add(900f);
        Assert.AreEqual(900f, tally.Percentile(0.5f), 0.001f, "a frame past the histogram's top isn't clipped to it");
        Assert.AreEqual(1, tally.Hitches);
    }

    [Test]
    public void Detail_CarriesTheSplit_OnlyWhenReported_AndClearForgetsAll()
    {
        var tally = new FrameTally();
        tally.Add(20f);
        StringAssert.DoesNotContain("gpu", tally.Detail());
        tally.AddSplit(6d, 8d);
        tally.AddSplit(0d, 0d); // not reported this frame
        tally.AddSplit(6d, 8600847833.7d); // the timer's garbage (a real Play session's first row)
        StringAssert.Contains("cpu 6.0 gpu 8.0", tally.Detail());

        tally.Clear();
        Assert.AreEqual(0, tally.Frames);
        Assert.AreEqual(0f, tally.WorstMs);
        StringAssert.DoesNotContain("gpu", tally.Detail());
    }
}
