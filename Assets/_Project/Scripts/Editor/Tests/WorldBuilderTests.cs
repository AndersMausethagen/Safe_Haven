using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class WorldBuilderTests
{
    private static SplineLine Straight(float length, bool closed = false) =>
        new SplineLine(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, length * 0.5f), new Vector3(0f, 0f, length) },
                       new[] { 0f, 0.5f, 1f }, closed, 2);

    // ---------------------------------------------------------------- per-knot values

    [Test]
    public void KnotValuesInterpolateBetweenKnotsAndDefaultToOne()
    {
        var values = new KnotValues();
        values.Resize(3);
        values[2] = 3f;
        Assert.AreEqual(1f, values.Evaluate(0.5f, 3, false), 1e-5f);
        Assert.AreEqual(2f, values.Evaluate(1.5f, 3, false), 1e-5f);
        Assert.AreEqual(3f, values.Evaluate(9f, 3, false), 1e-5f, "past the last knot of an open spline");
        Assert.AreEqual(2f, values.Evaluate(2.5f, 3, true), 1e-5f, "a closed spline wraps back to the first knot");
        Assert.AreEqual(1f, new KnotValues()[7], "missing values count as 1");
    }

    [Test]
    public void KnotValuesStayWithTheirKnots()
    {
        var values = new KnotValues();
        values.Resize(3);
        values[0] = 1f;
        values[1] = 3f;
        values[2] = 5f;
        values.Insert(1, 4);
        Assert.AreEqual(new[] { 1f, 2f, 3f, 5f }, new[] { values[0], values[1], values[2], values[3] });
        values.Remove(0, 3);
        Assert.AreEqual(new[] { 2f, 3f, 5f }, new[] { values[0], values[1], values[2] });
        values.Reverse();
        Assert.AreEqual(new[] { 5f, 3f, 2f }, new[] { values[0], values[1], values[2] });
    }

    // ---------------------------------------------------------------- geometry

    [Test]
    public void LineFieldGivesDistanceSideAndPlace()
    {
        SplineLine line = Straight(20f);
        var grid = new TerrainGrid(Vector2.zero - new Vector2(10f, 0f), new Vector2(20f, 20f), 21, true);
        LineField field = LineField.Build(line, grid, 3f);
        int left = field.IndexOf(8, 10);   // x = -2 (west of a line running north)
        int right = field.IndexOf(12, 10); // x = +2
        Assert.AreEqual(2f, field.Distance[left], 1e-4f);
        Assert.AreEqual(1, field.Side[left], "west is left when heading north");
        Assert.AreEqual(-1, field.Side[right]);
        Assert.IsTrue(float.IsPositiveInfinity(field.Distance[field.IndexOf(6, 10)]), "4 m away, outside a 3 m reach");
        float along = line.Lerp(line.Along, field.Segment[left], field.Fraction[left]);
        Assert.AreEqual(10f, along, 1e-4f);
    }

    [Test]
    public void ClosedLinesKnowTheirInside()
    {
        var square = new SplineLine(new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0), new Vector3(10, 0, 10), new Vector3(0, 0, 10), new Vector3(0, 0, 0) },
                                    new[] { 0f, 1f, 2f, 3f, 4f }, true, 4);
        Assert.IsTrue(square.Inside(new Vector2(5f, 5f)));
        Assert.IsFalse(square.Inside(new Vector2(15f, 5f)));
        Assert.AreEqual(2f, square.Nearest(new Vector2(5f, 2f), out _, out _), 1e-4f);
    }

    [Test]
    public void GridCellsCoverTheRectangle()
    {
        var grid = new TerrainGrid(Vector2.zero, new Vector2(100f, 100f), 101, true);
        RectInt cells = grid.Cells(new Rect(10f, 20f, 5f, 5f));
        Assert.LessOrEqual(cells.xMin, 10);
        Assert.GreaterOrEqual(cells.xMax, 16);
        Assert.AreEqual(new Vector2(10f, 20f), grid.World(10, 20));
    }

    // ---------------------------------------------------------------- shapes

    [Test]
    public void RiversNeverRunUphill()
    {
        float[] surface = RiverBuilder.Downhill(new[] { 10f, 9f, 9.5f, 7f, 8f, 8f, 4f, 5f });
        for (int i = 1; i < surface.Length; i++)
        {
            Assert.LessOrEqual(surface[i], surface[i - 1] + 1e-5f, $"sample {i} rises");
        }
    }

    [Test]
    public void RiverBedIsDeepestInTheMiddleAndBanksMeetTheGround()
    {
        const float water = 10f, around = 12f;
        Assert.AreEqual(water - 0.05f - 1.2f, RiverBuilder.Profile(0f, 2f, 1.2f, 3f, water, around), 1e-4f);
        Assert.Less(RiverBuilder.Profile(1.9f, 2f, 1.2f, 3f, water, around), water, "under water near the edge");
        Assert.Greater(RiverBuilder.Profile(2.1f, 2f, 1.2f, 3f, water, around), water, "the bank rises above the water");
        Assert.AreEqual(around, RiverBuilder.Profile(5f, 2f, 1.2f, 3f, water, around), 1e-4f);
        Assert.IsTrue(float.IsNaN(RiverBuilder.Profile(5.1f, 2f, 1.2f, 3f, water, around)), "beyond the banks");
    }

    [Test]
    public void PathWeightFadesOverTheSoftEdge()
    {
        Assert.AreEqual(1f, PathBuilder.Weight(1f, 1.1f, 1f));
        Assert.AreEqual(0.5f, PathBuilder.Weight(1.6f, 1.1f, 1f), 1e-4f);
        Assert.AreEqual(0f, PathBuilder.Weight(2.2f, 1.1f, 1f));
    }

    [Test]
    public void CliffsRaiseTheHighSideAndCutTheLowSide()
    {
        // Raise: the plateau goes up to the rim + height; the face climbs from the ground to it; the low side stays.
        Assert.AreEqual(16f, CliffBuilder.Shape(1f, 10f, 10f, 6f, 1.5f, 10f, CliffFeature.Shaping.Raise), 1e-4f);
        Assert.AreEqual(17f, CliffBuilder.Shape(1f, 17f, 10f, 6f, 1.5f, 10f, CliffFeature.Shaping.Raise), 1e-4f, "higher ground is kept");
        Assert.AreEqual(13f, CliffBuilder.Shape(-0.75f, 10f, 10f, 6f, 1.5f, 10f, CliffFeature.Shaping.Raise), 1e-4f, "halfway up the face");
        Assert.IsTrue(float.IsNaN(CliffBuilder.Shape(-3f, 10f, 10f, 6f, 1.5f, 10f, CliffFeature.Shaping.Raise)));
        // Cut: the low side drops by the height; the high side stays.
        Assert.AreEqual(4f, CliffBuilder.Shape(-3f, 10f, 10f, 6f, 1.5f, 10f, CliffFeature.Shaping.Cut), 1e-4f);
        Assert.IsTrue(float.IsNaN(CliffBuilder.Shape(2f, 10f, 10f, 6f, 1.5f, 10f, CliffFeature.Shaping.Cut)));
    }

    // ---------------------------------------------------------------- the ground under features

    [Test]
    public void HandEditsMoveTheKeptGround()
    {
        Assert.AreEqual(0.3f, WorldBuilder.AbsorbHeight(0.3f, 0.25f, 0.25f), 1e-6f, "nothing changed since the build");
        Assert.AreEqual(0.35f, WorldBuilder.AbsorbHeight(0.3f, 0.25f, 0.30f), 1e-6f, "sculpted up by 0.05");
    }

    [Test]
    public void StashKeepsAndReleasesCells()
    {
        var stash = ScriptableObject.CreateInstance<WorldTerrainStash>();
        try
        {
            stash.Prepare(33, 32, 2, 32);
            stash.SetHeight(5, 0.4f, 0.3f);
            stash.SetHeight(9, 0.5f, 0.5f);
            Assert.IsTrue(stash.TryGetHeight(5, out float baseHeight, out float last));
            Assert.AreEqual(0.4f, baseHeight);
            Assert.AreEqual(0.3f, last);
            stash.RemoveHeights(new HashSet<int> { 5 });
            Assert.IsFalse(stash.TryGetHeight(5, out _, out _));
            Assert.IsTrue(stash.TryGetHeight(9, out _, out _));

            stash.RemapHeights(0.5f, 0.1f);
            stash.TryGetHeight(9, out baseHeight, out _);
            Assert.AreEqual(0.35f, baseHeight, 1e-6f);

            stash.SetAlpha(3, new[] { 1f, 0f }, new[] { 0.2f, 0.8f });
            stash.Prepare(33, 32, 3, 32); // a layer was added: weights keep, the new layer is empty
            var b = new float[3];
            var l = new float[3];
            Assert.IsTrue(stash.TryGetAlpha(3, b, l));
            Assert.AreEqual(1f, b[0], 1e-2f);
            Assert.AreEqual(0.8f, l[1], 1e-2f);
            Assert.AreEqual(0f, l[2]);

            stash.Prepare(65, 32, 3, 32); // a new heightmap resolution: the old cells no longer line up
            Assert.AreEqual(0, stash.HeightCount);
        }
        finally
        {
            Object.DestroyImmediate(stash);
        }
    }

    // ---------------------------------------------------------------- paint and areas

    [Test]
    public void PaintRulesFollowSlopeAndHollows()
    {
        var steep = new TerrainPaintRules.Rule { strength = 1f, slope = new Vector2(40f, 90f), softness = 5f, patchiness = 0f };
        Assert.AreEqual(0f, WorldPaint.RuleWeight(steep, 20f, 0f, 0f, 0.5f));
        Assert.AreEqual(1f, WorldPaint.RuleWeight(steep, 60f, 0f, 0f, 0.5f));
        var hollow = new TerrainPaintRules.Rule { strength = 1f, slope = new Vector2(0f, 90f), hollows = 1f, softness = 5f, patchiness = 0f };
        Assert.AreEqual(1f, WorldPaint.RuleWeight(hollow, 5f, 0f, 0.5f, 0.5f));
        Assert.AreEqual(0f, WorldPaint.RuleWeight(hollow, 5f, 0f, -0.5f, 0.5f), "not on a ridge");
        Assert.Greater(WorldPaint.Hollowness(10f, 11f, 4f), 0f);
        Assert.Less(WorldPaint.Hollowness(10f, 9f, 4f), 0f);
    }

    [Test]
    public void ClumpingKeepsEverythingAtZeroAndOnlyClumpsAtOne()
    {
        Assert.AreEqual(1f, AreaBuilder.ClumpChance(0.1f, 0f));
        Assert.AreEqual(0f, AreaBuilder.ClumpChance(0.1f, 1f));
        Assert.AreEqual(1f, AreaBuilder.ClumpChance(0.9f, 1f));
    }

    [Test]
    public void SpacingKeepsObjectsApart()
    {
        var spacing = new WorldScatter.Spacing();
        spacing.Add(new Vector2(10f, 10f), 1f);
        Assert.IsFalse(spacing.IsFree(new Vector2(11.5f, 10f), 1f));
        Assert.IsTrue(spacing.IsFree(new Vector2(12.5f, 10f), 1f));
    }

    // ---------------------------------------------------------------- water and assets

    [Test]
    public void RiverWaterFollowsItsCentreLine()
    {
        var line = new[] { new Vector4(0f, 5f, 0f, 2f), new Vector4(0f, 3f, 10f, 1f) };
        WaterSurface.NearestOnCentreline(line, new Vector2(1f, 5f), out Vector4 at, out float distance);
        Assert.AreEqual(1f, distance, 1e-4f);
        Assert.AreEqual(4f, at.y, 1e-4f, "the surface height halfway down");
        Assert.AreEqual(1.5f, at.w, 1e-4f, "and the half width there");
    }

    [Test]
    public void DownloadedModelsGetReadableNames()
    {
        Assert.AreEqual("Rock Face A", WorldAssets.PrefabName("rock_face_01"));
        Assert.AreEqual("Mossy Boulder 3", WorldAssets.PrefabName("rock_moss_set_01_03"));
        Assert.AreEqual("Fern 2", WorldAssets.PrefabName("fern_02_02"));
    }

    // 2026-11-05: ravines and ledged walls for more verticality.
    [Test]
    public void WallProfile_ClimbsFromFootToTop_AndLedgesFlattenPartsOfIt()
    {
        Assert.AreEqual(0f, RavineBuilder.WallProfile(0f, 3, 0.8f), 1e-4f);
        Assert.AreEqual(1f, RavineBuilder.WallProfile(1f, 3, 0.8f), 1e-4f);
        Assert.AreEqual(0.4f, RavineBuilder.WallProfile(0.4f, 0, 1f), 1e-4f, "no ledges: an even face");
        float last = 0f, flattest = float.MaxValue;
        for (int i = 1; i <= 100; i++)
        {
            float v = RavineBuilder.WallProfile(i / 100f, 3, 0.8f);
            Assert.GreaterOrEqual(v, last - 1e-5f, "never dips");
            flattest = Mathf.Min(flattest, (v - last) * 100f);
            last = v;
        }

        Assert.Less(flattest, 0.5f, "ledges: somewhere the face rises at less than half its average rate");
    }
}
