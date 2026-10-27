using NUnit.Framework;
using UnityEngine;

/// <summary>The memory of explored ground: soft circles, never forgotten, saved as bits.</summary>
public sealed class ExplorationGridTests
{
    private static ExplorationGrid Grid() => new ExplorationGrid(new Rect(-50f, -50f, 100f, 100f), 1f);

    [Test]
    public void StartsUnexplored_AndOutsideIsNeverExplored()
    {
        ExplorationGrid grid = Grid();
        Assert.AreEqual(100, grid.Width);
        Assert.AreEqual(0f, grid.At(Vector2.zero));
        Assert.AreEqual(0f, grid.At(new Vector2(500f, 0f)));
    }

    [Test]
    public void Reveal_IsFullInside_SoftAtTheEdge_NothingBeyond()
    {
        ExplorationGrid grid = Grid();
        Assert.IsTrue(grid.Reveal(Vector2.zero, 10f, 0.4f));
        Assert.AreEqual(1f, grid.At(new Vector2(0.5f, 0.5f)), 1e-3f);
        float edge = grid.At(new Vector2(8.5f, 0.5f));
        Assert.Greater(edge, 0f);
        Assert.Less(edge, 1f);
        Assert.AreEqual(0f, grid.At(new Vector2(12f, 0f)));
    }

    [Test]
    public void Reveal_NeverForgets_AndReportsOnlyChanges()
    {
        ExplorationGrid grid = Grid();
        grid.Reveal(Vector2.zero, 10f, 0.4f);
        Assert.IsFalse(grid.Reveal(Vector2.zero, 4f, 0.4f), "a smaller circle inside adds nothing");
        Assert.AreEqual(1f, grid.At(new Vector2(5.5f, 0.5f)), 1e-3f, "and doesn't lower what was seen");
    }

    [Test]
    public void Bits_RoundTrip_AndRejectOtherSizes()
    {
        ExplorationGrid grid = Grid();
        grid.Reveal(new Vector2(20f, -10f), 6f, 0.2f);
        string saved = grid.ToBits();
        ExplorationGrid loaded = Grid();
        Assert.IsTrue(loaded.FromBits(saved));
        Assert.AreEqual(1f, loaded.At(new Vector2(20.5f, -9.5f)));
        Assert.AreEqual(0f, loaded.At(Vector2.zero));
        Assert.IsFalse(new ExplorationGrid(new Rect(0f, 0f, 30f, 30f), 1f).FromBits(saved), "another grid size");
        Assert.IsFalse(Grid().FromBits("not base64!"));
    }
}
