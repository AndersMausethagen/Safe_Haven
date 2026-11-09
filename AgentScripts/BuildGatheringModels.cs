using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Real models for the gathering nodes (`unity command script run --file AgentScripts/BuildGatheringModels.cs --entry
/// BuildGatheringModels.Apply`), modelled and baked in Blender by the Tools/Blender scripts on prop_bake.py
/// (forage_nodes.py: Small Rocks, Sticks, Berry Bush; ore_nodes.py, ...) into Art/Gathering/&lt;Asset&gt;. They replace
/// the primitive stand-ins inside the nodes' model prefabs (so the nodes, dropped items and every placed node take them),
/// and the other looks become the nodes' Model Variants, which each spot picks from by its position.
///
/// Each asset's material is Layered Lit with all its baked maps: Albedo, Normal, ORM (occlusion, roughness, metallic),
/// Height (parallax occlusion, at the asset's depth), BlendMask (curvature, edge wear, cavity, coverage) and, for
/// glowing ones, Emission. The live moss layer is copied from a Haven scan (the Field Stone's moss) and grows from the
/// baked coverage, at the asset's amount. Node and item colliders are fitted to the new models, node models are scaled
/// evenly, and the pieces mined nodes break into are cut again from the new models (NodeFractureBuilder). Assets whose
/// FBX hasn't been baked yet are skipped. Re-runnable.
/// </summary>
public static class BuildGatheringModels
{
    private const string Art = "Assets/_Project/Art/Gathering/";
    private const string FieldStone = "Assets/_Project/Models/PolyHaven/stone_01/stone_01.fbx";
    private const string Nodes = "Assets/_Project/Prefabs/Resources/";
    private const string Defs = "Assets/_Project/Data/Gathering/";
    private const string NodeModels = "Assets/_Project/Environment/ResourceNodes/";

    private sealed class Asset
    {
        public string Name;              // folder and FBX under Art/Gathering
        public string[] Variants;        // object names in the FBX; the first is the node's own model
        public string ModelPrefab;       // the stand-in prefab every node and item nests
        public string Definition;        // the GatheringNodeDefinition (null: no variants)
        public string Node;              // the node prefab
        public string Item;              // the dropped item prefab, if any
        public float NodeScale = 1f;     // the node's model, scaled evenly
        public int TextureSize = 1024;
        public float Parallax;           // parallax occlusion depth in UV units (0: none)
        public float Moss;               // live moss amount (0: none)
        public Color Glow = Color.black; // HDR multiplier for the baked emission (black: none)
    }

    private static string[] Looks(string name, int count) =>
        Enumerable.Range(0, count).Select(i => $"{name} {(char)('A' + i)}").ToArray();

    private static readonly Asset[] Assets =
    {
        new Asset
        {
            Name = "Small Rocks", Variants = Looks("Small Rocks", 3), ModelPrefab = "Assets/_Project/Art/Bootstrap/SmallRocks.prefab",
            Definition = Defs + "SmallRocks.asset", Node = Nodes + "SmallRocksNode.prefab", Item = "Assets/_Project/Prefabs/Items/SmallRocks.prefab",
            NodeScale = 1.35f, Parallax = 0.012f, Moss = 0.55f,
        },
        new Asset
        {
            Name = "Sticks", Variants = Looks("Sticks", 3), ModelPrefab = "Assets/_Project/Art/Bootstrap/Sticks.prefab",
            Definition = Defs + "Sticks.asset", Node = Nodes + "SticksNode.prefab", Item = "Assets/_Project/Prefabs/Items/Sticks.prefab",
            NodeScale = 1.35f, Parallax = 0.006f, Moss = 0.3f,
        },
        new Asset
        {
            Name = "Berry Bush", Variants = Looks("Berry Bush", 2), ModelPrefab = NodeModels + "BerryBushModel.prefab",
            Definition = Defs + "Berries.asset", Node = Nodes + "BerriesNode.prefab", TextureSize = 2048,
        },

        // Stone and ore (ore_nodes.py): boulders of 2 to 3 m.
        Boulder("Granite", "GraniteModel", "Granite", "GraniteNode", moss: 0.45f),
        Boulder("Copper Ore", "CopperOreModel", "CopperOre", "CopperOreNode", moss: 0.35f),
        Boulder("Iron Ore", "IronOreModel", "IronOre", "IronOreNode", moss: 0.25f),
        Boulder("Moonsilver Ore", "MoonsilverOreModel", "MoonsilverOre", "MoonsilverOreNode", moss: 0.15f,
            glow: new Color(1.6f, 1.6f, 1.6f)),
        Boulder("Deepstone", "DeepstoneModel", "Deepstone", "DeepstoneNode", moss: 0f, glow: new Color(2.6f, 2.6f, 2.6f)),
        new Asset
        {
            Name = "Rootbound Stone", Variants = Looks("Rootbound Stone", 1), ModelPrefab = NodeModels + "RootboundStoneModel.prefab",
            Node = Nodes + "RootboundStone.prefab", TextureSize = 2048, Parallax = 0.01f, Moss = 0.4f, Glow = new Color(2.2f, 2.2f, 2.2f),
        },

        // The Life Orbs' old places (orb_nodes.py): carved standing stones, roots cradling the light.
        Single("Life Orb", "LifeOrbModel", "LifeOrb", "LifeOrbNode", glow: 1.4f),
        Single("Life Mote", "LifeMoteModel", "LifeMote", "LifeMoteNode", glow: 1.2f),
        Single("Radiant Life Orb", "RadiantLifeOrbModel", "RadiantLifeOrb", "RadiantLifeOrbNode", glow: 1.7f),

        // Plants (plant_nodes.py): two looks each, no moss or parallax (thin leaves).
        Plant("Healing Herb", NodeModels + "HealingHerbModel", "HealingHerb", "HealingHerbNode"),
        Plant("Flax", NodeModels + "FlaxModel", "Flax", "FlaxNode"),
        Plant("Huldra Silk", NodeModels + "HuldraSilkModel", "HuldraSilk", "HuldraSilkNode", glow: 1.2f),
        Plant("Nettle", NodeModels + "NettleFiberModel", "NettleFiber", "NettleFiberNode"),
        Plant("Chanterelles", HavenModels + "Chanterelle Patch", "Chanterelles", "ChanterellesNode", parallax: 0.004f),
        Plant("Cloudberries", HavenModels + "Cloudberry Patch", "CloudberryPatch", "CloudberryPatchNode"),
        Plant("Wild Barley", HavenModels + "Wild Barley Stand", "WildBarley", "WildBarleyNode"),
        Plant("Wild Turnips", HavenModels + "Wild Turnip Clump", "WildTurnips", "WildTurnipsNode"),
    };

