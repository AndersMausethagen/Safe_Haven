using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The enemy AI round (Docs/EnemyAIReview_2026-11-19.md): bite and break away, fair turns, where blows come from, and the rules the later
/// items add. Pure rules on their real inputs; the creatures' use of them is seen in Play.
/// </summary>
public sealed class EnemyAITests
{
    [Test]
    public void ABreakAway_RunsOffToItsDistance_AwayFromTheOtherAttacker()
    {
        Vector3 target = Vector3.zero, self = new Vector3(0f, 0f, 1.5f);
        Vector3 other = new Vector3(1.5f, 0f, 0.5f); // to the east of the biter's line
        Vector3 spot = AttackTurns.BreakAwaySpot(self, target, other, 3.5f, 1f, 60f);
        Assert.AreEqual(3.5f, Vector3.Distance(spot, target), 0.01f, "the break-away distance from the target");
        Assert.Less(spot.x, 0f, "it swings west, away from the other attacker in the east");
        Assert.AreNotEqual(target, spot, "never the target's own spot");
        float swung = Vector3.Angle(self - target, spot - target);
        Assert.That(swung, Is.InRange(49.9f, 70.1f), "50-70° round from where it struck");
    }

    [Test]
    public void TheTurn_GoesToTheOneThatWaited_InFront_NotTheLastStriker()
    {
        CombatTechniqueSettings rules = CombatTechniqueSettings.Current;
        float lastStriker = AttackTurns.Rank(1f, true, true, true, rules.FrontBonus, rules.OnScreenBonus, rules.LastStrikerPenalty);
        float waiting = AttackTurns.Rank(1f, true, true, false, rules.FrontBonus, rules.OnScreenBonus, rules.LastStrikerPenalty);
        Assert.Greater(waiting, lastStriker, "the one that just struck waits for the others");
        float front = AttackTurns.Rank(0.5f, true, true, false, rules.FrontBonus, rules.OnScreenBonus, rules.LastStrikerPenalty);
        float back = AttackTurns.Rank(0.5f, false, true, false, rules.FrontBonus, rules.OnScreenBonus, rules.LastStrikerPenalty);
        Assert.Greater(front, back, "at equal waits, the one where the guard works goes first");
        float waitedLong = AttackTurns.Rank(3f, false, true, false, rules.FrontBonus, rules.OnScreenBonus, rules.LastStrikerPenalty);
        Assert.Greater(waitedLong, front, "but one behind that has waited long enough gets its turn");

        Assert.IsTrue(AttackTurns.InFront(new Vector3(0f, 0f, 2f), Vector3.zero, Vector3.forward, rules.FrontHalfArc));
        Assert.IsFalse(AttackTurns.InFront(new Vector3(0f, 0f, -2f), Vector3.zero, Vector3.forward, rules.FrontHalfArc));
    }

    [Test]
    public void AHurtRow_SaysWhichSideTheBlowCameFrom()
    {
        Assert.AreEqual("front", PlaytestLogger.FromSide(10f));
        Assert.AreEqual("side", PlaytestLogger.FromSide(90f));
        Assert.AreEqual("back", PlaytestLogger.FromSide(170f));
    }

    // §5: a search goes where the player was going, not where they are, and a pack spreads out over it.
    [Test]
    public void ASearch_GoesAheadAlongTheWay_AndAPackSpreads()
    {
        Vector3 lastKnown = new Vector3(10f, 0f, 10f), going = new Vector3(4f, 0f, 0f); // east at 4 m/s
        var points = AttackTurns.SearchPoints(lastKnown, going, 2, 0, new System.Random(3));
        Assert.AreEqual(3, points.Count, "a first guess and two more");
        Assert.AreEqual(16f, points[0].x, 0.01f, "1.5 s ahead along the way: 6 m east");
        Assert.Greater(points[2].x, points[0].x, "the sweep goes on ahead, not back");
        Assert.AreEqual(lastKnown, AttackTurns.SearchPoints(lastKnown, Vector3.zero, 2, 0, new System.Random(3))[0],
            "standing still: where it was last seen");
        Vector3 second = AttackTurns.SearchPoints(lastKnown, going, 2, 1, new System.Random(3))[1];
        Vector3 third = AttackTurns.SearchPoints(lastKnown, going, 2, 2, new System.Random(3))[1];
        Assert.Greater(Vector3.Distance(second, third), 2f, "two searchers of the same ground take different ways");
    }

