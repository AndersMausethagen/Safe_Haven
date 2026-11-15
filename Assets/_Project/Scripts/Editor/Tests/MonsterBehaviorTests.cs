using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MonsterBehaviorTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object value in created)
        {
            Object.DestroyImmediate(value);
        }
        created.Clear();
    }

    [Test]
    public void Selection_UsesPriorityAndListOrder()
    {
        MonsterAttack first = Action(2), second = Action(5), third = Action(5);
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(new[] { first, second, third }, new float[3], 2f, 0f, false));
    }

    [Test]
    public void Selection_RespectsIndependentCooldownsAndRange()
    {
        MonsterAttack shove = Action(10), stones = Action(1);
        shove.maximumRange = 2f;
        stones.minimumRange = 3f;
        stones.maximumRange = 14f;
        var entries = new[] { shove, stones };
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(entries, new float[2], 6f, 0f, false));
        Assert.AreEqual(-1, MonsterAttacks.SelectAttack(entries, new[] { 0f, 5f }, 6f, 4f, false));
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(entries, new[] { 0f, 5f }, 6f, 5f, false));
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(entries, new[] { 0f, 5f }, 1f, 4f, false));
    }

    [Test]
    public void Selection_HowlRequiresLeader()
    {
        MonsterAttack bite = Action(1), howl = Action(10);
        howl.packLeaderOnly = true;
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(new[] { bite, howl }, new float[2], 1f, 0f, false));
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(new[] { bite, howl }, new float[2], 1f, 0f, true));
    }

    [Test]
    public void Selection_WeightlessKeepsPriorityWhateverTheRoll()
    {
        MonsterAttack cleave = Action(10), lunge = Action(20);
        foreach (float roll in new[] { 0f, 0.5f, 0.99f })
        {
            Assert.AreEqual(1, MonsterAttacks.SelectAttack(new[] { cleave, lunge }, new float[2], 1f, 0f, false, roll: roll));
        }
    }

    [Test]
    public void Selection_WeightedShareTheirBandByChance()
    {
        MonsterAttack cleave = Action(10), lunge = Action(20);
        cleave.weight = 3f;
        lunge.weight = 1f;
        var both = new[] { cleave, lunge };
        // Cleave holds the first three quarters of the roll, the lunge the last.
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(both, new float[2], 1f, 0f, false, roll: 0.1f));
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(both, new float[2], 1f, 0f, false, roll: 0.74f));
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(both, new float[2], 1f, 0f, false, roll: 0.76f));
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(both, new float[2], 1f, 0f, false, roll: 1f));
        // A spent one isn't a candidate.
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(both, new[] { 0f, 5f }, 1f, 0f, false, roll: 0.9f));
    }

    [Test]
    public void Selection_ARepeatCountsHalfAndAnAnswerToTheFoeMore()
    {
        MonsterAttack cleave = Action(10), lunge = Action(20);
        cleave.weight = lunge.weight = 1f;
        lunge.whenFoe = FoeDoing.BackingOff;
        var both = new[] { cleave, lunge };
        Assert.AreEqual(MonsterAttacks.RepeatPenalty, MonsterAttacks.ChoiceWeight(cleave, true, FoeDoing.None), 1e-5f);
        Assert.AreEqual(MonsterAttacks.FoeBonus, MonsterAttacks.ChoiceWeight(lunge, false, FoeDoing.BackingOff | FoeDoing.Guarding), 1e-5f);
        Assert.AreEqual(1f, MonsterAttacks.ChoiceWeight(lunge, false, FoeDoing.Guarding), 1e-5f);
        // The cleave just used counts 0.5 against the lunge's 1: a third of the roll.
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(both, new float[2], 1f, 0f, false, last: 0, roll: 0.3f));
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(both, new float[2], 1f, 0f, false, last: 0, roll: 0.36f));
        // The foe backing off: the lunge counts 3 against 1, three quarters of the roll.
        Assert.AreEqual(1, MonsterAttacks.SelectAttack(both, new float[2], 1f, 0f, false, foe: FoeDoing.BackingOff, roll: 0.3f));
        Assert.AreEqual(0, MonsterAttacks.SelectAttack(both, new float[2], 1f, 0f, false, foe: FoeDoing.BackingOff, roll: 0.2f));
    }

    [Test]
    public void Selection_AReadyWeightlessActionAboveTheBandStillGoesFirst()
    {
        MonsterAttack cleave = Action(10), lunge = Action(20), warCry = Action(25), stones = Action(15);
        cleave.weight = lunge.weight = 1f;
        // A weightless call above them all wins outright.
        Assert.AreEqual(2, MonsterAttacks.SelectAttack(new[] { cleave, lunge, warCry }, new float[3], 1f, 0f, false, roll: 0.9f));
        // A weightless throw in the middle keeps what is below it out of the band: only the lunge is left.
        foreach (float roll in new[] { 0f, 0.5f, 1f })
        {
            Assert.AreEqual(1, MonsterAttacks.SelectAttack(new[] { cleave, lunge, stones }, new float[3], 1f, 0f, false, roll: roll));
        }
    }

    [Test]
    public void Telegraph_HeavyWhenMarkedOrHardHitting()
    {
        Assert.IsTrue(AttackTelegraph.IsHeavy(true, 1f, 1.5f), "a creature's great blow");
        Assert.IsTrue(AttackTelegraph.IsHeavy(false, 1.5f, 1.5f), "a weighty attack counts anyway");
        Assert.IsFalse(AttackTelegraph.IsHeavy(false, 1.2f, 1.5f), "an everyday strike");
    }

    [Test]
    public void Poise_AFriendsCopyShowsTheHostsShare()
    {
        var meter = new PoiseMeter(140f, 2f, 15f);
        meter.Show(0.25f);
        Assert.AreEqual(0.25f, meter.Fraction, 1e-5f);
        meter.Show(2f);
        Assert.AreEqual(1f, meter.Fraction, 1e-5f);
    }

    [Test]
    public void Reeling_SwaysBothWaysAndNotAtRest()
    {
        Assert.AreEqual(0f, ReelingTell.Sway(0f, 0.3f, 4f, 1f), 1e-5f);
        Assert.AreEqual(4f, ReelingTell.Sway(1f, 0.25f, 4f, 1f), 1e-4f);
        Assert.AreEqual(-4f, ReelingTell.Sway(1f, 0.75f, 4f, 1f), 1e-4f);
    }

    [Test]
    public void Felling_TheWeightyVerbsFellAReelingCreature()
    {
        foreach (HitTechnique fells in new[] { HitTechnique.Heavy, HitTechnique.Charged, HitTechnique.WeaponArt, HitTechnique.GuardCounter, HitTechnique.Melee | HitTechnique.Heavy })
        {
            Assert.IsTrue(GuardRules.FellsReeling(fells), fells.ToString());
        }

        foreach (HitTechnique not in new[] { HitTechnique.None, HitTechnique.Melee, HitTechnique.Rhythm, HitTechnique.Shot, HitTechnique.Backstab })
        {
            Assert.IsFalse(GuardRules.FellsReeling(not), not.ToString());
        }
    }

    [Test]
    public void WeaponGuard_RaisedByLightBlowsFromItsTierAndTurnsOnlyThoseAside()
    {
        Assert.IsTrue(GuardRules.IsLight(HitTechnique.Melee));
        Assert.IsTrue(GuardRules.IsLight(HitTechnique.Melee | HitTechnique.Rhythm));
        Assert.IsTrue(GuardRules.IsLight(HitTechnique.Shot), "an arrow is turned aside like a light blow");
        foreach (HitTechnique heavy in new[] { HitTechnique.Heavy, HitTechnique.Charged, HitTechnique.WeaponArt, HitTechnique.Finisher, HitTechnique.Backstab, HitTechnique.Counter, HitTechnique.GuardCounter, HitTechnique.OverTime })
        {
            Assert.IsFalse(GuardRules.IsLight(heavy | HitTechnique.Melee), heavy.ToString());
        }

        Assert.IsTrue(GuardRules.RaisesGuard(3, 3, 2, 2));
        Assert.IsFalse(GuardRules.RaisesGuard(2, 3, 2, 2), "not yet enough blows");
        Assert.IsFalse(GuardRules.RaisesGuard(5, 3, 1, 2), "below the tier it learns it");
        Assert.IsFalse(GuardRules.RaisesGuard(9, 0, 5, 0), "0 needed: never");
    }

    [Test]
    public void ArrowDodge_OnlyFromABowHeldPastItsPerfectRelease()
    {
        Assert.IsTrue(GuardRules.SidestepsDraw(true, true, 1f, false, false, true));
        Assert.IsFalse(GuardRules.SidestepsDraw(true, true, 1f, true, false, true), "never from a perfect release");
        Assert.IsFalse(GuardRules.SidestepsDraw(true, true, 0.8f, false, false, true), "not before the draw is full");
        Assert.IsFalse(GuardRules.SidestepsDraw(true, false, 1f, false, false, true), "aimed at someone else");
        Assert.IsFalse(GuardRules.SidestepsDraw(true, true, 1f, false, true, true), "one roll per draw");
        Assert.IsFalse(GuardRules.SidestepsDraw(true, true, 1f, false, false, false), "its step still cooling down");
        Assert.IsFalse(GuardRules.SidestepsDraw(false, true, 1f, false, false, true));
    }

    [Test]
    public void Poise_BreakingRefillsItAsABreakDoes()
    {
        var meter = new PoiseMeter(100f, 2f, 15f);
        meter.Hit(80f, 0f);
        Assert.AreEqual(0.2f, meter.Fraction, 1e-5f);
        meter.Break(1f);
        Assert.AreEqual(1f, meter.Fraction, 1e-5f);
        meter.Hit(30f, 1f);
        meter.Tick(2f, 1f);
        Assert.AreEqual(0.7f, meter.Fraction, 1e-5f, "a break counts as a hit: it waits its delay before coming back");
    }

    [Test]
    public void Selection_SkipsIncompleteEntries()
    {
        Assert.AreEqual(-1, MonsterAttacks.SelectAttack(new MonsterAttack[] { null, new MonsterAttack() }, new float[2], 1f, 0f, true));
    }

    [Test]
    public void Definition_DefaultsPreserveLegacyChaser()
    {
        MonsterDefinition monster = ScriptableObject.CreateInstance<MonsterDefinition>();
        created.Add(monster);
        Assert.AreEqual(0, monster.Attacks.Count);
        Assert.AreEqual(MonsterMovementTactic.Chase, monster.MovementTactic);
        Assert.IsTrue(monster.HasTemperament(MonsterTemperament.Aggressive));
        Assert.IsFalse(monster.HasTemperament(MonsterTemperament.Cowardly));
    }

    [Test]
    public void Definition_AllowsCombinedTemperaments()
    {
        MonsterDefinition monster = ScriptableObject.CreateInstance<MonsterDefinition>();
        created.Add(monster);
        var data = new SerializedObject(monster);
        data.FindProperty("temperament").intValue = (int)(MonsterTemperament.Aggressive | MonsterTemperament.Cowardly);
        data.ApplyModifiedPropertiesWithoutUndo();
        Assert.IsTrue(monster.HasTemperament(MonsterTemperament.Aggressive));
        Assert.IsTrue(monster.HasTemperament(MonsterTemperament.Cowardly));
        Assert.IsFalse(monster.HasTemperament(MonsterTemperament.Defensive));
    }

    private MonsterAttack Action(int priority)
    {
        AttackDefinition attack = ScriptableObject.CreateInstance<AttackDefinition>();
        created.Add(attack);
        return new MonsterAttack { attack = attack, priority = priority, maximumRange = 10f };
    }
}
