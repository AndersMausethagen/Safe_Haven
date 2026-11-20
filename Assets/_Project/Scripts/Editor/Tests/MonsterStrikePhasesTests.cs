using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Every humanoid monster's strike lands its blow on the clip's contact (the user, 2026-11-20: a slow wind-up, a fast strike, a
/// follow-through for all monster strikes): the phases' arithmetic, and each body's measured clips being current.
/// </summary>
public sealed class MonsterStrikePhasesTests
{
    private static MonsterStrikeClip Clip(float windupEnd, float contact, float length) =>
        new MonsterStrikeClip { state = "Strike", windupEnd = windupEnd, contact = contact, length = length };

    [TestCase(0.22f, 0.33f, 1.13f, 1.3f, 2.4f)]   // a troll's stomp: a long telegraph on a short clip
    [TestCase(0.37f, 0.48f, 1.13f, 1.0f, 1.9f)]
    [TestCase(0.17f, 0.50f, 0.70f, 0.45f, 0.9f)]  // a claw with a long swing
    [TestCase(0.27f, 0.38f, 0.70f, 0.30f, 0.75f)] // a quick shove: strained
    [TestCase(0.02f, 0.17f, 1.10f, 1.0f, 1.2f)]   // a clip moving from its first frame
    [TestCase(0.35f, 0.45f, 0.90f, 0.30f, 0.80f)] // too quick for the raise: it raises as fast as it may and strikes in what is left
    [TestCase(0.50f, 0.60f, 1.00f, 0.10f, 0.50f)] // too quick even for that: it plays evenly
    public void TheBlow_MeetsOnTheContact_AsTheHitLands(float windupEnd, float contact, float length, float hit, float recovery)
    {
        MonsterStrikePhases phases = MonsterStrikePhases.Fit(Clip(windupEnd, contact, length), hit, recovery);
        Assert.AreEqual(contact, phases.ClipAt(hit, hit), 0.002f, "clip time at the hit");
        Assert.LessOrEqual(phases.HangAt, phases.StrikeAt + 1e-4f);
        Assert.GreaterOrEqual(phases.Strike + 1e-4f, phases.RaiseSpeed, "the strike is never slower than the raise");
        if (phases.RaiseSpeed > MonsterStrikePhases.FastestRaise + 1e-4f)
        {
            Assert.AreEqual(phases.RaiseSpeed, phases.Strike, 1e-4f, "a raise faster than Fastest Raise only when the whole clip plays evenly");
        }
        Assert.GreaterOrEqual(phases.Follow, MonsterStrikePhases.StalledFollow - 1e-4f);
        // A held wind-up (a feint) holds before the strike, not in the middle of the blow.
        Assert.GreaterOrEqual(phases.StrikeAt, hit * MonsterAttacks.HoldAtShare - 1e-4f, "the strike starts after a hold would");
    }

    // The hang can fail: a long telegraph raises at the slowest readable pace and hangs near the top (slower still); a short one
    // doesn't hang at all.
    [Test]
    public void ALongTelegraph_RaisesThenHangs_AShortOneDoesNot()
    {
        MonsterStrikePhases slow = MonsterStrikePhases.Fit(Clip(0.22f, 0.33f, 1.13f), 1.3f, 2.4f);
        Assert.AreEqual(MonsterStrikePhases.SlowestRaise, slow.RaiseSpeed, 1e-4f);
        Assert.Less(slow.HangAt, slow.StrikeAt - 0.3f, "it hangs a while");
        Assert.Less(slow.HangSpeed, slow.RaiseSpeed * 0.5f);
        Assert.AreEqual(0.22f * (1f - MonsterStrikePhases.HangShare), slow.ClipAt(slow.HangAt, 1.3f), 0.002f, "it hangs near the top");

        MonsterStrikePhases quick = MonsterStrikePhases.Fit(Clip(0.37f, 0.48f, 1.13f), 0.55f, 1.2f);
        Assert.AreEqual(quick.StrikeAt, quick.HangAt, 1e-4f, "no hang");
        Assert.Greater(quick.RaiseSpeed, MonsterStrikePhases.SlowestRaise);
    }

    [Test]
    public void EveryHumanoidMonster_HasItsStrikeClipsMeasured()
    {
        Dictionary<string, MonsterStrikeClip[]> wanted = MonsterStrikeFit.Measure();
        // Some twenty humanoid monsters strike with clips of their own; fewer means the measuring found nothing to measure.
        Assert.Greater(wanted.Count(entry => entry.Value.Length > 0), 15, "monsters with measured strikes");
        var stale = new List<string>();
        foreach (KeyValuePair<string, MonsterStrikeClip[]> entry in wanted)
        {
            var driver = AssetDatabase.LoadAssetAtPath<GameObject>(entry.Key).GetComponentInChildren<MonsterAnimationDriver>(true);
            if (!MonsterStrikeFit.Same(driver.StrikeClips, entry.Value))
            {
                stale.Add($"{entry.Key}: has {driver.StrikeClips.Count}, measures {entry.Value.Length}");
            }
        }

        Assert.IsEmpty(stale, "run Safe Haven › Tools › Fit Monster Strikes\n" + string.Join("\n", stale));
    }
}
