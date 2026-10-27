using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using Unity.AI.Navigation.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

/// <summary>
/// Builds the world from its <see cref="WorldFeature"/>s (see Docs/LevelDesign.md, "World Builder"). It watches the
/// features and the terrains: when a spline or a setting changes, a feature is deleted, or the ground under one is
/// sculpted by hand, it rebuilds the terrains concerned shortly after (not while the mouse is held).
///
/// A build is non-destructive: every terrain cell a feature touches keeps its ground from before any feature in the
/// terrain's <see cref="WorldTerrainStash"/>, so each build starts again from that ground (hand edits made since the last
/// build are taken into it), applies the features in order (heights, then paint, grass and trees), puts back the
/// ground where no feature reaches any more, then regenerates the features' objects. A build joins the undo step of
/// the edit that caused it, so one undo takes back both (and the builder then has nothing to rebuild).
/// </summary>
[InitializeOnLoad]
public static class WorldBuilder
{
    private const float HeightTolerance = 2.5e-5f; // normalised: ~1.6 steps of the 16-bit heightmap
    private const float PaintTolerance = 2.5f / 255f;
    private const double Delay = 0.35;
    private const double HandEditDelay = 0.8;

    private static double dueAt = -1;
    private static int triggerGroup = -1; // the undo group of the edit that scheduled the build
    private static double ignoreTerrainUntil;
    private static readonly HashSet<Terrain> handEdited = new HashSet<Terrain>();
    private static readonly List<Rect> handEditedAreas = new List<Rect>();

    public static bool Building { get; private set; }

    /// <summary>What the last build did, for the World Terrain Inspector.</summary>
    public static string LastReport { get; private set; } = "";

    static WorldBuilder()
    {
        WorldFeature.Edited += _ => Schedule(Delay);
        Spline.Changed += OnSplineChanged;
        TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
        EditorApplication.hierarchyChanged += () => Schedule(Delay);
        EditorApplication.update += Tick;
        Undo.undoRedoPerformed += () => ignoreTerrainUntil = EditorApplication.timeSinceStartup + 1.0;
    }

