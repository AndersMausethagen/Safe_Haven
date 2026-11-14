#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The prologue in the new packs: its meadow becomes the Haven's lake biome (the Haven's grounds, grass and palettes) and
/// its dark forest the Misty Woods raid's (the raid's grounds, grass, cedars, rocks and logs, plus undergrowth placed by
/// the raid's habitat rules). The painted forest keeps its shape: each old tree kind becomes a new one in place, so the
/// story route, the camp and the portal stay as they were. Run with SafeHaven's and the raid's conversions done.
/// Entry: BuildPrologueForest.Apply (rerunnable).
/// </summary>
public static class BuildPrologueForest
{
    private const string ScenePath = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
    private const string DataOut = "Assets/_Project/Data/World/Prologue";
    private const string Haven = "Assets/_Project/Art/Vegetation/Safe Haven/";
    private const string HavenData = "Assets/_Project/Data/World/Safe Haven/";
    private const string Raid = "Assets/_Project/Art/Vegetation/Misty Woods/";
    private const string RaidMap = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const string TrunksAsset = "Assets/_Project/Data/Prologue/Vegetation/Prologue Forest Trunks.asset";
    private const string ForestTerrainName = "Raid Terrain";
    private const string MeadowTerrainName = "Meadow Terrain";

    public static string Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return "open the prologue first";
        if (scene.isDirty) return "the prologue has unsaved changes";
        var log = new List<string>();
        Terrain[] terrains = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToArray();
        Terrain forest = terrains.Single(t => t.name == ForestTerrainName);
        Terrain meadow = terrains.Single(t => t.name == MeadowTerrainName);

        // ---- grounds: one map for both terrains, so a road or river crossing the seam stays one ground
        var layers = new Dictionary<string, TerrainLayer>
        {
            { "Meadow Grass", Load<TerrainLayer>(Haven + "Ground/Haven Meadow.terrainlayer") },
            { "Lush Grass", Load<TerrainLayer>(Haven + "Ground/Haven Lush Moss.terrainlayer") },
            { "Trodden Path", Load<TerrainLayer>(Haven + "Ground/Haven Path.terrainlayer") },
            { "Mossy Forest Floor", Load<TerrainLayer>(Haven + "Ground/Haven Mossy Ground.terrainlayer") },
            { "Muddy Bank", Load<TerrainLayer>(Haven + "Ground/Haven Bank.terrainlayer") },
            { "Gravel", Load<TerrainLayer>(Haven + "Ground/Haven Rock.terrainlayer") },
            { "Forest Moss", Load<TerrainLayer>(Raid + "Ground/Cedar Moss.terrainlayer") },
            { "Forest Soil", Load<TerrainLayer>(Raid + "Ground/Cedar Soil.terrainlayer") },
            { "Weathered Stone", Load<TerrainLayer>(Raid + "Ground/Cedar Rock.terrainlayer") },
            { "Pine Needle Floor", Load<TerrainLayer>(Raid + "Ground/Cedar Litter.terrainlayer") },
        };
        var replaced = new Dictionary<Object, Object>();
        foreach (Terrain terrain in terrains)
        {
            foreach (TerrainLayer old in terrain.terrainData.terrainLayers)
            {
                if (old != null && layers.TryGetValue(old.name, out TerrainLayer made)) replaced[old] = made;
            }
        }

        foreach (Terrain terrain in terrains)
        {
            terrain.terrainData.terrainLayers = terrain.terrainData.terrainLayers
                .Select(l => l != null && replaced.TryGetValue(l, out Object made) ? (TerrainLayer)made : l).ToArray();
            EditorUtility.SetDirty(terrain.terrainData);
        }

        log.Add($"grounds: {replaced.Count} swapped in place on both terrains");

        // ---- grass and plants, in place (their cells stay, so the story route stays clear)
        var meadowDetails = new Dictionary<string, GameObject> { { "Glow Flowers", Load<GameObject>(Raid + "Details/Glow Flowers.prefab") } };
        foreach (string name in new[] { "Blueberry", "Bush", "Fern", "Heather", "Lingonberry", "Short Grass", "Tall Grass", "White Flowers", "Yellow Flowers" })
        {
            meadowDetails[name] = Load<GameObject>(Haven + $"Details/{name}.prefab");
        }

