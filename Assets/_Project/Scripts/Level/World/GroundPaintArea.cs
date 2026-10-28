using UnityEngine;

/// <summary>
/// A closed loop of ground painted by a set of Terrain Paint Rules: the same rules that auto-paint a whole terrain
/// (steepness, height, hollows, patches), applied only inside the loop and fading in over Edge Fade with a frayed edge.
/// Ready-made looks: GameObject › Safe Haven › World › Ground Paint (Haven Meadow / Haven Old-Growth / Raid Dark
/// Forest). Paths, rivers and cliffs paint over it (they build later); a forest area grows on top of it.
/// </summary>
public sealed class GroundPaintArea : WorldFeature
{
    [Tooltip("What the ground becomes: Data/World › Paint Rules - Haven Meadow, Old-Growth Forest, Dark Forest, or your own.")]
    [SerializeField] private TerrainPaintRules rules;
    [Tooltip("Metres over which the paint fades in from the loop's edge (frayed by noise).")]
    [SerializeField, Min(0f)] private float edgeFade = 8f;
    [Tooltip("How strongly it paints in the middle (0-1).")]
    [SerializeField, Range(0f, 1f)] private float strength = 1f;

    public TerrainPaintRules Rules => rules;
    public float EdgeFade => edgeFade;
    public float Strength => strength;
    public override float Reach => 0f;
    public override bool IsArea => true;
    protected override int DefaultOrder => 5;

    /// <summary>Sets it up (menus and editor tools).</summary>
    public void Configure(TerrainPaintRules paintRules, float fade = 8f, float paintStrength = 1f)
    {
        rules = paintRules;
        edgeFade = Mathf.Max(0f, fade);
        strength = Mathf.Clamp01(paintStrength);
    }
}
