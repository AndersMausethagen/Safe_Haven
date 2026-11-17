using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>Raids keep the world's graphical standard: these fail when a generated map would lose what Haven has.</summary>
public sealed class RaidLookTests
{
    // The Misty Woods' map, for what only it is checked on; every raid's own map (each raid scene has one, 2026-11-17) for the rest.
    private static RaidMapDefinition Map => AssetDatabase.LoadAssetAtPath<RaidMapDefinition>("Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset");

    private static RaidMapDefinition[] Maps => AssetDatabase.FindAssets("t:RaidMapDefinition", new[] { "Assets/_Project/Data/Raids/Maps" })
        .Select(guid => AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();

    private static int Sum(int[,] cells, int fromColumn, int toColumn)
    {
        int sum = 0;
        for (int z = 0; z < cells.GetLength(0); z++)
        {
            for (int x = fromColumn; x < toColumn; x++)
            {
                sum += cells[z, x];
            }
        }

        return sum;
    }

    [Test]
    public void TheRaidTerrain_IsTessellated()
    {
        Assert.GreaterOrEqual(Maps.Length, 2, "the Misty Woods and Troll Valley");
        foreach (RaidMapDefinition map in Maps)
        {
            Material material = map.TerrainMaterial;
            Assert.IsTrue(material.HasProperty("_DisplacementStrength"), map.name);
            Assert.Greater(material.GetFloat("_DisplacementStrength"), 0f, map.name);
            Assert.AreEqual(map.Layers.Length, material.GetFloat("_DisplacementLayerCount"), $"{map.name}: every layer the map paints");
            Assert.IsNotNull(material.GetTexture("_DisplacementMasks"), map.name);
        }
    }

    [Test]
    public void PuttingAMapOnTheGround_LeavesSpawnPointsOnTheGround()
    {
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(100f, 30f, 100f) };
        var heights = new float[33, 33];
        for (int z = 0; z < 33; z++)
        {
            for (int x = 0; x < 33; x++)
            {
                heights[z, x] = 0.5f;
            }
        }

        data.SetHeights(0, 0, heights);
        var holder = new GameObject("Terrain", typeof(Terrain));
        var content = new GameObject("Content");
        try
        {
            var terrain = holder.GetComponent<Terrain>();
            terrain.terrainData = data;
            var layout = new GameObject("Raid Layout", typeof(RaidGenerator));
            layout.transform.SetParent(content.transform);
            var group = new GameObject("Group", typeof(SpawnGroup));
            group.transform.SetParent(layout.transform);
            var point = new GameObject("Point");
            point.transform.SetParent(group.transform);
            point.transform.position = new Vector3(50f, 15f, 50f);

            RaidMapSurface.SnapToGround(content.transform, terrain);

            Assert.AreEqual(0f, layout.transform.position.y, 0.01f, "the generator isn't lifted, and everything in it with it");
            Assert.AreEqual(15f, point.transform.position.y, 0.01f);
        }
        finally
        {
            Object.DestroyImmediate(content);
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void BrownPathsAndRock_StayBare()
    {
        foreach (RaidMapDefinition map in Maps)
        {
            TerrainGrowth.Ground path = map.Growth.First(entry => entry.layer == map.PathLayer);
            TerrainGrowth.Ground rock = map.Growth.First(entry => entry.layer == map.CliffLayer);
            Assert.AreEqual(0f, path.shortGrass + path.tallGrass + path.bushes + path.plants, $"{map.name}: nothing grows on a path");
            Assert.Greater(path.bareAt, 0f, map.name);
            Assert.Greater(rock.bareAt, 0f, map.name);
        }
    }

    [Test]
    public void TheRaidsTrees_MoveInTheWind()
    {
        foreach (RaidMapDefinition.Scatter tree in Maps.SelectMany(map => map.Trees))
        {
            foreach (Renderer renderer in tree.prefab.GetComponentsInChildren<Renderer>(true))
            {
                Assert.IsTrue(renderer.sharedMaterials.All(material => material.shader.name == "Safe Haven/Tree Wind"), tree.prefab.name);
            }

            Assert.IsNotNull(tree.prefab.GetComponent<ChoppableTree>(), $"{tree.prefab.name} can still be felled");
            Assert.IsNotNull(tree.prefab.GetComponent<CapsuleCollider>(), $"{tree.prefab.name} still blocks the way");
        }
    }

    [Test]
    public void GeneratedMaps_StartWithEveryKindOfGrassAndPlant()
    {
        var data = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset");
        string[] kinds = data.detailPrototypes.Select(prototype => prototype.prototype != null ? prototype.prototype.name : "").ToArray();
        foreach (string kind in new[] { "Short Grass", "Tall Grass", "Bush", "Common Fern", "Eagle Fern", "Glow Mushrooms" })
        {
            CollectionAssert.Contains(kinds, kind);
        }

        Assert.AreEqual(DetailScatterMode.InstanceCountMode, data.detailScatterMode);
        Assert.Greater(data.detailResolution, 0);
    }

    [Test]
    public void GroundGrowth_CoversEveryLayerTheMapPaints_WithAColour()
    {
        foreach (RaidMapDefinition map in Maps)
        {
            foreach (TerrainLayer layer in map.Layers)
            {
                TerrainGrowth.Ground ground = map.Growth.First(entry => entry.layer == layer);
                Assert.Greater(ground.colour.maxColorComponent, 0f, $"{map.name}: {layer.name}");
            }

            Assert.Greater(map.Growth.First(entry => entry.layer == map.ForestLayer).shortGrass, 0.5f, $"{map.name}: the floor is thick");
            Assert.AreEqual(0f, map.Growth.First(entry => entry.layer == map.CliffLayer).shortGrass, $"{map.name}: cliffs are bare");
        }
    }

    [Test]
    public void Growth_FollowsTheGround_AndKeepsOutOfWhatStandsThere()
    {
        var source = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset");
        TerrainData data = Object.Instantiate(source);
        var holder = new GameObject("Terrain", typeof(Terrain));
        try
        {
            var terrain = holder.GetComponent<Terrain>();
            terrain.terrainData = data;
            var weights = new float[data.alphamapHeight, data.alphamapWidth, data.terrainLayers.Length];
            for (int z = 0; z < data.alphamapHeight; z++)
            {
                for (int x = 0; x < data.alphamapWidth; x++)
                {
                    weights[z, x, x < data.alphamapWidth / 2 ? 0 : 2] = 1f;
                }
            }

            data.SetAlphamaps(0, 0, weights);
            Assert.Greater(TerrainGrowth.Fill(terrain, Map.Growth, 1), 0);
            int layer = System.Array.FindIndex(data.detailPrototypes, prototype => prototype.prototype.name == "Short Grass");
            int[,] cells = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            int half = data.detailWidth / 2;
            Assert.Greater(Sum(cells, 0, half - 2), 0, "moss grows grass");
            Assert.AreEqual(0, Sum(cells, half + 2, data.detailWidth), "rock doesn't");

            Assert.AreEqual(0, TerrainGrowth.Fill(terrain, Map.Growth, 1, point => true), "nothing grows where something stands");

            for (int z = 0; z < data.alphamapHeight; z++)
            {
                for (int x = 0; x < data.alphamapWidth; x++)
                {
                    weights[z, x, 0] = 0.15f;
                    weights[z, x, 1] = 0.85f;
                    weights[z, x, 2] = 0f;
                }
            }

            data.SetAlphamaps(0, 0, weights);
            Assert.AreEqual(0, TerrainGrowth.Fill(terrain, Map.Growth, 1), "a path stays bare, even with some moss in its paint");

            for (int z = 0; z < data.alphamapHeight; z++)
            {
                for (int x = 0; x < data.alphamapWidth; x++)
                {
                    weights[z, x, 0] = 0.65f;
                    weights[z, x, 1] = 0.35f;
                }
            }

            data.SetAlphamaps(0, 0, weights);
            Assert.Greater(TerrainGrowth.Fill(terrain, Map.Growth, 1), 0, "open forest floor renders green and keeps its grass");

            Texture2D colour = TerrainGrowth.BakeColour(terrain, Map.Growth);
            Assert.AreEqual(data.alphamapWidth, colour.width);
            Object.DestroyImmediate(colour);
        }
        finally
        {
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(data);
        }
    }
}
