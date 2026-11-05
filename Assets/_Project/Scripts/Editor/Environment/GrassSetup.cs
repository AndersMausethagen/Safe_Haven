using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Sets up wind- and player-reactive grass for painting on a terrain: generates the grass and bush meshes
/// (vertex colour R = bend weight, G = per-blade random), their "Safe Haven/Grass" materials and prefabs in
/// Art/Generated/Grass, adds them to the selected terrain as GPU-instanced detail prototypes (paint them with the
/// terrain's Paint Details brush) and gives the player prefab a GrassInteractor. Running it again keeps your material
/// and prototype tuning; it only adds what is missing.</summary>
public static class GrassSetup
{
    public const string Folder = "Assets/_Project/Art/Generated/Grass";
    public const string PlayerPrefab = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
    public const string ShaderName = "Safe Haven/Grass";

    public enum Shape
    {
        Grass,
        Bush,
        Mushrooms,
        Flowers,
        Fern,
        BerryShrub,
        Heather,
        WildFlowers
    }

    /// <summary>name, shape, blade/card/plant count, radius, height (metres).</summary>
    private static readonly (string Name, Shape Shape, int Count, float Radius, float Height)[] Kinds =
    {
        ("Short Grass", Shape.Grass, 36, 0.22f, 0.28f),
        ("Tall Grass", Shape.Grass, 24, 0.26f, 0.6f),
        ("Bush", Shape.Bush, 70, 0.4f, 0.55f),
        ("Glow Mushrooms", Shape.Mushrooms, 5, 0.2f, 0.2f),
        ("Glow Flowers", Shape.Flowers, 5, 0.22f, 0.38f),
        // Nordic forest floor (fill by ground, or paint them).
        ("Fern", Shape.Fern, 8, 0.05f, 0.55f),
        ("Blueberry", Shape.BerryShrub, 44, 0.3f, 0.3f),
        ("Lingonberry", Shape.BerryShrub, 38, 0.26f, 0.22f),
        ("Heather", Shape.Heather, 44, 0.18f, 0.28f),
        ("White Flowers", Shape.WildFlowers, 7, 0.24f, 0.27f),
        ("Yellow Flowers", Shape.WildFlowers, 6, 0.24f, 0.36f),
    };

    public static int KindCount => Kinds.Length;

    /// <summary>Night glow of the glowing plants (HDR, light blue); vertex colour B marks the parts that glow.</summary>
    public static readonly Color GlowColour = new Color(0.15f, 0.55f, 1f) * 1.4f;

    public static Shape ShapeOf(string prefabName)
    {
        foreach (var kind in Kinds)
        {
            if (kind.Name == prefabName)
            {
                return kind.Shape;
            }
        }

        return Shape.Grass;
    }

    [MenuItem("Safe Haven/World/Grass and Plants/Add Grass and Bushes to Selected Terrain")]
    public static void AddToSelection()
    {
        Terrain[] terrains = Selection.GetFiltered<Terrain>(SelectionMode.Editable | SelectionMode.TopLevel);
        if (terrains.Length == 0 && Terrain.activeTerrain != null)
        {
            terrains = new[] { Terrain.activeTerrain };
        }

        List<GameObject> prefabs = EnsureAssets();
        int added = 0;
        foreach (Terrain terrain in terrains)
        {
            added += AddTo(terrain, prefabs);
            BakeGroundColour(terrain);
            SyncDrawDistance(terrain, prefabs);
        }

        bool player = EnsurePlayerInteractor();
        Debug.Log($"Grass: added {added} detail prototype(s) to {terrains.Length} terrain(s)" +
                  (player ? "; the player now pushes grass aside" : "") +
                  ". Select the terrain › Paint Details, pick a grass, bush, fern, berry shrub, heather, flower or glowing plant and paint. " +
                  $"Tune size, colour, wind and push on the materials in {Folder}.");
    }

    [MenuItem("Safe Haven/World/Grass and Plants/Add Grass and Bushes to Selected Terrain", true)]
    private static bool CanAdd() => Selection.GetFiltered<Terrain>(SelectionMode.Editable).Length > 0 ||
                                     Terrain.activeTerrain != null;

    [MenuItem("Safe Haven/World/Grass and Plants/Rebake Ground Colours of Selected Terrain")]
    private static void RebakeSelection()
    {
        foreach (Terrain terrain in Selection.GetFiltered<Terrain>(SelectionMode.Editable))
        {
            BakeGroundColour(terrain);
        }
    }

    [MenuItem("Safe Haven/World/Grass and Plants/Fill Selected Terrain With Grass...")]
    private static void FillMenu() => ScriptableWizard.DisplayWizard<FillWizard>("Fill Terrain With Grass", "Fill");

    [MenuItem("Safe Haven/World/Grass and Plants/Clear Grass Under Water")]
    private static void ClearUnderWaterMenu()
    {
        int cleared = 0;
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            cleared += ClearUnderWater(terrain);
        }

