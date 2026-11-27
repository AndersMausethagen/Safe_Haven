using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The ruin kit (Docs/HavenWorldStory_2026-11-26.md step 4, the user 2026-11-26: "signs of an old Haven or Town being there, now
/// overgrown and abandoned"): props of the old village made from the rustic parts of the Medieval Village Megapack (dry-stone wall
/// stubs, cut stone, stick and wattle fences, bare timber frames, a broken cart...), copied into Assets/MeshingunStudio without the
/// pack's materials or prefabs (kept in Plastic, not git). Each prop gets the project's own Layered Lit material with live moss (the
/// overgrowth), the LOD levels the top-down budget allows (ScreenBudget; the pack's LOD0 of the well is 20k triangles), the pack's
/// collision hulls as its collider, and a carving obstacle where it blocks a way (they are stood at runtime, outside the NavMesh
/// bake). Charred variants for the Burnt Farmstead. Run: unity command script run (RuinKitBuilder.Build).
/// </summary>
public static class RuinKitBuilder
{
    private const string Source = "Assets/MeshingunStudio/Medieval Village/Art/Meshes";
    private const string Out = "Assets/_Project/Art/World/Ruins";
    private const string MossFrom = "Assets/_Project/Art/World/Props/Charcoal Clamp/Charcoal Clamp.mat";

    private sealed class Piece
    {
        public string Model;
        public string Name;
        public bool Blocks;
        public bool Charred;
        public Piece(string model, string name, bool blocks, bool charred = false)
        {
            Model = model;
            Name = name;
            Blocks = blocks;
            Charred = charred;
        }
    }

    private static readonly Piece[] Pieces =
    {
        new Piece("SM_WallBricks_01a", "Dry Stone Wall A", true),
        new Piece("SM_WallBricks_02a", "Dry Stone Wall B", true),
        new Piece("SM_WallBricks_03a", "Dry Stone Wall C", true),
        new Piece("SM_WallCobble_01a", "Fieldstone Wall", true),
        new Piece("SM_WallCobble_01b", "Fieldstone Wall Corner", true),
        new Piece("SM_StoneBrick_01a", "Cut Stone A", false),
        new Piece("SM_StoneBrick_03a", "Cut Stone B", false),
        new Piece("SM_StoneBrick_05a", "Cut Stone C", false),
        new Piece("SM_StairStone_01a", "Threshold Stone", false),
        new Piece("SM_Fence_01a", "Stick Fence", true),
        new Piece("SM_Fence_02a", "Stick Fence Gate", true),
        new Piece("SM_Fence_03a", "Wattle Fence", true),
        new Piece("SM_Pebbes_01a", "Stone Pile", false),
        new Piece("SM_WoodStructure_01a", "Timber Frame A", false),
        new Piece("SM_WoodStructure_01b", "Timber Frame B", false),
        new Piece("SM_WoodStructure_01c", "Timber Frame C", false),
        new Piece("SM_Bucket_01a", "Old Bucket", false),
        new Piece("SM_Barrel_01a", "Old Barrel", false),
        new Piece("SM_Trough_01a", "Old Trough", true),
        new Piece("SM_Pile_WoodLog_01a", "Log Pile", true),
        new Piece("SM_CartWheel_01a", "Cart Wheel", false),
        new Piece("SM_Carriage_01a", "Broken Cart", true),
        new Piece("SM_Anvil_01a", "Old Anvil", false),
        new Piece("SM_Plank_4m_01a", "Plank", false),
        new Piece("SM_well_01a", "Village Well", true),
        new Piece("SM_WoodStructure_01a", "Charred Timber Frame A", false, true),
        new Piece("SM_WoodStructure_01c", "Charred Timber Frame C", false, true),
        new Piece("SM_Fence_01a", "Charred Stick Fence", true, true),
        new Piece("SM_Fence_03a", "Charred Wattle Fence", true, true),
        new Piece("SM_Carriage_01a", "Charred Cart", true, true),
        new Piece("SM_Plank_4m_01a", "Charred Plank", false, true),
    };

    // The pack's materials by name (as its prefabs use them, in submesh order), each to its textures' stem.
    private static readonly Dictionary<string, string[]> MaterialsOf = new Dictionary<string, string[]>
    {
        { "SM_WallBricks_01a", new[] { "WallBricks_01a" } }, { "SM_WallBricks_02a", new[] { "WallBricks_01a" } },
        { "SM_WallBricks_03a", new[] { "WallBricks_01a" } }, { "SM_WallCobble_01a", new[] { "WallCobbleKit_01a" } },
        { "SM_WallCobble_01b", new[] { "WallCobbleKit_01a" } }, { "SM_StoneBrick_01a", new[] { "Rocks_01a" } },
        { "SM_StoneBrick_03a", new[] { "Rocks_01a" } }, { "SM_StoneBrick_05a", new[] { "Rocks_01a" } },
        { "SM_StairStone_01a", new[] { "StairStone_01a" } }, { "SM_Fence_01a", new[] { "Wood_01a", "Atlas_01a" } },
        { "SM_Fence_02a", new[] { "Wood_01a", "Atlas_01a" } }, { "SM_Fence_03a", new[] { "Atlas_01a", "Wood_01a" } },
        { "SM_Pebbes_01a", new[] { "GroundFoliage_01a" } }, { "SM_WoodStructure_01a", new[] { "Planks_01a" } },
        { "SM_WoodStructure_01b", new[] { "Planks_01a" } }, { "SM_WoodStructure_01c", new[] { "Planks_01a" } },
        { "SM_Bucket_01a", new[] { "Bucket_01a" } }, { "SM_Barrel_01a", new[] { "Barrel_01a" } },
        { "SM_Trough_01a", new[] { "WoodenObjects_01a" } }, { "SM_Pile_WoodLog_01a", new[] { "WoodenObjects_03a" } },
        { "SM_CartWheel_01a", new[] { "WoodenObjects_02a" } }, { "SM_Carriage_01a", new[] { "Planks_01a" } },
        { "SM_Anvil_01a", new[] { "Anvil_01a" } }, { "SM_Plank_4m_01a", new[] { "Planks_01a" } },
        { "SM_well_01a", new[] { "TrimConcrete_01a", "Planks_01a", "Cobble_01a" } },
    };

    public static string Build()
    {
        Directory.CreateDirectory(Out + "/Materials");
        Directory.CreateDirectory(Out + "/Prefabs");
        var report = new List<string>();
        var moss = AssetDatabase.LoadAssetAtPath<Material>(MossFrom);
        foreach (Piece piece in Pieces)
        {
            string fbxPath = FindModel(piece.Model);
            if (fbxPath == null)
            {
                report.Add($"{piece.Name}: no {piece.Model} under {Source}");
                continue;
            }

            Material[] materials = MaterialsOf[piece.Model].Select(stem => MaterialFor(stem, piece.Charred, moss)).ToArray();
            report.Add(MakePrefab(piece, fbxPath, materials));
        }

        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    private static string FindModel(string model) =>
        AssetDatabase.FindAssets(model + " t:Model", new[] { "Assets/MeshingunStudio" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == model);

    // Sets whose material borrows another set's normal and mask (as the pack's materials do).
    private static readonly Dictionary<string, string> Borrowed = new Dictionary<string, string>
    {
        { "Cobble_01a_N", "Plaster_01a_N" }, { "Cobble_01a_ORM", "Plaster_01a_ORM" },
        { "TrimConcrete_01a_N", "TrimConcrete_01b_N" }, { "TrimConcrete_01a_ORM", "TrimConcrete_01b_ORM" },
    };

    private static Texture2D Texture(string stem, string suffix)
    {
        string wanted = Borrowed.TryGetValue($"{stem}_{suffix}", out string other) ? other : $"{stem}_{suffix}";
        stem = wanted.Substring(0, wanted.LastIndexOf('_'));
        suffix = wanted.Substring(wanted.LastIndexOf('_') + 1);
        foreach (string guid in AssetDatabase.FindAssets($"T_{stem}_{suffix}", new[] { "Assets/MeshingunStudio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path).Equals($"T_{stem}_{suffix}", System.StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        return null;
    }

    // One material per texture set, shared by the props that use it; the charred one darker, with ash rather than moss.
    private static Material MaterialFor(string stem, bool charred, Material moss)
    {
        string name = charred ? $"{stem} Charred" : stem;
        string path = $"{Out}/Materials/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(moss.shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.CopyPropertiesFromMaterial(moss);
        material.shaderKeywords = new string[0];
        material.SetTexture("_BaseMap", Texture(stem, "D"));
        material.SetTexture("_BumpMap", Texture(stem, "N"));
        material.SetTexture("_ORMMap", Texture(stem, "ORM"));
        material.SetTexture("_BlendMask", null);
        material.SetTexture("_ParallaxMap", null);
        material.SetFloat("_Parallax", 0f);
        material.SetColor("_BaseColor", charred ? new Color(0.3f, 0.27f, 0.25f) : Color.white);
        // Overgrown: moss on what faces up, in patches, everywhere (the pack has no blend mask of the project's kind).
        material.SetFloat("_L1Source", 4f);
        material.SetFloat("_L1Amount", charred ? 0.12f : 0.5f);
        material.SetFloat("_L1Upward", 0.55f);
        material.SetFloat("_L1Patchiness", 0.6f);
        material.SetFloat("_L1PatchScale", 2f);
        // Layered Lit always reads its normal map; the mask needs its keyword.
        material.EnableKeyword("_ORMMAP");
        material.EnableKeyword("_LAYERTRIPLANAR_ON");
        material.EnableKeyword("_SEASON_SNOW");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static string MakePrefab(Piece piece, string fbxPath, Material[] materials)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        var levels = new List<(int lod, Mesh mesh, Transform node)>();
        var hulls = new List<MeshFilter>();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh mesh = filter.sharedMesh;
            string n = filter.name.ToLowerInvariant();
            if (n.Contains("convexhull") || n.StartsWith("ucx_"))
            {
                hulls.Add(filter);
                continue;
            }

            int at = n.LastIndexOf("_lod", System.StringComparison.Ordinal);
            int lod = at >= 0 && int.TryParse(n.Substring(at + 4), out int parsed) ? parsed : 0;
            levels.Add((lod, mesh, filter.transform));
        }

        if (levels.Count == 0)
        {
            return $"{piece.Name}: {piece.Model} has no mesh to draw (only hulls)";
        }

        levels.Sort((a, b) => a.lod.CompareTo(b.lod));
        Bounds bounds = Placed(levels[0].mesh.bounds, levels[0].node, model.transform);
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        // The top-down budget: 5-9k for a prop of 1-5 m (the user's 11-26 floor: never cut a prop's shape away), less for small things.
        int cap = size < 1.2f ? 3000 : 9000;
        int first = levels.FindIndex(l => Triangles(l.mesh) <= cap);
        if (first < 0)
        {
            first = levels.Count - 1;
        }

        var chosen = new List<(int lod, Mesh mesh, Transform node)> { levels[first] };
        int top = Triangles(levels[first].mesh);
        foreach (float share in new[] { 0.4f, 0.12f })
        {
            int index = levels.FindIndex(first, l => Triangles(l.mesh) <= top * share && !chosen.Any(c => c.mesh == l.mesh));
            if (index >= 0)
            {
                chosen.Add(levels[index]);
            }
        }

        var root = new GameObject(piece.Name);
        try
        {
            var renderers = new List<Renderer>();
            for (int i = 0; i < chosen.Count; i++)
            {
                var level = new GameObject($"LOD{i}", typeof(MeshFilter), typeof(MeshRenderer));
                level.transform.SetParent(root.transform, false);
                Copy(chosen[i].node, model.transform, level.transform);
                level.GetComponent<MeshFilter>().sharedMesh = chosen[i].mesh;
                var drawn = level.GetComponent<MeshRenderer>();
                drawn.sharedMaterials = Enumerable.Range(0, chosen[i].mesh.subMeshCount).Select(s => materials[Mathf.Min(s, materials.Length - 1)]).ToArray();
                renderers.Add(drawn);
            }

            var group = root.AddComponent<LODGroup>();
            float[] heights = ScreenBudget.LodHeights(bounds, chosen.Select(c => Triangles(c.mesh)).ToList());
            group.SetLODs(chosen.Select((c, i) => new LOD(heights[i], new[] { renderers[i] })).ToArray());
            group.RecalculateBounds();

            // Static: the hulls as they are (a corner wall's two arms, not one hull round both), each placed as its node is.
            foreach (MeshFilter hull in hulls)
            {
                var collider = new GameObject(hulls.Count == 1 ? "Collider" : $"Collider {hulls.IndexOf(hull)}");
                collider.transform.SetParent(root.transform, false);
                Copy(hull.transform, model.transform, collider.transform);
                collider.AddComponent<MeshCollider>().sharedMesh = hull.sharedMesh;
            }

            if (hulls.Count == 0)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }

            if (piece.Blocks)
            {
                var obstacle = root.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = bounds.center;
                obstacle.size = bounds.size;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
            }

            string path = $"{Out}/Prefabs/{piece.Name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return $"{piece.Name}: {chosen.Count} LODs {string.Join("/", chosen.Select(c => Triangles(c.mesh)))} tris (pack LOD{chosen[0].lod} first), " +
                   $"{bounds.size.x:0.0}x{bounds.size.y:0.0}x{bounds.size.z:0.0} m, collider {(hulls.Count > 0 ? $"{hulls.Count} hull(s)" : "box")}{(piece.Blocks ? ", carves" : "")}";
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // A node's place under the model's root, given to a part of the prefab.
    private static void Copy(Transform node, Transform modelRoot, Transform to)
    {
        to.localPosition = modelRoot.InverseTransformPoint(node.position);
        to.localRotation = Quaternion.Inverse(modelRoot.rotation) * node.rotation;
        to.localScale = node.lossyScale;
    }

    // A mesh's bounds where its node puts it under the model's root.
    private static Bounds Placed(Bounds local, Transform node, Transform modelRoot)
    {
        Matrix4x4 m = modelRoot.worldToLocalMatrix * node.localToWorldMatrix;
        var placed = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x, (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z);
            placed.Encapsulate(m.MultiplyPoint3x4(corner));
        }

        return placed;
    }

    private static int Triangles(Mesh mesh)
    {
        int count = 0;
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            count += (int)mesh.GetIndexCount(s) / 3;
        }

        return count;
    }
}
