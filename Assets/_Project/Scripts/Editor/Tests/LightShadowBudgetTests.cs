using NUnit.Framework;
using UnityEngine;

/// <summary>Fire shadows: only world fires and lamps of some reach count, never a light someone carries, and the budget
/// fits the shadow atlas.</summary>
public sealed class LightShadowBudgetTests
{
    [Test]
    public void OnlyWorldLightsOfSomeReach_Count()
    {
        var fire = new GameObject("Fire", typeof(Light));
        var carrier = new GameObject("Carrier", typeof(Animator));
        var carried = new GameObject("Torch", typeof(Light));
        carried.transform.SetParent(carrier.transform);
        try
        {
            Light light = fire.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 8f;
            light.intensity = 2f;
            Assert.IsTrue(LightShadowBudget.Counts(light, 4f));
            light.range = 2f;
            Assert.IsFalse(LightShadowBudget.Counts(light, 4f), "a small glow");
            light.range = 8f;
            light.type = LightType.Directional;
            Assert.IsFalse(LightShadowBudget.Counts(light, 4f), "the sun has its own");
            Light torch = carried.GetComponent<Light>();
            torch.type = LightType.Point;
            torch.range = 8f;
            torch.intensity = 2f;
            Assert.IsFalse(LightShadowBudget.Counts(torch, 4f), "carried by someone");
        }
        finally
        {
            Object.DestroyImmediate(fire);
            Object.DestroyImmediate(carrier);
        }
    }

    [Test]
    public void TheBudget_FitsTheAtlas()
    {
        var settings = UnityEditor.AssetDatabase.LoadAssetAtPath<RenderingSettings>("Assets/_Project/Resources/RenderingSettings.asset");
        var pipeline = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(
            "Assets/_Project/Settings/Rendering/PC_RPAsset.asset");
        Assert.IsTrue(pipeline.supportsAdditionalLightShadows);
        // Six faces each: the nearest at the medium tier, the rest at the low one.
        int medium = pipeline.additionalLightsShadowResolutionTierMedium, low = pipeline.additionalLightsShadowResolutionTierLow;
        long needed = 6L * medium * medium + 6L * (settings.ShadowedLights - 1) * low * low;
        long atlas = (long)pipeline.additionalLightsShadowmapResolution * pipeline.additionalLightsShadowmapResolution;
        Assert.LessOrEqual(needed, atlas);
    }

    // Three point lights by night (the Haven's torches, the prologue's camp): URP warned every frame when they asked for more
    // than the atlas holds (the user, 2026-11-15). The finest tiers that fit are picked for the atlas the options give.
    [Test]
    public void Tiers_AreTheFinestThatFit()
    {
        const int high = 2, medium = 1, low = 0;
        LightShadowBudget.Tiers(4096, 1024, 512, 256, 6, 12, out int near, out int rest);
        Assert.AreEqual((high, medium), (near, rest), "4096 holds the nearest at 1024 and two more at 512");
        LightShadowBudget.Tiers(2048, 1024, 512, 256, 6, 12, out near, out rest);
        Assert.AreEqual((medium, low), (near, rest), "2048 can't hold one point light at 1024");
        LightShadowBudget.Tiers(4096, 1024, 512, 256, 6, 0, out near, out rest);
        Assert.AreEqual(high, near, "one light alone");
        LightShadowBudget.Tiers(1024, 1024, 512, 256, 6, 12, out near, out rest);
        Assert.AreEqual((low, low), (near, rest), "nothing fits: the coarsest");
    }
}
