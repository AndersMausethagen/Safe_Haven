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
}
