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
    private static readonly List<(UnityEngine.SceneManagement.Scene scene, Rect area)> handEditedAreas = new List<(UnityEngine.SceneManagement.Scene, Rect)>();

    public static bool Building { get; private set; }

    /// <summary>What the last build did, for the World Terrain Inspector.</summary>
    public static string LastReport { get; private set; } = "";

    static WorldBuilder()
    {
        WorldFeature.Edited += _ => Schedule(Delay);
        Spline.Changed += OnSplineChanged;
        TerrainCallbacks.heightmapChanged += OnHeightmapChanged;
        // Only changes to the world's features schedule a build: any hierarchy change anywhere used to, so a scene
        // load, a test or a dragged-in prop set off a build (and left the scene asking to be saved).
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        ObjectChangeEvents.changesPublished += OnChangesPublished;
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

        if (GUIUtility.hotControl != 0 || EditorApplication.isCompiling || WorldFeatureEditor.IsDrawing)
        {
            // Still dragging, or still drawing a feature (it builds when finished): wait.
            dueAt = EditorApplication.timeSinceStartup + 0.2;
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

    private static int featureSignature;

    // A feature added, removed, switched on or off, or moved in the hierarchy schedules a build; nothing else does.
    private static void OnHierarchyChanged()
    {
        int signature = 17;
        foreach (WorldFeature feature in Object.FindObjectsByType<WorldFeature>())
        {
            signature = signature * 31 + feature.GetEntityId().GetHashCode();
            signature = signature * 31 + feature.transform.localToWorldMatrix.GetHashCode();
        }

        if (signature != featureSignature)
        {
            featureSignature = signature;
            Schedule(Delay);
        }
    }

    // A world feature's settings or transform changed (in the Inspector, by a gizmo, by undo).
    private static void OnChangesPublished(ref ObjectChangeEventStream stream)
    {
        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) == ObjectChangeKind.ChangeAssetObjectProperties)
            {
                // A biome preset edited in its Inspector: the areas painted with it build again (their hash includes it).
                stream.GetChangeAssetObjectPropertiesEvent(i, out ChangeAssetObjectPropertiesEventArgs asset);
                if (EditorUtility.EntityIdToObject(asset.entityId) is BiomePreset)
                {
                    Schedule(Delay);
                    return;
                }

                continue;
            }

            if (stream.GetEventType(i) != ObjectChangeKind.ChangeGameObjectOrComponentProperties)
            {
                continue;
            }

            stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out ChangeGameObjectOrComponentPropertiesEventArgs change);
            Object changed = EditorUtility.EntityIdToObject(change.entityId);
            GameObject owner = changed is Component component ? component.gameObject : changed as GameObject;
            if (owner != null && owner.GetComponent<WorldFeature>() != null)
            {
                Schedule(Delay);
                return;
            }
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
        handEditedAreas.Add((terrain.gameObject.scene, Rect.MinMaxRect(a.x, a.y, b.x, b.y)));
        Schedule(HandEditDelay);
    }

    // ------------------------------------------------------------------ deciding what to build

    /// <summary>Builds what changed since the last build (or everything, when <paramref name="everything"/>). Returns
    /// how many terrains were rebuilt.</summary>
    public static int BuildChanged(bool everything)
    {
        // Scene by scene: scenes can lie on the same coordinates (the Haven and the prologue do), and one scene's
        // features must never shape, paint or regrow another scene's terrains and forests.
        int built = 0;
        var scenes = new List<UnityEngine.SceneManagement.Scene>();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.isLoaded) scenes.Add(scene);
        }

        foreach (UnityEngine.SceneManagement.Scene scene in scenes)
        {
            built += BuildChangedIn(scene, everything);
        }

        handEdited.Clear();
        handEditedAreas.Clear();
        return built;
    }

    private static int BuildChangedIn(UnityEngine.SceneManagement.Scene scene, bool everything)
    {
        List<WorldFeature> features = LiveFeatures().Where(f => f.gameObject.scene == scene).ToList();
        var terrainsToBuild = new List<Terrain>();
        var changedAreas = handEditedAreas.Where(e => e.scene == scene).Select(e => e.area).ToList();
        var areasToBuild = new HashSet<BiomeArea>();
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain.gameObject.scene != scene)
            {
                continue;
            }

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

            // Unity tells of a heightmap change as a terrain loads too: that rebuilt the world on every Editor start, and left the
            // scene asking to be saved. A "hand edit" that leaves the heights as they were built is not one.
            if (handEdited.Contains(terrain) && !everything && !string.IsNullOrEmpty(world.BuiltHeights) && world.BuiltHeights == HeightsFingerprint(terrain))
            {
                handEdited.Remove(terrain);
                Rect untouched = FlatRect(terrain);
                changedAreas.RemoveAll(area => area.Overlaps(untouched));
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
                    // A painting area reshapes the terrain; so does one that stopped painting (its ground comes back).
                    shape |= area.ShapesTerrain || built.paints;
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
                    shape |= area.ShapesTerrain;
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

        if (terrainsToBuild.Count == 0 && areasToBuild.Count == 0)
        {
            triggerGroup = -1;
            return 0;
        }

        Build(terrainsToBuild, areasToBuild.ToList(), features);
        return terrainsToBuild.Count;
    }

    /// <summary>A fingerprint of a terrain's heights: the same heights, the same text.</summary>
    public static string HeightsFingerprint(Terrain terrain)
    {
        TerrainData data = terrain != null ? terrain.terrainData : null;
        if (data == null)
        {
            return "";
        }

        int size = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, size, size);
        unchecked
        {
            long hash = 1469598103934665603L;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    hash = (hash ^ BitConverter.SingleToInt32Bits(heights[y, x])) * 1099511628211L;
                }
            }

            return size + ":" + hash.ToString("x16");
        }
    }

    /// <summary>Rebuilds one terrain with all its features and the areas on it (after Auto-Paint, for instance).</summary>
    public static void RebuildTerrain(Terrain terrain)
    {
        List<WorldFeature> features = LiveFeatures().Where(f => f.gameObject.scene == terrain.gameObject.scene).ToList();
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
    /// <summary>
    /// The feature's fingerprint: the settings that differ from its type's defaults, its spline and its placement. Only
    /// what differs from the defaults counts, so giving a feature type a new setting later doesn't change every such
    /// feature's fingerprint (which made the next automatic build rebuild all of them).
    /// </summary>
    public static string Hash(WorldFeature feature)
    {
        if (feature == null)
        {
            return "";
        }

        var text = new System.Text.StringBuilder();
        var serialized = new SerializedObject(feature);
        SerializedObject defaults = DefaultsOf(feature.GetType());
        SerializedProperty property = serialized.GetIterator();
        bool enter = true;
        while (property.NextVisible(enter))
        {
            enter = false;
            if (property.propertyPath == "m_Script")
            {
                continue;
            }

            SerializedProperty standard = defaults?.FindProperty(property.propertyPath);
            if (standard != null && SerializedProperty.DataEquals(property, standard))
            {
                continue;
            }

            text.Append(property.propertyPath).Append('=');
            AppendValue(text, property);
            text.Append(';');
        }

        text.Append(EditorJsonUtility.ToJson(feature.Container)).Append(feature.transform.localToWorldMatrix);
        if (feature is BiomeArea area && area.Biome != null)
        {
            text.Append(EditorJsonUtility.ToJson(area.Biome)); // editing the preset regrows the areas painted with it
        }

        return Hash128.Compute(text.ToString()).ToString();
    }

    private static readonly Dictionary<System.Type, SerializedObject> Defaults = new Dictionary<System.Type, SerializedObject>();

    // A fresh, hidden instance of the feature type, read for its default settings.
    private static SerializedObject DefaultsOf(System.Type type)
    {
        if (Defaults.TryGetValue(type, out SerializedObject known) && known.targetObject != null)
        {
            return known;
        }

        var holder = new GameObject("World Feature Defaults") { hideFlags = HideFlags.HideAndDontSave };
        holder.SetActive(false);
        Component instance = holder.AddComponent(type);
        var serialized = new SerializedObject(instance);
        Defaults[type] = serialized;
        return serialized;
    }

    // Every leaf value under a property, in order, as text.
    private static void AppendValue(System.Text.StringBuilder text, SerializedProperty property)
    {
        SerializedProperty walk = property.Copy();
        SerializedProperty end = property.GetEndProperty();
        bool first = true;
        while (first || (walk.Next(true) && !SerializedProperty.EqualContents(walk, end)))
        {
            first = false;
            switch (walk.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    text.Append(walk.objectReferenceValue != null
                        ? AssetDatabase.GetAssetPath(walk.objectReferenceValue) + "#" + walk.objectReferenceValue.name : "null");
                    break;
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ManagedReference:
                    continue;
                default:
                    object value = walk.boxedValue;
                    text.Append(value is float f ? f.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : value?.ToString());
                    break;
            }

            text.Append(',');
        }
    }

    // ------------------------------------------------------------------ building

    private static void Build(List<Terrain> terrains, List<BiomeArea> areas, List<WorldFeature> all)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Building = true;
        LastObjects.Clear();
        WorldGround.Scope = terrains.Count > 0 ? terrains[0].gameObject.scene : areas.Count > 0 ? areas[0].gameObject.scene : default;
        UnityEngine.SceneManagement.Scene builtScene = WorldGround.Scope;
        // What quests know by name in the scene: a rebuild that loses one says so (QuestTargetGuard).
        Dictionary<Object, string> questTargets = QuestTargetGuard.Snapshot(builtScene);
        int group = triggerGroup;
        triggerGroup = -1;
        if (group < 0)
        {
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build World");
        }
        var report = new List<string>();
        var phases = new List<string>();
        var lap = System.Diagnostics.Stopwatch.StartNew();
        void Lap(string phase)
        {
            if (lap.ElapsedMilliseconds >= 50) phases.Add($"{phase} {lap.ElapsedMilliseconds} ms");
            lap.Restart();
        }

        try
        {
            terrains = WithSeamNeighbours(terrains, all);
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
                AddMissingDetails(terrain, builders);
                world.Stash.Prepare(terrain.terrainData.heightmapResolution, terrain.terrainData.alphamapResolution,
                                    terrain.terrainData.alphamapLayers, terrain.terrainData.detailResolution);
                layers.Add(ReadHeights(terrain, world.Stash, shaping));
            }
            Lap("record undo + read heights");

            var sampler = new WorldSampler(layers);
            foreach (FeatureBuilder builder in builders)
            {
                builder.Prepare(sampler);
                foreach (HeightLayer layer in layers)
                {
                    builder.Heights(layer);
                }
            }
            Lap("feature heights");

            foreach (HeightLayer layer in layers)
            {
                MakeRoom(layer, layer.Terrain.GetComponent<WorldTerrain>().Stash);
            }

            ShareEdges(layers);
            foreach (HeightLayer layer in layers)
            {
                glue.Record(layer);
                WriteHeights(layer, layer.Terrain.GetComponent<WorldTerrain>().Stash);
            }
            Lap("write heights");

            // Paint, grass and trees.
            foreach (Terrain terrain in terrains)
            {
                WorldTerrainStash stash = terrain.GetComponent<WorldTerrain>().Stash;
                PaintTimings.Clear();
                Paint(terrain, stash, shaping, builders);
                Lap("paint");
                phases.AddRange(PaintTimings);
                ClearDetails(terrain, stash, shaping, builders);
                Lap("grass");
                ClearTrees(terrain, stash, builders);
                Lap("trees");
                terrain.Flush();
                Lap("flush");
            }

            glue.Apply(terrains);
            Lap("ground glue");

            // Objects: the shaping features on the rebuilt terrains, then the areas.
            var context = new BuildContext();
            context.Builders.AddRange(builders);
            foreach (FeatureBuilder builder in builders)
            {
                if (!(builder.Feature is BiomeArea))
                {
                    RegenerateObjects(builder, context); // areas (painting ones too) grow theirs below, after every feature
                }
            }
            Lap("feature objects");

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
                Lap(area.name);
            }

            if (placedSaveables > 0)
            {
                foreach (UnityEngine.SceneManagement.Scene scene in areas.Select(a => a.gameObject.scene).Distinct())
                {
                    SaveableEntityTools.AssignSceneIds(scene);
                }
            }

            RecordBuilt(all, terrains, areas);
            // The heights, paint and ground stash live in assets beside the scene, which saving the scene doesn't write: left
            // unsaved, every Editor start loaded the old heights, found them changed and rebuilt the world (2026-11-12).
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null) continue;
                AssetDatabase.SaveAssetIfDirty(terrain.terrainData);
                WorldTerrain world = terrain.GetComponent<WorldTerrain>();
                if (world != null && world.Stash != null) AssetDatabase.SaveAssetIfDirty(world.Stash);
            }
            Lap("saveable ids + record");
            foreach (Terrain terrain in terrains)
            {
                // The grass takes the new ground's colour from a quick in-memory preview; the full bake (seconds on a
                // big terrain, every build) waits for the scene to be saved.
                GrassSetup.GroundRepainted(terrain);

                EditorUtility.SetDirty(terrain.terrainData);
                EditorUtility.SetDirty(terrain.GetComponent<WorldTerrain>().Stash);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
            }

            foreach (BiomeArea area in areas)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(area.gameObject.scene);
            }

            Lap("mark dirty");
            report.Add($"{terrains.Count} terrain(s), {builders.Count} feature(s), {areas.Count} area(s) in {watch.ElapsedMilliseconds} ms" +
                       (phases.Count > 0 ? " (" + string.Join(", ", phases) + ")" : ""));
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
            WorldGround.Scope = default;
            Building = false;
            Undo.CollapseUndoOperations(group);
            ignoreTerrainUntil = EditorApplication.timeSinceStartup + 0.5;
        }

        List<string> lost = QuestTargetGuard.Report(questTargets, builtScene, "The World Builder");
        if (lost.Count > 0)
        {
            report.Add("REMOVED QUEST TARGETS: " + string.Join(", ", lost));
        }

        LastReport = string.Join("; ", report);
        Debug.Log("World Builder: " + LastReport);
    }

    private static void Record(Terrain terrain, WorldTerrain world, string name)
    {
        var objects = new List<Object> { terrain.terrainData, world, world.Stash, terrain.transform };
        objects.AddRange(terrain.terrainData.alphamapTextures);
        // Only saved assets and objects of the terrain's own scene: Unity puts the undo record of a loose in-memory object
        // (a new terrain's paint textures before they are saved) on the first open scene, which then asks to be saved.
        Undo.RegisterCompleteObjectUndo(objects.Where(o => o != null && (EditorUtility.IsPersistent(o) ||
            (o is Component part && part.gameObject.scene == terrain.gameObject.scene))).ToArray(), name);
    }

    /// <summary>Records what each terrain was built from: everything on the rebuilt terrains, and the rebuilt areas on
    /// the others (a terrain left for a manual build keeps its record, so its changes are still seen as pending).</summary>
    private static void RecordBuilt(List<WorldFeature> all, List<Terrain> terrains, List<BiomeArea> areas)
    {
        // Only the scene being built: another scene's terrain on the same coordinates is none of its business.
        UnityEngine.SceneManagement.Scene scene = terrains.Count > 0 ? terrains[0].gameObject.scene
            : areas.Count > 0 ? areas[0].gameObject.scene : default;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain.gameObject.scene != scene)
            {
                continue;
            }

            var world = terrain.GetComponent<WorldTerrain>();
            if (world == null)
            {
                continue;
            }

            Rect rect = FlatRect(terrain);
            bool rebuilt = terrains.Contains(terrain);
            // Only terrains whose record changes are touched: recording the others for undo marked their scenes changed
            // (a build in one scene left another asking to be saved).
            if (!rebuilt && !areas.Any(area => FlatRect(area.WorldBounds()).Overlaps(rect)))
            {
                continue;
            }

            Undo.RecordObject(world, "Build World");
            if (rebuilt)
            {
                world.BuiltFeatures.Clear();
            }

            // The heights as built, so a load that only says they changed doesn't rebuild (BuildChangedIn).
            world.BuiltHeights = HeightsFingerprint(terrain);

            foreach (WorldFeature feature in all.Where(f => FlatRect(f.WorldBounds()).Overlaps(rect)))
            {
                if (!rebuilt && !(feature is BiomeArea area && areas.Contains(area)))
                {
                    continue;
                }

                world.BuiltFeatures.RemoveAll(b => b.feature == feature);
                world.BuiltFeatures.Add(new WorldTerrain.Built { feature = feature, hash = Hash(feature), bounds = feature.WorldBounds(), paints = feature.ShapesTerrain });
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

        if (old == null)
        {
            Transform root = builder.Feature.GeneratedRoot(true);
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build World");
            builder.Objects(root, context);
            return;
        }

        // Made afresh beside the old objects (switched off, so they don't stand in the way of the new ones, as when they
        // were deleted first), then the old are kept wherever the fresh came out the same (GeneratedObjects): unchanged
        // objects keep their file ids, so the scene's diff is the real change, and their save ids.
        string name = old.name;
        bool wasActive = old.gameObject.activeSelf;
        Undo.RecordObject(old.gameObject, "Build World");
        old.name = name + " (previous)";
        old.gameObject.SetActive(false);
        Transform fresh = builder.Feature.GeneratedRoot(true);
        Undo.RegisterCreatedObjectUndo(fresh.gameObject, "Build World");
        builder.Objects(fresh, context);
        if (!GeneratedObjects.CanReconcile(old, fresh))
        {
            Undo.DestroyObjectImmediate(old.gameObject);
            return;
        }

        GeneratedObjects.Result result = GeneratedObjects.Reconcile(old, fresh);
        old.name = name;
        old.gameObject.SetActive(wasActive);
        LastObjects.Add($"{builder.Feature.name}: {result}");
    }

    /// <summary>For each feature whose objects the last build made again: how many were kept, new and gone.</summary>
    public static readonly List<string> LastObjects = new List<string>();

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

    /// <summary>Where two terrains being built touch, the cells on their shared edge take one height: each samples a feature on
    /// its own grid, so a road or river crossing the edge would otherwise leave a small step there. In the working values, so
    /// the stash remembers it as the builder's own work and the next build doesn't take it for a hand edit.</summary>
    private static void ShareEdges(List<HeightLayer> layers)
    {
        foreach (HeightLayer a in layers)
        {
            foreach (HeightLayer b in layers)
            {
                if (a == b || a.Rect.width == 0 || b.Rect.width == 0)
                {
                    continue;
                }

                int last = a.Terrain.terrainData.heightmapResolution - 1;
                Vector2 east = a.Grid.World(last, last), west = b.Grid.World(0, last);
                if (Mathf.Abs(east.x - west.x) < 0.01f && Mathf.Abs(east.y - west.y) < 0.01f && a.Rect.xMax > last && b.Rect.xMin == 0)
                {
                    for (int y = Mathf.Max(a.Rect.yMin, b.Rect.yMin); y < Mathf.Min(a.Rect.yMax, b.Rect.yMax); y++)
                    {
                        ShareCell(a, last, y, b, 0, y);
                    }
                }

                Vector2 north = a.Grid.World(last, last), south = b.Grid.World(last, 0);
                if (Mathf.Abs(north.x - south.x) < 0.01f && Mathf.Abs(north.y - south.y) < 0.01f && a.Rect.yMax > last && b.Rect.yMin == 0)
                {
                    for (int x = Mathf.Max(a.Rect.xMin, b.Rect.xMin); x < Mathf.Min(a.Rect.xMax, b.Rect.xMax); x++)
                    {
                        ShareCell(a, x, last, b, x, 0);
                    }
                }
            }
        }
    }

    // What a feature shaped wins over bare ground; where both were shaped they meet halfway.
    private static void ShareCell(HeightLayer a, int ax, int ay, HeightLayer b, int bx, int by)
    {
        bool shapedA = a.Touched[a.IndexOf(ax, ay)], shapedB = b.Touched[b.IndexOf(bx, by)];
        if (!shapedA && !shapedB)
        {
            return;
        }

        float height = shapedA && shapedB ? (a.Get(ax, ay) + b.Get(bx, by)) * 0.5f : shapedA ? a.Get(ax, ay) : b.Get(bx, by);
        a.Set(ax, ay, height);
        b.Set(bx, by, height);
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

    private static void AddMissingDetails(Terrain terrain, List<FeatureBuilder> builders)
    {
        foreach (GameObject plant in builders.SelectMany(b => b.DetailPrefabs).Where(p => p != null).Distinct())
        {
            BiomeBrush.EnsureDetail(terrain.terrainData, plant);
        }
    }

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

    private static readonly List<string> PaintTimings = new List<string>();

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

                // Painted by hand over a feature's paint: the hand paint stays, now and in every later build.
                if (handPainted)
                {
                    stash.MarkHandPainted((rect.y + y) * resolution + rect.x + x);
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
            var one = System.Diagnostics.Stopwatch.StartNew();
            builder.Paint(layer);
            if (one.ElapsedMilliseconds >= 50) PaintTimings.Add($"{builder.Feature.name} paint {one.ElapsedMilliseconds} ms");
        }

        var released = new HashSet<int>();
        for (int y = 0; y < rect.height; y++)
        {
            for (int x = 0; x < rect.width; x++)
            {
                int index = y * rect.width + x;
                int offset = index * layers;
                int cell = (rect.y + y) * resolution + rect.x + x;
                bool hand = stash.IsHandPainted(cell);
                bool touched = layer.Touched[index] && !hand;
                for (int l = 0; l < layers; l++)
                {
                    if (hand)
                    {
                        // Exactly as painted: not the ground kept from before the feature, nor the feature's paint.
                        baseWeights[l] = lastWeights[l] = maps[y, x, l];
                        continue;
                    }

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
                    int cell = y * rect.width + x;
                    bool kept = stash.TryGetDetail(key, out int baseValue, out int last);
                    bool clearHere = clear.Clear[cell];
                    int wanted = clear.Wanted(cell, prototype);
                    if (!kept && !clearHere && wanted < 0)
                    {
                        continue; // no feature here and nothing kept: the common case, untouched
                    }

                    int current = cells[y, x];
                    int value = ClearLayer.Decide(kept, baseValue, last, current, clearHere, wanted,
                                                  out int keepBase, out int keepLast, out bool release);
                    if (value != current)
                    {
                        cells[y, x] = value;
                        changed = true;
                    }

                    if (release)
                    {
                        released.Add(key);
                    }
                    else if (kept || value != keepBase)
                    {
                        stash.SetDetail(key, keepBase, keepLast);
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

    private const float TreeClearMargin = 6f;
    private const float TreeCell = 32f;

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

        // Every tree (standing, and kept in the stash) goes into a grid of cells; each feature then asks only the trees in the cells its
        // footprint covers. Asking every feature about every tree took minutes on a large terrain (150 000 trees, 46 features: 177 s).
        TreeInstance[] standing = data.treeInstances;
        List<WorldTerrainStash.StashedTree> kept = stash.Trees;
        int total = standing.Length + kept.Count;
        var points = new Vector2[total];
        for (int i = 0; i < standing.Length; i++)
        {
            points[i] = World(standing[i].position);
        }

        for (int i = 0; i < kept.Count; i++)
        {
            points[standing.Length + i] = World(kept[i].position);
        }

        int cells = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(size.x, size.z) / TreeCell));
        var grid = new List<int>[cells * cells];
        int CellOf(float value, float from, float extent) => Mathf.Clamp((int)((value - from) / Mathf.Max(0.001f, extent) * cells), 0, cells - 1);
        for (int i = 0; i < total; i++)
        {
            int key = CellOf(points[i].y, origin.z, size.z) * cells + CellOf(points[i].x, origin.x, size.x);
            (grid[key] ??= new List<int>()).Add(i);
        }

        var cleared = new bool[total];
        var visit = new HashSet<int>();
        foreach (FeatureBuilder builder in builders)
        {
            // An open line (path, river, ravine, cliff line) only near its own samples: a long, curving one has a bounding box covering much
            // of the map. A loop (outcrop, area) by its box, as what lies inside it may count too.
            visit.Clear();
            float margin = builder.Feature.Reach + TreeClearMargin;
            SplineLine line = builder.Line;
            if (line != null && !line.Closed && line.Count > 1)
            {
                foreach (Vector3 sample in line.Points)
                {
                    int x0 = CellOf(sample.x - margin, origin.x, size.x), x1 = CellOf(sample.x + margin, origin.x, size.x);
                    int y0 = CellOf(sample.z - margin, origin.z, size.z), y1 = CellOf(sample.z + margin, origin.z, size.z);
                    for (int y = y0; y <= y1; y++)
                    {
                        for (int x = x0; x <= x1; x++)
                        {
                            visit.Add(y * cells + x);
                        }
                    }
                }
            }
            else
            {
                Rect area = FlatRect(builder.Feature.WorldBounds());
                int x0 = CellOf(area.xMin - margin, origin.x, size.x), x1 = CellOf(area.xMax + margin, origin.x, size.x);
                int y0 = CellOf(area.yMin - margin, origin.z, size.z), y1 = CellOf(area.yMax + margin, origin.z, size.z);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        visit.Add(y * cells + x);
                    }
                }
            }

            foreach (int key in visit)
            {
                List<int> here = grid[key];
                if (here == null)
                {
                    continue;
                }

                foreach (int tree in here)
                {
                    if (!cleared[tree] && builder.ClearsTree(points[tree]))
                    {
                        cleared[tree] = true;
                    }
                }
            }
        }

        var standingKept = new List<TreeInstance>(standing.Length);
        var stashed = new List<WorldTerrainStash.StashedTree>();
        for (int i = 0; i < standing.Length; i++)
        {
            if (cleared[i])
            {
                stashed.Add(new WorldTerrainStash.StashedTree(standing[i]));
            }
            else
            {
                standingKept.Add(standing[i]);
            }
        }

        for (int i = 0; i < kept.Count; i++)
        {
            if (cleared[standing.Length + i])
            {
                stashed.Add(kept[i]);
            }
            else if (kept[i].prototypeIndex < data.treePrototypes.Length)
            {
                standingKept.Add(kept[i].ToTree()); // the feature moved away: the tree comes back
            }
        }

        stash.Trees.Clear();
        stash.Trees.AddRange(stashed);
        data.SetTreeInstances(standingKept.ToArray(), true);
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

    /// <summary>
    /// The terrains to build, with every neighbour that shares an edge a shaping feature crosses (a river from the meadow
    /// into the forest). Both sides of such an edge take one height (<see cref="ShareEdges"/>) only when both are built:
    /// building one alone samples the feature on its own grid again and leaves a step at the seam. A neighbour brought in
    /// may bring its own neighbours the same way.
    /// </summary>
    public static List<Terrain> WithSeamNeighbours(List<Terrain> terrains, List<WorldFeature> all)
    {
        var result = new List<Terrain>(terrains);
        for (int i = 0; i < result.Count; i++)
        {
            Terrain built = result[i];
            Rect here = FlatRect(built);
            foreach (Terrain other in Terrain.activeTerrains)
            {
                if (result.Contains(other) || other.terrainData == null || other.gameObject.scene != built.gameObject.scene
                    || other.GetComponent<WorldTerrain>() == null)
                {
                    continue;
                }

                Rect there = FlatRect(other);
                if (SharesEdge(here, there) && all.Any(f => f != null && f.ShapesTerrain
                        && FlatRect(f.WorldBounds()).Overlaps(here) && FlatRect(f.WorldBounds()).Overlaps(there)))
                {
                    result.Add(other);
                }
            }
        }

        return result;
    }

    /// <summary>Whether two terrain rectangles meet along an edge (not just at a corner).</summary>
    public static bool SharesEdge(Rect a, Rect b)
    {
        const float Touch = 0.01f;
        bool sideBySide = (Mathf.Abs(a.xMax - b.xMin) < Touch || Mathf.Abs(b.xMax - a.xMin) < Touch)
                          && Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin) > Touch;
        bool endToEnd = (Mathf.Abs(a.yMax - b.yMin) < Touch || Mathf.Abs(b.yMax - a.yMin) < Touch)
                        && Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin) > Touch;
        return sideBySide || endToEnd;
    }

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
