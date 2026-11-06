using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;
using Debug = UnityEngine.Debug;

/// <summary>
/// Safe Haven › World › Generate Haven World: turns a HavenWorldRecipe (through a HavenWorldPlan) into the Haven's world, as a baseline
/// a designer refines by hand. What the World Builder already does, it does: rivers, paths, cliffs and outcrops, ravines and maple groves
/// are ordinary World Builder features (each copied from the scene's own tuned feature of its kind, so the Haven's palettes, layers and
/// materials carry over) and the World Builder shapes, paints and fills them. Around them it lays what the World Builder has no feature
/// for: the land's shape (rings, mountain, lake basin, rim), the ground's paint (the Haven's and the Dark Forest's paint rules, blended by
/// distance), the lake and ponds, the woods as terrain trees (the raids' forest and its habitats, cheap at this size and fellable),
/// grass by ground, points of interest, creature spawn zones and resource points.
///
/// Three steps, each runnable alone from the command line: <see cref="Prepare"/> (everything up to the features), <see cref="Build"/>
/// (the World Builder builds them all) and <see cref="Finish"/> (what stands on the finished ground, moss, displacement, navmesh).
/// It keeps the gameplay: the portal, buildings, residents, quest and story objects (05 Gameplay, 06 Story &amp; Events) and the
/// starting overgrowth. It never saves the scene.
/// </summary>
public static class HavenWorldGenerator
{
    public const string RecipePath = "Assets/_Project/Data/World/Safe Haven/Haven World Recipe.asset";
    public const string ContentName = "Haven World";
    private const string WorldRootName = "World";
    private static readonly string[] Regenerated = { "Tarn", "Hand-placed Trees" };

    [MenuItem("Safe Haven/World/Generate Haven World")]
    private static void Menu()
    {
        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(RecipePath);
        if (recipe == null)
        {
            EditorUtility.DisplayDialog("Generate Haven World", $"No recipe at {RecipePath}.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Generate Haven World",
                "This replaces the Haven's terrain, World Builder features, woods, water, points of interest and spawns with a newly generated " +
                "world. The portal, buildings, residents, quest and story objects stay. The scene is not saved. Continue?", "Generate", "Cancel"))
        {
            return;
        }

        Debug.Log(Generate(recipe));
    }

    public static string Generate(HavenWorldRecipe recipe) => Prepare(recipe) + "\n" + Build() + "\n" + Finish(recipe);

    // ----- Step 1 -----

    public static string Prepare(HavenWorldRecipe recipe)
    {
        var clock = Stopwatch.StartNew();
        var report = new System.Text.StringBuilder("Haven world: ");
        Terrain terrain = Terrain.activeTerrain;
        WorldTerrain world = terrain != null ? terrain.GetComponent<WorldTerrain>() : null;
        if (terrain == null || world == null)
        {
            return "Haven world: no World Builder terrain in the scene.";
        }

        HavenWorldPlan plan = HavenWorldPlan.Make(recipe);
        report.Append($"plan {clock.ElapsedMilliseconds} ms ({plan.Paths.Count} paths, {plan.Rivers.Count} rivers, {plan.Ponds.Count} ponds, {plan.Islands.Count} islands, " +
                      $"{plan.Outcrops.Count} outcrops, {plan.Cliffs.Count} cliffs, {plan.Ravines.Count} ravines, {plan.Groves.Count} groves, {plan.Pois.Count} places, " +
                      $"{plan.Zones.Count} spawn zones, {plan.Nodes.Count} resource points); ");

        SetBuildAutomatically(world, false);
        var templates = new Templates(LiveFeatures());
        Material still = StillWater();
        var made = new GameObject("World (generated)").transform;
        MakeFeatures(plan, templates, made);
        ClearOld(made);
        report.Append($"features made, old cleared {clock.ElapsedMilliseconds} ms; ");

        Resize(terrain, world, recipe);
        float[,] heights = Heights(plan, terrain);
        report.Append($"terrain {clock.ElapsedMilliseconds} ms; ");
        Paint(plan, terrain, heights);
        report.Append($"paint {clock.ElapsedMilliseconds} ms; ");
        Transform content = ContentRoot();
        MakeWater(plan, terrain, still, content);
        int trees = Woods(plan, terrain);
        report.Append($"woods {trees} trees {clock.ElapsedMilliseconds} ms; ");
        int grass = GrassSetup.FillByGround(terrain, GrassSetup.EnsureAssets(), GrassSetup.DefaultFill(terrain.terrainData), recipe.seed);
        report.Append($"grass {grass} cells {clock.ElapsedMilliseconds} ms; ");

        Transform root = WorldRoot();
        foreach (Transform feature in made.Cast<Transform>().ToList())
        {
            feature.SetParent(root, true);
        }

        Object.DestroyImmediate(made.gameObject);
        templates.Dispose();
        SetBuildAutomatically(world, true);
        EditorUtility.SetDirty(terrain.terrainData);
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        return report.Append($"done in {clock.ElapsedMilliseconds} ms.").ToString();
    }

