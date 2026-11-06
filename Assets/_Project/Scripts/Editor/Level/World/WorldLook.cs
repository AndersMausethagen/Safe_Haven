using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The two sets of converted pack assets the world is built from: <see cref="ForestLake"/> (Mountain Lake, the Haven's
/// look) and <see cref="MossyForest"/> (Mossy Cedrus, the raids' dark forest). Each has its prefabs and ground layers
/// under Art/Vegetation, and its palettes, paint rules and crag kit under Data/World. World Tools' biomes, paths,
/// rivers, cliffs, ground paint and terrain looks all come from here. The original PolyHaven assets (WorldAssets) stay
/// for scenes that use them.
/// </summary>
public sealed class WorldLook
{
    public static readonly WorldLook ForestLake = new WorldLook(
        "Forest Lake", "Assets/_Project/Art/Vegetation/Safe Haven", "Assets/_Project/Data/World/Safe Haven", "Haven Lake",
        "Assets/_Project/Art/Terrain/SafeHaven/Haven World Terrain Displacement.mat",
        path: "Haven Path", riverBed: "Haven Rock", riverBank: "Haven Bank", paint: new[] { "Haven Meadow", "Old-Growth Forest" });

    public static readonly WorldLook MossyForest = new WorldLook(
        "Mossy Forest", "Assets/_Project/Art/Vegetation/Misty Woods", "Assets/_Project/Data/World/Mossy Forest", "Mossy Forest",
        "Assets/_Project/Data/Raids/Maps/MistyWoods Map Terrain Displacement.mat",
        path: "Cedar Soil", riverBed: "Cedar Rock", riverBank: "Cedar Soil", paint: new[] { "Dark Forest" });

    public static IReadOnlyList<WorldLook> All { get; } = new[] { ForestLake, MossyForest };

    public readonly string Name;
    public readonly string ArtFolder;
    public readonly string DataFolder;
    private readonly string prefix;
    private readonly string terrainTemplate;
    private readonly string path, riverBed, riverBank;
    private readonly string[] paint;

    private WorldLook(string name, string artFolder, string dataFolder, string prefix, string terrainTemplate,
                      string path, string riverBed, string riverBank, string[] paint)
    {
        Name = name;
        ArtFolder = artFolder;
        DataFolder = dataFolder;
        this.prefix = prefix;
        this.terrainTemplate = terrainTemplate;
        this.path = path;
        this.riverBed = riverBed;
        this.riverBank = riverBank;
        this.paint = paint;
    }

    public string GroundFolder => ArtFolder + "/Ground";

    /// <summary>A palette of this look: "Trees", "Young Trees", "Forest Floor", "Boulders", "Ferns", "River Stones",
    /// "Riverbank" or "Rubble" (the Mossy Forest has "Undergrowth" for ferns and shrubs).</summary>
    public ScatterPalette Palette(string part) => Load<ScatterPalette>($"{DataFolder}/{prefix} {part}.asset");

    public TerrainLayer Ground(string name) => Load<TerrainLayer>($"{GroundFolder}/{name}.terrainlayer");

    /// <summary>Every ground layer of this look, for painting.</summary>
    public IEnumerable<TerrainLayer> Grounds => AssetDatabase.FindAssets("t:TerrainLayer", new[] { GroundFolder })
        .Select(g => Load<TerrainLayer>(AssetDatabase.GUIDToAssetPath(g))).Where(l => l != null).OrderBy(l => l.name);

    public CliffKit Crags => Load<CliffKit>($"{DataFolder}/{prefix} Crags.asset");

    /// <summary>Ground paint rules: the Forest Lake has "Haven Meadow" and "Old-Growth Forest", the Mossy Forest "Dark Forest".</summary>
    public TerrainPaintRules Rules(string part) => Load<TerrainPaintRules>($"{DataFolder}/{prefix} Paint - {part}.asset");

    public IReadOnlyList<string> PaintLooks => paint;

    /// <summary>The look's own terrain material (tessellated, with its rock on steep ground): a terrain takes a copy.</summary>
    public Material TerrainTemplate => Load<Material>(terrainTemplate);

    public TerrainLayer PathGround => Ground(path);
    public TerrainLayer RiverBed => Ground(riverBed);
    public TerrainLayer RiverBank => Ground(riverBank);

    /// <summary>The look's rocky ground (steep slopes; Cliff Rock goes on the steepest, see TerrainRock).</summary>
    public TerrainLayer RockGround => Ground(riverBed);

    /// <summary>The look a terrain is painted in: the one most of its ground layers come from (the Mossy Forest if none).</summary>
    public static WorldLook Of(Terrain terrain)
    {
        TerrainLayer[] layers = terrain != null && terrain.terrainData != null ? terrain.terrainData.terrainLayers : new TerrainLayer[0];
        return All.OrderByDescending(look => layers.Count(l => l != null && AssetDatabase.GetAssetPath(l).StartsWith(look.GroundFolder + "/")))
                  .ThenBy(look => look == MossyForest ? 0 : 1).First();
    }

    /// <summary>The look's grass, moss and small plants (terrain details).</summary>
    public IEnumerable<GameObject> Details => AssetDatabase.FindAssets("t:Prefab", new[] { ArtFolder + "/Details" })
        .Select(g => Load<GameObject>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null).OrderBy(p => p.name);

    public override string ToString() => Name;

    private static T Load<T>(string assetPath) where T : Object => AssetDatabase.LoadAssetAtPath<T>(assetPath);

    // ------------------------------------------------------------------ making what is missing

    /// <summary>Makes the Mossy Forest's own palettes, crag kit and paint rules where missing (the Forest Lake's were made
    /// with the Haven). Existing assets are kept, so a designer's changes survive. Returns how many were made.</summary>
    public static int EnsureAll()
    {
        if (!AssetDatabase.IsValidFolder(MossyForest.ArtFolder))
        {
            return 0;
        }

        Directory.CreateDirectory(MossyForest.DataFolder);
        int made = 0;
        // Tuned for the Prologue's dark forest: the starting point for every mossy forest.
        foreach (string part in new[] { "Trees", "Young Trees", "Forest Floor", "Undergrowth", "Boulders" })
        {
            made += Copy($"Assets/_Project/Data/World/Prologue/Prologue Raid {part}.asset", $"{MossyForest.DataFolder}/Mossy Forest {part}.asset");
        }

        string rocks = MossyForest.ArtFolder + "/Rocks and Logs/";
        string under = MossyForest.ArtFolder + "/Undergrowth/";
        string details = MossyForest.ArtFolder + "/Details/";
        made += MakePalette(MossyForest, "River Stones", 1.4f, (under + "Mossy Stones", 1f), (under + "Small Stone", 0.8f), (under + "Pebbles A", 0.6f),
                            (under + "Pebbles B", 0.6f), (rocks + "Mossy Rock Small", 1f), (rocks + "Grey Rock", 0.4f));
        made += MakePalette(MossyForest, "Riverbank", 1.6f, (details + "Common Fern", 1f), (details + "Small Fern", 1f), (details + "Wild Fern", 0.6f),
                            (details + "Moss Cushion", 0.6f), (rocks + "Mossy Rock Small", 0.4f), (under + "Rotten Branch", 0.4f));
        made += MakePalette(MossyForest, "Rubble", 2f, (rocks + "Grey Rock", 1f), (rocks + "Mossy Rock", 1f), (rocks + "Mossy Rock Small", 1f),
                            (under + "Scree", 0.6f), (under + "Small Stone", 0.8f), (rocks + "Grey Boulder", 0.5f));
        // Along trodden paths: pebbles, stones, fallen twigs and patches of forest floor, nothing that blocks the way.
        made += MakePalette(MossyForest, "Path Edge", 1.4f, (under + "Pebbles A", 1f), (under + "Pebbles B", 1f), (under + "Small Stone", 0.8f),
                            (under + "Short Branches", 0.8f), (under + "Twig Pile", 0.5f), (under + "Rotten Branch", 0.5f), (under + "Mossy Stones", 0.6f),
                            (under + "Forest Floor Patch A", 0.6f), (under + "Forest Floor Patch B", 0.6f), (rocks + "Mossy Rock Small", 0.4f));
        made += MakeCrags(MossyForest, (rocks + "Cliff Boulder A", 1f), (rocks + "Cliff Boulder B", 1f), (rocks + "Cliff Boulder C", 1f),
                          (rocks + "Cliff Slab", 1f), (rocks + "Rock Outcrop A", 1.2f), (rocks + "Rock Outcrop B", 1.2f));
        made += MakeRules(MossyForest, "Dark Forest", "Assets/_Project/Data/World/Paint Rules - Dark Forest.asset", new Dictionary<string, string>
        {
            { "Pine Needle Floor", "Cedar Litter" }, { "Mossy Forest Floor", "Cedar Moss" }, { "Muddy Bank", "Cedar Soil" },
        });
        if (made > 0)
        {
            AssetDatabase.SaveAssets();
        }

        return made;
    }

    private static int Copy(string from, string to)
    {
        if (Load<Object>(to) != null || Load<Object>(from) == null)
        {
            return 0;
        }

        return AssetDatabase.CopyAsset(from, to) ? 1 : 0;
    }

    private static int MakePalette(WorldLook look, string part, float spacing, params (string prefab, float weight)[] entries)
    {
        string assetPath = $"{look.DataFolder}/{look.prefix} {part}.asset";
        if (Load<ScatterPalette>(assetPath) != null)
        {
            return 0;
        }

        var palette = ScriptableObject.CreateInstance<ScatterPalette>();
        palette.spacing = spacing;
        foreach ((string prefab, float weight) in entries)
        {
            var loaded = Load<GameObject>(prefab + ".prefab");
            if (loaded != null)
            {
                palette.entries.Add(new ScatterPalette.Entry { prefab = loaded, weight = weight, scale = new Vector2(0.8f, 1.25f), alignToSlope = 0.6f, sink = 0.05f });
            }
        }

        if (palette.entries.Count == 0)
        {
            Object.DestroyImmediate(palette);
            return 0;
        }

        AssetDatabase.CreateAsset(palette, assetPath);
        return 1;
    }

    private static int MakeCrags(WorldLook look, params (string prefab, float weight)[] pieces)
    {
        string assetPath = $"{look.DataFolder}/{look.prefix} Crags.asset";
        if (Load<CliffKit>(assetPath) != null)
        {
            return 0;
        }

        var kit = ScriptableObject.CreateInstance<CliffKit>();
        foreach ((string prefab, float weight) in pieces)
        {
            var loaded = Load<GameObject>(prefab + ".prefab");
            if (loaded != null)
            {
                kit.Pieces.Add(WorldAssets.Measure(new CliffKit.Piece { prefab = loaded, weight = weight }));
            }
        }

        AssetDatabase.CreateAsset(kit, assetPath);
        return 1;
    }

    private static int MakeRules(WorldLook look, string part, string from, Dictionary<string, string> grounds)
    {
        string assetPath = $"{look.DataFolder}/{look.prefix} Paint - {part}.asset";
        var source = Load<TerrainPaintRules>(from);
        if (Load<TerrainPaintRules>(assetPath) != null || source == null)
        {
            return 0;
        }

        var rules = ScriptableObject.CreateInstance<TerrainPaintRules>();
        foreach (TerrainPaintRules.Rule rule in source.Rules)
        {
            string json = JsonUtility.ToJson(rule);
            var copy = JsonUtility.FromJson<TerrainPaintRules.Rule>(json);
            copy.layer = rule.layer != null && grounds.TryGetValue(rule.layer.name, out string ground) ? look.Ground(ground) : rule.layer;
            rules.Rules.Add(copy);
        }

        AssetDatabase.CreateAsset(rules, assetPath);
        return 1;
    }
}
