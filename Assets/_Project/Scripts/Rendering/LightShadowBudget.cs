using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Shadows from fires and lamps, within a budget: at dusk and by night the few world lights nearest what the camera looks
/// at (campfires, torches, lantern posts, braziers: point and spot lights of some reach that no character carries) cast
/// soft shadows, the nearest at a finer resolution (picked from WorldLights' list, scanned every few seconds); every other light keeps none. By day the sun drowns them, so none
/// cast. Every light it switched on is put back as it was when it drops out of the budget. Made by WorldRendering;
/// numbers in RenderingSettings › Fire shadows.
/// The resolutions are picked to fit the additional-light shadow atlas (GraphicsOptions sizes it by the Shadows option):
/// the nearest as fine as fits, the rest one step coarser. A light without URP's additional data asked for the High tier
/// whatever was set (the data was never added), and URP halved them all with a console warning every frame.
/// </summary>
public sealed class LightShadowBudget : MonoBehaviour
{
    private const float Every = 0.5f;

    private readonly List<Light> candidates = new List<Light>();
    private readonly HashSet<Light> shadowed = new HashSet<Light>();
    private readonly Dictionary<Light, (LightShadows shadows, float strength, int tier)> before =
        new Dictionary<Light, (LightShadows, float, int)>();
    private readonly List<Light> chosen = new List<Light>();
    private readonly List<Light> dropped = new List<Light>();
    private RenderingSettings settings;
    private WorldLights world;
    private float nextLook;
    private Vector3 sortFrom;
    private System.Comparison<Light> nearer;

    /// <summary>Lights casting shadows now (for tests and probes).</summary>
    public IReadOnlyCollection<Light> Shadowed => shadowed;

    public void Initialise(RenderingSettings rules, WorldLights fires)
    {
        settings = rules;
        world = fires;
        nearer = (a, b) => (a.transform.position - sortFrom).sqrMagnitude.CompareTo((b.transform.position - sortFrom).sqrMagnitude);
    }

    /// <summary>Whether a light counts: a world fire or lamp of some reach, not carried by anyone.</summary>
    public static bool Counts(Light light, float leastRange) =>
        light != null && light.isActiveAndEnabled && (light.type == LightType.Point || light.type == LightType.Spot)
        && light.range >= leastRange && light.intensity > 0.05f && light.GetComponentInParent<Animator>() == null
        && light.GetComponentInParent<UnityEngine.AI.NavMeshAgent>() == null && light.GetComponentInParent<Player>() == null;

    private void Update()
    {
        if (Time.unscaledTime < nextLook)
        {
            return;
        }

        nextLook = Time.unscaledTime + Every;
        chosen.Clear();
        Camera camera = Camera.main;
        bool dark = AtmosphereConditions.Night > settings.FireShadowsFromNight || AtmosphereConditions.GoldenHour > 0.5f;
        if (camera != null && dark && settings.ShadowedLights > 0)
        {
            Choose(RainEffect.GroundInView(camera));
        }

        dropped.Clear();
        foreach (Light light in shadowed)
        {
            if (light == null || !chosen.Contains(light))
            {
                dropped.Add(light);
            }
        }

        foreach (Light light in dropped)
        {
            PutBack(light);
            shadowed.Remove(light);
        }

        PickTiers(out int nearestTier, out int otherTier);
        for (int i = 0; i < chosen.Count; i++)
        {
            Light light = chosen[i];
            if (shadowed.Add(light))
            {
                light.TryGetComponent(out UniversalAdditionalLightData was);
                before[light] = (light.shadows, light.shadowStrength, was != null ? was.additionalLightsShadowResolutionTier : -2);
                light.shadows = LightShadows.Soft;
                light.shadowStrength = settings.FireShadowStrength;
                light.shadowNearPlane = 0.2f;
            }

            // The nearest one sharp, the rest coarser: all of them fit the shadow atlas together.
            light.GetUniversalAdditionalLightData().additionalLightsShadowResolutionTier = i == 0 ? nearestTier : otherTier;
        }
    }

    // The finest pair of tiers (nearest, others) whose shadow maps fit the atlas together: a point light needs six maps.
    private void PickTiers(out int nearestTier, out int otherTier)
    {
        nearestTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
        otherTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
        if (!(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline) || chosen.Count == 0)
        {
            return;
        }

        int otherMaps = 0;
        for (int i = 1; i < chosen.Count; i++)
        {
            otherMaps += MapsFor(chosen[i]);
        }

        Tiers(pipeline.additionalLightsShadowmapResolution, pipeline.additionalLightsShadowResolutionTierHigh,
            pipeline.additionalLightsShadowResolutionTierMedium, pipeline.additionalLightsShadowResolutionTierLow,
            MapsFor(chosen[0]), otherMaps, out nearestTier, out otherTier);
    }

    /// <summary>
    /// The finest tiers (URP's High 2, Medium 1, Low 0) for the nearest light's <paramref name="nearestMaps"/> and the others'
    /// <paramref name="otherMaps"/> shadow maps that fit an <paramref name="atlas"/>-wide atlas: (High, Medium), then
    /// (Medium, Low), then (Low, Low), which is also the answer when nothing fits.
    /// </summary>
    public static void Tiers(int atlas, int high, int medium, int low, int nearestMaps, int otherMaps, out int nearestTier, out int otherTier)
    {
        long room = (long)atlas * atlas;
        (int near, int rest, long nearSize, long restSize)[] pairs =
        {
            (UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium, high, medium),
            (UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow, medium, low),
            (UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow, low, low),
        };
        foreach (var pair in pairs)
        {
            nearestTier = pair.near;
            otherTier = pair.rest;
            if (pair.nearSize * pair.nearSize * nearestMaps + pair.restSize * pair.restSize * otherMaps <= room)
            {
                return;
            }
        }

        nearestTier = otherTier = UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
    }

    private static int MapsFor(Light light) => light.type == LightType.Point ? 6 : 1;

    private void Choose(Vector3 centre)
    {
        candidates.Clear();
        float reach = settings.FireShadowReach * settings.FireShadowReach;
        foreach (Light light in world.Lights)
        {
            if (light == null || !light.isActiveAndEnabled)
            {
                continue;
            }

            Vector3 offset = light.transform.position - centre;
            if (offset.x * offset.x + offset.z * offset.z <= reach)
            {
                candidates.Add(light);
            }
        }

        sortFrom = centre;
        candidates.Sort(nearer);
        for (int i = 0; i < candidates.Count && chosen.Count < settings.ShadowedLights; i++)
        {
            chosen.Add(candidates[i]);
        }
    }

    private void PutBack(Light light)
    {
        if (light != null && before.TryGetValue(light, out var was))
        {
            light.shadows = was.shadows;
            light.shadowStrength = was.strength;
            if (was.tier >= -1 && light.TryGetComponent(out UniversalAdditionalLightData data))
            {
                data.additionalLightsShadowResolutionTier = was.tier;
            }
        }

        before.Remove(light);
    }

    private void OnDestroy()
    {
        foreach (Light light in shadowed)
        {
            PutBack(light);
        }
    }
}
