using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// The Heartroot's redesign (Docs/HeartrootBossReview_2026-11-17.md, approved 2026-11-17), its rules that need no scene: poise that grows
/// with each topple and is its own again at each stage (C2), a boss's own recovery delay, the heart strike as a share of its health whatever
/// its bark has left, strikes retired by stage (C5), a stage's opener used once and never as the answer to a backstab, the root walls' nudge
/// (C7) and the finale's rings (C10). Every new field starts off, so every other creature fights as before.
/// </summary>
public sealed class BossToppleTests
{
    private readonly List<Object> made = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object thing in made)
        {
            Object.DestroyImmediate(thing);
        }

        made.Clear();
    }

    private AttackDefinition Attack()
    {
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        made.Add(attack);
        return attack;
    }

    // ----- C2: topples -----

    [Test]
    public void EachToppleNeedsMore_AndANewStageSetsItBack()
    {
        var poise = new PoiseMeter(450f, 5f, 15f);
        Assert.IsTrue(poise.Hit(450f, 0f), "the first topple takes its own poise");
        poise.Grow(0.3f);
        Assert.AreEqual(585f, poise.Max, 1e-3f, "30% more for the next");
        Assert.AreEqual(585f, poise.Current, 1e-3f, "it stands full at the new size");
        Assert.IsFalse(poise.Hit(450f, 1f), "what toppled it before doesn't now");
        Assert.IsTrue(poise.Hit(135f, 1.5f));
        poise.Grow(0.3f);
        Assert.AreEqual(720f, poise.Max, 1e-3f, "growth adds up, by its own poise each time");
        poise.Hit(360f, 2f);
        poise.ResetGrowth();
        Assert.AreEqual(450f, poise.Max, 1e-3f, "a new stage: its own poise again");
        Assert.AreEqual(0.5f, poise.Fraction, 1e-3f, "as full as it was");
        Assert.AreEqual(450f, poise.BaseMax, 1e-3f, "what a parry's share is measured against never grows");
    }

    [Test]
    public void NoGrowth_ChangesNothing()
    {
        var poise = new PoiseMeter(320f, 2f, 15f);
        poise.Hit(20f, 0f);
        poise.Grow(0f);
        poise.ResetGrowth();
        Assert.AreEqual(320f, poise.Max, 1e-3f);
        Assert.AreEqual(300f, poise.Current, 1e-3f);
    }

    [Test]
    public void ABossPoiseWaitsItsOwnDelay_BeforeItComesBack()
    {
        var poise = new PoiseMeter(450f, 5f, 15f);
        Assert.AreEqual(5f, poise.RecoveryDelay);
        poise.Hit(100f, 0f);
        poise.Tick(4.9f, 1f);
        Assert.AreEqual(350f, poise.Current, 1e-3f, "still waiting: poise builds across exchanges");
        poise.Tick(5f, 1f);
        Assert.AreEqual(365f, poise.Current, 1e-3f, "coming back");
    }

    [Test]
    public void TheHeartStrike_CostsItsShareOfHealth_WhateverTheBarkHasLeft()
    {
        // 2% of 2,600, staggered (it takes more while down), through its whole bark, a chipped one and none.
        foreach (float armour in new[] { 35f, 23f, 0f })
        {
            var bark = new StatSheet();
            bark.SetBase(StatType.Armor, armour);
            float sent = DamageCalculator.BeforeDefenses(52f, DamageType.Physical, StrikeKind.None, bark, CombatRules.Default, 1.2f);
            float lost = DamageCalculator.Mitigate(sent, DamageType.Physical, StrikeKind.None, bark, CombatRules.Default) * 1.2f;
            Assert.AreEqual(52f, lost, 0.01f, $"armour {armour}");
        }

        Assert.AreEqual(0f, DamageCalculator.BeforeDefenses(0f, DamageType.Physical, StrikeKind.None, new StatSheet(), CombatRules.Default));
    }

    [Test]
    public void ToppleAndStageFields_StartOff_SoEveryOtherCreatureFightsAsBefore()
    {
        var monster = ScriptableObject.CreateInstance<MonsterDefinition>();
        made.Add(monster);
        Assert.IsFalse(monster.Topples);
        Assert.AreEqual(0f, monster.ToppleSeconds);
        Assert.AreEqual(0f, monster.PoiseGrowth);
        Assert.AreEqual(0f, monster.ToppleStrike);
        Assert.AreEqual(0f, monster.PoiseRecoveryDelay, "0: the game-wide delay");
        Assert.IsFalse(monster.ToppleBreaksArmour);

        var strike = new MonsterAttack();
        Assert.AreEqual(0, strike.untilPhase, "used to the end");
        Assert.AreEqual(0f, strike.reelWhenBroken, "a broken strike only waits, as before");
        Assert.IsFalse(strike.opener);
        Assert.IsFalse(strike.toppleAfter);
    }

    // ----- C5: each stage its own -----

    [Test]
    public void AStrikeRetiresAtItsUntilPhase()
    {
        var call = new MonsterAttack { attack = Attack(), maximumRange = 30f, untilPhase = 2 };
        var attacks = new[] { call };
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(attacks, new float[1], 5f, 0f, false, 0, 0));
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(attacks, new float[1], 5f, 0f, false, 0, 1));
        Assert.AreEqual(-1, MonsterAttacks.SelectAttack(attacks, new float[1], 5f, 0f, false, 0, 2), "gone at the third stage");
        Assert.IsFalse(MonsterAttacks.HoldsGround(attacks, new float[1], 5f, 0f, 0, 2), "a retired strike is no reason to stand");
        Assert.AreEqual(-1, MonsterAttacks.SelectAnswer(attacks, 5f, 2, 0), "nor an answer to a backstab");

        call.untilPhase = 0;
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(attacks, new float[1], 5f, 0f, false, 0, 2), "0 keeps it to the end");
    }

    [Test]
    public void InPhase_RunsFromItsFromPhase_ToBeforeItsUntilPhase()
    {
        var walls = new MonsterAttack { fromPhase = 1, untilPhase = 2 };
        Assert.IsFalse(walls.InPhase(0));
        Assert.IsTrue(walls.InPhase(1));
        Assert.IsFalse(walls.InPhase(2));
        Assert.IsFalse(walls.InPhase(3));
    }

    [Test]
    public void AStagesOpener_ComesFirst_Once_AndIsNeverTheAnswer()
    {
        var slam = new MonsterAttack { attack = Attack(), maximumRange = 3f, priority = 10, weight = 3f, cooldown = 3f };
        var uproot = new MonsterAttack { attack = Attack(), maximumRange = 40f, priority = 40, opener = true, fromPhase = 1, untilPhase = 2 };
        var attacks = new[] { slam, uproot };
        var cooldowns = new float[2];
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(attacks, cooldowns, 2f, 0f, false, 0, 0, roll: 0.9f), "not before its stage");
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(attacks, cooldowns, 2f, 0f, false, 0, 1, roll: 0.9f), "first as its stage begins");
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(attacks, cooldowns, 12f, 0f, false, 0, 1), "wherever its foe stands");

        cooldowns[1] = uproot.ReadyAfter(1f);
        Assert.IsTrue(float.IsPositiveInfinity(cooldowns[1]), "spent once used, its cooldown unread");
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(attacks, cooldowns, 2f, 1e6f, false, 0, 1, roll: 0.9f), "never again");
        Assert.AreEqual(0, MonsterAttacks.SelectAnswer(attacks, 2f, 1, 0), "a backstab is answered with an everyday strike");

        // The review's priority 40 and cooldown 9999 alone: the answer to a backstab ignores cooldowns, so the set piece would come back
        // after every backstab in reach.
        uproot.opener = false;
        uproot.cooldown = 9999f;
        Assert.AreEqual(1, MonsterAttacks.SelectAnswer(attacks, 2f, 1, 0), "why Opener exists");
    }

    [Test]
    public void AnOpenerNotUsedInItsStage_IsRetiredWithIt()
    {
        // The Heartroot's Heartburst opens stage 2; a boss taken through stage 2 too fast to use it mustn't open stage 3 with it.
        var heartburst = new MonsterAttack { attack = Attack(), maximumRange = 40f, priority = 40, opener = true, fromPhase = 2 };
        var slam = new MonsterAttack { attack = Attack(), maximumRange = 3f, priority = 10 };
        var attacks = new[] { slam, heartburst };
        Assert.IsFalse(heartburst.InPhase(1));
        Assert.IsTrue(heartburst.InPhase(2), "its own stage");
        Assert.IsFalse(heartburst.InPhase(3), "retired with its stage");
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(attacks, new float[2], 2f, 0f, false, 0, 2));
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(attacks, new float[2], 2f, 0f, false, 0, 3), "the next stage begins with its own");

        heartburst.untilPhase = 4;
        Assert.IsTrue(heartburst.InPhase(3), "an Until Phase past the next keeps it longer");
        Assert.IsFalse(heartburst.InPhase(4));

        heartburst.opener = false;
        heartburst.untilPhase = 0;
        Assert.IsTrue(heartburst.InPhase(3), "an everyday strike still runs to the end");
    }

    [Test]
    public void AFriendsCopy_LiesAsLongAsTheHostsStrikeTopple()
    {
        // Take Root: Open After 5 with Topple After; the boss's own Topple Seconds 3. The host's game says the topple is the strike's
        // (CreatureHost.StrikeToppleFlag); the copy finds the length from what it plays, played, or its kind's strikes.
        var takeRoot = new MonsterAttack { attack = Attack(), openAfter = 5f, toppleAfter = true };
        var slam = new MonsterAttack { attack = Attack(), openAfter = 1.2f };
        var attacks = new[] { slam, takeRoot };
        Assert.AreEqual(5f, MonsterAI.StrikeToppleSeconds(attacks, takeRoot, -1, 3f), "the strike it is playing");
        Assert.AreEqual(5f, MonsterAI.StrikeToppleSeconds(attacks, null, 1, 3f), "the strike it last played");
        Assert.AreEqual(5f, MonsterAI.StrikeToppleSeconds(attacks, slam, 0, 3f), "built after the strike began: its kind's");
        Assert.AreEqual(3f, MonsterAI.StrikeToppleSeconds(new[] { slam }, null, -1, 3f), "none of its kind: its own topple");
        Assert.AreEqual(3f, MonsterAI.StrikeToppleSeconds(null, null, 0, 3f));
    }

    [Test]
    public void AFriendsCopyBuiltMidFight_OnlyCatchesUpWithTheStagesItMissed()
    {
        // Built at 10% (the host's at stage 3 of 3), before it is awake: every stage is a catch-up (no reel told to the host, no rings).
        for (int stage = 0; stage < 3; stage++)
        {
            Assert.IsTrue(BossEncounter.CatchesUp(true, false, stage, 3), $"stage {stage + 1}");
        }

        // Awake, its health jumping past two marks: the first is passed through, the second is entered for real.
        Assert.IsTrue(BossEncounter.CatchesUp(true, true, 1, 3));
        Assert.IsFalse(BossEncounter.CatchesUp(true, true, 2, 3));
        Assert.IsFalse(BossEncounter.CatchesUp(true, true, 0, 1), "one stage at a time, as a fight goes");
        // The host's game, or a player alone, never catches up: every stage is its own.
        Assert.IsFalse(BossEncounter.CatchesUp(false, false, 0, 3));
        Assert.IsFalse(BossEncounter.CatchesUp(false, true, 1, 3));
    }

    [Test]
    public void AnEverydayStrike_IsReadyAfterItsCooldown_AsBefore()
    {
        var slam = new MonsterAttack { cooldown = 3f };
        Assert.AreEqual(4f, slam.ReadyAfter(1f), 1e-4f);
        Assert.AreEqual(1f + 3f * 0.85f, slam.ReadyAfter(1f, 0.85f), 1e-4f, "a fury's shorter waits");
    }

    // ----- C7: the root walls -----

    [Test]
    public void ARisingWall_NudgesThoseOnItsLine_OutThroughTheNearerSide()
    {
        // A wall 1.2 m thick and 12 m long, along its own z.
        var half = new Vector3(0.6f, 1f, 6f);
        float metres = RisingGround.NudgeOut(new Vector3(0.2f, 0f, 1f), half, 0.5f, out Vector3 way);
        Assert.AreEqual(Vector3.right, way, "out through the side, not along it");
        Assert.AreEqual(0.9f, metres, 1e-4f);

        metres = RisingGround.NudgeOut(new Vector3(-0.1f, 0f, 6.2f), half, 0.5f, out way);
        Assert.AreEqual(Vector3.forward, way, "near its end, past the end");
        Assert.AreEqual(0.3f, metres, 1e-4f);

        Assert.AreEqual(0f, RisingGround.NudgeOut(new Vector3(2f, 0f, 0f), half, 0.5f, out _), "clear already: left alone");
    }

    // ----- C10: the Hollow's Hold -----

    [Test]
    public void TheHollowsHold_LeavesNoGapInARing()
    {
        var points = new List<Vector3>();
        const float eruption = 2.5f;
        foreach (float radius in new[] { 18.5f, 14f, 9.5f })
        {
            EruptionRings.RingPoints(Vector3.zero, radius, eruption, 0.3f, 0.5f, points);
            Assert.GreaterOrEqual(points.Count, 3);
            Assert.Less(Vector3.Distance(points[0], points[1]), 2f * eruption, "neighbouring circles meet: nowhere in the ring to stand");
            foreach (Vector3 point in points)
            {
                Assert.AreEqual(radius, point.magnitude, 1e-3f, "on its ring");
            }
        }

        EruptionRings.RingPoints(Vector3.zero, 1f, eruption, 0.3f, 0f, points);
        Assert.AreEqual(3, points.Count, "a small ring still has three");
    }
}
