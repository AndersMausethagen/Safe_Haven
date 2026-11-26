using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The ruin kit (RuinKitBuilder): props of the old village made from the Medieval Village Megapack's rustic parts. Each holds the
/// top-down budget (LOD0 5-9k at most for its size, lighter levels after), can't be walked through, and draws with the project's
/// Layered Lit with its own colour, normal and mask maps: the pack's models came without materials, and a prop with a missing or
/// colour-imported normal map renders black in patches (the 11-26 material trap).
/// </summary>
public sealed class RuinKitTests
{
    private const string Prefabs = "Assets/_Project/Art/World/Ruins/Prefabs";

    [Test]
    public void EveryRuin_HoldsTheBudget_IsSolid_AndDrawsWithLayeredLit()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs });
        Assert.GreaterOrEqual(guids.Length, 20, "the kit: walls, stones, fences, frames, carts, the well");
        foreach (string guid in guids)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var group = prefab.GetComponent<LODGroup>();
            Assert.IsNotNull(group, $"{prefab.name}: an LODGroup");
            LOD[] lods = group.GetLODs();
            Assert.GreaterOrEqual(lods.Length, 2, $"{prefab.name}: a lighter level for the action camera's distance");
            int[] triangles = lods.Select(l => l.renderers.Sum(r => Triangles(r))).ToArray();
            Assert.LessOrEqual(triangles[0], 9000, $"{prefab.name}: LOD0 within the top-down budget");
            for (int i = 1; i < triangles.Length; i++)
            {
                Assert.Less(triangles[i], triangles[i - 1], $"{prefab.name}: each level lighter than the last");
            }

            Assert.IsNotNull(prefab.GetComponentInChildren<Collider>(true), $"{prefab.name}: solid");
            foreach (Renderer drawn in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in drawn.sharedMaterials)
                {
                    Assert.IsNotNull(material, $"{prefab.name}/{drawn.name}: a material");
                    Assert.AreEqual("Safe Haven/Layered Lit", material.shader.name, $"{prefab.name}: {material.name}");
                    Assert.IsNotNull(material.GetTexture("_BaseMap"), $"{material.name}: colour");
                    Assert.IsNotNull(material.GetTexture("_ORMMap"), $"{material.name}: occlusion, roughness, metal");
                    var normal = material.GetTexture("_BumpMap");
                    // Layered Lit always reads its normal map (it has no _NORMALMAP keyword): what matters is how it was imported.
                    Assert.IsNotNull(normal, $"{material.name}: normal map");
                    var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal)) as TextureImporter;
                    Assert.AreEqual(TextureImporterType.NormalMap, importer.textureType, $"{normal.name}: imported as a normal map");
                }
            }
        }
    }

    private static int Triangles(Renderer drawn)
    {
        var filter = drawn.GetComponent<MeshFilter>();
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        if (mesh == null)
        {
            return 0;
        }

        int count = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            count += (int)mesh.GetIndexCount(s) / 3;
        }

        return count;
    }
}
