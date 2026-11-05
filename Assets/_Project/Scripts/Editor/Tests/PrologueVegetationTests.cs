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
    public void PrologueTerrain_StartsFromTheRaidsForestWithoutSharingItsData()
    {
        // The road, the river and the pass in the west wall change this copy; the raid's map stays as it is.
        var source = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain.asset");
        Assert.AreNotSame(source, Terrain);
        // The painted forest's kinds are the raid's, in the same order; the raid's undergrowth follows them.
        CollectionAssert.AreEqual(source.treePrototypes.Select(p => p.prefab), Terrain.treePrototypes.Take(source.treePrototypes.Length).Select(p => p.prefab));
        Assert.Greater(Terrain.treePrototypes.Length, source.treePrototypes.Length, "The prologue's forest has the raid's undergrowth too.");
        // Only sideways: the World Builder gave it more height to fit the river's bed, without moving the ground.
        Assert.AreEqual(source.size.x, Terrain.size.x);
        Assert.AreEqual(source.size.z, Terrain.size.z);
    }

    [Test]
    public void TreeVariants_HaveWindWeightsAndCompatibleInstancedMaterials()
    {
        int animated = 0;
        foreach (var prototype in Terrain.treePrototypes)
        {
            if (!Sways(prototype.prefab)) continue;
            animated++;
            float crown = 0f;
            foreach (var filter in prototype.prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.TexCoord1));
                Assert.IsTrue(mesh.HasVertexAttribute(VertexAttribute.TexCoord2));
                Assert.AreEqual(mesh.vertexCount, mesh.uv2.Length);
                Assert.AreEqual(mesh.vertexCount, mesh.uv3.Length);
                crown = Mathf.Max(crown, mesh.uv2.Max(uv => uv.x));
                Assert.IsTrue(mesh.uv2.All(uv => uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f));
                foreach (var material in filter.GetComponent<Renderer>().sharedMaterials)
                {
                    Assert.AreEqual(TreeWindMaterials.ShaderName, material.shader.name);
                    Assert.IsTrue(material.enableInstancing);
                    Assert.Greater(material.GetFloat("_WindStrength"), 0f);
                    Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader), material.name);
                }
            }

            // Measured over the whole model: a trunk or a lower part on its own never reaches the top.
            Assert.Greater(crown, 0.9f, prototype.prefab.name + ": the crown must bend.");
        }
        Assert.GreaterOrEqual(animated, 5);
        Assert.AreEqual("Mossy Boulder A", Terrain.treePrototypes[5].prefab.name);
        Assert.IsFalse(Sways(Terrain.treePrototypes[5].prefab), "Rocks must not sway.");
        Assert.AreEqual("Rotten Log A", Terrain.treePrototypes[7].prefab.name);
        Assert.IsFalse(Sways(Terrain.treePrototypes[7].prefab), "Fallen logs must stay still.");
    }

    private static bool Sways(GameObject prefab)
    {
        Renderer drawn = prefab.GetComponentInChildren<Renderer>(true);
        return drawn != null && drawn.sharedMaterial != null && drawn.sharedMaterial.shader.name == TreeWindMaterials.ShaderName;
    }

    [Test]
    public void ForestFloor_HasPopulatedInstancedGrassAndTheRaidsPlants()
    {
        Assert.GreaterOrEqual(Terrain.detailResolution, 512);
        foreach (string name in new[] { "Short Grass", "Tall Grass", "Common Fern", "Moss Cushion", "Wild Fern", "Forest Clover" })
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
