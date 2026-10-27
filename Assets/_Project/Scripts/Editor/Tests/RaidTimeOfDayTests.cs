using System.Collections.Generic;
using NUnit.Framework;

/// <summary>The hour a raid takes place at: a weighted roll from the seed, and night's reward in the rules.</summary>
public sealed class RaidTimeOfDayTests
{
    private static List<RaidTimeOfDay> Times() => new List<RaidTimeOfDay>
    {
        new RaidTimeOfDay { name = "Day", hour = 11f, chance = 60f, lootMultiplier = 1f },
        new RaidTimeOfDay { name = "Dusk", hour = 19f, chance = 25f, lootMultiplier = 1.15f },
        new RaidTimeOfDay { name = "Night", hour = 23.5f, chance = 15f, lootMultiplier = 1.4f },
    };

    [Test]
    public void SameSeed_SamePick()
    {
        List<RaidTimeOfDay> times = Times();
        for (int seed = 0; seed < 50; seed++)
        {
            Assert.AreEqual(RaidTimeOfDay.Pick(times, seed), RaidTimeOfDay.Pick(times, seed));
        }
    }

    [Test]
    public void Picks_FollowTheChances()
    {
        List<RaidTimeOfDay> times = Times();
        var counts = new int[3];
        for (int seed = 0; seed < 6000; seed++)
        {
            counts[RaidTimeOfDay.Pick(times, seed)]++;
        }

        Assert.AreEqual(0.60, counts[0] / 6000.0, 0.04);
        Assert.AreEqual(0.25, counts[1] / 6000.0, 0.04);
        Assert.AreEqual(0.15, counts[2] / 6000.0, 0.04);
    }

    [Test]
    public void NothingToPick_IsMinusOne()
    {
        Assert.AreEqual(-1, RaidTimeOfDay.Pick(null, 3));
        Assert.AreEqual(-1, RaidTimeOfDay.Pick(new List<RaidTimeOfDay>(), 3));
        Assert.AreEqual(-1, RaidTimeOfDay.Pick(new List<RaidTimeOfDay> { new RaidTimeOfDay { chance = 0f }, null }, 3));
    }

    [Test]
    public void ZeroChance_IsNeverPicked()
    {
        var times = new List<RaidTimeOfDay> { new RaidTimeOfDay { name = "Never", chance = 0f }, new RaidTimeOfDay { name = "Always", chance = 1f } };
        for (int seed = 0; seed < 200; seed++)
        {
            Assert.AreEqual(1, RaidTimeOfDay.Pick(times, seed));
        }
    }
}