    private const string HavenModels = "Assets/_Project/Art/Haven/Models/";

    private static Asset Single(string name, string model, string definition, string node, float glow) =>
        new Asset
        {
            Name = name, Variants = Looks(name, 1), ModelPrefab = NodeModels + model + ".prefab", Definition = Defs + definition + ".asset",
            Node = Nodes + node + ".prefab", Parallax = 0.008f, Moss = 0.45f, Glow = new Color(glow, glow, glow),
        };

    private static Asset Plant(string name, string model, string definition, string node, float glow = 0f, float parallax = 0f) =>
        new Asset
        {
            Name = name, Variants = Looks(name, 2), ModelPrefab = model + ".prefab", Definition = Defs + definition + ".asset",
            Node = Nodes + node + ".prefab", TextureSize = 2048, Parallax = parallax, Moss = 0f, Glow = new Color(glow, glow, glow),
        };

    private static Asset Boulder(string name, string model, string definition, string node, float moss, Color? glow = null) =>
        new Asset
        {
            Name = name, Variants = Looks(name, 2), ModelPrefab = NodeModels + model + ".prefab", Definition = Defs + definition + ".asset",
            Node = Nodes + node + ".prefab", TextureSize = 2048, Parallax = 0.01f, Moss = moss, Glow = glow ?? Color.black,
        };

