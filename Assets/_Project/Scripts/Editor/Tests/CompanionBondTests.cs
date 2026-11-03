using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CompanionBondTests
{
    private static readonly List<CompanionBondRank> Ranks = new List<CompanionBondRank>
    {
        new CompanionBondRank { name = "Trusted", raids = 3 },
        new CompanionBondRank { name = "Sworn", raids = 8 },
    };

    [TestCase(0, null)]
    [TestCase(2, null)]
    [TestCase(3, "Trusted")]
    [TestCase(7, "Trusted")]
    [TestCase(8, "Sworn")]
    [TestCase(30, "Sworn")]
    public void RaidsTogetherMakeARank(int raids, string rank)
    {
        Assert.AreEqual(rank, CompanionBond.RankFor(raids, Ranks)?.name);
    }

    [Test]
    public void TheBiggestThingIsWhatStays()
    {
        Assert.AreEqual(CompanionMemory.BossBeaten, CompanionBond.Remember(false, 1, 3, true, 5f, 9));
        Assert.AreEqual(CompanionMemory.HelpedUp, CompanionBond.Remember(false, 0, 1, true, 5f, 9));
        Assert.AreEqual(CompanionMemory.Failed, CompanionBond.Remember(false, 0, 0, true, 5f, 9));
        Assert.AreEqual(CompanionMemory.RoseInRank, CompanionBond.Remember(true, 0, 0, true, 5f, 9));
        Assert.AreEqual(CompanionMemory.CloseCall, CompanionBond.Remember(true, 0, 0, false, 30f, 9));
        Assert.AreEqual(CompanionMemory.HardFight, CompanionBond.Remember(true, 0, 0, false, 500f, 9));
        Assert.AreEqual(CompanionMemory.None, CompanionBond.Remember(true, 0, 0, false, 500f, 1), "A quiet raid leaves nothing to say");
    }

    [Test]
    public void TheirOwnWordsComeFirstAndStayTheSame()
    {
        var own = new List<CompanionMemoryLines> { new CompanionMemoryLines { kind = CompanionMemory.HelpedUp, lines = new[] { "mine" } } };
        var general = new List<CompanionMemoryLines>
        {
            new CompanionMemoryLines { kind = CompanionMemory.HelpedUp, lines = new[] { "theirs" } },
            new CompanionMemoryLines { kind = CompanionMemory.Failed, lines = new[] { "a", "b", "c" } },
        };

        Assert.AreEqual("mine", CompanionBond.LineFor(CompanionMemory.HelpedUp, "x", own, general));
        string once = CompanionBond.LineFor(CompanionMemory.Failed, "sigrid", own, general);
        Assert.AreEqual(once, CompanionBond.LineFor(CompanionMemory.Failed, "sigrid", own, general));
        Assert.IsNull(CompanionBond.LineFor(CompanionMemory.None, "x", own, general));
    }

    [Test]
    public void ARaidTogetherGrowsTheBond()
    {
        var settlement = new SettlementState();
        var sigrid = ScriptableObject.CreateInstance<ResidentDefinition>();
        settlement.AddResident(sigrid);
        Assert.IsFalse(settlement.RecordRaidAlong(sigrid, Ranks, true, 0, 0, 500f, 2));
        Assert.IsFalse(settlement.RecordRaidAlong(sigrid, Ranks, true, 0, 0, 500f, 2));
        Assert.IsTrue(settlement.RecordRaidAlong(sigrid, Ranks, true, 0, 1, 500f, 3), "The third reaches Trusted");
        ResidentRecord record = settlement.Find(sigrid);
        Assert.AreEqual(3, record.RaidsAlong);
        Assert.AreEqual(7, record.KillsAlong);
        Assert.AreEqual(1, record.TimesHelpedUp);
        Assert.AreEqual(CompanionMemory.HelpedUp, record.Memory);
        Object.DestroyImmediate(sigrid);
    }

    [Test]
    public void ACairnRemembersHowLong()
    {
        var settlement = new SettlementState();
        var sigrid = ScriptableObject.CreateInstance<ResidentDefinition>();
        settlement.AddResident(sigrid);
        settlement.RecordRaidAlong(sigrid, Ranks, true, 0, 0, 500f, 0);
        settlement.RecordRaidAlong(sigrid, Ranks, true, 0, 0, 500f, 0);
        settlement.LoseResident(sigrid, "There");
        Assert.AreEqual(2, settlement.Fallen[0].RaidsAlong);
        Object.DestroyImmediate(sigrid);
    }

    [Test]
    public void TheBondIsInTheSaveFormat()
    {
        var saved = new ResidentSaveData { id = "x", raidsAlong = 5, killsAlong = 12, timesHelpedUp = 2, memory = (int)CompanionMemory.BossBeaten };
        ResidentSaveData back = JsonUtility.FromJson<ResidentSaveData>(JsonUtility.ToJson(saved));
        Assert.AreEqual(5, back.raidsAlong);
        Assert.AreEqual((int)CompanionMemory.BossBeaten, back.memory);
    }

    [Test]
    public void TheDataIsThere()
    {
        Assert.AreEqual(2, SettlementSettings.Current.BondRanks.Count);
        Assert.IsNotEmpty(SettlementSettings.Current.MemoryLines);
        var sigrid = AssetDatabase.LoadAssetAtPath<ResidentDefinition>("Assets/_Project/Data/Prologue/Residents/Sigrid.asset");
        Assert.IsNotEmpty(sigrid.MemoryLines);
    }
}
