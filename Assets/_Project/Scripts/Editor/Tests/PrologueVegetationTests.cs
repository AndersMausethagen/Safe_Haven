using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
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

    // Sampled along the route the party really walks (the scene's Walk Party nodes, with points between their stops), not at points
    // written down beside it: a route moved in the scene would leave fixed points checking empty ground.
    [Test]
    public void StoryRouteCampAndPortal_RemainFreeOfDetails()
    {
        TerrainData data = Terrain;
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Prologue_MistyWoods.unity");
        try
        {
            GameObject[] roots = scene.GetRootGameObjects();
            Terrain forest = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).Single(t => t.terrainData == data);
            var route = new List<Vector3>();
            foreach (WalkPartyNode walk in roots.SelectMany(r => r.GetComponentsInChildren<StoryScript>(true)).SelectMany(s => s.Nodes).OfType<WalkPartyNode>())
            {
                if (walk.route == null)
                {
                    continue;
                }

                Vector3? last = null;
                foreach (Transform stop in walk.route)
                {
                    for (float t = 0.25f; last.HasValue && t < 1f; t += 0.25f)
                    {
                        route.Add(Vector3.Lerp(last.Value, stop.position, t));
                    }

                    route.Add(stop.position);
                    last = stop.position;
                }
            }

            int resolution = data.detailResolution;
            int[][,] layers = Enumerable.Range(0, data.detailPrototypes.Length).Select(layer => data.GetDetailLayer(0, 0, resolution, resolution, layer)).ToArray();
            var grown = new List<string>();
            int sampled = 0;
            foreach (Vector3 at in route)
            {
                Vector3 local = at - forest.transform.position;
                if (local.x < 0f || local.z < 0f || local.x >= data.size.x || local.z >= data.size.z)
                {
                    continue; // on the meadow's terrain, not the forest's
                }

                sampled++;
                int x = Mathf.FloorToInt(local.x / data.size.x * resolution), y = Mathf.FloorToInt(local.z / data.size.z * resolution);
                for (int layer = 0; layer < layers.Length; layer++)
                {
                    if (layers[layer][y, x] > 0)
                    {
                        grown.Add($"{data.detailPrototypes[layer].prototype.name} at {at}");
                    }
                }
            }

            Assert.Greater(sampled, 0, "the party's walk crosses the forest");
            Assert.IsEmpty(grown, "Keep the story route readable:\n" + string.Join("\n", grown));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