    public static void Schedule(double delay)
    {
        if (Building || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        double at = EditorApplication.timeSinceStartup + delay;
        if (dueAt < 0)
        {
            triggerGroup = Undo.GetCurrentGroup();
        }

        dueAt = dueAt < 0 ? at : Math.Max(dueAt, at);
    }

    private static void Tick()
    {
        if (dueAt < 0 || EditorApplication.timeSinceStartup < dueAt)
        {
            return;
        }

        if (GUIUtility.hotControl != 0 || EditorApplication.isCompiling)
        {
            dueAt = EditorApplication.timeSinceStartup + 0.2; // still dragging: wait for the mouse to let go
            return;
        }

        dueAt = -1;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        BuildChanged(false);
    }

    // ------------------------------------------------------------------ watching

    private static void OnSplineChanged(Spline spline, int knot, SplineModification modification)
    {
        if (Building)
        {
            return;
        }

        foreach (WorldFeature feature in Object.FindObjectsByType<WorldFeature>())
        {
            if (feature.Spline != spline)
            {
                continue;
            }

            // Per-knot values stay with their knots.
            Undo.RecordObject(feature, "Edit Spline");
            foreach (KnotValues values in feature.PerKnot)
            {
                if (modification == SplineModification.KnotInserted)
                {
                    values.Insert(knot, spline.Count);
                }
                else if (modification == SplineModification.KnotRemoved)
                {
                    values.Remove(knot, spline.Count);
                }
                else
                {
                    values.Resize(spline.Count);
                }
            }

            Schedule(Delay);
        }
    }

    private static void OnHeightmapChanged(Terrain terrain, RectInt region, bool synched)
    {
        if (Building || EditorApplication.timeSinceStartup < ignoreTerrainUntil || EditorApplication.isPlaying ||
            terrain.GetComponent<WorldTerrain>() == null)
        {
            return;
        }

        TerrainGrid grid = TerrainGrid.Heights(terrain);
        Vector2 a = grid.World(region.xMin, region.yMin), b = grid.World(region.xMax, region.yMax);
        handEdited.Add(terrain);
        handEditedAreas.Add(Rect.MinMaxRect(a.x, a.y, b.x, b.y));
        Schedule(HandEditDelay);
    }

    // ------------------------------------------------------------------ deciding what to build

    /// <summary>Builds what changed since the last build (or everything, when <paramref name="everything"/>). Returns
    /// how many terrains were rebuilt.</summary>
    public static int BuildChanged(bool everything)
    {
        List<WorldFeature> features = LiveFeatures();
        var terrainsToBuild = new List<Terrain>();
        var changedAreas = new List<Rect>(handEditedAreas);
        var areasToBuild = new HashSet<BiomeArea>();
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain.terrainData == null)
            {
                continue;
            }

            Rect terrainRect = FlatRect(terrain);
            List<WorldFeature> here = features.Where(f => FlatRect(f.WorldBounds()).Overlaps(terrainRect)).ToList();
            var world = terrain.GetComponent<WorldTerrain>();
            if (world == null)
            {
                if (here.Count == 0)
                {
                    continue;
                }

                world = Undo.AddComponent<WorldTerrain>(terrain.gameObject);
            }

            if (!everything && !world.BuildAutomatically && !handEdited.Contains(terrain))
            {
                continue;
            }

            bool shape = everything || (handEdited.Contains(terrain) && here.Any(f => f.ShapesTerrain));
            foreach (WorldTerrain.Built built in world.BuiltFeatures)
            {
                bool gone = built.feature == null || !here.Contains(built.feature);
                bool changed = !gone && built.hash != Hash(built.feature);
                if (!gone && !changed)
                {
                    continue;
                }

                changedAreas.Add(FlatRect(built.bounds));
                if (built.feature is BiomeArea area && !gone)
                {
                    areasToBuild.Add(area);
                }
                else if (gone ? built.bounds.size != Vector3.zero : built.feature.ShapesTerrain)
                {
                    shape = true;
                }
            }

            foreach (WorldFeature feature in here)
            {
                if (world.BuiltFeatures.Any(b => b.feature == feature))
                {
                    continue;
                }

                changedAreas.Add(FlatRect(feature.WorldBounds()));
                if (feature is BiomeArea area)
                {
                    areasToBuild.Add(area);
                }
                else
                {
                    shape = true;
                }
            }

            if (shape)
            {
                terrainsToBuild.Add(terrain);
            }
        }

        // Forests regrow where the ground changed under them.
        foreach (BiomeArea area in features.OfType<BiomeArea>())
        {
            Rect rect = FlatRect(area.WorldBounds());
            if (everything || changedAreas.Any(r => r.Overlaps(rect)))
            {
                areasToBuild.Add(area);
            }
        }

        handEdited.Clear();
        handEditedAreas.Clear();
        if (terrainsToBuild.Count == 0 && areasToBuild.Count == 0)
        {
            triggerGroup = -1;
            return 0;
        }

