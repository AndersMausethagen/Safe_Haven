using System.Linq;
using NUnit.Framework;
using UnityEditor;

/// <summary>
/// The Heartroot's Rootlings as the key to a topple (Docs/HeartrootPlaytest_2026-11-23.md P1, the user: "make killing the saplings be a key
/// to a topple"): a call topples it only when all of it dies in time, a late or unfinished call doesn't, and the Heartroot's Call Roots is
/// bound.
/// </summary>
public sealed class BoundSummonsTests
{
    private readonly object first = new object();
    private readonly object second = new object();

    [Test]
    public void KillingAWholeCallInTime_ToppleTheSummoner()
    {
        var ledger = new SummonBondLedger();
        ledger.Called(new[] { first, second }, 10f, 12f, 4f);
        Assert.IsTrue(ledger.Died(first, 13f, out float afterFirst));
        Assert.AreEqual(0f, afterFirst, "one of two: a shock, no topple yet");
        Assert.IsTrue(ledger.Died(second, 18f, out float afterSecond));
        Assert.AreEqual(4f, afterSecond, "the whole call within 12 s: it topples");
        Assert.AreEqual(0, ledger.Bound);
    }

    [Test]
    public void ACallKilledLate_OrLeftUnfinished_DoesNotTopple()
    {
        var ledger = new SummonBondLedger();
        ledger.Called(new[] { first, second }, 10f, 12f, 4f);
        ledger.Died(first, 15f, out _);
        Assert.IsTrue(ledger.Died(second, 30f, out float late));
        Assert.AreEqual(0f, late, "20 s after the call: too late");

        var unfinished = new SummonBondLedger();
        unfinished.Called(new[] { first, second }, 0f, 12f, 4f);
        unfinished.Forget(second);
        Assert.IsTrue(unfinished.Died(first, 2f, out float gone));
        Assert.AreEqual(0f, gone, "one went without dying: the call can't be finished");
    }

    [Test]
    public void SummonsOfTwoCalls_AreCountedApart()
    {
        var ledger = new SummonBondLedger();
        object third = new object();
        ledger.Called(new[] { first }, 0f, 12f, 4f);
        ledger.Called(new[] { second, third }, 5f, 12f, 4f);
        Assert.IsTrue(ledger.Died(second, 6f, out float partOfSecond));
        Assert.AreEqual(0f, partOfSecond, "the second call still has one up");
        Assert.IsTrue(ledger.Died(first, 7f, out float wholeFirst));
        Assert.AreEqual(4f, wholeFirst, "the first call was one Rootling, now dead");
        Assert.IsFalse(ledger.Died(new object(), 8f, out _), "a creature it never called isn't bound");
    }

    [Test]
    public void TheHeartrootsCall_IsBound()
    {
        var heartroot = AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/TheHeartroot.asset");
        Assert.IsNotNull(heartroot);
        MonsterAttack call = heartroot.Attacks.FirstOrDefault(a => a != null && a.summonPrefab != null);
        Assert.IsNotNull(call, "it calls Rootlings");
        Assert.Greater(call.bondPoiseShare, 0f, "each Rootling killed shakes it");
        Assert.Greater(call.bondToppleSeconds, 0f, "a whole call killed floors it");
        Assert.Greater(call.bondWithin, 0f);
        Assert.IsTrue(heartroot.Topples, "it is a creature that topples");
    }
}
