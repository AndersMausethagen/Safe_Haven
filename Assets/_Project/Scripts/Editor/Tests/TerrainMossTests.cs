using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class TerrainMossTests
{
    private readonly List<Object> created = new List<Object>();
    private TerrainData data;
    private TerrainMoss moss;
    private TerrainLayer layer;

    [SetUp]
    public void SetUp()
    {
        data = Track(new TerrainData { heightmapResolution = 33, alphamapResolution = 16, size = new Vector3(8f, 2f, 8f) });
        layer = Track(new TerrainLayer());
        data.terrainLayers = new[] { Track(new TerrainLayer()), layer };
        Paint(1f);
        GameObject terrain = Track(Terrain.CreateTerrainGameObject(data));
        moss = terrain.AddComponent<TerrainMoss>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = created.Count - 1; i >= 0; i--)
        {
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        }
        created.Clear();
    }

    [Test]
    public void Bloom_PaintsASoftCircleAndKeepsWeightsNormalised()
    {
        const int size = 20;
        var alphas = new float[size, size, 3];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                alphas[y, x, 0] = 0.6f;
                alphas[y, x, 1] = 0.1f;
                alphas[y, x, 2] = 0.3f;
            }
        }

        TerrainMossBloom.PaintCircle(alphas, 1, new Vector2(10f, 10f), 6f, 2f, 1f);

        Assert.AreEqual(1f, alphas[10, 10, 1], 1e-4f, "The centre is fully moss.");
        Assert.AreEqual(0.1f, alphas[0, 0, 1], 1e-6f, "Outside the circle is untouched.");
        float edge = alphas[10, 15, 1];
        Assert.That(edge, Is.GreaterThan(0.1f).And.LessThan(1f), "The edge fades.");
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Assert.AreEqual(1f, alphas[y, x, 0] + alphas[y, x, 1] + alphas[y, x, 2], 1e-4f);
            }
        }

        TerrainMossBloom.PaintCircle(alphas, 1, new Vector2(10f, 10f), 6f, 2f, 0.5f);
        Assert.AreEqual(1f, alphas[10, 10, 1], 1e-4f, "A weaker repaint never removes moss.");
    }

    [Test]
    public void MaskSampling_UsesTerrainZYXLayoutAndBilinearInterpolation()
    {
        var weights = new float[2, 2, 2];
        weights[0, 0, 1] = 0f;
        weights[0, 1, 1] = 1f;
        weights[1, 0, 1] = 0.5f;
        weights[1, 1, 1] = 0.5f;
        Assert.AreEqual(1f, TerrainMoss.SampleWeight(weights, new Vector2(1f, 0f), 1));
        Assert.AreEqual(0.5f, TerrainMoss.SampleWeight(weights, new Vector2(0.5f, 0.5f), 1), 0.0001f);
        Assert.AreEqual(0f, TerrainMoss.SampleWeight(weights, Vector2.one, 0));
    }

    [Test]
    public void UnpaintedLayer_ProducesNoTufts()
    {
        Paint(0f);
        Configure();
        Assert.AreEqual(0, moss.TuftCount);
    }

    [Test]
    public void PaintedLayer_ProducesDeterministicTuftsAndKeepsRootsAnchored()
    {
        Configure();
        Assert.Greater(moss.TuftCount, 0);
        Mesh mesh = moss.transform.Find("Moss (generated)/Moss tufts").GetComponent<MeshFilter>().sharedMesh;
        Vector3[] before = mesh.vertices;
        Color[] colors = mesh.colors;
        Assert.AreEqual(0f, colors[0].a);
        Assert.AreEqual(1f, colors[4].a);
        Assert.Greater(before[4].y, before[0].y);
        Assert.IsTrue(moss.Rebuild());
        CollectionAssert.AreEqual(before, moss.transform.Find("Moss (generated)/Moss tufts").GetComponent<MeshFilter>().sharedMesh.vertices);
        Assert.AreEqual(1, moss.transform.childCount, "Rebuild must remove the old generated render objects.");
    }

    [Test]
    public void Tufts_RespectMaximumCount()
    {
        var serialized = new SerializedObject(moss);
        serialized.FindProperty("maximumTufts").intValue = 7;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Configure();
        Assert.AreEqual(7, moss.TuftCount);
    }

    [Test]
    public void TerrainHoles_ProduceNoTufts()
    {
        data.SetHoles(0, 0, new bool[data.holesResolution, data.holesResolution]);
        Configure();
        Assert.AreEqual(0, moss.TuftCount);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    public void PaintedLayer_BindsCorrectAlphamapAndChannel(int layerIndex)
    {
        var layers = new TerrainLayer[8];
        for (int i = 0; i < layers.Length; i++) layers[i] = i == layerIndex ? layer : Track(new TerrainLayer());
        data.terrainLayers = layers;
        var weights = new float[16, 16, 8];
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
                weights[z, x, layerIndex] = 1f;
        data.SetAlphamaps(0, 0, weights);
        Configure();
        Material material = moss.transform.Find("Moss (generated)/Moss surface").GetComponent<MeshRenderer>().sharedMaterial;
        Assert.AreEqual(data.alphamapTextures[layerIndex / 4], material.GetTexture("_Control"));
        Vector4 channel = Vector4.zero;
        channel[layerIndex % 4] = 1f;
        Assert.AreEqual(channel, material.GetVector("_MaskChannel"));
        Assert.Greater(moss.TuftCount, 0);
    }

    [Test]
    public void PaintedAndBareGround_DistributesTuftsOnlyOnPaintedSide()
    {
        var weights = new float[16, 16, 2];
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                weights[z, x, 0] = x < 8 ? 0f : 1f;
                weights[z, x, 1] = x < 8 ? 1f : 0f;
            }
        data.SetAlphamaps(0, 0, weights);
        Configure();
        Assert.Greater(moss.TuftCount, 0);
        Vector2[] roots = moss.transform.Find("Moss (generated)/Moss tufts").GetComponent<MeshFilter>().sharedMesh.uv2;
        foreach (Vector2 root in roots) Assert.Less(root.x, 0.6f);
    }

    [Test]
    public void LeafyTufts_StayDeterministicCappedAndFinite()
    {
        var serialized = new SerializedObject(moss);
        serialized.FindProperty("leafyTufts").boolValue = true;
        serialized.FindProperty("maximumTufts").intValue = 7;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Configure();
        Mesh mesh = moss.transform.Find("Moss (generated)/Moss tufts").GetComponent<MeshFilter>().sharedMesh;
        Assert.AreEqual(7, moss.TuftCount);
        Assert.AreEqual(7 * 200, mesh.vertexCount);
        Assert.AreEqual(7 * 300, mesh.triangles.Length);
        Vector3[] before = mesh.vertices;
        foreach (Vector3 vertex in before)
        {
            Assert.IsFalse(float.IsNaN(vertex.x) || float.IsInfinity(vertex.x));
            Assert.IsFalse(float.IsNaN(vertex.y) || float.IsInfinity(vertex.y));
            Assert.IsFalse(float.IsNaN(vertex.z) || float.IsInfinity(vertex.z));
            Assert.IsTrue(mesh.bounds.Contains(vertex));
        }
        foreach (int index in mesh.triangles) Assert.That(index, Is.InRange(0, mesh.vertexCount - 1));
        foreach (Color color in mesh.colors) Assert.That(color.a, Is.InRange(0f, 1f));
        Assert.IsTrue(moss.Rebuild());
        CollectionAssert.AreEqual(before, moss.transform.Find("Moss (generated)/Moss tufts").GetComponent<MeshFilter>().sharedMesh.vertices);
    }

    [Test]
    public void OptionalMaps_BindToBothMaterialsAndDisableCleanlyWhenRemoved()
    {
        Texture2D texture = Track(new Texture2D(2, 2));
        var serialized = new SerializedObject(moss);
        foreach (string property in new[] { "baseColorMap", "normalMap", "packedMap" })
            serialized.FindProperty(property).objectReferenceValue = texture;
        serialized.FindProperty("normalStrength").floatValue = 0.7f;
        serialized.FindProperty("textureInfluence").floatValue = 0.8f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Configure();
        foreach (MeshRenderer renderer in moss.GetComponentsInChildren<MeshRenderer>())
        {
            Material material = renderer.sharedMaterial;
            Assert.AreEqual(texture, material.GetTexture("_BaseMap"));
            Assert.AreEqual(texture, material.GetTexture("_BumpMap"));
            Assert.AreEqual(texture, material.GetTexture("_PackedMap"));
            Assert.AreEqual(0.8f, material.GetFloat("_UseTextures"), 0.0001f);
            Assert.AreEqual(0.7f, material.GetFloat("_UseNormal"), 0.0001f);
            Assert.AreEqual(1f, material.GetFloat("_UsePacked"));
        }
        serialized.Update();
        foreach (string property in new[] { "baseColorMap", "normalMap", "packedMap" })
            serialized.FindProperty(property).objectReferenceValue = null;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Assert.IsTrue(moss.Rebuild());
        foreach (MeshRenderer renderer in moss.GetComponentsInChildren<MeshRenderer>())
            foreach (string flag in new[] { "_UseTextures", "_UseNormal", "_UsePacked" })
                Assert.AreEqual(0f, renderer.sharedMaterial.GetFloat(flag));
    }

    [Test]
    public void SavedPreview_ContainsMossAndBarePath()
    {
        TerrainData preview = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Art/Moss/MossPreviewTerrain.asset");
        Assert.IsNotNull(preview, "Create the preview with Safe Haven > Art Pipeline > Open Moss Preview.");
        Assert.AreEqual(1f, preview.GetAlphamaps(10, 32, 1, 1)[0, 0, 1], 0.01f);
        Assert.AreEqual(0f, preview.GetAlphamaps(27, 32, 1, 1)[0, 0, 1], 0.01f);
    }

    [UnityTest]
    public IEnumerator ReenablingMoss_RebuildsWithoutManualInteraction()
    {
        Configure();
        moss.enabled = false;
        Assert.AreEqual(0, moss.TuftCount);
        moss.enabled = true;
        yield return null;
        yield return null;
        Assert.Greater(moss.TuftCount, 0);
        Assert.IsNotNull(moss.transform.Find("Moss (generated)/Moss tufts"));
    }

    [Test]
    public void DisablingMoss_ReleasesRenderObjectsWithoutChangingTerrain()
    {
        Configure();
        moss.enabled = false;
        Assert.AreEqual(0, moss.transform.childCount);
        Assert.AreEqual(layer, data.terrainLayers[1]);
        Assert.AreEqual(1f, data.GetAlphamaps(0, 0, 1, 1)[0, 0, 1]);
    }

    [Test]
    public void MissingPaintLayer_ReportsConfigurationError()
    {
        data.terrainLayers = new[] { Track(new TerrainLayer()) };
        LogAssert.Expect(LogType.Warning, "Terrain Moss: choose a Moss Layer that is present in this terrain's Paint Texture layers.");
        Configure();
        Assert.IsNotEmpty(moss.LastError);
        Assert.AreEqual(0, moss.transform.childCount);
    }

    private void Configure()
    {
        Shader shader = Shader.Find("Safe Haven/Terrain Moss");
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        moss.Configure(layer, shader);
    }

    private void Paint(float weight)
    {
        var weights = new float[16, 16, 2];
        for (int z = 0; z < 16; z++)
            for (int x = 0; x < 16; x++)
            {
                weights[z, x, 0] = 1f - weight;
                weights[z, x, 1] = weight;
            }
        data.SetAlphamaps(0, 0, weights);
    }

    private T Track<T>(T value) where T : Object
    {
        created.Add(value);
        return value;
    }
}
