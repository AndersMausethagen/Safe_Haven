using System;
using UnityEngine;

/// <summary>
/// How a raid's ground light field looks (RaidDefinition › Look; GroundLightMap builds it while a generated map loads;
/// Docs/LightingBakeResearch_2026-11-17.md §3): the land's shade in hollows and under walls, and the light sunlit ground throws
/// into the shade, in the raid's own colour. Off (the default) keeps the raid as it looked before the field. How much the canopy
/// shades stays the hour's Sky Cover (RaidTimeOfDay).
/// </summary>
[Serializable]
public sealed class RaidGroundLight
{
    [Tooltip("Work this raid's ground light out while its map loads. On, the canopy also shades the terrain, the water and the " +
             "mist (not only what stands on the ground), and Relief and Bounce below apply. Off: as before the field.")]
    [SerializeField] private bool enabled;
    [Tooltip("How much hollows, ravines, cliff feet and valley walls darken the ground they hide the sky from (0 none, 1 fully). " +
             "At night and at a dim dawn or dusk the ground's average light is kept as before the field (Rendering Settings › Night " +
             "Lift Most, Lift Below Sun), so nights don't get darker.")]
    [SerializeField, Range(0f, 1f)] private float relief;
    [Tooltip("How strongly sunlit ground nearby lights what stands in shade (0 none). Follows the sun or moon as it is, so clouds " +
             "and night dim it by themselves.")]
    [SerializeField, Range(0f, 4f)] private float bounce;
    [Tooltip("The colour that thrown-back light takes on over the ground's own (white: the ground's colour as it is).")]
    [SerializeField] private Color bounceTint = Color.white;
    [Tooltip("Sunflecks: spots of sun through gaps in the canopy on the shaded ground under the trees, drifting a little as the leaves " +
             "move (Docs/LookAndFeelResearch_2026-11-18.md L3), as a share of the sun's light. Under a dense crown the sun's shadow is one " +
             "even shade (tree shadows come from a lighter level of the trees). 0: none.")]
    [SerializeField, Range(0f, 1.5f)] private float sunflecks;

    public bool Enabled => enabled;
    public float Relief => relief;
    public float Bounce => bounce;
    public Color BounceTint => bounceTint;
    public float Sunflecks => sunflecks;

    public RaidGroundLight() { }

    public RaidGroundLight(bool enabled, float relief, float bounce, Color bounceTint, float sunflecks = 0f)
    {
        this.enabled = enabled;
        this.relief = relief;
        this.bounce = bounce;
        this.bounceTint = bounceTint;
        this.sunflecks = sunflecks;
    }
}