        var forestDetails = new Dictionary<string, string>
        {
            { "Short Grass", "Short Grass" }, { "Tall Grass", "Tall Grass" }, { "Bush", "Bush" }, { "Fern", "Common Fern" },
            { "Glow Flowers", "Glow Flowers" }, { "Heather", "Moss Cushion" }, { "Blueberry", "Eagle Fern" },
            { "Lingonberry", "Wild Fern" }, { "White Flowers", "Forest Clover" }, { "Yellow Flowers", "Forest Mushrooms" },
        }.ToDictionary(pair => pair.Key, pair => Load<GameObject>(Raid + $"Details/{pair.Value}.prefab"));
        log.Add($"grass: meadow {SwapDetails(meadow, meadowDetails)}, forest {SwapDetails(forest, forestDetails)} slots swapped");

        // ---- the painted forest: the old kinds become the raid's, in place
        log.Add(ForestTrees(forest));

        // ---- the World Builder's palettes and rules: Haven's for the meadow, the raid's for the forest edge
        var trees = Load<ScatterPalette>(HavenData + "Haven Lake Trees.asset");
        var meadowPalettes = new Dictionary<string, ScatterPalette>
        {
            { "Haven Trees", trees }, { "Mossy Boulders", Load<ScatterPalette>(HavenData + "Haven Lake Boulders.asset") },
            { "Ferns", Load<ScatterPalette>(HavenData + "Haven Lake Ferns.asset") },
            { "River Stones", Load<ScatterPalette>(HavenData + "Haven Lake River Stones.asset") },
            { "Riverbank", Load<ScatterPalette>(HavenData + "Haven Lake Riverbank.asset") },
        };
        GameObject R(string path) => Load<GameObject>(Raid + path + ".prefab");
        var forestPalettes = new Dictionary<string, ScatterPalette>
        {
            { "Dark Forest Trees", Palette("Dark Forest Trees", "Prologue Raid Trees",
                new[] { R("Trees/Cedar A"), R("Trees/Cedar B"), R("Trees/Cedar Tall A"), R("Trees/Cedar Slender"), R("Trees/Cedar Spire"), R("Trees/Dead Cedar") },
                new[] { 1f, 1f, 0.7f, 0.8f, 0.5f, 0.25f }, new Vector2(0.85f, 1.15f)) },
            { "Forest Floor Debris", Palette("Forest Floor Debris", "Prologue Raid Forest Floor",
                new[] { R("Rocks and Logs/Mossy Rotten Log"), R("Rocks and Logs/Rotten Log B"), R("Rocks and Logs/Stump C"), R("Undergrowth/Branch Pile"),
                    R("Undergrowth/Twig Pile"), R("Undergrowth/Mossy Branch Log"), R("Undergrowth/Lichen Branch") },
                new[] { 0.8f, 0.6f, 0.6f, 1f, 1f, 0.7f, 0.7f }, new Vector2(0.85f, 1.15f)) },
            { "Ferns", Palette("Ferns", "Prologue Raid Undergrowth",
                new[] { Load<GameObject>(Haven + "Plants/Haven Fern.prefab"), R("Undergrowth/Broadleaf Shrub"), R("Undergrowth/Forest Shrub A"),
                    R("Undergrowth/Forest Shrub B"), R("Undergrowth/Young Cedar Small"), R("Undergrowth/Cedar Sapling") },
                new[] { 1.2f, 0.6f, 0.8f, 0.8f, 0.4f, 0.4f }, new Vector2(0.8f, 1.2f)) },
            { "Mossy Boulders", Palette("Mossy Boulders", "Prologue Raid Boulders",
                new[] { R("Rocks and Logs/Mossy Boulder A"), R("Rocks and Logs/Mossy Boulder B"), R("Rocks and Logs/Mossy Rock"), R("Rocks and Logs/Mossy Rock Small") },
                new[] { 1f, 1f, 0.8f, 0.8f }, new Vector2(0.8f, 1.2f)) },
        };
        AssetDatabase.SaveAssets();
        foreach (string name in new[] { "Paint Rules - Haven Meadow", "Paint Rules - Dark Forest" })
        {
            replaced[Load<TerrainPaintRules>($"Assets/_Project/Data/World/{name}.asset")] = Rules(name, replaced);
        }

