using System.Collections.Generic;
using System.Text;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Bakes a RaidMapDefinition's painted layout into the raid scene: terrain paint from the Path/Forest/Clearing zones and
/// the slopes, trees, rocks and logs, the player start, exits and spawn group points from the markers, candidate points
/// from the spawn zones, then the navmesh. The heights are not baked: they are sculpted on the terrain itself and kept.
/// Run it from the RaidMap inspector after painting. Baking again gives the same map; hand-painted terrain textures and
/// trees, and the points of groups with a Site or a Zone, are replaced.
/// </summary>
public static class RaidMapBaker
{
    private const string MossShaderName = "Safe Haven/Terrain Moss";
    private const string TrunksName = "Tree Trunks";
    private const int NotWalkableArea = 1;

    public static string Bake(RaidMap map)
    {
        RaidMapDefinition definition = map.Definition;
        string problem = Validate(map);
        if (problem != null)
        {
            return problem;
        }

        Undo.RecordObject(definition, "Bake raid map");
        definition.EnsureZones();

        var report = new StringBuilder($"Baked {definition.name}:");
        Terrain terrain = EnsureTerrain(map, definition);
        TerrainData data = terrain.terrainData;
        float size = definition.Size;

        BakePaint(data, definition);

        List<RaidSite> sites = definition.Sites();
        var clearings = new List<Clearing>(sites.Count);
        foreach (RaidSite site in sites)
        {
            clearings.Add(new Clearing(site.Position * size, definition.ClearAroundSites, 3f));
        }

        Dictionary<RaidMapZone, List<Vector2>> zonePoints = ScatterZones(definition, data);
        foreach (KeyValuePair<RaidMapZone, List<Vector2>> pair in zonePoints)
        {
            if (pair.Key.clearTrees <= 0f) continue;
            foreach (Vector2 point in pair.Value)
            {
                clearings.Add(new Clearing(point * size, pair.Key.clearTrees, pair.Key.clearTrees));
            }
        }

        int trees = BakeTrees(data, definition, clearings, out int rocks);
        report.Append($"\n  terrain {size:0}×{size:0} m, {trees} trees, {rocks} rocks and logs");
        terrain.Flush();
        BakeTrunks(map, terrain, definition);
        Physics.SyncTransforms();

        Transform content = map.Content;
        Transform start = PlaceSites(map, terrain, sites, zonePoints, report);
        SnapToGround(content, terrain);

        if (terrain.TryGetComponent(out TerrainMoss moss))
        {
            moss.Configure(definition.ForestLayer, Shader.Find(MossShaderName));
        }

        if (map.NavMesh != null)
        {
            NavMeshAssetManager.instance.StartBakingSurfaces(new Object[] { map.NavMesh });
            report.Append("\n  navmesh baking in the background");
        }

        map.Configure(terrain, content, start, map.NavMesh);
        EditorUtility.SetDirty(map);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(data);
        EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
        return report.ToString();
    }

    private static string Validate(RaidMap map)
    {
        RaidMapDefinition definition = map.Definition;
        if (definition == null) return "Assign a Raid Map definition first.";
        if (!definition.HasPaintedLayout && definition.HasLegacyImages)
            return $"{definition.name} still uses images: press 'Import from Images' once, then Bake.";
        if (definition.ForestLayer == null || definition.PathLayer == null || definition.CliffLayer == null)
            return $"{definition.name} needs its three terrain layers (forest, path, cliff).";
        if (map.Content == null) return "Give the RaidMap a Content object for everything that stands on the map.";
        return null;
    }

    /// <summary>
    /// Candidate points of every spawn zone (0–1 map positions), on ground no steeper than the zone allows.
    /// Seeded by the definition's scatter seed and the zone's name, so a bake is repeatable.
    /// </summary>
    private static Dictionary<RaidMapZone, List<Vector2>> ScatterZones(RaidMapDefinition definition, TerrainData data)
    {
        var result = new Dictionary<RaidMapZone, List<Vector2>>();
        foreach (RaidMapZone zone in definition.Zones)
        {
            if (zone == null || !zone.IsSpawnZone || result.ContainsKey(zone)) continue;
            float maxSlope = zone.maxSlope;
            result[zone] = RaidZoneGrid.Scatter(
                spot => zone.Sample(spot.x, spot.y), definition.Size, zone.pointSpacing, zone.maxPoints,
                RaidZoneGrid.SeedFor(definition.ScatterSeed, zone.name),
                spot => data.GetSteepness(spot.x, spot.y) <= maxSlope);
        }

        return result;
    }

