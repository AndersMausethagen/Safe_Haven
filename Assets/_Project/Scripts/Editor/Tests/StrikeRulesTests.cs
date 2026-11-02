using NUnit.Framework;
using UnityEngine;

public sealed class StrikeRulesTests
{
    [Test]
    public void WindupSpeed_KeepsTheSwingLandingWhenItDid()
    {
        const float contact = 0.4f, landsAfter = 0.57f, strikeClip = 0.15f, strikeSpeed = 1.7f;
        float windup = StrikeRules.WindupSpeed(contact, landsAfter, strikeClip, strikeSpeed);
        float total = (contact - strikeClip) / windup + strikeClip / strikeSpeed;
        Assert.AreEqual(landsAfter, total, 1e-3f);
        Assert.Less(windup, strikeSpeed, "the build-up is slower than the strike: that is what makes it snap");
    }

    [Test]
    public void WindupSpeed_WithoutAStrikeIsJustTheOldSpeed()
    {
        Assert.AreEqual(0.4f / 0.5f, StrikeRules.WindupSpeed(0.4f, 0.5f, 0f, 1.5f), 1e-4f);
    }

    [Test]
    public void SweepPosition_ReachesTargetsTheWayTheBladeMoves()
    {
        Vector3 forward = Vector3.forward;
        Vector3 toRight = new Vector3(1f, 0f, 1f);
        Vector3 toLeft = new Vector3(-1f, 0f, 1f);
        Assert.Less(StrikeRules.SweepPosition(forward, toLeft, 120f, SlashSweep.LeftToRight),
            StrikeRules.SweepPosition(forward, toRight, 120f, SlashSweep.LeftToRight), "left to right reaches the left one first");
        Assert.Less(StrikeRules.SweepPosition(forward, toRight, 120f, SlashSweep.RightToLeft),
            StrikeRules.SweepPosition(forward, toLeft, 120f, SlashSweep.RightToLeft), "right to left reaches the right one first");
        Assert.AreEqual(0.5f, StrikeRules.SweepPosition(forward, forward, 120f, SlashSweep.LeftToRight), 1e-4f);
    }

    [Test]
    public void SweepSeconds_NarrowSwingsTakeLess()
    {
        Assert.AreEqual(0.09f, StrikeRules.SweepSeconds(180f, 0.09f), 1e-4f);
        Assert.AreEqual(0.02f, StrikeRules.SweepSeconds(40f, 0.09f), 1e-3f);
        Assert.AreEqual(0.09f, StrikeRules.SweepSeconds(360f, 0.09f), 1e-4f, "a full turn takes no longer than a half");
    }

    [Test]
    public void IsBehind_OnlyInTheArcBehindTheTarget()
    {
        Vector3 target = Vector3.zero, forward = Vector3.forward;
        Assert.IsTrue(StrikeRules.IsBehind(target, forward, new Vector3(0f, 0f, -2f), 130f));
        Assert.IsTrue(StrikeRules.IsBehind(target, forward, new Vector3(1f, 0f, -1f), 130f), "45 degrees off straight behind");
        Assert.IsFalse(StrikeRules.IsBehind(target, forward, new Vector3(2f, 0f, 0f), 130f), "beside it");
        Assert.IsFalse(StrikeRules.IsBehind(target, forward, new Vector3(0f, 0f, 2f), 130f), "in front of it");
    }

    [Test]
    public void HitForce_GrowsWithTheBlowAndStaysInRange()
    {
        float nick = StrikeRules.HitForce(0.05f, 0.65f, false, false);
        float blow = StrikeRules.HitForce(0.2f, 1f, false, false);
        float heavy = StrikeRules.HitForce(0.2f, 1.5f, true, false);
        Assert.Less(nick, blow);
        Assert.Less(blow, heavy);
        Assert.AreEqual(1f, StrikeRules.HitForce(5f, 3f, true, true), 1e-4f);
        Assert.GreaterOrEqual(nick, 0f);
    }

    [Test]
    public void SprayDirection_FollowsTheBlowAndTheSweep()
    {
        Vector3 thrust = StrikeRules.SprayDirection(Vector3.forward, 0f, SlashSweep.LeftToRight, 0f);
        Assert.AreEqual(1f, Vector3.Dot(thrust, Vector3.forward), 1e-4f, "a thrust sprays straight on");

        Vector3 rightward = StrikeRules.SprayDirection(Vector3.forward, 180f, SlashSweep.LeftToRight, 0f);
        Vector3 leftward = StrikeRules.SprayDirection(Vector3.forward, 180f, SlashSweep.RightToLeft, 0f);
        Assert.Greater(rightward.x, 0f, "a blade going left to right throws it right");
        Assert.Less(leftward.x, 0f);
        Assert.Greater(rightward.z, 0f, "and still mostly along the blow");
        Assert.Greater(StrikeRules.SprayDirection(Vector3.forward, 0f, SlashSweep.LeftToRight, 0.3f).y, 0f);
        Assert.AreEqual(1f, rightward.magnitude, 1e-4f);
    }