    public static string Apply()
    {
        // The Blender scripts write the files; the Editor may not have seen them yet (it doesn't refresh unfocused).
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new List<string>();
        foreach (Asset asset in Assets)
        {
            string folder = Art + asset.Name + "/";
            if (Load<GameObject>(folder + asset.Name + ".fbx") == null)
            {
                continue;
            }

            Import(asset, folder);
            Material material = MaterialFor(asset, folder);
            Mesh[] meshes = asset.Variants.Select(v => MeshIn(folder + asset.Name + ".fbx", v)).ToArray();
            if (meshes.Any(m => m == null))
            {
                return $"{asset.Name}: missing a mesh in the FBX (expected {string.Join(", ", asset.Variants)})";
            }

            ReplaceModel(asset.ModelPrefab, meshes[0], material);
            var variants = new List<GameObject>();
            for (int i = 1; i < meshes.Length; i++)
            {
                variants.Add(VariantPrefab(folder + asset.Variants[i] + ".prefab", meshes[i], material));
            }

            if (asset.Definition != null)
            {
                SetVariants(asset.Definition, variants);
            }

            Bounds node = FitNode(asset.Node, asset.NodeScale);
            string item = asset.Item != null ? $", item {FitItem(asset.Item).size}" : "";
            int triangles = meshes.Sum(m => (int)m.GetIndexCount(0) / 3);
            report.Add($"{asset.Name}: {meshes.Length} looks ({triangles / meshes.Length} triangles on average), node {node.size}{item}");
        }

        // Mined nodes break apart into pieces cut ahead of time from their models: cut them again from the new ones.
        NodeFractureBuilder.CrackAll();
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    // ----- Imports and the material -----

    private static void Import(Asset asset, string folder)
    {
        string prefix = folder + asset.Name;
        Texture(prefix + "_Albedo.png", TextureImporterType.Default, true, asset.TextureSize);
        Texture(prefix + "_Normal.png", TextureImporterType.NormalMap, false, asset.TextureSize);
        Texture(prefix + "_ORM.png", TextureImporterType.Default, false, asset.TextureSize);
        Texture(prefix + "_Height.png", TextureImporterType.Default, false, asset.TextureSize);
        Texture(prefix + "_BlendMask.png", TextureImporterType.Default, false, asset.TextureSize);
        Texture(prefix + "_Emission.png", TextureImporterType.Default, true, asset.TextureSize);
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

    private static void Texture(string path, TextureImporterType type, bool colour, int size)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
        {
            return;
        }

        importer.textureType = type;
        importer.sRGBTexture = colour;
        importer.maxTextureSize = size;
        importer.mipmapEnabled = true;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.SaveAndReimport();
    }

    private static Material MaterialFor(Asset asset, string folder)
    {
        Material scan = AssetDatabase.LoadAllAssetsAtPath(FieldStone).OfType<Material>().First();
        string path = folder + asset.Name + ".mat";
        var material = Load<Material>(path);
        if (material == null)
        {
            material = new Material(scan);
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = scan.shader;
            material.CopyPropertiesFromMaterial(scan);
        }

        string prefix = folder + asset.Name;
        material.name = asset.Name;
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
        material.SetTexture("_ParallaxMap", asset.Parallax > 0 ? height : null);
        material.SetFloat("_Parallax", asset.Parallax);
        if (asset.Parallax > 0 && height != null)
        {
            material.EnableKeyword("_PARALLAXMAP");
        }
        else
        {
            material.DisableKeyword("_PARALLAXMAP");
        }

        // Moss grows from the baked coverage (faces to the sky, in patches) in clumps sized for a prop, not a boulder.
        material.SetTexture("_BlendMask", Load<Texture2D>(prefix + "_BlendMask.png"));
        material.SetFloat("_L1Amount", asset.Moss);
        material.SetFloat("_L1Source", 3f);
        material.SetFloat("_L1Upward", 0.3f);
        material.SetFloat("_L1Patchiness", 0.5f);
        material.SetFloat("_L1PatchScale", 3f);
        material.SetFloat("_L2Amount", 0f);

        var glow = Load<Texture2D>(prefix + "_Emission.png");
        bool glows = glow != null && asset.Glow.maxColorComponent > 0;
        material.SetTexture("_EmissionMap", glows ? glow : null);
        material.SetColor("_EmissionColor", glows ? asset.Glow : Color.black);
        material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    private static Mesh MeshIn(string model, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().FirstOrDefault(mesh => mesh.name == name);

    // ----- Prefabs -----

    /// <summary>The stand-in prefab keeps its root (and its GUID, so every node and item still nests it); its primitive
    /// children give way to the new model. Lights and particle effects on it stay.</summary>
    private static void ReplaceModel(string path, Mesh mesh, Material material)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
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

            AddLook(root.transform, mesh, material);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static GameObject VariantPrefab(string path, Mesh mesh, Material material)
    {
        var root = new GameObject(mesh.name);
        try
        {
            AddLook(root.transform, mesh, material);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void AddLook(Transform parent, Mesh mesh, Material material)
    {
        var look = new GameObject(mesh.name);
        look.transform.SetParent(parent, false);
        look.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = look.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
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

    /// <summary>Scale the node's model evenly and fit its box collider (what the player aims at) to the new look.</summary>
    private static Bounds FitNode(string path, float scale)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform model = root.transform.Find("Model");
            if (model != null)
            {
                model.localScale = Vector3.one * scale;
            }

            Bounds bounds = FitCollider(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
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
            Bounds bounds = FitCollider(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return bounds;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Renderer bounds aren't up to date inside prefab contents: the box comes from the meshes' own bounds and
    /// the transforms down to them.</summary>
    private static Bounds FitCollider(GameObject root)
    {
        var bounds = new Bounds();
        bool first = true;
        Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
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

        // Sticks lie flat: keep the box at least as tall as the stand-ins' (0.2 m), so it stays easy to aim at.
        if (bounds.size.y < 0.2f)
        {
            bounds = new Bounds(new Vector3(bounds.center.x, bounds.min.y + 0.1f, bounds.center.z),
                new Vector3(bounds.size.x, 0.2f, bounds.size.z));
        }

        var box = root.GetComponent<BoxCollider>();
        if (box != null)
        {
            box.center = bounds.center;
            box.size = bounds.size;
        }

        return bounds;
    }
}
