#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Real models for the gathering nodes (`unity command script run --file AgentScripts/BuildGatheringModels.cs --entry
/// BuildGatheringModels.Apply`). Solid things (stone, ore, the Life Orbs, mushrooms, turnips, small rocks, sticks) are
/// made by LocalSpawn (Hunyuan3D + UltraTex, Tools/LocalSpawn/generate_gathering.py) and made game-ready by
/// Tools/Blender/ingest_generated.py; leafy plants are built by Tools/Blender/plant_nodes.py as leaf cards with wind.
/// Both write Art/Gathering/&lt;Asset&gt;: an FBX of the looks ("&lt;Asset&gt; A", "B"...), each with "LOD1", "LOD2"
/// and, for solid ones, a convex "Collider", and one texture set.
///
/// Set up the way the project's other props are (see the asset conventions): each look is a LODGroup (hard switches for
/// rock, crossfade for foliage, the last level culled); solid nodes collide with the convex hull, plants with a fitted
/// box, wherever the collider sits (the node's root or its nested model, whose overrides are reverted); rock and ore
/// use Layered Lit with every map (parallax, moss grown from the baked coverage, snow and rain), plants Tree Wind
/// (alpha-tested two-sided cards, translucency, wind, giving way to the player). The first look goes into the node's
/// stand-in model prefab (its GUID kept, so every node and item takes it), the others become Model Variants. The pieces
/// mined nodes break into are cut again from the new models. Assets whose FBX hasn't been made are skipped. Re-runnable.
/// </summary>
public static class BuildGatheringModels
{
    private const string Art = "Assets/_Project/Art/Gathering/";
    private const string FieldStone = "Assets/_Project/Models/PolyHaven/stone_01/stone_01.fbx";
    private const string FernLeaves = "Assets/_Project/Art/Vegetation/Safe Haven/Plants/Lake Fern A.prefab";
    private const string Nodes = "Assets/_Project/Prefabs/Resources/";
    private const string Defs = "Assets/_Project/Data/Gathering/";
    private const string NodeModels = "Assets/_Project/Environment/ResourceNodes/";
    private const string HavenModels = "Assets/_Project/Art/Haven/Models/";

    private enum Kind { Stone, Solid, Foliage }

    private sealed class Asset
    {
        public string Name;              // folder and FBX under Art/Gathering
        public int Looks = 2;            // "<Name> A", "B", ...; the first is the node's own model
        public Kind Kind;
        public string ModelPrefab;       // the stand-in prefab every node and item nests
        public string Definition;        // the GatheringNodeDefinition
        public string Node;              // the node prefab
        public string Item;              // the dropped item prefab, if any
        public float NodeScale = 1f;     // the node's model, scaled evenly
        public int TextureSize = 2048;
        public float Parallax;           // parallax occlusion depth in UV units (0: none)
        public float Moss;               // live moss amount (0: none)
        public Color Glow = Color.black; // HDR multiplier for the emission map (black: none)
        public bool Hull;                // collide with the convex hull (solid things you walk round), else a fitted box
        public float[] LodHeights;      // screen height where LOD0, 1, 2 end; null: from ScreenBudget (the game's own view)
        public bool KeepVariants;       // the definition's model variants stay (GreyStone's scanned rocks); extra looks are added

        public string Looked(int index) => $"{Name} {(char)('A' + index)}";
    }