        Build(terrainsToBuild, areasToBuild.ToList(), features);
        return terrainsToBuild.Count;
    }

    /// <summary>Rebuilds one terrain with all its features and the areas on it (after Auto-Paint, for instance).</summary>
    public static void RebuildTerrain(Terrain terrain)
    {
        List<WorldFeature> features = LiveFeatures();
        Rect rect = FlatRect(terrain);
        List<BiomeArea> areas = features.OfType<BiomeArea>().Where(a => FlatRect(a.WorldBounds()).Overlaps(rect)).ToList();
        if (terrain.GetComponent<WorldTerrain>() == null)
        {
            Undo.AddComponent<WorldTerrain>(terrain.gameObject);
        }

        Build(new List<Terrain> { terrain }, areas, features);
    }

    /// <summary>Enabled features in the open scenes with at least two knots.</summary>
    public static List<WorldFeature> LiveFeatures() =>
        Object.FindObjectsByType<WorldFeature>()
              .Where(f => f.isActiveAndEnabled && f.Spline != null && f.Spline.Count >= 2 && !EditorUtility.IsPersistent(f))
              .OrderBy(f => f.Order).ThenBy(PathOf, StringComparer.Ordinal).ToList();

    /// <summary>Changes whenever anything the build reads from the feature changes: its settings, spline and place.</summary>
    public static string Hash(WorldFeature feature)
    {
        if (feature == null)
        {
            return "";
        }

        string text = EditorJsonUtility.ToJson(feature) + EditorJsonUtility.ToJson(feature.Container) + feature.transform.localToWorldMatrix;
        return Hash128.Compute(text).ToString();
    }

    // ------------------------------------------------------------------ building

    private static void Build(List<Terrain> terrains, List<BiomeArea> areas, List<WorldFeature> all)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Building = true;
        int group = triggerGroup;
        triggerGroup = -1;
        if (group < 0)
        {
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build World");
        }
        var report = new List<string>();
        try
        {
            List<WorldFeature> shaping = all.Where(f => f.ShapesTerrain &&
                                                        terrains.Any(t => FlatRect(f.WorldBounds()).Overlaps(FlatRect(t)))).ToList();
            List<FeatureBuilder> builders = shaping.Select(FeatureBuilder.For).Where(b => b != null).ToList();
            var glue = new GroundGlue(terrains, all);

            // Heights.
            var layers = new List<HeightLayer>();
            foreach (Terrain terrain in terrains)
            {
                WorldTerrain world = terrain.GetComponent<WorldTerrain>();
                EnsureStash(world);
                Record(terrain, world, "Build World");
                AddMissingLayers(terrain, builders);
                world.Stash.Prepare(terrain.terrainData.heightmapResolution, terrain.terrainData.alphamapResolution,
                                    terrain.terrainData.alphamapLayers, terrain.terrainData.detailResolution);
                layers.Add(ReadHeights(terrain, world.Stash, shaping));
            }

            var sampler = new WorldSampler(layers);
            foreach (FeatureBuilder builder in builders)
            {
                builder.Prepare(sampler);
                foreach (HeightLayer layer in layers)
                {
                    builder.Heights(layer);
                }
            }

            foreach (HeightLayer layer in layers)
            {
                WorldTerrain world = layer.Terrain.GetComponent<WorldTerrain>();
                MakeRoom(layer, world.Stash);
                glue.Record(layer);
                WriteHeights(layer, world.Stash);
            }

            // Paint, grass and trees.
            foreach (Terrain terrain in terrains)
            {
                WorldTerrainStash stash = terrain.GetComponent<WorldTerrain>().Stash;
                Paint(terrain, stash, shaping, builders);
                ClearDetails(terrain, stash, shaping, builders);
                ClearTrees(terrain, stash, builders);
                terrain.Flush();
            }

            glue.Apply(terrains);

            // Objects: the shaping features on the rebuilt terrains, then the areas.
            var context = new BuildContext();
            context.Builders.AddRange(builders);
            foreach (FeatureBuilder builder in builders)
            {
                RegenerateObjects(builder, context);
            }

            foreach (WorldFeature feature in all.Where(f => f.ShapesTerrain && !shaping.Contains(f)))
            {
                FeatureBuilder other = FeatureBuilder.For(feature);
                other.Prepare(sampler);
                context.Builders.Add(other); // keep-away distances for the forests
            }

            int placedSaveables = 0;
            foreach (BiomeArea area in areas.OrderBy(a => a.Order))
            {
                FeatureBuilder builder = FeatureBuilder.For(area);
                builder.Prepare(sampler);
                RegenerateObjects(builder, context);
                placedSaveables += area.GetComponentsInChildren<SaveableEntity>(true).Length;
            }

            if (placedSaveables > 0)
            {
                foreach (UnityEngine.SceneManagement.Scene scene in areas.Select(a => a.gameObject.scene).Distinct())
                {
                    SaveableEntityTools.AssignSceneIds(scene);
                }
            }

            RecordBuilt(all, terrains, areas);
            foreach (Terrain terrain in terrains)
            {
                if (terrain.GetComponent<GrassTerrainColour>() != null)
                {
                    GrassSetup.BakeGroundColour(terrain);
                }

                EditorUtility.SetDirty(terrain.terrainData);
                EditorUtility.SetDirty(terrain.GetComponent<WorldTerrain>().Stash);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            }

            foreach (BiomeArea area in areas)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(area.gameObject.scene);
            }

            report.Add($"{terrains.Count} terrain(s), {builders.Count} feature(s), {areas.Count} area(s) in {watch.ElapsedMilliseconds} ms");
            if (terrains.Any(t => t.GetComponent<WorldTerrain>().RebakeNavMeshAfterBuild))
            {
                report.Add(RebakeNavMesh());
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            report.Add("failed: " + exception.Message);
        }
        finally
        {
            Building = false;
            Undo.CollapseUndoOperations(group);
            ignoreTerrainUntil = EditorApplication.timeSinceStartup + 0.5;
        }

        LastReport = string.Join("; ", report);
        Debug.Log("World Builder: " + LastReport);
    }

    private static void Record(Terrain terrain, WorldTerrain world, string name)
    {
        var objects = new List<Object> { terrain.terrainData, world, world.Stash, terrain.transform };
        objects.AddRange(terrain.terrainData.alphamapTextures);
        Undo.RegisterCompleteObjectUndo(objects.ToArray(), name);
    }

    /// <summary>Records what each terrain was built from: everything on the rebuilt terrains, and the rebuilt areas on
    /// the others (a terrain left for a manual build keeps its record, so its changes are still seen as pending).</summary>
    private static void RecordBuilt(List<WorldFeature> all, List<Terrain> terrains, List<BiomeArea> areas)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            var world = terrain.GetComponent<WorldTerrain>();
            if (world == null)
            {
                continue;
            }

            Undo.RecordObject(world, "Build World");
            Rect rect = FlatRect(terrain);
            bool rebuilt = terrains.Contains(terrain);
            if (rebuilt)
            {
                world.BuiltFeatures.Clear();
            }

            foreach (WorldFeature feature in all.Where(f => FlatRect(f.WorldBounds()).Overlaps(rect)))
            {
                if (!rebuilt && !(feature is BiomeArea area && areas.Contains(area)))
                {
                    continue;
                }

                world.BuiltFeatures.RemoveAll(b => b.feature == feature);
                world.BuiltFeatures.Add(new WorldTerrain.Built { feature = feature, hash = Hash(feature), bounds = feature.WorldBounds() });
            }
        }
    }

    private static void RegenerateObjects(FeatureBuilder builder, BuildContext context)
    {
        Transform old = builder.Feature.GeneratedRoot(false);
        if (builder.Feature.FreezeObjects && old != null)
        {
            return; // hand-adjusted: keep them
        }

        if (old != null)
        {
            Undo.DestroyObjectImmediate(old.gameObject);
        }

        Transform root = builder.Feature.GeneratedRoot(true);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Build World");
        builder.Objects(root, context);
    }

    // ------------------------------------------------------------------ heights

    private static HeightLayer ReadHeights(Terrain terrain, WorldTerrainStash stash, List<WorldFeature> shaping)
    {
        TerrainData data = terrain.terrainData;
        TerrainGrid grid = TerrainGrid.Heights(terrain);
        RectInt rect = WorkRect(grid, terrain, shaping, stash.HeightCells, data.heightmapResolution);
        var layer = new HeightLayer(terrain, rect);
        if (rect.width == 0 || rect.height == 0)
        {
            return layer;
        }

        float[,] heights = data.GetHeights(rect.x, rect.y, rect.width, rect.height);
        float scale = data.size.y, offset = terrain.transform.position.y;
        int resolution = data.heightmapResolution;
        for (int y = 0; y < rect.height; y++)
        {
            for (int x = 0; x < rect.width; x++)
            {
                int i = y * rect.width + x;
                float current = heights[y, x];
                float ground = current;
                if (stash.TryGetHeight((rect.y + y) * resolution + rect.x + x, out float baseHeight, out float last))
                {
                    ground = AbsorbHeight(baseHeight, last, current);
                }

                layer.Original[i] = current * scale + offset;
                layer.Base[i] = ground * scale + offset;
                layer.Values[i] = layer.Base[i];
            }
        }

        return layer;
    }

    /// <summary>The ground under a feature: its kept base, moved by any hand edit made since the last build (the
    /// difference between what is there now and what the builder last wrote).</summary>
    public static float AbsorbHeight(float baseHeight, float last, float current) =>
        Mathf.Abs(current - last) > HeightTolerance ? baseHeight + (current - last) : baseHeight;

    /// <summary>Gives the terrain more room above or below when a feature needs it (a deep river bed, a tall cliff):
    /// moves and stretches it so the ground stays exactly where it was.</summary>
    private static void MakeRoom(HeightLayer layer, WorldTerrainStash stash)
    {
        Terrain terrain = layer.Terrain;
        TerrainData data = terrain.terrainData;
        float bottom = terrain.transform.position.y, top = bottom + data.size.y;
        float low = float.MaxValue, high = float.MinValue;
        for (int i = 0; i < layer.Values.Length; i++)
        {
            if (layer.Touched[i])
            {
                low = Mathf.Min(low, layer.Values[i]);
                high = Mathf.Max(high, layer.Values[i]);
            }
        }

        if (low == float.MaxValue || (low >= bottom + 0.05f && high <= top - 0.05f))
        {
            return;
        }

        float newBottom = Mathf.Min(bottom, Mathf.Floor(low - 2f));
        float newTop = Mathf.Max(top, Mathf.Ceil(high + 2f));
        float newHeight = newTop - newBottom;
        int resolution = data.heightmapResolution;
        float[,] all = data.GetHeights(0, 0, resolution, resolution);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                all[y, x] = (all[y, x] * data.size.y + bottom - newBottom) / newHeight;
            }
        }

        stash.RemapHeights(data.size.y / newHeight, (bottom - newBottom) / newHeight);
        data.size = new Vector3(data.size.x, newHeight, data.size.z);
        terrain.transform.position = new Vector3(terrain.transform.position.x, newBottom, terrain.transform.position.z);
        data.SetHeights(0, 0, all);
        Debug.Log($"World Builder: {terrain.name} now reaches {newBottom:0.#} to {newTop:0.#} m (was {bottom:0.#} to {top:0.#}) to fit the features; the ground did not move.");
    }

    private static void WriteHeights(HeightLayer layer, WorldTerrainStash stash)
    {
        RectInt rect = layer.Rect;
        if (rect.width == 0 || rect.height == 0)
        {
            return;
        }

        Terrain terrain = layer.Terrain;
        TerrainData data = terrain.terrainData;
        float scale = data.size.y, offset = terrain.transform.position.y;
        int resolution = data.heightmapResolution;
        var heights = new float[rect.height, rect.width];
        var released = new HashSet<int>();
        for (int y = 0; y < rect.height; y++)
        {
            for (int x = 0; x < rect.width; x++)
            {
                int i = y * rect.width + x;
                int cell = (rect.y + y) * resolution + rect.x + x;
                float baseHeight = Mathf.Clamp01((layer.Base[i] - offset) / scale);
                if (layer.Touched[i])
                {
                    float value = Mathf.Clamp01((layer.Values[i] - offset) / scale);
                    heights[y, x] = value;
                    stash.SetHeight(cell, baseHeight, value);
                }
                else
                {
                    heights[y, x] = baseHeight; // no feature here (any more): the ground as it was
                    if (stash.TryGetHeight(cell, out _, out _))
                    {
                        released.Add(cell);
                    }
                }
            }
        }

        stash.RemoveHeights(released);
        data.SetHeightsDelayLOD(rect.x, rect.y, heights);
        data.SyncHeightmap();
    }

    // ------------------------------------------------------------------ paint

    private static void AddMissingLayers(Terrain terrain, List<FeatureBuilder> builders)
    {
        TerrainData data = terrain.terrainData;
        var layers = new List<TerrainLayer>(data.terrainLayers);
        int before = layers.Count;
        foreach (TerrainLayer layer in builders.SelectMany(b => b.PaintLayers).Distinct())
        {
            if (!layers.Contains(layer))
            {
                layers.Add(layer);
            }
        }

        if (layers.Count != before)
        {
            data.terrainLayers = layers.ToArray();
            if (layers.Count > 4 && before <= 4)
            {
                Debug.LogWarning($"World Builder: {terrain.name} now has {layers.Count} ground layers; the terrain draws in two passes above 4.");
            }
        }
    }

    private static void Paint(Terrain terrain, WorldTerrainStash stash, List<WorldFeature> shaping, List<FeatureBuilder> builders)
    {
        TerrainData data = terrain.terrainData;
        TerrainGrid grid = TerrainGrid.Alphas(terrain);
        RectInt rect = WorkRect(grid, terrain, shaping, stash.AlphaCells, data.alphamapResolution);
        if (rect.width == 0 || rect.height == 0 || data.alphamapLayers == 0)
        {
            return;
        }

        var layer = new PaintLayer(terrain, rect);
        int layers = layer.Layers;
        float[,,] maps = data.GetAlphamaps(rect.x, rect.y, rect.width, rect.height);
        int resolution = data.alphamapResolution;
        var baseWeights = new float[layers];
        var lastWeights = new float[layers];
        var bases = new float[layer.Values.Length];
        for (int y = 0; y < rect.height; y++)
        {
            for (int x = 0; x < rect.width; x++)
            {
                int offset = (y * rect.width + x) * layers;
                bool kept = stash.TryGetAlpha((rect.y + y) * resolution + rect.x + x, baseWeights, lastWeights);
                bool handPainted = false;
                for (int l = 0; l < layers && kept; l++)
                {
                    handPainted |= Mathf.Abs(maps[y, x, l] - lastWeights[l]) > PaintTolerance;
                }

                for (int l = 0; l < layers; l++)
                {
                    // Hand painting since the last build becomes the new ground under the feature.
                    float ground = kept && !handPainted ? baseWeights[l] : maps[y, x, l];
                    bases[offset + l] = ground;
                    layer.Values[offset + l] = ground;
                }
            }
        }

        foreach (FeatureBuilder builder in builders)
        {
            builder.Paint(layer);
        }

        var released = new HashSet<int>();
        for (int y = 0; y < rect.height; y++)
        {
            for (int x = 0; x < rect.width; x++)
            {
                int index = y * rect.width + x;
                int offset = index * layers;
                int cell = (rect.y + y) * resolution + rect.x + x;
                bool touched = layer.Touched[index];
                for (int l = 0; l < layers; l++)
                {
                    baseWeights[l] = bases[offset + l];
                    lastWeights[l] = touched ? layer.Values[offset + l] : bases[offset + l];
                    maps[y, x, l] = lastWeights[l];
                }

                if (touched)
                {
                    stash.SetAlpha(cell, baseWeights, lastWeights);
                }
                else if (stash.TryGetAlpha(cell, baseWeights, lastWeights))
                {
                    released.Add(cell);
                }
            }
        }

        stash.RemoveAlphas(released);
        data.SetAlphamaps(rect.x, rect.y, maps);
    }

    // ------------------------------------------------------------------ grass and trees

    private static void ClearDetails(Terrain terrain, WorldTerrainStash stash, List<WorldFeature> shaping, List<FeatureBuilder> builders)
    {
        TerrainData data = terrain.terrainData;
        int resolution = data.detailResolution;
        int prototypes = data.detailPrototypes.Length;
        if (resolution == 0 || prototypes == 0)
        {
            return;
        }

        TerrainGrid grid = TerrainGrid.Details(terrain);
        int square = resolution * resolution;
        RectInt rect = WorkRect(grid, terrain, shaping, stash.DetailCells.Select(key => key % square), resolution);
        if (rect.width == 0 || rect.height == 0)
        {
            return;
        }

        var clear = new ClearLayer(terrain, rect);
        foreach (FeatureBuilder builder in builders)
        {
            builder.ClearDetails(clear);
        }

        var released = new HashSet<int>();
        for (int prototype = 0; prototype < prototypes; prototype++)
        {
            int[,] cells = data.GetDetailLayer(rect.x, rect.y, rect.width, rect.height, prototype);
            bool changed = false;
            for (int y = 0; y < rect.height; y++)
            {
                for (int x = 0; x < rect.width; x++)
                {
                    int key = prototype * square + (rect.y + y) * resolution + rect.x + x;
                    bool kept = stash.TryGetDetail(key, out int value);
                    if (clear.Clear[y * rect.width + x])
                    {
                        if (cells[y, x] != 0)
                        {
                            stash.SetDetail(key, cells[y, x]); // planted (or painted since) here: keep it for later
                            cells[y, x] = 0;
                            changed = true;
                        }
                    }
                    else if (kept)
                    {
                        if (cells[y, x] == 0)
                        {
                            cells[y, x] = value; // the feature moved away: the grass grows back
                            changed = true;
                        }

                        released.Add(key);
                    }
                }
            }

            if (changed)
            {
                data.SetDetailLayer(rect.x, rect.y, prototype, cells);
            }
        }

        stash.RemoveDetails(released);
    }

    private static void ClearTrees(Terrain terrain, WorldTerrainStash stash, List<FeatureBuilder> builders)
    {
        TerrainData data = terrain.terrainData;
        if (data.treeInstanceCount == 0 && stash.Trees.Count == 0)
        {
            return;
        }

        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        Vector2 World(Vector3 normalised) => new Vector2(origin.x + normalised.x * size.x, origin.z + normalised.z * size.z);
        bool Cleared(Vector3 normalised) => builders.Any(b => b.ClearsTree(World(normalised)));

        var kept = new List<TreeInstance>();
        var stashed = new List<WorldTerrainStash.StashedTree>();
        foreach (TreeInstance tree in data.treeInstances)
        {
            if (Cleared(tree.position))
            {
                stashed.Add(new WorldTerrainStash.StashedTree(tree));
            }
            else
            {
                kept.Add(tree);
            }
        }

        foreach (WorldTerrainStash.StashedTree tree in stash.Trees)
        {
            if (Cleared(tree.position))
            {
                stashed.Add(tree);
            }
            else if (tree.prototypeIndex < data.treePrototypes.Length)
            {
                kept.Add(tree.ToTree()); // the feature moved away: the tree comes back
            }
        }

        stash.Trees.Clear();
        stash.Trees.AddRange(stashed);
        data.SetTreeInstances(kept.ToArray(), true);
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>The grid rectangle a build works on: every shaping feature's footprint on this terrain plus every cell
    /// kept in the stash (so cells a feature left get their ground back).</summary>
    private static RectInt WorkRect(TerrainGrid grid, Terrain terrain, List<WorldFeature> shaping, IEnumerable<int> kept, int resolution)
    {
        int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
        Rect terrainRect = FlatRect(terrain);
        foreach (WorldFeature feature in shaping)
        {
            Rect rect = FlatRect(feature.WorldBounds());
            if (!rect.Overlaps(terrainRect))
            {
                continue;
            }

            RectInt cells = grid.Cells(rect);
            if (cells.width == 0 || cells.height == 0)
            {
                continue;
            }

            xMin = Mathf.Min(xMin, cells.xMin);
            yMin = Mathf.Min(yMin, cells.yMin);
            xMax = Mathf.Max(xMax, cells.xMax);
            yMax = Mathf.Max(yMax, cells.yMax);
        }

        foreach (int cell in kept)
        {
            int x = cell % resolution, y = cell / resolution;
            xMin = Mathf.Min(xMin, x);
            yMin = Mathf.Min(yMin, y);
            xMax = Mathf.Max(xMax, x + 1);
            yMax = Mathf.Max(yMax, y + 1);
        }

        if (xMin == int.MaxValue)
        {
            return new RectInt(0, 0, 0, 0);
        }

        xMin = Mathf.Clamp(xMin, 0, resolution);
        yMin = Mathf.Clamp(yMin, 0, resolution);
        xMax = Mathf.Clamp(xMax, 0, resolution);
        yMax = Mathf.Clamp(yMax, 0, resolution);
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private static void EnsureStash(WorldTerrain world)
    {
        if (world.Stash != null)
        {
            return;
        }

        TerrainData data = world.GetComponent<Terrain>().terrainData;
        string dataPath = AssetDatabase.GetAssetPath(data);
        string folder = string.IsNullOrEmpty(dataPath) ? "Assets/_Project/Data/World" : Path.GetDirectoryName(dataPath).Replace('\\', '/');
        Directory.CreateDirectory(folder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{data.name} World.asset");
        var stash = ScriptableObject.CreateInstance<WorldTerrainStash>();
        AssetDatabase.CreateAsset(stash, path);
        Undo.RecordObject(world, "Build World");
        world.Stash = stash;
    }

    public static Rect FlatRect(Terrain terrain)
    {
        Vector3 p = terrain.transform.position, s = terrain.terrainData.size;
        return new Rect(p.x, p.z, s.x, s.z);
    }

    public static Rect FlatRect(Bounds bounds) => Rect.MinMaxRect(bounds.min.x, bounds.min.z, bounds.max.x, bounds.max.z);

    private static string PathOf(WorldFeature feature)
    {
        string path = feature.name;
        for (Transform t = feature.transform.parent; t != null; t = t.parent)
        {
            path = t.name + "/" + path;
        }

        return path;
    }

    /// <summary>Rebakes every NavMesh Surface in the open scenes (monsters and residents walk the new ground).</summary>
    public static string RebakeNavMesh()
    {
        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>();
        if (surfaces.Length > 0)
        {
            NavMeshAssetManager.instance.StartBakingSurfaces(surfaces);
        }

        return $"rebaking {surfaces.Length} NavMesh surface(s)";
    }

    // ------------------------------------------------------------------ objects on reshaped ground

    /// <summary>Moves objects standing on the ground (props, nodes, buildings) up or down with it when a build reshapes
    /// the ground under them. Objects the features generate, and objects not resting on the ground, are left alone.</summary>
    private sealed class GroundGlue
    {
        private readonly List<(Transform transform, Vector2 xz, float bottom)> candidates = new List<(Transform, Vector2, float)>();
        private readonly Dictionary<Transform, float> moves = new Dictionary<Transform, float>();

        public GroundGlue(List<Terrain> terrains, List<WorldFeature> features)
        {
            var scenes = terrains.Where(t => t.GetComponent<WorldTerrain>().KeepObjectsOnGround).Select(t => t.gameObject.scene).Distinct();
            var owners = new HashSet<Transform>(features.Select(f => f.transform));
            foreach (UnityEngine.SceneManagement.Scene scene in scenes)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Collect(root.transform, owners);
                }
            }
        }

        private void Collect(Transform t, HashSet<Transform> owners)
        {
            if (owners.Contains(t) || t.GetComponent<Terrain>() != null || !t.gameObject.activeInHierarchy)
            {
                return;
            }

            bool grounded = PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject) || t.GetComponent<SaveableEntity>() != null;
            Renderer[] renderers = grounded ? t.GetComponentsInChildren<Renderer>() : null;
            if (grounded && renderers.Length > 0 && t.GetComponentInChildren<WorldFeature>(true) == null &&
                t.GetComponentInChildren<Terrain>(true) == null)
            {
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                // A whole map or village packed into one prefab does not stand on one spot: look inside it instead.
                if (bounds.size.x < 40f && bounds.size.z < 40f)
                {
                    candidates.Add((t, SplineLine.Flat(t.position), bounds.min.y));
                    return; // an object and its children move together
                }
            }

            foreach (Transform child in t)
            {
                Collect(child, owners);
            }
        }

        /// <summary>Notes the height change under each object resting on this layer's ground (within 0.8 m).</summary>
        public void Record(HeightLayer layer)
        {
            foreach (var (transform, xz, bottom) in candidates)
            {
                Vector2 g = layer.Grid.GridPosition(xz);
                int x = Mathf.RoundToInt(g.x), y = Mathf.RoundToInt(g.y);
                if (x < layer.Rect.xMin || y < layer.Rect.yMin || x >= layer.Rect.xMax || y >= layer.Rect.yMax)
                {
                    continue;
                }

                int i = layer.IndexOf(x, y);
                float before = layer.Original[i];
                float after = layer.Touched[i] ? layer.Values[i] : layer.Base[i];
                if (Mathf.Abs(after - before) > 0.01f && Mathf.Abs(bottom - before) < 0.8f)
                {
                    moves[transform] = after - before;
                }
            }
        }

        public void Apply(List<Terrain> terrains)
        {
            foreach (KeyValuePair<Transform, float> move in moves)
            {
                Undo.RecordObject(move.Key, "Build World");
                move.Key.position += Vector3.up * move.Value;
            }
        }
    }
}
