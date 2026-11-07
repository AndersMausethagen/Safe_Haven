using System.Linq;
using UnityEngine;

/// <summary>
/// The ground's shape: the woods' and the mountain's weight at a point, the ground under them, and the height in metres
/// above the portal's ground, with water set level into it. Part of HavenWorldPlan, in its own file.
/// </summary>
public sealed partial class HavenWorldPlan
{
    private const float LakeBank = 25f;

    private const float LakeSlope = 18f;

    private const float PondBank = 4f;

    /// <summary>0 in the Haven's ring, 1 in the Misty Woods, with a wandering border.</summary>
    public float WoodsWeight(Vector2 point)
    {
        float wander = (Perlin(point, 70f, 41f) - 0.5f) * 60f;
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.haven, Recipe.forest, point.magnitude + wander));
    }

    /// <summary>How much of the mountain stands at a point, 0 to 1.</summary>
    public float MountainWeight(Vector2 point)
    {
        float half = Recipe.mountainAt.y * 0.5f;
        float off = Mathf.Abs(Mathf.DeltaAngle(BearingOf(point), Recipe.mountainAt.x));
        float across = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(half * 0.55f, half, off));
        float along = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.mountainFrom, Recipe.mountainFrom + 170f, point.magnitude));
        return across * along;
    }

    /// <summary>The ground before the lake and ponds are dug, in metres above the portal's ground.</summary>
    public float Ground(Vector2 point)
    {
        float r = point.magnitude;
        float far = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.buildable, Half, r));
        float hills = (Perlin(point, Recipe.hillScale, 0f) - 0.5f) * 2f * Mathf.Lerp(Recipe.havenHills, Recipe.outerHills, far);
        // Rounded crests (a parabola, not the V of ridged noise) on a wide ramp: ridges you see in the light and walk over, whose
        // sides stay under the trees' 36 degrees. Sharp ones left a sixth of the woods bare, pale ribbons that read as paths.
        float fold = Perlin(point, Recipe.hillScale * 0.6f, 97f) * 2f - 1f;
        float ridged = 1f - fold * fold;
        float ridge = Recipe.ridges * WoodsWeight(point) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 1f, ridged));
        // Gentle and open where the Haven is built: the ward reaches about 50 m.
        float open = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(Recipe.buildable, Recipe.buildable + 70f, r));
        float near = (Perlin(point, 25f, 13f) - 0.5f) * 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 40f, r));
        float height = (hills + ridge) * open + near * (1f - open);
        height += Recipe.mountainHeight * MountainWeight(point) * (0.7f + 0.3f * ridged);
        // The edge of the world rises into a broken, rocky ridge (steep enough for the terrain's rock), not a smooth wall.
        float edge = Half - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.y));
        float rim = Mathf.Clamp01(1f - edge / Recipe.rim.y);
        float crest = 1f - Mathf.Abs(Perlin(point, 38f, 211f) * 2f - 1f);
        float rocky = Mathf.Lerp(0.55f, 1.35f, crest) + (Perlin(point, 11f, 223f) - 0.5f) * 0.3f;
        return height + Recipe.rim.x * Mathf.Pow(rim, 1.6f) * rocky;
    }

    /// <summary>The ground with the lake, its islands and the ponds dug, in metres above the portal's ground.</summary>
    public float Height(Vector2 point)
    {
        float ground = Ground(point);
        if (Lake != null && (point - Lake.Position).sqrMagnitude < (Lake.Reach + LakeBank) * (Lake.Reach + LakeBank))
        {
            float edge = Lake.Edge(point);
            if (edge < LakeBank)
            {
                ground = edge >= 0f
                    ? Mathf.Lerp(Lake.Level, ground, Mathf.SmoothStep(0f, 1f, edge / LakeBank))
                    : Lake.Level - Lake.Depth * Mathf.SmoothStep(0f, 1f, -edge / LakeSlope);
                foreach (Island island in Islands)
                {
                    float d = Vector2.Distance(island.Centre, point);
                    if (d < island.Radius * 1.6f)
                    {
                        float dome = Mathf.SmoothStep(0f, 1f, 1f - d / (island.Radius * 1.6f));
                        ground = Mathf.Max(ground, Mathf.Lerp(Lake.Level - Lake.Depth, Lake.Level + island.Height, dome));
                    }
                }
            }
        }

        ground = RiverValley(point, ground);
        foreach (RaidLayoutPond pond in Ponds)
        {
            if ((point - pond.Position).sqrMagnitude >= (pond.Reach + PondBank) * (pond.Reach + PondBank))
            {
                continue;
            }

            float edge = pond.Edge(point);
            if (edge >= PondBank)
            {
                continue;
            }

            ground = edge >= 0f
                ? Mathf.Lerp(pond.Level, ground, Mathf.SmoothStep(0f, 1f, edge / PondBank))
                : pond.Level - pond.Depth * Mathf.SmoothStep(0f, 1f, -edge / Mathf.Min(3f, Mathf.Min(pond.Radii.x, pond.Radii.y) * 0.8f));
        }

        return ground;
    }

    // The water lies just under the lowest ground around it, so it never stands above its own banks.
    private float LevelAround(RaidLayoutPond water, float beyond, float below)
    {
        float lowest = float.MaxValue;
        for (int i = 0; i < 32; i++)
        {
            lowest = Mathf.Min(lowest, Ground(water.Rim(i * Mathf.PI * 2f / 32f, beyond)));
        }

        return lowest - below;
    }
}
