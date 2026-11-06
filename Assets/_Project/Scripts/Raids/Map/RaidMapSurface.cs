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

    // Layer 0 forest floor (moss), 1 path soil, 2 cliff rock, 3 litter (when the map has it). Rock follows the slope; soil
    // follows the paths and clearings and gives way to moss on wet ground; litter lies in patches under the densest canopy,
    // where it is dry; moss covers the rest.
    public static void Paint(TerrainData data, RaidMapDefinition definition, Func<Vector2, float> wetness = null)
    {
        int resolution = data.alphamapResolution;
        int layers = data.alphamapLayers;
        bool hasLitter = definition.LitterLayer != null && layers >= 4;
        var weights = new float[resolution, resolution, layers];
        Vector2 cliff = definition.CliffSlope;
        float size = definition.Size;

        // Soil first, on its own, so its border can be feathered: the zones step from forest to path within one cell.
        var soils = new float[resolution, resolution];
        var wets = new float[resolution, resolution];
        for (int row = 0; row < resolution; row++)
        {
            for (int column = 0; column < resolution; column++)
            {
                float u = (column + 0.5f) / resolution;
                float v = (row + 0.5f) / resolution;
                float path = definition.Strength(RaidZoneRole.Path, u, v);
                float forest = definition.Strength(RaidZoneRole.Forest, u, v);
                float clearing = definition.Strength(RaidZoneRole.Clearing, u, v);
                float wet = wetness != null ? wetness(new Vector2(u * size, v * size)) : 0f;
                float soil = Mathf.Clamp01(Mathf.Max(path, clearing * 0.55f, (1f - forest) * 0.35f));
                soils[row, column] = soil * (1f - 0.6f * wet * (1f - path));
                wets[row, column] = wet;
            }
        }

        Feather(soils, Mathf.Max(1, Mathf.RoundToInt(SoilFeather * resolution / size)));

        for (int row = 0; row < resolution; row++)
        {
            for (int column = 0; column < resolution; column++)
            {
                float u = (column + 0.5f) / resolution;
                float v = (row + 0.5f) / resolution;
                float forest = definition.Strength(RaidZoneRole.Forest, u, v);
                float wet = wets[row, column];
                float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(cliff.x, cliff.y, data.GetSteepness(u, v)));
                float soil = soils[row, column];
                // Moss creeps over the path's feathered margin in tufts: noise frays the edge instead of a clean line.
                float ragged = Mathf.PerlinNoise(u * size / EdgeNoise + 11.3f, v * size / EdgeNoise - 5.1f);
                float fine = Mathf.PerlinNoise(u * size / FineNoise - 2.7f, v * size / FineNoise + 8.9f);
                float margin = soil * (1f - soil) * 4f;
                soil = Mathf.Clamp01(soil + ((ragged - 0.5f) * 0.6f + (fine - 0.5f) * 0.4f) * margin);

                float litter = 0f;
                if (hasLitter)
                {
                    float canopy = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.95f, forest));
                    float patch = Mathf.PerlinNoise(u * size / LitterPatch + 3.1f, v * size / LitterPatch - 7.7f) * 0.8f + ragged * 0.2f;
                    litter = definition.Litter * canopy * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.75f, patch)) * (1f - wet);
                }

                float rest = 1f - rock;
                float floor = rest * (1f - soil);
                // Leaves drift over the trails in patches, so a path is dirt, needles and leaves, never one bare strip.
                float drift = 0f;
                if (hasLitter)
                {
                    float leaves = Mathf.PerlinNoise(u * size / PathLitterPatch - 13.7f, v * size / PathLitterPatch + 4.2f) * 0.75f + fine * 0.25f;
                    drift = PathLitter * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.75f, leaves)) * (1f - wet);
                }

                weights[row, column, 2] = rock;
                weights[row, column, 1] = rest * soil * (1f - drift);
                weights[row, column, 0] = floor * (1f - litter);
                if (hasLitter)
                {
                    weights[row, column, 3] = floor * litter + rest * soil * drift;
                }
            }
        }

        data.SetAlphamaps(0, 0, weights);
    }

    // Two box blurs of the given radius (cells) along each axis: close to a gaussian, at four cheap passes.
    private static void Feather(float[,] values, int radius)
    {
        int rows = values.GetLength(0), columns = values.GetLength(1);
        var line = new float[Mathf.Max(rows, columns)];
        for (int pass = 0; pass < 2; pass++)
        {
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++) line[column] = values[row, column];
                for (int column = 0; column < columns; column++) values[row, column] = Average(line, columns, column, radius);
            }

            for (int column = 0; column < columns; column++)
            {
                for (int row = 0; row < rows; row++) line[row] = values[row, column];
                for (int row = 0; row < rows; row++) values[row, column] = Average(line, rows, row, radius);
            }
        }
    }

    private static float Average(float[] line, int count, int at, int radius)
    {
        float sum = 0f;
        int from = Mathf.Max(0, at - radius), to = Mathf.Min(count - 1, at + radius);
        for (int i = from; i <= to; i++) sum += line[i];
        return sum / (to - from + 1);
    }

    /// <summary>
    /// Places trees, then undergrowth (saplings, shrubs, clutter), then rocks and logs over the terrain. Each spot is offered
    /// to every kind of its group, weighted by how well its habitat suits the spot; trunks keep the room their kind needs.
    /// <paramref name="wetness"/> and <paramref name="inWater"/> take metres from the map's south-west corner. Returns how many trees.
    /// </summary>
    public static int Scatter(TerrainData data, RaidMapDefinition definition, List<Clearing> clearings, out int rockCount,
        Func<Vector2, float> wetness = null, Func<Vector2, bool> inWater = null, Func<Vector2, int, float> treeWeightAt = null)
    {
        var prototypes = new List<TreePrototype>();
        Kind[] trees = Kinds(definition.Trees, prototypes);
        Kind[] undergrowth = Kinds(definition.Undergrowth, prototypes);
        Kind[] rocks = Kinds(definition.Rocks, prototypes);
        data.treePrototypes = prototypes.ToArray();
        data.RefreshPrototypes();

        float size = definition.Size;
        var random = new System.Random(definition.ScatterSeed);
        var placed = new List<TreeInstance>();
        var trunks = new TrunkGrid(size);
        var weights = new float[Mathf.Max(trees.Length, Mathf.Max(undergrowth.Length, rocks.Length))];

        RaidScatterHabitat.Spot Read(Vector2 spot, float u, float v) => new RaidScatterHabitat.Spot(spot, data.GetSteepness(u, v),
            definition.Strength(RaidZoneRole.Forest, u, v), wetness != null ? wetness(spot) : 0f, Edge(definition, u, v, size),
            1f - Mathf.Clamp01(trunks.Nearest(spot, NearTreesReach) / NearTreesReach));

        // Trees: one candidate per cell of a jittered grid; the forest's density decides whether one grows, the habitats which.
        foreach (Vector2 spot in Grid(definition.TreeSpacing, size, random))
        {
            float u = spot.x / size, v = spot.y / size;
            if (definition.Strength(RaidZoneRole.Path, u, v) > 0.15f || definition.Strength(RaidZoneRole.Clearing, u, v) > 0.3f
                || data.GetSteepness(u, v) > definition.MaxTreeSlope) continue;
            if (Blocked(spot, clearings, forTrees: true) || (inWater != null && inWater(spot))) continue;
            if (random.NextDouble() > Mathf.Pow(definition.Strength(RaidZoneRole.Forest, u, v), 1.3f)) continue;

            Place(trees, Read(spot, u, v), random, weights, trunks, placed, u, v, true, treeWeightAt);
        }

        int treeCount = placed.Count;

        // Undergrowth: a finer grid, thickest where the forest is and along its edges; saplings take gaps, clutter lies by trunks.
        if (undergrowth.Length > 0)
        {
            foreach (Vector2 spot in Grid(definition.UndergrowthSpacing, size, random))
            {
                float u = spot.x / size, v = spot.y / size;
                if (definition.Strength(RaidZoneRole.Path, u, v) > 0.1f || definition.Strength(RaidZoneRole.Clearing, u, v) > 0.6f) continue;
                if (Blocked(spot, clearings, forTrees: true) || (inWater != null && inWater(spot))) continue;

                RaidScatterHabitat.Spot here = Read(spot, u, v);
                float chance = definition.UndergrowthChance * Mathf.Clamp01(0.1f + here.Forest * 0.8f + here.Edge * 0.6f);
                if (random.NextDouble() > chance) continue;

                Place(undergrowth, here, random, weights, trunks, placed, u, v, false);
            }
        }

        // Rocks and logs: a coarser grid, most likely at the foot of cliffs, now and then in open forest; which one by habitat.
        foreach (Vector2 spot in Grid(definition.RockSpacing, size, random))
        {
            float u = spot.x / size, v = spot.y / size;
            if (definition.Strength(RaidZoneRole.Path, u, v) > 0.1f || Blocked(spot, clearings, forTrees: false)) continue;
            if (inWater != null && inWater(spot)) continue;

            float slope = data.GetSteepness(u, v);
            float forest = definition.Strength(RaidZoneRole.Forest, u, v);
            float chance = slope > 16f && slope < 38f ? definition.RocksAtCliffs : forest > 0.3f ? definition.RocksInForest : 0f;
            if (random.NextDouble() > chance) continue;

            Place(rocks, Read(spot, u, v), random, weights, trunks, placed, u, v, false);
        }

        rockCount = placed.Count - treeCount;
        data.SetTreeInstances(placed.ToArray(), true);
        return treeCount;
    }

    private const float NearTreesReach = 4f;
    private const float EdgeReach = 7f;
    private const float LitterPatch = 9f;
    private const float EdgeNoise = 2.5f;
    private const float FineNoise = 0.7f;
    // Metres over which a path's soil fades into the forest floor (each of two blur passes).
    private const float SoilFeather = 0.9f;
    private const float PathLitterPatch = 3.5f;
    // The most of a trail that leaf litter covers.
    private const float PathLitter = 0.5f;
    // Nothing is placed inside a trunk, whatever its room.
    private const float TrunkClearance = 0.6f;

    private readonly struct Kind
    {
        public readonly int Prototype;
        public readonly RaidMapDefinition.Scatter Scatter;
        /// <summary>Its place in the map's list (which skips entries without a prefab here).</summary>
        public readonly int ListIndex;

        public Kind(int prototype, RaidMapDefinition.Scatter scatter, int listIndex)
        {
            Prototype = prototype;
            Scatter = scatter;
            ListIndex = listIndex;
        }

        public float Room => Scatter.habitat != null ? Scatter.habitat.room : 0f;
    }

    private static Kind[] Kinds(IReadOnlyList<RaidMapDefinition.Scatter> list, List<TreePrototype> prototypes)
    {
        var kinds = new List<Kind>();
        for (int i = 0; i < list.Count; i++)
        {
            RaidMapDefinition.Scatter scatter = list[i];
            if (scatter?.prefab == null) continue;
            kinds.Add(new Kind(prototypes.Count, scatter, i));
            prototypes.Add(new TreePrototype { prefab = scatter.prefab });
        }

        return kinds.ToArray();
    }

    // One jittered point per cell, row by row, from the map's own random stream.
    private static IEnumerable<Vector2> Grid(float cell, float size, System.Random random)
    {
        int cells = Mathf.CeilToInt(size / cell);
        for (int gz = 0; gz < cells; gz++)
        {
            for (int gx = 0; gx < cells; gx++)
            {
                var spot = new Vector2((gx + (float)random.NextDouble()) * cell, (gz + (float)random.NextDouble()) * cell);
                if (spot.x < size && spot.y < size)
                {
                    yield return spot;
                }
            }
        }
    }

    // Picks a kind by weight × suitability among those with room to stand here, and places it at its own random size.
    // A tree's weight can come from where it stands (a large map's regions each favour their own trees), by its place in the map's list.
    private static void Place(Kind[] kinds, in RaidScatterHabitat.Spot spot, System.Random random, float[] weights, TrunkGrid trunks,
        List<TreeInstance> placed, float u, float v, bool trunk, Func<Vector2, int, float> weightAt = null)
    {
        float total = 0f;
        for (int i = 0; i < kinds.Length; i++)
        {
            RaidMapDefinition.Scatter scatter = kinds[i].Scatter;
            float room = kinds[i].Room * (scatter.scale.x + scatter.scale.y) * 0.5f;
            bool fits = trunks.Fits(spot.Position, Mathf.Max(room, TrunkClearance));
            float weight = weightAt != null && scatter.weight > 0f ? weightAt(spot.Position, kinds[i].ListIndex) : scatter.weight;
            weights[i] = fits && weight > 0f ? weight * (scatter.habitat != null ? scatter.habitat.Suitability(spot, kinds[i].Prototype) : 1f) : 0f;
            total += weights[i];
        }

        if (total <= 0f)
        {
            return;
        }

        double roll = random.NextDouble() * total;
        int chosen = kinds.Length - 1;
        for (int i = 0; i < kinds.Length; i++)
        {
            roll -= weights[i];
            if (roll < 0d)
            {
                chosen = i;
                break;
            }
        }

        TreeInstance instance = MakeInstance(kinds[chosen].Prototype, u, v, kinds[chosen].Scatter.scale, random);
        placed.Add(instance);
        float standing = kinds[chosen].Room * instance.widthScale;
        if (trunk || standing > 0f)
        {
            trunks.Add(spot.Position, Mathf.Max(standing, TrunkClearance));
        }
    }

    // How close the forest's edge is: open ground (trail or clearing) within EdgeReach of a spot that is not open itself.
    private static float Edge(RaidMapDefinition definition, float u, float v, float size)
    {
        float Open(float x, float y) => Mathf.Max(definition.Strength(RaidZoneRole.Path, x, y), definition.Strength(RaidZoneRole.Clearing, x, y));
        float here = Open(u, v);
        float around = 0f;
        float reach = EdgeReach / size;
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3f;
            around = Mathf.Max(around, Open(u + Mathf.Cos(angle) * reach, v + Mathf.Sin(angle) * reach));
        }

        return Mathf.Clamp01(around - here * 0.5f);
    }

    /// <summary>Trunks placed so far, in buckets, so a new one can keep the room both of them need.</summary>
    private sealed class TrunkGrid
    {
        private const float Bucket = 4f;
        private readonly int cells;
        private readonly List<Vector3>[] buckets;

        public TrunkGrid(float size)
        {
            cells = Mathf.Max(1, Mathf.CeilToInt(size / Bucket));
            buckets = new List<Vector3>[cells * cells];
        }

        public void Add(Vector2 spot, float room)
        {
            int index = Index(spot);
            (buckets[index] ??= new List<Vector3>()).Add(new Vector3(spot.x, spot.y, room));
        }

        /// <summary>True when every trunk is at least the mean of both rooms away.</summary>
        public bool Fits(Vector2 spot, float room)
        {
            bool fits = true;
            Visit(spot, Mathf.Max(room, 6f), (trunk, distance) => fits &= distance >= (room + trunk.z) * 0.5f);
            return fits;
        }

        /// <summary>Metres to the nearest trunk, or <paramref name="reach"/> when none is closer.</summary>
        public float Nearest(Vector2 spot, float reach)
        {
            float nearest = reach;
            Visit(spot, reach, (trunk, distance) => nearest = Mathf.Min(nearest, distance));
            return nearest;
        }

        private void Visit(Vector2 spot, float reach, Action<Vector3, float> each)
        {
            int span = Mathf.CeilToInt(reach / Bucket);
            int cx = Mathf.Clamp((int)(spot.x / Bucket), 0, cells - 1), cz = Mathf.Clamp((int)(spot.y / Bucket), 0, cells - 1);
            for (int z = Mathf.Max(0, cz - span); z <= Mathf.Min(cells - 1, cz + span); z++)
            {
                for (int x = Mathf.Max(0, cx - span); x <= Mathf.Min(cells - 1, cx + span); x++)
                {
                    List<Vector3> bucket = buckets[z * cells + x];
                    if (bucket == null) continue;
                    foreach (Vector3 trunk in bucket)
                    {
                        each(trunk, Vector2.Distance(spot, new Vector2(trunk.x, trunk.y)));
                    }
                }
            }
        }

        private int Index(Vector2 spot) =>
            Mathf.Clamp((int)(spot.y / Bucket), 0, cells - 1) * cells + Mathf.Clamp((int)(spot.x / Bucket), 0, cells - 1);
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

    // The generator only holds the spawn groups: moving it would lift every point inside, after they were put on the ground.
    private static bool IsFolder(Transform node) => node.childCount > 0
        && (node.GetComponents<Component>().Length == 1 || node.TryGetComponent(out RaidGenerator _));

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
