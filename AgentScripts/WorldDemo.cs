using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// Builds the World Builder demo scene, Scenes/Demo/WorldBuilderDemo.unity (`unity command run_script --file
/// AgentScripts/WorldDemo.cs --entry WorldDemo.Create`): a sloping, hilly terrain with a river that falls over a cliff,
/// a path fording it, a rock outcrop and a dark forest, all made with the World Builder's features. Other entries move
/// things to check that the builder follows (and that undo works).
/// </summary>
public static class WorldDemo
{
    private const string Folder = "Assets/_Project/Scenes/Demo";
    private const string ScenePath = Folder + "/WorldBuilderDemo.unity";

    public static string Create()
    {
        var open = EditorSceneManager.GetActiveScene();
        if (open.isDirty && open.path != ScenePath) // the demo itself is rebuilt from scratch
        {
            return "the open scene has unsaved changes: save or discard them first";
        }

        AssetDatabase.DeleteAsset(Folder); // the demo's own scene, terrain and stash
        Directory.CreateDirectory(Folder);
        WorldAssets.EnsureAll();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var data = new TerrainData { heightmapResolution = 257, alphamapResolution = 256 };
        data.size = new Vector3(160f, 40f, 160f);
        data.SetDetailResolution(256, 16);
        AssetDatabase.CreateAsset(data, Folder + "/WorldBuilderDemo Terrain.asset");
        var heights = new float[257, 257];
        for (int y = 0; y < 257; y++)
        {
            for (int x = 0; x < 257; x++)
            {
                float wx = x / 256f * 160f, wz = y / 256f * 160f;
                float hills = Mathf.PerlinNoise(wx / 38f + 3.1f, wz / 38f + 7.7f) * 5f + Mathf.PerlinNoise(wx / 11f, wz / 11f) * 0.8f;
                heights[y, x] = (6f + wz / 160f * 7f + hills) / 40f; // falls towards the south
            }
        }

        data.SetHeights(0, 0, heights);
        GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
        terrainObject.name = "Terrain";
        var terrain = terrainObject.GetComponent<Terrain>();
        terrain.materialTemplate = WorldAssets.TerrainMaterial(false);
        terrain.basemapDistance = 1000f;
        var world = terrainObject.AddComponent<WorldTerrain>();
        world.PaintRules = WorldAssets.PaintRules(false);
        data.terrainLayers = new[] { WorldAssets.Layer("Pine Needle Floor") };
        WorldPaint.Apply(terrain, world.PaintRules);
        AddGrass(terrain);

        var root = new GameObject("World").transform;
        var cliff = Feature<CliffFeature>(root, "Cliff", new[] { new Vector2(-5f, 86f), new Vector2(40f, 92f), new Vector2(80f, 84f), new Vector2(125f, 90f), new Vector2(165f, 86f) });
        cliff.Configure(WorldAssets.RockFaces(), WorldAssets.Palette("Cliff Rubble"), WorldAssets.Layer("Gravel"));
        Set(cliff, "height", 5f); // drawn west to east, so its high side (left) is the north: the river falls over it
        Set(cliff, "plateau", 80f); // the whole north stays up (a terrace, not a ridge)
        var outcrop = Feature<CliffFeature>(root, "Outcrop", Circle(new Vector2(128f, 40f), 13f, 7));
        outcrop.Configure(WorldAssets.RockFaces(), WorldAssets.Palette("Cliff Rubble"), WorldAssets.Layer("Gravel"));
        outcrop.Spline.Closed = true;
        Set(outcrop, "height", 7f);
        Set(outcrop, "plateau", 6f);

        var river = Feature<RiverFeature>(root, "River", new[] { new Vector2(60f, 158f), new Vector2(70f, 125f), new Vector2(56f, 100f), new Vector2(64f, 70f), new Vector2(45f, 40f), new Vector2(58f, 2f) });
        river.Configure(WorldAssets.RiverWater(), WorldAssets.Layer("Gravel"), WorldAssets.Layer("Muddy Bank"),
                        WorldAssets.Palette("River Stones"), WorldAssets.Palette("Riverbank"));
        river.WidthAtKnots.Resize(6);
        river.WidthAtKnots[0] = 0.6f;
        river.WidthAtKnots[3] = 1.4f;

        var path = Feature<PathFeature>(root, "Path", new[] { new Vector2(4f, 30f), new Vector2(30f, 50f), new Vector2(55f, 55f), new Vector2(85f, 60f), new Vector2(110f, 70f), new Vector2(150f, 66f) });
        path.Configure(WorldAssets.Layer("Muddy Bank"), null);

        var forest = Feature<BiomeArea>(root, "Dark Forest", new[] { new Vector2(4f, 4f), new Vector2(110f, 4f), new Vector2(110f, 80f), new Vector2(4f, 80f) });
        forest.Spline.Closed = true;
        forest.Configure(WorldMenus.DarkForestLayers());

        Camera camera = Camera.main;
        camera.transform.SetPositionAndRotation(new Vector3(80f, 70f, -45f), Quaternion.Euler(38f, 0f, 0f));
        camera.farClipPlane = 600f;
        EditorSceneManager.SaveScene(scene, ScenePath);
        WorldBuilder.BuildChanged(true);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "demo built: " + WorldBuilder.LastReport;
    }

