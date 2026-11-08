using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The world's fires and lamps (campfires, torches, lantern posts, hearths, braziers: point and spot lights of some reach
/// that no character carries, LightShadowBudget.Counts), found by a scene scan every few seconds rather than by each system
/// every frame, since lights seldom come and go. LightShadowBudget picks its shadow casters from this list. Each one also
/// glows in the volumetric fog: any without an AtmosphereLight gets one (the Campfire and Torch prefabs had theirs; lantern
/// posts, hearths and kitchen fires lit nothing in the mist). Made by WorldRendering.
/// </summary>
public sealed class WorldLights : MonoBehaviour
{
    private const float ScanEvery = 3f;

    private readonly List<Light> lights = new List<Light>();
    private RenderingSettings settings;
    private float nextScan;

    /// <summary>Fires and lamps as of the last scan (some may have gone since: skip nulls).</summary>
    public IReadOnlyList<Light> Lights => lights;

    public void Initialise(RenderingSettings rules) => settings = rules;

    /// <summary>Looks again on the next frame (a new scene).</summary>
    public void Rescan() => nextScan = 0f;

    private void Update()
    {
        if (Time.unscaledTime < nextScan)
        {
            return;
        }

        nextScan = Time.unscaledTime + ScanEvery;
        lights.Clear();
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
        {
            if (!LightShadowBudget.Counts(light, settings.FireShadowLeastRange))
            {
                continue;
            }

            lights.Add(light);
            if (settings.FireFogGlow && light.type == LightType.Point && !light.TryGetComponent(out AtmosphereLight _))
            {
                light.gameObject.AddComponent<AtmosphereLight>().Configure(settings.FireFogGlowStrength, settings.FireFogClearing);
            }
        }
    }
}