    private static List<WorldFeature> LiveFeatures() =>
        Object.FindObjectsByType<WorldFeature>(FindObjectsInactive.Include).Where(feature => feature.gameObject.scene.IsValid()).ToList();

    private static void SetBuildAutomatically(WorldTerrain world, bool on)
    {
        var data = new SerializedObject(world);
        data.FindProperty("buildAutomatically").boolValue = on;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Material StillWater()
    {
        foreach (WaterSurface water in Object.FindObjectsByType<WaterSurface>(FindObjectsInactive.Include))
        {
            if (water.IsStill && water.TryGetComponent(out MeshRenderer renderer) && renderer.sharedMaterial != null)
            {
                return renderer.sharedMaterial;
            }
        }

        return AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Generated/Water/Water Still.mat");
    }

    // The world's features, its water and woods are regenerated; the gameplay stays (05 Gameplay, 06 Story & Events, the overgrowth).
    private static void ClearOld(Transform keep)
    {
        foreach (WorldFeature feature in LiveFeatures())
        {
            if (!feature.transform.IsChildOf(keep))
            {
                Object.DestroyImmediate(feature.gameObject);
            }
        }

        GameObject worldFolder = SceneFolders.Find(EditorSceneManager.GetActiveScene(), "04 World");
        if (worldFolder != null)
        {
            foreach (string name in Regenerated)
            {
                Transform old = worldFolder.transform.Find(name);
                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }
            }
        }

        GameObject content = SceneFolders.Find(EditorSceneManager.GetActiveScene(), ContentName);
        if (content != null)
        {
            Object.DestroyImmediate(content);
        }
    }

    // ----- Terrain -----

    // Centred on the portal, at the recipe's size and resolutions. The stash's cells no longer line up, so it starts over (Prepare).
    private static void Resize(Terrain terrain, WorldTerrain world, HavenWorldRecipe recipe)
    {
        TerrainData data = terrain.terrainData;
        data.heightmapResolution = recipe.heightResolution;
        data.size = new Vector3(recipe.size, recipe.depthAndHeight.y, recipe.size);
        data.alphamapResolution = recipe.paintResolution;
        data.SetDetailResolution(recipe.grassResolution, 32);
        data.SetTreeInstances(new TreeInstance[0], false);
        float half = recipe.size * 0.5f;
        terrain.transform.position = new Vector3(-half, -recipe.depthAndHeight.x, -half);
        if (world.Stash != null)
        {
            world.Stash.Trees.Clear();
            world.Stash.Prepare(data.heightmapResolution, data.alphamapResolution, data.alphamapLayers, data.detailResolution);
            EditorUtility.SetDirty(world.Stash);
        }

        world.BuiltFeatures.Clear();
    }

    // The plan's ground, row by row on every core, as the terrain takes it (0-1 of its height). Returns metres above the portal's ground.
    private static float[,] Heights(HavenWorldPlan plan, Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        int resolution = data.heightmapResolution;
        Vector3 origin = terrain.transform.position;
        float depth = -origin.y, height = data.size.y, size = data.size.x;
        var metres = new float[resolution, resolution];
        var scaled = new float[resolution, resolution];
        Parallel.For(0, resolution, row =>
        {
            for (int column = 0; column < resolution; column++)
            {
                var point = new Vector2(origin.x + column / (resolution - 1f) * size, origin.z + row / (resolution - 1f) * size);
                float h = plan.Height(point);
                metres[row, column] = h;
                scaled[row, column] = Mathf.Clamp01((h + depth) / height);
            }
        });
        data.SetHeights(0, 0, scaled);
        terrain.Flush();
        return metres;
    }

