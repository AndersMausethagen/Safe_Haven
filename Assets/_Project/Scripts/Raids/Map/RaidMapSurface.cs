using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a map's zones become what the player walks on: terrain paint, trees, rocks and logs, the invisible trunks the navmesh
/// cuts around, and the points of a site. Shared by the editor's Bake (RaidMapBaker) and by maps generated while a raid
/// loads (RaidMapGenerator), so a generated forest is made exactly like a painted one.
/// </summary>
public static class RaidMapSurface
{
    public const string TrunksName = "Tree Trunks";

    /// <summary>A spot (metres from the map's south-west corner) that trees and rocks keep away from, each by its own distance.</summary>
    public readonly struct Clearing
    {
        public readonly Vector2 Spot;
        public readonly float TreeRadius;
        public readonly float RockRadius;

        public Clearing(Vector2 spot, float treeRadius, float rockRadius)
        {
            Spot = spot;
            TreeRadius = treeRadius;
            RockRadius = rockRadius;
        }
    }

    // Layer 0 forest floor (moss), 1 path soil, 2 cliff rock. Rock follows the slope; soil follows the paths and
    // clearings; moss covers the rest, thickest where the forest is densest.
    public static void Paint(TerrainData data, RaidMapDefinition definition)
    {
        int resolution = data.alphamapResolution;
        var weights = new float[resolution, resolution, 3];
        Vector2 cliff = definition.CliffSlope;
        for (int row = 0; row < resolution; row++)
        {
            for (int column = 0; column < resolution; column++)
            {
                float u = (column + 0.5f) / resolution;
                float v = (row + 0.5f) / resolution;
                float path = definition.Strength(RaidZoneRole.Path, u, v);
                float forest = definition.Strength(RaidZoneRole.Forest, u, v);
                float clearing = definition.Strength(RaidZoneRole.Clearing, u, v);
                float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(cliff.x, cliff.y, data.GetSteepness(u, v)));
                float soil = Mathf.Clamp01(Mathf.Max(path, clearing * 0.55f, (1f - forest) * 0.35f));
                float rest = 1f - rock;
                weights[row, column, 2] = rock;
                weights[row, column, 1] = rest * soil;
                weights[row, column, 0] = rest * (1f - soil);
            }
        }

