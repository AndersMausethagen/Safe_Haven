using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>GameObject › Safe Haven › World: makes a river, path, cliff, ravine or area (in the look of the terrain in
/// view: the Mossy Forest of the raids' dark forest or the Forest Lake of the Haven, see <see cref="WorldLook"/>) under the
/// scene's "World" object and starts drawing it. Safe Haven › World › Build Now / Rebuild Everything build from the menu.</summary>
public static class WorldMenus
{
    private const string RootName = "World";

    // One item per feature, in the look of the terrain in the Scene view (World Tools › Build picks a look on purpose).
    [MenuItem("GameObject/Safe Haven/World/River", false, 10)]
    private static void River() => MakeRiver(LookInView());

    public static RiverFeature MakeRiver(WorldLook look)
    {
        var river = Create<RiverFeature>("River");
        river.Configure(WorldAssets.RiverWater(), look.RiverBed, look.RiverBank, look.Palette("River Stones"), look.Palette("Riverbank"));
        return river;
    }

    [MenuItem("GameObject/Safe Haven/World/Path", false, 11)]
    private static void PathMenu() => MakePath(LookInView());

    public static PathFeature MakePath(WorldLook look)
    {
        var path = Create<PathFeature>("Path");
        path.Configure(look.PathGround, null);
        return path;
    }

    [MenuItem("GameObject/Safe Haven/World/Cliff", false, 12)]
    private static void Cliff() => MakeCliff(LookInView());

    public static CliffFeature MakeCliff(WorldLook look)
    {
        // No crag pieces by default: the kit's slabs stacked along a face looked placed, not grown, and took longer to
        // fix by hand than they saved. The face is the shaped terrain in Cliff Rock (tessellated) with rubble at its foot;
        // a Kit can still be set on the cliff for pieces.
        var cliff = Create<CliffFeature>("Cliff");
        cliff.Configure(null, look.Palette("Rubble"), TerrainRock.CliffRock != null ? TerrainRock.CliffRock : look.RiverBed);
        return cliff;
    }

    [MenuItem("GameObject/Safe Haven/World/Ravine", false, 13)]
    private static void Ravine() => MakeRavine(LookInView());

    public static RavineFeature MakeRavine(WorldLook look)
    {
        var ravine = Create<RavineFeature>("Ravine");
        ravine.Configure(null, look.Palette("Rubble"), TerrainRock.CliffRock != null ? TerrainRock.CliffRock : look.RockGround, // no crag pieces, as cliffs look.Ground(look == WorldLook.ForestLake ? "Haven Rock" : "Cedar Soil"),
            look.Ground(look == WorldLook.ForestLake ? "Haven Lush Moss" : "Cedar Moss"));
        return ravine;
    }

    [MenuItem("GameObject/Safe Haven/World/Forest Area", false, 30)]
    private static void ForestMenu() => MakeForest(LookInView());

    /// <summary>The look's forest: the Mossy Forest's dark forest (its floor painted with the Misty Woods biome) or the
    /// Haven's open grove.</summary>
    public static BiomeArea MakeForest(WorldLook look)
    {
        bool haven = look == WorldLook.ForestLake;
        var area = Create<BiomeArea>(haven ? "Haven Grove" : "Dark Forest");
        area.Configure(haven ? HavenGroveLayers() : DarkForestLayers(), haven ? null : Biome("Misty Woods Floor"));
        return area;
    }

    [MenuItem("GameObject/Safe Haven/World/Biome Area", false, 32)]
    private static void BiomeAreaMenu() => MakeBiomeArea(BiomesFor(LookInView()).FirstOrDefault());

    /// <summary>The look of the terrain under the middle of the Scene view (the Mossy Forest when there is none).</summary>
    public static WorldLook LookInView()
    {
        SceneView view = SceneView.lastActiveSceneView;
        return WorldLook.Of(view != null ? WorldGround.TerrainAt(new Vector2(view.pivot.x, view.pivot.z)) : null);
    }

    /// <summary>An area painted with a biome (ground, plants and props, no trees of its own): draw its loop on the ground.</summary>
    public static BiomeArea MakeBiomeArea(BiomePreset biome)
    {
        var area = Create<BiomeArea>(biome != null ? biome.name : "Biome Area");
        area.Configure(new List<BiomeArea.Layer>(), biome);
        return area;
    }

    /// <summary>The biome presets of a look (and those for any look), by name.</summary>
    public static List<BiomePreset> BiomesFor(WorldLook look) =>
        AssetDatabase.FindAssets("t:" + nameof(BiomePreset))
            .Select(g => AssetDatabase.LoadAssetAtPath<BiomePreset>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(b => b != null && (string.IsNullOrEmpty(b.look) || look == null || b.look == look.Name))
            .OrderBy(b => b.name).ToList();

    private static BiomePreset Biome(string name) => AssetDatabase.LoadAssetAtPath<BiomePreset>($"{BiomeBrush.Folder}/{name}.asset");

    [MenuItem("GameObject/Safe Haven/World/Ground Paint/Haven Meadow", false, 40)]
    private static void PaintMeadow() => Paint(WorldLook.ForestLake, "Haven Meadow");

    [MenuItem("GameObject/Safe Haven/World/Ground Paint/Haven Old-Growth", false, 41)]
    private static void PaintOldGrowth() => Paint(WorldLook.ForestLake, "Old-Growth Forest");

    [MenuItem("GameObject/Safe Haven/World/Ground Paint/Raid Dark Forest", false, 42)]
    private static void PaintDarkForest() => Paint(WorldLook.MossyForest, "Dark Forest");

    /// <summary>A Ground Paint Area with a ready-made look; draw its loop on the ground.</summary>
    private static void Paint(WorldLook look, string rules) => MakeGroundPaint(look, rules);

    public static GroundPaintArea MakeGroundPaint(WorldLook look, string rules)
    {
        var area = Create<GroundPaintArea>("Ground Paint (" + rules + ")");
        area.Configure(look.Rules(rules));
        return area;
    }

    [MenuItem("Safe Haven/World/Build Now")]
    private static void BuildNow() => WorldBuilder.BuildChanged(false);

    [MenuItem("Safe Haven/World/Rebuild Everything")]
    private static void RebuildAll() => WorldBuilder.BuildChanged(true);

    /// <summary>Ground painted (and grass grown or cleared) by hand over a feature's stays as it is through every build.
    /// This lets the features paint it again: under the selected features, or on the whole terrain when none is selected.</summary>
    [MenuItem("Safe Haven/World/Give Hand Paint Back to Features")]
    private static void ReleaseHandPaint()
    {
        var features = new List<WorldFeature>();
        foreach (GameObject selected in Selection.gameObjects)
        {
            features.AddRange(selected.GetComponentsInParent<WorldFeature>());
        }

        if (features.Count == 0 && !EditorUtility.DisplayDialog("Give hand paint back",
                "No feature is selected. Let the features repaint all hand-painted ground on every terrain?", "Repaint all", "Cancel"))
        {
            return;
        }

        int released = 0;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            WorldTerrain world = terrain.GetComponent<WorldTerrain>();
            if (world == null || world.Stash == null || (world.Stash.HandPaintedCount == 0 && world.Stash.HandKeptDetailCount == 0))
            {
                continue;
            }

            Undo.RecordObject(world.Stash, "Give Hand Paint Back");
            int before = released;
            if (features.Count == 0)
            {
                released += world.Stash.ReleaseHandPainted(null) + world.Stash.ReleaseHandKeptDetails(null);
            }
            else
            {
                TerrainGrid grid = TerrainGrid.Alphas(terrain);
                foreach (WorldFeature feature in features)
                {
                    Bounds bounds = feature.WorldBounds();
                    Vector2 low = grid.GridPosition(new Vector2(bounds.min.x, bounds.min.z));
                    Vector2 high = grid.GridPosition(new Vector2(bounds.max.x, bounds.max.z));
                    var min = new Vector2Int(Mathf.FloorToInt(low.x), Mathf.FloorToInt(low.y));
                    var max = new Vector2Int(Mathf.CeilToInt(high.x), Mathf.CeilToInt(high.y));
                    released += world.Stash.ReleaseHandPainted(new RectInt(min, max - min + Vector2Int.one));
                    TerrainGrid details = TerrainGrid.Details(terrain);
                    Vector2 dLow = details.GridPosition(new Vector2(bounds.min.x, bounds.min.z));
                    Vector2 dHigh = details.GridPosition(new Vector2(bounds.max.x, bounds.max.z));
                    var dMin = new Vector2Int(Mathf.FloorToInt(dLow.x), Mathf.FloorToInt(dLow.y));
                    var dMax = new Vector2Int(Mathf.CeilToInt(dHigh.x), Mathf.CeilToInt(dHigh.y));
                    released += world.Stash.ReleaseHandKeptDetails(new RectInt(dMin, dMax - dMin + Vector2Int.one));
                }
            }

            if (released > before)
            {
                EditorUtility.SetDirty(world.Stash);
                WorldBuilder.RebuildTerrain(terrain);
            }
        }

        Debug.Log($"World Builder: gave {released} hand-painted ground and grass cell(s) back to the features.");
    }

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
        GameObject found = SceneFolders.Find(scene, RootName);
        if (found != null)
        {
            return found.transform;
        }

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create World");
        SceneFolders.Place(root);
        return root.transform;
    }

    private static Vector3 SceneCentre()
    {
        SceneView view = SceneView.lastActiveSceneView;
        Vector3 pivot = view != null ? view.pivot : Vector3.zero;
        return WorldGround.TryGround(new Vector2(pivot.x, pivot.z), out Vector3 ground, out _) ? ground : pivot;
    }

    /// <summary>The Mossy Forest (the raids' dark forest): dense, clumped cedars with young cedars and dead wood between
    /// them, mossy logs and stumps, ferns and shrubs, mossy boulders.</summary>
    public static List<BiomeArea.Layer> DarkForestLayers()
    {
        WorldLook look = WorldLook.MossyForest;
        return new List<BiomeArea.Layer>
        {
            new BiomeArea.Layer
            {
                name = "Trees", palette = look.Palette("Trees"), density = 5f, slope = new Vector2(0f, 35f),
                awayFromWater = 2f, awayFromPaths = 1.5f, awayFromCliffs = 2f, clumping = 0.55f, clumpSize = 22f, edgeFade = 6f,
            },
            new BiomeArea.Layer
            {
                name = "Young trees", palette = look.Palette("Young Trees"), density = 1.5f, slope = new Vector2(0f, 32f),
                awayFromWater = 1.5f, awayFromPaths = 1.2f, awayFromCliffs = 1f, clumping = 0.6f, clumpSize = 14f, edgeFade = 4f,
            },
            new BiomeArea.Layer
            {
                name = "Logs and stumps", palette = look.Palette("Forest Floor"), density = 1.2f, slope = new Vector2(0f, 30f),
                awayFromWater = 1f, awayFromPaths = 1f, awayFromCliffs = 1f, clumping = 0.3f, clumpSize = 15f, edgeFade = 3f,
            },
            new BiomeArea.Layer
            {
                name = "Ferns and shrubs", palette = look.Palette("Undergrowth"), density = 6f, slope = new Vector2(0f, 32f),
                awayFromWater = 0.6f, awayFromPaths = 0.8f, awayFromCliffs = 0.5f, clumping = 0.7f, clumpSize = 9f, edgeFade = 2f,
            },
            new BiomeArea.Layer
            {
                name = "Boulders", palette = look.Palette("Boulders"), density = 0.4f, slope = new Vector2(0f, 40f),
                awayFromWater = 0.5f, awayFromPaths = 1f, awayFromCliffs = 0f, clumping = 0.5f, clumpSize = 20f, edgeFade = 2f,
            },
        };
    }

    /// <summary>The Forest Lake (the light, green Haven): open groves of sugar maples and a few cedars, saplings, mossy logs
    /// and stumps between them, lake boulders, and ferns by the water.</summary>
    public static List<BiomeArea.Layer> HavenGroveLayers()
    {
        WorldLook look = WorldLook.ForestLake;
        return new List<BiomeArea.Layer>
        {
            new BiomeArea.Layer
            {
                name = "Trees", palette = look.Palette("Trees"), density = 1.2f, slope = new Vector2(0f, 30f),
                awayFromWater = 3f, awayFromPaths = 2.5f, awayFromCliffs = 3f, clumping = 0.6f, clumpSize = 25f, edgeFade = 8f,
            },
            new BiomeArea.Layer
            {
                name = "Saplings", palette = look.Palette("Young Trees"), density = 0.6f, slope = new Vector2(0f, 30f),
                awayFromWater = 2f, awayFromPaths = 2f, awayFromCliffs = 1.5f, clumping = 0.6f, clumpSize = 16f, edgeFade = 5f,
            },
            new BiomeArea.Layer
            {
                name = "Logs and stumps", palette = look.Palette("Forest Floor"), density = 0.6f, slope = new Vector2(0f, 30f),
                awayFromWater = 1f, awayFromPaths = 1.5f, awayFromCliffs = 1f, clumping = 0.4f, clumpSize = 18f, edgeFade = 3f,
            },
            new BiomeArea.Layer
            {
                name = "Boulders", palette = look.Palette("Boulders"), density = 0.3f, slope = new Vector2(0f, 35f),
                awayFromWater = 1f, awayFromPaths = 1.5f, awayFromCliffs = 0f, clumping = 0.4f, clumpSize = 20f, edgeFade = 3f,
            },
            new BiomeArea.Layer
            {
                name = "Ferns by the water", palette = look.Palette("Ferns"), density = 8f, slope = new Vector2(0f, 35f),
                awayFromWater = -4f, awayFromPaths = 1f, awayFromCliffs = 0f, clumping = 0.5f, clumpSize = 8f, edgeFade = 1f,
            },
        };
    }
}
