using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Every generated prop in Art/World/Props has its own Layered Lit material, which the prefabs that show it use, and maps
/// imported as what they are. The 2026-11-26 props had neither: they drew with the FBX's plain Lit and a colour-imported
/// normal map, nearly black in patches.
/// </summary>
public sealed class PropMaterialTests
{
    private static string[] Props() => Directory.GetDirectories(PropMapImport.PropsRoot)
        .Select(d => d.Replace('\\', '/'))
        .Where(d => File.Exists($"{d}/{Path.GetFileName(d)}.fbx") && File.Exists($"{d}/{Path.GetFileName(d)}_Albedo.png"))
        .ToArray();

    [Test]
    public void EveryPropHasItsOwnMaterial_WithItsMapsImportedAsWhatTheyAre()
    {
        string[] props = Props();
        Assert.IsNotEmpty(props);
        foreach (string folder in props)
        {
            string name = Path.GetFileName(folder);
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{folder}/{name}.mat");
            Assert.IsNotNull(material, $"{name}: its own material");
            Assert.AreEqual("Safe Haven/Layered Lit", material.shader.name, $"{name}: the props' shader");
            Assert.AreEqual($"{folder}/{name}_Albedo.png", AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), $"{name}: its own colour");

            var normal = (TextureImporter)AssetImporter.GetAtPath($"{folder}/{name}_Normal.png");
            if (normal != null)
            {
                Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType, $"{name}_Normal: imported as a normal map");
                Assert.AreEqual($"{folder}/{name}_Normal.png", AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")), $"{name}: its own normal map");
            }

            foreach (string map in new[] { "_ORM", "_BlendMask" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath($"{folder}/{name}{map}.png");
                if (importer != null)
                {
                    Assert.IsFalse(importer.sRGBTexture, $"{name}{map}: data, not colour");
                }
            }
        }
    }

    [Test]
    public void EveryPrefabShowingAPropUsesItsMaterial()
    {
        var own = Props().ToDictionary(f => $"{f}/{Path.GetFileName(f)}.fbx", f => $"{f}/{Path.GetFileName(f)}.mat");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Raids" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (MeshRenderer renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                Mesh mesh = renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
                if (mesh == null ||!own.TryGetValue(AssetDatabase.GetAssetPath(mesh), out string materialPath))
                {
                    continue;
                }

                Assert.AreEqual(materialPath, AssetDatabase.GetAssetPath(renderer.sharedMaterial), $"{path} › {renderer.name}: the prop's own material, not the FBX's");
            }
        }
    }
}