    // §6: a shooter between shots prefers higher ground and keeps clear of the other shooters.
    [Test]
    public void AShooter_PrefersHighGround_AwayFromOtherShooters()
    {
        Assert.Greater(AttackTurns.ScoreSpot(1.5f, false), AttackTurns.ScoreSpot(0f, false));
        Assert.Greater(AttackTurns.ScoreSpot(0f, false), AttackTurns.ScoreSpot(1.5f, true), "a crowded rise loses to an open flat spot");
        Assert.AreEqual(2f, AttackTurns.ScoreSpot(9f, false), 1e-4f, "height pays up to 2");
    }

    // §4: a flanker comes only for a player who turtles or stands still.
    [Test]
    public void AFlanker_ComesOnlyForAPassivePlayer()
    {
        Assert.IsTrue(MonsterAI.IsPassive(1.5f, 0f), "the guard held a while");
        Assert.IsTrue(MonsterAI.IsPassive(0f, 2.5f), "standing still");
        Assert.IsFalse(MonsterAI.IsPassive(0.5f, 1f), "moving, guarding now and then");
    }

    // §9: the pacing builds, peaks, fades, gives a breather, and builds again; only the build sends roamers.
    [Test]
    public void ThePacing_GivesABreatherAfterAHardFight()
    {
        var pacing = new RaidIntensity();
        var random = new System.Random(1);
        float now = 0f;
        Assert.IsTrue(pacing.AllowsRoamers);
        pacing.Hurt(0.6f);
        pacing.Hurt(0.5f);
        Assert.AreEqual(RaidIntensity.Phase.Peak, pacing.Tick(0.5f, 3, Vector3.zero, now += 0.5f, random), "110 from the blows taken: the peak");
        Assert.IsFalse(pacing.AllowsRoamers);
        pacing.Tick(7f, 0, Vector3.zero, now += 7f, random);
        Assert.AreEqual(RaidIntensity.Phase.Fade, pacing.Now, "after its hold, it fades");
        for (int i = 0; i < 40 && pacing.Now == RaidIntensity.Phase.Fade; i++)
        {
            pacing.Tick(1f, 0, Vector3.zero, now += 1f, random);
        }

        Assert.AreEqual(RaidIntensity.Phase.Relax, pacing.Now, "quiet long enough: the breather");
        Assert.IsFalse(pacing.AllowsRoamers, "no roamers in the breather");
        pacing.Tick(1f, 0, new Vector3(50f, 0f, 0f), now += 1f, random);
        Assert.AreEqual(RaidIntensity.Phase.Relax, pacing.Now, "a walk right away doesn't end it (the user's run, 2026-11-25: peaks a minute and a half apart)");
        pacing.Tick(1f, 0, new Vector3(50f, 0f, 0f), now += RaidIntensity.MinRelax, random);
        Assert.AreEqual(RaidIntensity.Phase.Build, pacing.Now, "40 m of travel ends it early once it has lasted a while");
        Assert.IsTrue(pacing.AllowsRoamers);
    }

    // The review of 2026-11-19: a hunter stuck where it can't reach kept the score up, and the fade held the roamers off for good.
    [Test]
    public void TheFade_EndsEvenWhileAFightGoesOn()
    {
        var pacing = new RaidIntensity();
        var random = new System.Random(2);
        float now = 0f;
        pacing.Hurt(5f);
        Assert.LessOrEqual(pacing.Score, RaidIntensity.PeakAt * 1.2f + 0.01f, "the score is capped near the peak");
        pacing.Tick(0.5f, 4, Vector3.zero, now += 0.5f, random);
        for (int i = 0; i < 60 && pacing.Now != RaidIntensity.Phase.Relax; i++)
        {
            pacing.Tick(1f, 4, Vector3.zero, now += 1f, random);
        }

        Assert.AreEqual(RaidIntensity.Phase.Relax, pacing.Now, "within the fade's longest, whatever the score");
    }
}
