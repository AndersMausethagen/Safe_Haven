using NUnit.Framework;

/// <summary>A shaped wind-up (SwingWindup) reaches the strike on the same clip frame at the same moment, so the blow doesn't move.</summary>
public sealed class SwingWindupTests
{
    private static float ClipCovered(float speed, float animationSpeed)
    {
        const int Steps = 1000;
        float covered = 0f;
        for (int i = 0; i < Steps; i++)
        {
            covered += SwingWindup.Speed(speed, animationSpeed, (i + 0.5f) / Steps) / Steps;
        }

        return covered;
    }

    [TestCase(0.24f)]
    [TestCase(0.38f)]
    [TestCase(0.45f)]
    public void ASlowWindup_StartsFaster_EndsSlower_AndCoversTheSameClip(float animationSpeed)
    {
        Assert.Greater(SwingWindup.Strength(animationSpeed), 0f);
        Assert.Greater(SwingWindup.Speed(animationSpeed, animationSpeed, 0f), animationSpeed);
        Assert.Less(SwingWindup.Speed(animationSpeed, animationSpeed, 1f), animationSpeed);
        Assert.AreEqual(animationSpeed, ClipCovered(animationSpeed, animationSpeed), 0.0005f, "the strike starts on the same frame");
        // A heavy grown from a light swing plays the rest of its wind-up slower by as much: the same shape over what is left.
        Assert.AreEqual(animationSpeed * 0.6f, ClipCovered(animationSpeed * 0.6f, animationSpeed), 0.0005f);
    }

    [Test]
    public void AWindupAtItsOwnPace_IsLeftAsItIs()
    {
        Assert.AreEqual(0f, SwingWindup.Strength(0.8f));
        Assert.AreEqual(0.8f, SwingWindup.Speed(0.8f, 0.8f, 0f), 1e-6f);
        Assert.AreEqual(0.8f, SwingWindup.Speed(0.8f, 0.8f, 1f), 1e-6f);
    }
}
