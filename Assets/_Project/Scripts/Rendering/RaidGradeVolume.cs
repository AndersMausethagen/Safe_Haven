using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The running raid's grade (RaidDefinition › Look): a global volume above all of the scene's own, overriding only the
/// tonemapping (the filmic curve, with URP's Tonemapping off as FilmicTonemapping needs) and the filmic exposure, contrast and
/// saturation, so the scene's night, overcast and golden-hour volumes keep blending below it. Off at home. Made by
/// WorldRendering.
/// </summary>
public sealed class RaidGradeVolume : MonoBehaviour
{
    /// <summary>Above every scene volume.</summary>
    public const float Priority = 1000f;

    private Volume volume;
    private VolumeProfile profile;
    private FilmicTonemapping filmic;
    private Tonemapping tonemapping;

    public void Initialise()
    {
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Raid Grade (runtime)";
        profile.hideFlags = HideFlags.DontSave;
        filmic = profile.Add<FilmicTonemapping>();
        tonemapping = profile.Add<Tonemapping>();
        volume = gameObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = Priority;
        volume.sharedProfile = profile;
        volume.enabled = false;
    }

    /// <summary>Grades the raid, or switches off (at home, or a raid that keeps its scene's own curve).</summary>
    public void Apply(RaidGrade grade)
    {
        bool on = grade != null && grade.Curve != FilmicCurve.Off;
        volume.enabled = on;
        if (!on)
        {
            return;
        }

        filmic.curve.Override(grade.Curve);
        filmic.exposure.Override(grade.Exposure);
        filmic.saturation.Override(grade.Saturation);
        filmic.contrast.Override(grade.Contrast);
        filmic.filter.Override(grade.Filter);
        tonemapping.mode.Override(TonemappingMode.None);
    }

    private void OnDestroy()
    {
        if (profile != null)
        {
            Destroy(profile);
        }
    }
}
