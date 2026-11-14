#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

/// <summary>
/// Safe Haven's ground made again with the World Builder (`unity command run_script --file AgentScripts/BuildHavenWorld.cs
/// --entry BuildHavenWorld.Apply`): one 400 m terrain round the portal in the Haven look (meadow paint rules, rock on
/// steep ground, grass). The clearing stays flat; gentle hills roll outward and the land rises to the north into a
/// terrace with a crag. A Haven river runs past the east of the base (the drinking spot on its bank), two Haven paths
/// lead out (north-west up the slope from the portal, east over a ford), and Haven groves ring the clearing. Everything
/// that stood on the old ground keeps its height above the new one; the old terrain tiles and stream go; the NavMesh and
/// the fog's ground heights are baked again. Reopens the scene that was open.
/// </summary>
public static class BuildHavenWorld
{
    private const string ScenePath = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string DataPath = "Assets/_Project/Art/Terrain/SafeHaven/Haven World Terrain.asset";
    private const float Size = 400f;
    private const float Height = 50f;
    private const float Base = 10f;   // metres of ground under the clearing (the terrain sits 10 m below y = 0)

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        WorldAssets.EnsureAll();

        Terrain[] old = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include);
        Dictionary<Transform, float> heights = HeightsAboveGround(scene, old);
        TerrainMoss oldMoss = old.Select(t => t.GetComponent<TerrainMoss>()).FirstOrDefault(m => m != null);

        Terrain terrain = MakeTerrain(oldMoss);
        foreach (Terrain tile in old)
        {
            Object.DestroyImmediate(tile.gameObject);
        }

        // The old stream goes with its drinking spot: any drinkable water serves now (WaterSurface.Drinkable), and a spot
        // kept from it was left stranded on dry ground when the river replaced it.
        GameObject stream = GameObject.Find("Stream");
        if (stream != null) Object.DestroyImmediate(stream);

        RiverFeature river = Features();
        WorldBuilder.BuildChanged(true);
        string report = WorldBuilder.LastReport;

        HavenOldGrowth.Grass(terrain);

        int moved = 0;
        foreach (KeyValuePair<Transform, float> item in heights)
        {
            if (item.Key == null) continue;
            Vector3 p = item.Key.position;
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y + item.Value;
            item.Key.position = p;
            moved++;
        }

        string spot = drinking != null ? PlaceDrinkingSpot(drinking, river, terrain) : "no drinking spot";
        foreach (NavMeshSurface surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include)) HavenNavMesh.Bake(surface);
        foreach (FogGround fog in Object.FindObjectsByType<FogGround>(FindObjectsInactive.Include)) fog.Bake();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previous) && previous != ScenePath) EditorSceneManager.OpenScene(previous);
        return $"terrain {Size} m, {old.Length} old tiles removed, {moved} objects set on the new ground, {spot}; build: {report}";
    }

    // How high everything that stands on the ground stands above it now: kept on the new ground.
    private static Dictionary<Transform, float> HeightsAboveGround(Scene scene, Terrain[] old)
    {
        string[] skip = { "Clearing", "Directional Light", "Global Volume", "Main Camera", "Player Cinemachine Camera", "GameStateManager", "NavMesh",
            "SaveHotkeys", "Persistent Scene", "Scene Audio", "UI Root", "Sky and Weather", "Atmosphere", "Night Prowlers", "Prologue Homecoming",
            "Forest Wind", "World", "Stream" };
        string[] groups = { "Portal", "Gathering", "Tier 0 Forage", "Overgrowth", "Residents", "Ward Breach", "Clearing", "Portal Bloom" };
        var targets = new List<Transform>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<Terrain>() != null) continue;
            if (groups.Contains(root.name))
            {
                foreach (Transform child in root.transform)
                {
                    if (child.GetComponent<Terrain>() == null) targets.Add(child);
                }

                if (root.name == "Gathering")
                {
                    foreach (Transform group in root.transform)
                    {
                        foreach (Transform node in group) targets.Add(node);
                    }
                }

                continue;
            }

            if (!skip.Contains(root.name)) targets.Add(root.transform);
        }

        var heights = new Dictionary<Transform, float>();
        foreach (Transform target in targets.Distinct())
        {
            float? ground = OldGround(target.position, old);
            if (ground.HasValue) heights[target] = Mathf.Clamp(target.position.y - ground.Value, -0.5f, 3f);
        }

        return heights;
    }

    private static float? OldGround(Vector3 at, Terrain[] old)
    {
        foreach (Terrain tile in old)
        {
            Vector3 origin = tile.transform.position;
            Vector3 size = tile.terrainData.size;
            if (at.x >= origin.x && at.x <= origin.x + size.x && at.z >= origin.z && at.z <= origin.z + size.z)
            {
                return tile.SampleHeight(at) + origin.y;
            }
        }

        return null;
    }

    private static Terrain MakeTerrain(TerrainMoss oldMoss)
    {
        const int resolution = 513;
        var data = new TerrainData { heightmapResolution = resolution, alphamapResolution = 512 };
        data.size = new Vector3(Size, Height, Size);
        data.SetDetailResolution(512, 16);
        AssetDatabase.DeleteAsset(DataPath);
        AssetDatabase.CreateAsset(data, DataPath);
        var heights = new float[resolution, resolution];
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float wx = x / (resolution - 1f) * Size - Size * 0.5f;
                float wz = y / (resolution - 1f) * Size - Size * 0.5f;
                heights[y, x] = Ground(wx, wz) / Height;
            }
        }

        data.SetHeights(0, 0, heights);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Safe Haven Terrain";
        terrainObject.transform.position = new Vector3(-Size * 0.5f, -Base, -Size * 0.5f);
        GameObject clearing = GameObject.Find("Clearing");
        if (clearing != null) terrainObject.transform.SetParent(clearing.transform, true);
        var terrain = terrainObject.GetComponent<Terrain>();
        var world = terrainObject.AddComponent<WorldTerrain>();
        WorldTerrainEditor.UseLook(world, terrain, true);
        data.terrainLayers = new[] { WorldAssets.Layer("Meadow Grass") };
        WorldPaint.Apply(terrain, world.PaintRules);
        if (oldMoss != null)
        {
            TerrainMoss moss = terrainObject.AddComponent<TerrainMoss>();
            EditorUtility.CopySerialized(oldMoss, moss);
            var mossLayer = (TerrainLayer)new SerializedObject(moss).FindProperty("mossLayer").objectReferenceValue;
            if (mossLayer != null && !data.terrainLayers.Contains(mossLayer))
            {
                data.terrainLayers = data.terrainLayers.Append(mossLayer).ToArray();
            }
        }

        return terrain;
    }

    // Metres above the terrain's foot: a flat clearing (10 m: y = 0 in the world), rolling hills beyond it, the land
    // rising to the north.
    private static float Ground(float x, float z)
    {
        float distance = Mathf.Sqrt(x * x + z * z);
        float away = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(38f, 115f, distance));
        float hills = (Mathf.PerlinNoise(x / 55f + 11.3f, z / 55f + 4.1f) - 0.45f) * 7f + (Mathf.PerlinNoise(x / 17f + 3f, z / 17f) - 0.5f) * 1.2f;
        float north = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(45f, 175f, z)) * 12f;
        float south = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-60f, -190f, z)) * -3f;
        float micro = (Mathf.PerlinNoise(x / 6f + 21f, z / 6f + 9f) - 0.5f) * 0.25f;
        return Base + micro + away * (hills + south) + north;
    }

    private static RiverFeature Features()
    {
        GameObject oldWorld = GameObject.Find("World");
        if (oldWorld != null) Object.DestroyImmediate(oldWorld);
        var root = new GameObject("World").transform;

        // The land rises to a terrace in the north; drawn west to east, so its high side (left) is the north.
        var ridge = Feature<CliffFeature>(root, "North Crag", new[] { new Vector2(-190f, 118f), new Vector2(-120f, 104f), new Vector2(-55f, 112f), new Vector2(10f, 98f),
            new Vector2(70f, 108f), new Vector2(130f, 100f), new Vector2(190f, 112f) });
        ridge.Configure(WorldAssets.RockFaces(), WorldAssets.Palette("Cliff Rubble"), WorldAssets.Layer("Gravel"));
        Set(ridge, "height", 4.5f);
        Set(ridge, "plateau", 70f);

        var river = Feature<RiverFeature>(root, "River", new[] { new Vector2(118f, 198f), new Vector2(84f, 140f), new Vector2(58f, 75f), new Vector2(47f, 20f),
            new Vector2(52f, -35f), new Vector2(36f, -95f), new Vector2(62f, -150f), new Vector2(48f, -198f) });
        river.Configure(WorldAssets.RiverWater(), WorldAssets.Layer("Gravel"), WorldAssets.Layer("Trodden Path"),
            WorldAssets.Palette("River Stones"), WorldAssets.Palette("Riverbank"));

        var north = Feature<PathFeature>(root, "Path to the Heights", new[] { new Vector2(-12f, 33f), new Vector2(-24f, 60f), new Vector2(-48f, 95f),
            new Vector2(-70f, 140f), new Vector2(-88f, 196f) });
        north.Configure(WorldAssets.Layer("Trodden Path"), null);
        var east = Feature<PathFeature>(root, "Path to the Ford", new[] { new Vector2(6f, -8f), new Vector2(24f, -24f), new Vector2(52f, -36f),
            new Vector2(95f, -52f), new Vector2(145f, -48f), new Vector2(196f, -60f) });
        east.Configure(WorldAssets.Layer("Trodden Path"), null);

        // Groves all round, leaving the clearing open.
        (string name, Vector2[] points)[] groves =
        {
            ("Grove North", new[] { new Vector2(-198f, 52f), new Vector2(198f, 52f), new Vector2(198f, 198f), new Vector2(-198f, 198f) }),
            ("Grove South", new[] { new Vector2(-198f, -198f), new Vector2(198f, -198f), new Vector2(198f, -55f), new Vector2(-198f, -55f) }),
            ("Grove West", new[] { new Vector2(-198f, -55f), new Vector2(-58f, -55f), new Vector2(-58f, 52f), new Vector2(-198f, 52f) }),
            ("Grove East", new[] { new Vector2(62f, -55f), new Vector2(198f, -55f), new Vector2(198f, 52f), new Vector2(62f, 52f) }),
        };
        foreach ((string name, Vector2[] points) in groves)
        {
            var grove = Feature<BiomeArea>(root, name, points);
            grove.Spline.Closed = true;
            grove.Configure(WorldMenus.HavenGroveLayers());
            HavenOldGrowth.PaintUnder(root, name, points);
        }

        return river;
    }

    // The drinking spot on the river bank nearest the base.
    private static string PlaceDrinkingSpot(Transform spot, RiverFeature river, Terrain terrain)
    {
        float best = float.MaxValue;
        Vector3 bank = spot.position;
        for (int i = 0; i <= 400; i++)
        {
            Vector3 p = river.transform.TransformPoint((Vector3)river.Spline.EvaluatePosition(i / 400f));
            float d = new Vector2(p.x, p.z).magnitude;
            if (d < best)
            {
                best = d;
                bank = p;
            }
        }

        Vector3 toBase = new Vector3(-bank.x, 0f, -bank.z).normalized;
        Vector3 at = bank + toBase * 3.2f;
        at.y = terrain.SampleHeight(at) + terrain.transform.position.y;
        spot.SetPositionAndRotation(at, Quaternion.LookRotation(-toBase));
        GameObject portal = GameObject.Find("Portal");
        if (portal != null) spot.SetParent(portal.transform.parent, true);
        return $"drinking spot {at:F0}";
    }

    private static T Feature<T>(Transform root, string name, IEnumerable<Vector2> points) where T : WorldFeature
    {
        var go = new GameObject(name, typeof(SplineContainer));
        go.transform.SetParent(root, false);
        var feature = go.AddComponent<T>();
        Spline spline = feature.Spline;
        spline.Clear();
        foreach (Vector2 p in points)
        {
            float y = WorldGround.TryGround(p, out Vector3 ground, out _) ? ground.y : 0f;
            spline.Add(new BezierKnot(new float3(p.x, y, p.y)), TangentMode.AutoSmooth);
        }

        return feature;
    }

    private static void Set(Object target, string field, float value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}