    private static readonly Asset[] Assets =
    {
        // Small things the player picks up: tiny on screen, culled early, boxes to aim at; textures at 1024 (at 2048 a
        // 0.5 m pile was ~3,500 px/m, the Haven's scans are 700-1500).
        new Asset { Name = "Small Rocks", TextureSize = 1024, Kind = Kind.Stone, ModelPrefab = "Assets/_Project/Art/Bootstrap/SmallRocks.prefab",
            Definition = Defs + "SmallRocks.asset", Node = Nodes + "SmallRocksNode.prefab", Item = "Assets/_Project/Prefabs/Items/SmallRocks.prefab",
            NodeScale = 1.35f, Parallax = 0.01f, Moss = 0.5f },
        new Asset { Name = "Sticks", TextureSize = 1024, Kind = Kind.Solid, ModelPrefab = "Assets/_Project/Art/Bootstrap/Sticks.prefab",
            Definition = Defs + "Sticks.asset", Node = Nodes + "SticksNode.prefab", Item = "Assets/_Project/Prefabs/Items/Sticks.prefab",
            NodeScale = 1.35f, Parallax = 0.006f, Moss = 0.25f },

        // Stone and ore: 2-3 m boulders, seen from far, convex hulls.
        Boulder("Granite", "GraniteModel", "Granite", "GraniteNode", moss: 0.5f),
        // GreyStone: the older generated rock (it had 90k vertices and 128 MB of uncompressed textures), now through the
        // same pipeline; its node gets a model prefab nested as "Model" like the others, and keeps the scanned rocks
        // (Field Stone, Grey Boulder, Lichen Boulder) as its other looks.
        new Asset { Name = "Grey Stone", Looks = 1, Kind = Kind.Stone, ModelPrefab = NodeModels + "GreyStoneModel.prefab",
            Definition = Defs + "GreyStone.asset", Node = Nodes + "GreyStoneNode.prefab", Parallax = 0.01f, Moss = 0.4f,
            Hull = true, KeepVariants = true },
        // Ore glows faintly where it is, so it can be found in the dark (copper warm, iron steel, moonsilver white); faint
        // enough that by day the ore shows its own colour and metal (stronger read as orange fungus and white iron).
        Boulder("Copper Ore", "CopperOreModel", "CopperOre", "CopperOreNode", moss: 0.3f, glow: 0.3f),
        Boulder("Iron Ore", "IronOreModel", "IronOre", "IronOreNode", moss: 0.25f, glow: 0.2f),
        Boulder("Moonsilver Ore", "MoonsilverOreModel", "MoonsilverOre", "MoonsilverOreNode", moss: 0.15f, glow: 0.6f),
        Boulder("Deepstone", "DeepstoneModel", "Deepstone", "DeepstoneNode", moss: 0f, glow: 4f),
        new Asset { Name = "Rootbound Stone", Looks = 1, Kind = Kind.Stone, ModelPrefab = NodeModels + "RootboundStoneModel.prefab",
            Node = Nodes + "RootboundStone.prefab", Parallax = 0.01f, Moss = 0.4f, Glow = Grey(2.2f), Hull = true },

        // The Life Orbs' old places.
        Shrine("Life Orb", "LifeOrbModel", "LifeOrb", "LifeOrbNode", glow: 1.6f),
        Shrine("Life Mote", "LifeMoteModel", "LifeMote", "LifeMoteNode", glow: 1.4f),
        Shrine("Radiant Life Orb", "RadiantLifeOrbModel", "RadiantLifeOrb", "RadiantLifeOrbNode", glow: 1.9f),

        // Solid plants (LocalSpawn): Layered Lit, no moss.
        new Asset { Name = "Chanterelles", TextureSize = 1024, Kind = Kind.Solid, ModelPrefab = HavenModels + "Chanterelle Patch.prefab",
            Definition = Defs + "Chanterelles.asset", Node = Nodes + "ChanterellesNode.prefab", Parallax = 0.004f },
        new Asset { Name = "Wild Turnips", TextureSize = 1024, Kind = Kind.Solid, ModelPrefab = HavenModels + "Wild Turnip Clump.prefab",
            Definition = Defs + "WildTurnips.asset", Node = Nodes + "WildTurnipsNode.prefab" },

        // Leaf-card plants (plant_nodes.py): Tree Wind.
        Plant("Berry Bush", NodeModels + "BerryBushModel", "Berries", "BerriesNode"),
        Plant("Healing Herb", NodeModels + "HealingHerbModel", "HealingHerb", "HealingHerbNode"),
        Plant("Flax", NodeModels + "FlaxModel", "Flax", "FlaxNode"),
        Plant("Huldra Silk", NodeModels + "HuldraSilkModel", "HuldraSilk", "HuldraSilkNode"),
        Plant("Nettle", NodeModels + "NettleFiberModel", "NettleFiber", "NettleFiberNode"),
        Plant("Cloudberries", HavenModels + "Cloudberry Patch", "CloudberryPatch", "CloudberryPatchNode"),
        Plant("Wild Barley", HavenModels + "Wild Barley Stand", "WildBarley", "WildBarleyNode"),
    };

