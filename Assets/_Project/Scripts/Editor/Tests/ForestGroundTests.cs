using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

public sealed class ForestGroundTests
{
    [TestCase("Moss")]
    [TestCase("Soil")]
    [TestCase("Stone")]
    public void TextureSource_IsPeriodicDeterministicAndBounded(string kind)
    {
        for (int y = 0; y < 13; y++)
            for (int x = 0; x < 13; x++)
            {
                Vector2 uv = new Vector2(x / 16f, y / 16f);
                ForestGroundTextures.Sample(kind, uv, out Color color, out float height, out float roughness);
                ForestGroundTextures.Sample(kind, uv, out Color repeated, out float repeatedHeight, out float repeatedRoughness);
                Assert.AreEqual(color, repeated);
                Assert.AreEqual(height, repeatedHeight);
                Assert.AreEqual(roughness, repeatedRoughness);
                Assert.That(height, Is.InRange(0f, 1f));
                Assert.That(roughness, Is.InRange(0f, 1f));
                for (int channel = 0; channel < 4; channel++) Assert.That(color[channel], Is.InRange(0f, 1f));
                foreach (Vector2 offset in new[] { Vector2.right, Vector2.up, -Vector2.one })
                {
                    ForestGroundTextures.Sample(kind, uv + offset, out Color tiled, out float tiledHeight, out float tiledRoughness);
                    Assert.Less((new Vector4(color.r, color.g, color.b, color.a) - new Vector4(tiled.r, tiled.g, tiled.b, tiled.a)).magnitude, 0.0001f);
                    Assert.AreEqual(height, tiledHeight, 0.0001f);
                    Assert.AreEqual(roughness, tiledRoughness, 0.0001f);
                }
            }
    }

    [TestCase("Moss")]
    [TestCase("Soil")]
    [TestCase("Stone")]
    public void SavedMaps_HaveCorrectResolutionColorSpaceAndNormalImport(string kind)
    {
        foreach (string map in new[] { "BaseColor", "Normal", "Packed", "TerrainMask" })
        {
            string path = ForestGroundTextures.Root + "/" + kind + "_" + map + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(texture, path);
            Assert.AreEqual(ForestGroundTextures.Resolution, texture.width);
            Assert.AreEqual(ForestGroundTextures.Resolution, texture.height);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(map == "BaseColor", importer.sRGBTexture, path);
            Assert.AreEqual(map == "Normal" ? TextureImporterType.NormalMap : TextureImporterType.Default, importer.textureType, path);
            Assert.AreEqual(TextureWrapMode.Repeat, importer.wrapMode);
            Assert.IsTrue(importer.mipmapEnabled);
        }
    }

    [TestCase(ForestGroundShaders.GroundPath)]
    [TestCase(ForestGroundShaders.StonePath)]
    public void SavedAmplifyShaders_AreEditablePopulatedAndCompile(string path)
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        Assert.IsNotNull(shader);
        Assert.IsTrue(shader.isSupported);
        string source = File.ReadAllText(path);
        StringAssert.Contains("ASEBEGIN", source);
        StringAssert.Contains("AmplifyShaderEditor.SamplerNode", source);
        StringAssert.Contains("#define _NORMAL_DROPOFF_TS 1", source);
        foreach (string property in new[] { "_GroundColor", "_GroundNormal", "_GroundSurface", "_MossColor", "_MossNormal", "_MossSurface", "_Tint", "_Relief", "_MossCoverage", "_Tiling" })
            Assert.GreaterOrEqual(shader.FindPropertyIndex(property), 0, property);
        var errors = ShaderUtil.GetShaderMessages(shader).Where(message => message.severity == ShaderCompilerMessageSeverity.Error).ToArray();
        Assert.IsEmpty(errors, string.Join("\n", errors.Select(error => error.message)));
    }
}
