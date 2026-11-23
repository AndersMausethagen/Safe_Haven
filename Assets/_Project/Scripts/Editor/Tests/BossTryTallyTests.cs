using NUnit.Framework;
using UnityEngine;

/// <summary>
/// A boss try in the playtest log (Docs/HeartrootPlaytest_2026-11-23.md P0): the user's first Heartroot try left no row of the boss's
/// side, so whether it ever toppled couldn't be told. The tally sorts the player's damage by kind of blow and writes the try in one row.
/// </summary>
public sealed class BossTryTallyTests
{
    private static DamageInfo Hit(DamageType type, HitTechnique technique, AttackDefinition attack = null) =>
        new DamageInfo(null, attack, 10f, type, false, Vector3.zero, Vector3.forward, 0f, technique);

    [Test]
    public void EachBlow_IsSortedByWhatItWas()
    {
        var attack = ScriptableObject.CreateInstance<AttackDefinition>();
        try
        {
            Assert.AreEqual("fire", BossTryTally.KindOf(Hit(DamageType.Fire, HitTechnique.OverTime)), "a coating's fire or a burn");
            Assert.AreEqual("heart", BossTryTally.KindOf(Hit(DamageType.Physical, HitTechnique.Finisher)), "the heart strike has no attack");
            Assert.AreEqual("backstab", BossTryTally.KindOf(Hit(DamageType.Physical, HitTechnique.Melee | HitTechnique.Backstab, attack)));
            Assert.AreEqual("charged", BossTryTally.KindOf(Hit(DamageType.Physical, HitTechnique.Melee | HitTechnique.Heavy | HitTechnique.Charged, attack)));
            Assert.AreEqual("heavy", BossTryTally.KindOf(Hit(DamageType.Physical, HitTechnique.Melee | HitTechnique.Heavy, attack)));
            Assert.AreEqual("art", BossTryTally.KindOf(Hit(DamageType.Physical, HitTechnique.Melee | HitTechnique.WeaponArt, attack)));
            Assert.AreEqual("light", BossTryTally.KindOf(Hit(DamageType.Physical, HitTechnique.Melee, attack)));
            Assert.AreEqual("arrow", BossTryTally.KindOf(Hit(DamageType.Physical, HitTechnique.Shot, attack)));
        }
        finally
        {
            Object.DestroyImmediate(attack);
        }
    }

    [Test]
    public void TheRow_SaysHowFarItGotAndWhatOpenedItUp()
    {
        var tally = new BossTryTally { WokeAt = 100f, Stage = 1, Calls = 6, SummonsKilled = 11, Draughts = 1, LastBlowOnPlayer = "HeartrootEruption" };
        tally.Toppled(ToppleCause.Bond);
        tally.Toppled(ToppleCause.Bond);
        tally.Toppled(ToppleCause.Parry);
        tally.Dealt(Hit(DamageType.Physical, HitTechnique.Finisher), 130f);
        tally.Dealt(Hit(DamageType.Fire, HitTechnique.OverTime), 40f);
        tally.Dealt(Hit(DamageType.Fire, HitTechnique.OverTime), 0f);
        Assert.AreEqual(2, tally.ToppledBy(ToppleCause.Bond));
        Assert.AreEqual(1, tally.HeartStrikes, "a heart strike counts as one");
        Assert.AreEqual(40f, tally.DamageOf("fire"), "a hit that took nothing adds nothing");

        string row = tally.Detail("died", 350f);
        StringAssert.StartsWith("died; 250 s; stage 2", row);
        StringAssert.Contains("bond 2", row);
        StringAssert.Contains("parry 1", row);
        StringAssert.Contains("heart 1", row);
        StringAssert.Contains("summons killed 11", row);
        StringAssert.Contains("heart 130", row);
        StringAssert.Contains("fire 40", row);
        StringAssert.Contains("last blow HeartrootEruption", row);
    }
}
