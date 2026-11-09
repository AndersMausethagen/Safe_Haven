using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How big a model is on screen in the game's own view, for its triangle budget and where its levels of detail switch.
/// Best practice measures triangles by their size on screen, not by a fixed count: GPUs shade pixels in 2x2 quads, so
/// triangles smaller than about 10x10 pixels waste work (a one-pixel triangle shades four), and a level of detail should
/// take over before the triangles get that small. Safe Haven's numbers, measured in Play on 2026-11-09:
/// <list type="bullet">
/// <item>the top-down camera at its default zoom sits 20 m from the player at a 48 degree pitch with a 40 degree field
/// of view: 147 pixels a metre at 2160p (73 at 1080p); zoom runs 14-28 m, the action camera 2.5-7 m;</item>
/// <item>QualitySettings.lodBias is 2 on PC;</item>
/// <item>the Haven's scanned rocks (BoulderNode, FallenLogNode) draw 32-50 pixels per visible triangle at 2160p
/// there.</item>
/// </list>
/// The target is 40 pixels per visible triangle at 2160p (10 at 1080p, the quad line); half a model's triangles face
/// away.
/// <para>Nothing in the top-down view shows a lower level (the user, 2026-11-09: "nothing in the top down camera view
/// should have a lower LOD"; top-down practice agrees: with a fixed or narrow zoom, LODs inside the view only pop and
/// show low-poly versions, so make the one mesh efficient and rely on culling and mipmaps). So LOD0 is sized for the
/// top-down view (<see cref="ViewTriangles"/>, also in Tools/Blender/ingest_generated.py) and stays until an object is
/// farther than the farthest ground at full zoom-out; the lighter levels serve the action camera's far horizon.</para>
/// </summary>
public static class ScreenBudget
{
    public const float PixelsPerMetre = 147f;
    public const float ScreenHeight = 2160f;
    public const float LodBias = 2f;
    public const float PitchDegrees = 48f;
    public const float PixelsPerVisibleTriangle = 40f;
    /// <summary>The share of its projected bounding box a rock or bush covers (measured ~0.6).</summary>
    public const float Fill = 0.6f;
    /// <summary>The farthest ground the top-down view shows (the top of the screen at full zoom-out, 28 m: ~46 m away
    /// against 20 m at the default) is at this share of the default view's size, less a margin: LOD0 holds down to it.</summary>
    public const float FarthestTopDownShare = 0.38f;
    /// <summary>The last level culls at this share of the default size (~130 m away): only the action camera, whose
    /// 12 degree pitch looks out to the horizon, sees that far.</summary>
    public const float CullShareOfDefault = 0.15f;
    /// <summary>The fewest triangles an automatically decimated generated model keeps: below ~1,200 they lose their
    /// shape (small rocks went spiky and mushroom caps turned to shards at 150-350; artist-made low poly can go lower).
    /// For the handful of small props that costs a few thousand triangles in all.</summary>
    public const int SilhouetteFloor = 1200;

    /// <summary>LOD0 triangles for a model of these bounds: what the default top-down view can show at the target size,
    /// between the silhouette floor and <paramref name="cap"/>.</summary>
    public static int ViewTriangles(Bounds bounds, int cap) =>
        Mathf.Clamp(Mathf.RoundToInt(DefaultArea(bounds) / (PixelsPerVisibleTriangle * 0.5f)), SilhouetteFloor, Mathf.Max(SilhouetteFloor, cap));

    /// <summary>The relative height at the farthest ground the top-down view shows.</summary>
    public static float FarthestTopDownRelativeHeight(Bounds bounds) => DefaultRelativeHeight(bounds) * FarthestTopDownShare;

    /// <summary>The LODGroup's relative screen height (lodBias applied, as Unity compares it) at the default view.</summary>
    public static float DefaultRelativeHeight(Bounds bounds) =>
        Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) * PixelsPerMetre / ScreenHeight * LodBias;

    /// <summary>Pixels the model covers at the default view, from its bounds seen at the camera's pitch.</summary>
    public static float DefaultArea(Bounds bounds)
    {
        float pitch = PitchDegrees * Mathf.Deg2Rad;
        float across = Mathf.Max(bounds.size.x, bounds.size.z);
        float deep = Mathf.Min(bounds.size.x, bounds.size.z);
        return Fill * PixelsPerMetre * PixelsPerMetre * across * (bounds.size.y * Mathf.Cos(pitch) + deep * Mathf.Sin(pitch));
    }

    /// <summary>Pixels per visible triangle for a level of <paramref name="triangles"/> shown at relative height
    /// <paramref name="relativeHeight"/> (area grows with the square of the size on screen).</summary>
    public static float PixelsPerTriangle(Bounds bounds, int triangles, float relativeHeight)
    {
        float scale = relativeHeight / DefaultRelativeHeight(bounds);
        return DefaultArea(bounds) * scale * scale / Mathf.Max(1f, triangles * 0.5f);
    }

    /// <summary>The relative height below which a level of <paramref name="triangles"/> would draw triangles smaller
    /// than the target: where it should hand over to the next.</summary>
    public static float SwitchHeight(Bounds bounds, int triangles) =>
        DefaultRelativeHeight(bounds) * Mathf.Sqrt(PixelsPerVisibleTriangle * 0.5f * triangles / DefaultArea(bounds));

    /// <summary>LODGroup transition heights: LOD0 down to the farthest ground of the top-down view, the lighter levels
    /// between it and the cull (beyond the top-down view: the action camera's horizon), always decreasing.</summary>
    public static float[] LodHeights(Bounds bounds, IReadOnlyList<int> triangles, bool foliage = false)
    {
        int count = triangles.Count;
        var heights = new float[count];
        float first = FarthestTopDownRelativeHeight(bounds);
        float cull = Mathf.Clamp(DefaultRelativeHeight(bounds) * CullShareOfDefault, 0.002f, first * 0.5f);
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 1f : i / (float)(count - 1);
            heights[i] = Mathf.Clamp(Mathf.Lerp(first, cull, t), 0.001f, 0.95f);
        }

        return heights;
    }
}
