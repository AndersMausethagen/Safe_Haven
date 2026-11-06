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
    private const float WardKeepAway = 120f;

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
                      $"{plan.Glades.Count} glades, {plan.Zones.Count} spawn zones, {plan.Nodes.Count} resource points); ");

        SetBuildAutomatically(world, false);
        WorldLook.EnsureAll();
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
        // A fresh start every run: kept cells (and paint the World Builder would take for hand painting, so features never paint over it)
        // belong to the ground that is about to be replaced. Changing every resolution is what clears them.
        if (world.Stash != null)
        {
            world.Stash.Trees.Clear();
            world.Stash.Prepare(1, 1, data.alphamapLayers, 1);
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
        int moss = Index("Cedar Moss");
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
                // The Dark Forest leaves steep ground to its tan litter, which from above reads as a path: moss the woods' banks instead.
                if (moss >= 0 && slope > 22f)
                {
                    Blend(b, moss, Mathf.SmoothStep(0f, 0.85f, Mathf.InverseLerp(22f, 34f, slope)));
                }
                // Glades open onto the Haven's lighter meadow ground: from above they read as bright rooms in the dark woods.
                float share = plan.WoodsWeight(point) * (1f - 0.8f * plan.GladeOpen(point));
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
        var verge = new byte[resolution * resolution];
        float[] thin = Corridors(plan, resolution, new Vector2(origin.x, origin.z), path, verge);
        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                var point = new Vector2(origin.x + (x + 0.5f) / resolution * recipe.size, origin.z + (y + 0.5f) / resolution * recipe.size);
                // The raids' cedars only where the woods begin; the Haven's ring has its maple groves.
                float woods = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.6f, plan.WoodsWeight(point)));
                int cell = y * resolution + x;
                // The woods thin out beside the paths, so a walk along one sees into the trees and has room to fight.
                float density = Mathf.Lerp(recipe.woodsDensity.x, recipe.woodsDensity.y, Mathf.InverseLerp(recipe.haven, plan.Half, point.magnitude)) *
                                woods * (1f - plan.MountainWeight(point) * 0.55f) * (1f - 0.92f * thin[cell]);
                bool open = point.magnitude < recipe.buildable + 20f || plan.Lake.Edge(point) < 4f || plan.Pois.Exists(poi => Vector2.Distance(poi.At, point) < poi.Rule.clearing);
                forest[cell] = (byte)Mathf.RoundToInt(Mathf.Clamp01(density) * 255f);
                clearing[cell] = open ? (byte)255 : (byte)Mathf.Max(verge[cell], Mathf.RoundToInt(plan.GladeOpen(point) * 255f));
                // Seen from above a crown spreads 4-6 m: a place keeps that much more free of trees (undergrowth may stay).
                if (!open && plan.Pois.Exists(poi => Vector2.Distance(poi.At, point) < poi.Rule.clearing + recipe.crownMargin))
                {
                    clearing[cell] = (byte)Mathf.Max(clearing[cell], 115);
                }

                // A ravine's lip is seen before it is fallen into: bare at the lip, then no trees for rimClearing metres.
                float lip = RavineLipDistance(plan, point);
                if (lip < 2f)
                {
                    clearing[cell] = (byte)Mathf.Max(clearing[cell], 200);
                }
                else if (lip < recipe.rimClearing)
                {
                    clearing[cell] = (byte)Mathf.Max(clearing[cell], 115);
                }
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

    // How much the woods thin beside each path (1 at the tread's edge, 0 past its clearing), the tread itself marked as path (no trees,
    // no undergrowth) and a verge beside it marked as open (no trees, but undergrowth): a cedar's crown spreads 4-6 m, so trunks any
    // closer would close over the path seen from above. Stamped segment by segment, so only the cells near a path are visited.
    private static float[] Corridors(HavenWorldPlan plan, int resolution, Vector2 origin, byte[] tread, byte[] verge)
    {
        HavenWorldRecipe recipe = plan.Recipe;
        float cellSize = recipe.size / resolution;
        var thin = new float[resolution * resolution];
        for (int p = 0; p < plan.Paths.Count; p++)
        {
            List<Vector2> line = plan.Paths[p];
            HavenWorldPlan.PathKind kind = plan.PathKinds[p];
            float half = PathWidth(recipe, kind) * 0.5f;
            bool trail = kind == HavenWorldPlan.PathKind.Trail || kind == HavenWorldPlan.PathKind.Spur;
            float clearingWidth = trail ? recipe.pathClearing.y : recipe.pathClearing.x;
            float reach = half + clearingWidth + 1f;
            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                int x0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach - origin.x) / cellSize));
                int x1 = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + reach - origin.x) / cellSize));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - reach - origin.y) / cellSize));
                int y1 = Mathf.Min(resolution - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + reach - origin.y) / cellSize));
                Vector2 along = b - a;
                float squared = Mathf.Max(0.0001f, along.sqrMagnitude);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        var point = new Vector2(origin.x + (x + 0.5f) * cellSize, origin.y + (y + 0.5f) * cellSize);
                        float t = Mathf.Clamp01(Vector2.Dot(point - a, along) / squared);
                        float edge = Vector2.Distance(a + along * t, point) - half;
                        if (edge > clearingWidth)
                        {
                            continue;
                        }

                        int cell = y * resolution + x;
                        // Ragged, so the corridor's sides are not two ruled lines of trunks.
                        float ragged = clearingWidth * (0.75f + 0.5f * Mathf.PerlinNoise(point.x / 9f + 3.1f, point.y / 9f - 7.7f));
                        thin[cell] = Mathf.Max(thin[cell], 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(edge / ragged)));
                        if (edge < 1.2f)
                        {
                            tread[cell] = 255;
                        }

                        // Clearing strength 0.45: past the scatter's "no trees" (0.3), short of its "no undergrowth" (0.6).
                        if (edge < (trail ? 3f : 4f) * (0.8f + 0.4f * Mathf.PerlinNoise(point.x / 6f - 2.3f, point.y / 6f + 5.9f)))
                        {
                            verge[cell] = (byte)Mathf.Max(verge[cell], 115);
                        }
                    }
                }
            }
        }

        return thin;
    }

    // Metres outside a ravine's lip (negative inside it): its floor's half width and the wall run, raggedness and all.
    private const float RavineWall = 2.5f * 1.6f;

    private static float RavineLipDistance(HavenWorldPlan plan, Vector2 point)
    {
        float best = float.MaxValue;
        foreach (HavenWorldPlan.Ravine ravine in plan.Ravines)
        {
            float lip = ravine.FloorWidth * 0.5f + RavineWall;
            if (!HavenWorldPlan.NearLine(ravine.Line, point, lip + 20f))
            {
                continue;
            }

            Vector2 on = HavenWorldPlan.NearestOn(new List<List<Vector2>> { ravine.Line }, point);
            best = Mathf.Min(best, Vector2.Distance(on, point) - lip);
        }

        return best;
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
            // The water follows the plan's downhill levels (the valley Height carved for it), not a running minimum of the shaped
            // ground, which a path's cutting or a bank could drag down metres for the rest of the river.
            Spline spline = river.Spline;
            for (int k = 0; k < spline.Count; k++)
            {
                BezierKnot knot = spline[k];
                knot.Position = new Unity.Mathematics.float3(knot.Position.x, plan.RiverLevel(i, k) - 0.35f, knot.Position.z);
                spline.SetKnot(k, knot);
            }

            SetEnum(river, "surface", (int)RiverFeature.SurfaceMode.SplineHeight);
            // Fords where paths cross: broad and shallow, so a path walks through the water rather than down a bank.
            var knots = new SerializedObject(river);
            SerializedProperty depths = knots.FindProperty("depthAtKnots.values"), widths = knots.FindProperty("widthAtKnots.values");
            depths.arraySize = widths.arraySize = plan.Rivers[i].Count;
            for (int k = 0; k < plan.Rivers[i].Count; k++)
            {
                bool ford = plan.Fords.Contains((i, k));
                depths.GetArrayElementAtIndex(k).floatValue = ford ? 0.18f : 1f;
                widths.GetArrayElementAtIndex(k).floatValue = ford ? 1.5f : 1f;
            }

            knots.ApplyModifiedPropertiesWithoutUndo();
        }

        ScatterPalette verge = woods.Palette("Path Edge");
        for (int i = 0; i < plan.Paths.Count; i++)
        {
            HavenWorldPlan.PathKind kind = plan.PathKinds[i];
            PathFeature path = Make(parent, $"{kind} {i + 1}", templates.Path, feature => feature.Configure(haven.PathGround, null));
            Line(path, plan, plan.Paths[i], false, seed + 100 + i);
            // Worn, not ruled: the tread swells and pinches, the ground shows through, the edges lip and fray, stones and twigs lie by it.
            bool trail = kind == HavenWorldPlan.PathKind.Trail || kind == HavenWorldPlan.PathKind.Loop;
            // Graded over 20-30 m: up and over the woods' ridges in a cutting, not straight up their 40-degree sides.
            path.ConfigureLook(PathWidth(plan.Recipe, kind), trail ? 0.7f : 0.5f, trail ? 0.6f : 0.4f, trail ? 0.06f : 0.09f, trail ? 0.9f : 0.7f,
                               verge != null ? (trail ? 0.8f : 1.2f) : 0f, trail ? 20f : 30f);
            Set(path, "sink", trail ? 0.08f : 0.12f);
            if (verge != null)
            {
                var data = new SerializedObject(path);
                data.FindProperty("edgeScatter").objectReferenceValue = verge;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
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

    /// <summary>The tread's width of a kind of path, from the recipe.</summary>
    public static float PathWidth(HavenWorldRecipe recipe, HavenWorldPlan.PathKind kind) => kind switch
    {
        HavenWorldPlan.PathKind.Road => recipe.roadWidth,
        HavenWorldPlan.PathKind.Ring => recipe.ringWidth,
        HavenWorldPlan.PathKind.Spur => recipe.spurWidth,
        HavenWorldPlan.PathKind.Loop => recipe.loopWidth,
        _ => recipe.trailWidth,
    };

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
        foreach (string name in new[] { "Points of Interest", "Spawn Zones", "Resources", "Mountain Rocks", "Glades", "Along the Way", "Wildlife" })
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

        if (recipe.landing != null)
        {
            foreach ((Vector2 at, float bearing) in plan.Landings)
            {
                var landing = (GameObject)PrefabUtility.InstantiatePrefab(recipe.landing, places);
                landing.name = "Boat Landing";
                Vector3 shore = Ground(at);
                // The deck at the bank's height, running out over the water.
                landing.transform.SetPositionAndRotation(new Vector3(shore.x, Mathf.Max(shore.y, plan.Lake.Level) - 0.3f, shore.z), Quaternion.Euler(0f, bearing, 0f));
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
            // Home stays safe: refilled only at night, and never within reach of the ward as it grows.
            spawner.ConfigureHome(true, WardKeepAway);
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

        var gladeRoot = new GameObject("Glades").transform;
        gladeRoot.SetParent(content, false);
        int marked = 0;
        foreach (HavenWorldPlan.Glade glade in plan.Glades)
        {
            GameObject mark = glade.Landmark && glade.LandmarkKind < recipe.gladeLandmarks.Count ? recipe.gladeLandmarks[glade.LandmarkKind] : null;
            if (mark == null)
            {
                continue;
            }

            var made = (GameObject)PrefabUtility.InstantiatePrefab(mark, gladeRoot);
            made.name = $"{glade.Kind} Glade Landmark";
            Vector2 at = glade.Centre + HavenWorldPlan.Dir(glade.Radius * 37f) * glade.Radius * 0.15f;
            made.transform.SetPositionAndRotation(Ground(at), Quaternion.Euler(0f, glade.Radius * 53f % 360f, 0f));
            foreach (Transform part in made.transform)
            {
                Vector3 position = part.position;
                position.y = Ground(new Vector2(position.x, position.z)).y + part.localPosition.y;
                part.position = position;
            }

            marked++;
        }

        var strewn = new GameObject("Mountain Rocks").transform;
        strewn.SetParent(content, false);
        var facings = new Dictionary<GameObject, Vector2>();
        var footprints = new List<Bounds>();
        foreach (HavenWorldPlan.Rock rock in plan.Rocks)
        {
            GameObject prefab = recipe.mountainRocks[rock.Kind];
            if (prefab == null)
            {
                continue;
            }

            if (!facings.TryGetValue(prefab, out Vector2 facing))
            {
                facings[prefab] = facing = Facing(prefab);
            }

            var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, strewn);
            Vector3 at = Ground(rock.At);
            Vector3 normal = terrain.terrainData.GetInterpolatedNormal((rock.At.x - terrain.transform.position.x) / recipe.size,
                                                                       (rock.At.y - terrain.transform.position.z) / recipe.size);
            var downhill = new Vector2(normal.x, normal.z);
            // A face turns its open side down the slope (its back into the hill); a lump just turns at random.
            float yaw = downhill.sqrMagnitude > 0.01f && facing.sqrMagnitude > 0.09f
                ? HavenWorldPlan.BearingOf(downhill) - HavenWorldPlan.BearingOf(facing) + rock.Turn
                : rock.Turn * 5f;
            Quaternion lean = Quaternion.Slerp(Quaternion.identity, Quaternion.FromToRotation(Vector3.up, normal), 0.5f);
            float height = Height(prefab) * rock.Scale;
            made.transform.SetPositionAndRotation(at - normal * (height * 0.2f), lean * Quaternion.Euler(0f, yaw, 0f));
            made.transform.localScale = Vector3.one * rock.Scale;
            Renderer drawn = made.GetComponentInChildren<Renderer>();
            if (drawn != null)
            {
                footprints.Add(drawn.bounds);
            }
        }

        int along = PlaceAlongTheWay(plan, terrain, content, Ground, footprints);
        int herds = PlaceWildlife(plan, content, Ground);
        int cleared = ClearTreesUnder(terrain, footprints);

        // Felled trees in the woods are kept in the save (TerrainTreeMemory), on the terrain's own saveable entity.
        if (!terrain.TryGetComponent(out SaveableEntity entity))
        {
            entity = terrain.gameObject.AddComponent<SaveableEntity>();
            entity.AssignNewId();
        }

        if (!terrain.TryGetComponent(out TerrainTreeMemory _))
        {
            terrain.gameObject.AddComponent<TerrainTreeMemory>();
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
        return $"Placed {plan.Pois.Count} places, {plan.Zones.Count} spawn zones, {plan.Nodes.Count} resource points, {marked} glade landmarks, {along} scenes, markers, dressing and rim stones, {herds} wildlife herds, {plan.Rocks.Count} mountain rocks ({cleared} trees cleared from under them) in {placed} ms; moss, displacement, " +
               $"ground colour and navmesh ({navmesh}) in {clock.ElapsedMilliseconds - placed} ms.";
    }

    // What makes the way between places worth walking and readable from above: scenes beside the paths, a cairn where a trail
    // turns off, each glade dressed for its kind, and stones along every ravine's lip.
    private static int PlaceAlongTheWay(HavenWorldPlan plan, Terrain terrain, Transform content, System.Func<Vector2, Vector3> ground, List<Bounds> footprints)
    {
        HavenWorldRecipe recipe = plan.Recipe;
        var root = new GameObject("Along the Way").transform;
        root.SetParent(content, false);
        int placed = 0;

        void Put(GameObject prefab, Vector2 at, float turn, float scale, Transform parent, float sink = 0f)
        {
            if (prefab == null)
            {
                return;
            }

            var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            made.transform.SetPositionAndRotation(ground(at) - Vector3.up * sink, Quaternion.Euler(0f, turn, 0f));
            made.transform.localScale = Vector3.one * scale;
            // Each part of a scene on the ground where it stands.
            foreach (Transform part in made.transform)
            {
                Vector3 position = part.position;
                position.y = ground(new Vector2(position.x, position.z)).y + part.localPosition.y * scale - sink;
                part.position = position;
            }

            foreach (Renderer drawn in made.GetComponentsInChildren<Renderer>())
            {
                footprints.Add(drawn.bounds);
            }

            placed++;
        }

        var scenes = new GameObject("Trail Scenes").transform;
        scenes.SetParent(root, false);
        foreach (HavenWorldPlan.Placed scene in plan.TrailScenes)
        {
            Put(recipe.trailScenes[scene.Kind], scene.At, scene.Turn, 1f, scenes);
        }

        var markers = new GameObject("Junction Markers").transform;
        markers.SetParent(root, false);
        foreach (HavenWorldPlan.Placed marker in plan.Junctions)
        {
            Put(recipe.junctionMarker, marker.At, marker.Turn, 1f, markers);
        }

        var dressing = new GameObject("Glade Dressing").transform;
        dressing.SetParent(root, false);
        foreach ((HavenWorldPlan.Glade glade, HavenWorldPlan.Placed prop) in plan.GladeProps)
        {
            List<GameObject> kit = plan.DressingFor(glade.Kind);
            Put(kit[prop.Kind % kit.Count], prop.At, prop.Turn, prop.Scale, dressing);
        }

        var rims = new GameObject("Ravine Rims").transform;
        rims.SetParent(root, false);
        if (recipe.rimStones.Count > 0)
        {
            var random = new System.Random(plan.Seed + 977);
            foreach (HavenWorldPlan.Ravine ravine in plan.Ravines)
            {
                for (int i = 0; i + 1 < ravine.Line.Count; i++)
                {
                    Vector2 a = ravine.Line[i], b = ravine.Line[i + 1];
                    Vector2 along = (b - a).normalized;
                    int steps = Mathf.Max(1, Mathf.RoundToInt(Vector2.Distance(a, b) / recipe.rimStoneSpacing));
                    for (int k = 0; k < steps; k++)
                    {
                        Vector2 centre = Vector2.Lerp(a, b, (k + 0.5f) / steps);
                        foreach (float side in new[] { -1f, 1f })
                        {
                            Vector2 outward = Vector2.Perpendicular(along) * side;
                            if (!FindLip(ground, centre, outward, ravine.FloorWidth * 0.5f, out Vector2 lip))
                            {
                                continue;
                            }

                            // Gaps now and then, so it reads as stones that came to rest there, not a wall.
                            if (random.NextDouble() < 0.25)
                            {
                                continue;
                            }

                            GameObject stone = recipe.rimStones[random.Next(recipe.rimStones.Count)];
                            Put(stone, lip + outward * (0.6f + (float)random.NextDouble() * 0.8f), (float)random.NextDouble() * 360f,
                                0.8f + (float)random.NextDouble() * 0.5f, rims, 0.15f);
                        }
                    }
                }
            }
        }

        return placed;
    }

    // Deer and stags graze the open glades and the Haven's groves at dawn and dusk; foxes keep to the rocky glades and lairs
    // from dusk. Each herd only makes its animals while the player is near (WildlifeHerd).
    private static int PlaceWildlife(HavenWorldPlan plan, Transform content, System.Func<Vector2, Vector3> ground)
    {
        HavenWorldRecipe recipe = plan.Recipe;
        var root = new GameObject("Wildlife").transform;
        root.SetParent(content, false);
        var random = new System.Random(plan.Seed + 433);
        var dawnAndDusk = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(4.5f, 0.6f), new Keyframe(6.5f, 1f), new Keyframe(9.5f, 0.5f),
                                             new Keyframe(13f, 0.3f), new Keyframe(17f, 0.6f), new Keyframe(19.5f, 1f), new Keyframe(22f, 0.4f), new Keyframe(24f, 0.15f));
        var night = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(5f, 0.8f), new Keyframe(7f, 0.1f), new Keyframe(17f, 0.1f),
                                       new Keyframe(19f, 0.9f), new Keyframe(24f, 1f));
        int made = 0;

        void Herd(string name, List<Wildlife> kinds, Vector2 at, float radius, Vector2Int count, AnimationCurve hours)
        {
            if (kinds == null || kinds.Count == 0)
            {
                return;
            }

            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.position = ground(at);
            var herd = go.AddComponent<WildlifeHerd>();
            herd.Configure(kinds, count, radius, hours);
            if (kinds == recipe.grazers)
            {
                herd.SetHollowed(recipe.hollowedGrazer);
            }

            made++;
        }

        foreach (HavenWorldPlan.Glade glade in plan.Glades)
        {
            if (glade.Kind == HavenWorldPlan.GladeKind.Meadow || glade.Kind == HavenWorldPlan.GladeKind.Wet)
            {
                Herd($"Grazers ({glade.Kind} Glade)", recipe.grazers, glade.Centre, glade.Radius * 0.6f, new Vector2Int(2, 4), dawnAndDusk);
            }
            else if (random.NextDouble() < 0.5)
            {
                Herd($"Fox ({glade.Kind} Glade)", recipe.foxes, glade.Centre, glade.Radius * 0.6f, new Vector2Int(1, 1), night);
            }
        }

        foreach (List<Vector2> grove in plan.Groves)
        {
            Vector2 centre = Vector2.zero;
            foreach (Vector2 point in grove)
            {
                centre += point;
            }

            Herd("Grazers (Grove)", recipe.grazers, centre / Mathf.Max(1, grove.Count), 14f, new Vector2Int(1, 3), dawnAndDusk);
        }

        return made;
    }

    // Walks out from a ravine's floor to where the ground stops falling away: its lip.
    private static bool FindLip(System.Func<Vector2, Vector3> ground, Vector2 centre, Vector2 outward, float halfFloor, out Vector2 lip)
    {
        float top = ground(centre + outward * (halfFloor + 18f)).y;
        for (float d = halfFloor; d < halfFloor + 14f; d += 0.5f)
        {
            Vector2 at = centre + outward * d;
            if (ground(at).y >= top - 0.5f)
            {
                lip = at;
                return true;
            }
        }

        lip = default;
        return false;
    }

    // The woods are painted before the rocks are placed: no terrain tree grows up through one.
    private static int ClearTreesUnder(Terrain terrain, List<Bounds> footprints)
    {
        if (footprints.Count == 0)
        {
            return 0;
        }

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        TreeInstance[] trees = data.treeInstances;
        var kept = new List<TreeInstance>(trees.Length);
        foreach (TreeInstance tree in trees)
        {
            var at = new Vector3(origin.x + tree.position.x * size.x, 0f, origin.z + tree.position.z * size.z);
            bool under = false;
            foreach (Bounds footprint in footprints)
            {
                if (at.x > footprint.min.x - 0.5f && at.x < footprint.max.x + 0.5f && at.z > footprint.min.z - 0.5f && at.z < footprint.max.z + 0.5f)
                {
                    under = true;
                    break;
                }
            }

            if (!under)
            {
                kept.Add(tree);
            }
        }

        if (kept.Count != trees.Length)
        {
            data.SetTreeInstances(kept.ToArray(), true);
        }

        return trees.Length - kept.Count;
    }

    // Which way a model's surface mostly faces on the ground (its area-weighted normal, x and z): long for a rock face, short for a lump.
    private static Vector2 Facing(GameObject prefab)
    {
        MeshFilter filter = prefab.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.sharedMesh != null);
        if (filter == null || !filter.sharedMesh.isReadable)
        {
            return Vector2.zero;
        }

        Mesh mesh = filter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        Vector3 sum = Vector3.zero;
        float area = 0f;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 cross = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
            sum += cross;
            area += cross.magnitude;
        }

        Vector3 local = filter.transform.rotation * (sum / Mathf.Max(area, 0.0001f));
        return new Vector2(local.x, local.z);
    }

    private static float Height(GameObject prefab)
    {
        MeshFilter filter = prefab.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.sharedMesh != null);
        return filter != null ? filter.sharedMesh.bounds.size.y : 1f;
    }
}
