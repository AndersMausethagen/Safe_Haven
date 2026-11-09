using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Real models for the Haven's stations and buildings (`unity command script run --file
/// AgentScripts/BuildStructureModels.cs --entry BuildStructureModels.Apply`), set up like the gathering nodes'
/// (BuildGatheringModels.cs; the asset conventions): Layered Lit with every map, a LODGroup from ScreenBudget whose
/// LOD0 holds over the whole top-down view, instanced materials, meshes that are not readable.
///
/// Two sources, both under Art/Haven/Structures/&lt;Asset&gt;:
/// - generated (LocalSpawn: Tools/LocalSpawn/generate_gathering.py --group structures, made game-ready by
///   Tools/Blender/ingest_generated.py): an FBX of "&lt;Asset&gt; A" with "LOD1", "LOD2" and one baked texture set;
/// - kit-built (Tools/Blender/smithy.py): an FBX of "&lt;Asset&gt;", "LOD1", "LOD2" whose faces use the kit's tiling
///   scanned materials (Art/Haven/Kit/Textures; "Kit Wood", "Kit Planks", ...), so the textures stay sharp on a
///   building of any size and several buildings share them.
///
/// The pilot: the Campfire's stones and logs (its flame and light stay), the Small Chest (its own model in place of the
/// Storage Shed's scaled-down one, which the Storage Shed keeps), and the Smithy made both ways as two prefabs to
/// compare ("Smithy Generated", "Smithy Kit"); the Smithy the game places is not changed until one is chosen.
/// Assets whose FBX hasn't been made are skipped. Re-runnable.
/// </summary>
public static class BuildStructureModels
{
    private const string Art = "Assets/_Project/Art/Haven/Structures/";
    private const string Kit = "Assets/_Project/Art/Haven/Kit/";
    private const string KitTextures = Kit + "Textures/";
    private const string FieldStone = "Assets/_Project/Models/PolyHaven/stone_01/stone_01.fbx";
    private const string Stations = "Assets/_Project/Environment/Stations/";
    private const string Structures = "Assets/_Project/Prefabs/Structures/";
    private const string MeadowGrass = "Assets/_Project/Art/Terrain/World/Meadow Grass/Meadow Grass";

    private sealed class Generated
    {
        public string Name;
        public int TextureSize = 2048; // the import's size; the baked files may be larger
        public float Parallax;
        public float Moss;
        public Color Tint = Color.white; // over the baked colour, where it sits too bright or too green beside the ground
        public int Cap = 5000;         // the manifest's lod0_triangles, for the budget report
    }

    /// <summary>A tiling scanned material of the kit: its texture set, how it is tinted, and its import size (the kit's
    /// meshes repeat it once every Tile metres, so the size sets the texels a metre).</summary>
    private sealed class KitMaterial
    {
        public string Slot;            // the material's name in the FBX and on disk
        public string Scan;            // texture set under Kit/Textures ("rough_wood"); null: its own maps below
        public Color Tint = Color.white;
        public int Size = 1024;
        public float Parallax;
        public float Metallic = -1f;   // 0-1: one value for the whole material (iron); below 0: the scan's own map
        public float Moss;
    }

    private static readonly KitMaterial[] KitMaterials =
    {
        // Tints above 1 lift a dark scan: under the Haven's low sun the wood read as black and the wall as mud.
        new KitMaterial { Slot = "Kit Wood", Scan = "rough_wood", Tint = new Color(1.15f, 1.02f, 0.88f), Size = 512, Parallax = 0.01f },
        new KitMaterial { Slot = "Kit Planks", Scan = "weathered_planks", Tint = new Color(1.2f, 1.08f, 0.96f), Parallax = 0.004f },
        new KitMaterial { Slot = "Kit Stone", Scan = "rock_wall_10", Tint = new Color(1.2f, 1.3f, 1.45f), Parallax = 0.012f },
        new KitMaterial { Slot = "Kit Iron", Scan = "rust_coarse_01", Tint = new Color(0.5f, 0.5f, 0.55f), Size = 512, Metallic = 0.6f },
    };