    [Test]
    public void RunningAttack_NeedsARunUpTowardTheAim()
    {
        Assert.IsTrue(StrikeRules.IsRunningAttack(0.3f, 0.2f, 20f, 70f));
        Assert.IsFalse(StrikeRules.IsRunningAttack(0.1f, 0.2f, 20f, 70f), "only just started running");
        Assert.IsFalse(StrikeRules.IsRunningAttack(0.3f, 0.2f, 120f, 70f), "running away from where it aims");
    }

    [Test]
    public void TheFightGoesOnWhileHunted()
    {
        Assert.IsFalse(StrikeRules.FightIsOver(30f, 5f, true), "being chased is a fight, however long since a blow");
        Assert.IsTrue(StrikeRules.FightIsOver(6f, 5f, false));
        Assert.IsFalse(StrikeRules.FightIsOver(4f, 5f, false));
        Assert.IsFalse(StrikeRules.FightIsOver(99f, 0f, false), "0 stays in combat");
    }

    [Test]
    public void ArmSolver_ReachesTheTarget_KeepsItsLengths_AndBendsTheWayItBent()
    {
        Vector3 shoulder = new Vector3(0f, 1.4f, 0f), elbow = new Vector3(0f, 1.1f, -0.1f), target = new Vector3(0.1f, 1.2f, 0.4f);
        Vector3 solved = ArmSolver.Solve(shoulder, elbow, target, 0.3f, 0.28f, out Vector3 wrist);
        Assert.AreEqual(0f, (wrist - target).magnitude, 1e-3f);
        Assert.AreEqual(0.3f, (solved - shoulder).magnitude, 1e-3f);
        Assert.AreEqual(0.28f, (wrist - solved).magnitude, 1e-3f);
        Assert.Less(solved.y, (shoulder.y + target.y) * 0.5f, "the elbow stays low, as it was");

        Vector3 far = shoulder + Vector3.forward * 3f;
        ArmSolver.Solve(shoulder, elbow, far, 0.3f, 0.28f, out Vector3 stretched);
        Assert.AreEqual(0.58f, (stretched - shoulder).magnitude, 2e-3f, "too far: as far as the arm goes");
    }

    [Test]
    public void Dodge_StartsWhereSteered_ElseBackFromTheEnemy()
    {
        Vector3 toEnemy = Vector3.forward * 3f;
        Assert.AreEqual(Vector3.right, DodgeRules.StartDirection(Vector3.right, Vector3.forward, toEnemy));
        Assert.AreEqual(Vector3.back, DodgeRules.StartDirection(Vector3.zero, Vector3.forward, toEnemy), "standing still, it steps back");
        Assert.AreEqual(Vector3.left, DodgeRules.StartDirection(Vector3.zero, Vector3.left, Vector3.zero), "no enemy: toward the aim");
    }

    [Test]
    public void Dodge_GoesAroundWhenSteeredTowardOrPast_NotAway()
    {
        Vector3 toEnemy = Vector3.forward;
        Assert.IsTrue(DodgeRules.GoesAround(Vector3.forward, toEnemy), "straight at it");
        Assert.IsTrue(DodgeRules.GoesAround(Vector3.right, toEnemy), "past it");
        Assert.IsFalse(DodgeRules.GoesAround(Vector3.back, toEnemy), "away");
        Assert.IsFalse(DodgeRules.GoesAround(Vector3.zero, toEnemy), "no direction");
    }

    // Walks the curve the way the dodge does: it must end at the spot behind without ever going through the enemy's body.
    [TestCase(1f)]
    [TestCase(-1f)]
    public void Dodge_CurvesAroundTheEnemyToTheSpotBehind(float side)
    {
        Vector3 enemy = Vector3.zero;
        Vector3 enemyForward = Vector3.back; // it faces the player, who starts in front of it, at -Z
        Vector3 position = new Vector3(0f, 0f, -2.5f);
        Vector3 input = new Vector3(side, 0f, 0.5f).normalized;
        Vector3 spot = DodgeRules.SpotBehind(enemy, enemyForward, 1.5f);
        Assert.Greater(spot.z, 0f, "behind it is the far side from the player");

        const float clearance = 1f, speed = 10f, turn = 720f, step = 1f / 60f;
        Vector3 going = input;
        float travelled = 0f, closest = float.MaxValue;
        while (!DodgeRules.HasArrived(position, spot) && travelled < 12f)
        {
            Vector3 heading = DodgeRules.Heading(position, enemy, spot, input, clearance);
            going = Vector3.RotateTowards(going, heading, turn * Mathf.Deg2Rad * step, 0f);
            position += going * (speed * step);
            travelled += speed * step;
            closest = Mathf.Min(closest, position.magnitude);
        }

        Assert.IsTrue(DodgeRules.HasArrived(position, spot), $"never got there: ended at {position} after {travelled:0.0} m");
        Assert.Greater(closest, clearance * 0.75f, "cut through the enemy");
        Assert.Less(travelled, 7f, "too far for a dodge to go");
        Assert.IsTrue(StrikeRules.IsBehind(enemy, enemyForward, position, 130f), "and from there a blow is a backstab");
    }

