using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PrologueVegetationTests
{
    private const string Root = "Assets/_Project/Data/Prologue/Vegetation";
    private TerrainData Terrain => AssetDatabase.LoadAssetAtPath<TerrainData>(Root + "/Prologue Forest Terrain.asset");

    [Test]
    public void PrologueTerrain_PreservesRaidHeightsAndPlacementsWithoutSharingItsData()
    {
        var source = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset");
        Assert.AreNotSame(source, Terrain);
        Assert.AreEqual(source.treeInstanceCount, Terrain.treeInstanceCount);
        Assert.AreEqual(source.treePrototypes.Length, Terrain.treePrototypes.Length);
        Assert.AreEqual(source.size, Terrain.size);
        CollectionAssert.AreEqual(source.GetHeights(0, 0, source.heightmapResolution, source.heightmapResolution),
            Terrain.GetHeights(0, 0, Terrain.heightmapResolution, Terrain.heightmapResolution));
        CollectionAssert.AreEqual(source.treeInstances, Terrain.treeInstances);
    }

    [Test]
    public void TreeVariants_HaveWindWeightsAndCompatibleInstancedMaterials()
    {
        int animated = 0;
        foreach (var prototype in Terrain.treePrototypes)
        {
            if (!prototype.prefab.name.EndsWith(" Wind")) continue;
            animated++;
            foreach (var filter in prototype.prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.TexCoord1));
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.TexCoord2));
                Assert.AreEqual(mesh.vertexCount, mesh.uv2.Length);
                Assert.AreEqual(mesh.vertexCount, mesh.uv3.Length);
                Assert.Greater(mesh.uv2.Max(uv => uv.x), 0.9f, "The crown must bend.");
                Assert.IsTrue(mesh.uv2.All(uv => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f));
                foreach (var material in filter.GetComponent<Renderer>().sharedMaterials)
                {
                    Assert.AreEqual(TreeWindMaterials.ShaderName, material.shader.name);
                    Assert.IsTrue(material.enableInstancing);
                    Assert.Greater(material.GetFloat("_WindStrength"), 0f);
                    Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader), material.name);
                }
            }
        }
        Assert.AreEqual(5, animated);
        Assert.AreEqual("Boulder A", Terrain.treePrototypes[5].prefab.name, "Rocks must not sway.");
        Assert.AreEqual("Fallen Log", Terrain.treePrototypes[7].prefab.name, "Fallen logs must stay still.");
    }

    [Test]
    public void ForestFloor_HasPopulatedInstancedGrassAndNordicPlants()
    {
        Assert.GreaterOrEqual(Terrain.detailResolution, 512);
        foreach (string name in new[] { "Short Grass", "Tall Grass", "Fern", "Blueberry", "Lingonberry", "Heather" })
        {
            int layer = System.Array.FindIndex(Terrain.detailPrototypes, p => p.prototype.name == name);
            Assert.GreaterOrEqual(layer, 0, name);
            Assert.IsTrue(Terrain.detailPrototypes[layer].useInstancing, name);
            int total = Terrain.GetDetailLayer(0, 0, Terrain.detailResolution, Terrain.detailResolution, layer).Cast<int>().Sum();
            Assert.Greater(total, 0, name);
            var material = Terrain.detailPrototypes[layer].prototype.GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual(GrassSetup.ShaderName, material.shader.name);
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
        }
    }

    [Test]
    public void StoryRouteCampAndPortal_RemainFreeOfDetails()
    {
        int resolution = Terrain.detailResolution;
        foreach (var prototype in Terrain.detailPrototypes.Select((p, index) => index))
        {
            int[,] cells = Terrain.GetDetailLayer(0, 0, resolution, resolution, prototype);
            foreach (float z in new[] { -50f, -40f, -27f, -10f, 7f })
            {
                int x = Mathf.FloorToInt(100f / Terrain.size.x * resolution);
                int y = Mathf.FloorToInt((z + 100f) / Terrain.size.z * resolution);
                Assert.AreEqual(0, cells[y, x], "Keep the story route readable.");
            }
        }
    }
}
