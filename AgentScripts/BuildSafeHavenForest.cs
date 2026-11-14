#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Dresses Safe Haven in the Mountain Lake Forest Pack (most of it) with the Mossy Cedrus Forest filling its gaps
/// (`unity command script run --file AgentScripts/BuildSafeHavenForest.cs --entry BuildSafeHavenForest.Apply`, from SafeHaven
/// with nothing unsaved; `.Rebuild` remakes converted assets in place). The scene is made by the World Builder, so nothing is
/// placed by hand here: the features' shared palettes, cliff kit and paint rules get Haven-only copies holding the new
/// assets (the Prologue and the demo keep theirs), the terrain's nine ground layers and eleven grass and plant slots are
/// swapped in place (the builder's stash stays valid), the features are pointed at the copies and the world is rebuilt by
/// its own rules. Then grass regrows by ground, the hand-placed trees become maples, displacement is rebaked for the new
/// grounds and the navmesh for the new trees. Resource nodes stay as they are.
/// </summary>
public static class BuildSafeHavenForest
{
    private const string ScenePath = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string Out = "Assets/_Project/Art/Vegetation/Safe Haven";
    private const string DataOut = "Assets/_Project/Data/World/Safe Haven";
    private const string Lake = "Assets/VIVID_Arts/Assets/MountainLake/Prefabs/";
    private const string LakeLayers = "Assets/VIVID_Arts/Mountain_Lake/TerrainLayers/";
    private const string Cedrus = "Assets/SeedMesh/Mossy_Cedrus_Forest/Environment";
    private const string Raid = "Assets/_Project/Art/Vegetation/Misty Woods/";
    private const string TreeTemplate = "Assets/_Project/Models/Generated/Vegetation/rich_oak/rich_oak_Wind.prefab";
    private const string RockTemplate = "Assets/_Project/Art/Forest/Prefabs/Boulder A.prefab";

    private static readonly string[] Maples = { "Sugar Maple A", "Sugar Maple B", "Sugar Maple C", "Sugar Maple D" };

    public static string Rebuild()
    {
        VegetationPackConverter.Rebuild = true;
        try
        {
            return Apply();
        }
        finally
        {
            VegetationPackConverter.Rebuild = false;
        }
    }

