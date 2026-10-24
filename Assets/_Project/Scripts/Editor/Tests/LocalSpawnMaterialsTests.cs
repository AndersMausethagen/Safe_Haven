using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class LocalSpawnMaterialsTests
{
    private const string Fixture = "Assets/_Project/Scripts/Editor/Tests/Fixtures/LocalSpawnQuad.glb.bytes";
    private const string Folder = LocalSpawnMaterials.ModelsRoot + "__LocalSpawnTest";
    private const string Outside = "Assets/__LocalSpawnOutsideTest";
    private const string Glb = Folder + "/quad.glb";

    [TearDown]
    public void TearDown()
    {
        AssetDatabase.DeleteAsset(Folder);
        AssetDatabase.DeleteAsset(Outside);
    }

    private static Material Import(string glbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(glbPath));
        File.Copy(Fixture, glbPath, true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset(glbPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAllAssetsAtPath(glbPath).OfType<Material>().Single();
    }

    private static void WritePng(string path, Color color)
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.SetPixels(Enumerable.Repeat(color, 16).ToArray());
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }

    [Test]
    public void GeneratedGlb_ImportsWithLayeredLit_AndKeepsTheGltfMaps()
    {
        Material material = Import(Glb);

        Assert.AreEqual(LocalSpawnMaterials.DefaultShader, material.shader.name);
        Assert.IsNotNull(material.GetTexture("_BaseMap"));
        Assert.AreEqual(0.3f, material.GetFloat("_Smoothness"), 1e-4f); // roughness 0.7, no ORM map
        Assert.AreEqual(0f, material.GetFloat("_L1Amount"));
        Assert.IsFalse(material.IsKeywordEnabled("_EMISSION"));
    }

    [Test]
    public void GlbOutsideGeneratedModels_KeepsGltfastsShader()
    {
        Material material = Import(Outside + "/quad.glb");

        StringAssert.Contains("glTF", material.shader.name);
    }

    [Test]
    public void Sidecar_SetsShaderValuesLayerTexturesAndLayerNames()
    {
        Directory.CreateDirectory(Folder);
        WritePng(Folder + "/Moss_Color.png", Color.green);
        var sidecar = new LocalSpawnMaterials.Sidecar
        {
            shader = LocalSpawnMaterials.DefaultShader,
            layers = new[] { "Moss", "Rust" },
            properties = new[]
            {
                new LocalSpawnMaterials.Property { name = "_L1Amount", type = "float", value = new[] { 0.4f } },
                new LocalSpawnMaterials.Property { name = "_EmissionColor", type = "color", value = new[] { 2f, 1f, 0.5f, 1f } },
                new LocalSpawnMaterials.Property { name = "_L1Color", type = "texture", texture = "Moss_Color.png" },
            },
        };
        File.WriteAllText(LocalSpawnMaterials.SidecarPath(Glb), JsonUtility.ToJson(sidecar));

        Material material = Import(Glb);

        Assert.AreEqual(0.4f, material.GetFloat("_L1Amount"), 1e-4f);
        Color emission = QualitySettings.activeColorSpace == ColorSpace.Linear
            ? material.GetColor("_EmissionColor").linear
            : material.GetColor("_EmissionColor");
        Assert.AreEqual(2f, emission.r, 1e-2f);
        Assert.AreEqual(0.5f, emission.b, 1e-2f); // Layered Lit always samples emission (no _EMISSION keyword)
        Assert.AreEqual(Folder + "/Moss_Color.png", AssetDatabase.GetAssetPath(material.GetTexture("_L1Color")));
        Assert.AreEqual("Moss", material.GetTag("SurfaceLayer1", false));

        // A controller added from code finds the layers by the names LocalSpawn exported.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var controller = go.AddComponent<SurfaceLayerController>();
            Assert.AreEqual(1, controller.IndexOf("Rust"));
            controller.SetAmount("Moss", 0.8f);
            Assert.AreEqual(0.8f, controller.GetAmount(0), 1e-4f);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void SidecarNamingGltfastsShader_KeepsItAndAppliesTheValues()
    {
        Directory.CreateDirectory(Folder);
        var sidecar = new LocalSpawnMaterials.Sidecar
        {
            shader = "Shader Graphs/glTF-pbrMetallicRoughness",
            properties = new[]
            {
                new LocalSpawnMaterials.Property { name = "roughnessFactor", type = "float", value = new[] { 0.25f } },
            },
        };
        File.WriteAllText(LocalSpawnMaterials.SidecarPath(Glb), JsonUtility.ToJson(sidecar));

        Material material = Import(Glb);

        Assert.AreEqual(sidecar.shader, material.shader.name);
        Assert.AreEqual(0.25f, material.GetFloat("roughnessFactor"), 1e-4f);
    }

    [Test]
    public void BlendMaskAndLayerTextures_GetLinearImporters()
    {
        Directory.CreateDirectory(Folder);
        WritePng(Folder + "/quad_BlendMask.png", Color.white);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/quad_BlendMask.png");
        Assert.IsFalse(importer.sRGBTexture);
        Assert.AreEqual(TextureImporterAlphaSource.FromInput, importer.alphaSource);
    }
}
