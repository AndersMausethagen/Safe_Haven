using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The Biome Brush on a small flat terrain made for the test: plants and ground where it paints, nothing away
/// from water for a bank biome, and erasing what it grew.</summary>
public sealed class BiomeBrushTests
{
    private const string Folder = BiomeBrush.Folder;
    private GameObject terrainObject;
    private TerrainData data;

    [SetUp]
    public void MakeTerrain()
    {
        data = new TerrainData { heightmapResolution = 65, alphamapResolution = 64 };
        data.size = new Vector3(64f, 20f, 64f);
        data.SetDetailResolution(64, 16);
        TerrainLayer start = AssetDatabase.LoadAssetAtPath<BiomePreset>(Folder + "/Misty Woods Floor.asset").ground;
        data.terrainLayers = new[] { start };
        terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.transform.position = new Vector3(5000f, 0f, 5000f); // clear of anything in the open scene
        Physics.SyncTransforms();
    }

    [TearDown]
    public void RemoveTerrain()
    {
        Object.DestroyImmediate(terrainObject);
        Object.DestroyImmediate(data);
    }

    private Terrain Terrain => terrainObject.GetComponent<Terrain>();
    private Vector3 Middle => terrainObject.transform.position + new Vector3(32f, 0f, 32f);

    private static BiomePreset Preset(string name) => AssetDatabase.LoadAssetAtPath<BiomePreset>($"{Folder}/{name}.asset");

    private int Plants(BiomePreset biome)
    {
        int total = 0;
        foreach (BiomePreset.Detail detail in biome.details)
        {
            int index = System.Array.FindIndex(data.detailPrototypes, p => p.prototype == detail.prefab);
            if (index < 0) continue;
            int[,] cells = data.GetDetailLayer(0, 0, data.detailResolution, data.detailResolution, index);
            foreach (int cell in cells) total += cell;
        }

        return total;
    }

    [Test]
    public void TheMeadow_GrowsItsPlantsAndLaysItsGround_UnderTheBrushOnly()
    {
        BiomePreset meadow = Preset("Haven Meadow");
        BiomeBrush.PaintPlants(Terrain, meadow, Middle, 8f, 1f, false, new System.Random(3));
        BiomeBrush.PaintGround(Terrain, meadow, Middle, 8f, 1f);

        Assert.Greater(Plants(meadow), 20, "short grass and the rest grow");
        int grass = System.Array.FindIndex(data.detailPrototypes, p => p.prototype == meadow.details[0].prefab);
        Assert.AreEqual(0, data.GetDetailLayer(0, 0, 8, 8, grass).Cast<int>().Sum(), "nothing far outside the brush");

        int layer = System.Array.IndexOf(data.terrainLayers, meadow.ground);
        Assert.GreaterOrEqual(layer, 0, "its ground layer was added");
        float[,,] maps = data.GetAlphamaps(32, 32, 1, 1);
        Assert.Greater(maps[0, 0, layer], 0.3f, "its ground is laid in the middle");
        float sum = 0f;
        for (int l = 0; l < data.alphamapLayers; l++) sum += maps[0, 0, l];
        Assert.AreEqual(1f, sum, 0.01f, "the weights still add up");
    }

    [Test]
    public void ABankBiome_PaintsNothingAwayFromWater()
    {
        BiomePreset bank = Preset("Riverbank");
        Assert.Greater(bank.nearWater, 0f);
        BiomeBrush.PaintPlants(Terrain, bank, Middle, 8f, 1f, false, new System.Random(3));
        Assert.AreEqual(0, Plants(bank));
    }

    [Test]
    public void Erasing_ClearsTheBiomesPlants()
    {
        BiomePreset floor = Preset("Misty Woods Floor");
        BiomeBrush.PaintPlants(Terrain, floor, Middle, 8f, 1f, false, new System.Random(3));
        Assert.Greater(Plants(floor), 0);
        BiomeBrush.PaintPlants(Terrain, floor, Middle, 10f, 0f, true, new System.Random(3));
        Assert.AreEqual(0, Plants(floor));
    }
}
