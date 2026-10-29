using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Volumetric fog, set on a scene's volumes like any post effect (Add Override › Safe Haven › Atmospheric Fog), so the
/// night and weather volumes can thicken it. Fog lies in the low ground (or, with Follow Terrain, as a blanket over the
/// ground itself) and thins with height. Stationary by default; optional drift animates clumps and banks.
/// thickens with the weather's fog, is lit by the sun or moon through the shadow maps (light shafts between trees) and
/// by fog lights (torches, beacons: AtmosphereLight), and is pushed back around visibility sources. FogArea adds
/// pockets of extra fog. Drawn by AtmosphereFeature (see Docs/Atmosphere.md).
/// </summary>
[Serializable]
[VolumeComponentMenu("Safe Haven/Atmospheric Fog")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class AtmosphericFog : VolumeComponent
{
    [Tooltip("How thick the fog is at its base height (extinction per metre). 0 switches the fog off.")]
    public ClampedFloatParameter density = new ClampedFloatParameter(0f, 0f, 0.3f);
    [Tooltip("Where the fog is thickest: a world height, or with Follow Terrain at 1, metres above the ground. Above it, it thins out.")]
    public FloatParameter baseHeight = new FloatParameter(0f);
    [Tooltip("0: the fog lies at a fixed world height and pools in valleys. 1: it lies as a blanket over the ground, as " +
             "deep on a hilltop as in a hollow, and spills down slopes (needs a FogGround in the scene).")]
    public ClampedFloatParameter followTerrain = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("How quickly it thins above the base height (per metre). Higher keeps it low on the ground.")]
    public ClampedFloatParameter heightFalloff = new ClampedFloatParameter(0.08f, 0f, 2f);
    [Tooltip("Colour the fog scatters light in.")]
    public ColorParameter albedo = new ColorParameter(new Color(0.85f, 0.9f, 0.95f), false, false, true);
    [Tooltip("Light the fog has everywhere (sky light). Darker at night through the night volume.")]
    public ColorParameter ambient = new ColorParameter(new Color(0.25f, 0.28f, 0.3f), true, false, true);
    [Tooltip("How strongly the sun or moon lights the fog, through its shadows.")]
    public ClampedFloatParameter mainLight = new ClampedFloatParameter(1f, 0f, 8f);
    [Tooltip("Extra airborne particles for sun/moon shafts, independent of the existing fog density. Zero preserves the original fog. Requires shadow-casting branches and a directional light.")]
    public ClampedFloatParameter shaftDensity = new ClampedFloatParameter(0f, 0f, 0.03f);
    [Tooltip("Brightness of the extra shadowed sun/moon scattering, independent of the fog's ambient light.")]
    public ClampedFloatParameter shaftIntensity = new ClampedFloatParameter(2f, 0f, 12f);
    [Tooltip("Shaft particles thin above this height relative to the terrain (or the base height without Fog Ground).")]
    public ClampedFloatParameter shaftHeight = new ClampedFloatParameter(18f, 1f, 100f);
    [Tooltip("Forward scattering: above 0 the fog glows toward the light (sun shafts), below 0 away from it.")]
    public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.45f, -0.9f, 0.9f);
    [Tooltip("How strongly fog lights (torches, beacons) light the fog around them.")]
    public ClampedFloatParameter localLights = new ClampedFloatParameter(1f, 0f, 8f);
    [Tooltip("Size of the drifting clumps, in metres.")]
    public ClampedFloatParameter noiseSize = new ClampedFloatParameter(14f, 1f, 100f);
    [Tooltip("How patchy the fog is: 0 even, 1 in clumps with gaps.")]
    public ClampedFloatParameter noiseStrength = new ClampedFloatParameter(0.15f, 0f, 1f);
    [Tooltip("Fog is a stationary veil by default (0). Raise this to opt into animated fog; Terrain Mist moves independently.")]
    public ClampedFloatParameter driftSpeed = new ClampedFloatParameter(0f, 0f, 4f);
    [Tooltip("How fast the clumps drift, in metres per second.")]
    public Vector3Parameter wind = new Vector3Parameter(new Vector3(0.6f, 0.05f, 0.3f));
    [Tooltip("0 uses authored wind speed; 1 follows the weather's blended Wind multiplier, so storms move fog banks faster. Does not change density.")]
    public ClampedFloatParameter weatherWindInfluence = new ClampedFloatParameter(1f, 0f, 1f);
    [Tooltip("Size of the large fog banks, in metres. They roll over the land with the wind.")]
    public ClampedFloatParameter bankSize = new ClampedFloatParameter(120f, 20f, 600f);
    [Tooltip("How much the banks matter: 0 the fog is even, 1 thick banks with clear stretches between them.")]
    public ClampedFloatParameter bankStrength = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("Banks move this many times the clumps' wind speed (slow banks feel heavy).")]
    public ClampedFloatParameter bankSpeed = new ClampedFloatParameter(0.5f, 0f, 4f);
    [Tooltip("Partial weather control when Weather Only Atmosphere is off. When enabled, weather fully controls fog: Clear and Mist 0, Fog and Storm more.")]
    public ClampedFloatParameter weatherInfluence = new ClampedFloatParameter(0.5f, 0f, 1f);
    [Tooltip("How much a visibility source (the player, a torch, the Heart Tree) clears the fog inside its radius.")]
    public ClampedFloatParameter clearing = new ClampedFloatParameter(0.5f, 0f, 1f);
    [Tooltip("Fog is gathered up to this distance from the camera.")]
    public ClampedFloatParameter maxDistance = new ClampedFloatParameter(90f, 10f, 400f);
    [Tooltip("Samples along each view ray. More is smoother and slower.")]
    public ClampedIntParameter steps = new ClampedIntParameter(28, 8, 64);

    public bool IsActive() => active && (density.value > 0f || shaftDensity.value > 0f);
}
