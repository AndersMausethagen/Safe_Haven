using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds a <see cref="BiomeArea"/>: for each layer, candidate points on a jittered grid at the layer's
/// density, kept only where its rules allow (inside the area and away from its edge, steepness, height, ground layer,
/// distance from water, paths and cliffs, clumps), with room around existing objects and the palette's spacing.</summary>
public sealed class AreaBuilder : FeatureBuilder
{
    private readonly BiomeArea area;

    public AreaBuilder(BiomeArea area) : base(area)
    {
        this.area = area;
    }

    public override void Prepare(WorldSampler ground)
    {
        Line = SplineLine.Sample(Feature.Container, 1f);
        Random = new System.Random(Feature.Seed);
    }

    /// <summary>Chance (0-1) a candidate survives the clumping noise: 1 everywhere at clumping 0, only inside clumps at 1.</summary>
    public static float ClumpChance(float noise, float clumping) =>
        Mathf.Lerp(1f, WorldNoise2D.Smooth((noise - 0.42f) / 0.16f), clumping);

    public override void Objects(Transform root, BuildContext context)
    {
        if (Line.Count < 3)
        {
            return;
        }

        Rect bounds = Line.FlatBounds(0f);
        Physics.SyncTransforms();
        var hits = new Collider[16];
        for (int layerIndex = 0; layerIndex < area.Layers.Count; layerIndex++)
        {
            BiomeArea.Layer layer = area.Layers[layerIndex];
            if (layer.palette == null || layer.density <= 0f)
            {
                continue;
            }

            float cell = Mathf.Sqrt(100f / layer.density);
            float radius = layer.palette.spacing * 0.5f;
            int seed = Feature.Seed + layerIndex * 101;
            for (float z = bounds.yMin; z < bounds.yMax; z += cell)
            {
                for (float x = bounds.xMin; x < bounds.xMax; x += cell)
                {
                    var xz = new Vector2(x + (float)Random.NextDouble() * cell, z + (float)Random.NextDouble() * cell);
                    float roll = (float)Random.NextDouble();
                    ScatterPalette.Entry entry = layer.palette.Pick(Random);
                    if (entry == null || !Line.Inside(xz))
                    {
                        continue;
                    }

                    float chance = layer.edgeFade > 0f ? WorldNoise2D.Smooth(Line.Nearest(xz, out _, out _) / layer.edgeFade) : 1f;
                    chance *= ClumpChance(WorldNoise2D.Fbm(xz.x / layer.clumpSize, xz.y / layer.clumpSize, seed), layer.clumping);
                    if (roll > chance || !context.Spacing.IsFree(xz, radius) ||
                        !WorldGround.TryGround(xz, out Vector3 ground, out Vector3 normal) || !Allowed(layer, xz, ground, normal, context))
                    {
                        continue;
                    }

                    if (area.RoomAroundObjects > 0f && Blocked(ground, area.RoomAroundObjects + radius * 0.5f, root, hits))
                    {
                        continue;
                    }

                    WorldScatter.Place(entry, ground, normal, root, Random);
                    context.Spacing.Add(xz, radius);
                }
            }
        }
    }

    private static bool Allowed(BiomeArea.Layer layer, Vector2 xz, Vector3 ground, Vector3 normal, BuildContext context)
    {
        float slope = WorldGround.Slope(normal);
        if (slope < layer.slope.x || slope > layer.slope.y)
        {
            return false;
        }

        if (layer.limitHeight && (ground.y < layer.height.x || ground.y > layer.height.y))
        {
            return false;
        }

        if (layer.onGround != null && layer.onGround.Length > 0 && context.GroundWeight(xz, layer.onGround) < layer.groundAmount)
        {
            return false;
        }

        float water = context.DistanceTo<RiverBuilder>(xz);
        if (WaterSurface.TryFind(ground + Vector3.up * 0.05f, 0.2f, out _))
        {
            water = 0f;
        }

        if (layer.awayFromWater >= 0f ? water < layer.awayFromWater : water > -layer.awayFromWater)
        {
            return false;
        }

        return context.DistanceTo<PathBuilder>(xz) >= layer.awayFromPaths && context.DistanceTo<CliffBuilder>(xz) >= layer.awayFromCliffs;
    }

    /// <summary>Whether something with a collider (not the terrain or this area's own objects) stands within the radius.</summary>
    private static bool Blocked(Vector3 ground, float radius, Transform root, Collider[] hits)
    {
        int count = Physics.OverlapCapsuleNonAlloc(ground + Vector3.up * (radius + 0.3f), ground + Vector3.up * 2.5f, radius,
                                                   hits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            if (!(hits[i] is TerrainCollider) && !hits[i].transform.IsChildOf(root))
            {
                return true;
            }
        }

        return false;
    }
}
