using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FinishingBlowTests
{
    private const float Arc = 90f;
    private const float Reach = 2.6f;

    private static bool CanFinish(bool staggered, bool can, Vector3 attacker) =>
        StrikeRules.CanFinish(staggered, can, Vector3.zero, Vector3.forward, attacker, Arc, Reach);

    [Test]
    public void CloseBehindAStaggeredCreatureIsAFinishingBlow()
    {
        Assert.IsTrue(CanFinish(true, true, new Vector3(0f, 0f, -2f)));
    }

    [Test]
    public void ItHasToBeStaggered()
    {
        Assert.IsFalse(CanFinish(false, true, new Vector3(0f, 0f, -2f)), "That is only a backstab");
    }

    [Test]
    public void ItHasToBeSomethingTheBodiesLineUpWith()
    {
        Assert.IsFalse(CanFinish(true, false, new Vector3(0f, 0f, -2f)));
    }

    [Test]
    public void ItHasToBeFromBehindAndClose()
    {
        Assert.IsFalse(CanFinish(true, true, new Vector3(0f, 0f, 2f)), "In front");
        Assert.IsFalse(CanFinish(true, true, new Vector3(0f, 0f, -4f)), "Too far");
    }

    [Test]
    public void TheHoldIsStraightBehindIt()
    {
        Vector3 spot = StrikeRules.FinishSpot(new Vector3(1f, 2f, 3f), Vector3.forward, 0.95f);
        Assert.AreEqual(1f, spot.x, 0.001f);
        Assert.AreEqual(2.05f, spot.z, 0.001f);
    }

    [TestCase("Draug", true)]
    [TestCase("BarrowShieldbearer", true)]
    [TestCase("ForestBittenWolf", false)]
    [TestCase("SmallMossTroll", false)]
    public void OnlyThePersonShapedTakeOne(string monster, bool can)
    {
        var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"Assets/_Project/Data/Monsters/{monster}.asset");
        Assert.AreEqual(can, definition.CanBeFinished);
    }
}
