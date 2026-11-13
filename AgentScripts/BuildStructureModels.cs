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
/// - generated, for stations and props (LocalSpawn: Tools/LocalSpawn/generate_gathering.py --group structures, made
///   game-ready by Tools/Blender/ingest_generated.py): an FBX of "&lt;Asset&gt; A" with "LOD1", "LOD2" and one baked
///   texture set;
/// - kit-built, for buildings (Tools/Blender/building_kit.py and a script per building, e.g. smithy.py): an FBX of
///   "&lt;Asset&gt;", "LOD1", "LOD2" and "Tier N" parts whose faces use the kit's tiling scanned materials
///   (Art/Haven/Kit/Textures; "Kit Wood", "Kit Planks", ...), so the textures stay sharp on a building of any size
///   and all buildings share them.
///
/// So far: the Campfire's stones and logs (its flame and light stay), the Small Chest (its own model in place of the
/// Storage Shed's scaled-down one, which the Storage Shed keeps), and the Smithy: its model prefab
/// (Art/Haven/Models/Smithy Model.prefab, which the Smithy's building prefab holds) becomes the kit model with a
/// "Tier N" child for what each upgrade adds, and the building's collider and navigation size are fitted again.
/// AgentScripts/BuildBaseTiers.cs keeps such a model when it runs. Assets whose FBX hasn't been made are skipped.
/// Re-runnable.
///
/// Sod roofs are modelled bare and get the meadow's own grass here (RoofGrass): clumps of the terrain's grass
/// scattered over the roof and merged into one mesh, drawn by the grass shader's merged-clumps variant, so a roof
/// has the ground's colour, wind, seasons and snow.
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
    private const string DarkHumus = "Assets/_Project/Art/Terrain/World/Dark Humus/Dark Humus";
    // The terrain's grass clump that grows on roofs, and how many of them a square metre of roof gets.
    // (The Haven's meadow is painted almost wholly with this clump.)
    private const string GrassClump = "Assets/_Project/Art/Vegetation/Safe Haven/Details/Short Grass.prefab";
    private const float GrassPerSquareMetre = 6f;
    private const string Models = "Assets/_Project/Art/Haven/Models/";
    private const string Tints = "Assets/_Project/Materials/Haven/";

    // Kit-built buildings: the asset under Art/Haven/Structures, the model prefab it becomes (the one the building's
    // prefab holds), the building's definition under Data/Buildings, and the most triangles the view can show of it.
    private static readonly (string name, string model, string building, int cap)[] KitBuildings =
    {
        ("Smithy", "Smithy Model", "Forge", 16000),
        ("Workshop", "Workshop Model", "ConstructionTable", 16000),
        ("Woodcutters Lodge", "Woodcutters Lodge Model", "Sawbench", 16000),
        ("Hunters Lodge", "Hunters Lodge Model", "TanningRack", 16000),
        ("Tailors Shop", "Tailors Shop Model", "Loom", 16000),
        ("Trading Post", "Trading Post Model", "TradingPost", 16000),
        ("Cottage", "Cottage Model", "Cottage", 12000),
        ("Hut", "Hut Model", "Hut", 10000),
        ("Sauna", "Sauna Model", "Sauna", 10000),
        ("Dock", "Dock Model", "Dock", 10000),
        ("Stable", "Stable Model", "Stable", 16000),
        ("Wainwrights Yard", "WainwrightsYard Model", "WainwrightsYard", 10000),
    };

    // What a kit building's model prefab holds besides the kit model, put back after ModelWithTiers clears it: the
    // Stable's paddock of the Haven's own skigard fence before its open front (BuildHorses keeps its horses at
    // stall points under the roof and a door point at z 5.2, inside the paddock's 2.3 m gate).
    private static readonly Dictionary<string, (Vector3 at, float yaw)[]> Fences = new Dictionary<string, (Vector3, float)[]>
    {
        ["Stable"] = new[]
        {
            (new Vector3(-5.3f, 0f, 4.2f), 90f), (new Vector3(5.3f, 0f, 4.2f), 90f),
            (new Vector3(-3.2f, 0f, 6.25f), 0f), (new Vector3(3.2f, 0f, 6.25f), 0f),
        },
    };

    // Where a lived-in building's smoke rises (its chimney's top) and where its window glows at night, in the
    // building's own space (HearthSmoke on its prefab).
    private static readonly Dictionary<string, (Vector3 chimney, Vector3 light)> Hearths = new Dictionary<string, (Vector3, Vector3)>
    {
        ["Cottage"] = (new Vector3(-1.25f, 4.05f, -0.55f), new Vector3(-1.15f, 1.4f, 2.0f)),
        ["Hut"] = (new Vector3(0.8f, 3.5f, -0.35f), new Vector3(-0.45f, 1.0f, 1.7f)),
    };

    // What small things and a tier's parts are made of: the kit's slot and the game's tint material. One with a
    // colour is made from Base Cloth when it isn't there yet (dyed cloth).
    private static readonly (string slot, string tint, Color make)[] TierTints =
    {
        ("Kit Copper", "Base Copper", Color.clear), ("Kit Moonsilver", "Base Moonsilver", Color.clear),
        ("Kit Trollstone", "Base Trollstone", Color.clear), ("Kit Cloth", "Base Cloth", Color.clear),
        ("Kit Water", "Base Water", Color.clear), ("Kit Linen", "Base Linen", Color.clear), ("Kit Hide", "Base Hide", Color.clear),
        ("Kit Dye Blue", "Base Dye Blue", new Color(0.16f, 0.24f, 0.36f)), ("Kit Dye Ochre", "Base Dye Ochre", new Color(0.6f, 0.42f, 0.14f)),
    };

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
        // Field stones one by one (footings, chimneys, forges): the gathering rocks' pale scan. No moss: a tiling
        // material's moss falls everywhere, the forge's stones under the roof included.
        new KitMaterial { Slot = "Kit Boulder", Scan = "rock_boulder_dry", Tint = new Color(0.64f, 0.66f, 0.7f), Parallax = 0.012f },
        // Birch bark under the sod, white along the eaves and gables: the plank scan, bleached.
        new KitMaterial { Slot = "Kit Birch", Scan = "weathered_planks", Tint = new Color(2.1f, 2.0f, 1.82f) },
        // Hay and straw (stalls, racks, heaps): no scan exists; Tools/LocalSpawn/kit_textures.py makes the set.
        new KitMaterial { Slot = "Kit Hay", Scan = "hay", Tint = new Color(0.95f, 0.9f, 0.82f), Parallax = 0.008f },
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

        // The pilot's two candidate Smithies; the kit one was chosen and is now "Smithy".
        foreach (string candidate in new[] { "Smithy Generated", "Smithy Kit" })
        {
            if (AssetDatabase.IsValidFolder(Art + candidate))
            {
                AssetDatabase.DeleteAsset(Art + candidate);
            }
        }

        report.AddRange(Kits(KitBuildings.Select(kit => kit.name)));
        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    /// <summary>Only the Dock, the Stable and the Wainwright's Yard (`--entry BuildStructureModels.DockStableYard`),
    /// kit-built 2026-11-13, without touching the other buildings.</summary>
    public static string DockStableYard()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        List<string> report = Kits(new[] { "Dock", "Stable", "Wainwrights Yard" });
        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    private static List<string> Kits(IEnumerable<string> names)
    {
        var report = new List<string>();
        var wanted = new HashSet<string>(names);
        foreach ((string name, string model, string building, int cap) in KitBuildings)
        {
            if (wanted.Contains(name) && Load<GameObject>(Art + name + "/" + name + ".fbx") != null)
            {
                report.Add(KitBuilding(name, cap, Models + model + ".prefab", building));
                if (Fences.TryGetValue(name, out (Vector3 at, float yaw)[] fences))
                {
                    report.Add(Fence(Models + model + ".prefab", fences));
                }
            }
        }

        return report;
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

    private static string KitBuilding(string name, int cap, string modelPrefab, string building)
    {
        string fbx = Art + name + "/" + name + ".fbx";
        var materials = new Dictionary<string, Material>();
        foreach ((string slot, string tint, Color make) in TierTints)
        {
            var material = Load<Material>(Tints + tint + ".mat");
            if (material == null && make.a > 0f)
            {
                material = new Material(Load<Material>(Tints + "Base Cloth.mat")) { name = tint };
                material.SetColor("_BaseColor", make);
                material.SetColor("_Color", make);
                AssetDatabase.CreateAsset(material, Tints + tint + ".mat");
            }

            if (material != null)
            {
                materials[slot] = material;
            }
        }

        foreach (KitMaterial kit in KitMaterials)
        {
            materials[kit.Slot] = Scanned(kit);
        }

        materials["Kit Turf"] = Turf();
        materials["Kit Soil"] = Soil();
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

        // Grass on the sod: the faces of the roof's own material.
        Material[] first = slots[0];
        (Mesh grass, Material grassMaterial) = RoofGrass(Art + name + "/", name, levels[0],
            (sub, centre, uv) => sub < first.Length && first[sub] != null && first[sub].name == "Kit Turf");
        GameObject look = LookPrefab(System.IO.Path.ChangeExtension(fbx, ".prefab"), name, levels.ToArray(), slots.ToArray(), grass, grassMaterial);

        // What each tier adds, and the model prefab the building holds.
        var tiers = new List<(int tier, Mesh mesh, Material[] materials)>();
        for (int tier = 2; tier <= 9; tier++)
        {
            Transform part = model.transform.Find($"{name} Tier {tier}");
            if (part != null)
            {
                tiers.Add((tier, part.GetComponent<MeshFilter>().sharedMesh, part.GetComponent<MeshRenderer>().sharedMaterials));
            }
        }

        string placed = "";
        if (Load<GameObject>(modelPrefab) != null)
        {
            ModelWithTiers(modelPrefab, look, tiers);
            var definition = Load<BuildingDefinition>("Assets/_Project/Data/Buildings/" + building + ".asset");
            if (definition != null && definition.Prefab != null)
            {
                BuildingPrefabBuilder.Update(definition.Prefab, definition);
                Bounds body = levels[0].bounds;
                PrefabBuildUtility.EditPrefab(definition.Prefab, root =>
                {
                    // The kit model is life-size (the Workshop's stand-in was shown at 0.7).
                    var modelAsset = Load<GameObject>(modelPrefab);
                    foreach (Transform child in root.transform)
                    {
                        if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == modelAsset)
                        {
                            child.localScale = Vector3.one;
                        }
                    }

                    // The washing line of a lived-in house stands behind the house, and is no part of its collider.
                    Transform lived = root.transform.Find("Lived In");
                    Transform line = lived != null ? lived.Find("Line") : null;
                    if (line != null)
                    {
                        lived.localPosition += new Vector3(0f, 0f, body.min.z - 1.3f - (lived.localPosition.z + line.localPosition.z));
                    }

                    if (lived != null)
                    {
                        lived.SetParent(null, false);
                    }

                    PrefabBuildUtility.FitToModel(root); // the collider and navigation size of the stand-in
                    if (lived != null)
                    {
                        lived.SetParent(root.transform, false);
                    }
                });
                if (Hearths.TryGetValue(name, out (Vector3 chimney, Vector3 light) hearth))
                {
                    PrefabBuildUtility.EditPrefab(definition.Prefab, root =>
                    {
                        if (root.TryGetComponent(out HearthSmoke smoke))
                        {
                            var data = new SerializedObject(smoke);
                            data.FindProperty("chimney").vector3Value = hearth.chimney;
                            data.FindProperty("lightAt").vector3Value = hearth.light;
                            data.ApplyModifiedPropertiesWithoutUndo();
                        }
                    });
                }

                // A kit building's front is +Z: that is where its yard lies.
                var yard = new SerializedObject(definition);
                if (yard.FindProperty("yardSide").enumValueIndex != (int)YardSide.Front)
                {
                    yard.FindProperty("yardSide").enumValueIndex = (int)YardSide.Front;
                    yard.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(definition);
                }
                placed = $" | in {System.IO.Path.GetFileName(AssetDatabase.GetAssetPath(definition.Prefab))}, tiers {string.Join(" ", tiers.Select(t => t.tier))}";
            }
        }

        return Budget(name, levels.ToArray(), cap, 1024) + GrassLine(grass) + placed +
               $" | {slots[0].Length} materials: {string.Join(", ", slots[0].Select(m => m != null ? m.name : "none"))}";
    }

    // Skigard pieces in a model prefab (the fence's own model and collider, as the Skigard prefab has them).
    private static string Fence(string modelPrefab, (Vector3 at, float yaw)[] fences)
    {
        Transform fence = Load<GameObject>("Assets/_Project/Prefabs/Structures/Skigard.prefab").transform.Find("Model");
        GameObject root = PrefabUtility.LoadPrefabContents(modelPrefab);
        try
        {
            foreach ((Vector3 at, float yaw) in fences)
            {
                var made = Object.Instantiate(fence.gameObject, root.transform);
                made.name = "Skigard";
                made.transform.localPosition = at;
                made.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                made.transform.localScale = fence.localScale;
            }

            PrefabUtility.SaveAsPrefabAsset(root, modelPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return $"{System.IO.Path.GetFileNameWithoutExtension(modelPrefab)}: {fences.Length} skigard pieces";
    }

    /// <summary>The model prefab a building's prefab holds, remade as the kit model's look and one switched-off
    /// "Tier N" child for each upgrade's parts (Construction switches them on).</summary>
    private static void ModelWithTiers(string path, GameObject look, List<(int tier, Mesh mesh, Material[] materials)> tiers)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            PrefabUtility.InstantiatePrefab(look, root.transform);
            foreach ((int tier, Mesh mesh, Material[] materials) in tiers)
            {
                var part = new GameObject("Tier " + tier);
                part.transform.SetParent(root.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = materials;
                part.SetActive(false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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

    /// <summary>The sod under a roof's grass: the meadow's own ground at the meadow's own size (4 m a repeat, as the
    /// kit maps it), toned to the olive the ground has under its blades.</summary>
    private static Material Turf()
    {
        Material material = Begin(Kit + "Kit Turf.mat");
        material.SetTexture("_BaseMap", Load<Texture2D>(MeadowGrass + "_Color.jpg"));
        material.SetColor("_BaseColor", new Color(0.8f, 0.68f, 0.52f));
        material.SetTexture("_BumpMap", Load<Texture2D>(MeadowGrass + "_Normal.jpg"));
        material.SetFloat("_BumpScale", 1.5f);
        material.SetTexture("_ORMMap", null);
        material.DisableKeyword("_ORMMAP");
        material.SetTexture("_MetallicGlossMap", null);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.06f);
        Parallax(material, null, 0f);
        Moss(material, null, 0f, 1f);
        return Finish(material);
    }

    /// <summary>Bare earth: the cut edge of a sod roof, a smithy's floor. The forest's dark humus.</summary>
    private static Material Soil()
    {
        Material material = Begin(Kit + "Kit Soil.mat");
        material.SetTexture("_BaseMap", Load<Texture2D>(DarkHumus + "_Color.jpg"));
        material.SetTexture("_BumpMap", Load<Texture2D>(DarkHumus + "_Normal.jpg"));
        material.SetFloat("_BumpScale", 1.5f);
        material.SetTexture("_ORMMap", null);
        material.DisableKeyword("_ORMMAP");
        material.SetTexture("_MetallicGlossMap", null);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.08f);
        Parallax(material, null, 0f);
        Moss(material, null, 0f, 1f);
        return Finish(material);
    }

    // ----- Roof grass -----

    /// <summary>The meadow's grass on a sod roof: clumps of the terrain's grass detail scattered over the roof's
    /// faces that look to the sky (and that <paramref name="grows"/> accepts: submesh, face centre, face UV), merged
    /// into one mesh saved beside the model. Each vertex carries its clump's root in UV1; the grass shader's
    /// merged-clumps variant (_BAKED_ROOTS) sways, colours and thins every clump about that root, as it does a
    /// terrain detail about its instance. The material is the terrain clump's own, with that variant on.</summary>
    private static (Mesh, Material) RoofGrass(string folder, string name, Mesh roof, System.Func<int, Vector3, Vector2, bool> grows,
        float perSquareMetre = GrassPerSquareMetre)
    {
        var prefab = Load<GameObject>(GrassClump);
        Mesh clump = prefab.GetComponentInChildren<MeshFilter>().sharedMesh;
        Material meadow = prefab.GetComponentInChildren<MeshRenderer>().sharedMaterial;

        Vector3[] roofVertices = roof.vertices;
        Vector2[] roofUv = roof.uv;
        var random = new System.Random(7);
        var roots = new List<Vector3>();
        for (int sub = 0; sub < roof.subMeshCount; sub++)
        {
            int[] triangles = roof.GetTriangles(sub);
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = roofVertices[triangles[i]], b = roofVertices[triangles[i + 1]], c = roofVertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                float area = normal.magnitude * 0.5f;
                if (area < 1e-6f || normal.y / (area * 2f) < 0.5f)
                {
                    continue;
                }

                Vector2 uv = roofUv.Length > 0 ? (roofUv[triangles[i]] + roofUv[triangles[i + 1]] + roofUv[triangles[i + 2]]) / 3f : Vector2.zero;
                if (!grows(sub, (a + b + c) / 3f, uv))
                {
                    continue;
                }

                float expected = area * perSquareMetre;
                int count = (int)expected + (random.NextDouble() < expected - (int)expected ? 1 : 0);
                for (int k = 0; k < count; k++)
                {
                    float u = (float)random.NextDouble(), v = (float)random.NextDouble();
                    if (u + v > 1f)
                    {
                        u = 1f - u;
                        v = 1f - v;
                    }

                    roots.Add(a + (b - a) * u + (c - a) * v);
                }
            }
        }

        Vector3[] clumpVertices = clump.vertices;
        Vector3[] clumpNormals = clump.normals;
        Color[] clumpColours = clump.colors;
        Vector2[] clumpUv = clump.uv;
        int[] clumpTriangles = clump.triangles;
        int per = clumpVertices.Length;
        var vertices = new List<Vector3>(roots.Count * per);
        var normals = new List<Vector3>(roots.Count * per);
        var colours = new List<Color>(roots.Count * per);
        var uvs = new List<Vector2>(roots.Count * per);
        var rootUvs = new List<Vector3>(roots.Count * per);
        var indices = new List<int>(roots.Count * clumpTriangles.Length);
        foreach (Vector3 root in roots)
        {
            Quaternion turn = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
            float scale = Mathf.Lerp(0.8f, 1.25f, (float)random.NextDouble());
            float phase = (float)random.NextDouble();
            int first = vertices.Count;
            for (int i = 0; i < per; i++)
            {
                vertices.Add(root + turn * (clumpVertices[i] * scale));
                normals.Add(turn * clumpNormals[i]);
                Color colour = i < clumpColours.Length ? clumpColours[i] : Color.white;
                colour.g = Mathf.Repeat(colour.g + phase, 1f); // each blade's own number: its sway and its shade
                colours.Add(colour);
                uvs.Add(i < clumpUv.Length ? clumpUv[i] : Vector2.zero);
                rootUvs.Add(root);
            }

            foreach (int index in clumpTriangles)
            {
                indices.Add(first + index);
            }
        }

        var mesh = new Mesh { name = name + " Roof Grass" };
        mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetColors(colours);
        mesh.SetUVs(0, uvs);
        mesh.SetUVs(1, rootUvs);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();
        Bounds bounds = mesh.bounds;
        bounds.Expand(0.3f); // the wind moves the blades (kept small: a building's footprint is its meshes' bounds)
        mesh.bounds = bounds;
        mesh.UploadMeshData(true);

        string meshPath = folder + mesh.name + ".asset";
        var saved = Load<Mesh>(meshPath);
        if (saved != null)
        {
            EditorUtility.CopySerialized(mesh, saved);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
        }
        else
        {
            AssetDatabase.CreateAsset(mesh, meshPath);
            saved = mesh;
        }

        string materialPath = Kit + "Roof Grass.mat";
        var material = Load<Material>(materialPath);
        if (material == null)
        {
            material = new Material(meadow);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = meadow.shader;
            material.CopyPropertiesFromMaterial(meadow);
        }

        material.name = "Roof Grass";
        material.SetFloat("_BakedRoots", 1f);
        material.EnableKeyword("_BAKED_ROOTS");
        material.enableInstancing = false; // one mesh per roof: there is nothing to instance
        EditorUtility.SetDirty(material);
        return (saved, material);
    }

    private static string GrassLine(Mesh grass) =>
        grass == null ? "" : $" | roof grass {TriangleCount(grass)} tris {grass.vertexCount} verts (one draw; the shader thins it with distance)";

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
    private static GameObject LookPrefab(string path, string name, Mesh[] levels, Material[][] materials,
        Mesh grass = null, Material grassMaterial = null)
    {
        bool exists = Load<GameObject>(path) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            AddLevels(root, levels, materials, grass, grassMaterial);
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
    /// (LOD0 holds over the whole top-down view), the last level casting no shadow. Roof grass, where there is any,
    /// is drawn with every level but the last.</summary>
    private static void AddLevels(GameObject look, Mesh[] levels, Material[][] materials, Mesh grass = null, Material grassMaterial = null)
    {
        MeshRenderer grassRenderer = null;
        if (grass != null && grass.vertexCount > 0)
        {
            var part = new GameObject("Roof Grass");
            part.transform.SetParent(look.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = grass;
            grassRenderer = part.AddComponent<MeshRenderer>();
            grassRenderer.sharedMaterial = grassMaterial;
            grassRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

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
            bool grassy = grassRenderer != null && (i < levels.Length - 1 || levels.Length == 1);
            lods[i] = new LOD(i == levels.Length - 1 ? heights[heights.Length - 1] : heights[Mathf.Min(i, heights.Length - 1)],
                grassy ? new Renderer[] { renderer, grassRenderer } : new Renderer[] { renderer });
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
