using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Deep water as a NavMesh area of its own ("Water", Project Settings › Navigation › Areas), so creatures that don't swim keep to land
/// and the shallows (the user's playtest, 2026-11-21: monsters walked along the bed under the water; "Most monsters should avoid
/// water, unless they are aquatic and can swim or dive"). Each open WaterSurface is sampled on a grid; where the surface stands more
/// than <see cref="Depth"/> over the terrain under it, the navmesh is marked Water by modifier boxes merged along rows. Shallow water
/// (a fen's wide sheet, a ford) stays walkable. Creatures leave the area out of their mask unless MonsterDefinition › Swims.
/// </summary>
public static class DeepWater
{
    public const string AreaName = "Water";
    /// <summary>Metres of water over the ground from which it counts as deep: a creature would be under it to the chest.</summary>
    public const float Depth = 0.7f;
    private const float Cell = 2f;
    private const float Above = 0.25f;

    /// <summary>The Water area's index, or -1 when the project has none.</summary>
    public static int Area => NavMesh.GetAreaFromName(AreaName);

    /// <summary><paramref name="mask"/> without deep water, for a creature that doesn't swim; unchanged for one that does.</summary>
    public static int MaskFor(int mask, bool swims)
    {
        int area = Area;
        return swims || area < 0 ? mask : mask & ~(1 << area);
    }

    /// <summary>Adds a modifier box for each run of deep water inside <paramref name="within"/> to <paramref name="sources"/>; returns how many.</summary>
    public static int AddSources(List<NavMeshBuildSource> sources, Bounds within)
    {
        int area = Area;
        if (area < 0)
        {
            return 0;
        }

        int added = 0;
        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water == null || water.IsFrozen || !water.isActiveAndEnabled || !water.TryGetComponent(out Renderer shown))
            {
                continue;
            }

            Bounds bounds = shown.bounds;
            if (!bounds.Intersects(within))
            {
                continue;
            }

            added += AddRuns(sources, water, bounds, within, area);
        }

        return added;
    }

    // Row by row across the water's bounds: each unbroken run of deep cells becomes one box, from under the deepest bed in it to just
    // over the surface.
    private static int AddRuns(List<NavMeshBuildSource> sources, WaterSurface water, Bounds bounds, Bounds within, int area)
    {
        float minX = Mathf.Max(bounds.min.x, within.min.x), maxX = Mathf.Min(bounds.max.x, within.max.x);
        float minZ = Mathf.Max(bounds.min.z, within.min.z), maxZ = Mathf.Min(bounds.max.z, within.max.z);
        float probeY = bounds.max.y;
        float reach = bounds.size.y + 1f;
        int added = 0;
        for (float z = minZ + Cell * 0.5f; z < maxZ; z += Cell)
        {
            float runStart = 0f, low = float.MaxValue, high = float.MinValue;
            bool inRun = false;
            for (float x = minX + Cell * 0.5f; ; x += Cell)
            {
                bool deep = false;
                float bed = 0f, surface = 0f;
                if (x < maxX && water.Contains(new Vector3(x, probeY, z), reach, out surface))
                {
                    bed = GroundAt(new Vector3(x, surface, z));
                    deep = !float.IsNegativeInfinity(bed) && surface - bed > Depth;
                }

                if (deep)
                {
                    if (!inRun)
                    {
                        inRun = true;
                        runStart = x - Cell * 0.5f;
                        low = float.MaxValue;
                        high = float.MinValue;
                    }

                    low = Mathf.Min(low, bed);
                    high = Mathf.Max(high, surface);
                }
                else if (inRun)
                {
                    inRun = false;
                    float runEnd = x - Cell * 0.5f;
                    float bottom = low - 1f, top = high + Above;
                    sources.Add(new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.ModifierBox,
                        transform = Matrix4x4.Translate(new Vector3((runStart + runEnd) * 0.5f, (bottom + top) * 0.5f, z)),
                        size = new Vector3(runEnd - runStart, top - bottom, Cell),
                        area = area,
                    });
                    added++;
                }

                if (x >= maxX)
                {
                    break;
                }
            }
        }

        return added;
    }

    // The terrain under a point (raids and the Haven lie on one), or -infinity off every terrain.
    private static float GroundAt(Vector3 point)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                continue;
            }

            Vector3 corner = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (point.x >= corner.x && point.x <= corner.x + size.x && point.z >= corner.z && point.z <= corner.z + size.z)
            {
                return terrain.SampleHeight(point) + corner.y;
            }
        }

        return float.NegativeInfinity;
    }
}
