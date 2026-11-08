using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>The shader warm-up behind the first loading screen: recorded, holding the world's main shaders, and never
/// holding a load in the Editor.</summary>
public sealed class ShaderWarmupTests
{
    [Test]
    public void TheWarmup_HoldsTheWorldsShaders()
    {
        var settings = AssetDatabase.LoadAssetAtPath<RenderingSettings>("Assets/_Project/Resources/RenderingSettings.asset");
        ShaderVariantCollection variants = settings.WarmupVariants;
        Assert.IsNotNull(variants, "RenderingSettings › Warmup Variants (AgentScripts/RecordShaderWarmup.cs)");
        Assert.Greater(variants.variantCount, 50);
        var so = new SerializedObject(variants);
        SerializedProperty shaders = so.FindProperty("m_Shaders");
        var names = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < shaders.arraySize; i++)
        {
            if (shaders.GetArrayElementAtIndex(i).FindPropertyRelative("first").objectReferenceValue is Shader shader)
            {
                names.Add(shader.name);
            }
        }

        foreach (string wanted in new[] { "Safe Haven/Terrain (World)", "Safe Haven/Tree Wind", "Safe Haven/Grass", "Universal Render Pipeline/Lit" })
        {
            Assert.IsTrue(names.Contains(wanted), wanted);
        }

        Assert.LessOrEqual(settings.WarmupMostSeconds, 30f, "never keeps the player out long");
        Assert.IsTrue(ShaderWarmup.Done, "the Editor compiles shaders its own way: no wait there");
    }
}