    private static Color Grey(float v) => new Color(v, v, v);

    private static Asset Boulder(string name, string model, string definition, string node, float moss, float glow = 0f) =>
        new Asset
        {
            Name = name, Kind = Kind.Stone, ModelPrefab = NodeModels + model + ".prefab", Definition = Defs + definition + ".asset",
            Node = Nodes + node + ".prefab", Parallax = 0.01f, Moss = moss, Glow = Grey(glow), Hull = true,
        };

    private static Asset Shrine(string name, string model, string definition, string node, float glow, int textureSize = 1024) =>
        new Asset
        {
            TextureSize = textureSize,
            Name = name, Looks = 1, Kind = Kind.Stone, ModelPrefab = NodeModels + model + ".prefab", Definition = Defs + definition + ".asset",
            Node = Nodes + node + ".prefab", Parallax = 0.008f, Moss = 0.45f, Glow = Grey(glow), Hull = true,
        };

    private static Asset Plant(string name, string model, string definition, string node) =>
        new Asset
        {
            Name = name, Kind = Kind.Foliage, ModelPrefab = model + ".prefab", Definition = Defs + definition + ".asset",
            Node = Nodes + node + ".prefab", TextureSize = 1024,
        };

    public static string Apply()
    {
        // The Blender scripts write the files; the Editor may not have seen them yet (it doesn't refresh unfocused).
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new List<string>();
        foreach (Asset asset in Assets)
        {
            string folder = Art + asset.Name + "/";
            string fbx = folder + asset.Name + ".fbx";
            if (Load<GameObject>(fbx) == null)
            {
                continue;
            }

            Import(asset, folder);
            Material material = asset.Kind == Kind.Foliage ? FoliageMaterial(asset, folder) : StoneMaterial(asset, folder);
            var looks = new List<Mesh[]>();
            for (int i = 0; i < asset.Looks; i++)
            {
                string look = asset.Looked(i);
                Mesh[] levels = asset.Kind == Kind.Foliage
                    ? new[] { MeshIn(fbx, look), MeshIn(fbx, look + " LOD1"), MeshIn(fbx, look + " LOD2") }
                    : SolidLevels(folder, fbx, look);
                if (levels[0] == null)
                {
                    return $"{asset.Name}: no mesh '{look}' in {fbx}";
                }

                looks.Add(levels.Where(m => m != null).ToArray());
            }

            Mesh hull = asset.Hull ? MeshIn(fbx, asset.Looked(0) + " Collider") : null;
            AdoptModel(asset);
            ReplaceModel(asset, looks[0], material, hull);
            var variants = new List<GameObject>();
            for (int i = 1; i < looks.Count; i++)
            {
                variants.Add(VariantPrefab(asset, folder + asset.Looked(i) + ".prefab", looks[i], material));
            }

            RemoveStaleVariants(asset, folder);
            if (asset.Definition != null && !asset.KeepVariants)
            {
                SetVariants(asset.Definition, variants);
            }

            Bounds node = FitNode(asset, hull);
            string item = asset.Item != null ? $", item {FitItem(asset.Item).size}" : "";
            int triangles = looks.Sum(levels => (int)levels[0].GetIndexCount(0) / 3);
            report.Add($"{asset.Name}: {looks.Count} looks ({triangles / looks.Count} triangles, {looks[0].Length} levels), " +
                       $"{(hull != null ? "hull" : "box")} {node.size}{item}");
        }

        // Mined nodes break apart into pieces cut ahead of time from their models: cut them again from the new ones.
        NodeFractureBuilder.CrackAll();
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    // ----- Imports and materials -----

    private static void Import(Asset asset, string folder)
    {
        string prefix = folder + asset.Name;
        bool cutout = asset.Kind == Kind.Foliage;
        Texture(prefix + "_Albedo.png", TextureImporterType.Default, true, asset.TextureSize, cutout);
        Texture(prefix + "_Normal.png", TextureImporterType.NormalMap, false, asset.TextureSize, false);
        Texture(prefix + "_ORM.png", TextureImporterType.Default, false, asset.TextureSize / 2, false);
        Texture(prefix + "_Height.png", TextureImporterType.Default, false, asset.TextureSize / 2, false);
        Texture(prefix + "_BlendMask.png", TextureImporterType.Default, false, asset.TextureSize / 2, false);
        Texture(prefix + "_Emission.png", TextureImporterType.Default, true, asset.TextureSize / 2, false); // a soft glow
        var model = (ModelImporter)AssetImporter.GetAtPath(prefix + ".fbx");
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importAnimation = false;
        model.animationType = ModelImporterAnimationType.None;
        model.importCameras = false;
        model.importLights = false;
        model.importBlendShapes = false;
        model.isReadable = false;
        model.SaveAndReimport();
    }

    private static void Texture(string path, TextureImporterType type, bool colour, int size, bool cutout)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
        {
            return;
        }

        importer.textureType = type;
        importer.sRGBTexture = colour;
        importer.maxTextureSize = size;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = cutout;
        // Leaf cards: keep the leaves' coverage in the smaller mipmaps, or they thin out and vanish with distance.
        importer.mipMapsPreserveCoverage = cutout;
        importer.alphaTestReferenceValue = 0.45f;
        importer.SaveAndReimport();
    }