    public static string Apply()
    {
        // Blender writes the files; the Editor may not have seen them yet (it doesn't refresh unfocused).
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new List<string>();

        var campfire = new Generated { Name = "Campfire", Parallax = 0.008f, Moss = 0.1f, Cap = 6000, Tint = new Color(0.68f, 0.66f, 0.64f) };
        if (Ready(campfire))
        {
            (Mesh[] levels, Material material) = Setup(campfire);
            ReplaceCampfire(levels, material);
            Refit(Structures + "Campfire.prefab");
            report.Add(Budget(campfire.Name, levels, campfire.Cap, campfire.TextureSize));
        }

        // 1.05 m of chest at 2048 would be ~2,000 texels a metre; the Haven's scans are 700-1500.
        var chest = new Generated { Name = "Small Chest", TextureSize = 1024, Parallax = 0.004f };
        if (Ready(chest))
        {
            (Mesh[] levels, Material material) = Setup(chest);
            GameObject model = LookPrefab(Stations + "SmallChestModel.prefab", "SmallChestModel", levels, new[] { new[] { material } });
            var prefab = Load<GameObject>(Structures + "SmallChest.prefab");
            Transform current = prefab.transform.Find(PrefabBuildUtility.ModelChildName);
            if (current == null || PrefabUtility.GetCorrespondingObjectFromSource(current.gameObject) != model)
            {
                PrefabBuildUtility.ReplaceModel(prefab, model, new PrefabBuildUtility.Placeholder(PrimitiveType.Cube, 0.6f));
            }
            else
            {
                Refit(Structures + "SmallChest.prefab");
            }

            report.Add(Budget(chest.Name, levels, chest.Cap, chest.TextureSize));
        }

        var smithy = new Generated { Name = "Smithy Generated", TextureSize = 4096, Parallax = 0.004f, Cap = 16000, Tint = new Color(0.92f, 0.8f, 0.74f) };
        if (Ready(smithy))
        {
            (Mesh[] levels, Material material) = Setup(smithy);
            LookPrefab(Art + smithy.Name + "/" + smithy.Name + ".prefab", smithy.Name, levels, new[] { new[] { material } });
            report.Add(Budget(smithy.Name, levels, smithy.Cap, smithy.TextureSize));
        }

        string kitSmithy = Art + "Smithy Kit/Smithy Kit.fbx";
        if (Load<GameObject>(kitSmithy) != null)
        {
            report.Add(KitBuilding("Smithy Kit", kitSmithy, 16000));
        }

        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    // ----- Generated models -----

    private static bool Ready(Generated asset) => Load<GameObject>(Art + asset.Name + "/" + asset.Name + ".fbx") != null;

    private static (Mesh[], Material) Setup(Generated asset)
    {
        string folder = Art + asset.Name + "/";
        string prefix = folder + asset.Name;
        Texture(prefix + "_Albedo.png", TextureImporterType.Default, true, asset.TextureSize);
        Texture(prefix + "_Normal.png", TextureImporterType.NormalMap, false, asset.TextureSize);
        Texture(prefix + "_ORM.png", TextureImporterType.Default, false, asset.TextureSize / 2);
        Texture(prefix + "_Height.png", TextureImporterType.Default, false, asset.TextureSize / 2);
        Texture(prefix + "_BlendMask.png", TextureImporterType.Default, false, asset.TextureSize / 2);
        Model(prefix + ".fbx", null);

        Material material = Begin(prefix + ".mat");
        material.SetTexture("_BaseMap", Load<Texture2D>(prefix + "_Albedo.png"));
        material.SetColor("_BaseColor", asset.Tint);
        material.SetTexture("_BumpMap", Load<Texture2D>(prefix + "_Normal.png"));
        material.SetTexture("_ORMMap", Load<Texture2D>(prefix + "_ORM.png"));
        material.EnableKeyword("_ORMMAP");
        material.SetFloat("_Roughness", 1f);
        material.SetFloat("_Metallic", 1f); // the ORM's blue is the metallic itself
        Parallax(material, Load<Texture2D>(prefix + "_Height.png"), asset.Parallax);
        Moss(material, Load<Texture2D>(prefix + "_BlendMask.png"), asset.Moss, 3f);
        Finish(material);
        return (Levels(folder, prefix + ".fbx", asset.Name + " A"), material);
    }

    /// <summary>The Campfire's primitive stones and logs go; its flame and its light (which the Campfire prefab hangs
    /// the flicker on) stay where they are.</summary>
    private static void ReplaceCampfire(Mesh[] levels, Material material)
    {
        string path = Stations + "CampfireModel.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = root.transform.GetChild(i).gameObject;
                bool flame = child.TryGetComponent(out MeshRenderer renderer) && renderer.sharedMaterial != null &&
                             renderer.sharedMaterial.name.Contains("Fire") && child.GetComponent<LODGroup>() == null;
                if (!flame && child.GetComponentInChildren<Light>(true) == null && child.GetComponentInChildren<ParticleSystem>(true) == null)
                {
                    Object.DestroyImmediate(child);
                }
            }

            var look = new GameObject(levels[0].name);
            look.transform.SetParent(root.transform, false);
            look.transform.SetAsFirstSibling();
            AddLevels(look, levels, new[] { new[] { material } });
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>The structure's collider, health bar and navigation size, fitted to its model as it now is.</summary>
    private static void Refit(string prefabPath) =>
        PrefabBuildUtility.EditPrefab(Load<GameObject>(prefabPath), PrefabBuildUtility.FitToModel);

    // ----- Kit-built buildings -----

    private static string KitBuilding(string name, string fbx, int cap)
    {
        var materials = new Dictionary<string, Material>();
        foreach (KitMaterial kit in KitMaterials)
        {
            materials[kit.Slot] = Scanned(kit);
        }

        materials["Kit Turf"] = Turf();
        materials["Kit Ember"] = Load<Material>("Assets/_Project/Materials/Haven/Base Ember.mat");

        // The FBX's faces carry the kit's material names; the import maps each to the kit's own material.
        Model(fbx, materials);
        var model = Load<GameObject>(fbx);
        var levels = new List<Mesh>();
        var slots = new List<Material[]>();
        foreach (string suffix in new[] { "", " LOD1", " LOD2" })
        {
            Transform part = model.transform.Find(name + suffix);
            if (part == null && suffix.Length == 0 && model.TryGetComponent(out MeshFilter _))
            {
                part = model.transform;
            }

            if (part == null)
            {
                continue;
            }

            levels.Add(part.GetComponent<MeshFilter>().sharedMesh);
            slots.Add(part.GetComponent<MeshRenderer>().sharedMaterials);
        }

        LookPrefab(System.IO.Path.ChangeExtension(fbx, ".prefab"), name, levels.ToArray(), slots.ToArray());
        return Budget(name, levels.ToArray(), cap, 1024) + $" | {slots[0].Length} materials: {string.Join(", ", slots[0].Select(m => m != null ? m.name : "none"))}";
    }

    private static Material Scanned(KitMaterial kit)
    {
        string prefix = KitTextures + kit.Scan;
        Texture(prefix + "_diff.jpg", TextureImporterType.Default, true, kit.Size);
        Texture(prefix + "_nor_gl.jpg", TextureImporterType.NormalMap, false, kit.Size);
        Texture(prefix + "_arm.jpg", TextureImporterType.Default, false, kit.Size / 2);
        Texture(prefix + "_disp.png", TextureImporterType.Default, false, kit.Size / 2);

        Material material = Begin(Kit + kit.Slot + ".mat");
        material.SetTexture("_BaseMap", Load<Texture2D>(prefix + "_diff.jpg"));
        material.SetColor("_BaseColor", kit.Tint);
        material.SetTexture("_BumpMap", Load<Texture2D>(prefix + "_nor_gl.jpg"));
        // Poly Haven's "arm" is occlusion, roughness, metallic: Layered Lit's ORM as it comes.
        material.SetTexture("_ORMMap", Load<Texture2D>(prefix + "_arm.jpg"));
        material.EnableKeyword("_ORMMAP");
        material.SetFloat("_Roughness", 1f);
        material.SetFloat("_Metallic", 1f);
        if (kit.Metallic >= 0f)
        {
            // Wrought iron: the rust scan's colour and bumps, one metallic value (its own map says "no metal": it is rust).
            material.SetTexture("_ORMMap", null);
            material.DisableKeyword("_ORMMAP");
            material.SetTexture("_MetallicGlossMap", null);
            material.SetFloat("_Metallic", kit.Metallic);
            material.SetFloat("_Smoothness", 0.35f);
        }

        Parallax(material, Load<Texture2D>(prefix + "_disp.png"), kit.Parallax);
        Moss(material, null, kit.Moss, 1f);
        return Finish(material);
    }

    /// <summary>The sod roof: the meadow's own grass at the meadow's own size (the kit repeats it every 2.5 m, the
    /// ground every 4), toned to the olive the ground has under its blades, with drier patches so it is not a lawn.</summary>
    private static Material Turf()
    {
        Material material = Begin(Kit + "Kit Turf.mat");
        material.SetTexture("_BaseMap", Load<Texture2D>(MeadowGrass + "_Color.jpg"));
        material.SetTextureScale("_BaseMap", new Vector2(0.625f, 0.625f));
        material.SetColor("_BaseColor", new Color(0.95f, 0.8f, 0.62f));
        material.SetTexture("_BumpMap", Load<Texture2D>(MeadowGrass + "_Normal.jpg"));
        material.SetFloat("_BumpScale", 1.5f);
        material.SetTexture("_ORMMap", null);
        material.DisableKeyword("_ORMMAP");
        material.SetTexture("_MetallicGlossMap", null);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.06f);
        Parallax(material, null, 0f);
        Moss(material, null, 0.3f, 2f);
        material.SetFloat("_L1Upward", 0f);
        material.SetFloat("_L1Patchiness", 0.9f);
        material.SetColor("_L1Tint", new Color(1.1f, 0.95f, 0.5f)); // the moss, turned to dry grass
        material.SetFloat("_L1Sharpness", 1.5f);
        return Finish(material);
    }

