using UnityEngine;

/// <summary>
/// The shape of a slow wind-up (technical animation audit, 2026-11-20). A heavy blow fitted to its Hit Delay can play its
/// anticipation at a quarter of the clip's speed, which reads as slow motion and then a snap into the strike. Shaped, it starts
/// faster and slows into the top of the backswing, as a body gathers itself; the average is the fitted Animation Speed, so the strike
/// starts on the same clip frame at the same moment and the blow and damage per second don't move. Wind-ups at Shaped Below or
/// faster play as they are.
/// </summary>
public static class SwingWindup
{
    /// <summary>Wind-ups slower than this are shaped.</summary>
    public const float ShapedBelow = 0.6f;
    /// <summary>At or below this the shape is at its strongest.</summary>
    public const float FullShapeAt = 0.25f;
    /// <summary>The strongest shape: it starts at 1.6 times the speed and ends at 0.4 times.</summary>
    public const float MostShape = 0.6f;

    /// <summary>How strongly a wind-up at <paramref name="animationSpeed"/> is shaped (0 not at all).</summary>
    public static float Strength(float animationSpeed) =>
        Mathf.Clamp01((ShapedBelow - animationSpeed) / (ShapedBelow - FullShapeAt)) * MostShape;

    /// <summary>The playback speed <paramref name="progress"/> (0 at the wind-up's start, 1 at the strike) of the way through a wind-up
    /// whose fitted speed is <paramref name="speed"/> and whose attack's Animation Speed is <paramref name="animationSpeed"/>.</summary>
    public static float Speed(float speed, float animationSpeed, float progress) =>
        speed * (1f + Strength(animationSpeed) * (1f - 2f * Mathf.Clamp01(progress)));
}