    private static Material StoneMaterial(Asset asset, string folder)
    {
        Material scan = AssetDatabase.LoadAllAssetsAtPath(FieldStone).OfType<Material>().First();
        Material material = Begin(folder + asset.Name + ".mat", scan);
        string prefix = folder + asset.Name;
        material.SetTexture("_BaseMap", Load<Texture2D>(prefix + "_Albedo.png"));
        material.SetTextureScale("_BaseMap", Vector2.one);
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", Load<Texture2D>(prefix + "_Normal.png"));
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_ORMMap", Load<Texture2D>(prefix + "_ORM.png"));
        material.EnableKeyword("_ORMMAP");
        material.SetFloat("_Roughness", 1f);
        material.SetFloat("_Metallic", 1f); // the ORM's blue is the metallic itself
        material.SetFloat("_OcclusionStrength", 1f);

        var height = Load<Texture2D>(prefix + "_Height.png");
        bool parallax = asset.Parallax > 0 && height != null;
        material.SetTexture("_ParallaxMap", parallax ? height : null);
        material.SetFloat("_Parallax", parallax ? asset.Parallax : 0f);
        SetKeyword(material, "_PARALLAXMAP", parallax);

        // Moss grows from the baked coverage (faces to the sky, in patches) in clumps sized for a prop, not a boulder.
        material.SetTexture("_BlendMask", Load<Texture2D>(prefix + "_BlendMask.png"));
        material.SetFloat("_L1Amount", asset.Moss);
        material.SetFloat("_L1Source", 3f);
        material.SetFloat("_L1Upward", 0.3f);
        material.SetFloat("_L1Patchiness", 0.5f);
        material.SetFloat("_L1PatchScale", 3f);
        material.SetFloat("_L2Amount", 0f);

        // Everything solid gets wet in the rain and takes snow (one keyword in Layered Lit does both): stone and wood
        // like the Haven's other rocks, and mushroom caps and turnip leaves, which stayed dry in the rain before.
        bool weather = asset.Kind == Kind.Stone || asset.Kind == Kind.Solid;
        material.SetFloat("_SeasonSnow", weather ? 1f : 0f);
        SetKeyword(material, "_SEASON_SNOW", weather);

        var glow = Load<Texture2D>(prefix + "_Emission.png");
        bool glows = glow != null && asset.Glow.maxColorComponent > 0;
        material.SetTexture("_EmissionMap", glows ? glow : null);
        material.SetColor("_EmissionColor", glows ? asset.Glow : Color.black);
        material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        return Finish(material);
    }

