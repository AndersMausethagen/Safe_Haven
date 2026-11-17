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

        RaidMapSurface.Paint(data, definition);

        List<RaidSite> sites = definition.Sites();
        var clearings = new List<RaidMapSurface.Clearing>(sites.Count);
        foreach (RaidSite site in sites)
        {
            clearings.Add(new RaidMapSurface.Clearing(site.Position * size, definition.ClearAroundSites, 3f));
        }

        Dictionary<RaidMapZone, List<Vector2>> zonePoints = ScatterZones(definition, data);
        foreach (KeyValuePair<RaidMapZone, List<Vector2>> pair in zonePoints)
        {
            if (pair.Key.clearTrees <= 0f) continue;
            foreach (Vector2 point in pair.Value)
            {
                clearings.Add(new RaidMapSurface.Clearing(point * size, pair.Key.clearTrees, pair.Key.clearTrees));
            }
        }

        int trees = RaidMapSurface.Scatter(data, definition, clearings, out int rocks);
        report.Append($"\n  terrain {size:0}×{size:0} m, {trees} trees, {rocks} rocks and logs");
        terrain.Flush();
        BakeTrunks(map, terrain, definition);
        Physics.SyncTransforms();

        Transform content = map.Content;
        Transform start = PlaceSites(map, terrain, sites, zonePoints, report);
        SnapToGround(content, terrain);

        // The moss grows where the map says (its Moss Layer, the forest floor by default); with No Moss the scene's is left as it is.
        if (!definition.NoMoss && terrain.TryGetComponent(out TerrainMoss moss))
        {
            moss.Configure(definition.MossLayer, Shader.Find(MossShaderName));
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
        Transform holder = map.Content.Find(RaidMapSurface.TrunksName);
        if (holder == null)
        {
            holder = new GameObject(RaidMapSurface.TrunksName).transform;
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

        Mesh mesh = RaidMapSurface.TrunkMesh(terrain, holder.position, RaidMapSurface.Prisms(definition));

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
        data.terrainLayers = definition.Layers;

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
                    Vector3 spot = RaidMapSurface.SitePoint(terrain, centre, rule, i, k);
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

    private static void SnapToGround(Transform node, Terrain terrain) =>
        RaidMapSurface.SnapToGround(node, terrain, target => Undo.RecordObject(target, "Bake raid map"));

    private static Vector3 Ground(Terrain terrain, Vector3 position) => RaidMapSurface.Ground(terrain, position);
}
