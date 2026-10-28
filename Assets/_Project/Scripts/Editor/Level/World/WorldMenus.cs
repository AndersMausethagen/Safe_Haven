using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>GameObject › Safe Haven › World: makes a river, path, cliff or area (with the default look for the dark
/// raid forests or the green Haven) under the scene's "World" object and starts drawing it. Safe Haven › World ›
/// Build Now / Rebuild Everything build from the menu.</summary>
public static class WorldMenus
{
    private const string RootName = "World";

    [MenuItem("GameObject/Safe Haven/World/River", false, 10)]
    private static void River()
    {
        var river = Create<RiverFeature>("River");
        river.Configure(WorldAssets.RiverWater(), WorldAssets.Layer("Gravel"), WorldAssets.Layer("Muddy Bank"),
                        WorldAssets.Palette("River Stones"), WorldAssets.Palette("Riverbank"));
    }

    [MenuItem("GameObject/Safe Haven/World/River (Haven)", false, 10)]
    private static void HavenRiver()
    {
        var river = Create<RiverFeature>("River");
        river.Configure(WorldAssets.RiverWater(), WorldAssets.Layer("Gravel"), WorldAssets.Layer("Trodden Path"),
                        WorldAssets.Palette("River Stones"), WorldAssets.Palette("Riverbank"));
    }

    [MenuItem("GameObject/Safe Haven/World/Path (Forest)", false, 11)]
    private static void ForestPath() => Create<PathFeature>("Path").Configure(WorldAssets.Layer("Muddy Bank"), null);

    [MenuItem("GameObject/Safe Haven/World/Path (Haven)", false, 12)]
    private static void HavenPath() => Create<PathFeature>("Path").Configure(WorldAssets.Layer("Trodden Path"), null);

    [MenuItem("GameObject/Safe Haven/World/Cliff", false, 13)]
    private static void Cliff() =>
        Create<CliffFeature>("Cliff").Configure(WorldAssets.RockFaces(), WorldAssets.Palette("Cliff Rubble"), WorldAssets.Layer("Gravel"));

    [MenuItem("GameObject/Safe Haven/World/Dark Forest Area", false, 30)]
    private static void DarkForest() => Create<BiomeArea>("Dark Forest").Configure(DarkForestLayers());

    [MenuItem("GameObject/Safe Haven/World/Haven Grove Area", false, 31)]
    private static void HavenGrove() => Create<BiomeArea>("Haven Grove").Configure(HavenGroveLayers());

    [MenuItem("GameObject/Safe Haven/World/Ground Paint (Haven Meadow)", false, 40)]
    private static void PaintMeadow() => Paint("Haven Meadow");

    [MenuItem("GameObject/Safe Haven/World/Ground Paint (Haven Old-Growth)", false, 41)]
    private static void PaintOldGrowth() => Paint("Old-Growth Forest");

    [MenuItem("GameObject/Safe Haven/World/Ground Paint (Raid Dark Forest)", false, 42)]
    private static void PaintDarkForest() => Paint("Dark Forest");

    /// <summary>A Ground Paint Area with a ready-made look; draw its loop on the ground.</summary>
    private static void Paint(string look) => Create<GroundPaintArea>("Ground Paint (" + look + ")").Configure(WorldAssets.PaintRules(look));

    [MenuItem("Safe Haven/World/Build Now")]
    private static void BuildNow() => WorldBuilder.BuildChanged(false);

    [MenuItem("Safe Haven/World/Rebuild Everything")]
    private static void RebuildAll() => WorldBuilder.BuildChanged(true);

    public static T Create<T>(string name) where T : WorldFeature
    {
        WorldAssets.EnsureAll();
        Transform root = Root();
        var go = new GameObject(GameObjectUtility.GetUniqueNameForSibling(root, name), typeof(SplineContainer));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(root, false);
        go.transform.position = SceneCentre();
        var container = go.GetComponent<SplineContainer>();
        container.Spline.Clear();
        var feature = Undo.AddComponent<T>(go);
        WorldFeatureEditor.StartDrawing(feature);
        return feature;
    }

    private static Transform Root()
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go.name == RootName)
            {
                return go.transform;
            }
        }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create World");
        return root.transform;
    }

    private static Vector3 SceneCentre()
    {
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 pivot = view != null ? view.pivot : Vector3.zero;
        return WorldGround.TryGround(new Vector2(pivot.x, pivot.z), out Vector3 ground, out _) ? ground : pivot;
    }

    /// <summary>Dense, clumped spruce with dead trees, stumps, logs and ferns (the Gord-like raid forest).</summary>
    public static List<BiomeArea.Layer> DarkForestLayers() => new List<BiomeArea.Layer>
    {
        new BiomeArea.Layer
        {
            name = "Trees", palette = WorldAssets.Palette("Dark Forest Trees"), density = 5f, slope = new Vector2(0f, 35f),
            awayFromWater = 2f, awayFromPaths = 1.5f, awayFromCliffs = 2f, clumping = 0.55f, clumpSize = 22f, edgeFade = 6f,
        },
        new BiomeArea.Layer
        {
            name = "Stumps and logs", palette = WorldAssets.Palette("Forest Floor Debris"), density = 1.2f, slope = new Vector2(0f, 30f),
            awayFromWater = 1f, awayFromPaths = 1f, awayFromCliffs = 1f, clumping = 0.3f, clumpSize = 15f, edgeFade = 3f,
        },
        new BiomeArea.Layer
        {
            name = "Ferns", palette = WorldAssets.Palette("Ferns"), density = 6f, slope = new Vector2(0f, 32f),
            awayFromWater = 0.6f, awayFromPaths = 0.8f, awayFromCliffs = 0.5f, clumping = 0.7f, clumpSize = 9f, edgeFade = 2f,
        },
        new BiomeArea.Layer
        {
            name = "Boulders", palette = WorldAssets.Palette("Mossy Boulders"), density = 0.4f, slope = new Vector2(0f, 40f),
            awayFromWater = 0.5f, awayFromPaths = 1f, awayFromCliffs = 0f, clumping = 0.5f, clumpSize = 20f, edgeFade = 2f,
        },
    };

    /// <summary>Open oak groves with a few pines, mossy boulders and ferns by the water (the light, green Haven).</summary>
    public static List<BiomeArea.Layer> HavenGroveLayers() => new List<BiomeArea.Layer>
    {
        new BiomeArea.Layer
        {
            name = "Trees", palette = WorldAssets.Palette("Haven Trees"), density = 1.2f, slope = new Vector2(0f, 30f),
            awayFromWater = 3f, awayFromPaths = 2.5f, awayFromCliffs = 3f, clumping = 0.6f, clumpSize = 25f, edgeFade = 8f,
        },
        new BiomeArea.Layer
        {
            name = "Boulders", palette = WorldAssets.Palette("Mossy Boulders"), density = 0.3f, slope = new Vector2(0f, 35f),
            awayFromWater = 1f, awayFromPaths = 1.5f, awayFromCliffs = 0f, clumping = 0.4f, clumpSize = 20f, edgeFade = 3f,
        },
        new BiomeArea.Layer
        {
            name = "Ferns by the water", palette = WorldAssets.Palette("Ferns"), density = 8f, slope = new Vector2(0f, 35f),
            awayFromWater = -4f, awayFromPaths = 1f, awayFromCliffs = 0f, clumping = 0.5f, clumpSize = 8f, edgeFade = 1f,
        },
    };
}
