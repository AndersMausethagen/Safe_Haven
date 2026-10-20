using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class RaidMapTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void RotateOffset_TurnsLikeTheQuaternionUsedForSceneObjects(int quarterTurns)
    {
        var offset = new Vector2(3f, -7f);
        Vector3 expected = Quaternion.Euler(0f, MapRotation.Yaw(quarterTurns), 0f) * new Vector3(offset.x, 0f, offset.y);

        Vector2 turned = MapRotation.RotateOffset(offset, quarterTurns);

        Assert.AreEqual(expected.x, turned.x, 1e-4f);
        Assert.AreEqual(expected.z, turned.y, 1e-4f);
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    public void RotateGrid_MovesACellTheSameWayAsAnObjectOnTheMap(int quarterTurns)
    {
        const int size = 5;
        var grid = new float[size, size];
        grid[1, 4] = 1f; // row = z, column = x

        float[,] turned = MapRotation.RotateGrid(grid, quarterTurns);

        float centre = (size - 1) / 2f;
        Vector2 offset = MapRotation.RotateOffset(new Vector2(4 - centre, 1 - centre), quarterTurns);
        Assert.AreEqual(1f, turned[Mathf.RoundToInt(offset.y + centre), Mathf.RoundToInt(offset.x + centre)]);
    }

    [Test]
    public void RotateGrid_FourQuarterTurnsGiveTheSameGrid()
    {
        var grid = new float[4, 4];
        for (int i = 0; i < 16; i++) grid[i / 4, i % 4] = i;

        float[,] turned = grid;
        for (int i = 0; i < 4; i++) turned = MapRotation.RotateGrid(turned, 1);

        CollectionAssert.AreEqual(grid, turned);
    }

    [Test]
    public void RotateGrid_RejectsNonSquareGrids()
    {
        Assert.Throws<System.ArgumentException>(() => MapRotation.RotateGrid(new float[2, 3], 1));
    }

    [Test]
    public void RotateLayers_KeepsEachCellsPaintTogether()
    {
        var layers = new float[3, 3, 2];
        layers[0, 2, 0] = 0.25f;
        layers[0, 2, 1] = 0.75f;

        float[,,] turned = MapRotation.RotateLayers(layers, 1);

        MapRotation.RotateIndex(0, 2, 3, 1, out int row, out int column);
        Assert.AreEqual(0.25f, turned[row, column, 0]);
        Assert.AreEqual(0.75f, turned[row, column, 1]);
    }

    [Test]
    public void RotateTrees_TurnsPositionAndFacing()
    {
        var trees = new[] { new TreeInstance { position = new Vector3(0.75f, 0.2f, 0.5f), rotation = 0f } };

        MapRotation.RotateTrees(trees, 1);

        Assert.AreEqual(0.5f, trees[0].position.x, 1e-5f);
        Assert.AreEqual(0.25f, trees[0].position.z, 1e-5f);
        Assert.AreEqual(0.2f, trees[0].position.y, 1e-5f);
        Assert.AreEqual(Mathf.PI / 2f, trees[0].rotation, 1e-5f);
    }

    [Test]
    public void Pick_IsTheSameForASeedAndUsesAllFourDirections()
    {
        Assert.AreEqual(MapRotation.Pick(1234), MapRotation.Pick(1234));

        var seen = new bool[4];
        for (int seed = 0; seed < 200; seed++) seen[MapRotation.Pick(seed)] = true;
        CollectionAssert.AreEqual(new[] { true, true, true, true }, seen);
    }

    [Test]
    public void Decode_FindsEachBlobAtItsCentreAndIgnoresUnknownColours()
    {
        const int width = 10;
        const int height = 10;
        var pixels = new Color32[width * height];
        void Paint(int x, int y, Color32 colour) => pixels[y * width + x] = colour;

        Color32 red = RaidMapMarkers.ColorOf(RaidSiteKind.EnemyCamp);
        Paint(1, 1, red);
        Paint(2, 1, red);
        Paint(7, 8, RaidMapMarkers.ColorOf(RaidSiteKind.Extraction));
        Paint(5, 5, new Color32(128, 128, 128, 255));
        Paint(8, 2, new Color32(250, 10, 5, 255)); // slightly off red still counts

        var sites = RaidMapMarkers.Decode(pixels, width, height);

        Assert.AreEqual(3, sites.Count);
        Assert.AreEqual(RaidSiteKind.Extraction, sites[0].Kind);
        Assert.AreEqual(0.75f, sites[0].Position.x, 1e-5f);
        Assert.AreEqual(0.85f, sites[0].Position.y, 1e-5f);
        Assert.AreEqual(RaidSiteKind.EnemyCamp, sites[1].Kind);
        Assert.AreEqual(0.2f, sites[1].Position.x, 1e-5f);
        Assert.AreEqual(0.15f, sites[1].Position.y, 1e-5f);
        Assert.AreEqual(RaidSiteKind.EnemyCamp, sites[2].Kind);
        Assert.AreEqual(0.85f, sites[2].Position.x, 1e-5f);
    }

    [Test]
    public void ShareOut_GivesWholeSitesToGroupsInTurn()
    {
        CollectionAssert.AreEqual(new[] { 0, 1, 0, 1, 0 }, RaidMapMarkers.ShareOut(5, 2));
        CollectionAssert.AreEqual(new[] { -1, -1 }, RaidMapMarkers.ShareOut(2, 0));
    }

    // ---------------------------------------------------------------- Painted zones

    [Test]
    public void ZoneSample_MatchesTextureBilinearConvention()
    {
        const int size = 8;
        var grid = new byte[size * size];
        var texture = new Texture2D(size, size, TextureFormat.R8, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var random = new System.Random(3);
        var pixels = new Color32[size * size];
        for (int i = 0; i < grid.Length; i++)
        {
            grid[i] = (byte)random.Next(256);
            pixels[i] = new Color32(grid[i], 0, 0, 255);
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        try
        {
            for (int n = 0; n < 50; n++)
            {
                float u = (float)random.NextDouble() * 1.2f - 0.1f;
                float v = (float)random.NextDouble() * 1.2f - 0.1f;
                Assert.AreEqual(texture.GetPixelBilinear(u, v).r, RaidZoneGrid.Sample(grid, size, u, v), 0.01f, $"at {u}, {v}");
            }
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    [Test]
    public void ZonePaint_FillsInsideTheRadiusOnly()
    {
        const int size = 32;
        var grid = new byte[size * size];

        RectInt touched = RaidZoneGrid.Paint(grid, size, new Vector2(0.5f, 0.5f), 0.25f, 1f, 1f, 1f);

        Assert.Greater(touched.width, 0);
        Assert.AreEqual(255, grid[16 * size + 16]);
        Assert.AreEqual(0, grid[0]);
        Assert.AreEqual(0, grid[16 * size + 2]);
    }

    [Test]
    public void ZonePaint_WeakDabsNeverStallAndEraseGoesBackToZero()
    {
        const int size = 16;
        var grid = new byte[size * size];
        int centre = 8 * size + 8;

        for (int i = 0; i < 600; i++)
        {
            RaidZoneGrid.Paint(grid, size, new Vector2(0.5f, 0.5f), 0.2f, 0.002f, 1f, 0.5f);
        }

        Assert.AreEqual(255, grid[centre]);

        for (int i = 0; i < 600; i++)
        {
            RaidZoneGrid.Paint(grid, size, new Vector2(0.5f, 0.5f), 0.2f, 0.3f, 0f, 0.5f);
        }

        Assert.AreEqual(0, grid[centre]);
    }

    [Test]
    public void ZoneResample_KeepsFlatAndHalfPaint()
    {
        const int from = 16;
        var grid = new byte[from * from];
        for (int y = 0; y < from; y++)
        {
            for (int x = from / 2; x < from; x++)
            {
                grid[y * from + x] = 200;
            }
        }

        byte[] bigger = RaidZoneGrid.Resample(grid, from, 64);

        Assert.AreEqual(64 * 64, bigger.Length);
        Assert.AreEqual(0, bigger[10 * 64 + 2]);
        Assert.AreEqual(200, bigger[10 * 64 + 61]);
    }

    [Test]
    public void ZoneScatter_IsRepeatableAndCapped()
    {
        List<Vector2> first = RaidZoneGrid.Scatter(_ => 1f, 100f, 5f, 1000, 42);
        List<Vector2> again = RaidZoneGrid.Scatter(_ => 1f, 100f, 5f, 1000, 42);
        List<Vector2> capped = RaidZoneGrid.Scatter(_ => 1f, 100f, 5f, 30, 42);

        Assert.AreEqual(400, first.Count, "full strength keeps one point per cell");
        CollectionAssert.AreEqual(first, again);
        Assert.AreEqual(30, capped.Count);
        CollectionAssert.IsSubsetOf(capped, first);
    }

    [Test]
    public void ZoneScatter_KeepsOnlyPaintedAndAcceptedPoints()
    {
        List<Vector2> none = RaidZoneGrid.Scatter(_ => 0f, 100f, 5f, 1000, 1);
        List<Vector2> east = RaidZoneGrid.Scatter(spot => spot.x > 0.5f ? 1f : 0f, 100f, 5f, 1000, 1);
        List<Vector2> south = RaidZoneGrid.Scatter(_ => 1f, 100f, 5f, 1000, 1, spot => spot.y < 0.5f);

        Assert.IsEmpty(none);
        Assert.IsTrue(east.TrueForAll(spot => spot.x > 0.5f));
        Assert.AreEqual(200, east.Count);
        Assert.IsTrue(south.TrueForAll(spot => spot.y < 0.5f));
    }

    [Test]
    public void ZoneSeed_IsStableAndDiffersByName()
    {
        Assert.AreEqual(RaidZoneGrid.SeedFor(7, "Ore"), RaidZoneGrid.SeedFor(7, "Ore"));
        Assert.AreNotEqual(RaidZoneGrid.SeedFor(7, "Ore"), RaidZoneGrid.SeedFor(7, "Herbs"));
        Assert.AreNotEqual(RaidZoneGrid.SeedFor(7, "Ore"), RaidZoneGrid.SeedFor(8, "Ore"));
    }

    [Test]
    public void Definition_EnsureZonesAddsTheTerrainZonesOnceAndSizesThem()
    {
        var definition = ScriptableObject.CreateInstance<RaidMapDefinition>();
        try
        {
            definition.SetZoneResolution(64);
            definition.Zones.Add(new RaidMapZone { name = "Ore", role = RaidZoneRole.Spawn });

            definition.EnsureZones();
            definition.EnsureZones();

            Assert.AreEqual(4, definition.Zones.Count);
            Assert.IsTrue(definition.Zones.TrueForAll(zone => zone.Resolution == 64 && zone.Weights.Length == 64 * 64));
            Assert.AreSame(definition.Zones[0], definition.SpawnZone("Ore"));
            Assert.IsNull(definition.SpawnZone("Path"));
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void Definition_StrengthTakesTheStrongestZoneOfARole()
    {
        var definition = ScriptableObject.CreateInstance<RaidMapDefinition>();
        try
        {
            definition.SetZoneResolution(16);
            definition.EnsureZones();
            var extraPath = new RaidMapZone { name = "Old Road", role = RaidZoneRole.Path };
            extraPath.EnsureResolution(16);
            definition.Zones.Add(extraPath);
            for (int i = 0; i < extraPath.Weights.Length; i++) extraPath.Weights[i] = 255;

            Assert.AreEqual(1f, definition.Strength(RaidZoneRole.Path, 0.3f, 0.3f), 1e-4f);
            Assert.AreEqual(0f, definition.Strength(RaidZoneRole.Forest, 0.3f, 0.3f), 1e-4f);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void Definition_SitesAreSortedAndSkipNone()
    {
        var definition = ScriptableObject.CreateInstance<RaidMapDefinition>();
        try
        {
            definition.Markers.Add(new RaidMapMarker { kind = RaidSiteKind.EnemyCamp, position = new Vector2(0.2f, 0.9f) });
            definition.Markers.Add(new RaidMapMarker { kind = RaidSiteKind.None, position = new Vector2(0.5f, 0.5f) });
            definition.Markers.Add(new RaidMapMarker { kind = RaidSiteKind.EnemyCamp, position = new Vector2(0.8f, 0.1f) });

            List<RaidSite> sites = definition.Sites();

            Assert.AreEqual(2, sites.Count);
            Assert.AreEqual(new Vector2(0.8f, 0.1f), sites[0].Position);
            Assert.AreEqual(new Vector2(0.2f, 0.9f), sites[1].Position);
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }
}
