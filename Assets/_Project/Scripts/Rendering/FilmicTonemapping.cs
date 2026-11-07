using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Which curve FilmicTonemapping brings the scene's light into the screen's range with.</summary>
public enum FilmicCurve
{
    /// <summary>Off: URP's own Tonemapping (ACES in the raids) does it.</summary>
    Off = 0,
    /// <summary>Khronos PBR Neutral: keeps base colours as painted, rolls only the highlights off. Greens stay green.</summary>
    Neutral = 1,
    /// <summary>AgX: bright colours ease towards white as film does, never skewing (fire goes yellow-white, not pink).</summary>
    AgX = 2,
    /// <summary>AgX with more contrast and colour, for a sunlit look.</summary>
    AgXPunchy = 3,
}

/// <summary>
/// A tonemapper URP lacks (TonemapFeature on PC_Renderer): Khronos PBR Neutral or AgX in place of ACES, which turns greens
/// olive and crushes the shadows. It runs after the fog and before URP's post-processing, so a volume using it sets URP's
/// Tonemapping to None: bloom and the colour grading then work on what it hands on, in the screen's range (Bloom's
/// threshold is in that range too). The Haven's volumes use it; the raids keep ACES.
/// </summary>
[Serializable]
[VolumeComponentMenu("Safe Haven/Filmic Tonemapping")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class FilmicTonemapping : VolumeComponent
{
    [Serializable]
    public sealed class CurveParameter : VolumeParameter<FilmicCurve>
    {
        public CurveParameter(FilmicCurve value, bool overrideState = false) : base(value, overrideState) { }
    }

    [Tooltip("The curve. Off leaves it to URP's Tonemapping.")]
    public CurveParameter curve = new CurveParameter(FilmicCurve.Off);
    [Tooltip("Stops of exposure before the curve: + brighter, - darker.")]
    public ClampedFloatParameter exposure = new ClampedFloatParameter(0f, -3f, 3f);
    [Tooltip("Colour after the curve: 1 as it comes, more richer.")]
    public ClampedFloatParameter saturation = new ClampedFloatParameter(1f, 0f, 2f);

    public bool IsActive() => active && curve.value != FilmicCurve.Off;
}
