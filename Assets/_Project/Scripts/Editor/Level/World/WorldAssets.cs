using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The World Builder's default assets, made from the CC0 models and textures in Models/PolyHaven and Art/Terrain/World
/// (see their LICENSE.md): rock and forest-floor prefabs, the Rock Faces cliff kit, palettes for the dark raid forests
/// and the light, green Haven, ground layers, paint rules, the spline-river water and the world terrain materials.
/// Existing assets are kept (so a designer's changes survive); Safe Haven › World › Set Up World Assets makes what is
/// missing.
/// </summary>
public static class WorldAssets
{
    public const string DataFolder = "Assets/_Project/Data/World";
    public const string PrefabFolder = "Assets/_Project/Prefabs/Environment/World";
    public const string LayerFolder = "Assets/_Project/Art/Terrain/World";
    private const string ModelFolder = LocalSpawnMaterials.DownloadedModelsRoot;
    private const string Oaks = "Assets/_Project/Models/Generated/Vegetation/";

    /// <summary>Prefab names for the downloaded models (by file name; numbered set pieces keep their number).</summary>
    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        { "rock_face_01", "Rock Face A" },
        { "rock_face_02", "Rock Face B" },
        { "mountainside", "Mountainside Crag" },
        { "boulder_01", "Lichen Boulder" },
        { "rock_07", "Grey Boulder" },
        { "stone_01", "Field Stone" },
        { "rock_moss_set_01", "Mossy Boulder" },
        { "rock_moss_set_02", "Mossy Rock" },
        { "tree_stump_01", "Old Stump A" },
        { "tree_stump_02", "Old Stump B" },
        { "dead_tree_trunk", "Dead Branch" },
        { "dead_tree_trunk_02", "Rotting Log" },
        { "root_cluster_01", "Root Tangle" },
        { "fern_02", "Fern" },
    };

    [MenuItem("Safe Haven/World/Set Up World Assets")]
    private static void SetUpMenu() => Debug.Log("World Builder: " + EnsureAll());

    /// <summary>Makes every missing default asset; returns a summary.</summary>
    public static string EnsureAll()
    {
        int prefabs = EnsurePrefabs();
        int layers = EnsureTerrainLayers();
        int palettes = EnsurePalettes();
        CliffKit kit = RockFaces();
        EnsurePaintRules();
        RiverWater();
        TerrainMaterial(false);
        TerrainMaterial(true);
        AssetDatabase.SaveAssets();
        return $"{prefabs} prefab(s), {layers} ground layer(s), {palettes} palette(s) made; cliff kit has {kit.Pieces.Count} piece(s).";
    }

    // ------------------------------------------------------------------ prefabs

    public static string PrefabPath(string name) => $"{PrefabFolder}/{name}.prefab";

    public static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));

    /// <summary>A prefab per downloaded model (rocks with their colliders and LODs, ferns, stumps, logs).</summary>
    public static int EnsurePrefabs()
    {
        Directory.CreateDirectory(PrefabFolder);
        int made = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ModelFolder.TrimEnd('/') }))
        {
            string modelPath = AssetDatabase.GUIDToAssetPath(guid);
            string name = PrefabName(Path.GetFileNameWithoutExtension(modelPath));
            if (Prefab(name) != null)
            {
                continue;
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = name;
            if (IsTree(instance))
            {
                AddTrunkCollider(instance);
            }

            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath(name));
            Object.DestroyImmediate(instance);
            made++;
        }

        return made;
    }

    /// <summary>A baked conifer (bake_conifer.py): its needles use Tree Wind.</summary>
    public static bool IsTree(GameObject model) =>
        model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
             .Any(m => m != null && m.name.Contains("Needles") && m.shader.name == "Safe Haven/Tree Wind");

    /// <summary>A capsule round the trunk (the bark near the ground), so the player and monsters bump into it but walk
    /// under the branches.</summary>
    private static void AddTrunkCollider(GameObject tree)
    {
        var capsule = tree.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = tree.AddComponent<CapsuleCollider>();
        }

        FitTrunkCollider(tree, capsule);
    }

    /// <summary>Fits the capsule to the trunk: its vertices 0.5-2 m above the trunk's foot (above the root flare), the
    /// 75th percentile of their distance from the trunk's axis (a few bark ridges do not widen it). Only the trunk's
    /// own material counts (not branches, dead twigs or needles).</summary>
    public static bool FitTrunkCollider(GameObject tree, CapsuleCollider capsule)
    {
        MeshFilter lod0 = tree.GetComponentsInChildren<MeshFilter>().FirstOrDefault(f => f.name.EndsWith("_LOD0"));
        if (lod0 == null || lod0.sharedMesh == null)
        {
            return false;
        }

        Mesh mesh = lod0.sharedMesh;
        Material[] materials = lod0.GetComponent<MeshRenderer>().sharedMaterials;
        Matrix4x4 toTree = tree.transform.worldToLocalMatrix * lod0.transform.localToWorldMatrix;
        Vector3[] vertices = mesh.vertices;
        var trunk = new List<Vector3>();
        for (int sub = 0; sub < mesh.subMeshCount && sub < materials.Length; sub++)
        {
            if (materials[sub] != null && materials[sub].name.Contains("trunk"))
            {
                trunk.AddRange(mesh.GetIndices(sub).Distinct().Select(i => toTree.MultiplyPoint3x4(vertices[i])));
            }
        }

        if (trunk.Count == 0)
        {
            return false;
        }

        float foot = trunk.Min(v => v.y);
        List<Vector3> band = trunk.Where(v => v.y > foot + 0.5f && v.y < foot + 2f).ToList();
        if (band.Count < 6)
        {
            band = trunk.Where(v => v.y < foot + 3f).ToList();
        }

        var axis = new Vector2(band.Average(v => v.x), band.Average(v => v.z));
        float[] distances = band.Select(v => Vector2.Distance(new Vector2(v.x, v.z), axis)).OrderBy(d => d).ToArray();
        float radius = distances[Mathf.Min(distances.Length - 1, (int)(distances.Length * 0.75f))];
        capsule.radius = Mathf.Clamp(radius, 0.08f, 0.9f);
        capsule.height = Mathf.Max(mesh.bounds.size.y * 0.6f, capsule.radius * 2f);
        capsule.center = new Vector3(axis.x, foot + capsule.height * 0.5f, axis.y);
        return true;
    }

    /// <summary>Refits the trunk capsules of the existing tree prefabs (after a rebake); returns how many changed.</summary>
    public static int RefitTreeColliders()
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (IsTree(root))
                {
                    AddTrunkCollider(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changed++;
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changed;
    }

    public static string PrefabName(string file)
    {
        if (Names.TryGetValue(file, out string name))
        {
            return name;
        }

        int split = file.LastIndexOf('_');
        if (split > 0 && int.TryParse(file.Substring(split + 1), out int number) && Names.TryGetValue(file.Substring(0, split), out string set))
        {
            return $"{set} {number}";
        }

        return UnityEditor.ObjectNames.NicifyVariableName(file.Replace('_', ' '));
    }

    private static IEnumerable<GameObject> Numbered(string set) =>
        Enumerable.Range(1, 12).Select(i => Prefab($"{set} {i}")).Where(p => p != null);

    // ------------------------------------------------------------------ ground layers

    public static TerrainLayer Layer(string name) =>
        AssetDatabase.LoadAssetAtPath<TerrainLayer>($"{LayerFolder}/{name}/{name}.terrainlayer");

    /// <summary>A TerrainLayer per folder in Art/Terrain/World (colour, normal and mask map; see make_terrain_layers.py).</summary>
    public static int EnsureTerrainLayers()
    {
        if (!Directory.Exists(LayerFolder))
        {
            return 0;
        }

        int made = 0;
        foreach (string folder in Directory.GetDirectories(LayerFolder))
        {
            string name = Path.GetFileName(folder);
            string basePath = $"{LayerFolder}/{name}/{name}";
            SetUpTexture(basePath + "_Normal.jpg", TextureImporterType.NormalMap, true);
            SetUpTexture(basePath + "_Mask.png", TextureImporterType.Default, false);
            SetUpTexture(basePath + "_Color.jpg", TextureImporterType.Default, true);
            SetUpTexture(basePath + "_Height.png", TextureImporterType.Default, false);
            SetUpTexture(basePath + "_AO.jpg", TextureImporterType.Default, false);
            SetUpTexture(basePath + "_Roughness.jpg", TextureImporterType.Default, false);
            if (Layer(name) != null)
            {
                continue;
            }

            var layer = new TerrainLayer
            {
                name = name,
                diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_Color.jpg"),
                normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_Normal.jpg"),
                maskMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_Mask.png"),
                tileSize = name == "Cliff Rock" ? new Vector2(7f, 7f) : name == "Forest Moss" ? new Vector2(3f, 3f) : new Vector2(4f, 4f),
                normalScale = 1f,
            };
            AssetDatabase.CreateAsset(layer, basePath + ".terrainlayer");
            made++;
        }

        return made;
    }

    private static void SetUpTexture(string path, TextureImporterType type, bool sRGB)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
        {
            return;
        }

        if (importer.textureType == type && importer.sRGBTexture == (type == TextureImporterType.NormalMap || sRGB) &&
            importer.wrapMode == TextureWrapMode.Repeat)
        {
            return;
        }

        importer.textureType = type;
        importer.sRGBTexture = sRGB;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.SaveAndReimport();
    }

    // ------------------------------------------------------------------ palettes

    public static ScatterPalette Palette(string name) =>
        AssetDatabase.LoadAssetAtPath<ScatterPalette>($"{ScatterBrush.PaletteFolder}/{name}.asset");

    public static int EnsurePalettes()
    {
        Directory.CreateDirectory(ScatterBrush.PaletteFolder);
        GameObject oak = AssetDatabase.LoadAssetAtPath<GameObject>(Oaks + "rich_oak/rich_oak_Wind.prefab");
        GameObject greenOak = AssetDatabase.LoadAssetAtPath<GameObject>(Oaks + "rich_green_oak_tree_set_in_a_magical_lan/rich_green_oak_tree_set_in_a_magical_lan_Wind.prefab");

        int made = 0;
        made += MakePalette("River Stones", 1.4f, Numbered("Mossy Rock").Select(p => Entry(p, 1f, 0.5f, 1.1f, 0.6f, 0.12f))
                                                    .Append(Entry(Prefab("Field Stone"), 1f, 0.6f, 1.2f, 0.6f, 0.1f)));
        made += MakePalette("Riverbank", 1.6f, Numbered("Fern").Select(p => Entry(p, 1.2f, 0.9f, 1.6f, 0.4f, 0.02f))
                                                 .Concat(Numbered("Mossy Rock").Select(p => Entry(p, 0.4f, 0.4f, 0.9f, 0.7f, 0.1f)))
                                                 .Append(Entry(Prefab("Dead Branch"), 0.4f, 0.8f, 1.2f, 0.95f, 0.05f)));
        made += MakePalette("Cliff Rubble", 2f, new[]
        {
            Entry(Prefab("Grey Boulder"), 1f, 0.6f, 1.3f, 0.6f, 0.15f), Entry(Prefab("Lichen Boulder"), 0.8f, 0.7f, 1.4f, 0.6f, 0.15f),
            Entry(Prefab("Field Stone"), 1f, 0.6f, 1.5f, 0.7f, 0.1f),
        }.Concat(Numbered("Mossy Rock").Select(p => Entry(p, 0.3f, 0.5f, 1f, 0.7f, 0.1f))));
        // The dark forest: tall Norway spruce with some Scots pine (baked from Poly Haven scans, see bake_conifer.py).
        made += MakePalette("Dark Forest Trees", 4f, new[]
        {
            Entry(Prefab("Norway Spruce A"), 1f, 0.8f, 1.15f, 0.05f, 0.1f), Entry(Prefab("Norway Spruce B"), 1f, 0.85f, 1.2f, 0.05f, 0.1f),
            Entry(Prefab("Norway Spruce C"), 0.8f, 0.85f, 1.2f, 0.05f, 0.1f), Entry(Prefab("Scots Pine A"), 0.35f, 0.85f, 1.1f, 0.05f, 0.1f),
            Entry(Prefab("Scots Pine B"), 0.35f, 0.85f, 1.1f, 0.05f, 0.1f), Entry(Prefab("Scots Pine C"), 0.3f, 0.85f, 1.1f, 0.05f, 0.1f),
        });
        made += MakePalette("Forest Floor Debris", 3f, new[]
        {
            Entry(Prefab("Old Stump A"), 1f, 0.8f, 1.2f, 0.4f, 0.08f), Entry(Prefab("Old Stump B"), 1f, 0.8f, 1.2f, 0.4f, 0.08f),
            Entry(Prefab("Rotting Log"), 0.7f, 0.8f, 1.2f, 0.95f, 0.1f), Entry(Prefab("Root Tangle"), 0.4f, 0.7f, 1f, 0.5f, 0.2f),
            Entry(Prefab("Dead Branch"), 0.8f, 0.8f, 1.3f, 0.95f, 0.04f),
        }.Concat(Numbered("Mossy Boulder").Select(p => Entry(p, 0.25f, 0.6f, 1f, 0.6f, 0.15f))));
        made += MakePalette("Ferns", 0.9f, Numbered("Fern").Select(p => Entry(p, 1f, 0.9f, 1.7f, 0.5f, 0.02f)));
        made += MakePalette("Haven Trees", 6f, new[]
        {
            Entry(oak, 1f, 0.85f, 1.2f, 0.05f, 0.05f), Entry(greenOak, 0.7f, 0.85f, 1.2f, 0.05f, 0.05f),
            Entry(Prefab("Scots Pine B"), 0.35f, 0.8f, 1.05f, 0.05f, 0.1f), Entry(Prefab("Norway Spruce C"), 0.25f, 0.7f, 1f, 0.05f, 0.1f),
        });
        made += MakePalette("Mossy Boulders", 4f, Numbered("Mossy Boulder").Select(p => Entry(p, 1f, 0.5f, 0.9f, 0.6f, 0.15f))
                                                   .Append(Entry(Prefab("Lichen Boulder"), 0.6f, 0.6f, 1f, 0.6f, 0.15f)));
        return made;
    }

    private static ScatterPalette.Entry Entry(GameObject prefab, float weight, float minScale, float maxScale, float align, float sink) =>
        new ScatterPalette.Entry { prefab = prefab, weight = weight, scale = new Vector2(minScale, maxScale), alignToSlope = align, sink = sink };

    private static int MakePalette(string name, float spacing, IEnumerable<ScatterPalette.Entry> entries)
    {
        string path = $"{ScatterBrush.PaletteFolder}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<ScatterPalette>(path) != null)
        {
            return 0;
        }

        var palette = ScriptableObject.CreateInstance<ScatterPalette>();
        palette.spacing = spacing;
        palette.entries.AddRange(entries.Where(e => e.prefab != null));
        if (palette.entries.Count == 0)
        {
            Object.DestroyImmediate(palette);
            return 0;
        }

        AssetDatabase.CreateAsset(palette, path);
        return 1;
    }

    // ------------------------------------------------------------------ cliff kit

    /// <summary>The Rock Faces kit: scanned rock faces and a crag (boulders go in the rubble at the foot instead).</summary>
    public static CliffKit RockFaces()
    {
        string path = $"{DataFolder}/Rock Faces.asset";
        var kit = AssetDatabase.LoadAssetAtPath<CliffKit>(path);
        if (kit != null)
        {
            return kit;
        }

        Directory.CreateDirectory(DataFolder);
        kit = ScriptableObject.CreateInstance<CliffKit>();
        void Add(GameObject prefab, float weight)
        {
            if (prefab != null)
            {
                kit.Pieces.Add(Measure(new CliffKit.Piece { prefab = prefab, weight = weight }));
            }
        }

        Add(Prefab("Rock Face A"), 2f);
        Add(Prefab("Rock Face B"), 2f);
        Add(Prefab("Mountainside Crag"), 1.2f);

        AssetDatabase.CreateAsset(kit, path);
        return kit;
    }

    /// <summary>Works out which way a piece's finished side faces (the area-weighted average of its surface normals:
    /// its direction on the ground plane and how far it leans back) and its size once stood up, seen from the front.</summary>
    public static CliffKit.Piece Measure(CliffKit.Piece piece)
    {
        if (piece.prefab == null)
        {
            return piece;
        }

        Vector3 facing = Vector3.zero;
        var points = new List<Vector3>();
        Transform rootTransform = piece.prefab.transform;
        foreach (MeshFilter filter in piece.prefab.GetComponentsInChildren<MeshFilter>())
        {
            Mesh mesh = filter.sharedMesh;
            var renderer = filter.GetComponent<MeshRenderer>();
            if (mesh == null || renderer == null || !mesh.isReadable && mesh.vertexCount == 0)
            {
                continue;
            }

            if (filter.name.Contains("_LOD") && !filter.name.EndsWith("_LOD0"))
            {
                continue;
            }

            Matrix4x4 toRoot = rootTransform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Vector3 a = toRoot.MultiplyPoint3x4(vertices[triangles[t]]);
                Vector3 b = toRoot.MultiplyPoint3x4(vertices[triangles[t + 1]]);
                Vector3 c = toRoot.MultiplyPoint3x4(vertices[triangles[t + 2]]);
                facing += Vector3.Cross(b - a, c - a); // length = twice the area
            }

            foreach (Vector3 v in vertices)
            {
                points.Add(toRoot.MultiplyPoint3x4(v));
            }
        }

        Vector3 flat = new Vector3(facing.x, 0f, facing.z);
        bool faces = flat.sqrMagnitude > 1e-6f && flat.magnitude > facing.magnitude * 0.15f; // a closed boulder faces nowhere
        piece.faceYaw = faces ? Vector3.SignedAngle(Vector3.forward, flat, Vector3.up) : 0f;
        piece.faceTilt = faces ? Mathf.Clamp(Mathf.Atan2(facing.y, flat.magnitude) * Mathf.Rad2Deg, 0f, 70f) : 0f;
        Quaternion toFront = StandUp(piece);
        if (points.Count > 0)
        {
            var bounds = new Bounds(toFront * points[0], Vector3.zero);
            foreach (Vector3 p in points)
            {
                bounds.Encapsulate(toFront * p);
            }

            piece.size = bounds.size;
        }

        return piece;
    }

    /// <summary>Turns a piece so its finished side faces +Z, upright.</summary>
    public static Quaternion StandUp(CliffKit.Piece piece) =>
        Quaternion.Euler(piece.faceTilt, 0f, 0f) * Quaternion.Euler(0f, -piece.faceYaw, 0f);

    // ------------------------------------------------------------------ paint rules

    public static TerrainPaintRules PaintRules(bool haven) => PaintRules(haven ? "Haven Meadow" : "Dark Forest");

    /// <summary>A ready-made paint rule set: "Haven Meadow", "Old-Growth Forest" or "Dark Forest".</summary>
    public static TerrainPaintRules PaintRules(string name) =>
        AssetDatabase.LoadAssetAtPath<TerrainPaintRules>($"{DataFolder}/Paint Rules - {name}.asset");

    public static void EnsurePaintRules()
    {
        Directory.CreateDirectory(DataFolder);
        MakeRules("Dark Forest", new[]
        {
            Rule("Pine Needle Floor", 1f, 0f, 90f, 0f, 0f, 8f),
            Rule("Mossy Forest Floor", 0.9f, 0f, 28f, 0f, 0.65f, 11f),
            Rule("Muddy Bank", 0.85f, 0f, 30f, 0.55f, 0.35f, 6f),
        });
        // Norwegian old growth round the Haven: a dark moss carpet, mossy mud and litter in patches, black humus in hollows.
        MakeRules("Old-Growth Forest", new[]
        {
            Rule("Forest Moss", 1f, 0f, 90f, 0f, 0f, 8f),
            Rule("Muddy Bank", 0.6f, 0f, 35f, 0f, 0.75f, 9f),
            Rule("Forest Litter", 0.5f, 0f, 35f, -0.2f, 0.8f, 12f),
            Rule("Dark Humus", 0.8f, 0f, 30f, 0.75f, 0.5f, 5f),
        });
        MakeRules("Haven Meadow", new[]
        {
            Rule("Meadow Grass", 1f, 0f, 90f, 0f, 0f, 8f),
            Rule("Lush Grass", 0.8f, 0f, 22f, 0f, 0.6f, 14f),
            Rule("Trodden Path", 0.55f, 0f, 30f, 0.6f, 0.5f, 6f),
        });
    }

    private static TerrainPaintRules.Rule Rule(string layer, float strength, float minSlope, float maxSlope, float hollows,
                                               float patchiness, float patchSize) =>
        new TerrainPaintRules.Rule
        {
            layer = Layer(layer), strength = strength, slope = new Vector2(minSlope, maxSlope), hollows = hollows,
            patchiness = patchiness, patchSize = patchSize, softness = 6f,
        };

    private static void MakeRules(string name, TerrainPaintRules.Rule[] rules)
    {
        string path = $"{DataFolder}/Paint Rules - {name}.asset";
        if (AssetDatabase.LoadAssetAtPath<TerrainPaintRules>(path) != null || rules.Any(r => r.layer == null))
        {
            return;
        }

        var asset = ScriptableObject.CreateInstance<TerrainPaintRules>();
        asset.Rules.AddRange(rules);
        AssetDatabase.CreateAsset(asset, path);
    }

    // ------------------------------------------------------------------ materials

    /// <summary>Water for spline rivers: the river water, its flow taken from the mesh (so it follows the bends) and
    /// steep stretches drawn as falling water.</summary>
    public static Material RiverWater()
    {
        string path = WaterSetup.Folder + "/Water River (World).mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
        {
            return material;
        }

        WaterSetup.Set set = WaterSetup.EnsureAssets();
        material = new Material(set.River) { name = "Water River (World)" };
        material.SetFloat("_FlowFromMesh", 1f);
        material.SetFloat("_FlowSpeed", 1f);
        material.SetFloat("_SteepFaces", 1f);
        material.SetFloat("_FallFoam", 0.85f);
        material.SetFloat("_ShoreFoam", 0.3f);
        material.SetFloat("_FoamTrail", 1.4f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    /// <summary>The world terrain material: URP's terrain plus rock on steep ground. The Haven's is lighter and warmer.</summary>
    public static Material TerrainMaterial(bool haven)
    {
        string path = $"{LayerFolder}/World Terrain ({(haven ? "Haven" : "Dark Forest")}).mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Safe Haven/Terrain (World)");
        if (material != null || shader == null)
        {
            return material;
        }

        string rock = $"{LayerFolder}/Cliff Rock/Cliff Rock";
        material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path), enableInstancing = true };
        material.SetTexture("_CliffAlbedo", AssetDatabase.LoadAssetAtPath<Texture2D>(rock + "_Color.jpg"));
        material.SetTexture("_CliffNormal", AssetDatabase.LoadAssetAtPath<Texture2D>(rock + "_Normal.jpg"));
        material.SetTexture("_CliffMask", AssetDatabase.LoadAssetAtPath<Texture2D>(rock + "_Mask.png"));
        material.SetColor("_CliffTint", haven ? new Color(1.05f, 1.02f, 0.95f) : new Color(0.82f, 0.86f, 0.84f));
        material.SetFloat("_CliffSlopeStart", haven ? 40f : 36f);
        material.SetFloat("_CliffSlopeEnd", haven ? 56f : 52f);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}