    // ----- Shared -----

    private static void Texture(string path, TextureImporterType type, bool colour, int size)
    {
        if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
        {
            return;
        }

        if (importer.textureType == type && importer.sRGBTexture == colour && importer.maxTextureSize == size && importer.streamingMipmaps)
        {
            return;
        }

        importer.textureType = type;
        importer.sRGBTexture = colour;
        importer.maxTextureSize = size;
        importer.mipmapEnabled = true;
        importer.streamingMipmaps = true;
        importer.SaveAndReimport();
    }

    /// <summary>Meshes only; with <paramref name="materials"/>, the file's material names are mapped to them.</summary>
    private static void Model(string path, Dictionary<string, Material> materials)
    {
        var model = (ModelImporter)AssetImporter.GetAtPath(path);
        model.materialImportMode = materials == null ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        if (materials != null)
        {
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (KeyValuePair<string, Material> pair in materials)
            {
                model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
            }
        }

        model.importAnimation = false;
        model.animationType = ModelImporterAnimationType.None;
        model.importCameras = false;
        model.importLights = false;
        model.importBlendShapes = false;
        model.isReadable = false;
        model.SaveAndReimport();
    }

    /// <summary>A Layered Lit material at <paramref name="path"/>, started from the Haven's scanned field stone so it
    /// has the same defaults as the other rocks and props; plain (one layer, no maps) until the caller fills it.</summary>
    private static Material Begin(string path)
    {
        Material scan = AssetDatabase.LoadAllAssetsAtPath(FieldStone).OfType<Material>().First();
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

        material.name = System.IO.Path.GetFileNameWithoutExtension(path);
        material.SetTextureScale("_BaseMap", Vector2.one);
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_BumpScale", 1f);
        material.SetFloat("_OcclusionStrength", 1f);
        material.SetFloat("_L2Amount", 0f);
        material.SetTexture("_EmissionMap", null);
        material.SetColor("_EmissionColor", Color.black);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        // Wood, stone, iron and sod all get wet in the rain and take snow (one keyword in Layered Lit does both).
        material.SetFloat("_SeasonSnow", 1f);
        material.EnableKeyword("_SEASON_SNOW");
        return material;
    }