        data.SetAlphamaps(0, 0, weights);
    }

    /// <summary>Places trees, then rocks and logs, over the terrain. Returns how many trees.</summary>
    public static int Scatter(TerrainData data, RaidMapDefinition definition, List<Clearing> clearings, out int rockCount)
    {
        var prototypes = new List<TreePrototype>();
        var treeWeights = new List<float>();
        var rockWeights = new List<float>();
        var scales = new List<Vector2>();
        foreach (RaidMapDefinition.Scatter scatter in definition.Trees)
        {
            if (scatter?.prefab == null) continue;
            prototypes.Add(new TreePrototype { prefab = scatter.prefab });
            treeWeights.Add(scatter.weight);
            rockWeights.Add(0f);
            scales.Add(scatter.scale);
        }

        foreach (RaidMapDefinition.Scatter scatter in definition.Rocks)
        {
            if (scatter?.prefab == null) continue;
            prototypes.Add(new TreePrototype { prefab = scatter.prefab });
            treeWeights.Add(0f);
            rockWeights.Add(scatter.weight);
            scales.Add(scatter.scale);
        }

        data.treePrototypes = prototypes.ToArray();
        data.RefreshPrototypes();

        float size = definition.Size;
        var random = new System.Random(definition.ScatterSeed);
        var placed = new List<TreeInstance>();

        // Trees: one candidate per cell of a jittered grid; the forest's density decides whether it grows.
        float cell = definition.TreeSpacing;
        int cells = Mathf.CeilToInt(size / cell);
        for (int gz = 0; gz < cells; gz++)
        {
            for (int gx = 0; gx < cells; gx++)
            {
                var spot = new Vector2((gx + (float)random.NextDouble()) * cell, (gz + (float)random.NextDouble()) * cell);
                if (spot.x >= size || spot.y >= size) continue;

                float u = spot.x / size;
                float v = spot.y / size;
                if (definition.Strength(RaidZoneRole.Path, u, v) > 0.15f || definition.Strength(RaidZoneRole.Clearing, u, v) > 0.3f
                    || data.GetSteepness(u, v) > definition.MaxTreeSlope) continue;
                if (Blocked(spot, clearings, forTrees: true)) continue;
                if (random.NextDouble() > Mathf.Pow(definition.Strength(RaidZoneRole.Forest, u, v), 1.3f)) continue;

                int prototype = RaidLayout.PickWeighted(random, treeWeights);
                if (prototype < 0) continue;
                placed.Add(MakeInstance(prototype, u, v, scales[prototype], random));
            }
        }

        int treeCount = placed.Count;

        // Rocks and logs: a coarser grid, most likely at the foot of cliffs, now and then in open forest.
        cell = definition.RockSpacing;
        cells = Mathf.CeilToInt(size / cell);
        for (int gz = 0; gz < cells; gz++)
        {
            for (int gx = 0; gx < cells; gx++)
            {
                var spot = new Vector2((gx + (float)random.NextDouble()) * cell, (gz + (float)random.NextDouble()) * cell);
                if (spot.x >= size || spot.y >= size) continue;

                float u = spot.x / size;
                float v = spot.y / size;
                if (definition.Strength(RaidZoneRole.Path, u, v) > 0.1f || Blocked(spot, clearings, forTrees: false)) continue;

                float slope = data.GetSteepness(u, v);
                float forest = definition.Strength(RaidZoneRole.Forest, u, v);
                float chance = slope > 16f && slope < 38f ? definition.RocksAtCliffs : forest > 0.3f ? definition.RocksInForest : 0f;
                if (random.NextDouble() > chance) continue;

                int prototype = RaidLayout.PickWeighted(random, rockWeights);
                if (prototype < 0) continue;
                placed.Add(MakeInstance(prototype, u, v, scales[prototype], random));
            }
        }

        rockCount = placed.Count - treeCount;
        data.SetTreeInstances(placed.ToArray(), true);
        return treeCount;
    }

    private static TreeInstance MakeInstance(int prototype, float u, float v, Vector2 scale, System.Random random)
    {
        float s = Mathf.Lerp(scale.x, scale.y, (float)random.NextDouble());
        float turn = (float)random.NextDouble();
        float shade = 0.82f + 0.18f * (float)random.NextDouble();
        return new TreeInstance
        {
            prototypeIndex = prototype,
            position = new Vector3(u, 0f, v),
            widthScale = s,
            heightScale = s * Mathf.Lerp(0.92f, 1.08f, (float)random.NextDouble()),
            rotation = turn * Mathf.PI * 2f,
            color = new Color(shade, shade, shade, 1f),
            lightmapColor = Color.white,
        };
    }

    private static bool Blocked(Vector2 spot, List<Clearing> clearings, bool forTrees)
    {
        foreach (Clearing clearing in clearings)
        {
            float radius = forTrees ? clearing.TreeRadius : clearing.RockRadius;
            if ((clearing.Spot - spot).sqrMagnitude < radius * radius)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// A navmesh bake ignores terrain trees, so creatures would walk through trunks. Every tree and rock with a capsule
    /// collider gets a matching prism in one mesh, for an invisible, not-walkable collider the navmesh cuts around.
    /// </summary>
    public static Mesh TrunkMesh(Terrain terrain, Vector3 holderPosition)
    {
        TerrainData data = terrain.terrainData;
        TreePrototype[] prototypes = data.treePrototypes;
        var shapes = new CapsuleCollider[prototypes.Length];
        for (int i = 0; i < prototypes.Length; i++)
        {
            shapes[i] = prototypes[i].prefab != null ? prototypes[i].prefab.GetComponent<CapsuleCollider>() : null;
        }

        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        Vector3 origin = terrain.transform.position;
        foreach (TreeInstance tree in data.treeInstances)
        {
            CapsuleCollider shape = shapes[tree.prototypeIndex];
            if (shape == null) continue;

            Vector3 foot = origin + Vector3.Scale(tree.position, data.size) - holderPosition;
            var turn = Quaternion.Euler(0f, tree.rotation * Mathf.Rad2Deg, 0f);
            float radius = shape.radius * tree.widthScale;
            if (shape.direction == 1)
            {
                AddPrism(vertices, triangles, foot, turn, radius, radius, Mathf.Min(3f, shape.height * tree.heightScale));
            }
            else
            {
                AddPrism(vertices, triangles, foot, turn, shape.height * tree.widthScale * 0.5f, radius, radius * 2f);
            }
        }

        var mesh = new Mesh { name = TrunksName, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // A box around a tree's foot (half width along its turned x and z), standing on the ground.
    private static void AddPrism(List<Vector3> vertices, List<int> triangles, Vector3 foot, Quaternion turn, float halfX, float halfZ, float height)
    {
        int first = vertices.Count;
        for (int level = 0; level < 2; level++)
        {
            float y = level == 0 ? -0.5f : height;
            vertices.Add(foot + turn * new Vector3(-halfX, y, -halfZ));
            vertices.Add(foot + turn * new Vector3(halfX, y, -halfZ));
            vertices.Add(foot + turn * new Vector3(halfX, y, halfZ));
            vertices.Add(foot + turn * new Vector3(-halfX, y, halfZ));
        }

        int[] faces = { 0, 4, 5, 0, 5, 1, 1, 5, 6, 1, 6, 2, 2, 6, 7, 2, 7, 3, 3, 7, 4, 3, 4, 0, 4, 7, 6, 4, 6, 5 };
        foreach (int index in faces)
        {
            triangles.Add(first + index);
        }
    }

    /// <summary>Where the given point of a site goes: the first on its centre, the rest in a ring around it.</summary>
    public static Vector3 SitePoint(Terrain terrain, Vector3 centre, RaidMapDefinition.SiteRule rule, int site, int point)
    {
        if (point == 0)
        {
            return centre;
        }

        float angle = (point - 1) * Mathf.PI * 2f / Mathf.Max(1, rule.pointsPerSite - 1) + site;
        return Ground(terrain, centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rule.spread);
    }

    /// <summary>Everything on the map sits on the ground: spawn points, and every object directly inside a plain folder object.</summary>
    public static void SnapToGround(Transform node, Terrain terrain, Action<Transform> beforeMoving = null)
    {
        foreach (Transform child in node)
        {
            if (child.name == TrunksName)
            {
                continue;
            }

            if (child.TryGetComponent(out SpawnGroup _))
            {
                foreach (Transform point in child)
                {
                    Snap(point, terrain, beforeMoving);
                }
            }
            else if (IsFolder(child))
            {
                SnapToGround(child, terrain, beforeMoving);
            }
            else
            {
                Snap(child, terrain, beforeMoving);
            }
        }
    }

    private static bool IsFolder(Transform node) => node.childCount > 0 && node.GetComponents<Component>().Length == 1;

    private static void Snap(Transform target, Terrain terrain, Action<Transform> beforeMoving)
    {
        Vector3 grounded = Ground(terrain, target.position);
        if (Mathf.Abs(grounded.y - target.position.y) > 0.001f)
        {
            beforeMoving?.Invoke(target);
            target.position = grounded;
        }
    }

    public static Vector3 Ground(Terrain terrain, Vector3 position)
    {
        position.y = terrain.transform.position.y + terrain.SampleHeight(position);
        return position;
    }
}
