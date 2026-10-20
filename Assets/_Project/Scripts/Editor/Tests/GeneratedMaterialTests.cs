using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GeneratedMaterialTests
{
    private const string Folder = GeneratedTexturePostprocessor.MaterialsRoot + "__MaterialMapTest";
    private const string Name = "__MaterialMapTest";

    [TearDown]
    public void TearDown()
    {
        AssetDatabase.DeleteAsset(Folder);
    }

    private static void WritePng(string suffix, Color color)
    {
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        var pixels = new Color[64];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        texture.SetPixels(pixels);
        File.WriteAllBytes($"{Folder}/{Name}{suffix}.png", texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
    }

    [Test]
    public void ExportedMapSet_GetsLinearImportersAndUrpLitMaterial()
    {
        Directory.CreateDirectory(Folder);
        WritePng("_BaseColor", Color.gray);
        WritePng("_Normal", new Color(0.5f, 0.5f, 1f));
        WritePng("_MetallicSmoothness", new Color(0f, 0f, 0f, 0.3f));
        WritePng("_Occlusion", Color.white);
        WritePng("_Height", Color.gray);
        File.WriteAllText($"{Folder}/{Name}_material.json", "{\"parallax\": 0.02, \"metallic\": 0.0}");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var normal = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Name}_Normal.png");
        var mask = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Name}_MetallicSmoothness.png");
        var baseColor = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Name}_BaseColor.png");
        Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
        Assert.IsFalse(mask.sRGBTexture);
        Assert.IsTrue(baseColor.sRGBTexture);
        Assert.AreEqual(TextureWrapMode.Repeat, baseColor.wrapMode);

        Material material = GeneratedTexturePostprocessor.BuildMaterial(Folder, Name);
        Assert.IsNotNull(material);
        Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
        Assert.IsNotNull(material.GetTexture("_BaseMap"));
        Assert.IsNotNull(material.GetTexture("_BumpMap"));
        Assert.IsNotNull(material.GetTexture("_MetallicGlossMap"));
        Assert.IsNotNull(material.GetTexture("_OcclusionMap"));
        Assert.IsNotNull(material.GetTexture("_ParallaxMap"));
        Assert.AreEqual(0.02f, material.GetFloat("_Parallax"), 1e-5f);
        Assert.IsTrue(material.IsKeywordEnabled("_NORMALMAP"));
        Assert.IsTrue(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
        Assert.IsTrue(material.IsKeywordEnabled("_PARALLAXMAP"));
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{Name}.mat"));
    }

    [Test]
    public void OptionalEmissionDetailAndMasks_AreWiredAndRemovedAgain()
    {
        Directory.CreateDirectory(Folder);
        WritePng("_BaseColor", Color.gray);
        WritePng("_Emission", Color.red);
        WritePng("_DetailAlbedo", Color.gray);
        WritePng("_DetailNormal", new Color(0.5f, 0.5f, 1f));
        WritePng("_DetailMask", Color.white);
        WritePng("_EdgeWear", Color.white);
        File.WriteAllText($"{Folder}/{Name}_material.json",
            "{\"parallax\": 0.0, \"emission_intensity\": 3.0, \"detail_tiling\": 6.0, \"detail_normal_scale\": 0.5}");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var emissionImporter = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Name}_Emission.png");
        var detailAlbedo = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Name}_DetailAlbedo.png");
        var detailNormal = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Name}_DetailNormal.png");
        var edgeWear = (TextureImporter)AssetImporter.GetAtPath($"{Folder}/{Name}_EdgeWear.png");
        Assert.IsTrue(emissionImporter.sRGBTexture);
        Assert.IsFalse(detailAlbedo.sRGBTexture);
        Assert.AreEqual(TextureImporterType.NormalMap, detailNormal.textureType);
        Assert.IsFalse(edgeWear.sRGBTexture);

        Material material = GeneratedTexturePostprocessor.BuildMaterial(Folder, Name);
        Assert.IsNotNull(material.GetTexture("_EmissionMap"));
        Assert.IsTrue(material.IsKeywordEnabled("_EMISSION"));
        Assert.AreEqual(3f, material.GetColor("_EmissionColor").r, 1e-4f);
        Assert.IsNotNull(material.GetTexture("_DetailAlbedoMap"));
        Assert.IsNotNull(material.GetTexture("_DetailNormalMap"));
        Assert.IsNotNull(material.GetTexture("_DetailMask"));
        Assert.IsTrue(material.IsKeywordEnabled("_DETAIL_MULX2"));
        Assert.AreEqual(new Vector2(6f, 6f), material.GetTextureScale("_DetailAlbedoMap"));
        Assert.AreEqual(0.5f, material.GetFloat("_DetailNormalMapScale"), 1e-5f);
        Assert.IsFalse(material.IsKeywordEnabled("_PARALLAXMAP"));

        AssetDatabase.DeleteAsset($"{Folder}/{Name}_Emission.png");
        AssetDatabase.DeleteAsset($"{Folder}/{Name}_DetailAlbedo.png");
        AssetDatabase.DeleteAsset($"{Folder}/{Name}_DetailNormal.png");
        material = GeneratedTexturePostprocessor.BuildMaterial(Folder, Name);
        Assert.IsNull(material.GetTexture("_EmissionMap"));
        Assert.IsFalse(material.IsKeywordEnabled("_EMISSION"));
        Assert.AreEqual(Color.black, material.GetColor("_EmissionColor"));
        Assert.IsFalse(material.IsKeywordEnabled("_DETAIL_MULX2"));
        Assert.IsNull(material.GetTexture("_DetailMask"));
    }

    [Test]
    public void DetailNormalSuffix_IsNotParsedAsNormal()
    {
        Assert.IsTrue(GeneratedTexturePostprocessor.TryParse(
            GeneratedTexturePostprocessor.MaterialsRoot + "rock/rock_DetailNormal.png", out _, out string name,
            out GeneratedTexturePostprocessor.MapKind kind));
        Assert.AreEqual("rock", name);
        Assert.AreEqual(GeneratedTexturePostprocessor.MapKind.DetailNormal, kind);
    }

    [Test]
    public void FilesOutsideMaterialsFolder_AreIgnored()
    {
        Assert.IsFalse(GeneratedTexturePostprocessor.TryParse("Assets/_Project/Art/Generated/rock_Normal.png",
            out _, out _, out _));
        Assert.IsTrue(GeneratedTexturePostprocessor.TryParse(
            GeneratedTexturePostprocessor.MaterialsRoot + "rock/rock_Normal.png", out string folder, out string name,
            out GeneratedTexturePostprocessor.MapKind kind));
        Assert.AreEqual("rock", name);
        Assert.AreEqual(GeneratedTexturePostprocessor.MaterialsRoot + "rock", folder);
        Assert.AreEqual(GeneratedTexturePostprocessor.MapKind.Normal, kind);
    }
}