        int pointed = 0, added = 0;
        foreach (MonoBehaviour owner in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                     .Where(b => b is WorldFeature || b is WorldTerrain))
        {
            bool inForest = owner is BiomeArea && owner.name.StartsWith("Forest Edge");
            var map = new Dictionary<Object, Object>(replaced);
            foreach (var pair in inForest ? forestPalettes : meadowPalettes) map[PaletteNamed(pair.Key)] = pair.Value;
            pointed += Repoint(owner, map);
            if (owner is BiomeArea grove)
            {
                if (inForest)
                {
                    added += Layer(grove, "Young trees", Palette("Ferns", "Prologue Raid Young Trees",
                        new[] { R("Undergrowth/Young Cedar Bushy"), R("Undergrowth/Young Cedar Small"), R("Undergrowth/Cedar Sapling"), R("Trees/Young Cedar") },
                        new[] { 1f, 1f, 0.8f, 0.6f }, new Vector2(0.8f, 1.2f), 2.5f), 1, 0.35f);
                }
                else
                {
                    added += Layer(grove, "Young trees", Load<ScatterPalette>(HavenData + "Haven Lake Young Trees.asset"), 1, 0.25f);
                    added += Layer(grove, "Forest floor", Load<ScatterPalette>(HavenData + "Haven Lake Forest Floor.asset"), 2, 0.3f);
                }
            }
        }

        AssetDatabase.SaveAssets();
        log.Add($"world: {pointed} references now the new biomes', {added} grove layers added");

        int built = WorldBuilder.BuildChanged(true);
        log.Add($"world rebuilt: {built} terrain(s); {WorldBuilder.LastReport}");
        log.Add(Undergrowth(forest));
        log.Add(Trunks(scene, forest));
        foreach (Terrain terrain in terrains) log.Add(Surface(terrain));
        log.Add(Debris(scene));
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add(WorldBuilder.RebakeNavMesh() + " (save the prologue again when it finishes)");
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    private static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new System.IO.FileNotFoundException(path);

    private static ScatterPalette PaletteNamed(string name) =>
        AssetDatabase.FindAssets($"t:ScatterPalette {name}").Select(g => AssetDatabase.LoadAssetAtPath<ScatterPalette>(AssetDatabase.GUIDToAssetPath(g)))
            .First(p => p.name == name);

    private static int SwapDetails(Terrain terrain, Dictionary<string, GameObject> swaps)
    {
        TerrainData data = terrain.terrainData;
        DetailPrototype[] prototypes = data.detailPrototypes;
        int swapped = 0;
        foreach (DetailPrototype prototype in prototypes)
        {
            if (prototype.prototype == null) continue;
            // Already swapped on an earlier run: find the old name by the new prefab.
            string old = swaps.FirstOrDefault(pair => pair.Value == prototype.prototype).Key ?? prototype.prototype.name;
            if (!swaps.TryGetValue(old, out GameObject made)) continue;
            prototype.prototype = made;
            prototype.usePrototypeMesh = true;
            prototype.useInstancing = true;
            swapped++;
        }

        data.detailPrototypes = prototypes;
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        return swapped;
    }

    // The painted forest's eight kinds, in their order (the same swap the raid's painted map had).
    private static string ForestTrees(Terrain forest)
    {
        TerrainData data = forest.terrainData;
        var swaps = new Dictionary<string, string>
        {
            { "Spruce A Wind", "Trees/Cedar A" }, { "Spruce B Wind", "Trees/Cedar B" }, { "Spruce C Wind", "Trees/Cedar Tall A" },
            { "Pine Wind", "Trees/Cedar Slender" }, { "Dead Spruce Wind", "Trees/Dead Cedar" },
            { "Boulder A", "Rocks and Logs/Mossy Boulder A" }, { "Boulder B", "Rocks and Logs/Mossy Boulder B" }, { "Fallen Log", "Rocks and Logs/Rotten Log A" },
        };
        TreePrototype[] prototypes = data.treePrototypes;
        int changed = 0;
        foreach (TreePrototype prototype in prototypes)
        {
            if (prototype.prefab != null && swaps.TryGetValue(prototype.prefab.name, out string made))
            {
                prototype.prefab = Load<GameObject>(Raid + made + ".prefab");
                changed++;
            }
        }

        data.treePrototypes = prototypes;
        data.RefreshPrototypes();
        EditorUtility.SetDirty(data);
        return $"painted forest: {changed} of its tree kinds now the raid's";
    }