    /// <summary>
    /// A navmesh bake ignores terrain trees, so creatures would walk through trunks. Every tree and rock with a capsule
    /// collider gets a matching prism in one invisible, not-walkable mesh collider under Content, which the navmesh cuts
    /// around (and which turns with the content when the map is rotated).
    /// </summary>
    private static void BakeTrunks(RaidMap map, Terrain terrain, RaidMapDefinition definition)
    {
        Transform holder = map.Content.Find(TrunksName);
        if (holder == null)
        {
            holder = new GameObject(TrunksName).transform;
            Undo.RegisterCreatedObjectUndo(holder.gameObject, "Bake raid map");
            holder.SetParent(map.Content, false);
        }

        holder.position = map.Content.position;
        holder.rotation = map.Content.rotation;
        holder.gameObject.isStatic = true;
        if (!holder.TryGetComponent(out NavMeshModifier modifier)) modifier = holder.gameObject.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true;
        modifier.area = NotWalkableArea;
        if (!holder.TryGetComponent(out MeshCollider collider)) collider = holder.gameObject.AddComponent<MeshCollider>();

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

            Vector3 foot = origin + Vector3.Scale(tree.position, data.size) - holder.position;
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

        string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(definition)).Replace('\\', '/');
        string path = $"{folder}/{definition.name} Trunks.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            mesh = existing;
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, path);
        }

        collider.sharedMesh = null;
        collider.sharedMesh = mesh;
        EditorUtility.SetDirty(collider);
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

    /// <summary>
    /// The scene's terrain, made (flat) when missing, sized and layered from the definition. Its heights are the map's
    /// sculpted shape, so a new heightmap resolution resamples them instead of starting flat.
    /// </summary>
    public static Terrain EnsureTerrain(RaidMap map, RaidMapDefinition definition)
    {
        Terrain terrain = map.Terrain;
        if (terrain == null)
        {
            var created = new GameObject("Raid Terrain");
            Undo.RegisterCreatedObjectUndo(created, "Bake raid map");
            created.transform.SetParent(map.transform, false);
            terrain = created.AddComponent<Terrain>();
            created.AddComponent<TerrainCollider>();
        }

        TerrainData data = terrain.terrainData;
        if (data == null || !AssetDatabase.Contains(data))
        {
            string folder = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(definition)).Replace('\\', '/');
            string path = $"{folder}/{definition.name} Terrain.asset";
            data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (data == null)
            {
                data = new TerrainData { heightmapResolution = definition.HeightmapResolution };
                AssetDatabase.CreateAsset(data, path);
            }
        }

        if (data.heightmapResolution != definition.HeightmapResolution)
        {
            int from = data.heightmapResolution;
            float[,] old = data.GetHeights(0, 0, from, from);
            Vector3 keepSize = data.size;
            data.heightmapResolution = definition.HeightmapResolution;
            data.size = keepSize;
            int to = data.heightmapResolution;
            var heights = new float[to, to];
            for (int row = 0; row < to; row++)
            {
                for (int column = 0; column < to; column++)
                {
                    heights[row, column] = Bilinear(old, column / (to - 1f) * (from - 1), row / (to - 1f) * (from - 1));
                }
            }

            data.SetHeights(0, 0, heights);
        }

        if (data.alphamapResolution != definition.PaintResolution) data.alphamapResolution = definition.PaintResolution;
        data.baseMapResolution = Mathf.Min(1024, definition.PaintResolution);
        data.size = new Vector3(definition.Size, definition.Height, definition.Size);
        data.terrainLayers = new[] { definition.ForestLayer, definition.PathLayer, definition.CliffLayer };

        terrain.terrainData = data;
        terrain.GetComponent<TerrainCollider>().terrainData = data;
        terrain.materialTemplate = definition.TerrainMaterial;
        terrain.drawInstanced = true;
        terrain.treeDistance = 160f;
        terrain.treeBillboardDistance = 400f;
        terrain.detailObjectDistance = 60f;
        terrain.heightmapPixelError = 4f;
        terrain.transform.position = map.transform.position - new Vector3(definition.Size * 0.5f, 0f, definition.Size * 0.5f);
        terrain.gameObject.isStatic = true;
        return terrain;
    }

    private static float Bilinear(float[,] grid, float x, float y)
    {
        int last = grid.GetLength(0) - 1;
        int x0 = Mathf.Clamp((int)x, 0, last);
        int y0 = Mathf.Clamp((int)y, 0, last);
        int x1 = Mathf.Min(x0 + 1, last);
        int y1 = Mathf.Min(y0 + 1, last);
        float tx = x - x0;
        float ty = y - y0;
        return Mathf.Lerp(Mathf.Lerp(grid[y0, x0], grid[y0, x1], tx), Mathf.Lerp(grid[y1, x0], grid[y1, x1], tx), ty);
    }

    // Layer 0 forest floor (moss), 1 path soil, 2 cliff rock. Rock follows the slope; soil follows the paths and
    // clearings; moss covers the rest, thickest where the forest is densest.
    private static void BakePaint(TerrainData data, RaidMapDefinition definition)
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

    private static int BakeTrees(TerrainData data, RaidMapDefinition definition, List<Clearing> clearings, out int rockCount)
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

    // A spot (metres from the map's south-west corner) that trees and rocks keep away from, each by its own distance.
    private readonly struct Clearing
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

    // Markers become the player start, the exits' places and the points of every spawn group that has a Site; spawn
    // zones' candidate points go to the groups that name the zone.
    private static Transform PlaceSites(RaidMap map, Terrain terrain, List<RaidSite> sites,
        Dictionary<RaidMapZone, List<Vector2>> zonePoints, StringBuilder report)
    {
        Transform content = map.Content;
        float size = map.Definition.Size;
        Vector3 origin = terrain.transform.position;
        Vector3 World(Vector2 position) => Ground(terrain, origin + new Vector3(position.x * size, 0f, position.y * size));

        // Player start.
        Transform start = map.PlayerStart;
        RaidSite? startSite = null;
        foreach (RaidSite site in sites)
        {
            if (site.Kind == RaidSiteKind.PlayerStart)
            {
                startSite = site;
                break;
            }
        }

        if (startSite.HasValue)
        {
            if (start == null)
            {
                start = new GameObject("Player Start").transform;
                Undo.RegisterCreatedObjectUndo(start.gameObject, "Bake raid map");
                start.SetParent(content, false);
            }

            Undo.RecordObject(start, "Bake raid map");
            start.position = World(startSite.Value.Position);
            Vector3 toCentre = Vector3.ProjectOnPlane(map.Center - start.position, Vector3.up);
            if (toCentre.sqrMagnitude > 0.01f) start.rotation = Quaternion.LookRotation(toCentre);
            report.Append($"\n  player start at {start.position:F0}");
        }
        else
        {
            report.Append("\n  no player start marker (white): the player stays where the scene puts them");
        }

        // Exits: each moves to the nearest free blue marker, its alarm group with it.
        var exitSpots = new List<Vector3>();
        foreach (RaidSite site in sites)
        {
            if (site.Kind == RaidSiteKind.Extraction) exitSpots.Add(World(site.Position));
        }

        var exits = new List<ExtractionPoint>(content.GetComponentsInChildren<ExtractionPoint>(true));
        exits.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        foreach (ExtractionPoint exit in exits)
        {
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < exitSpots.Count; i++)
            {
                float distance = Vector2.SqrMagnitude(new Vector2(exitSpots[i].x - exit.transform.position.x, exitSpots[i].z - exit.transform.position.z));
                if (distance < best)
                {
                    best = distance;
                    nearest = i;
                }
            }

            if (nearest < 0)
            {
                report.Append($"\n  {exit.name}: no free blue marker, left where it was");
                continue;
            }

            Vector3 moved = exitSpots[nearest] - exit.transform.position;
            exitSpots.RemoveAt(nearest);
            Undo.RecordObject(exit.transform, "Bake raid map");
            exit.transform.position += moved;
            if (exit.Alarm != null && !exit.Alarm.IsBakedFromMap)
            {
                Undo.RecordObject(exit.Alarm.transform, "Bake raid map");
                exit.Alarm.transform.position += new Vector3(moved.x, 0f, moved.z);
            }

            report.Append($"\n  {exit.name} at {exit.transform.position:F0}");
        }

        // Spawn groups with a Site or a Zone: their points are rebuilt from the map.
        var groupsByKind = new Dictionary<RaidSiteKind, List<SpawnGroup>>();
        var groupsByZone = new Dictionary<string, List<SpawnGroup>>();
        foreach (SpawnGroup group in content.GetComponentsInChildren<SpawnGroup>(true))
        {
            if (!group.IsBakedFromMap) continue;
            for (int i = group.transform.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(group.transform.GetChild(i).gameObject);
            }

            if (group.Site != RaidSiteKind.None)
            {
                if (!groupsByKind.TryGetValue(group.Site, out List<SpawnGroup> list)) groupsByKind[group.Site] = list = new List<SpawnGroup>();
                list.Add(group);
            }

            if (!string.IsNullOrEmpty(group.Zone))
            {
                if (!groupsByZone.TryGetValue(group.Zone, out List<SpawnGroup> list)) groupsByZone[group.Zone] = list = new List<SpawnGroup>();
                list.Add(group);
            }
        }

        // Markers: groups of one kind take turns, a whole site each.
        foreach (KeyValuePair<RaidSiteKind, List<SpawnGroup>> pair in groupsByKind)
        {
            List<SpawnGroup> groups = pair.Value;
            groups.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            var ofKind = sites.FindAll(site => site.Kind == pair.Key);
            int[] owners = RaidMapMarkers.ShareOut(ofKind.Count, groups.Count);
            RaidMapDefinition.SiteRule rule = map.Definition.RuleFor(pair.Key);
            var counts = new int[groups.Count];
            for (int i = 0; i < ofKind.Count; i++)
            {
                SpawnGroup group = groups[owners[i]];
                Vector3 centre = World(ofKind[i].Position);
                for (int k = 0; k < rule.pointsPerSite; k++)
                {
                    Vector3 spot = centre;
                    if (k > 0)
                    {
                        float angle = (k - 1) * Mathf.PI * 2f / Mathf.Max(1, rule.pointsPerSite - 1) + i;
                        spot = Ground(terrain, centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * rule.spread);
                    }

                    var point = new GameObject($"{pair.Key} {i + 1}.{k + 1}");
                    Undo.RegisterCreatedObjectUndo(point, "Bake raid map");
                    point.transform.SetParent(group.transform, false);
                    point.transform.position = spot;
                    counts[owners[i]]++;
                }
            }

            for (int g = 0; g < groups.Count; g++)
            {
                report.Append($"\n  {groups[g].name}: {counts[g]} points ({pair.Key})");
            }
        }

        foreach (RaidSiteKind kind in System.Enum.GetValues(typeof(RaidSiteKind)))
        {
            bool used = kind == RaidSiteKind.None || kind == RaidSiteKind.PlayerStart || kind == RaidSiteKind.Extraction || groupsByKind.ContainsKey(kind);
            if (!used && sites.Exists(site => site.Kind == kind))
            {
                report.Append($"\n  {kind} markers found but no spawn group uses them (set a group's Site)");
            }
        }

        // Spawn zones: each candidate point goes to the zone's groups in turn.
        RaidMapDefinition definition = map.Definition;
        foreach (KeyValuePair<string, List<SpawnGroup>> pair in groupsByZone)
        {
            RaidMapZone zone = definition.SpawnZone(pair.Key);
            if (zone == null || !zonePoints.TryGetValue(zone, out List<Vector2> points))
            {
                report.Append($"\n  ! zone '{pair.Key}' (used by {pair.Value[0].name}) isn't a spawn zone of {definition.name}");
                continue;
            }

            List<SpawnGroup> groups = pair.Value;
            groups.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            int[] owners = RaidMapMarkers.ShareOut(points.Count, groups.Count);
            var counts = new int[groups.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var point = new GameObject($"{zone.name} {i + 1}");
                Undo.RegisterCreatedObjectUndo(point, "Bake raid map");
                point.transform.SetParent(groups[owners[i]].transform, false);
                point.transform.position = World(points[i]);
                counts[owners[i]]++;
            }

            for (int g = 0; g < groups.Count; g++)
            {
                report.Append($"\n  {groups[g].name}: {counts[g]} points (zone {zone.name})");
            }
        }

        foreach (KeyValuePair<RaidMapZone, List<Vector2>> pair in zonePoints)
        {
            if (!groupsByZone.ContainsKey(pair.Key.name))
            {
                report.Append($"\n  zone {pair.Key.name}: {pair.Value.Count} points but no spawn group uses it (set a group's Zone)");
            }
        }

        return start;
    }

    // Everything on the map sits on the ground: spawn points, and every object directly inside a plain folder object.
    private static void SnapToGround(Transform node, Terrain terrain)
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
                    Snap(point, terrain);
                }
            }
            else if (IsFolder(child))
            {
                SnapToGround(child, terrain);
            }
            else
            {
                Snap(child, terrain);
            }
        }
    }

    private static bool IsFolder(Transform node) => node.childCount > 0 && node.GetComponents<Component>().Length == 1;

    private static void Snap(Transform target, Terrain terrain)
    {
        Vector3 grounded = Ground(terrain, target.position);
        if (Mathf.Abs(grounded.y - target.position.y) > 0.001f)
        {
            Undo.RecordObject(target, "Bake raid map");
            target.position = grounded;
        }
    }

    private static Vector3 Ground(Terrain terrain, Vector3 position)
    {
        position.y = terrain.transform.position.y + terrain.SampleHeight(position);
        return position;
    }
}
