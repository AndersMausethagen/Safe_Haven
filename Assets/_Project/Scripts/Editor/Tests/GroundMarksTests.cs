using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Warnings that lie on the ground and read without colour, and the great blow's accent (Docs/LookAndFeelResearch_2026-11-18.md V1, U5,
/// V6): the projected material is set and compiles, its box covers a mark's radius and its reach in metres, a mark's shape rides on its
/// property block, the high-contrast and fog options are in Accessibility, and the impact blur eases out and is on the renderer.
/// </summary>
public sealed class GroundMarksTests
{
    [Test]
    public void TheGroundMarkMaterialIsSetAndCompiles()
    {
        var feel = AssetDatabase.LoadAssetAtPath<CombatFeelSettings>("Assets/_Project/Resources/CombatFeel.asset");
        Assert.IsNotNull(feel.GroundMarkMaterial, "warnings are drawn on the ground");
        Shader shader = feel.GroundMarkMaterial.shader;
        Assert.AreEqual("Safe Haven/Ground Telegraph", shader.name);
        Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
        Assert.IsNotNull(feel.TelegraphMaterial, "the flat fans stay for a pipeline with no depth texture");
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Project/Settings/Rendering/PC_RPAsset.asset");
        Assert.IsTrue(pipeline.supportsCameraDepthTexture, "the projection reads the depth texture");
    }

    [Test]
    public void TheBoxCoversTheRadiusAndReachesUpAndDownInMetres()
    {
        Mesh box = GroundMarks.Box;
        Assert.AreSame(box, GroundMarks.Box, "one for every mark");
        Assert.AreEqual(8, box.vertexCount);
        Assert.AreEqual(36, box.triangles.Length);
        Assert.AreEqual(new Vector3(2f, 2f * GroundMarks.Reach, 2f), box.bounds.size);
        Assert.AreEqual(Vector3.zero, box.bounds.center);
        Assert.AreEqual(new Vector3(3f, 1f, 3f), GroundMarks.Scale(3f), "x and z the radius, y 1: the reach stays in metres");
        // Every face wound outward (the shader draws the far ones).
        Vector3[] vertices = box.vertices;
        int[] triangles = box.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Assert.Greater(Vector3.Dot(normal, (a + b + c) / 3f), 0f, $"triangle {i / 3} faces out");
        }
    }

    [Test]
    public void AMarksShapeRidesOnItsBlock()
    {
        var block = new MaterialPropertyBlock();
        GroundMarks.Shape(block, 90f, 1.2f, 0.07f, GroundMarks.Pattern.Dashed, 0.5f, 1f);
        Assert.AreEqual(90f, block.GetFloat("_Arc"));
        Assert.AreEqual(0.99f, block.GetFloat("_Inner"), 1e-5f, "a ring keeps some width");
        Assert.AreEqual(0.07f, block.GetFloat("_Rim"), 1e-5f);
        Assert.AreEqual(1f, block.GetFloat("_Pattern"), "dashed: a strike any blow breaks off");
        Assert.AreEqual(GroundMarks.Reach, block.GetFloat("_Height"));
        GroundMarks.Flash(block, 3f);
        Assert.AreEqual(1f, block.GetFloat("_Flash"));
        Assert.AreEqual(2, (int)GroundMarks.Pattern.Hatched, "hatched: no guard stops it (the shader's 2)");
    }

    [Test]
    public void AMarkBindsTheTerrainUnderIt()
    {
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32f, 20f, 32f) };
        GameObject holder = Terrain.CreateTerrainGameObject(data);
        try
        {
            holder.transform.position = new Vector3(100f, 5f, 200f);
            GroundMarks.BindGround(new Vector3(110f, 0f, 210f));
            Vector4 scale = Shader.GetGlobalVector("_GroundMarkScale");
            Vector4 place = Shader.GetGlobalVector("_GroundMarkPlace");
            Assert.AreEqual(1f, scale.w, "bound");
            Assert.AreEqual(5f, scale.x, 1e-4f, "its base height");
            Assert.AreEqual(20f / (32766f / 65535f), scale.y, 1e-2f, "metres per heightmap unit");
            Assert.AreEqual(new Vector2(100f, 200f), new Vector2(place.x, place.y), "its corner");
            Assert.AreEqual(32f / (33f * 32f), place.z, 1e-6f, "uv per metre, samples on the corners");
            GroundMarks.BindGround(new Vector3(-500f, 0f, -500f));
            Assert.AreEqual(0f, Shader.GetGlobalVector("_GroundMarkScale").w, "no terrain under it: unbound");
        }
        finally
        {
            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(data);
            GroundMarks.BindGround(new Vector3(-500f, 0f, -500f));
        }
    }

    [Test]
    public void TheWarningAndFogOptionsAreInAccessibility()
    {
        CollectionAssert.Contains(GameOptions.Accessibility, GameOptions.HighContrastWarnings);
        CollectionAssert.Contains(GameOptions.Accessibility, GameOptions.FogAmount);
        Assert.IsFalse(GameOptions.HighContrastWarnings.Default > 0.5f, "off by default: the game as designed");
        Assert.AreEqual(1f, GameOptions.FogAmount.Default, "all the fog by default");
    }

    [Test]
    public void TheImpactBlurEasesOutAndIsOnTheRenderer()
    {
        Assert.AreEqual(0.06f, ImpactBlur.Strength(0.06f, 0f, 0.4f), 1e-5f, "full at once");
        Assert.AreEqual(0.015f, ImpactBlur.Strength(0.06f, 0.2f, 0.4f), 1e-5f, "a quarter halfway (squared)");
        Assert.AreEqual(0f, ImpactBlur.Strength(0.06f, 0.4f, 0.4f), "gone at the end");
        Assert.AreEqual(0f, ImpactBlur.Strength(0.06f, 0.1f, 0f), "no seconds: none");
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Project/Settings/Rendering/PC_Renderer.asset");
        ImpactBlurFeature feature = renderer.rendererFeatures.OfType<ImpactBlurFeature>().FirstOrDefault();
        Assert.IsNotNull(feature, "the blur's pass is on the game's renderer");
        Assert.IsTrue(feature.isActive);
        Shader shader = Shader.Find("Hidden/Safe Haven/Impact Blur");
        Assert.IsNotNull(shader);
        Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
    }

    // Docs/LookAndFeelResearch_2026-11-18.md 3.2: contact shadows are on the game's renderer, compile, and are a Graphics option, on.
    [Test]
    public void ContactShadowsAreOnTheRendererAndAnOption()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Project/Settings/Rendering/PC_Renderer.asset");
        ContactShadowsFeature feature = renderer.rendererFeatures.OfType<ContactShadowsFeature>().FirstOrDefault();
        Assert.IsNotNull(feature);
        Assert.IsTrue(feature.isActive);
        Shader shader = Shader.Find("Hidden/Safe Haven/Contact Shadows");
        Assert.IsNotNull(shader);
        Assert.IsFalse(ShaderUtil.ShaderHasError(shader));
        CollectionAssert.Contains(GameOptions.Graphics, GameOptions.ContactShadows);
        Assert.AreEqual(1f, GameOptions.ContactShadows.Default, "on by default: the game as designed");
    }
}