/// <summary>Takes away the old placeholder ring of dark trees round the clearing (the Haven groves stand there now) and
/// bakes the NavMesh again (`--entry HavenForestEdge.Remove`).</summary>
public static class HavenForestEdge
{
    public static string Remove()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";
        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SafeHaven.unity");
        GameObject edge = GameObject.Find("Clearing/Forest Edge");
        int count = edge != null ? edge.transform.childCount : 0;
        if (edge != null) Object.DestroyImmediate(edge);
        foreach (NavMeshSurface surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include)) HavenNavMesh.Bake(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(previous) && previous != "Assets/_Project/Scenes/SafeHaven.unity") EditorSceneManager.OpenScene(previous);
        return $"removed the forest edge ({count} trees)";
    }
}

/// <summary>
/// Bakes Safe Haven's NavMesh into its own asset, as the editor's Bake button does. Built from a script, the data would
/// otherwise live inside the scene, and Unity then saves the whole scene as binary (`--entry HavenNavMesh.Rebake`).
/// </summary>
public static class HavenNavMesh
{
    private const string AssetPath = "Assets/_Project/Art/Terrain/SafeHaven/SafeHavenTerrainNavMesh.asset";

    public static void Bake(NavMeshSurface surface)
    {
        surface.BuildNavMesh();
        UnityEngine.AI.NavMeshData data = surface.navMeshData;
        AssetDatabase.DeleteAsset(AssetPath);
        AssetDatabase.CreateAsset(data, AssetPath);
        surface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(AssetPath);
        EditorUtility.SetDirty(surface);
    }

