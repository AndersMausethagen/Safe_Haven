using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds a <see cref="BiomeArea"/>: for each layer, candidate points on a jittered grid at the layer's
/// density, kept only where its rules allow (inside the area and away from its edge, steepness, height, ground layer,
/// distance from water, paths and cliffs, clumps), with room around existing objects and the palette's spacing.
/// With a biome it also paints the biome's ground and grows its plants inside the loop (faded in from the edge, off
/// steep slopes and out of the water, the same per-cell result every build), and its props become extra layers.</summary>
public sealed class AreaBuilder : FeatureBuilder
{
    private readonly BiomeArea area;

    public AreaBuilder(BiomeArea area) : base(area)
    {
        this.area = area;
    }

    public override IEnumerable<TerrainLayer> PaintLayers
    {
        get
        {
            if (area.Biome != null && area.Biome.ground != null) yield return area.Biome.ground;
        }
    }

    public override IEnumerable<GameObject> DetailPrefabs
    {
        get
        {
            if (area.Biome == null) yield break;
            foreach (BiomePreset.Detail detail in area.Biome.details)
            {
                if (detail.prefab != null) yield return detail.prefab;
            }
        }
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
        List<BiomeArea.Layer> layers = AllLayers();
        for (int layerIndex = 0; layerIndex < layers.Count; layerIndex++)
        {
            BiomeArea.Layer layer = layers[layerIndex];
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

    /// <summary>The area's layers, then one for each of its biome's prop palettes (after them, so a forest built before
    /// the biome was added comes out the same).</summary>
    private List<BiomeArea.Layer> AllLayers()
    {
        var all = new List<BiomeArea.Layer>(area.Layers);
        BiomePreset biome = area.Biome;
        if (biome == null)
        {
            return all;
        }

        foreach (BiomePreset.Props props in biome.props)
        {
            if (props.palette == null) continue;
            all.Add(new BiomeArea.Layer
            {
                name = props.palette.name, palette = props.palette, density = props.perHundredSquareMetres * area.BiomeStrength,
                slope = props.slope, awayFromWater = biome.nearWater > 0f ? -biome.nearWater : 0.5f, awayFromPaths = 1f,
                awayFromCliffs = 1f, clumping = 0.35f, clumpSize = 12f, edgeFade = area.BiomeEdgeFade,
            });
        }

        return all;
    }

    // ------------------------------------------------------------------ biome ground and plants

    public override void Paint(PaintLayer layer)
    {
        BiomePreset biome = area.Biome;
        if (biome == null || biome.ground == null || biome.groundStrength <= 0f || Line == null || Line.Count < 3)
        {
            return;
        }

        int index = layer.LayerIndex(biome.ground);
        if (index < 0)
        {
            return;
        }

        TerrainData data = layer.Terrain.terrainData;
        Vector3 origin = layer.Terrain.transform.position;
        // A brush lays Ground Strength per dab over several dabs; the area lays it once, so twice as strong.
        float strength = Mathf.Clamp01(biome.groundStrength * 2f) * area.BiomeStrength;
        new AreaMask(Line, area.BiomeEdgeFade, Feature.Seed).ForEachInside(layer.Grid, layer.Rect, (x, y, p, fade) =>
        {
            float u = Mathf.Clamp01((p.x - origin.x) / data.size.x), v = Mathf.Clamp01((p.y - origin.z) / data.size.z);
            float slope = data.GetSteepness(u, v);
            if (slope < biome.groundSlope.x || slope > biome.groundSlope.y)
            {
                return;
            }

            if (biome.nearWater > 0f && !NearWater(new Vector3(p.x, origin.y + data.GetInterpolatedHeight(u, v), p.y), biome.nearWater))
            {
                return;
            }

            float noise = 0.65f + 0.35f * WorldNoise2D.Fbm(p.x * 0.35f, p.y * 0.35f, Feature.Seed + 7);
            layer.Paint(x, y, index, strength * fade * noise);
        });
    }

    public override void ClearDetails(ClearLayer layer)
    {
        BiomePreset biome = area.Biome;
        if (biome == null || biome.details.Count == 0 || Line == null || Line.Count < 3)
        {
            return;
        }

        Terrain terrain = layer.Terrain;
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        WaterSurface[] water = Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Exclude);
        var mask = new AreaMask(Line, area.BiomeEdgeFade, Feature.Seed);
        for (int d = 0; d < biome.details.Count; d++)
        {
            BiomePreset.Detail detail = biome.details[d];
            int prototype = layer.PrototypeIndex(detail.prefab);
            if (prototype < 0 || detail.density <= 0f)
            {
                continue;
            }

            int count = GrassSetup.MaxPerCell(detail.prefab.name);
            float offset = (detail.prefab.name.GetHashCode() & 0xffff) * 0.013f;
            int seed = Feature.Seed + 1000 + d * 31;
            mask.ForEachInside(layer.Grid, layer.Rect, (x, y, p, fade) =>
            {
                float u = Mathf.Clamp01((p.x - origin.x) / data.size.x), v = Mathf.Clamp01((p.y - origin.z) / data.size.z);
                float slope = data.GetSteepness(u, v);
                if (slope < detail.slope.x || slope > detail.slope.y)
                {
                    return;
                }

                float patch = Mathf.PerlinNoise(p.x / detail.patchSize + offset, p.y / detail.patchSize + offset);
                float grows = detail.density * area.BiomeStrength * fade * Mathf.Lerp(1f, Mathf.Clamp01(patch * 2f - 0.5f), detail.patchiness);
                // Hashed by cell, not drawn from a sequence: the same cells grow every build, however big the rebuilt area.
                if (WorldNoise2D.Hash(x, y, seed) >= grows)
                {
                    return;
                }

                var world = new Vector3(p.x, origin.y + data.GetInterpolatedHeight(u, v), p.y);
                if ((biome.nearWater > 0f && !NearWater(world, biome.nearWater)) || GrassSetup.UnderWater(terrain, world, water))
                {
                    return;
                }

                layer.Grow(x, y, prototype, count);
            });
        }
    }

    private static bool NearWater(Vector3 world, float reach)
    {
        foreach (WaterSurface surface in WaterSurface.Surfaces)
        {
            if (surface != null && surface.Contains(world, reach, out _)) return true;
        }

        return false;
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