        Debug.Log($"Grass: cleared {cleared} detail cell(s) under water.");
    }

    private sealed class FillWizard : ScriptableWizard
    {
        [Tooltip("How much of each kind grows on each ground layer (a guess from the layer's colour to start with). " +
                 "Replaces the grass, bushes and Nordic plants (ferns, berry shrubs, heather, wild flowers); glowing plants and other details are kept.")]
        public GroundFill[] layers;
        public int seed = 1;
        private Terrain terrain;

        private void OnEnable()
        {
            terrain = Selection.activeGameObject != null ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            terrain = terrain != null ? terrain : Terrain.activeTerrain;
            layers = terrain != null ? DefaultFill(terrain.terrainData) : new GroundFill[0];
        }

        private void OnWizardUpdate()
        {
            isValid = terrain != null;
            errorString = isValid ? "" : "Select a terrain first.";
        }

        private void OnWizardCreate()
        {
            int grown = FillByGround(terrain, EnsureAssets(), layers, seed);
            EnsurePlayerInteractor();
            Debug.Log($"Grass: grew grass in {grown} cell(s) of {terrain.name}. Touch up with Paint Details.");
        }
    }

    [MenuItem("Safe Haven/World/Grass and Plants/Make Selected Push Grass")]
    private static void AddInteractorToSelection()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go.GetComponent<GrassInteractor>() == null)
            {
                Undo.AddComponent<GrassInteractor>(go);
            }
        }
    }

    /// <summary>Meshes, materials and prefabs (existing ones are kept, so your tuning survives).</summary>
    public static List<GameObject> EnsureAssets()
    {
        Directory.CreateDirectory(Folder);
        Shader shader = Shader.Find(ShaderName);
        if (shader == null)
        {
            throw new FileNotFoundException($"Shader '{ShaderName}' not found.");
        }

        Texture2D leaves = EnsureLeafTexture();
        Texture2D palette = EnsureGlowPalette();
        var prefabs = new List<GameObject>();
        foreach ((string name, Shape shape, int count, float radius, float height) in Kinds)
        {
            bool bush = shape == Shape.Bush;
            bool glows = shape == Shape.Mushrooms || shape == Shape.Flowers;
            string meshPath = $"{Folder}/{name}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                mesh = shape switch
                {
                    Shape.Bush => BuildBush(count, radius, height, 11),
                    Shape.Mushrooms => BuildMushrooms(count, radius, height, 21),
                    Shape.Flowers => BuildFlowers(count, radius, height, 31),
                    Shape.Fern => BuildFern(count, height, 41),
                    Shape.BerryShrub => BuildBush(count, radius, height, name.Length * 7),
                    Shape.Heather => BuildClump(count, radius, height, 51),
                    Shape.WildFlowers => BuildFlowers(count, radius, height, name.Length * 3),
                    _ => BuildClump(count, radius, height, name.Length)
                };
                mesh.name = name;
                AssetDatabase.CreateAsset(mesh, meshPath);
            }

            string materialPath = $"{Folder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = name, enableInstancing = true };
                Configure(material, name, shape, leaves, palette);
                AssetDatabase.CreateAsset(material, materialPath);
            }

            string prefabPath = $"{Folder}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var go = new GameObject(name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                // Grass shadows cost a lot for little; bushes are big enough to show theirs.
                renderer.shadowCastingMode = bush ? ShadowCastingMode.On : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                Object.DestroyImmediate(go);
            }

            prefabs.Add(prefab);
        }

        AssetDatabase.SaveAssets();
        return prefabs;
    }

    /// <summary>Adds the prefabs the terrain does not have yet as instanced detail meshes; returns how many.</summary>
    public static int AddTo(Terrain terrain, IReadOnlyList<GameObject> prefabs)
    {
        TerrainData data = terrain.terrainData;
        var prototypes = new List<DetailPrototype>(data.detailPrototypes);
        int added = 0;
        foreach (GameObject prefab in prefabs)
        {
            if (prototypes.Exists(p => p.prototype == prefab))
            {
                continue;
            }

            Shape shape = ShapeOf(prefab.name);
            bool bush = shape == Shape.Bush;
            bool glows = shape == Shape.Mushrooms || shape == Shape.Flowers;
            var prototype = new DetailPrototype
            {
                usePrototypeMesh = true,
                prototype = prefab,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = bush ? 0.7f : 0.8f,
                maxWidth = bush ? 1.4f : 1.3f,
                minHeight = bush ? 0.7f : 0.75f,
                maxHeight = bush ? 1.4f : 1.3f,
                noiseSpread = 0.35f,
                density = bush ? 0.25f : glows ? 0.6f : 1f,
                // Dense lawn: many blades per instance (cheap vertices) and more coverage near the camera.
                targetCoverage = prefab.name == "Short Grass" ? 2.5f : prefab.name == "Tall Grass" ? 1.5f : 1f,
                alignToGround = bush ? 0.2f : 0.6f,
                positionJitter = 0.8f,
                useDensityScaling = true,
            };
            if (!prototype.Validate(out string error))
            {
                Debug.LogWarning($"Grass: {prefab.name} cannot be a detail prototype: {error}");
                continue;
            }

            prototypes.Add(prototype);
            added++;
        }

        if (added > 0)
        {
            Undo.RegisterCompleteObjectUndo(data, "Add Grass Detail Prototypes");
            if (data.detailResolution <= 0)
            {
                // A terrain never painted with details has none: about 4 cells per metre, in 32-cell patches.
                data.SetDetailResolution(Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.CeilToInt(data.size.x * 4f)), 128, 1024), 32);
            }

            UseInstanceCounts(data);

            data.detailPrototypes = prototypes.ToArray();
            EditorUtility.SetDirty(data);
            terrain.Flush();
        }

        return added;
    }

    /// <summary>Puts a GrassInteractor on the player prefab (once). False when the prefab is missing.</summary>
    public static bool EnsurePlayerInteractor(string prefabPath = PlayerPrefab)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            return false;
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            if (contents.GetComponent<GrassInteractor>() == null)
            {
                contents.AddComponent<GrassInteractor>();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return true;
    }

    /// <summary>The grass materials fade out at the terrain's Detail Distance instead of popping.</summary>
    public static void SyncDrawDistance(Terrain terrain, IEnumerable<GameObject> prefabs)
    {
        foreach (GameObject prefab in prefabs)
        {
            Material material = prefab.GetComponent<MeshRenderer>().sharedMaterial;
            if (material != null && material.HasProperty("_FarEnd") &&
                !Mathf.Approximately(material.GetFloat("_FarEnd"), terrain.detailObjectDistance))
            {
                material.SetFloat("_FarEnd", terrain.detailObjectDistance);
                material.SetFloat("_FarStart", Mathf.Min(material.GetFloat("_FarStart"), terrain.detailObjectDistance * 0.5f));
                EditorUtility.SetDirty(material);
            }
        }
    }

    // ---------- ground colour ----------
    /// <summary>Bakes the terrain's top-down colour (each painted layer's average colour, mixed by its paint weight)
    /// next to its TerrainData and hands it to the terrain's GrassTerrainColour (added when missing).</summary>
    public static Texture2D BakeGroundColour(Terrain terrain) => BakeGroundColour(terrain, null);

    // dry: a water mask already built for this terrain's splat map (testing every pixel against the water takes seconds).
    private static Texture2D BakeGroundColour(Terrain terrain, bool[] dry)
    {
        TerrainData data = terrain.terrainData;
        if (data.terrainLayers.Length == 0)
        {
            return null;
        }

        int width = data.alphamapWidth, height = data.alphamapHeight;
        if (dry == null || dry.Length != width * height)
        {
            dry = DryMask(terrain);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(GroundColourPixels(terrain, dry, AverageColours(data.terrainLayers)));
        texture.Apply();
        string path = ColourMapPath(data);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.wrapMode != TextureWrapMode.Clamp || importer.mipmapEnabled)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        var map = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var colour = terrain.GetComponent<GrassTerrainColour>();
        if (colour == null)
        {
            colour = Undo.AddComponent<GrassTerrainColour>(terrain.gameObject);
        }

        colour.ColourMap = map;
        EditorUtility.SetDirty(colour);
        return map;
    }

    private static Color[] AverageColours(TerrainLayer[] layers)
    {
        var averages = new Color[layers.Length];
        for (int i = 0; i < layers.Length; i++)
        {
            averages[i] = AverageColour(layers[i]);
        }

        return averages;
    }

    /// <summary>Per splat-map pixel: false where the ground is under water (the grass grows nothing there).</summary>
    private static bool[] DryMask(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        int width = data.alphamapWidth, height = data.alphamapHeight;
        WaterSurface[] water = Object.FindObjectsByType<WaterSurface>();
        Vector3 origin = terrain.transform.position;
        var dry = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector3 point = new Vector3(origin.x + (x + 0.5f) / width * data.size.x, 0f, origin.z + (y + 0.5f) / height * data.size.z);
                dry[y * width + x] = !UnderWater(terrain, point, water);
            }
        }

        return dry;
    }

    private static Color[] GroundColourPixels(Terrain terrain, bool[] dry, Color[] averages)
    {
        TerrainData data = terrain.terrainData;
        int width = data.alphamapWidth, height = data.alphamapHeight;
        int layers = Mathf.Min(averages.Length, data.alphamapLayers);
        float[,,] weights = data.GetAlphamaps(0, 0, width, height);
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color mixed = Color.black;
                for (int i = 0; i < layers; i++)
                {
                    mixed += averages[i] * weights[y, x, i];
                }

                // Alpha 0 under water: the grass shader grows nothing there, even if painted later.
                mixed.a = dry[y * width + x] ? 1f : 0f;
                pixels[y * width + x] = mixed;
            }
        }

        return pixels;
    }

    /// <summary>Most clumps a detail cell (about 25 cm) holds when fully painted: a dense lawn of short grass, single
    /// tall tufts, bushes and glowing plants.</summary>
    public static int MaxPerCell(string prefabName) => prefabName == "Short Grass" ? 2 : 1;

    /// <summary>Instance Count scatter mode: a cell's value is how many clumps grow there, so a lawn can be dense.
    /// (Coverage mode treats each clump as about a metre wide and plants very few.) Existing paint is converted.</summary>
    public static void UseInstanceCounts(TerrainData data)
    {
        if (data.detailScatterMode == DetailScatterMode.InstanceCountMode || data.detailResolution <= 0)
        {
            return;
        }

        int resolution = data.detailResolution;
        DetailPrototype[] prototypes = data.detailPrototypes;
        var layers = new int[prototypes.Length][,];
        for (int layer = 0; layer < prototypes.Length; layer++)
        {
            int[,] cells = data.GetDetailLayer(0, 0, resolution, resolution, layer);
            int most = MaxPerCell(prototypes[layer].prototype != null ? prototypes[layer].prototype.name : "");
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    cells[z, x] = cells[z, x] > 0 ? Mathf.Max(1, Mathf.RoundToInt(cells[z, x] / 255f * most)) : 0;
                }
            }

            layers[layer] = cells;
        }

        data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        for (int layer = 0; layer < layers.Length; layer++)
        {
            data.SetDetailLayer(0, 0, layer, layers[layer]);
        }
    }

    /// <summary>Whether a WaterSurface covers the ground at this point (x, z; y is taken from the terrain).</summary>
    public static bool UnderWater(Terrain terrain, Vector3 point, IReadOnlyList<WaterSurface> water)
    {
        if (water.Count == 0)
        {
            return false;
        }

        point.y = terrain.transform.position.y + terrain.SampleHeight(point);
        foreach (WaterSurface surface in water)
        {
            if (surface != null && surface.isActiveAndEnabled && surface.Contains(point, 0.3f, out _))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Removes every detail (grass, bushes, flowers...) from the terrain cells under water and rebakes the
    /// ground colour (whose alpha hides grass under water). Returns the number of cells cleared.</summary>
    public static int ClearUnderWater(Terrain terrain)
    {
        TerrainData data = terrain.terrainData;
        WaterSurface[] water = Object.FindObjectsByType<WaterSurface>();
        int resolution = data.detailResolution;
        int cleared = 0;
        if (resolution > 0 && water.Length > 0 && data.detailPrototypes.Length > 0)
        {
            Undo.RegisterCompleteObjectUndo(data, "Clear Grass Under Water");
            bool[,] wet = new bool[resolution, resolution];
            Vector3 origin = terrain.transform.position;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    wet[z, x] = UnderWater(terrain, new Vector3(origin.x + (x + 0.5f) / resolution * data.size.x, 0f,
                                                                origin.z + (z + 0.5f) / resolution * data.size.z), water);
                }
            }

            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                int[,] cells = data.GetDetailLayer(0, 0, resolution, resolution, layer);
                for (int z = 0; z < resolution; z++)
                {
                    for (int x = 0; x < resolution; x++)
                    {
                        if (wet[z, x] && cells[z, x] != 0)
                        {
                            cells[z, x] = 0;
                            cleared++;
                        }
                    }
                }

                data.SetDetailLayer(0, 0, layer, cells);
            }

            EditorUtility.SetDirty(data);
            terrain.Flush();
        }

        if (terrain.GetComponent<GrassTerrainColour>() != null)
        {
            BakeGroundColour(terrain);
        }

        return cleared;
    }

    /// <summary>How much of each kind grows on a terrain layer (0..1).</summary>
    [System.Serializable]
    public struct GroundFill
    {
        public TerrainLayer layer;
        [Range(0f, 1f)] public float shortGrass;
        [Range(0f, 1f)] public float tallGrass;
        [Range(0f, 1f)] public float bushes;
        [Tooltip("Ferns, berry shrubs, heather and wild flowers, in patches.")]
        [Range(0f, 1f)] public float plants;
    }

    /// <summary>A starting guess per layer from its colour: green layers (moss, grass) get dense grass with tall
    /// patches and bushes, earthy ones sparse grass, grey ones (stone, paths) none.</summary>
    public static GroundFill[] DefaultFill(TerrainData data)
    {
        var fills = new GroundFill[data.terrainLayers.Length];
        for (int i = 0; i < fills.Length; i++)
        {
            Color c = AverageColour(data.terrainLayers[i]);
            // Saturation keeps grey layers (stone, gravel) bare even when they are faintly green.
            float saturation = Mathf.Max(c.r, c.g, c.b) - Mathf.Min(c.r, c.g, c.b);
            float green = Mathf.Clamp01((c.g - (c.r + c.b) * 0.5f) * 8f) * Mathf.Clamp01(saturation * 8f);
            float colourful = Mathf.Clamp01((saturation - 0.05f) * 6f);
            fills[i] = new GroundFill
            {
                layer = data.terrainLayers[i],
                shortGrass = Mathf.Max(green, 0.2f * colourful),
                tallGrass = 0.5f * green,
                bushes = 0.4f * green,
                plants = Mathf.Max(0.8f * green, 0.35f * colourful)
            };
            fills[i].shortGrass = fills[i].shortGrass < 0.08f ? 0f : fills[i].shortGrass;
            fills[i].tallGrass = fills[i].tallGrass < 0.08f ? 0f : fills[i].tallGrass;
            fills[i].bushes = fills[i].bushes < 0.08f ? 0f : fills[i].bushes;
            fills[i].plants = fills[i].plants < 0.08f ? 0f : fills[i].plants;
        }

        return fills;
    }

    /// <summary>Replaces the Short Grass, Tall Grass, Bush and Nordic plant layers (ferns, berry shrubs, heather, wild
    /// flowers) with growth by ground layer: patchy (noise), none under water or under objects standing on the terrain
    /// (buildings, rocks). Other details (glowing plants) are kept. Undoable. Returns the number of cells with grass.</summary>
    public static int FillByGround(Terrain terrain, IReadOnlyList<GameObject> prefabs, GroundFill[] fills, int seed = 1)
    {
        AddTo(terrain, prefabs);
        TerrainData data = terrain.terrainData;
        int Layer(string name) => System.Array.FindIndex(data.detailPrototypes, p => p.prototype != null && p.prototype.name == name);
        int shortLayer = Layer("Short Grass"), tallLayer = Layer("Tall Grass"), bushLayer = Layer("Bush");
        if (shortLayer < 0 || tallLayer < 0 || bushLayer < 0)
        {
            return 0;
        }

        Undo.RegisterCompleteObjectUndo(data, "Fill Terrain With Grass");
        UseInstanceCounts(data);
        WaterSurface[] water = Object.FindObjectsByType<WaterSurface>();
        Physics.SyncTransforms();
        var growth = fills.Select(fill => new TerrainGrowth.Ground
        {
            layer = fill.layer,
            shortGrass = fill.shortGrass,
            tallGrass = fill.tallGrass,
            bushes = fill.bushes,
            plants = fill.plants,
        }).ToArray();
        int grown = TerrainGrowth.Fill(terrain, growth, seed, point => UnderWater(terrain, point, water) || Covered(terrain, point));
        EditorUtility.SetDirty(data);
        terrain.Flush();
        BakeGroundColour(terrain);
        return grown;
    }

    /// <summary>Something other than the terrain (a building, a rock, a fence) stands on the ground here.</summary>
    private static bool Covered(Terrain terrain, Vector3 point)
    {
        float ground = terrain.transform.position.y + terrain.SampleHeight(point);
        var ray = new Ray(new Vector3(point.x, ground + 30f, point.z), Vector3.down);
        return Physics.Raycast(ray, out RaycastHit hit, 30.2f, ~0, QueryTriggerInteraction.Ignore) &&
               !(hit.collider is TerrainCollider) && hit.point.y > ground - 0.1f;
    }

    public static string ColourMapPath(TerrainData data)
    {
        string dataPath = AssetDatabase.GetAssetPath(data);
        string folder = string.IsNullOrEmpty(dataPath) ? Folder : Path.GetDirectoryName(dataPath).Replace('\\', '/');
        Directory.CreateDirectory(folder);
        return $"{folder}/{data.name}_GrassColour.png";
    }

    /// <summary>Average colour of a terrain layer's texture (times its colour tint); readable or not.</summary>
    public static Color AverageColour(TerrainLayer layer)
    {
        Texture2D source = layer != null ? layer.diffuseTexture : null;
        if (source == null)
        {
            return new Color(0.3f, 0.3f, 0.3f, 1f);
        }

        RenderTexture target = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(source, target);
        RenderTexture.active = target;
        var read = new Texture2D(16, 16, TextureFormat.RGBA32, false);
        read.ReadPixels(new Rect(0, 0, 16, 16), 0, 0);
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Color sum = Color.black;
        foreach (Color pixel in read.GetPixels())
        {
            sum += pixel;
        }

        Object.DestroyImmediate(read);
        Color average = sum / 256f;
        Vector4 tint = layer.diffuseRemapMax;
        return new Color(average.r * tint.x, average.g * tint.y, average.b * tint.z, 1f);
    }

    // Repainting a terrain that has a ground colour map updates the grass once painting has paused. The full bake wrote
    // a PNG and reimported it (and tested every pixel against the water) after each stroke, which stalled the editor
    // while painting. Now a stroke only refreshes an in-memory preview, reusing that terrain's water mask and layer
    // colours; the PNG is written when the scene is saved, before scripts reload, and when the editor quits.
    private const double PreviewAfterSeconds = 0.5;

    private sealed class PaintPreview
    {
        public bool[] Dry;
        public Color[] Averages;
        public TerrainLayer[] Layers;
        public Texture2D Texture;
        public double DueAt = double.MaxValue;
        public bool NeedsBake;
    }

    private static readonly Dictionary<Terrain, PaintPreview> Previews = new Dictionary<Terrain, PaintPreview>();

    [InitializeOnLoadMethod]
    private static void WatchTerrainPaint()
    {
        TerrainCallbacks.textureChanged += (terrain, textureName, region, synced) =>
        {
            if (textureName != TerrainData.AlphamapTextureName || !synced || terrain == null ||
                terrain.GetComponent<GrassTerrainColour>() == null)
            {
                return;
            }

            if (!Previews.TryGetValue(terrain, out PaintPreview preview))
            {
                Previews[terrain] = preview = new PaintPreview();
            }

            preview.DueAt = EditorApplication.timeSinceStartup + PreviewAfterSeconds;
            preview.NeedsBake = true;
        };
        EditorApplication.update += UpdatePreviews;
        EditorSceneManager.sceneSaving += (scene, path) => BakePainted(scene);
        AssemblyReloadEvents.beforeAssemblyReload += () => BakePainted(null);
        EditorApplication.quitting += () => BakePainted(null);
    }

    private static void UpdatePreviews()
    {
        if (Previews.Count == 0)
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        foreach (KeyValuePair<Terrain, PaintPreview> entry in Previews)
        {
            if (entry.Value.DueAt <= now && entry.Key != null)
            {
                entry.Value.DueAt = double.MaxValue;
                RefreshPreview(entry.Key, entry.Value);
            }
        }
    }

    private static void RefreshPreview(Terrain terrain, PaintPreview preview)
    {
        TerrainData data = terrain.terrainData;
        var colour = terrain.GetComponent<GrassTerrainColour>();
        if (data == null || colour == null || data.terrainLayers.Length == 0)
        {
            return;
        }

        int width = data.alphamapWidth, height = data.alphamapHeight;
        if (preview.Dry == null || preview.Dry.Length != width * height)
        {
            preview.Dry = DryMask(terrain);
        }

        if (preview.Layers == null || !preview.Layers.SequenceEqual(data.terrainLayers))
        {
            preview.Layers = data.terrainLayers;
            preview.Averages = AverageColours(preview.Layers);
        }

        if (preview.Texture == null || preview.Texture.width != width || preview.Texture.height != height)
        {
            Object.DestroyImmediate(preview.Texture);
            preview.Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Grass Colour (painting)",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        preview.Texture.SetPixels(GroundColourPixels(terrain, preview.Dry, preview.Averages));
        preview.Texture.Apply(false);
        // Only the active ground colour feeds the grass; the serialized map keeps pointing at the PNG asset.
        if (GrassTerrainColour.Current == colour || GrassTerrainColour.Current == null)
        {
            Shader.SetGlobalTexture("_GrassTerrainColor", preview.Texture);
        }
    }

    /// <summary>Writes the colour maps of terrains painted since their last bake (in this scene, or all when null).
    /// Each terrain keeps its water mask for the rest of the session, so later strokes and saves skip the water test;
    /// moving water while painting is not picked up until scripts reload (or Bake Ground Colour is run).</summary>
    private static void BakePainted(Scene? scene)
    {
        foreach (KeyValuePair<Terrain, PaintPreview> entry in Previews.ToList())
        {
            Terrain terrain = entry.Key;
            if (terrain == null)
            {
                Object.DestroyImmediate(entry.Value.Texture);
                Previews.Remove(terrain);
                continue;
            }

            if ((scene.HasValue && terrain.gameObject.scene != scene.Value) || !entry.Value.NeedsBake)
            {
                continue;
            }

            entry.Value.NeedsBake = false;
            entry.Value.DueAt = double.MaxValue;
            if (terrain.terrainData != null)
            {
                BakeGroundColour(terrain, entry.Value.Dry);
            }
        }
    }

    // ---------- meshes ----------
    /// <summary>Half-width (metres) that vertex colour A = 1 stands for on camera-facing grass blades.</summary>
    public const float MaxHalfWidth = 0.05f;

    /// <summary>A clump of curved, tapering grass blades rooted in a disc of the given radius. Vertices lie on each
    /// blade's centre line with the blade's direction as normal, its half-width in vertex colour A (times
    /// <see cref="MaxHalfWidth"/>) and its side in UV0.x (0 left, 1 right): the shader (Facing Blades) turns each blade
    /// around its own axis to face the camera so it shows its full width (from a top-down camera, edge-on blades look
    /// like hair). Only channels terrain details always pass along are used.</summary>
    public static Mesh BuildClump(int blades, float radius, float height, int seed)
    {
        const int segments = 4;
        var random = new System.Random(seed);
        float Next(float low, float high) => low + (float)random.NextDouble() * (high - low);
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colours = new List<Color>();
        var triangles = new List<int>();
        float widest = 0f;
        for (int b = 0; b < blades; b++)
        {
            float angle = Next(0f, Mathf.PI * 2f);
            float distance = Mathf.Sqrt(Next(0f, 1f)) * radius;
            var root = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            float facing = Next(0f, Mathf.PI * 2f);
            var side = new Vector3(Mathf.Cos(facing), 0f, Mathf.Sin(facing));
            Vector3 front = Vector3.Cross(side, Vector3.up);
            // Splayed outwards so the clump opens up when seen from above.
            Vector3 lean = (root.sqrMagnitude > 1e-6f ? root.normalized : side) * Next(0.15f, 0.5f) + front * Next(-0.15f, 0.15f);
            float bladeHeight = height * Next(0.6f, 1.15f);
            float width = Next(0.03f, 0.055f) * (height / 0.3f + 1f) * 0.5f;
            widest = Mathf.Max(widest, width);
            float shade = Next(0f, 1f);
            int start = positions.Count;
            for (int s = 0; s <= segments; s++)
            {
                float t = s / (float)segments;
                Vector3 centre = root + Vector3.up * (bladeHeight * t) + lean * (bladeHeight * t * t);
                float half = width * 0.5f * (1f - t * 0.92f);
                Vector3 axis = (Vector3.up * bladeHeight + lean * (bladeHeight * 2f * t)).normalized; // along the blade
                foreach (float sign in new[] { -1f, 1f })
                {
                    positions.Add(centre);
                    normals.Add(axis);
                    uvs.Add(new Vector2(sign < 0 ? 0f : 1f, t));
                    colours.Add(new Color(t, shade, 0f, half / MaxHalfWidth));
                }
            }

            for (int s = 0; s < segments; s++)
            {
                int i = start + s * 2;
                triangles.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
            }
        }

        Mesh mesh = Finish(positions, normals, uvs, colours, triangles);
        Bounds bounds = mesh.bounds;
        bounds.Expand(new Vector3(widest, 0f, widest)); // the shader widens the blades
        mesh.bounds = bounds;
        return mesh;
    }

    /// <summary>A new material's look for its kind (existing materials keep your tuning).</summary>
    private static void Configure(Material material, string name, Shape shape, Texture2D leaves, Texture2D glowPalette)
    {
        material.SetFloat("_FacingBlades", shape == Shape.Grass || shape == Shape.Heather ? 1f : 0f);
        switch (shape)
        {
            case Shape.Bush:
                material.SetTexture("_BaseMap", leaves);
                material.SetFloat("_Cutoff", 0.5f);
                material.SetColor("_BottomColor", new Color(0.12f, 0.21f, 0.07f));
                material.SetColor("_TopColor", new Color(0.38f, 0.52f, 0.19f));
                material.SetFloat("_WindStrength", 0.05f);
                material.SetFloat("_PushStrength", 0.15f);
                material.SetFloat("_Flatten", 0.15f);
                material.SetFloat("_NormalUp", 0.55f);
                material.SetFloat("_GroundColor", 0.3f);
                break;
            case Shape.Mushrooms:
            case Shape.Flowers:
                // Colours come from the palette texture (by UV); the glow from vertex colour B at night.
                Coloured(material, glowPalette, 0.1f);
                material.SetFloat("_NormalUp", 0.25f);
                material.SetFloat("_WindStrength", shape == Shape.Flowers ? 0.08f : 0.01f);
                material.SetFloat("_PushStrength", shape == Shape.Flowers ? 0.25f : 0.05f);
                material.SetFloat("_Flatten", shape == Shape.Flowers ? 0.4f : 0.1f);
                material.SetColor("_GlowColor", GlowColour);
                break;
            case Shape.Fern:
                Coloured(material, EnsureTexture("Fern Frond.png", () => FernPixels(128, 256)), 0.4f);
                material.SetColor("_BottomColor", new Color(0.8f, 0.85f, 0.8f));
                material.SetColor("_DryColor", new Color(0.62f, 0.5f, 0.24f));
                material.SetFloat("_DryAmount", 0.15f);
                material.SetFloat("_GroundColor", 0.12f);
                material.SetFloat("_NormalUp", 0.5f);
                material.SetFloat("_WindStrength", 0.07f);
                material.SetFloat("_PushStrength", 0.25f);
                material.SetFloat("_Flatten", 0.35f);
                break;
            case Shape.BerryShrub:
                bool blue = name == "Blueberry";
                Coloured(material, EnsureTexture(name + " Leaves.png", () => BerryShrubPixels(256, blue)), 0.5f);
                material.SetColor("_BottomColor", new Color(0.75f, 0.8f, 0.75f));
                material.SetFloat("_GroundColor", 0.1f);
                material.SetFloat("_NormalUp", 0.5f);
                material.SetFloat("_Smoothness", blue ? 0.15f : 0.35f); // lingonberry leaves are glossy
                material.SetFloat("_WindStrength", 0.03f);
                material.SetFloat("_PushStrength", 0.12f);
                material.SetFloat("_Flatten", 0.2f);
                break;
            case Shape.Heather:
                material.SetColor("_BottomColor", new Color(0.13f, 0.18f, 0.09f));
                material.SetColor("_TopColor", new Color(0.56f, 0.33f, 0.58f));
                material.SetColor("_DryColor", new Color(0.45f, 0.36f, 0.3f));
                material.SetFloat("_DryAmount", 0.15f);
                material.SetFloat("_GroundColor", 0.15f);
                material.SetFloat("_GroundTip", 0.05f);
                material.SetFloat("_WindStrength", 0.05f);
                break;
            case Shape.WildFlowers:
                bool white = name.StartsWith("White");
                Coloured(material, EnsurePalette(name + ".png", new[]
                {
                    Color.white, Color.white, new Color(0.25f, 0.42f, 0.17f),
                    white ? new Color(0.96f, 0.96f, 0.92f) : new Color(1f, 0.84f, 0.15f),
                    white ? new Color(0.98f, 0.85f, 0.25f) : new Color(0.9f, 0.62f, 0.08f)
                }), 0.1f);
                material.SetFloat("_NormalUp", 0.3f);
                material.SetFloat("_WindStrength", 0.08f);
                material.SetFloat("_PushStrength", 0.25f);
                material.SetFloat("_Flatten", 0.4f);
                break;
        }
    }

    /// <summary>Colour from the texture, not from the grass gradient or the ground.</summary>
    private static void Coloured(Material material, Texture2D texture, float cutoff)
    {
        material.SetTexture("_BaseMap", texture);
        material.SetFloat("_Cutoff", cutoff);
        material.SetColor("_BottomColor", Color.white);
        material.SetColor("_TopColor", Color.white);
        material.SetFloat("_GroundColor", 0f);
        material.SetFloat("_DryAmount", 0f);
        material.SetFloat("_BladeVariation", 0.15f);
        material.SetFloat("_ClumpHeight", 0.2f);
    }

    private static Texture2D EnsureTexture(string file, System.Func<Texture2D> pixels)
    {
        string path = $"{Folder}/{file}";
        if (!File.Exists(path))
        {
            Texture2D texture = pixels();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true; // cut-outs keep their shape at a distance
            importer.alphaTestReferenceValue = 0.45f;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Palette texture of the glowing plants: one colour per column, picked by UV x.
    private const float StemU = 0.5f / 5f, CapU = 1.5f / 5f, StalkU = 2.5f / 5f, PetalU = 3.5f / 5f, CentreU = 4.5f / 5f;

    private static Texture2D EnsureGlowPalette() => EnsurePalette("Glow Plants.png", new[]
    {
        new Color(0.86f, 0.86f, 0.8f),  // mushroom stem
        new Color(0.62f, 0.78f, 0.9f),  // mushroom cap
        new Color(0.25f, 0.45f, 0.18f), // flower stalk
        new Color(0.72f, 0.86f, 0.98f), // petals
        new Color(0.9f, 0.95f, 0.75f),  // flower centre
    });

    /// <summary>Five colours in a row (mushroom stem, cap, flower stalk, petals, flower centre), picked by UV x.</summary>
    private static Texture2D EnsurePalette(string file, Color[] colours)
    {
        string path = $"{Folder}/{file}";
        if (!File.Exists(path))
        {
            var texture = new Texture2D(5, 1, TextureFormat.RGBA32, false);
            texture.SetPixels(colours);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>A little cluster of mushrooms: six-sided stems and domed caps. Vertex colour R = sway (slight),
    /// G = per-mushroom random, B = glow (caps 1, stems faint).</summary>
    public static Mesh BuildMushrooms(int count, float radius, float height, int seed)
    {
        var random = new System.Random(seed);
        var vertices = new List<Vector3>();
        var colours = new List<Color>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int m = 0; m < count; m++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = m == 0 ? 0f : radius * (0.35f + 0.65f * (float)random.NextDouble());
            Vector3 root = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            float size = m == 0 ? 1f : 0.5f + 0.4f * (float)random.NextDouble();
            float stemHeight = height * size;
            float stemRadius = 0.016f * size + 0.008f;
            float capRadius = 0.065f * size + 0.02f;
            float shade = (float)random.NextDouble();
            Vector3 lean = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * (0.25f * stemHeight);
            const int sides = 6;
            // Stem: a ring at the ground and one under the cap.
            int stem = vertices.Count;
            for (int ring = 0; ring < 2; ring++)
            {
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    Vector3 offset = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * stemRadius * (ring == 0 ? 1.2f : 0.9f);
                    vertices.Add(root + offset + (ring == 1 ? lean + Vector3.up * stemHeight : Vector3.zero));
                    colours.Add(new Color(ring * 0.15f, shade, 0.15f * ring, 1f));
                    uv.Add(new Vector2(StemU, 0.5f));
                }
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                triangles.AddRange(new[] { stem + i, stem + sides + i, stem + j, stem + j, stem + sides + i, stem + sides + j });
            }

            // Cap: a dome of three rings over the stem top, closed underneath by a flat disc.
            Vector3 top = root + lean + Vector3.up * stemHeight;
            int cap = vertices.Count;
            float[] heights = { -0.15f, 0.35f, 0.75f };
            float[] widths = { 1f, 0.85f, 0.45f };
            for (int ring = 0; ring < heights.Length; ring++)
            {
                for (int i = 0; i < sides * 2; i++)
                {
                    float a = i * Mathf.PI / sides;
                    vertices.Add(top + new Vector3(Mathf.Cos(a) * capRadius * widths[ring], heights[ring] * capRadius, Mathf.Sin(a) * capRadius * widths[ring]));
                    colours.Add(new Color(0.15f, shade, 1f, 1f));
                    uv.Add(new Vector2(CapU, 0.5f));
                }
            }

            int apex = vertices.Count;
            vertices.Add(top + Vector3.up * capRadius * 0.95f);
            colours.Add(new Color(0.15f, shade, 1f, 1f));
            uv.Add(new Vector2(CapU, 0.5f));
            int under = vertices.Count;
            vertices.Add(top - Vector3.up * capRadius * 0.1f);
            colours.Add(new Color(0.15f, shade, 0.6f, 1f));
            uv.Add(new Vector2(CapU, 0.5f));
            int n = sides * 2;
            for (int ring = 0; ring < heights.Length - 1; ring++)
            {
                for (int i = 0; i < n; i++)
                {
                    int a = cap + ring * n + i, b = cap + ring * n + (i + 1) % n;
                    triangles.AddRange(new[] { a, a + n, b, b, a + n, cap + (ring + 1) * n + (i + 1) % n });
                }
            }

            for (int i = 0; i < n; i++)
            {
                int last = cap + (heights.Length - 1) * n;
                triangles.AddRange(new[] { last + i, apex, last + (i + 1) % n });
                triangles.AddRange(new[] { cap + (i + 1) % n, under, cap + i });
            }
        }

        return Finish(vertices, colours, uv, triangles);
    }

    /// <summary>A few flowers: a curved stalk (a thin blade) with five petals around a small centre at the top.
    /// Vertex colour R = sway (0 at the ground, 1 at the flower), G = per-flower random, B = glow (petals, centre).</summary>
    public static Mesh BuildFlowers(int count, float radius, float height, int seed)
    {
        var random = new System.Random(seed);
        var vertices = new List<Vector3>();
        var colours = new List<Color>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int f = 0; f < count; f++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float distance = radius * Mathf.Sqrt((float)random.NextDouble());
            Vector3 root = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            float tall = height * (0.65f + 0.35f * (float)random.NextDouble());
            float shade = (float)random.NextDouble();
            Vector3 bend = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * (0.3f * tall);
            Vector3 side = Vector3.Cross(Vector3.up, bend.sqrMagnitude > 1e-6f ? bend.normalized : Vector3.forward) * 0.008f;
            int stalk = vertices.Count;
            const int segments = 4;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                Vector3 p = root + bend * (t * t) + Vector3.up * (tall * t);
                vertices.Add(p - side * (1f - 0.5f * t));
                vertices.Add(p + side * (1f - 0.5f * t));
                colours.Add(new Color(t, shade, 0f, 1f));
                colours.Add(new Color(t, shade, 0f, 1f));
                uv.Add(new Vector2(StalkU, 0.5f));
                uv.Add(new Vector2(StalkU, 0.5f));
            }

            for (int i = 0; i < segments; i++)
            {
                int a = stalk + i * 2;
                triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }

            Vector3 centre = root + bend + Vector3.up * tall;
            float petal = 0.045f + 0.02f * (float)random.NextDouble();
            float spin = (float)random.NextDouble() * Mathf.PI * 2f;
            for (int k = 0; k < 5; k++)
            {
                float a = spin + k * Mathf.PI * 2f / 5f;
                Vector3 out1 = new Vector3(Mathf.Cos(a), 0.35f, Mathf.Sin(a)).normalized;
                Vector3 across = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a)) * (petal * 0.45f);
                int b = vertices.Count;
                vertices.Add(centre);
                vertices.Add(centre + out1 * (petal * 0.6f) - across);
                vertices.Add(centre + out1 * petal);
                vertices.Add(centre + out1 * (petal * 0.6f) + across);
                for (int v = 0; v < 4; v++)
                {
                    colours.Add(new Color(1f, shade, 1f, 1f));
                    uv.Add(new Vector2(PetalU, 0.5f));
                }

                triangles.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            }

            // A small raised centre: a pyramid over the petals.
            int c = vertices.Count;
            float r = petal * 0.22f;
            for (int k = 0; k < 4; k++)
            {
                float a = spin + k * Mathf.PI / 2f;
                vertices.Add(centre + new Vector3(Mathf.Cos(a) * r, 0.002f, Mathf.Sin(a) * r));
            }

            vertices.Add(centre + Vector3.up * r * 0.8f);
            for (int v = 0; v < 5; v++)
            {
                colours.Add(new Color(1f, shade, 0.8f, 1f));
                uv.Add(new Vector2(CentreU, 0.5f));
            }

            for (int k = 0; k < 4; k++)
            {
                triangles.AddRange(new[] { c + k, c + 4, c + (k + 1) % 4 });
            }
        }

        return Finish(vertices, colours, uv, triangles);
    }

    /// <summary>A fern: fronds (textured cards) arching up and out from the middle, lying open so they show from above.
    /// Vertex colour R = sway (0 at the root, 1 at the tip), G = per-frond random.</summary>
    public static Mesh BuildFern(int fronds, float length, int seed)
    {
        var random = new System.Random(seed);
        float Next(float low, float high) => low + (float)random.NextDouble() * (high - low);
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colours = new List<Color>();
        var triangles = new List<int>();
        const int segments = 6;
        for (int f = 0; f < fronds; f++)
        {
            float angle = (f + Next(-0.3f, 0.3f)) * Mathf.PI * 2f / fronds;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var side = new Vector3(-direction.z, 0f, direction.x);
            float frond = length * Next(0.7f, 1.1f);
            float rise = Next(0.4f, 0.6f);
            float width = frond * 0.45f;
            float shade = Next(0f, 1f);
            Vector3 root = direction * Next(0f, 0.04f);
            int start = positions.Count;
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 centre = root + direction * (frond * t * 0.9f) + Vector3.up * (frond * rise * Mathf.Sin(t * Mathf.PI * 0.75f));
                foreach (float sign in new[] { -1f, 1f })
                {
                    positions.Add(centre + side * (width * 0.5f * sign));
                    normals.Add((Vector3.up + direction * 0.3f).normalized);
                    uvs.Add(new Vector2(sign < 0f ? 0f : 1f, t));
                    colours.Add(new Color(t, shade, 0f, 1f));
                }
            }

            for (int i = 0; i < segments; i++)
            {
                int a = start + i * 2;
                triangles.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 });
            }
        }

        return Finish(positions, normals, uvs, colours, triangles);
    }

    /// <summary>A fern frond (base at the bottom, tip at the top): a stem with paired, tapering leaflets.</summary>
    public static Texture2D FernPixels(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        var stem = new Color(0.22f, 0.33f, 0.11f);
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width - 0.5f;
                Color colour = Color.clear;
                if (Mathf.Abs(u) < 0.012f + 0.01f * (1f - v) && v < 0.97f)
                {
                    colour = stem;
                }
                else
                {
                    for (int i = 0; i < 20; i++)
                    {
                        float at = 0.06f + i * 0.046f;
                        float reach = 0.46f * Mathf.Pow(Mathf.Clamp01(1f - at), 0.7f) * (at < 0.15f ? 0.6f + at * 2.6f : 1f);
                        float side = Mathf.Sign(u);
                        // Each leaflet leans towards the tip; distance along and across it.
                        Vector2 d = new Vector2(Mathf.Abs(u), v - at - Mathf.Abs(u) * 0.35f);
                        float along = d.x / Mathf.Max(reach, 1e-3f);
                        float half = 0.022f * (1f - 0.6f * at) * Mathf.Sqrt(Mathf.Max(0f, 1f - along * along));
                        half *= 0.85f + 0.15f * Mathf.Sin(along * 40f); // small lobes
                        if (along < 1f && Mathf.Abs(d.y) < half)
                        {
                            float light = 0.8f + 0.25f * along + 0.1f * side;
                            colour = new Color(0.3f * light, 0.5f * light, 0.17f * light, 1f);
                            break;
                        }
                    }
                }

                pixels[y * width + x] = colour;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    /// <summary>A sprig of a dwarf shrub: small oval leaves at all angles and a few berries (blue for blueberry, red
    /// for lingonberry, whose leaves are darker).</summary>
    public static Texture2D BerryShrubPixels(int size, bool blue)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        var random = new System.Random(blue ? 61 : 67);
        float Next(float low, float high) => low + (float)random.NextDouble() * (high - low);
        Color leafA = blue ? new Color(0.3f, 0.48f, 0.17f) : new Color(0.17f, 0.32f, 0.11f);
        Color leafB = blue ? new Color(0.22f, 0.38f, 0.13f) : new Color(0.12f, 0.24f, 0.08f);
        Color berry = blue ? new Color(0.18f, 0.22f, 0.5f) : new Color(0.75f, 0.08f, 0.1f);
        var leaves = new List<(Vector2 Centre, float Angle, float Length, float Width, float Shade)>();
        for (int i = 0; i < 26; i++)
        {
            float r = Mathf.Sqrt(Next(0f, 1f)) * 0.36f, a = Next(0f, Mathf.PI * 2f);
            leaves.Add((new Vector2(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r), Next(0f, Mathf.PI * 2f),
                        Next(0.1f, 0.15f), Next(0.045f, 0.065f), Next(0f, 1f)));
        }

        var berries = new List<(Vector2 Centre, float Radius)>();
        for (int i = 0; i < 9; i++)
        {
            float r = Mathf.Sqrt(Next(0f, 1f)) * 0.3f, a = Next(0f, Mathf.PI * 2f);
            berries.Add((new Vector2(0.5f + Mathf.Cos(a) * r, 0.5f + Mathf.Sin(a) * r), Next(0.025f, 0.038f)));
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                Color colour = Color.clear;
                foreach (var leaf in leaves)
                {
                    Vector2 d = p - leaf.Centre;
                    float along = d.x * Mathf.Cos(leaf.Angle) + d.y * Mathf.Sin(leaf.Angle);
                    float across = -d.x * Mathf.Sin(leaf.Angle) + d.y * Mathf.Cos(leaf.Angle);
                    float e = (along / leaf.Length) * (along / leaf.Length) * 4f + (across / leaf.Width) * (across / leaf.Width) * 4f;
                    if (e < 1f)
                    {
                        colour = Color.Lerp(leafA, leafB, leaf.Shade) * (0.85f + 0.3f * (1f - e));
                        colour.a = 1f;
                    }
                }

                foreach (var b in berries)
                {
                    float d = Vector2.Distance(p, b.Centre) / b.Radius;
                    if (d < 1f)
                    {
                        float highlight = Mathf.Clamp01(1f - Vector2.Distance(p, b.Centre + new Vector2(-0.3f, 0.3f) * b.Radius) / (b.Radius * 0.45f));
                        colour = Color.Lerp(berry * (1.1f - 0.4f * d), Color.white, highlight * 0.6f);
                        colour.a = 1f;
                    }
                }

                pixels[y * size + x] = colour;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Mesh Finish(List<Vector3> vertices, List<Color> colours, List<Vector2> uv, List<int> triangles)
    {
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetColors(colours);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>A rounded bush of leaf cards (textured quads) facing outwards over a squashed dome.</summary>
    public static Mesh BuildBush(int cards, float radius, float height, int seed)
    {
        var random = new System.Random(seed);
        float Next(float low, float high) => low + (float)random.NextDouble() * (high - low);
        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var colours = new List<Color>();
        var triangles = new List<int>();
        for (int c = 0; c < cards; c++)
        {
            float azimuth = Next(0f, Mathf.PI * 2f);
            float elevation = Mathf.Asin(Next(0.05f, 1f));
            var outward = new Vector3(Mathf.Cos(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation),
                                      Mathf.Sin(azimuth) * Mathf.Cos(elevation));
            Vector3 centre = new Vector3(outward.x * radius, outward.y * height, outward.z * radius) * Next(0.55f, 0.95f);
            Quaternion roll = Quaternion.AngleAxis(Next(0f, 360f), outward);
            Vector3 right = roll * Vector3.Cross(Vector3.up, outward).normalized;
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            Vector3 up = Vector3.Cross(outward, right).normalized;
            float size = Next(0.22f, 0.34f) * (radius / 0.4f);
            float shade = Next(0f, 1f);
            int start = positions.Count;
            for (int corner = 0; corner < 4; corner++)
            {
                float u = corner == 1 || corner == 2 ? 1f : 0f, v = corner >= 2 ? 1f : 0f;
                Vector3 p = centre + right * ((u - 0.5f) * size) + up * ((v - 0.5f) * size);
                positions.Add(p);
                normals.Add(outward);
                uvs.Add(new Vector2(u, v));
                colours.Add(new Color(Mathf.Clamp01(p.y / height) * 0.8f, shade, 0f, 1f));
            }

            triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }

        return Finish(positions, normals, uvs, colours, triangles);
    }

    private static Mesh Finish(List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs, List<Color> colours,
                               List<int> triangles)
    {
        var mesh = new Mesh();
        mesh.SetVertices(positions);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colours);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------- leaf texture ----------
    private static Texture2D EnsureLeafTexture()
    {
        string path = $"{Folder}/Bush Leaves.png";
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, LeafPixels(256).EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = true; // leaves keep their shape at a distance
            importer.alphaTestReferenceValue = 0.5f;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>A few overlapping leaves (light grey, tinted by the material; alpha = shape).</summary>
    public static Texture2D LeafPixels(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        var random = new System.Random(5);
        var leaves = new List<(Vector2 Centre, float Angle, float Length, float Width)>();
        for (int i = 0; i < 6; i++)
        {
            leaves.Add((new Vector2(0.3f + (float)random.NextDouble() * 0.4f, 0.3f + (float)random.NextDouble() * 0.4f),
                        (float)random.NextDouble() * Mathf.PI * 2f, 0.28f + (float)random.NextDouble() * 0.12f,
                        0.1f + (float)random.NextDouble() * 0.05f));
        }

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float alpha = 0f, shade = 0.75f;
                foreach ((Vector2 centre, float angle, float length, float width) in leaves)
                {
                    Vector2 d = p - centre;
                    float along = d.x * Mathf.Cos(angle) + d.y * Mathf.Sin(angle);
                    float across = -d.x * Mathf.Sin(angle) + d.y * Mathf.Cos(angle);
                    float t = along / length; // -0.5..0.5 along the leaf
                    float edge = width * Mathf.Sqrt(Mathf.Max(0f, 1f - 4f * t * t)) * (1f - 0.35f * (t + 0.5f));
                    if (Mathf.Abs(t) < 0.5f && Mathf.Abs(across) < edge)
                    {
                        alpha = 1f;
                        float vein = Mathf.Abs(across) < 0.006f ? 1.15f : 1f;
                        shade = (0.7f + 0.3f * (1f - Mathf.Abs(across) / Mathf.Max(edge, 1e-4f))) * vein;
                    }
                }

                pixels[y * size + x] = new Color(shade, shade, shade, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}