    public static string Rebake()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";
        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SafeHaven.unity");
        foreach (NavMeshSurface surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include)) Bake(surface);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previous) && previous != scene.path) EditorSceneManager.OpenScene(previous);
        return "baked into " + AssetPath;
    }
}

/// <summary>
/// The groves round Safe Haven get a Norwegian old-growth floor (`--entry HavenOldGrowth.Apply`): a Ground Paint Area
/// with the Old-Growth Forest rules (moss carpet, mossy mud, litter, black humus in hollows) under each grove, fading in
/// over 10 m from the meadow; the terrain blends its layers by height (the textures' displacement); and the grass is
/// filled again, sparse on the forest floor (ferns, berry shrubs and heather instead). Reopens the scene that was open.
/// </summary>
public static class HavenOldGrowth
{
    private static readonly (string name, Vector2[] points)[] Groves =
    {
        ("Grove North", new[] { new Vector2(-198f, 52f), new Vector2(198f, 52f), new Vector2(198f, 198f), new Vector2(-198f, 198f) }),
        ("Grove South", new[] { new Vector2(-198f, -198f), new Vector2(198f, -198f), new Vector2(198f, -55f), new Vector2(-198f, -55f) }),
        ("Grove West", new[] { new Vector2(-198f, -55f), new Vector2(-58f, -55f), new Vector2(-58f, 52f), new Vector2(-198f, 52f) }),
        ("Grove East", new[] { new Vector2(62f, -55f), new Vector2(198f, -55f), new Vector2(198f, 52f), new Vector2(62f, 52f) }),
    };

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";
        WorldAssets.EnsureAll();
        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SafeHaven.unity");
        Transform root = GameObject.Find("World").transform;
        foreach ((string name, Vector2[] points) in Groves)
        {
            Transform old = root.Find(name + " Floor");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            PaintUnder(root, name, points);
        }