    // The raid's undergrowth over the painted forest, by the raid's own habitats: saplings in gaps, clutter by trunks,
    // nothing on paths, in riverbeds or where the floor was kept bare (the story route, the camp, the portal).
    private static string Undergrowth(Terrain forest)
    {
        TerrainData data = forest.terrainData;
        var definition = Load<RaidMapDefinition>(RaidMap);
        List<RaidMapDefinition.Scatter> kinds = definition.Undergrowth.Where(s => s.prefab != null).ToList();
        var own = new HashSet<GameObject>(kinds.Select(k => k.prefab));
        List<TreePrototype> prototypes = data.treePrototypes.ToList();
        var index = new int[kinds.Count];
        for (int k = 0; k < kinds.Count; k++)
        {
            index[k] = prototypes.FindIndex(p => p.prefab == kinds[k].prefab);
            if (index[k] < 0)
            {
                prototypes.Add(new TreePrototype { prefab = kinds[k].prefab });
                index[k] = prototypes.Count - 1;
            }
        }

        data.treePrototypes = prototypes.ToArray();
        data.RefreshPrototypes();
        List<TreeInstance> kept = data.treeInstances.Where(t => !own.Contains(prototypes[t.prototypeIndex].prefab)).ToList();

        Vector3 size = data.size;
        int alphaRes = data.alphamapResolution;
        float[,,] alpha = data.GetAlphamaps(0, 0, alphaRes, alphaRes);
        TerrainLayer[] grounds = data.terrainLayers;
        int[] Indices(params string[] names) => Enumerable.Range(0, grounds.Length).Where(i => grounds[i] != null && names.Contains(grounds[i].name)).ToArray();
        int[] path = Indices("Haven Path", "Cedar Soil"), bank = Indices("Haven Bank"), bed = Indices("Haven Rock");
        float Sum(int[] at, float x, float z)
        {
            int ax = Mathf.Clamp(Mathf.RoundToInt(x / size.x * (alphaRes - 1)), 0, alphaRes - 1);
            int az = Mathf.Clamp(Mathf.RoundToInt(z / size.z * (alphaRes - 1)), 0, alphaRes - 1);
            float total = 0f;
            foreach (int i in at) total += alpha[az, ax, i];
            return total;
        }

        int detailRes = data.detailResolution;
        var bare = new bool[detailRes, detailRes];
        for (int y = 0; y < detailRes; y++)
        for (int x = 0; x < detailRes; x++) bare[y, x] = true;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            int[,] cells = data.GetDetailLayer(0, 0, detailRes, detailRes, layer);
            for (int y = 0; y < detailRes; y++)
            for (int x = 0; x < detailRes; x++)
                if (cells[y, x] > 0) bare[y, x] = false;
        }

