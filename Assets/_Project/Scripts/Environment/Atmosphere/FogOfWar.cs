using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// How what the player has not seen, and what is far from any light at night, looks (Add Override › Safe Haven › Fog
/// of War). Unexplored ground (ExplorationMap) sinks into dark, drifting fog at any hour. At night, everything outside
/// every visibility source's radius (the player's sight, torches, the Heart Tree's ward) gets a grey, colourless veil,
/// in the way of Gord: by day all that was explored is plain to see. Drawn by AtmosphereFeature.
/// </summary>
[Serializable]
[VolumeComponentMenu("Safe Haven/Fog of War")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class FogOfWar : VolumeComponent
{
    [Tooltip("How completely unexplored ground is hidden. 0 shows it.")]
    public ClampedFloatParameter unexplored = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("Colour of the fog over unexplored ground.")]
    public ColorParameter unexploredColor = new ColorParameter(new Color(0.03f, 0.035f, 0.045f), false, false, true);
    [Tooltip("How strongly the night veil covers what is far from any light (× how dark the night is).")]
    public ClampedFloatParameter nightVeil = new ClampedFloatParameter(0f, 0f, 1f);
    [Tooltip("Colour the veiled world is washed toward.")]
    public ColorParameter veilColor = new ColorParameter(new Color(0.42f, 0.45f, 0.5f), false, false, true);
    [Tooltip("How much colour the veil takes away (1 = grey).")]
    public ClampedFloatParameter veilDesaturation = new ClampedFloatParameter(0.85f, 0f, 1f);
    [Tooltip("How much darker the veiled world gets.")]
    public ClampedFloatParameter veilDarkening = new ClampedFloatParameter(0.45f, 0f, 1f);
    [Tooltip("A flat grey haze laid over the veiled world (in the veil colour), so the far night reads as grey, not black.")]
    public ClampedFloatParameter veilHaze = new ClampedFloatParameter(0.05f, 0f, 0.5f);

    public bool IsActive() => active && (unexplored.value > 0f || nightVeil.value > 0f);
}