        Terrain terrain = Object.FindObjectsByType<Terrain>().First();
        WorldBuilder.BuildChanged(true);
        Grass(terrain);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(previous) && previous != scene.path) EditorSceneManager.OpenScene(previous);
        return $"old-growth floor under {Groves.Length} groves; layers {string.Join(", ", terrain.terrainData.terrainLayers.Select(l => l.name))}";
    }

    /// <summary>After the rules change in the open Safe Haven (the World Builder has already rebuilt it): the grass again,
    /// then save (`--entry HavenOldGrowth.Refresh`).</summary>
    public static string Refresh()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/SafeHaven.unity") return "open Safe Haven first";
        Terrain terrain = Object.FindObjectsByType<Terrain>().First();
        Grass(terrain);
        EditorSceneManager.SaveScene(scene);
        return "grass refilled, saved";
    }

    public static void PaintUnder(Transform root, string grove, Vector2[] points)
    {
        var go = new GameObject(grove + " Floor", typeof(SplineContainer));
        go.transform.SetParent(root, false);
        var paint = go.AddComponent<GroundPaintArea>();
        paint.Spline.Clear();
        foreach (Vector2 p in points)
        {
            float y = WorldGround.TryGround(p, out Vector3 ground, out _) ? ground.y : 0f;
            paint.Spline.Add(new BezierKnot(new float3(p.x, y, p.y)), TangentMode.Linear);
        }

        paint.Spline.Closed = true;
        paint.Configure(WorldAssets.PaintRules("Old-Growth Forest"), 10f);
        UseHeightBlend(Object.FindObjectsByType<Terrain>().FirstOrDefault());
    }

    // The layers' displacement (in their mask maps) decides which one shows where they meet: moss over the soil's
    // hollows rather than a smooth cross-fade.
    private static void UseHeightBlend(Terrain terrain)
    {
        Material material = terrain != null ? terrain.materialTemplate : null;
        if (material == null || !material.HasProperty("_EnableHeightBlend")) return;
        material.SetFloat("_EnableHeightBlend", 1f);
        material.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
        EditorUtility.SetDirty(material);
    }

    /// <summary>Grass by ground layer, but sparse on the forest floor: ferns, berry shrubs and heather instead.</summary>
    public static void Grass(Terrain terrain)
    {
        List<GameObject> grass = GrassSetup.EnsureAssets();
        GrassSetup.AddTo(terrain, grass);
        GrassSetup.GroundFill[] fills = GrassSetup.DefaultFill(terrain.terrainData);
        for (int i = 0; i < fills.Length; i++)
        {
            switch (fills[i].layer != null ? fills[i].layer.name : "")
            {
                case "Forest Moss": Set(ref fills[i], 0.12f, 0f, 0.12f, 0.45f); break;
                case "Forest Litter": Set(ref fills[i], 0.05f, 0f, 0.05f, 0.3f); break;
                case "Dark Humus": Set(ref fills[i], 0f, 0f, 0f, 0.15f); break;
                case "Muddy Bank": Set(ref fills[i], 0.1f, 0f, 0.08f, 0.3f); break;
            }
        }

        GrassSetup.FillByGround(terrain, grass, fills, 3);
        GrassSetup.ClearUnderWater(terrain);
        GrassSetup.BakeGroundColour(terrain);
    }

    private static void Set(ref GrassSetup.GroundFill fill, float shortGrass, float tallGrass, float bushes, float plants)
    {
        fill.shortGrass = shortGrass;
        fill.tallGrass = tallGrass;
        fill.bushes = bushes;
        fill.plants = plants;
    }
}