    [Test]
    public void BackstabMemory_LearnsOneAtATime_AndForgetsThem()
    {
        var memory = new BackstabMemory();
        Assert.AreEqual(0, memory.Remembered(0f, 20f), "the first always lands");
        memory.Learn(1f, 20f);
        memory.Learn(2f, 20f);
        Assert.AreEqual(2, memory.Remembered(2f, 20f));
        Assert.AreEqual(1, memory.Remembered(23f, 20f), "one forgotten");
        Assert.AreEqual(0, memory.Remembered(43f, 20f), "both forgotten");
        memory.Learn(50f, 20f);
        Assert.AreEqual(1, memory.Remembered(50f, 20f), "starts again from what it still remembers");

        Assert.AreEqual(0f, BackstabMemory.ReadChance(0, 0.35f, 0.9f));
        Assert.AreEqual(0.7f, BackstabMemory.ReadChance(2, 0.35f, 0.9f), 0.001f);
        Assert.AreEqual(0.9f, BackstabMemory.ReadChance(5, 0.35f, 0.9f), 0.001f, "never a sure thing");
    }

    [Test]
    public void Shield_TakesWhatComesFromTheFront_NotBehindOrHeavy()
    {
        Vector3 facing = Vector3.forward;
        Vector3 fromFront = Vector3.back;
        Vector3 fromBehind = Vector3.forward;
        Assert.AreEqual(GuardOutcome.Blocked, GuardRules.ResolveShield(150f, facing, fromFront, HitTechnique.None));
        Assert.AreEqual(GuardOutcome.None, GuardRules.ResolveShield(150f, facing, fromBehind, HitTechnique.None));
        Assert.AreEqual(GuardOutcome.None, GuardRules.ResolveShield(150f, facing, Vector3.left, HitTechnique.None), "the flank is open");
        Assert.AreEqual(GuardOutcome.None, GuardRules.ResolveShield(150f, facing, fromFront, HitTechnique.Heavy), "a heavy blow goes through");
        Assert.AreEqual(GuardOutcome.None, GuardRules.ResolveShield(150f, facing, fromFront, HitTechnique.WeaponArt));
        Assert.AreEqual(GuardOutcome.Blocked, GuardRules.ResolveShield(150f, facing, fromFront, HitTechnique.Rhythm));
        Assert.AreEqual(GuardOutcome.None, GuardRules.ResolveShield(0f, facing, fromFront, HitTechnique.None), "no shield");
    }

    [Test]
    public void GreatBeasts_AreOnlyOpenWhenStaggered()
    {
        foreach (string name in new[] { "OldMossTroll", "RootGuardian", "Huldra" })
        {
            Assert.IsFalse(UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"Assets/_Project/Data/Monsters/{name}.asset").OpenWhileStriking, name);
        }

        Assert.IsTrue(UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/Draug.asset").OpenWhileStriking);
        Assert.Greater(UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/BarrowShieldbearer.asset").ShieldArc, 0f);
    }

    [Test]
    public void TargetRules_PreferWhatIsNearTheAim_AndHoldOn()
    {
        float nearAim = TargetRules.Score(5f, 3f, 9f);
        float offAim = TargetRules.Score(60f, 3f, 9f);
        Assert.Less(nearAim, offAim);
        Assert.Less(TargetRules.Score(5f, 2f, 9f), TargetRules.Score(5f, 6f, 9f), "the nearer of two on the aim");

        Assert.IsFalse(TargetRules.ShouldSwitch(0.5f, 0.4f, 0.35f), "a little better is no reason to change");
        Assert.IsTrue(TargetRules.ShouldSwitch(1.2f, 0.4f, 0.35f));

        Assert.IsTrue(TargetRules.CanAcquire(60f, 5f, 70f, 6f));
        Assert.IsFalse(TargetRules.CanAcquire(80f, 5f, 70f, 6f), "outside the cone");
        Assert.IsFalse(TargetRules.CanAcquire(10f, 7f, 70f, 6f), "too far");

        Assert.IsFalse(TargetRules.IsAimingAway(90f, 100f));
        Assert.IsTrue(TargetRules.IsAimingAway(120f, 100f), "aiming well away, the player means to look elsewhere");
    }
}