    public static string Report()
    {
        var sb = new System.Text.StringBuilder();
        foreach (WorldFeature feature in Object.FindObjectsByType<WorldFeature>())
        {
            Transform generated = feature.GeneratedRoot(false);
            sb.Append($"{feature.name}: knots={feature.Spline.Count} generated={(generated != null ? generated.childCount : 0)}");
            if (feature is RiverFeature)
            {
                Transform water = generated != null ? generated.Find("Water") : null;
                var mesh = water != null ? water.GetComponent<MeshFilter>().sharedMesh : null;
                sb.Append($" waterVerts={(mesh != null ? mesh.vertexCount : 0)} plunges={CountNamed(generated, "Plunge")}");
            }

            sb.Append("\n");
        }

        foreach (WorldTerrain world in Object.FindObjectsByType<WorldTerrain>())
        {
            Terrain terrain = world.GetComponent<Terrain>();
            sb.Append($"{terrain.name}: y={terrain.transform.position.y:0.0} size={terrain.terrainData.size} layers={terrain.terrainData.terrainLayers.Length} " +
                      $"stash h={world.Stash?.HeightCount} a={world.Stash?.AlphaCount} d={world.Stash?.DetailCount} trees={world.Stash?.Trees.Count} built={world.BuiltFeatures.Count}\n");
        }

        return sb.Append("last: " + WorldBuilder.LastReport).ToString();
    }

    /// <summary>Height of the ground at a point (for before / after checks).</summary>
    public static string Ground(float x, float z) =>
        WorldGround.TryGround(new Vector2(x, z), out Vector3 p, out Vector3 n) ? $"{p.y:0.000} slope={WorldGround.Slope(n):0}" : "off terrain";

    /// <summary>Moves the river's fourth knot sideways (the old bed should fill back in).</summary>
    public static string MoveRiver(float dx)
    {
        var river = Object.FindAnyObjectByType<RiverFeature>();
        Undo.IncrementCurrentGroup();
        Undo.RecordObject(river.Container, "Move River Knot");
        BezierKnot knot = river.Spline[3];
        knot.Position += new float3(dx, 0f, 0f);
        river.Spline[3] = knot;
        WorldBuilder.Schedule(0); // as the editor does after an edit
        WorldBuilder.BuildChanged(false);
        return Report();
    }

    /// <summary>Raises the ground in a circle the way a sculpting brush would (not through the builder), then lets
    /// the builder react as it does in the editor.</summary>
    public static string HandRaise(float x, float z, float radius, float metres)
    {
        Terrain terrain = Terrain.activeTerrain;
        TerrainData data = terrain.terrainData;
        TerrainGrid grid = TerrainGrid.Heights(terrain);
        RectInt rect = grid.Cells(new Rect(x - radius, z - radius, radius * 2f, radius * 2f));
        float[,] h = data.GetHeights(rect.x, rect.y, rect.width, rect.height);
        for (int j = 0; j < rect.height; j++)
        {
            for (int i = 0; i < rect.width; i++)
            {
                Vector2 p = grid.World(rect.x + i, rect.y + j);
                float w = 1f - Mathf.Clamp01(Vector2.Distance(p, new Vector2(x, z)) / radius);
                h[j, i] += metres / data.size.y * w;
            }
        }

        Undo.RegisterCompleteObjectUndo(data, "Sculpt");
        data.SetHeights(rect.x, rect.y, h); // fires TerrainCallbacks.heightmapChanged like a brush stroke
        WorldBuilder.BuildChanged(false);
        return Report();
    }

    public static string DeletePath()
    {
        var path = Object.FindAnyObjectByType<PathFeature>();
        Undo.IncrementCurrentGroup();
        Undo.DestroyObjectImmediate(path.gameObject);
        WorldBuilder.Schedule(0);
        WorldBuilder.BuildChanged(false);
        return Report();
    }

    public static string UndoLast()
    {
        Undo.PerformUndo();
        return Report();
    }

    public static string View(float x, float y, float z, float pitch, float yaw)
    {
        Camera.main.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(pitch, yaw, 0f));
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null)
        {
            view.AlignViewToObject(Camera.main.transform);
        }

        return "ok";
    }

    private static int CountNamed(Transform root, string prefix)
    {
        int count = 0;
        if (root != null)
        {
            foreach (Transform child in root)
            {
                if (child.name.StartsWith(prefix))
                {
                    count++;
                }
            }
        }

        return count;
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

    private static Vector2[] Circle(Vector2 centre, float radius, int count)
    {
        var points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float a = i / (float)count * Mathf.PI * 2f;
            float r = radius * (0.85f + 0.3f * Mathf.PerlinNoise(i * 0.7f, 1.3f));
            points[i] = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }

        return points;
    }

    private static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        switch (value)
        {
            case float f: property.floatValue = f; break;
            case int i when property.propertyType == SerializedPropertyType.Enum: property.enumValueIndex = i; break;
            case int i: property.intValue = i; break;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddGrass(Terrain terrain)
    {
        List<GameObject> grass = GrassSetup.EnsureAssets();
        GrassSetup.AddTo(terrain, grass);
        GrassSetup.FillByGround(terrain, grass, GrassSetup.DefaultFill(terrain.terrainData), 3);
    }
}
