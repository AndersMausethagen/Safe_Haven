using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Shadows from fires and lamps, within a budget: at dusk and by night the few world lights nearest what the camera looks
/// at (campfires, torches, lantern posts, braziers: point and spot lights of some reach that no character carries) cast
/// soft shadows, the nearest at a finer resolution; every other light keeps none. By day the sun drowns them, so none
/// cast. Every light it switched on is put back as it was when it drops out of the budget. Made by WorldRendering;
/// numbers in RenderingSettings › Fire shadows.
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
    private float nextLook;

    /// <summary>Lights casting shadows now (for tests and probes).</summary>
    public IReadOnlyCollection<Light> Shadowed => shadowed;

    public void Initialise(RenderingSettings rules) => settings = rules;

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

            if (light.TryGetComponent(out UniversalAdditionalLightData data))
            {
                // The nearest one sharp, the rest coarser: all of them fit the shadow atlas together.
                data.additionalLightsShadowResolutionTier = i == 0 ? UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium
                    : UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow;
            }
        }
    }

    private void Choose(Vector3 centre)
    {
        candidates.Clear();
        float reach = settings.FireShadowReach * settings.FireShadowReach;
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (!Counts(light, settings.FireShadowLeastRange))
            {
                continue;
            }

            Vector3 offset = light.transform.position - centre;
            if (offset.x * offset.x + offset.z * offset.z <= reach)
            {
                candidates.Add(light);
            }
        }

        candidates.Sort((a, b) => (a.transform.position - centre).sqrMagnitude.CompareTo((b.transform.position - centre).sqrMagnitude));
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