    private static Material Finish(Material material)
    {
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void Parallax(Material material, Texture2D height, float depth)
    {
        bool on = depth > 0f && height != null;
        material.SetTexture("_ParallaxMap", on ? height : null);
        material.SetFloat("_Parallax", on ? depth : 0f);
        if (on)
        {
            material.EnableKeyword("_PARALLAXMAP");
        }
        else
        {
            material.DisableKeyword("_PARALLAXMAP");
        }
    }

    /// <summary>Moss in patches on faces to the sky: from the baked coverage where the model has one, else everywhere
    /// the patches fall (a tiling material has no mask of its own).</summary>
    private static void Moss(Material material, Texture2D mask, float amount, float patchScale)
    {
        material.SetTexture("_BlendMask", mask);
        material.SetFloat("_L1Amount", amount);
        material.SetFloat("_L1Source", mask != null ? 3f : 4f);
        material.SetFloat("_L1Upward", mask != null ? 0.3f : 0.6f);
        material.SetFloat("_L1Patchiness", mask != null ? 0.5f : 0.8f);
        material.SetFloat("_L1PatchScale", patchScale);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    private static int TriangleCount(Mesh mesh)
    {
        long indices = 0;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            indices += mesh.GetIndexCount(i);
        }

        return (int)(indices / 3);
    }

    /// <summary>A generated look's levels: Blender's (decimated within the UV islands, so the texture stays put), or,
    /// where Blender couldn't reduce LOD1 to half of LOD0 or LOD2 to a quarter, Unity's mesh LOD generator, saved in the
    /// asset's LODs folder (reused, so references keep their GUIDs).</summary>
    private static Mesh[] Levels(string folder, string fbx, string look)
    {
        Mesh lod0 = MeshIn(fbx, look);
        Mesh[] blender = { lod0, MeshIn(fbx, look + " LOD1"), MeshIn(fbx, look + " LOD2") };
        int triangles = TriangleCount(lod0);
        if (blender[1] != null && blender[2] != null &&
            TriangleCount(blender[1]) <= triangles * 0.5f && TriangleCount(blender[2]) <= triangles * 0.25f)
        {
            return blender;
        }

        string lodFolder = folder + "LODs";
        if (!AssetDatabase.IsValidFolder(lodFolder))
        {
            AssetDatabase.CreateFolder(folder.TrimEnd('/'), "LODs");
        }

        var levels = new List<Mesh> { lod0 };
        foreach ((float share, string suffix) in new[] { (0.35f, " LOD1"), (0.1f, " LOD2") })
        {
            Mesh lighter = ModelSimplifier.Lighter(lod0, share);
            lighter.name = lod0.name + suffix;
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

    /// <summary>A model prefab that is the look itself: the LODGroup on its root.</summary>
    private static GameObject LookPrefab(string path, string name, Mesh[] levels, Material[][] materials)
    {
        bool exists = Load<GameObject>(path) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            AddLevels(root, levels, materials);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            if (exists)
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            else
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    /// <summary>A LODGroup on <paramref name="look"/> over its levels: hard switches, heights from the game's own view
    /// (LOD0 holds over the whole top-down view), the last level casting no shadow.</summary>
    private static void AddLevels(GameObject look, Mesh[] levels, Material[][] materials)
    {
        float[] heights = ScreenBudget.LodHeights(levels[0].bounds, levels.Select(TriangleCount).ToArray());
        var lods = new LOD[levels.Length];
        for (int i = 0; i < levels.Length; i++)
        {
            var part = new GameObject($"LOD{i}");
            part.transform.SetParent(look.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = levels[i];
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials[Mathf.Min(i, materials.Length - 1)];
            bool shadow = i < levels.Length - 1 || levels.Length == 1;
            renderer.shadowCastingMode = shadow ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            lods[i] = new LOD(i == levels.Length - 1 ? heights[heights.Length - 1] : heights[Mathf.Min(i, heights.Length - 1)], new Renderer[] { renderer });
        }

        LODGroup group = look.TryGetComponent(out LODGroup existing) ? existing : look.AddComponent<LODGroup>();
        group.SetLODs(lods);
        group.fadeMode = LODFadeMode.None;
        group.RecalculateBounds();
    }

    /// <summary>The numbers the asset budgets ask for: triangles against what the top-down view can show, vertices a
    /// triangle, how far each LOD reduces, the size on the ground.</summary>
    private static string Budget(string name, Mesh[] levels, int cap, int textureSize)
    {
        Bounds bounds = levels[0].bounds;
        int triangles = TriangleCount(levels[0]);
        string lods = string.Join(" / ", levels.Select(m => $"{TriangleCount(m)} tris {m.vertexCount} verts"));
        float[] heights = ScreenBudget.LodHeights(bounds, levels.Select(TriangleCount).ToArray());
        return $"{name}: {bounds.size.x:0.00} x {bounds.size.y:0.00} x {bounds.size.z:0.00} m | {lods} | {(float)levels[0].vertexCount / triangles:0.00} verts/tri | " +
               $"view budget {ScreenBudget.ViewTriangles(bounds, cap)} (cap {cap}) | LOD heights {string.Join(", ", heights.Select(h => h.ToString("0.000")))} | " +
               $"textures {textureSize}";
    }
}
