using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WaterTests
{
    private readonly List<Object> created = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        TestCleanup.Destroy(created);
        WaterRipples.Release();
        WaterInteractor.UpdateAll(0f);
    }

    private GameObject Water(Vector3 position, Vector3 scale, float yaw = 0f)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        created.Add(go);
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = scale;
        go.AddComponent<WaterSurface>();
        return go;
    }

    private static float[] ReadHeights()
    {
        RenderTexture texture = WaterRipples.Texture;
        var read = new Texture2D(texture.width, texture.height, TextureFormat.RGFloat, false, true);
        RenderTexture active = RenderTexture.active;
        RenderTexture.active = texture;
        read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        RenderTexture.active = active;
        float[] heights = read.GetPixels().Select(c => c.r).ToArray();
        Object.DestroyImmediate(read);
        return heights;
    }

    [TestCase("Safe Haven/Water")]
    [TestCase("Safe Haven/Water Spray")]
    [TestCase("Hidden/Safe Haven/Water Ripples")]
    public void Shader_CompilesWithoutMessages(string name)
    {
        Shader shader = Shader.Find(name);
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        Assert.AreEqual(0, ShaderUtil.GetShaderMessageCount(shader));
    }

    [Test]
    public void Ripples_SpreadOutwardsAndDieDown()
    {
        int n = WaterRipples.Resolution, centre = n / 2;
        WaterRipples.AddStamp(Vector3.zero, 0.5f, 0.5f);
        WaterRipples.Run(1, Vector3.zero);
        float[] first = ReadHeights();
        Assert.Less(first.Min(), -0.3f, "the stamp pushes the water down");

        WaterRipples.Run(40, Vector3.zero);
        float[] later = ReadHeights();
        int ringTexel = centre * n + centre + 10; // about 0.9 m out, where nothing was stamped
        Assert.AreEqual(0f, first[ringTexel], 1e-4f);
        Assert.Greater(Mathf.Abs(later[ringTexel]) + Mathf.Abs(later[ringTexel + 2]) + Mathf.Abs(later[ringTexel - 2]), 1e-3f,
                       "the wave reached the ring");

        for (int i = 0; i < 20; i++)
        {
            WaterRipples.Run(30, Vector3.zero);
        }

        Assert.Less(ReadHeights().Max(Mathf.Abs), 0.05f, "the waves fade");
    }

    [Test]
    public void Ripples_FollowTheCentreOnTheTexelGrid()
    {
        WaterRipples.Run(1, new Vector3(10f, 0f, -4f));
        float texel = WaterRipples.Size / WaterRipples.Resolution;
        Vector2 origin = WaterRipples.Origin;
        Assert.AreEqual(10f - WaterRipples.Size / 2f, origin.x, texel);
        Assert.AreEqual(0f, Mathf.Repeat(origin.x / texel + 0.5f, 1f) - 0.5f, 1e-3f);
        Assert.AreEqual(1f, Shader.GetGlobalFloat("_WaterRippleOn"));
    }

    [Test]
    public void Surface_FindsItsTopInItsOwnSpace()
    {
        Water(new Vector3(5f, 1f, 0f), new Vector3(10f, 0.2f, 2f), 30f);
        Vector3 along = Quaternion.Euler(0f, 30f, 0f) * Vector3.right;
        Assert.IsTrue(WaterSurface.TryFind(new Vector3(5f, 0.5f, 0f) + along * 4f, 0.3f, out float height));
        Assert.AreEqual(1.1f, height, 1e-4f);
        Assert.IsFalse(WaterSurface.TryFind(new Vector3(5f, 0.5f, 0f) + Vector3.forward * 4f, 0.3f, out _), "beside the rotated river");
        Assert.IsFalse(WaterSurface.TryFind(new Vector3(5f, 2f, 0f), 0.3f, out _), "too high above it");
    }

    [Test]
    public void Waterfall_FallsFromTheLipToThePoolWithFadedEdges()
    {
        var mesh = new Mesh();
        created.Add(mesh);
        Waterfall.Build(mesh, 3f, 4f, 1f, 1.2f, 0.15f, 0.3f);
        Vector3[] vertices = mesh.vertices;
        Assert.AreEqual(4f, vertices.Max(v => v.y), 1e-4f);
        Assert.AreEqual(0f, vertices.Min(v => v.y), 1e-3f);
        Assert.AreEqual(-1f, vertices.Min(v => v.z), 1e-4f);
        Assert.AreEqual(1.2f * Mathf.Sqrt(8f / 9.81f), vertices.Max(v => v.z), 1e-3f, "lands where the arc ends");
        Color[] colours = mesh.colors;
        Vector2[] uv = mesh.uv;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (uv[i].x < 1e-4f || uv[i].x > 3f - 1e-4f)
            {
                Assert.AreEqual(0f, colours[i].a, 1e-4f, "the sides fade out");
            }
        }

        Assert.Greater(uv.Max(t => t.y), 4f, "v runs down the sheet in metres");
        Vector3 lipNormal = mesh.normals[vertices.ToList().FindIndex(v => v.z < -0.9f)];
        Assert.Greater(lipNormal.y, 0.9f, "the lip faces up");
    }

    [Test]
    public void Interactors_ChurnFoamOnlyInTheWater()
    {
        Water(Vector3.zero, new Vector3(10f, 0.1f, 10f));
        var inside = new GameObject("plunge");
        var outside = new GameObject("dry");
        created.Add(inside);
        created.Add(outside);
        inside.transform.position = new Vector3(1f, 0f, 1f);
        outside.transform.position = new Vector3(20f, 0f, 0f);
        WaterInteractor plunge = inside.AddComponent<WaterInteractor>();
        plunge.Mode = WaterInteractor.Kind.Constant;
        plunge.Foam = 1f;
        WaterInteractor dry = outside.AddComponent<WaterInteractor>();
        dry.Mode = WaterInteractor.Kind.Constant;
        dry.Foam = 1f;

        Assert.AreEqual(1, WaterInteractor.UpdateAll(0f));
        Assert.IsTrue(plunge.InWater);
        Assert.IsFalse(dry.InWater);
        Assert.AreEqual(1f, Shader.GetGlobalFloat("_WaterFoamSpotCount"));
    }

    [Test]
    public void Interactors_DroppingInMakeRipples()
    {
        Water(Vector3.zero, new Vector3(10f, 0.1f, 10f));
        var stone = new GameObject("stone");
        created.Add(stone);
        stone.transform.position = new Vector3(0f, 1.5f, 0f);
        WaterInteractor interactor = stone.AddComponent<WaterInteractor>();
        interactor.Radius = 0.2f;
        WaterInteractor.UpdateAll(0.001f); // remember where it was
        stone.transform.position = new Vector3(0f, 0.1f, 0f); // fell 1.4 m in one 1/30 s frame
        WaterInteractor.UpdateAll(1f / 30f);
        Assert.IsTrue(interactor.InWater);
        Assert.IsNotNull(WaterRipples.Texture, "the ripple simulation ran");
        Assert.Less(ReadHeights().Min(), -0.3f, "the drop pushed the water down");
    }

    [Test]
    public void Setup_CreatesMaterialsAndPrefabs()
    {
        WaterSetup.Set set = WaterSetup.EnsureAssets();
        foreach (Material material in new[] { set.Still, set.River, set.Fall })
        {
            Assert.AreEqual(WaterSetup.ShaderName, material.shader.name);
            Assert.IsNotNull(material.GetTexture("_NormalMap"));
            Assert.IsNotNull(material.GetTexture("_FoamMap"));
        }

        Assert.Greater(set.River.GetFloat("_FlowSpeed"), 0f);
        Assert.AreEqual(1f, set.Fall.GetFloat("_VertexAlpha"));
        Assert.AreEqual(1f, set.Fall.GetFloat("_SteepFaces"));
        Assert.AreEqual(0f, set.River.GetFloat("_SteepFaces"), "river boxes hide their sides");
        var normal = (TextureImporter)AssetImporter.GetAtPath(WaterSetup.Folder + "/Water Normal.png");
        Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
        Assert.AreEqual(WaterInteractor.Kind.Constant, set.Plunge.GetComponent<WaterInteractor>().Mode);
        Assert.AreEqual(2, set.Plunge.GetComponentsInChildren<ParticleSystem>().Length);
        Assert.IsNotNull(set.Splash.GetComponent<ParticleSystem>());
    }

    [Test]
    public void Carve_DigsABedUnderTheWaterAndKeepsTheGroundElsewhere()
    {
        var data = new TerrainData { heightmapResolution = 65, size = new Vector3(32f, 10f, 32f) };
        created.Add(data);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        created.Add(terrainObject);
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        GameObject water = Water(new Vector3(16f, 0.02f, 16f), new Vector3(12f, 0.04f, 4f));
        var grass = new TerrainLayer { diffuseTexture = Texture2D.whiteTexture };
        var gravel = new TerrainLayer { diffuseTexture = Texture2D.grayTexture };
        created.Add(grass);
        created.Add(gravel);
        data.alphamapResolution = 32;
        data.terrainLayers = new[] { grass, gravel };

        Assert.Greater(WaterSetup.Carve(terrain, water.transform, 1.2f, 1f, gravel), 0);
        float worldY = terrain.transform.position.y;
        Assert.Less(worldY, 0f, "the terrain was lowered to make room for the bed");
        Assert.AreEqual(0.04f - 1.2f, terrain.SampleHeight(new Vector3(16f, 0f, 16f)) + worldY, 0.05f, "bed in the middle");
        Assert.AreEqual(0f, terrain.SampleHeight(new Vector3(4f, 0f, 4f)) + worldY, 0.01f, "ground outside unchanged");
        float[,,] maps = data.GetAlphamaps(0, 0, 32, 32);
        Assert.Greater(maps[16, 16, 1], 0.9f, "the bed is painted with the bed layer");
        Assert.AreEqual(1f, maps[16, 16, 0] + maps[16, 16, 1], 1e-3f);
        Assert.AreEqual(1f, maps[4, 4, 0], 1e-3f, "outside keeps its layer");
    }

    // Docs/LookAndFeelResearch_2026-11-18.md W3: a river runs faster where it narrows or falls, slower at its banks, faster round the
    // outside of a bend; the strip's flow (uv2) carries it, which the shader whitens, streaks and clouds.
    [Test]
    public void ARiverRunsFasterWhereItNarrowsOrFallsAndRoundTheOutsideOfABend()
    {
        var line = new List<Vector2>();
        for (int i = 0; i < 21; i++)
        {
            line.Add(new Vector2(0f, i * 2f));
        }

        // Narrow in the middle (1 m half width against 3), falling over the last stretch (10 cm a metre).
        float[] along = RaidMapGenerator.FlowAlong(line, i => i >= 8 && i <= 12 ? 1f : 3f, i => i >= 16 ? 10f - (i - 16) * 0.2f : 10f);
        Assert.Greater(along[10], along[3] * 1.4f, "faster through the neck");
        Assert.Greater(along[18], along[3] * 1.4f, "faster where it falls");
        Assert.AreEqual(1f, along[3], 0.05f, "its everyday speed where it is its usual width on the level");
        Assert.Less(Mathf.Abs(along[9] - along[8]), 0.25f, "eased: never a jump");

        Assert.AreEqual(0.55f, RaidMapGenerator.FlowAcross(1f, 0f), 1e-4f, "slower at the bank");
        Assert.AreEqual(1f, RaidMapGenerator.FlowAcross(0f, 0.5f), 1e-4f, "the middle runs at its speed whatever the bend");
        Assert.Greater(RaidMapGenerator.FlowAcross(0.5f, 0.3f), RaidMapGenerator.FlowAcross(-0.5f, 0.3f), "turning left, the right (outside) runs faster");

        var bend = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 2f), new Vector2(0f, 4f), new Vector2(-1f, 5.7f), new Vector2(-2.5f, 7f), new Vector2(-4.5f, 7.6f) };
        Assert.Greater(RaidMapGenerator.Bends(bend)[3], 0f, "a left turn bends above zero");

        Mesh strip = RaidMapGenerator.WaterStrip(line, i => 3f, i => 10f, i => 3f, 1f, out _);
        created.Add(strip);
        float middle = strip.uv2[10 * 5 + 2].magnitude, bank = strip.uv2[10 * 5 + 0].magnitude;
        Assert.Greater(middle, bank * 1.5f, "the strip carries it: the middle runs faster than the banks");
    }

    // W2, W5: the raids' waters carry their own life: streaks on the rivers, duckweed and bubbles in the marsh, a few leaves in the woods.
    [Test]
    public void EachRaidsWaterCarriesItsOwnLife()
    {
        Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Generated/Water/" + name + ".mat");
        Assert.Greater(Load("Water River (Valley)").GetFloat("_FoamStreaks"), 0.3f, "the Valley's river streaks");
        Assert.Greater(Load("Water River (World)").GetFloat("_FoamStreaks"), 0.3f);
        Assert.Greater(Load("Water Fen").GetFloat("_Floating"), 0.4f, "duckweed on the fen");
        Assert.Greater(Load("Water Fen").GetFloat("_Bubbles"), 0f, "and gas rising");
        Assert.Greater(Load("Water Still (Woods)").GetFloat("_FloatingLong"), 1.05f, "leaves in the woods' ponds, pointed");
        Assert.AreEqual(0f, Load("Water Still").GetFloat("_Floating"), "the Haven's and the Valley's ponds stay clear");
        Assert.IsFalse(ShaderUtil.ShaderHasError(Load("Water Fen").shader));
        Object Layout(string name) => AssetDatabase.LoadMainAssetAtPath("Assets/_Project/Data/Raids/Maps/" + name + " Layout.asset");
        Assert.AreEqual(Load("Water Still (Marsh)"), new SerializedObject(Layout("MossMarsh")).FindProperty("pondMaterial").objectReferenceValue);
        Assert.AreEqual(Load("Water Still (Woods)"), new SerializedObject(Layout("MistyWoods")).FindProperty("pondMaterial").objectReferenceValue);
        Assert.AreEqual(Load("Water Still"), new SerializedObject(Layout("TrollValley")).FindProperty("pondMaterial").objectReferenceValue);
    }
}
