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

    /// <summary>Adds a modifier box for each stretch of deep water inside <paramref name="within"/> to <paramref name="sources"/>; returns how many.</summary>
    public static int AddSources(List<NavMeshBuildSource> sources, Bounds within)
    {
        int area = Area;
        if (area < 0)
        {
            return 0;
        }

        List<Bounds> boxes = Boxes(within);
        foreach (Bounds box in boxes)
        {
            sources.Add(new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.ModifierBox,
                transform = Matrix4x4.Translate(box.center),
                size = box.size,
                area = area,
            });
        }

        return boxes.Count;
    }

    /// <summary>
    /// The deep water inside <paramref name="within"/> as boxes from under its bed to just over its surface: runs of deep cells along
    /// each row, and rows under rows with the same run merged into one (a pond is a few boxes, not one a row). The Haven's baked navmesh
    /// gets them as modifier volumes (Safe Haven › Navigation › Mark Deep Water), a raid's built one as sources.
    /// </summary>
    public static List<Bounds> Boxes(Bounds within)
    {
        var boxes = new List<Bounds>();
        foreach (WaterSurface water in WaterSurface.Surfaces)
        {
            if (water == null || water.IsFrozen || !water.isActiveAndEnabled || !water.TryGetComponent(out Renderer shown))
            {
                continue;
            }

            Bounds bounds = shown.bounds;
            if (bounds.Intersects(within))
            {
                AddRuns(boxes, water, bounds, within);
            }
        }

        return boxes;
    }

    // Row by row across the water's bounds: each unbroken run of deep cells is a box, merged with the box above it when that one
    // runs over the same cells.
    private static void AddRuns(List<Bounds> boxes, WaterSurface water, Bounds bounds, Bounds within)
    {
        float minX = Mathf.Max(bounds.min.x, within.min.x), maxX = Mathf.Min(bounds.max.x, within.max.x);
        float minZ = Mathf.Max(bounds.min.z, within.min.z), maxZ = Mathf.Min(bounds.max.z, within.max.z);
        float probeY = bounds.max.y;
        float reach = bounds.size.y + 1f;
        // The open boxes of the row before (by their run's ends), carried on while the next row has the same run.
        var open = new Dictionary<(int, int), int>();
        var next = new Dictionary<(int, int), int>();
        int row = 0;
        for (float z = minZ + Cell * 0.5f; z < maxZ; z += Cell, row++)
        {
            next.Clear();
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
                    var key = (Mathf.RoundToInt(runStart / Cell), Mathf.RoundToInt(runEnd / Cell));
                    var made = new Bounds(new Vector3((runStart + runEnd) * 0.5f, 0f, z), Vector3.zero);
                    made.SetMinMax(new Vector3(runStart, low - 1f, z - Cell * 0.5f), new Vector3(runEnd, high + Above, z + Cell * 0.5f));
                    if (open.TryGetValue(key, out int index))
                    {
                        Bounds grown = boxes[index];
                        grown.Encapsulate(made);
                        boxes[index] = grown;
                        next[key] = index;
                    }
                    else
                    {
                        boxes.Add(made);
                        next[key] = boxes.Count - 1;
                    }
                }

                if (x >= maxX)
                {
                    break;
                }
            }

            (open, next) = (next, open);
        }
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