    /// <summary>Tree Wind, set up like the Haven's ferns: alpha-tested two-sided cards, translucency, wind from the
    /// mesh's uv1/uv2, and small plants giving way to the player.</summary>
    private static Material FoliageMaterial(Asset asset, string folder)
    {
        Material fern = Load<GameObject>(FernLeaves).GetComponentInChildren<Renderer>(true).sharedMaterial;
        Material material = Begin(folder + asset.Name + ".mat", fern);
        string prefix = folder + asset.Name;
        material.SetTexture("_BaseMap", Load<Texture2D>(prefix + "_Albedo.png"));
        material.SetTextureScale("_BaseMap", Vector2.one);
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", Load<Texture2D>(prefix + "_Normal.png"));
        material.SetTexture("_ORMMap", Load<Texture2D>(prefix + "_ORM.png"));
        // The baked ORM is glTF's (R occlusion, G roughness, B metallic), not HDRP's mask map.
        material.SetFloat("_MaskHDRP", 0f);
        SetKeyword(material, "_MASK_HDRP", false);
        material.SetFloat("_AlphaClip", 1f);
        SetKeyword(material, "_ALPHATEST_ON", true);
        material.SetFloat("_Cutoff", 0.45f);
        material.SetFloat("_Cull", 0f);
        material.SetFloat("_Translucency", 0.5f);
        material.SetFloat("_WindStrength", 1f);
        material.SetFloat("_PushStrength", 0.35f);
        material.SetFloat("_MossAmount", 0f);
        Mesh first = MeshIn(prefix + ".fbx", asset.Looked(0));
        material.SetFloat("_TreeHeight", first != null ? Mathf.Max(0.2f, first.bounds.size.y) : 1f);
        return Finish(material);
    }

    private static Material Begin(string path, Material from)
    {
        var material = Load<Material>(path);
        if (material == null)
        {
            material = new Material(from);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = from.shader;
            material.CopyPropertiesFromMaterial(from);
        }

        material.name = System.IO.Path.GetFileNameWithoutExtension(path);
        return material;
    }

