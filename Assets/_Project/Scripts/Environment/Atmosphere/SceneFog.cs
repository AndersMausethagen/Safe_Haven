using UnityEngine;

/// <summary>
/// A scene's own share of its fog: every fog drawn while this scene is open (the volumetric fog of whichever profile is
/// blending in, fog areas, mists, the distance fog) is thinned or thickened by Density Scale, without touching the shared
/// profiles the Haven and the raids use. The prologue has one at 0.7 (the user, 2026-11-15: "make the fog in the prologue
/// 30% less dense"). Writes AtmosphereConditions.SceneFogScale while enabled.
/// </summary>
[AddComponentMenu("Safe Haven/Environment/Scene Fog")]
public sealed class SceneFog : MonoBehaviour
{
    [Tooltip("Share of every fog's density in this scene: 1 as the profiles have it, 0.7 a third thinner.")]
    [SerializeField, Range(0f, 2f)] private float densityScale = 1f;

    public float DensityScale => densityScale;

    private void OnEnable() => AtmosphereConditions.SceneFogScale = densityScale;

    private void OnValidate()
    {
        if (isActiveAndEnabled && Application.isPlaying)
        {
            AtmosphereConditions.SceneFogScale = densityScale;
        }
    }

    private void OnDisable() => AtmosphereConditions.SceneFogScale = 1f;
}