        var trunks = new Dictionary<Vector2Int, List<Vector2>>();
        Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / 4f), Mathf.FloorToInt(p.y / 4f));
        void Remember(Vector2 p)
        {
            if (!trunks.TryGetValue(Cell(p), out List<Vector2> list)) trunks[Cell(p)] = list = new List<Vector2>();
            list.Add(p);
        }

        float Nearest(Vector2 p)
        {
            float best = 99f;
            Vector2Int c = Cell(p);
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
                if (trunks.TryGetValue(new Vector2Int(c.x + dx, c.y + dz), out List<Vector2> list))
                    foreach (Vector2 q in list) best = Mathf.Min(best, Vector2.Distance(p, q));
            return best;
        }

        foreach (TreeInstance tree in kept) Remember(new Vector2(tree.position.x * size.x, tree.position.z * size.z));

        var random = new System.Random(7341);
        var placed = new List<TreeInstance>();
        var weights = new float[kinds.Count];
        const float spacing = 1.9f;
        for (float z = spacing * 0.5f; z < size.z; z += spacing)
        for (float x = spacing * 0.5f; x < size.x; x += spacing)
        {
            var spot = new Vector2(x + (float)(random.NextDouble() - 0.5) * spacing, z + (float)(random.NextDouble() - 0.5) * spacing);
            if (spot.x < 1f || spot.y < 1f || spot.x > size.x - 1f || spot.y > size.z - 1f) continue;
            float u = spot.x / size.x, v = spot.y / size.z;
            int dx = Mathf.Clamp(Mathf.FloorToInt(u * detailRes), 0, detailRes - 1), dz = Mathf.Clamp(Mathf.FloorToInt(v * detailRes), 0, detailRes - 1);
            float onPath = Sum(path, spot.x, spot.y);
            if (bare[dz, dx] || onPath > 0.15f || Sum(bed, spot.x, spot.y) > 0.35f || Sum(bank, spot.x, spot.y) > 0.5f) continue;

            float edge = 0f;
            for (int a = 0; a < 4; a++)
            {
                Vector2 off = new Vector2(Mathf.Cos(a * Mathf.PI * 0.5f), Mathf.Sin(a * Mathf.PI * 0.5f)) * 4f;
                edge = Mathf.Max(edge, Sum(path, spot.x + off.x, spot.y + off.y));
            }

            float nearest = Nearest(spot);
            var here = new RaidScatterHabitat.Spot(spot, data.GetSteepness(u, v), Mathf.Clamp01(1f - onPath * 2f), Sum(bank, spot.x, spot.y),
                Mathf.Clamp01(edge * 1.5f), 1f - Mathf.Clamp01(nearest / 4f));
            float chance = 0.55f * Mathf.Clamp01(0.1f + here.Forest * 0.8f + here.Edge * 0.6f);
            if (random.NextDouble() > chance) continue;

            float total = 0f;
            for (int k = 0; k < kinds.Count; k++)
            {
                RaidMapDefinition.Scatter kind = kinds[k];
                weights[k] = nearest < Mathf.Max(0.7f, kind.habitat.room) ? 0f : kind.weight * kind.habitat.Suitability(here, k);
                total += weights[k];
            }

            if (total <= 0f) continue;
            double pick = random.NextDouble() * total;
            int chosen = 0;
            while (chosen < kinds.Count - 1 && (pick -= weights[chosen]) > 0) chosen++;
            float scale = Mathf.Lerp(kinds[chosen].scale.x, kinds[chosen].scale.y, (float)random.NextDouble());
            placed.Add(new TreeInstance
            {
                prototypeIndex = index[chosen],
                position = new Vector3(u, data.GetInterpolatedHeight(u, v) / size.y, v),
                rotation = (float)(random.NextDouble() * Mathf.PI * 2f),
                heightScale = scale,
                widthScale = scale,
                color = Color.white,
                lightmapColor = Color.white,
            });
            if (kinds[chosen].prefab.GetComponent<ChoppableTree>() != null) Remember(spot);
        }

        data.SetTreeInstances(kept.Concat(placed).ToArray(), true);
        EditorUtility.SetDirty(data);
        return $"undergrowth: {placed.Count} raid saplings, shrubs and clutter among {kept.Count} forest trees";
    }

    private static string Trunks(Scene scene, Terrain forest)
    {
        Transform holder = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == RaidMapSurface.TrunksName);
        if (holder == null) return "trunks: no holder";
        Mesh mesh = RaidMapSurface.TrunkMesh(forest, holder.position);
        mesh.name = "Prologue Forest Trunks";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(TrunksAsset);
        if (existing != null)
        {
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetTriangles(mesh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, TrunksAsset);
        }

        var collider = holder.GetComponent<MeshCollider>();
        collider.sharedMesh = null;
        collider.sharedMesh = Load<Mesh>(TrunksAsset);
        return $"trunks: {forest.terrainData.treeInstanceCount} instances in the forest's collider";
    }

    // A copy of an old palette (its placement settings kept) holding new prefabs at their own sizes.
    private static ScatterPalette Palette(string from, string name, GameObject[] prefabs, float[] weights, Vector2 scale, float spacing = 0f)
    {
        string path = $"{DataOut}/{name}.asset";
        VegetationPackConverter.EnsureFolder(DataOut);
        if (AssetDatabase.LoadAssetAtPath<ScatterPalette>(path) == null) AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(PaletteNamed(from)), path);
        var palette = Load<ScatterPalette>(path);
        var so = new SerializedObject(palette);
        SerializedProperty entries = so.FindProperty("entries");
        entries.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            bool tree = prefabs[i].GetComponent<ChoppableTree>() != null;
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefabs[i];
            entry.FindPropertyRelative("weight").floatValue = weights[i];
            entry.FindPropertyRelative("scale").vector2Value = scale;
            entry.FindPropertyRelative("alignToSlope").floatValue = tree ? 0f : 0.8f;
            entry.FindPropertyRelative("randomYaw").boolValue = true;
            entry.FindPropertyRelative("sink").floatValue = 0.05f + (tree ? BelowPivot(prefabs[i]) * (scale.x + scale.y) * 0.5f : 0f);
        }

        if (spacing > 0f) so.FindProperty("spacing").floatValue = spacing;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(palette);
        return palette;
    }

    // How far a tree's model reaches below its pivot (roots): the World Builder sets things on their lowest point.
    private static float BelowPivot(GameObject prefab)
    {
        Renderer[] drawn = prefab.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        return drawn.Length == 0 ? 0f : Mathf.Max(0f, prefab.transform.position.y - drawn.Min(r => r.bounds.min.y));
    }

    private static TerrainPaintRules Rules(string from, Dictionary<Object, Object> replaced)
    {
        string path = $"{DataOut}/{from.Replace("Paint Rules - ", "Prologue Paint - ")}.asset";
        VegetationPackConverter.EnsureFolder(DataOut);
        if (AssetDatabase.LoadAssetAtPath<TerrainPaintRules>(path) == null) AssetDatabase.CopyAsset($"Assets/_Project/Data/World/{from}.asset", path);
        var rules = Load<TerrainPaintRules>(path);
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

    // Adds (or retunes) a named grove layer at a place in its order: big trees first, then what fits between.
    private static int Layer(BiomeArea grove, string name, ScatterPalette palette, int index, float density)
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
        layer.FindPropertyRelative("slope").vector2Value = new Vector2(0f, 28f);
        layer.FindPropertyRelative("limitHeight").boolValue = false;
        layer.FindPropertyRelative("onGround").arraySize = 0;
        layer.FindPropertyRelative("awayFromWater").floatValue = 1.5f;
        layer.FindPropertyRelative("awayFromPaths").floatValue = 2f;
        layer.FindPropertyRelative("awayFromCliffs").floatValue = 1.5f;
        layer.FindPropertyRelative("clumping").floatValue = 0.5f;
        layer.FindPropertyRelative("clumpSize").floatValue = 14f;
        layer.FindPropertyRelative("edgeFade").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(grove);
        return added;
    }

    // Displacement masks for the new grounds, deeper where the ground has relief, and the shader's tiling breakup.
    private static string Surface(Terrain terrain)
    {
        if (!terrain.TryGetComponent(out TerrainSurface surface) || surface.Profile == null) return $"{terrain.name}: no surface profile";
        TerrainDisplacementEditor.Apply(terrain);
        foreach (TerrainSurfaceProfile.Layer entry in surface.Profile.layers)
        {
            if (entry.terrainLayer == null) continue;
            string name = entry.terrainLayer.name;
            entry.amplitude = name.Contains("Rock") || name.Contains("Mossy") ? 2f : name.Contains("Path") || name.Contains("Soil") ? 1f : 1.4f;
        }

        EditorUtility.SetDirty(surface.Profile);
        Material material = TerrainDisplacementEditor.Apply(terrain);
        material.SetFloat("_HeightTransition", 0.8f);
        material.SetFloat("_AntiTiling", 0.85f);
        material.SetFloat("_AntiTilingScale", 0.57f);
        material.SetFloat("_MacroVariation", 0.15f);
        TerrainLayer rock = terrain.terrainData.terrainLayers.FirstOrDefault(l => l != null && (l.name == "Cedar Rock" || l.name == "Haven Rock"));
        if (rock != null)
        {
            material.SetTexture("_CliffAlbedo", rock.diffuseTexture);
            material.SetTexture("_CliffNormal", rock.normalMapTexture);
            material.SetTexture("_CliffMask", rock.maskMapTexture);
            material.SetColor("_CliffTint", Color.white);
        }

        EditorUtility.SetDirty(material);
        return $"{terrain.name}: {terrain.terrainData.terrainLayers.Length} displaced grounds";
    }

    // The raid's needles and twigs falling from the crowns.
    private static string Debris(Scene scene)
    {
        GameObject found = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Canopy Debris");
        if (found != null) Object.DestroyImmediate(found);
        var placed = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Raid + "Effects/Canopy Debris.prefab"), scene);
        placed.name = "Canopy Debris";
        return "atmosphere: canopy debris";
    }
}