    public static string Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return "open SafeHaven first";
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).isDirty) return "an open scene has unsaved changes";
        }

        var log = new List<string>();
        Terrain terrain = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<Terrain>(true)).First(t => t != null);
        TerrainData data = terrain.terrainData;

        // ---- assets
        var tree = AssetDatabase.LoadAssetAtPath<GameObject>(TreeTemplate);
        var rock = AssetDatabase.LoadAssetAtPath<GameObject>(RockTemplate);
        GameObject[] maples =
        {
            VegetationPackConverter.Tree(Lake + "MLRT_SugarMaple_01_Var1.prefab", Out + "/Trees", Maples[0], tree),
            VegetationPackConverter.Tree(Lake + "MLRT_SugarMaple_02_Var1.prefab", Out + "/Trees", Maples[1], tree),
            VegetationPackConverter.Tree(Lake + "MLRT_SugarMaple_03_Var1.prefab", Out + "/Trees", Maples[2], tree),
            VegetationPackConverter.Tree(Lake + "MLRT_SugarMaple_04_Var1.prefab", Out + "/Trees", Maples[3], tree),
        };
        GameObject Solid(string source, string name) => VegetationPackConverter.Prop(source, Out + "/Rocks", name, true, rock);
        GameObject Loose(string source, string name) => VegetationPackConverter.Prop(source, Out + "/Rocks", name, false);
        GameObject Plant(string source, string name) => VegetationPackConverter.Tree(source, Out + "/Plants", name, null, solid: false, windStrength: 1.2f);
        GameObject[] boulders =
        {
            Solid(Lake + "MT_Rock_Med_01.prefab", "Lake Boulder A"), Solid(Lake + "MT_Rock_Med_02.prefab", "Lake Boulder B"),
            Solid(Lake + "MT_Rock_Med_04.prefab", "Lake Boulder C"), Solid(Lake + "MT_Rock_Med_05.prefab", "Lake Boulder D"),
            Solid(Source("Mossy_Rock_var3"), "Haven Mossy Boulder"),
        };
        GameObject[] smallStones =
        {
            Loose(Lake + "MT_Rock_Small_01.prefab", "Lake Stone A"), Loose(Lake + "MT_Rock_Small_02.prefab", "Lake Stone B"),
            Loose(Lake + "MT_Rock_Small_03.prefab", "Lake Stone C"), Loose(Lake + "MT_Rock_Small_Composite_01.prefab", "Lake Stones A"),
            Loose(Lake + "MT_Rock_Small_Composite_03.prefab", "Lake Stones B"), Loose(Source("Mossy_rock_var5"), "Haven Mossy Stone"),
        };
        GameObject[] ferns =
        {
            Plant(Lake + "MLRT_Fern_01.prefab", "Lake Fern A"), Plant(Lake + "MLRT_Fern_02.prefab", "Lake Fern B"),
            Plant(Lake + "MLRT_Fern_03.prefab", "Lake Fern C"), Plant(Source("Common_fern_var3"), "Haven Fern"),
        };
        GameObject lakeGrass = Plant(Lake + "MLRT_Grass_01_Var3.prefab", "Lake Grass Clump");
        GameObject branch = Loose(Source("Rotten_cedrus_branch_var1"), "Haven Fallen Branch");
        GameObject[] cliffs =
        {
            Solid(Lake + "MT_Rock_Large_02.prefab", "Lake Crag A"), Solid(Lake + "MT_Rock_Med_03.prefab", "Lake Crag B"),
            Solid(Lake + "MLRT_Rock_Composite_005.prefab", "Lake Crag C"), Solid(Lake + "MLRT_Rock_Composite_007.prefab", "Lake Crag D"),
        };
        Dictionary<string, GameObject> details = Details();
        Dictionary<TerrainLayer, TerrainLayer> grounds = Grounds(data);
        AssetDatabase.SaveAssets();
        log.Add($"converted 4 maples, {boulders.Length} boulders, {smallStones.Length} stones, {ferns.Length + 1} plants, {cliffs.Length} crags, {details.Count} details, {grounds.Count} grounds");

        // ---- Haven-only copies of the World Builder's shared assets, holding the new ones
        var replaced = new Dictionary<Object, Object>();
        foreach (var pair in grounds) replaced[pair.Key] = pair.Value;
        float maple = MapleScale(tree, maples[0]);
        replaced[Load<ScatterPalette>("Assets/_Project/Data/Level/Palettes/Haven Trees.asset")] = Palette("Haven Trees", "Haven Lake Trees",
            new[] { maples[0], maples[1], maples[2], maples[3], Load<GameObject>(Raid + "Trees/Cedar Slender.prefab"), Load<GameObject>(Raid + "Trees/Cedar B.prefab") },
            new[] { 1f, 0.8f, 0.8f, 0.6f, 0.3f, 0.2f }, maple);
        replaced[Load<ScatterPalette>("Assets/_Project/Data/Level/Palettes/Mossy Boulders.asset")] = Palette("Mossy Boulders", "Haven Lake Boulders", boulders);
        replaced[Load<ScatterPalette>("Assets/_Project/Data/Level/Palettes/Ferns.asset")] = Palette("Ferns", "Haven Lake Ferns", ferns);
        replaced[Load<ScatterPalette>("Assets/_Project/Data/Level/Palettes/Cliff Rubble.asset")] = Palette("Cliff Rubble", "Haven Lake Rubble",
            smallStones.Concat(new[] { boulders[4] }).ToArray());
        replaced[Load<ScatterPalette>("Assets/_Project/Data/Level/Palettes/River Stones.asset")] = Palette("River Stones", "Haven Lake River Stones", smallStones);
        replaced[Load<ScatterPalette>("Assets/_Project/Data/Level/Palettes/Riverbank.asset")] = Palette("Riverbank", "Haven Lake Riverbank",
            ferns.Take(3).Concat(new[] { lakeGrass, smallStones[5], branch }).ToArray());
        replaced[Load<CliffKit>("Assets/_Project/Data/World/Rock Faces.asset")] = Kit(cliffs);
        foreach (string rules in new[] { "Paint Rules - Haven Meadow", "Paint Rules - Old-Growth Forest" })
        {
            replaced[Load<TerrainPaintRules>($"Assets/_Project/Data/World/{rules}.asset")] = Rules(rules, grounds);
        }

        AssetDatabase.SaveAssets();
        log.Add($"world assets: {replaced.Count - grounds.Count} Haven copies (palettes, cliff kit, paint rules)");

        // ---- the terrain: grounds and grass slots in place, the features pointed at the copies
        data.terrainLayers = data.terrainLayers.Select(l => l != null && grounds.TryGetValue(l, out TerrainLayer made) ? made : l).ToArray();
        DetailPrototype[] prototypes = data.detailPrototypes;
        foreach (DetailPrototype prototype in prototypes)
        {
            if (prototype.prototype != null && details.TryGetValue(prototype.prototype.name, out GameObject made)) prototype.prototype = made;
        }

        data.detailPrototypes = prototypes;
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        int pointed = 0;
        foreach (MonoBehaviour owner in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                     .Where(b => b is WorldFeature || b is WorldTerrain || b is TerrainMoss))
        {
            pointed += Repoint(owner, replaced);
        }

        log.Add($"terrain: {data.terrainLayers.Length} grounds and {prototypes.Length} grass slots swapped; {pointed} references on the world's features now Haven's");

        // ---- rebuild by the world's own rules, then what the builder doesn't own
        int built = WorldBuilder.BuildChanged(true);
        log.Add($"world rebuilt: {built} terrain(s); {WorldBuilder.LastReport}");
        log.Add(HandPlaced(scene, maples, maple));
        log.Add(Grass(terrain, details, grounds));
        log.Add(Moss(terrain));
        log.Add(Surface(terrain, grounds));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add(WorldBuilder.RebakeNavMesh() + " (save SafeHaven again when it finishes)");
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    // ------------------------------------------------------------------ assets

    private static string Source(string name)
    {
        string file = name + ".prefab";
        return AssetDatabase.FindAssets("t:Prefab", new[] { Cedrus }).Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(path => Path.GetFileName(path) == file) ?? throw new FileNotFoundException(file);
    }

    private static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new FileNotFoundException(path);

    // Grass and plant kinds by the slot they fill (TerrainGrowth grows them by these names). The blue glowing mushrooms stay.
    private static Dictionary<string, GameObject> Details()
    {
        string folder = Out + "/Details";
        var grass = VegetationPackConverter.DetailLook.Grass;
        var plant = VegetationPackConverter.DetailLook.Plant;
        var low = VegetationPackConverter.DetailLook.Low;
        return new Dictionary<string, GameObject>
        {
            { "Short Grass", VegetationPackConverter.Detail(Lake + "MLRT_Grass_01_Var1.prefab", folder, "Short Grass", grass) },
            { "Tall Grass", VegetationPackConverter.Detail(Lake + "MLRT_Grass_01_Var2.prefab", folder, "Tall Grass", grass) },
            { "Bush", VegetationPackConverter.Detail(Source("Roldana_plant_var6"), folder, "Bush", plant) },
            { "Glow Flowers", Load<GameObject>(Raid + "Details/Glow Flowers.prefab") },
            { "Fern", VegetationPackConverter.Detail(Lake + "MLRT_Fern_02.prefab", folder, "Fern", plant) },
            { "Blueberry", VegetationPackConverter.Detail(Source("ForestClover_var2"), folder, "Blueberry", low) },
            { "Lingonberry", VegetationPackConverter.Detail(Source("Ground_weed_var4"), folder, "Lingonberry", low) },
            { "Heather", VegetationPackConverter.Detail(Lake + "MLRT_Grass_01_Var3.prefab", folder, "Heather", grass) },
            { "White Flowers", VegetationPackConverter.Detail(Source("Ground_flower_weed_var1 Variant"), folder, "White Flowers", plant) },
            { "Yellow Flowers", VegetationPackConverter.Detail(Source("Ground_flower_weed_var2 Variant"), folder, "Yellow Flowers", plant) },
        };
    }

    // The nine grounds, old by its file name to new: every Mountain Lake layer, Cedrus for what the lake pack lacks.
    private static Dictionary<TerrainLayer, TerrainLayer> Grounds(TerrainData data)
    {
        string folder = Out + "/Ground";
        string moss = Cedrus + "/Global_textures/Moss/Moss_surface.terrainlayer";
        var byName = new Dictionary<string, (string source, string name)>
        {
            { "Meadow Grass", (LakeLayers + "MLRT_Moss.terrainlayer", "Haven Meadow") },
            { "Lush Grass", (moss, "Haven Lush Moss") },
            { "Trodden Path", (LakeLayers + "MLRT_ForestDirt.terrainlayer", "Haven Path") },
            { "Gravel", (LakeLayers + "MLRT_Rocks.terrainlayer", "Haven Rock") },
            { "Muddy Bank", (LakeLayers + "MLRT_Soil.terrainlayer", "Haven Bank") },
            { "Forest Litter", (Cedrus + "/Terrain_Textures/Fallen_Leaves/Fallen_leaves.terrainlayer", "Haven Litter") },
            { "Dark Humus", (Cedrus + "/Terrain_Textures/Dirt/Cedrus_Dirt.terrainlayer", "Haven Humus") },
        };
        var map = new Dictionary<TerrainLayer, TerrainLayer>();
        foreach (TerrainLayer old in data.terrainLayers.Where(l => l != null))
        {
            string path = AssetDatabase.GetAssetPath(old);
            if (path.StartsWith(Out)) continue; // already new (a rerun)
            (string source, string name) made;
            if (old.name == "Forest Moss") made = path.Contains("/SafeHaven/") ? (Cedrus + "/Terrain_Textures/Mossy_Ground_rocks/Mossy_Ground_rocks.terrainlayer", "Haven Mossy Ground") : (moss, "Haven Old Moss");
            else if (!byName.TryGetValue(old.name, out made)) continue;
            map[old] = VegetationPackConverter.Ground(made.source, folder, made.name);
        }

        return map;
    }

    // Maples are far taller than the oaks they replace: scaled to the oaks' height, give or take.
    private static float MapleScale(GameObject oak, GameObject maple)
    {
        float Height(GameObject prefab) => prefab.GetComponentsInChildren<Renderer>().Select(r => r.bounds.max.y).DefaultIfEmpty(10f).Max();
        float mapleHeight = maple.GetComponentInChildren<Renderer>().sharedMaterial.GetFloat("_TreeHeight");
        return Mathf.Clamp(Height(oak) / Mathf.Max(1f, mapleHeight), 0.45f, 1.1f);
    }

    // A copy of a shared palette with new prefabs: its placement settings kept, sizes matched to what was there.
    private static ScatterPalette Palette(string from, string name, GameObject[] prefabs, float[] weights = null, float sizeFactor = 0f)
    {
        var source = Load<ScatterPalette>($"Assets/_Project/Data/Level/Palettes/{from}.asset");
        string path = $"{DataOut}/{name}.asset";
        VegetationPackConverter.EnsureFolder(DataOut);
        if (AssetDatabase.LoadAssetAtPath<ScatterPalette>(path) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
        var palette = Load<ScatterPalette>(path);
        var so = new SerializedObject(palette);
        SerializedProperty entries = new SerializedObject(source).FindProperty("entries");
        SerializedProperty first = entries.GetArrayElementAtIndex(0);
        Vector2 oldScale = first.FindPropertyRelative("scale").vector2Value;
        float oldSize = Enumerable.Range(0, entries.arraySize).Select(i => Footprint(entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue as GameObject)).Average();
        SerializedProperty made = so.FindProperty("entries");
        made.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++)
        {
            SerializedProperty entry = made.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefabs[i];
            entry.FindPropertyRelative("weight").floatValue = weights != null ? weights[i] : 1f;
            float factor = sizeFactor > 0f ? sizeFactor : Mathf.Clamp(oldSize / Mathf.Max(0.05f, Footprint(prefabs[i])), 0.2f, 3f);
            entry.FindPropertyRelative("scale").vector2Value = oldScale * factor;
            entry.FindPropertyRelative("alignToSlope").floatValue = first.FindPropertyRelative("alignToSlope").floatValue;
            entry.FindPropertyRelative("sink").floatValue = first.FindPropertyRelative("sink").floatValue;
            entry.FindPropertyRelative("randomYaw").boolValue = first.FindPropertyRelative("randomYaw").boolValue;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(palette);
        return SinkRoots(palette);
    }

    // A tree with roots below its pivot (the lake maples) is meant to stand on its pivot, but the World Builder sets things
    // on the ground by their lowest point: each entry sinks by how far its model reaches below the pivot, at its mean size.
    private static ScatterPalette SinkRoots(ScatterPalette palette)
    {
        var so = new SerializedObject(palette);
        SerializedProperty entries = so.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            var prefab = entry.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
            if (prefab == null || prefab.GetComponent<ChoppableTree>() == null) continue;
            Renderer[] drawn = prefab.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            if (drawn.Length == 0) continue;
            float below = Mathf.Max(0f, prefab.transform.position.y - drawn.Min(r => r.bounds.min.y));
            Vector2 scale = entry.FindPropertyRelative("scale").vector2Value;
            entry.FindPropertyRelative("sink").floatValue = 0.05f + below * (scale.x + scale.y) * 0.5f;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(palette);
        return palette;
    }

    private static float Footprint(GameObject prefab)
    {
        if (prefab == null) return 1f;
        Renderer[] drawn = prefab.GetComponentsInChildren<Renderer>(true);
        if (drawn.Length == 0) return 1f;
        Bounds bounds = drawn[0].bounds;
        foreach (Renderer part in drawn) bounds.Encapsulate(part.bounds);
        return Mathf.Max(bounds.size.x, bounds.size.z);
    }

    private static CliffKit Kit(GameObject[] crags)
    {
        string path = DataOut + "/Haven Lake Crags.asset";
        if (AssetDatabase.LoadAssetAtPath<CliffKit>(path) == null) AssetDatabase.CopyAsset("Assets/_Project/Data/World/Rock Faces.asset", path);
        var kit = Load<CliffKit>(path);
        kit.Pieces.Clear();
        kit.Pieces.AddRange(crags.Select((prefab, i) => WorldAssets.Measure(new CliffKit.Piece { prefab = prefab, weight = i < 2 ? 2f : 1.2f })));
        EditorUtility.SetDirty(kit);
        return kit;
    }

    private static TerrainPaintRules Rules(string from, Dictionary<TerrainLayer, TerrainLayer> grounds)
    {
        string path = $"{DataOut}/{from.Replace("Paint Rules - ", "Haven Lake Paint - ")}.asset";
        if (AssetDatabase.LoadAssetAtPath<TerrainPaintRules>(path) == null) AssetDatabase.CopyAsset($"Assets/_Project/Data/World/{from}.asset", path);
        var rules = Load<TerrainPaintRules>(path);
        var replaced = grounds.ToDictionary(pair => (Object)pair.Key, pair => (Object)pair.Value);
        Repoint(rules, replaced);
        return rules;
    }

    private static int Repoint(Object owner, Dictionary<Object, Object> replaced)
    {
        var so = new SerializedObject(owner);
        SerializedProperty it = so.GetIterator();
        int changed = 0;
        while (it.Next(true))
        {
            if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue != null &&
                replaced.TryGetValue(it.objectReferenceValue, out Object made))
            {
                it.objectReferenceValue = made;
                changed++;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        if (changed > 0) EditorUtility.SetDirty(owner);
        return changed;
    }

    // ------------------------------------------------------------------ liveliness

    private const string LakeWindClip = "Assets/VIVID_Arts/Assets/MountainLake/Audio/MLRT_Wind.wav";

    /// <summary>What makes the Haven's groves feel lived-in, on top of Apply: young maples and cedars coming up between the
    /// big trees, a forest floor of old logs, stumps and fallen branches, needles and twigs shed from the crowns, flies,
    /// and the lake pack's own wind recording taking turns with the Haven breeze. Rerunnable.</summary>
    public static string Alive()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return "open SafeHaven first";
        if (scene.isDirty) return "SafeHaven has unsaved changes";
        var log = new List<string>();
        var tree = AssetDatabase.LoadAssetAtPath<GameObject>(TreeTemplate);
        GameObject Young(string source, string name) => VegetationPackConverter.Tree(source, Out + "/Trees", name, tree, windStrength: 1.2f);
        GameObject[] young =
        {
            Load<GameObject>(Raid + "Undergrowth/Maple Sapling A.prefab"), Load<GameObject>(Raid + "Undergrowth/Maple Sapling B.prefab"),
            Young(Lake + "MLRT_SugarMaple_Sapling_02.prefab", "Maple Sapling C"), Young(Lake + "MLRT_SugarMaple_Sapling_03.prefab", "Maple Sapling D"),
            Load<GameObject>(Raid + "Undergrowth/Young Cedar Small.prefab"),
        };
        GameObject[] floor =
        {
            Load<GameObject>(Raid + "Rocks and Logs/Mossy Rotten Log.prefab"), Load<GameObject>(Raid + "Rocks and Logs/Rotten Log A.prefab"),
            Load<GameObject>(Raid + "Rocks and Logs/Stump A.prefab"), Load<GameObject>(Raid + "Rocks and Logs/Stump B.prefab"),
            Load<GameObject>(Raid + "Undergrowth/Mossy Branch Log.prefab"), Load<GameObject>(Raid + "Undergrowth/Branch Pile.prefab"),
            Load<GameObject>(Raid + "Undergrowth/Twig Pile.prefab"),
        };
        ScatterPalette youngPalette = Fixed(Palette("Mossy Boulders", "Haven Lake Young Trees", young, new[] { 1f, 1f, 0.8f, 0.8f, 0.5f }, 1f), new Vector2(0.8f, 1.2f), 3f);
        ScatterPalette floorPalette = Fixed(Palette("Mossy Boulders", "Haven Lake Forest Floor", floor, new[] { 0.8f, 0.6f, 0.7f, 0.5f, 0.6f, 1f, 1f }, 1f), new Vector2(0.85f, 1.15f), 2.5f);
        foreach (string found in AssetDatabase.FindAssets("t:ScatterPalette", new[] { DataOut })) SinkRoots(Load<ScatterPalette>(AssetDatabase.GUIDToAssetPath(found)));
        AssetDatabase.SaveAssets();
        log.Add($"palettes: {young.Length} young trees, {floor.Length} forest-floor pieces");

        int added = 0;
        foreach (BiomeArea grove in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BiomeArea>(true)))
        {
            added += Layer(grove, "Young trees", youngPalette, 1, density: 0.25f, clumping: 0.6f, clumpSize: 12f, edgeFade: 2f, slope: 28f);
            added += Layer(grove, "Forest floor", floorPalette, 2, density: 0.3f, clumping: 0.35f, clumpSize: 16f, edgeFade: 4f, slope: 25f);
        }

        log.Add($"groves: {added} layers added");
        int built = WorldBuilder.BuildChanged(true);
        log.Add($"world rebuilt: {built} terrain(s); {WorldBuilder.LastReport}");
        Terrain terrain = scene.GetRootGameObjects().Select(r => r.GetComponentInChildren<Terrain>(true)).First(t => t != null);
        log.Add(Grass(terrain, Details(), null));
        log.Add(Atmosphere(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add(WorldBuilder.RebakeNavMesh() + " (save SafeHaven again when it finishes)");
        return string.Join("\n", log);
    }

    private static ScatterPalette Fixed(ScatterPalette palette, Vector2 scale, float spacing)
    {
        var so = new SerializedObject(palette);
        SerializedProperty entries = so.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("scale").vector2Value = scale;
            entry.FindPropertyRelative("alignToSlope").floatValue = entry.FindPropertyRelative("prefab").objectReferenceValue is GameObject g && g.GetComponent<ChoppableTree>() != null ? 0f : 0.8f;
            entry.FindPropertyRelative("randomYaw").boolValue = true;
        }

        so.FindProperty("spacing").floatValue = spacing;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(palette);
        return SinkRoots(palette);
    }

    // Adds (or retunes) a named layer in a grove, at a place in its order: big trees first, then what fits between.
    private static int Layer(BiomeArea grove, string name, ScatterPalette palette, int index, float density, float clumping, float clumpSize, float edgeFade, float slope)
    {
        var so = new SerializedObject(grove);
        SerializedProperty layers = so.FindProperty("layers");
        SerializedProperty layer = null;
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name) layer = layers.GetArrayElementAtIndex(i);
        }

        int added = 0;
        if (layer == null)
        {
            int at = Mathf.Min(index, layers.arraySize);
            layers.InsertArrayElementAtIndex(at);
            layer = layers.GetArrayElementAtIndex(at);
            added = 1;
        }

        layer.FindPropertyRelative("name").stringValue = name;
        layer.FindPropertyRelative("palette").objectReferenceValue = palette;
        layer.FindPropertyRelative("density").floatValue = density;
        layer.FindPropertyRelative("slope").vector2Value = new Vector2(0f, slope);
        layer.FindPropertyRelative("limitHeight").boolValue = false;
        layer.FindPropertyRelative("onGround").arraySize = 0;
        layer.FindPropertyRelative("awayFromWater").floatValue = 1.5f;
        layer.FindPropertyRelative("awayFromPaths").floatValue = 2f;
        layer.FindPropertyRelative("awayFromCliffs").floatValue = 1.5f;
        layer.FindPropertyRelative("clumping").floatValue = clumping;
        layer.FindPropertyRelative("clumpSize").floatValue = clumpSize;
        layer.FindPropertyRelative("edgeFade").floatValue = edgeFade;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(grove);
        return added;
    }

    // The raid's canopy debris and flies, and the lake wind as a second ambience track.
    private static string Atmosphere(Scene scene)
    {
        foreach (string old in new[] { "Canopy Debris", "Forest Flies" })
        {
            GameObject found = scene.GetRootGameObjects().FirstOrDefault(root => root.name == old);
            if (found != null) Object.DestroyImmediate(found);
        }

        var debris = Load<GameObject>(Raid + "Effects/Canopy Debris.prefab");
        var placed = (GameObject)PrefabUtility.InstantiatePrefab(debris, scene);
        placed.name = "Canopy Debris";
        var buzz = new GameObject("Forest Flies");
        SceneManager.MoveGameObjectToScene(buzz, scene);
        buzz.AddComponent<AmbientOneShots>().Configure(Load<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Forest Fly.asset"),
            new Vector2(14f, 40f), new Vector2(1.5f, 6f), 0.1f);

        if (AssetImporter.GetAtPath(LakeWindClip) is AudioImporter importer)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        var wind = Load<AudioClip>(LakeWindClip);
        SceneAudio audio = scene.GetRootGameObjects().Select(root => root.GetComponentInChildren<SceneAudio>(true)).First(a => a != null);
        var so = new SerializedObject(audio);
        SerializedProperty tracks = so.FindProperty("ambience.tracks");
        bool has = Enumerable.Range(0, tracks.arraySize).Any(i => tracks.GetArrayElementAtIndex(i).FindPropertyRelative("clip").objectReferenceValue == wind);
        if (!has)
        {
            tracks.arraySize++;
            SerializedProperty track = tracks.GetArrayElementAtIndex(tracks.arraySize - 1);
            track.FindPropertyRelative("clip").objectReferenceValue = wind;
            track.FindPropertyRelative("volume").floatValue = 0.3f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return $"atmosphere: canopy debris, flies, {tracks.arraySize} ambience tracks";
    }

    // ------------------------------------------------------------------ the scene

    // The few trees placed by hand (not by a grove) become maples where they stood, a little apart in size.
    private static string HandPlaced(Scene scene, GameObject[] maples, float size)
    {
        var old = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "rich_oak") old.Add(root);
            if (root.name == "Hand-placed Trees") old.AddRange(root.transform.Cast<Transform>().Select(t => t.gameObject));
        }

        int index = 0;
        foreach (GameObject tree in old)
        {
            if (tree == null || tree.name.StartsWith("Sugar Maple")) continue;
            var made = (GameObject)PrefabUtility.InstantiatePrefab(maples[index % maples.Length], scene);
            made.transform.SetParent(tree.transform.parent, false);
            made.transform.SetPositionAndRotation(tree.transform.position, tree.transform.rotation);
            made.transform.localScale = Vector3.one * size * Mathf.Lerp(0.9f, 1.1f, (index * 0.618f) % 1f);
            made.transform.SetSiblingIndex(tree.transform.GetSiblingIndex());
            made.name = maples[index % maples.Length].name;
            Object.DestroyImmediate(tree);
            index++;
        }

        return $"hand-placed: {index} oaks now maples";
    }

    // Grass and plants grown again over the new grounds, by how much each ground holds (none on paths or rock).
    private static string Grass(Terrain terrain, Dictionary<string, GameObject> details, Dictionary<TerrainLayer, TerrainLayer> grounds)
    {
        var amounts = new Dictionary<string, (float shortGrass, float tall, float bushes, float plants)>
        {
            { "Haven Meadow", (0.95f, 0.45f, 0.1f, 0.55f) }, { "Haven Lush Moss", (0.8f, 0.5f, 0.3f, 0.7f) },
            { "Haven Path", (0f, 0f, 0f, 0f) }, { "Haven Mossy Ground", (0.3f, 0.1f, 0.1f, 0.5f) }, { "Haven Rock", (0f, 0f, 0f, 0f) },
            { "Haven Old Moss", (0.6f, 0.3f, 0.3f, 0.8f) }, { "Haven Bank", (0.15f, 0.1f, 0f, 0.2f) },
            { "Haven Litter", (0.2f, 0.05f, 0.1f, 0.5f) }, { "Haven Humus", (0.25f, 0.1f, 0.1f, 0.4f) },
        };
        GrassSetup.GroundFill[] fills = terrain.terrainData.terrainLayers.Select(layer =>
        {
            amounts.TryGetValue(layer.name, out var a);
            return new GrassSetup.GroundFill { layer = layer, shortGrass = a.shortGrass, tallGrass = a.tall, bushes = a.bushes, plants = a.plants };
        }).ToArray();
        int grown = GrassSetup.FillByGround(terrain, details.Values.ToList(), fills, 11);
        return $"grass: {grown} cells grown over the new grounds";
    }

    // The moss that grows over the forest floor takes its new ground's own textures, without the old leafy sprigs.
    private static string Moss(Terrain terrain)
    {
        if (!terrain.TryGetComponent(out TerrainMoss moss)) return "moss: none";
        var so = new SerializedObject(moss);
        var layer = (TerrainLayer)so.FindProperty("mossLayer").objectReferenceValue;
        if (layer != null)
        {
            so.FindProperty("baseColorMap").objectReferenceValue = layer.diffuseTexture;
            so.FindProperty("normalMap").objectReferenceValue = layer.normalMapTexture;
        }

        so.FindProperty("packedMap").objectReferenceValue = null;
        so.FindProperty("leafyTufts").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        moss.Configure(layer, (Shader)so.FindProperty("mossShader").objectReferenceValue);
        return $"moss: grows on {(layer != null ? layer.name : "nothing")}";
    }

    // Displacement masks for the new grounds, deeper where the ground has relief, and the shader's tiling breakup.
    private static string Surface(Terrain terrain, Dictionary<TerrainLayer, TerrainLayer> grounds)
    {
        Material material = TerrainDisplacementEditor.Apply(terrain);
        TerrainSurfaceProfile profile = terrain.GetComponent<TerrainSurface>().Profile;
        foreach (TerrainSurfaceProfile.Layer entry in profile.layers)
        {
            if (entry.terrainLayer == null || !grounds.ContainsValue(entry.terrainLayer)) continue;
            string name = entry.terrainLayer.name;
            entry.amplitude = name.Contains("Rock") || name.Contains("Mossy") ? 2f : name.Contains("Path") ? 1f : 1.4f;
        }

        EditorUtility.SetDirty(profile);
        material = TerrainDisplacementEditor.Apply(terrain);
        material.SetFloat("_HeightTransition", 0.8f);
        material.SetFloat("_AntiTiling", 0.85f);
        material.SetFloat("_AntiTilingScale", 0.57f);
        material.SetFloat("_MacroVariation", 0.15f);
        TerrainLayer rock = grounds.Values.FirstOrDefault(l => l.name == "Haven Rock");
        if (rock != null)
        {
            material.SetTexture("_CliffAlbedo", rock.diffuseTexture);
            material.SetTexture("_CliffNormal", rock.normalMapTexture);
            material.SetTexture("_CliffMask", rock.maskMapTexture);
            material.SetColor("_CliffTint", Color.white);
        }

        EditorUtility.SetDirty(material);
        return $"surface: {terrain.terrainData.terrainLayers.Length} displaced grounds, tiling broken up";
    }
}
