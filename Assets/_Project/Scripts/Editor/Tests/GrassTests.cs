using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GrassTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        TestCleanup.Destroy(created);
        AssetDatabase.DeleteAsset(GrassSetup.Folder + "/__GrassTest_GrassColour.png");
    }

    [Test]
    public void Clump_BendsFromTheRootAndStaysWithinItsSize()
    {
        Mesh mesh = GrassSetup.BuildClump(10, 0.15f, 0.3f, 1);
        created.Add(mesh);
        Color[] colours = mesh.colors;
        Vector3[] positions = mesh.vertices;
        Assert.AreEqual(10 * 5 * 2, positions.Length); // 4 segments = 5 rows of 2 per blade
        for (int i = 0; i < positions.Length; i++)
        {
            if (positions[i].y < 1e-4f) Assert.AreEqual(0f, colours[i].r, 1e-4f, "roots do not move");
        }

        Assert.AreEqual(1f, colours.Max(c => c.r), 1e-4f);
        Assert.LessOrEqual(mesh.bounds.max.y, 0.3f * 1.16f);
    }

    [Test]
    public void Bush_IsMadeOfLeafCards()
    {
        Mesh mesh = GrassSetup.BuildBush(12, 0.4f, 0.5f, 2);
        created.Add(mesh);
        Assert.AreEqual(12 * 4, mesh.vertexCount);
        Assert.AreEqual(12 * 6, mesh.triangles.Length);
        Assert.IsTrue(mesh.colors.All(c => c.r >= 0f && c.r <= 0.8f + 1e-4f));
    }

    [Test]
    public void GlowPlants_MarkTheirGlowingPartsInVertexColourB()
    {
        Mesh mushrooms = GrassSetup.BuildMushrooms(4, 0.15f, 0.14f, 3);
        Mesh flowers = GrassSetup.BuildFlowers(4, 0.18f, 0.3f, 3);
        created.Add(mushrooms);
        created.Add(flowers);
        foreach (Mesh mesh in new[] { mushrooms, flowers })
        {
            Assert.Greater(mesh.vertexCount, 0);
            Assert.AreEqual(1f, mesh.colors.Max(c => c.b), 1e-4f, "caps and petals glow");
            Color[] colours = mesh.colors;
            Vector3[] positions = mesh.vertices;
            float glowing = Enumerable.Range(0, colours.Length).Where(i => colours[i].b > 0.9f).Average(i => positions[i].y);
            float plain = Enumerable.Range(0, colours.Length).Where(i => colours[i].b < 0.2f).Average(i => positions[i].y);
            Assert.Greater(glowing, plain, "the glow sits on top (caps, petals), not on the stems");
            Assert.AreEqual(0f, mesh.bounds.min.y, 0.03f, "they stand on the ground");
        }

        Assert.IsTrue(flowers.colors.Any(c => c.b == 0f), "stalks do not glow");
    }

    [Test]
    public void Interactors_AreSentToTheShaderUpToTheLimit()
    {
        var all = new List<GameObject>();
        for (int i = 0; i < GrassInteractor.Max + 2; i++)
        {
            var go = new GameObject("interactor " + i);
            created.Add(go);
            all.Add(go);
            go.AddComponent<GrassInteractor>();
        }

        Assert.AreEqual(GrassInteractor.Max, GrassInteractor.Upload());
        foreach (GameObject go in all.Skip(3)) Object.DestroyImmediate(go);
        Assert.AreEqual(3, GrassInteractor.Upload());
        Assert.AreEqual(3f, Shader.GetGlobalFloat("_GrassInteractorCount"));
    }

    [Test]
    public void Setup_AddsInstancedPrototypesOnceAndBakesTheGroundColour()
    {
        var data = new TerrainData { name = "__GrassTest", alphamapResolution = 16, size = new Vector3(20, 10, 20) };
        created.Add(data);
        var red = new Texture2D(4, 4); red.SetPixels(Enumerable.Repeat(Color.red, 16).ToArray()); red.Apply();
        var blue = new Texture2D(4, 4); blue.SetPixels(Enumerable.Repeat(Color.blue, 16).ToArray()); blue.Apply();
        var redLayer = new TerrainLayer { diffuseTexture = red };
        var blueLayer = new TerrainLayer { diffuseTexture = blue };
        created.AddRange(new Object[] { red, blue, redLayer, blueLayer });
        data.terrainLayers = new[] { redLayer, blueLayer };
        var weights = new float[16, 16, 2];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                weights[y, x, 0] = x < 8 ? 1f : 0f;
                weights[y, x, 1] = x < 8 ? 0f : 1f;
            }

        data.SetAlphamaps(0, 0, weights);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        created.Add(terrainObject);
        Terrain terrain = terrainObject.GetComponent<Terrain>();

        List<GameObject> prefabs = GrassSetup.EnsureAssets();
        Assert.AreEqual(GrassSetup.KindCount, GrassSetup.AddTo(terrain, prefabs));
        Assert.AreEqual(0, GrassSetup.AddTo(terrain, prefabs));
        Assert.IsTrue(data.detailPrototypes.All(p => p.useInstancing && p.usePrototypeMesh));
        Assert.Greater(data.detailResolution, 0, "the terrain can now be painted with details");

        Texture2D map = GrassSetup.BakeGroundColour(terrain);
        Assert.IsNotNull(map);
        var readable = new Texture2D(2, 2);
        created.Add(readable);
        readable.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(map)));
        Color left = readable.GetPixel(2, 8), right = readable.GetPixel(13, 8);
        Assert.Greater(left.r, 0.9f);
        Assert.Greater(right.b, 0.9f);
        var colour = terrain.GetComponent<GrassTerrainColour>();
        Assert.AreSame(map, colour.ColourMap);
        Assert.AreSame(colour, GrassTerrainColour.Current);
        Assert.AreEqual(1f, Shader.GetGlobalFloat("_GrassTerrainColorOn"));
    }

    [Test]
    public void Fill_GrowsByGroundLayerButNotOnStoneOrUnderWater()
    {
        var data = new TerrainData { name = "__GrassTest", heightmapResolution = 33, alphamapResolution = 16, size = new Vector3(16, 10, 16) };
        created.Add(data);
        var green = new TerrainLayer { diffuseTexture = Texture2D.whiteTexture };
        var stone = new TerrainLayer { diffuseTexture = Texture2D.grayTexture };
        created.Add(green);
        created.Add(stone);
        data.terrainLayers = new[] { green, stone };
        var weights = new float[16, 16, 2];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                weights[y, x, 0] = x < 8 ? 1f : 0f; // left half grass, right half stone
                weights[y, x, 1] = x < 8 ? 0f : 1f;
            }

        data.SetAlphamaps(0, 0, weights);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        created.Add(terrainObject);
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube); // a pond over the grass's bottom quarter
        created.Add(water);
        water.transform.position = new Vector3(4f, 0.02f, 2f);
        water.transform.localScale = new Vector3(8f, 0.04f, 4f);
        water.AddComponent<WaterSurface>();

        var fills = new[]
        {
            new GrassSetup.GroundFill { layer = green, shortGrass = 1f },
            new GrassSetup.GroundFill { layer = stone }
        };
        Assert.Greater(GrassSetup.FillByGround(terrain, GrassSetup.EnsureAssets(), fills), 0);
        Assert.AreEqual(DetailScatterMode.InstanceCountMode, data.detailScatterMode, "a cell's value is its clump count");
        int layer = System.Array.FindIndex(data.detailPrototypes, p => p.prototype.name == "Short Grass");
        int n = data.detailResolution;
        int[,] cells = data.GetDetailLayer(0, 0, n, n, layer);
        int Sum(int x0, int x1, int z0, int z1)
        {
            int total = 0;
            for (int z = z0; z < z1; z++)
                for (int x = x0; x < x1; x++)
                    total += cells[z, x];
            return total;
        }

        Assert.Greater(Sum(n / 16, n * 7 / 16, n * 6 / 16, n * 15 / 16), 0, "grass on the grass layer");
        Assert.AreEqual(0, Sum(n * 9 / 16, n, 0, n), "no grass on stone");
        Assert.AreEqual(0, Sum(n / 16, n * 7 / 16, n / 32, n * 7 / 32), "no grass under water");

        var map = new Texture2D(2, 2);
        created.Add(map);
        map.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(terrain.GetComponent<GrassTerrainColour>().ColourMap)));
        Assert.Less(map.GetPixel(4, 1).a, 0.5f, "the colour map hides grass painted under water later");
        Assert.Greater(map.GetPixel(4, 12).a, 0.5f);
    }

    [Test]
    public void NordicPlants_FernsOpenUpAndShrubsCarryBerries()
    {
        Mesh fern = GrassSetup.BuildFern(8, 0.55f, 4);
        created.Add(fern);
        Assert.AreEqual(8 * 7 * 2, fern.vertexCount);
        Assert.Greater(fern.bounds.size.x, 0.6f, "fronds spread out, visible from above");
        Assert.Less(fern.bounds.max.y, 0.4f);
        Assert.AreEqual(0f, fern.colors.Min(c => c.r), 1e-4f, "roots do not sway");

        foreach (bool blue in new[] { true, false })
        {
            Texture2D leaves = GrassSetup.BerryShrubPixels(64, blue);
            created.Add(leaves);
            Color[] pixels = leaves.GetPixels();
            Assert.Greater(pixels.Count(p => p.a > 0.5f), 64 * 64 / 10, "a sprig of leaves");
            Assert.IsTrue(pixels.Any(p => p.a > 0.5f && (blue ? p.b > p.g && p.b > 0.3f : p.r > 0.5f && p.g < 0.3f)), "berries");
        }

        Texture2D frond = GrassSetup.FernPixels(32, 64);
        created.Add(frond);
        Assert.Greater(frond.GetPixel(16, 20).a, 0.5f, "the stem");
        Assert.AreEqual(0f, frond.GetPixel(0, 63).a, 1e-4f, "open around the tip");
    }

    [Test]
    public void GrassShader_CompilesWithoutMessages()
    {
        Shader shader = Shader.Find(GrassSetup.ShaderName);
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        Assert.AreEqual(0, ShaderUtil.GetShaderMessageCount(shader));
    }
}
