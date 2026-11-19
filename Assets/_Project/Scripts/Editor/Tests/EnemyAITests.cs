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
}
