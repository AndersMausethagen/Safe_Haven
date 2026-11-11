using NUnit.Framework;

/// <summary>
/// The frame limit holds with VSync on too (DisplayOptions.Pacing). It didn't: a player on a 240 Hz screen chose 30 and their
/// GPU drew 240 frames a second (2026-11-11).
/// </summary>
public sealed class FramePacingTests
{
    private static double Rate((int vSyncCount, int renderEvery, int targetFrameRate) pacing, double refresh) =>
        pacing.vSyncCount > 0 ? refresh / pacing.vSyncCount / pacing.renderEvery : pacing.targetFrameRate;

    [Test]
    public void WithoutVSync_TheLimitIsTheFrameRate()
    {
        Assert.AreEqual((0, 1, 30), DisplayOptions.Pacing(false, 30, 240));
        Assert.AreEqual((0, 1, -1), DisplayOptions.Pacing(false, -1, 240));
    }

    [Test]
    public void WithVSync_TheLimitStillHolds()
    {
        Assert.AreEqual(30, Rate(DisplayOptions.Pacing(true, 30, 240), 240), 0.01, "the player's case: 30 on a 240 Hz screen");
        Assert.AreEqual(60, Rate(DisplayOptions.Pacing(true, 60, 240), 240), 0.01);
        Assert.AreEqual(60, Rate(DisplayOptions.Pacing(true, 60, 120), 120), 0.01);
        Assert.AreEqual(27.5, Rate(DisplayOptions.Pacing(true, 30, 165), 165), 0.01, "every sixth refresh, not every eighth");
        Assert.AreEqual((1, 1, -1), DisplayOptions.Pacing(true, -1, 240), "no limit: every refresh");
        Assert.AreEqual((1, 1, -1), DisplayOptions.Pacing(true, 144, 144));
        Assert.AreEqual((0, 1, 60), DisplayOptions.Pacing(true, 60, 144), "no whole fraction of 144 near 60: kept without VSync");
    }

    [Test]
    public void TheLimit_IsNeverPassed()
    {
        foreach (double refresh in new[] { 60.0, 75, 100, 120, 144, 165, 175, 200, 240, 360 })
        {
            foreach (int limit in new[] { 30, 60, 90, 120, 144, 165, 240 })
            {
                Assert.LessOrEqual(Rate(DisplayOptions.Pacing(true, limit, refresh), refresh), System.Math.Max(limit, refresh <= limit * 1.02 ? refresh : limit) + 0.01, $"{limit} on {refresh} Hz");
            }
        }
    }
}
