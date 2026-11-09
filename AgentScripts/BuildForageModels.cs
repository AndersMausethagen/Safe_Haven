using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Real models for the first things the player gathers (`unity command script run --file AgentScripts/BuildForageModels.cs
/// --entry BuildForageModels.Apply`): Small Rocks, Sticks and the Berry Bush, modelled and baked in Blender by
/// Tools/Blender/forage_nodes.py (Art/Gathering). They replace the sphere and cylinder stand-ins inside the nodes' model
/// prefabs (so the nodes, the dropped Small Rocks and Sticks, and every placed node take them), and the other variants
/// become the nodes' Model Variants, which each spot picks from by its position.
///
/// The materials are the Haven's scans' own (Layered Lit, copied from the Field Stone and the fallen log), so moss grows
/// on the stones and a little on the sticks the same way; the bush has none. Node and item colliders are fitted to the
/// new models, and the node models are scaled evenly (the stand-ins were stretched 1.7 high). Re-runnable.
/// </summary>
public static class BuildForageModels
{
    private const string Art = "Assets/_Project/Art/Gathering/";

    private sealed class Asset
    {
        public string Name;           // folder and FBX under Art/Gathering
        public string[] Variants;     // object names in the FBX; the first is the node's own model
        public string ModelPrefab;    // the stand-in prefab every node and item nests
        public string Definition;     // the GatheringNodeDefinition
        public string Node;           // the node prefab
        public string Item;           // the dropped item prefab, if any
        public float NodeScale;       // the node's model, scaled evenly
        public string MaterialFrom;   // the scan whose material this copies
        public float Moss;            // share of the scan's moss
        public int TextureSize;
    }

    private static readonly Asset[] Assets =
    {
        new Asset
        {
            Name = "Small Rocks", Variants = new[] { "Small Rocks A", "Small Rocks B", "Small Rocks C" },
            ModelPrefab = "Assets/_Project/Art/Bootstrap/SmallRocks.prefab", Definition = "Assets/_Project/Data/Gathering/SmallRocks.asset",
            Node = "Assets/_Project/Prefabs/Resources/SmallRocksNode.prefab", Item = "Assets/_Project/Prefabs/Items/SmallRocks.prefab",
            NodeScale = 1.35f, MaterialFrom = "Assets/_Project/Models/PolyHaven/stone_01/stone_01.fbx", Moss = 1f, TextureSize = 1024,
        },
        new Asset
        {
            Name = "Sticks", Variants = new[] { "Sticks A", "Sticks B", "Sticks C" },
            ModelPrefab = "Assets/_Project/Art/Bootstrap/Sticks.prefab", Definition = "Assets/_Project/Data/Gathering/Sticks.asset",
            Node = "Assets/_Project/Prefabs/Resources/SticksNode.prefab", Item = "Assets/_Project/Prefabs/Items/Sticks.prefab",
            NodeScale = 1.35f, MaterialFrom = "Assets/_Project/Models/PolyHaven/dead_tree_trunk_02/dead_tree_trunk_02.fbx", Moss = 0.4f,
            TextureSize = 1024,
        },
        new Asset
        {
            Name = "Berry Bush", Variants = new[] { "Berry Bush A", "Berry Bush B" },
            ModelPrefab = "Assets/_Project/Environment/ResourceNodes/BerryBushModel.prefab", Definition = "Assets/_Project/Data/Gathering/Berries.asset",
            Node = "Assets/_Project/Prefabs/Resources/BerriesNode.prefab", Item = null,
            NodeScale = 1f, MaterialFrom = "Assets/_Project/Models/PolyHaven/stone_01/stone_01.fbx", Moss = 0f, TextureSize = 2048,
        },
    };

    public static string Apply()
    {
        // The Blender script writes the files; the Editor may not have seen them yet (it doesn't refresh unfocused).
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new List<string>();
        foreach (Asset asset in Assets)
        {
            string folder = Art + asset.Name + "/";
            Import(asset, folder);
            Material material = MaterialFor(asset, folder);
            var meshes = asset.Variants.Select(v => MeshIn(folder + asset.Name + ".fbx", v)).ToArray();
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

            SetVariants(asset.Definition, variants);
            Bounds node = FitNode(asset.Node, asset.NodeScale);
            string item = asset.Item != null ? $", item {FitItem(asset.Item)}" : "";
            int triangles = meshes.Sum(m => (int)m.GetIndexCount(0) / 3);
            report.Add($"{asset.Name}: {meshes.Length} looks ({triangles / meshes.Length} triangles on average), node {node.size}{item}");
        }

        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    // ----- Imports and the material -----

    private static void Import(Asset asset, string folder)
    {
        Texture(folder + asset.Name + "_Albedo.png", TextureImporterType.Default, true, asset.TextureSize);
        Texture(folder + asset.Name + "_Normal.png", TextureImporterType.NormalMap, false, asset.TextureSize);
        Texture(folder + asset.Name + "_ORM.png", TextureImporterType.Default, false, asset.TextureSize);
        var model = (ModelImporter)AssetImporter.GetAtPath(folder + asset.Name + ".fbx");
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
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = type;
        importer.sRGBTexture = colour;
        importer.maxTextureSize = size;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }

    private static Material MaterialFor(Asset asset, string folder)
    {
        Material scan = global::UnityEditor.AssetDatabase.LoadAllAssetsAtPath(asset.MaterialFrom).OfType<Material>().First();
        string path = folder + asset.Name + ".mat";
        var material = global::UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(scan);
            global::UnityEditor.AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = scan.shader;
            material.CopyPropertiesFromMaterial(scan);
        }

        material.name = asset.Name;
        material.SetTexture("_BaseMap", Load<Texture2D>(folder + asset.Name + "_Albedo.png"));
        material.SetTextureScale("_BaseMap", Vector2.one);
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", Load<Texture2D>(folder + asset.Name + "_Normal.png"));
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_ORMMap", Load<Texture2D>(folder + asset.Name + "_ORM.png"));
        material.EnableKeyword("_ORMMAP");
        material.SetFloat("_Roughness", 1f);
        material.SetFloat("_OcclusionStrength", 1f);
        material.SetFloat("_L1Amount", scan.GetFloat("_L1Amount") * asset.Moss);
        material.SetFloat("_L2Amount", scan.GetFloat("_L2Amount") * asset.Moss);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static T Load<T>(string path) where T : Object => global::UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);

    private static Mesh MeshIn(string model, string name) =>
        global::UnityEditor.AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().FirstOrDefault(mesh => mesh.name == name);

    // ----- Prefabs -----

    /// <summary>The stand-in prefab keeps its root (and its GUID, so every node and item still nests it); its primitive
    /// children give way to the new model.</summary>
    private static void ReplaceModel(string path, Mesh mesh, Material material)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
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
            model.localScale = Vector3.one * scale;
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
