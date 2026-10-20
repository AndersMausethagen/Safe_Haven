using NUnit.Framework;

public sealed class StaminaTests
{
    [Test]
    public void StartsFull()
    {
        var pool = new StaminaPool(100f);
        Assert.AreEqual(100f, pool.Current);
        Assert.IsTrue(pool.IsFull);
    }

    [Test]
    public void SpendingReducesStamina()
    {
        var pool = new StaminaPool(100f);
        Assert.IsTrue(pool.TrySpend(25f));
        Assert.AreEqual(75f, pool.Current, 0.001f);
    }

    [TestCase(15f)]
    [TestCase(20f)]
    [TestCase(25f)]
    [TestCase(40f)]
    public void InsufficientStaminaRefusesTheActionWithoutSpending(float cost)
    {
        var pool = new StaminaPool(100f);
        Assert.IsTrue(pool.TrySpend(100f - (cost - 0.01f)));
        float remaining = pool.Current;

        Assert.IsFalse(pool.TrySpend(cost));
        Assert.AreEqual(remaining, pool.Current);
        Assert.IsFalse(pool.IsEmpty);
    }

    [TestCase(15f)]
    [TestCase(20f)]
    [TestCase(25f)]
    [TestCase(40f)]
    public void ExactStaminaPaysTheFullCost(float cost)
    {
        var pool = new StaminaPool(100f);
        Assert.IsTrue(pool.TrySpend(100f - cost));
        Assert.IsTrue(pool.TrySpend(cost));
        Assert.AreEqual(0f, pool.Current);
        Assert.IsTrue(pool.IsEmpty);
    }

    [Test]
    public void CostAboveMaximumIsRefusedWithoutSpending()
    {
        var pool = new StaminaPool(100f);
        Assert.IsFalse(pool.TrySpend(101f));
        Assert.AreEqual(100f, pool.Current);
    }

    [Test]
    public void ZeroCostDoesNotSpendFromANonemptyPool()
    {
        var pool = new StaminaPool(100f);
        Assert.IsTrue(pool.TrySpend(0f));
        Assert.AreEqual(100f, pool.Current);
    }

    [Test]
    public void EmptyPoolRefusesActions()
    {
        var pool = new StaminaPool(100f);
        pool.TrySpend(100f);
        Assert.IsFalse(pool.TrySpend(15f));
        Assert.IsFalse(pool.TrySpend(0f));
        Assert.AreEqual(0f, pool.Current);
    }

    [Test]
    public void RegenerationAllowsActionsAgainAndStopsAtMax()
    {
        var pool = new StaminaPool(100f);
        pool.TrySpend(100f);
        Assert.IsTrue(pool.Regenerate(10f));
        Assert.IsFalse(pool.TrySpend(15f));
        Assert.AreEqual(10f, pool.Current);
        Assert.IsTrue(pool.Regenerate(5f));
        Assert.IsTrue(pool.TrySpend(15f));
        Assert.AreEqual(0f, pool.Current);

        pool.Regenerate(500f);
        Assert.AreEqual(100f, pool.Current);
        Assert.IsFalse(pool.Regenerate(5f));
    }
}
