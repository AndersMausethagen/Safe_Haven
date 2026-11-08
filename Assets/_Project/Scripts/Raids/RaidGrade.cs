using System;
using UnityEngine;

/// <summary>
/// A raid's own colour grade on top of its scene's volumes (RaidGradeVolume): the filmic curve the Haven uses in place
/// of the raids' old ACES, and how much darker or more washed-out this place looks than the Haven. The raids' night,
/// overcast and golden-hour volumes still blend underneath, as only the curve, exposure, contrast, tint and saturation are set here.
/// </summary>
[Serializable]
public sealed class RaidGrade
{
    [Tooltip("Off keeps the scene's own tonemapping (ACES in the raid scenes).")]
    [SerializeField] private FilmicCurve curve = FilmicCurve.Neutral;
    [Tooltip("Stops of light over the scene's own exposure: below 0 darker (the raids are darker than home).")]
    [SerializeField, Range(-3f, 3f)] private float exposure = -0.9f;
    [Tooltip("Colour: 1 as painted, lower more washed-out and grim.")]
    [SerializeField, Range(0f, 2f)] private float saturation = 1.2f;
    [Tooltip("Contrast round middle grey: the raids' volumes were graded under ACES, which had plenty of its own.")]
    [SerializeField, Range(0.5f, 2f)] private float contrast = 1.35f;
    [Tooltip("A tint over the whole raid. The raids' volumes leaned on ACES pushing blue mist toward teal.")]
    [SerializeField] private Color filter = new Color(0.8f, 0.98f, 1f, 1f);

    public FilmicCurve Curve => curve;
    public float Exposure => exposure;
    public float Saturation => saturation;
    public float Contrast => contrast;
    public Color Filter => filter;

    public RaidGrade() { }

    public RaidGrade(FilmicCurve curve, float exposure, float saturation, float contrast, Color filter)
    {
        this.filter = filter;
        this.contrast = contrast;
        this.curve = curve;
        this.exposure = exposure;
        this.saturation = saturation;
    }
}