    // The Haven's meadow rules in the middle and the Dark Forest's in the woods, blended by distance; scree on the mountain, banks at the water.
    private static void Paint(HavenWorldPlan plan, Terrain terrain, float[,] heights)
    {
        TerrainData data = terrain.terrainData;
        TerrainPaintRules haven = WorldLook.ForestLake.Rules("Haven Meadow");
        TerrainPaintRules woods = WorldLook.MossyForest.Rules("Dark Forest");
        TerrainLayer[] layers = data.terrainLayers;
        int Index(string name) => System.Array.FindIndex(layers, layer => layer != null && layer.name == name);
        // By name: the Haven's terrain carries its own copies of the looks' grounds.
        int[] havenIndex = haven.Rules.Select(rule => rule.layer != null ? Index(rule.layer.name) : -1).ToArray();
        int[] woodsIndex = woods.Rules.Select(rule => rule.layer != null ? Index(rule.layer.name) : -1).ToArray();
        int meadow = Index("Haven Meadow"), litter = Index("Cedar Litter"), scree = Index("Gravel"), mud = Index("Muddy Bank"), bank = Index("Haven Bank");
        int count = layers.Length, resolution = data.alphamapResolution, heightsCount = heights.GetLength(0);
        Vector3 origin = terrain.transform.position;
        float size = data.size.x, cell = size / (heightsCount - 1);
        var maps = new float[resolution, resolution, count];

        float HeightAt(float u, float v)
        {
            float x = Mathf.Clamp(u * (heightsCount - 1), 0f, heightsCount - 1.001f), y = Mathf.Clamp(v * (heightsCount - 1), 0f, heightsCount - 1.001f);
            int x0 = (int)x, y0 = (int)y;
            float tx = x - x0, ty = y - y0;
            return Mathf.Lerp(Mathf.Lerp(heights[y0, x0], heights[y0, x0 + 1], tx), Mathf.Lerp(heights[y0 + 1, x0], heights[y0 + 1, x0 + 1], tx), ty);
        }

        Parallel.For(0, resolution, y =>
        {
            var a = new float[count];
            var b = new float[count];
            float du = WorldPaint.HollowRadius / size;
            for (int x = 0; x < resolution; x++)
            {
                float u = (x + 0.5f) / resolution, v = (y + 0.5f) / resolution;
                var point = new Vector2(origin.x + u * size, origin.z + v * size);
                float h = HeightAt(u, v);
                float dx = HeightAt(u + cell / size, v) - HeightAt(u - cell / size, v);
                float dz = HeightAt(u, v + cell / size) - HeightAt(u, v - cell / size);
                float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz) / (2f * cell)) * Mathf.Rad2Deg;
                float around = (HeightAt(u - du, v) + HeightAt(u + du, v) + HeightAt(u, v - du) + HeightAt(u, v + du)) * 0.25f;
                float hollow = WorldPaint.Hollowness(h, around, WorldPaint.HollowRadius);
                System.Array.Clear(a, 0, count);
                System.Array.Clear(b, 0, count);
                if (meadow >= 0) a[meadow] = 1f;
                if (litter >= 0) b[litter] = 1f;
                Apply(haven, havenIndex, a, slope, h, hollow, point);
                Apply(woods, woodsIndex, b, slope, h, hollow, point);
                float share = plan.WoodsWeight(point);
                float mountain = plan.MountainWeight(point) * 0.55f;
                float lakeEdge = plan.Lake.Edge(point);
                float pondEdge = float.MaxValue;
                foreach (RaidLayoutPond pond in plan.Ponds)
                {
                    pondEdge = Mathf.Min(pondEdge, pond.Edge(point));
                }

                float total = 0f;
                for (int l = 0; l < count; l++)
                {
                    float w = Mathf.Lerp(a[l], b[l], share) * (1f - mountain);
                    if (l == scree) w += mountain;
                    a[l] = w;
                }

                Blend(a, mud, 1f - Mathf.Clamp01((lakeEdge + 1f) / 8f));
                Blend(a, bank, 1f - Mathf.Clamp01((pondEdge + 0.5f) / 3f));
                for (int l = 0; l < count; l++) total += a[l];
                for (int l = 0; l < count; l++) maps[y, x, l] = total > 0f ? a[l] / total : 0f;
            }
        });
        data.SetAlphamaps(0, 0, maps);
    }

    private static void Apply(TerrainPaintRules rules, int[] index, float[] weights, float slope, float height, float hollow, Vector2 point)
    {
        for (int r = 0; r < index.Length; r++)
        {
            if (index[r] < 0)
            {
                continue;
            }

            TerrainPaintRules.Rule rule = rules.Rules[r];
            float noise = WorldNoise2D.Fbm(point.x / rule.patchSize, point.y / rule.patchSize, rules.Seed + r * 17);
            float w = WorldPaint.RuleWeight(rule, slope, height, hollow, noise);
            if (w <= 0f)
            {
                continue;
            }

            for (int l = 0; l < weights.Length; l++)
            {
                weights[l] = weights[l] * (1f - w) + (l == index[r] ? w : 0f);
            }
        }
    }

    private static void Blend(float[] weights, int layer, float amount)
    {
        if (layer < 0 || amount <= 0f)
        {
            return;
        }

        for (int l = 0; l < weights.Length; l++)
        {
            weights[l] = weights[l] * (1f - amount) + (l == layer ? amount : 0f);
        }
    }

    // ----- Water -----

    private static void MakeWater(HavenWorldPlan plan, Terrain terrain, Material material, Transform content)
    {
        var holder = new GameObject("Water").transform;
        holder.SetParent(content, false);
        Water(holder, "Lake", plan.Lake, material, true);
        for (int i = 0; i < plan.Ponds.Count; i++)
        {
            Water(holder, $"Pond {i + 1}", plan.Ponds[i], material, true);
        }
    }

    // A disc running a little past the shore, under the banks: the terrain draws the waterline (as the raids' ponds).
    private static void Water(Transform holder, string name, RaidLayoutPond water, Material material, bool drinkable)
    {
        const int points = 64;
        var go = new GameObject(name);
        go.transform.SetParent(holder, false);
        go.transform.position = new Vector3(water.Position.x, water.Level, water.Position.y);
        var vertices = new Vector3[points + 1];
        var normals = new Vector3[points + 1];
        var uvs = new Vector2[points + 1];
        var triangles = new int[points * 3];
        normals[0] = Vector3.up;
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < points; i++)
        {
            Vector2 rim = water.Rim(i * Mathf.PI * 2f / points, 2f) - water.Position;
            vertices[i + 1] = new Vector3(rim.x, 0f, rim.y);
            normals[i + 1] = Vector3.up;
            uvs[i + 1] = new Vector2(0.5f + rim.x * 0.5f / water.Reach, 0.5f + rim.y * 0.5f / water.Reach);
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % points + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var surface = go.AddComponent<WaterSurface>();
        surface.IsStill = true;
        surface.Drinkable = drinkable;
    }

    // ----- Woods -----

    // The raids' forest painted as terrain trees: its trees, undergrowth and mineable rocks by their habitats, thick where the woods are,
    // thin in the Haven's ring, none in the buildable middle, the water or the clearings of the places.
    private static int Woods(HavenWorldPlan plan, Terrain terrain)
    {
        HavenWorldRecipe recipe = plan.Recipe;
        if (recipe.woods == null)
        {
            return 0;
        }

        TerrainData data = terrain.terrainData;
        RaidMapDefinition scaled = recipe.woods.ScaledBy(recipe.size / recipe.woods.Size);
        const int resolution = 512;
        var path = new byte[resolution * resolution];
        var forest = new byte[resolution * resolution];
        var clearing = new byte[resolution * resolution];
        Vector3 origin = terrain.transform.position;
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                var point = new Vector2(origin.x + (x + 0.5f) / resolution * recipe.size, origin.z + (y + 0.5f) / resolution * recipe.size);
                // The raids' cedars only where the woods begin; the Haven's ring has its maple groves.
                float woods = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, plan.WoodsWeight(point)));
                float density = Mathf.Lerp(recipe.woodsDensity.x, recipe.woodsDensity.y, Mathf.InverseLerp(recipe.haven, plan.Half, point.magnitude)) *
                                woods * (1f - plan.MountainWeight(point) * 0.55f);
                bool open = point.magnitude < recipe.buildable + 20f || plan.Lake.Edge(point) < 4f || plan.Pois.Exists(poi => Vector2.Distance(poi.At, point) < poi.Rule.clearing);
                forest[y * resolution + x] = (byte)Mathf.RoundToInt(Mathf.Clamp01(density) * 255f);
                clearing[y * resolution + x] = open ? (byte)255 : (byte)0;
            }
        }

        scaled.UseGenerated(resolution, path, forest, clearing, new RaidMapMood(), recipe.seed);
        System.Func<Vector2, bool> water = local =>
        {
            Vector2 point = local + new Vector2(origin.x, origin.z);
            return plan.Lake.Edge(point) < 1f || plan.Ponds.Exists(pond => pond.Edge(point) < 1f) || HavenWorldPlan.Near(plan.Rivers, point, 7f);
        };
        System.Func<Vector2, float> wet = local =>
        {
            Vector2 point = local + new Vector2(origin.x, origin.z);
            return 1f - Mathf.Clamp01(plan.Lake.Edge(point) / 25f);
        };
        int trees = RaidMapSurface.Scatter(data, scaled, new List<RaidMapSurface.Clearing>(), out int rocks, wet, water);
        Object.DestroyImmediate(scaled);
        terrain.Flush();
        return trees + rocks;
    }

    // ----- World Builder features -----

    // One of each kind from the scene, so new features carry the Haven's tuned palettes, layers and materials.
    private sealed class Templates : System.IDisposable
    {
        private readonly List<GameObject> copies = new List<GameObject>();
        public RiverFeature River;
        public PathFeature Path;
        public CliffFeature Cliff;
        public BiomeArea Grove;

        public Templates(List<WorldFeature> features)
        {
            River = Keep(features.OfType<RiverFeature>().FirstOrDefault());
            Path = Keep(features.OfType<PathFeature>().FirstOrDefault());
            Cliff = Keep(features.OfType<CliffFeature>().FirstOrDefault());
            Grove = Keep(features.OfType<BiomeArea>().FirstOrDefault(area => area.Layers.Count > 0 && area.Biome == null)
                         ?? features.OfType<BiomeArea>().FirstOrDefault(area => area.Layers.Count > 0));
        }

        // A hidden, inactive copy (the World Builder ignores it), so the original can be cleared away.
        private T Keep<T>(T feature) where T : WorldFeature
        {
            if (feature == null)
            {
                return null;
            }

            var go = new GameObject("Template " + typeof(T).Name, typeof(SplineContainer)) { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            var copy = go.AddComponent<T>();
            EditorUtility.CopySerialized(feature, copy);
            copies.Add(go);
            return copy;
        }

        public void Dispose()
        {
            foreach (GameObject copy in copies)
            {
                Object.DestroyImmediate(copy);
            }
        }
    }

    private static void MakeFeatures(HavenWorldPlan plan, Templates templates, Transform parent)
    {
        WorldLook haven = WorldLook.ForestLake, woods = WorldLook.MossyForest;
        TerrainLayer rock = TerrainRock.CliffRock != null ? TerrainRock.CliffRock : woods.RockGround;
        int seed = plan.Recipe.seed;
        for (int i = 0; i < plan.Rivers.Count; i++)
        {
            RiverFeature river = Make(parent, $"River {i + 1}", templates.River, feature => feature.Configure(WorldAssets.RiverWater(), haven.RiverBed, haven.RiverBank, haven.Palette("River Stones"), haven.Palette("Riverbank")));
            Line(river, plan, plan.Rivers[i], false, seed + i);
            Set(river, "width", 4.5f + i);
            Set(river, "depth", 1.3f);
        }

        for (int i = 0; i < plan.Paths.Count; i++)
        {
            PathFeature path = Make(parent, $"Path {i + 1}", templates.Path, feature => feature.Configure(haven.PathGround, null));
            Line(path, plan, plan.Paths[i], false, seed + 100 + i);
        }

        for (int i = 0; i < plan.Outcrops.Count; i++)
        {
            HavenWorldPlan.Outcrop outcrop = plan.Outcrops[i];
            CliffFeature cliff = Make(parent, $"Outcrop {i + 1}", templates.Cliff, feature => feature.Configure(null, woods.Palette("Rubble"), rock));
            Line(cliff, plan, outcrop.Loop, true, seed + 200 + i);
            Set(cliff, "height", outcrop.Height);
            Set(cliff, "plateau", outcrop.Radius);
            SetEnum(cliff, "highSide", (int)CliffFeature.HighSide.Left);
            SetEnum(cliff, "shaping", (int)CliffFeature.Shaping.Raise);
        }

        for (int i = 0; i < plan.Cliffs.Count; i++)
        {
            HavenWorldPlan.Cliff line = plan.Cliffs[i];
            CliffFeature cliff = Make(parent, $"Mountain Cliff {i + 1}", templates.Cliff, feature => feature.Configure(null, woods.Palette("Rubble"), rock));
            Line(cliff, plan, line.Line, false, seed + 300 + i);
            Set(cliff, "height", line.Height);
            Set(cliff, "plateau", 30f);
            SetEnum(cliff, "highSide", (int)(line.HighOnLeft ? CliffFeature.HighSide.Left : CliffFeature.HighSide.Right));
            SetEnum(cliff, "shaping", (int)CliffFeature.Shaping.Raise);
        }

        for (int i = 0; i < plan.Ravines.Count; i++)
        {
            HavenWorldPlan.Ravine line = plan.Ravines[i];
            RavineFeature ravine = Make<RavineFeature>(parent, $"Ravine {i + 1}", null,
                feature => feature.Configure(null, woods.Palette("Rubble"), rock, woods.Ground("Cedar Soil"), woods.Ground("Cedar Moss")));
            Line(ravine, plan, line.Line, false, seed + 400 + i);
            Set(ravine, "depth", line.Depth);
            Set(ravine, "floorWidth", line.FloorWidth);
        }

        for (int i = 0; i < plan.Groves.Count; i++)
        {
            BiomeArea grove = Make(parent, $"Grove {i + 1}", templates.Grove, feature => feature.Configure(WorldMenus.HavenGroveLayers()));
            Line(grove, plan, plan.Groves[i], true, seed + 500 + i);
        }
    }

    // A new feature, copied from the template of its kind (or configured from the look when there is none), with a clean spline.
    private static T Make<T>(Transform parent, string name, T template, System.Action<T> configure) where T : WorldFeature
    {
        var go = new GameObject(name, typeof(SplineContainer));
        go.transform.SetParent(parent, false);
        go.GetComponent<SplineContainer>().Spline.Clear();
        var feature = go.AddComponent<T>();
        if (template != null)
        {
            EditorUtility.CopySerialized(template, feature);
            ClearKnotValues(feature);
        }
        else
        {
            configure(feature);
        }

        return feature;
    }

    // Copied per-knot values belong to the template's knots: the new spline starts with none (each knot counts 1).
    private static void ClearKnotValues(WorldFeature feature)
    {
        var data = new SerializedObject(feature);
        SerializedProperty property = data.GetIterator();
        while (property.NextVisible(true))
        {
            if (property.type == nameof(KnotValues))
            {
                SerializedProperty values = property.FindPropertyRelative("values");
                if (values != null)
                {
                    values.arraySize = 0;
                }
            }
        }

        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Line(WorldFeature feature, HavenWorldPlan plan, List<Vector2> points, bool closed, int seed)
    {
        Spline spline = feature.Spline;
        spline.Clear();
        foreach (Vector2 point in points)
        {
            spline.Add(new BezierKnot(new Unity.Mathematics.float3(point.x, plan.Height(point), point.y)), TangentMode.AutoSmooth);
        }

        spline.Closed = closed;
        var data = new SerializedObject(feature);
        data.FindProperty("seed").intValue = seed;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Set(Object target, string field, float value)
    {
        var data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field);
        if (property != null)
        {
            property.floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetEnum(Object target, string field, int value)
    {
        var data = new SerializedObject(target);
        SerializedProperty property = data.FindProperty(field);
        if (property != null)
        {
            property.enumValueIndex = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Transform WorldRoot()
    {
        GameObject found = SceneFolders.Find(EditorSceneManager.GetActiveScene(), WorldRootName);
        if (found != null)
        {
            return found.transform;
        }

        var root = new GameObject(WorldRootName);
        SceneFolders.Place(root);
        return root.transform;
    }

    private static Transform ContentRoot()
    {
        GameObject found = SceneFolders.Find(EditorSceneManager.GetActiveScene(), ContentName);
        if (found != null)
        {
            return found.transform;
        }

        var root = new GameObject(ContentName);
        SceneFolders.Place(root);
        return root.transform;
    }

    // ----- Step 2 -----

    /// <summary>The World Builder builds every feature (shapes, paints and fills the terrain, makes their objects).</summary>
    public static string Build()
    {
        var clock = Stopwatch.StartNew();
        int built = WorldBuilder.BuildChanged(true);
        return $"World Builder built {built} feature(s) in {clock.ElapsedMilliseconds} ms: {WorldBuilder.LastReport}";
    }

    // ----- Step 3 -----

    /// <summary>What stands on the finished ground (places, spawn zones, resource points), then moss, displacement and the navmesh.</summary>
    public static string Finish(HavenWorldRecipe recipe)
    {
        var clock = Stopwatch.StartNew();
        Terrain terrain = Terrain.activeTerrain;
        HavenWorldPlan plan = HavenWorldPlan.Make(recipe);
        Transform content = ContentRoot();
        foreach (string name in new[] { "Points of Interest", "Spawn Zones", "Resources" })
        {
            Transform old = content.Find(name);
            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }
        }

        Vector3 Ground(Vector2 point)
        {
            var at = new Vector3(point.x, 0f, point.y);
            at.y = terrain.SampleHeight(at) + terrain.transform.position.y;
            return at;
        }

        var places = new GameObject("Points of Interest").transform;
        places.SetParent(content, false);
        foreach (HavenWorldPlan.Poi poi in plan.Pois)
        {
            var made = (GameObject)PrefabUtility.InstantiatePrefab(poi.Rule.prefab, places);
            made.name = poi.Rule.name;
            made.transform.SetPositionAndRotation(Ground(poi.At), Quaternion.Euler(0f, poi.Turn, 0f));
            foreach (Transform part in made.transform)
            {
                Vector3 position = part.position;
                position.y = Ground(new Vector2(position.x, position.z)).y + part.localPosition.y;
                part.position = position;
            }
        }

        var zones = new GameObject("Spawn Zones").transform;
        zones.SetParent(content, false);
        foreach (HavenWorldPlan.Zone zone in plan.Zones)
        {
            var go = new GameObject($"Spawn Zone ({zone.Band.name})");
            go.transform.SetParent(zones, false);
            go.transform.position = Ground(zone.At);
            var spawner = go.AddComponent<MobSpawnZone>();
            var corners = new List<Vector3>();
            for (int k = 0; k < 6; k++)
            {
                Vector2 offset = HavenWorldPlan.Dir(k * 60f) * zone.Radius;
                corners.Add(new Vector3(offset.x, Ground(zone.At + offset).y - go.transform.position.y, offset.y));
            }

            spawner.SetCorners(corners);
            var data = new SerializedObject(spawner);
            SerializedProperty creatures = data.FindProperty("creatures");
            creatures.arraySize = zone.Band.creatures.Count;
            for (int k = 0; k < zone.Band.creatures.Count; k++)
            {
                SerializedProperty entry = creatures.GetArrayElementAtIndex(k);
                entry.FindPropertyRelative("prefab").objectReferenceValue = zone.Band.creatures[k].prefab;
                entry.FindPropertyRelative("weight").floatValue = zone.Band.creatures[k].weight;
                entry.FindPropertyRelative("maxPerGroup").intValue = zone.Band.creatures[k].maxPerGroup;
            }

            data.FindProperty("count").vector2IntValue = zone.Band.group;
            data.FindProperty("trigger").enumValueIndex = (int)MobSpawnTrigger.PlayerNear;
            data.FindProperty("wakeDistance").floatValue = 30f;
            data.FindProperty("respawn").boolValue = zone.Band.respawnSeconds > 0f;
            data.FindProperty("respawnSeconds").floatValue = zone.Band.respawnSeconds;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        var resources = new GameObject("Resources").transform;
        resources.SetParent(content, false);
        foreach (HavenWorldPlan.Node node in plan.Nodes)
        {
            var go = new GameObject($"Resource ({node.Band.name})");
            go.transform.SetParent(resources, false);
            go.transform.position = Ground(node.At);
            var point = go.AddComponent<RandomResourceNode>();
            var data = new SerializedObject(point);
            data.FindProperty("kind").enumValueIndex = (int)node.Band.kind;
            data.FindProperty("tiers").vector2IntValue = node.Band.tiers;
            data.FindProperty("stablePick").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        long placed = clock.ElapsedMilliseconds;
        if (terrain.TryGetComponent(out TerrainMoss moss))
        {
            moss.Rebuild();
        }

        GroundLayerPainter.RefreshDisplacement(terrain);
        GrassSetup.BakeGroundColour(terrain);
        string navmesh = WorldBuilder.RebakeNavMesh();
        EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
        return $"Placed {plan.Pois.Count} places, {plan.Zones.Count} spawn zones, {plan.Nodes.Count} resource points in {placed} ms; moss, displacement, " +
               $"ground colour and navmesh ({navmesh}) in {clock.ElapsedMilliseconds - placed} ms.";
    }
}