    private static Material Finish(Material material)
    {
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetKeyword(Material material, string keyword, bool on)
    {
        if (on)
        {
            material.EnableKeyword(keyword);
        }
        else
        {
            material.DisableKeyword(keyword);
        }
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    /// <summary>A solid look's levels: Blender's (decimated within the UV islands, so the texture stays put), or, where
    /// Blender couldn't reduce LOD1 to half of LOD0 or LOD2 to a quarter (aiming at ~35% and ~10%), Unity's mesh LOD generator
    /// (ModelSimplifier.Lighter), saved beside the model. Unity's generator simplifies across UV seams, which a rock's
    /// texture hides but a mushroom patch's doesn't (its caps turned grey), so it is the fallback.</summary>
    private static Mesh[] SolidLevels(string folder, string fbx, string look)
    {
        Mesh lod0 = MeshIn(fbx, look);
        if (lod0 == null)
        {
            return new Mesh[] { null };
        }

        int triangles = (int)lod0.GetIndexCount(0) / 3;
        // LOD0 is the import's own mesh: decimated as far as its shape allows (measured there), never further here.
        Mesh[] blender = { lod0, MeshIn(fbx, look + " LOD1"), MeshIn(fbx, look + " LOD2") };
        if (blender[1] != null && blender[2] != null &&
            blender[1].GetIndexCount(0) / 3 <= triangles * 0.5f && blender[2].GetIndexCount(0) / 3 <= triangles * 0.25f)
        {
            return blender;
        }

        return new[] { lod0 }.Concat(SaveLevels(folder, lod0, new[] { (0.35f, " LOD1"), (0.1f, " LOD2") })).ToArray();
    }

    /// <summary>Levels made by Unity's mesh LOD generator from <paramref name="source"/> at these shares of its
    /// triangles, saved in the asset's LODs folder (reused, so references keep their GUIDs).</summary>
    private static Mesh[] SaveLevels(string folder, Mesh source, (float share, string suffix)[] wanted)
    {
        string lodFolder = folder + "LODs";
        if (!AssetDatabase.IsValidFolder(lodFolder))
        {
            AssetDatabase.CreateFolder(folder.TrimEnd('/'), "LODs");
        }

        var levels = new List<Mesh>();
        foreach ((float share, string suffix) in wanted)
        {
            Mesh lighter = ModelSimplifier.Lighter(source, share);
            lighter.name = source.name + suffix;
            string path = $"{lodFolder}/{lighter.name}.asset";
            var existing = Load<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(lighter, existing);
                Object.DestroyImmediate(lighter);
                EditorUtility.SetDirty(existing);
                levels.Add(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(lighter, path);
                levels.Add(lighter);
            }
        }

        AssetDatabase.SaveAssets();
        return levels.ToArray();
    }

    private static Mesh MeshIn(string model, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().FirstOrDefault(mesh => mesh.name == name);

    // ----- Prefabs -----

    /// <summary>A node whose model is a raw child (GreyStone's GLB with a sphere collider) gets a model prefab nested as
    /// "Model", like every other node, so the look, its hull and its variants are built the same way. Nodes that have
    /// one already are left alone.</summary>
    private static void AdoptModel(Asset asset)
    {
        if (Load<GameObject>(asset.ModelPrefab) == null)
        {
            var empty = new GameObject(System.IO.Path.GetFileNameWithoutExtension(asset.ModelPrefab));
            PrefabUtility.SaveAsPrefabAsset(empty, asset.ModelPrefab);
            Object.DestroyImmediate(empty);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(asset.Node);
        try
        {
            if (root.transform.Find("Model") != null)
            {
                return;
            }

            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.transform.GetChild(i).gameObject;
                if (child.GetComponentInChildren<Renderer>(true) != null && child.GetComponentInChildren<ParticleSystem>(true) == null)
                {
                    Object.DestroyImmediate(child);
                }
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(asset.ModelPrefab), root.transform);
            model.name = "Model";
            model.transform.localPosition = Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(root, asset.Node);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>The stand-in prefab keeps its root (and its GUID, so every node and item still nests it); its primitive
    /// children give way to the new look (lights and particle effects stay). Solid things collide with the hull,
    /// on the model's root, where the stand-in's box was.</summary>
    private static void ReplaceModel(Asset asset, Mesh[] levels, Material material, Mesh hull)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(asset.ModelPrefab);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.transform.GetChild(i).gameObject;
                if (child.GetComponentInChildren<Light>(true) == null && child.GetComponentInChildren<ParticleSystem>(true) == null)
                {
                    Object.DestroyImmediate(child);
                }
            }

            AddLook(asset, root.transform, levels, material);
            if (hull != null)
            {
                foreach (BoxCollider box in root.GetComponents<BoxCollider>())
                {
                    Object.DestroyImmediate(box);
                }

                MeshCollider meshCollider = root.TryGetComponent(out MeshCollider existingCollider) ? existingCollider : root.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = hull;
                meshCollider.convex = true;
            }
            else if (root.TryGetComponent(out BoxCollider box))
            {
                FitBox(root, box);
            }

            PrefabUtility.SaveAsPrefabAsset(root, asset.ModelPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject VariantPrefab(Asset asset, string path, Mesh[] levels, Material material)
    {
        var root = new GameObject(levels[0].name);
        try
        {
            AddLook(asset, root.transform, levels, material, onRoot: true);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>A look: a LODGroup over its levels, culled when it gets smaller on screen than the last height.
    /// Rock switches hard; foliage crossfades; distant foliage and the last rock level cast no shadow.</summary>
    private static void AddLook(Asset asset, Transform parent, Mesh[] levels, Material material, bool onRoot = false)
    {
        GameObject look = onRoot ? parent.gameObject : new GameObject(levels[0].name);
        if (!onRoot)
        {
            look.transform.SetParent(parent, false);
        }

        var lods = new LOD[levels.Length];
        Bounds seen = levels[0].bounds;
        seen.size *= asset.NodeScale;
        float[] heights = asset.LodHeights ?? ScreenBudget.LodHeights(seen, levels.Select(m => (int)m.GetIndexCount(0) / 3).ToArray(), asset.Kind == Kind.Foliage);
        for (int i = 0; i < levels.Length; i++)
        {
            var part = new GameObject($"LOD{i}");
            part.transform.SetParent(look.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = levels[i];
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            bool shadow = asset.Kind == Kind.Foliage ? i == 0 : i < levels.Length - 1 || levels.Length == 1;
            renderer.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            float height = heights[Mathf.Min(i, heights.Length - 1)];
            if (i == levels.Length - 1)
            {
                height = heights[heights.Length - 1];
            }

            lods[i] = new LOD(height, new Renderer[] { renderer });
        }

        LODGroup group = look.TryGetComponent(out LODGroup existing) ? existing : look.AddComponent<LODGroup>();
        group.SetLODs(lods);
        group.fadeMode = asset.Kind == Kind.Foliage ? LODFadeMode.CrossFade : LODFadeMode.None;
        group.animateCrossFading = asset.Kind == Kind.Foliage;
        group.RecalculateBounds();
    }

    private static void RemoveStaleVariants(Asset asset, string folder)
    {
        for (int i = asset.Looks; i < 8; i++)
        {
            string path = folder + asset.Looked(i) + ".prefab";
            if (Load<GameObject>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static void SetVariants(string definitionPath, List<GameObject> variants)
    {
        var definition = Load<GatheringNodeDefinition>(definitionPath);
        var data = new SerializedObject(definition);
        SerializedProperty list = data.FindProperty("modelVariants");
        list.arraySize = variants.Count;
        for (int i = 0; i < variants.Count; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = variants[i];
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    /// <summary>The node: its model scaled evenly; the nested model's collider takes the model prefab's values again
    /// (the stand-in's overrides are reverted), and a box on the node's own root is fitted to the new look (or, for a
    /// solid thing, becomes the hull).</summary>
    private static Bounds FitNode(Asset asset, Mesh hull)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(asset.Node);
        try
        {
            Transform model = root.transform.Find("Model");
            if (model != null)
            {
                model.localScale = Vector3.one * asset.NodeScale;
                foreach (Collider nested in model.GetComponents<Collider>())
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(nested))
                    {
                        PrefabUtility.RevertObjectOverride(nested, InteractionMode.AutomatedAction);
                    }
                }
            }

            Bounds bounds = Measure(root);
            if (root.TryGetComponent(out BoxCollider box))
            {
                if (hull != null && model != null && model.GetComponent<MeshCollider>() != null)
                {
                    Object.DestroyImmediate(box); // the model's hull collides now
                }
                else
                {
                    SetBox(box, bounds);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, asset.Node);
            return bounds;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Bounds FitItem(string path)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Bounds bounds = Measure(root);
            if (root.TryGetComponent(out BoxCollider box))
            {
                SetBox(box, bounds);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return bounds;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void FitBox(GameObject owner, BoxCollider box) => SetBox(box, Measure(owner));

    private static void SetBox(BoxCollider box, Bounds boundsInRootSpace)
    {
        // The bounds are in the space of the prefab root the box sits on (Measure is called on that root).
        box.center = boundsInRootSpace.center;
        box.size = boundsInRootSpace.size;
    }

    /// <summary>The LOD0 meshes' bounds in the root's space (renderer bounds aren't up to date inside prefab contents),
    /// at least 0.2 m high so flat things stay easy to aim at.</summary>
    private static Bounds Measure(GameObject root)
    {
        var bounds = new Bounds();
        bool first = true;
        Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || (filter.name.StartsWith("LOD") && filter.name != "LOD0"))
            {
                continue;
            }

            Bounds local = filter.sharedMesh.bounds;
            Matrix4x4 matrix = toRoot * filter.transform.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = local.center + Vector3.Scale(local.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                Vector3 p = matrix.MultiplyPoint3x4(point);
                if (first)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }
        }

        if (bounds.size.y < 0.2f)
        {
            bounds = new Bounds(new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.center.z),
                new Vector3(bounds.size.x, 0.2f, bounds.size.z));
        }

        return bounds;
    }
}
