#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
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
        public string Folder = Art;    // where ingest_generated.py baked it: the manifest's "art" folder
        public int TextureSize = 2048; // the import's size; the baked files may be larger
        public float Parallax;
        public float Moss;
        public Color Tint = Color.white; // over the baked colour, where it sits too bright or too green beside the ground
        public Color Glow = Color.black; // HDR strength of the baked emission (runes); black for none
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

    private static bool Ready(Generated asset) => Load<GameObject>(asset.Folder + asset.Name + "/" + asset.Name + ".fbx") != null;

    private static (Mesh[], Material) Setup(Generated asset)
    {
        string folder = asset.Folder + asset.Name + "/";
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
        if (asset.Glow.maxColorComponent > 0f)
        {
            // Runes glow from the emission ingest_generated.py made from the albedo (the manifest's "glow").
            Texture(prefix + "_Emission.png", TextureImporterType.Default, true, asset.TextureSize / 2);
            var glow = Load<Texture2D>(prefix + "_Emission.png");
            if (glow != null)
            {
                material.SetTexture("_EmissionMap", glow);
                material.SetColor("_EmissionColor", asset.Glow);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

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

    // ----- Placeholders: the last primitive stand-ins -----

    // The user (2026-11-14): "look for primitives and replace them with models from LocalSpawn; generate them there and
    // optimise in Blender". Generated by Tools/LocalSpawn/generate_gathering.py (--group structures for the Haven's,
    // --group props for the raids' and the items'), made game-ready by ingest_generated.py, put in their prefabs here
    // (`--entry BuildStructureModels.Placeholders`). What the stand-ins carried that is not a stand-in stays: flames,
    // embers, orbs, beacons and eyes (materials named so), lights and particles (moved off a stand-in before it goes),
    // colliders (a stand-in with one keeps its object and loses only its mesh). Each swap runs once: its stand-ins are
    // gone afterwards, so to place a look again, take the prefab back from git first.
    private const string PropArt = "Assets/_Project/Art/World/Props/";
    private const string Project = "Assets/_Project/";
    private static readonly Color RuneGlow = new Color(0.55f, 1.1f, 2.6f); // the world props' blue runes
    private static readonly Color AltarGlow = new Color(0.6f, 1.7f, 0.75f); // the Life Orbs' green

    private static readonly Generated[] PlaceholderLooks =
    {
        // The Haven (structures_manifest.json, Art/Haven/Structures).
        new Generated { Name = "Kitchen", Cap = 7000, Parallax = 0.01f, Moss = 0.1f },
        new Generated { Name = "Well", Cap = 6000, Parallax = 0.01f, Moss = 0.2f },
        new Generated { Name = "Watch Post", Cap = 9000, Parallax = 0.006f, Moss = 0.1f },
        new Generated { Name = "Storage Shed", Cap = 9000, Parallax = 0.006f, Moss = 0.1f },
        new Generated { Name = "Furnace", Cap = 5000, Parallax = 0.012f, Moss = 0.05f },
        new Generated { Name = "Purification Altar", Cap = 5000, Parallax = 0.012f, Moss = 0.25f, Glow = AltarGlow },
        new Generated { Name = "Standing Torch", TextureSize = 1024, Cap = 2000, Parallax = 0.004f },
        new Generated { Name = "Lantern Post", TextureSize = 1024, Cap = 2500, Parallax = 0.004f },
        new Generated { Name = "Haven Banner", Cap = 3000, Parallax = 0.003f },
        new Generated { Name = "Herb Planter", Cap = 4000, Parallax = 0.006f },
        new Generated { Name = "Beehive", TextureSize = 1024, Cap = 3000, Parallax = 0.008f },
        new Generated { Name = "Ward Totem", Cap = 4000, Parallax = 0.012f, Moss = 0.25f, Glow = RuneGlow },
        new Generated { Name = "Spike Trap", Cap = 3000, Parallax = 0.006f },
        new Generated { Name = "Iron Trap", Cap = 3000, Parallax = 0.006f },
        new Generated { Name = "Festival Bonfire", Cap = 6000, Parallax = 0.008f },
        new Generated { Name = "Training Dummy", TextureSize = 1024, Cap = 3000, Parallax = 0.004f },
        new Generated { Name = "Armored Dummy", TextureSize = 1024, Cap = 3500, Parallax = 0.004f },
        new Generated { Name = "Farm Bed", Cap = 4000, Parallax = 0.01f },
        // Wall pieces, 4 m each, laid end to end along a line (their colliders stay the stand-ins' boxes).
        new Generated { Name = "Palisade", Cap = 6000, Parallax = 0.008f, Moss = 0.15f },
        new Generated { Name = "Stone Wall", Cap = 6000, Parallax = 0.012f, Moss = 0.3f },
        // The raids and the items (props_manifest.json, Art/World/Props).
        new Generated { Name = "Cache Crate", Folder = PropArt, TextureSize = 1024, Cap = 3000, Parallax = 0.004f, Moss = 0.15f },
        new Generated { Name = "Cache Lid", Folder = PropArt, TextureSize = 1024, Cap = 800, Parallax = 0.003f, Moss = 0.15f },
        new Generated { Name = "Extraction Stone", Folder = PropArt, Cap = 5000, Parallax = 0.012f, Moss = 0.3f },
        // No glow: the emission caught the pale stone itself, not only its runes (the stone has its own light).
        new Generated { Name = "Waystone", Folder = PropArt, Cap = 2500, Parallax = 0.012f, Moss = 0.25f },
        new Generated { Name = "Burnt Longhouse", Folder = PropArt, TextureSize = 4096, Cap = 16000, Parallax = 0.008f, Moss = 0.15f },
        new Generated { Name = "Ruined Watchtower", Folder = PropArt, Cap = 12000, Parallax = 0.006f, Moss = 0.3f },
        new Generated { Name = "Old Bone", Folder = PropArt, TextureSize = 1024, Cap = 600, Parallax = 0.003f },
        new Generated { Name = "Hooded Stranger", Folder = PropArt, Cap = 4000, Parallax = 0.004f },
        new Generated { Name = "Standing Stone", Folder = PropArt, Cap = 2000, Parallax = 0.012f, Moss = 0.35f },
        new Generated { Name = "Hand Torch", Folder = PropArt, TextureSize = 1024, Cap = 1200, Parallax = 0.003f },
        new Generated { Name = "Fishing Rod", Folder = PropArt, TextureSize = 1024, Cap = 800 },
        new Generated { Name = "Shaman Staff", Folder = PropArt, TextureSize = 1024, Cap = 1500, Parallax = 0.004f, Moss = 0.2f },
    };

    // What is never a stand-in, by its material's name: the effects the stand-ins carried.
    private static readonly string[] Effects = { "Fire", "Ember", "Orb", "Beacon", "Beam", "Portal", "Eye", "Wisp", "Spirit" };

    private enum Fit
    {
        Footprint, // one look where the stand-ins stood together, at its own size unless told to fit
        EachPiece, // one look per stand-in: laid along a long one and stretched to its length, set on a round one
        Longest,   // one look along the longest stand-in (a rod, a staff); the others just go
    }

    /// <summary>One swap: in a prefab, under a holder, primitive stand-ins give way to a look.</summary>
    private sealed class Swap
    {
        public string Prefab;                  // under Assets/_Project/
        public string Holder = "";             // a child path, or a name found anywhere below; "" the root
        public string Look;                    // a generated asset's name, or a look prefab under Assets/_Project/ (".prefab")
        public Fit Fit = Fit.Footprint;
        public System.Func<Renderer, bool> Takes; // which stand-ins go (default: every one)
        public float Yaw;                      // the look turned about the vertical
        public float Scale = 1f;
        public bool FitHeight, FitWidth;       // scaled to the stand-ins' height or widest side
        public bool FitX, FitFloor;            // stretched to the stand-ins' length (a wall piece) or their ground (a trap)
        public bool Centre;                    // Footprint: centred on the stand-ins, not stood on their foot (a thrown stone)
        public Vector3? Around;                // Footprint: here instead of the stand-ins' middle (some had strayed)
        public bool Uniform;                   // EachPiece/Longest: scaled whole to a long piece's length, not stretched
        public bool Flip;                      // EachPiece/Longest: end for end
        public float Lift;                     // added to the height (the holder's units)
        public float FlameOnTop = -1f;         // >= 0: the kept effects stand on the look's top, this share of their height in it
        public string Refit;                   // a structure prefab whose collider and size follow the new look
        public bool Chimney;                   // its HearthSmoke rises from the look's highest point
    }

    private static Swap[] PlaceholderSwaps()
    {
        bool Named(Renderer r, string name) => r.name == name;
        bool Made(Renderer r, string material) => r.sharedMaterial != null && r.sharedMaterial.name == material;
        return new[]
        {
            // The Haven: the stations' and buildings' model prefabs (each building holds its own).
            new Swap { Prefab = "Art/Haven/Models/Kitchen Model.prefab", Look = "Kitchen", Refit = "Prefabs/Structures/Kitchen.prefab", Chimney = true },
            new Swap { Prefab = "Art/Haven/Models/Well Model.prefab", Look = "Well", Refit = "Prefabs/Structures/Well.prefab" },
            new Swap { Prefab = "Art/Haven/Models/WatchPost Model.prefab", Look = "Watch Post", Refit = "Prefabs/Structures/WatchPost.prefab" },
            new Swap { Prefab = "Environment/Stations/StorageShedModel.prefab", Look = "Storage Shed", Refit = "Prefabs/Structures/StorageShed.prefab" },
            new Swap { Prefab = "Environment/Stations/FurnaceModel.prefab", Look = "Furnace", Refit = "Prefabs/Structures/Furnace.prefab", Chimney = true },
            new Swap { Prefab = "Environment/Stations/PurificationAltarModel.prefab", Look = "Purification Altar", Refit = "Prefabs/Structures/PurificationAltar.prefab", FlameOnTop = 1.1f },
            new Swap { Prefab = "Environment/Stations/TorchModel.prefab", Look = "Standing Torch", Refit = "Prefabs/Structures/Torch.prefab", FlameOnTop = 0.6f },
            new Swap { Prefab = "Art/Haven/Models/LanternPost Model.prefab", Look = "Lantern Post", Refit = "Prefabs/Structures/LanternPost.prefab" },
            new Swap { Prefab = "Art/Haven/Models/HavenBanner Model.prefab", Look = "Haven Banner", Refit = "Prefabs/Structures/HavenBanner.prefab" },
            new Swap { Prefab = "Art/Haven/Models/HerbPlanter Model.prefab", Look = "Herb Planter", Refit = "Prefabs/Structures/HerbPlanter.prefab" },
            new Swap { Prefab = "Art/Haven/Models/Beehive Model.prefab", Look = "Beehive", Refit = "Prefabs/Structures/Beehive.prefab" },
            new Swap { Prefab = "Art/Haven/Models/Runic Ward Totem Model.prefab", Look = "Ward Totem", Refit = "Prefabs/Structures/RunicWardTotem.prefab" },
            new Swap { Prefab = "Art/Haven/Models/SpikeTrap Model.prefab", Look = "Spike Trap", FitFloor = true, Refit = "Prefabs/Structures/SpikeTrap.prefab" },
            // The farm's crops are the wild turnips' own model, small, then the bed under them.
            new Swap { Prefab = "Art/Haven/Models/FarmPlot Model.prefab", Look = "Art/Gathering/Wild Turnips/Wild Turnips B.prefab", Fit = Fit.EachPiece, FitWidth = true, Scale = 1.6f, Takes = r => Made(r, "Plant Leaf") },
            new Swap { Prefab = "Art/Haven/Models/FarmPlot Model.prefab", Look = "Farm Bed", Refit = "Prefabs/Structures/FarmPlot.prefab" },
            new Swap { Prefab = "Art/Haven/Models/ElderTree Model.prefab", Look = "Art/Gathering/Trees/Oak Wood C.prefab", FitHeight = true, Refit = "Prefabs/Structures/ElderTree.prefab" },
            new Swap { Prefab = "Prefabs/World/Living/Festival Bonfire.prefab", Holder = "Model", Look = "Festival Bonfire" },
            new Swap { Prefab = "Prefabs/Enemies/TrainingDummy.prefab", Look = "Training Dummy" },
            new Swap { Prefab = "Prefabs/Structures/Palisade.prefab", Look = "Palisade", FitX = true, Refit = "Prefabs/Structures/Palisade.prefab" },
            new Swap { Prefab = "Prefabs/Structures/Stone Wall.prefab", Look = "Stone Wall", FitX = true, Refit = "Prefabs/Structures/Stone Wall.prefab" },
            // The raids.
            new Swap { Prefab = "Prefabs/Raids/ExtractionPoint.prefab", Look = "Extraction Stone", Takes = r => Named(r, "Pad") },
            new Swap { Prefab = "Prefabs/Interactables/ExperienceStone.prefab", Look = "Waystone" },
            new Swap { Prefab = "Prefabs/Raids/Forward Camp.prefab", Look = "Art/Haven/Structures/Campfire/Campfire.prefab", Takes = r => Named(r, "Stone") },
            new Swap { Prefab = "Prefabs/Raids/Forward Camp.prefab", Look = "Standing Torch", Fit = Fit.EachPiece, Uniform = true, Takes = r => Named(r, "Timber") },
            new Swap { Prefab = "Prefabs/Raids/SetPieces/Burnt Farmstead.prefab", Look = "Burnt Longhouse", Around = new Vector3(0.6f, 0f, -5.5f), Scale = 1.4f },
            new Swap { Prefab = "Prefabs/Raids/SetPieces/Ruined Watchtower.prefab", Look = "Ruined Watchtower", Around = new Vector3(5.5f, 0f, 0f) },
            new Swap { Prefab = "Prefabs/Raids/SetPieces/Wolf Den.prefab", Look = "Old Bone", Fit = Fit.EachPiece },
            new Swap { Prefab = "Prefabs/Raids/SetPieces/Guardian's Hollow.prefab", Look = "Art/Vegetation/Misty Woods/Rocks and Logs/Rotten Log A.prefab", Fit = Fit.EachPiece },
            new Swap { Prefab = "Prefabs/Raids/SetPieces/Old Barrow.prefab", Look = "Art/Raids/Sites/Open Barrow/Open Barrow.prefab", Around = new Vector3(0f, 0f, -7.2f) },
            new Swap { Prefab = "Prefabs/Raids/Events/Sites/Crossroads Stranger.prefab", Holder = "Model", Look = "Hooded Stranger" },
            new Swap { Prefab = "Prefabs/Raids/Events/Sites/Fog Bank.prefab", Holder = "Model", Look = "Standing Stone", Fit = Fit.EachPiece, Takes = r => Made(r, "Shelter Stone") },
            new Swap { Prefab = "Prefabs/Raids/Events/Sites/Deep Patch.prefab", Holder = "Model", Look = "Art/Gathering/Berry Bush/Berry Bush B.prefab", Fit = Fit.EachPiece, FitWidth = true },
            new Swap { Prefab = "Prefabs/Raids/Events/Sites/The Elder.prefab", Holder = "Model", Look = "Art/Gathering/Trees/Elderwood B.prefab", Scale = 0.7f },
            // The items and the creatures' things.
            new Swap { Prefab = "Prefabs/Items/Models/HandTorchModel.prefab", Holder = "Expedition Torch(Clone)", Look = "Hand Torch", FitHeight = true, FlameOnTop = 0.6f },
            new Swap { Prefab = "Prefabs/Items/Torch.prefab", Holder = "Expedition Torch(Clone)", Look = "Hand Torch", FitHeight = true, FlameOnTop = 0.6f },
            new Swap { Prefab = "Art/Haven/Models/Fishing Rod Model.prefab", Look = "Fishing Rod", Fit = Fit.Longest },
            new Swap { Prefab = "Prefabs/Enemies/MossShaman.prefab", Holder = "hand_R", Look = "Shaman Staff", Fit = Fit.Longest, Takes = r => Named(r, "Staff") },
            new Swap { Prefab = "Prefabs/Items/Dropped Item.prefab", Holder = "Visual/Sack", Look = "Art/Transport/Cargo/Cargo Sack.prefab", FitWidth = true },
            new Swap { Prefab = "Prefabs/Enemies/TrollStone.prefab", Look = "Art/Gathering/Small Rocks/Small Rocks B.prefab", Centre = true, FitWidth = true },
        };
    }

    // The raids' loot caches (LootCache shows "Lid Closed" while full, "Lid Open" once emptied).
    private static readonly string[] Caches =
    {
        "Prefabs/Raids/SupplyCache.prefab", "Prefabs/Raids/Events/Abandoned Camp.prefab", "Prefabs/Raids/Events/Wolf Den.prefab",
        "Prefabs/Raids/Events/Sites/Fog Bank.prefab",
    };

    public static string Placeholders()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var report = new List<string>();

        // Every generated look as a prefab beside its model: LODGroup on the root, what the swaps place.
        var looks = new Dictionary<string, GameObject>();
        foreach (Generated asset in PlaceholderLooks)
        {
            if (!Ready(asset))
            {
                report.Add($"{asset.Name}: not baked yet");
                continue;
            }

            (Mesh[] levels, Material material) = Setup(asset);
            looks[asset.Name] = LookPrefab(asset.Folder + asset.Name + "/" + asset.Name + ".prefab", asset.Name, levels, new[] { new[] { material } });
            report.Add(Budget(asset.Name, levels, asset.Cap, asset.TextureSize));
        }

        // Looks made before (the Campfire, the cargo sack) as prefabs of their own, to place elsewhere too.
        var campfire = new Generated { Name = "Campfire", Parallax = 0.008f, Moss = 0.1f, Cap = 6000, Tint = new Color(0.68f, 0.66f, 0.64f) };
        if (Ready(campfire))
        {
            (Mesh[] levels, Material material) = Setup(campfire);
            LookPrefab(Art + "Campfire/Campfire.prefab", "Campfire", levels, new[] { new[] { material } });
        }

        const string cargo = "Assets/_Project/Art/Transport/Cargo/";
        if (Load<GameObject>(cargo + "Cargo.fbx") != null)
        {
            LookPrefab(cargo + "Cargo Sack.prefab", "Cargo Sack", Levels(cargo, cargo + "Cargo.fbx", "Cargo A"),
                new[] { new[] { Load<Material>(cargo + "Cargo.mat") } });
        }

        foreach (string cache in Caches)
        {
            report.Add(Cache(Project + cache, looks));
        }

        foreach (Swap swap in PlaceholderSwaps())
        {
            GameObject look = swap.Look.EndsWith(".prefab")
                ? Load<GameObject>(Project + swap.Look)
                : looks.TryGetValue(swap.Look, out GameObject made) ? made : null;
            if (look == null)
            {
                report.Add($"{swap.Prefab}: no {swap.Look} yet");
                continue;
            }

            string done = "";
            PrefabBuildUtility.EditPrefab(Load<GameObject>(Project + swap.Prefab), root => done = Place(root, swap, look));
            if (swap.Refit != null)
            {
                PrefabBuildUtility.EditPrefab(Load<GameObject>(Project + swap.Refit), structure =>
                {
                    PrefabBuildUtility.FitToModel(structure);
                    if (swap.Chimney && structure.TryGetComponent(out HearthSmoke smoke))
                    {
                        Vector3 top = Highest(structure, look);
                        PrefabBuildUtility.Configure(smoke, so => so.FindProperty("chimney").vector3Value = top);
                    }
                });
            }

            report.Add($"{System.IO.Path.GetFileNameWithoutExtension(swap.Prefab)} <- {look.name}: {done}");
        }

        report.Add(FieldCampFlames(Project + "Prefabs/Raids/Forward Camp.prefab"));

        // The Iron Trap held the Spike Trap's model in iron; it has its own now.
        if (looks.TryGetValue("Iron Trap", out GameObject iron))
        {
            var trap = Load<GameObject>(Structures + "Iron Trap.prefab");
            Transform model = trap.transform.Find(PrefabBuildUtility.ModelChildName);
            if (model == null || PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject) != iron)
            {
                PrefabBuildUtility.ReplaceModel(trap, iron, new PrefabBuildUtility.Placeholder(PrimitiveType.Cube, 1f));
                // Over the 1.8 m square the Spike Trap's stand-in covered (the ingest's height cap shrank it).
                PrefabBuildUtility.EditPrefab(trap, root =>
                {
                    Transform placed = root.transform.Find(PrefabBuildUtility.ModelChildName);
                    (Bounds shape, _) = LookShape(iron);
                    placed.localScale = new Vector3(1.8f / shape.size.x, 1f, 1.8f / shape.size.z);
                    PrefabBuildUtility.FitToModel(root);
                });
                report.Add("Iron Trap <- Iron Trap");
            }
        }

        // The Armored Dummy is a variant of the Training Dummy: the straw one it inherits is put away, its own goes in.
        if (looks.TryGetValue("Armored Dummy", out GameObject armored))
        {
            PrefabBuildUtility.EditPrefab(Load<GameObject>(Project + "Prefabs/Enemies/ArmoredDummy.prefab"), root =>
            {
                Transform straw = root.transform.Find("Training Dummy");
                if (straw != null) straw.gameObject.SetActive(false);
                if (root.transform.Find(armored.name) == null)
                {
                    var placed = (GameObject)PrefabUtility.InstantiatePrefab(armored, root.transform);
                    placed.transform.localPosition = Vector3.zero;
                }
            });
            report.Add("ArmoredDummy <- Armored Dummy");
        }

        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    private static string Place(GameObject root, Swap swap, GameObject look)
    {
        Transform holder = string.IsNullOrEmpty(swap.Holder) ? root.transform
            : root.transform.Find(swap.Holder) ?? root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == swap.Holder);
        if (holder == null)
        {
            return $"no {swap.Holder}";
        }

        // The stand-ins: primitive meshes below the holder, not effects, chosen by the swap.
        var pieces = new List<(Vector3 centre, Quaternion rotation, Vector3 size, Renderer renderer)>();
        foreach (Renderer renderer in holder.GetComponentsInChildren<MeshRenderer>(true))
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || AssetDatabase.GetAssetPath(filter.sharedMesh) != "Library/unity default resources" ||
                (renderer.sharedMaterial != null && Effects.Any(renderer.sharedMaterial.name.Contains)) ||
                (swap.Takes != null && !swap.Takes(renderer)))
            {
                continue;
            }

            Matrix4x4 toHolder = holder.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Bounds mesh = filter.sharedMesh.bounds;
            Vector3 size = Vector3.Scale(renderer.transform.lossyScale, mesh.size);
            size = new Vector3(size.x / holder.lossyScale.x, size.y / holder.lossyScale.y, size.z / holder.lossyScale.z);
            pieces.Add((toHolder.MultiplyPoint3x4(mesh.center), toHolder.rotation, size, renderer));
        }

        if (pieces.Count == 0)
        {
            return "no stand-ins left";
        }

        var kept = new List<Transform>();
        foreach (Renderer renderer in holder.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer.sharedMaterial != null && Effects.Any(renderer.sharedMaterial.name.Contains))
            {
                kept.Add(renderer.transform);
            }
        }

        (Bounds shape, Vector3 longest) = LookShape(look);
        var placedLooks = new List<Transform>();
        if (swap.Fit == Fit.Footprint)
        {
            var area = new Bounds(pieces[0].centre, Vector3.zero);
            foreach (var piece in pieces)
            {
                for (int corner = 0; corner < 8; corner++)
                {
                    var unit = new Vector3((corner & 1) == 0 ? -0.5f : 0.5f, (corner & 2) == 0 ? -0.5f : 0.5f, (corner & 4) == 0 ? -0.5f : 0.5f);
                    area.Encapsulate(piece.centre + piece.rotation * Vector3.Scale(unit, piece.size));
                }
            }

            float scale = swap.FitHeight ? area.size.y / shape.size.y
                : swap.FitWidth ? Mathf.Max(area.size.x, area.size.z) / Mathf.Max(shape.size.x, shape.size.z)
                : 1f / holder.lossyScale.x;
            scale *= swap.Scale;
            Quaternion turn = Quaternion.Euler(0f, swap.Yaw, 0f);
            Vector3 at = swap.Around ?? area.center;
            at.y = swap.Centre ? area.center.y : area.min.y;
            Vector3 anchor = swap.Centre ? shape.center : new Vector3(shape.center.x, shape.min.y, shape.center.z);
            // A wall piece must be exactly as long as its line's step; a trap must cover the ground it guards (the
            // height cap in the ingest shrank both).
            var stretch = Vector3.one * scale;
            if (swap.FitX || swap.FitFloor) stretch.x = area.size.x / shape.size.x;
            if (swap.FitFloor) stretch.z = area.size.z / shape.size.z;
            placedLooks.Add(Instance(look, holder, at + Vector3.up * swap.Lift - turn * Vector3.Scale(anchor, stretch), turn, stretch));
        }
        else
        {
            IEnumerable<(Vector3 centre, Quaternion rotation, Vector3 size, Renderer renderer)> chosen = swap.Fit == Fit.Longest
                ? pieces.OrderByDescending(p => Mathf.Max(p.size.x, p.size.y, p.size.z)).Take(1)
                : pieces;
            foreach (var piece in chosen)
            {
                float[] sides = { piece.size.x, piece.size.y, piece.size.z };
                int axis = sides[0] >= sides[1] && sides[0] >= sides[2] ? 0 : sides[1] >= sides[2] ? 1 : 2;
                float length = sides[axis], across = sides.Where((_, i) => i != axis).Max();
                Quaternion rotation;
                Vector3 scale;
                if (length > across * 1.6f || swap.Fit == Fit.Longest)
                {
                    // Laid along the piece, its own up kept as near the world's up as the piece allows.
                    Vector3 along = piece.rotation * (axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward);
                    if (swap.Flip) along = -along;
                    Vector3 up = Mathf.Abs(Vector3.Dot(along, Vector3.up)) > 0.95f ? piece.rotation * Vector3.forward : Vector3.up;
                    Vector3 ownUp = Mathf.Abs(Vector3.Dot(longest, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                    rotation = Quaternion.LookRotation(along, up) * Quaternion.Inverse(Quaternion.LookRotation(longest, ownUp));
                    float ratio = length / Mathf.Abs(Vector3.Dot(shape.size, longest));
                    // Stretched to the piece's length; across it, the look's own thickness in metres (a hand bone is
                    // scaled down, so 1 in its units would make a staff a few centimetres thick).
                    float own = 1f / holder.lossyScale.x;
                    scale = swap.Uniform ? Vector3.one * ratio : new Vector3(
                        Mathf.Approximately(Mathf.Abs(longest.x), 1f) ? ratio : own,
                        Mathf.Approximately(Mathf.Abs(longest.y), 1f) ? ratio : own,
                        Mathf.Approximately(Mathf.Abs(longest.z), 1f) ? ratio : own) * swap.Scale;
                    placedLooks.Add(Instance(look, holder, piece.centre + Vector3.up * swap.Lift - rotation * Vector3.Scale(shape.center, scale), rotation, scale));
                }
                else
                {
                    // Set on the ground where a round piece stood, turned as it was.
                    rotation = Quaternion.Euler(0f, piece.rotation.eulerAngles.y + swap.Yaw, 0f);
                    float fit = swap.FitWidth ? Mathf.Max(piece.size.x, piece.size.z) / Mathf.Max(shape.size.x, shape.size.z) : 1f / holder.lossyScale.x;
                    scale = Vector3.one * fit * swap.Scale;
                    Vector3 foot = piece.centre - Vector3.up * piece.size.y * 0.5f;
                    placedLooks.Add(Instance(look, holder, foot + Vector3.up * swap.Lift - rotation * Vector3.Scale(new Vector3(shape.center.x, shape.min.y, shape.center.z), scale), rotation, scale));
                }
            }
        }

        foreach (var piece in pieces)
        {
            Remove(piece.renderer, holder);
        }

        if (swap.FlameOnTop >= 0f && placedLooks.Count > 0)
        {
            // The flame on the torch's head, the orb over the altar's basin.
            Transform first = placedLooks[0];
            float top = first.localPosition.y + (first.localRotation * Vector3.Scale(new Vector3(0f, shape.max.y, 0f), first.localScale)).y;
            foreach (Transform effect in kept)
            {
                if (effect.parent != holder) continue;
                float half = effect.localScale.y * 0.5f;
                Vector3 at = effect.localPosition;
                at.y = top + half * swap.FlameOnTop;
                effect.localPosition = at;
            }
        }

        return $"{pieces.Count} stand-ins, {placedLooks.Count} placed";
    }

    /// <summary>The Forward Camp's glowing stand-ins (an ember disc in its ring, an ember cube on each post) become the
    /// game's flame, the one the Campfire and the torches burn with: on the fire's ash and on each torch's head.</summary>
    private static string FieldCampFlames(string path)
    {
        var flame = Load<Material>("Assets/_Project/Materials/Stations/StationFire.mat") ??
                    AssetDatabase.FindAssets("StationFire t:Material").Select(g => Load<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        Mesh sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        int done = 0;
        PrefabBuildUtility.EditPrefab(Load<GameObject>(path), root =>
        {
            Transform[] torches = root.GetComponentsInChildren<LODGroup>(true).Select(g => g.transform).Where(t => t.name == "Standing Torch").ToArray();
            foreach (Transform ember in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Ember").ToArray())
            {
                if (!ember.TryGetComponent(out MeshRenderer renderer) || renderer.sharedMaterial == flame) continue;
                ember.GetComponent<MeshFilter>().sharedMesh = sphere;
                renderer.sharedMaterial = flame;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Transform torch = torches.OrderBy(t => (new Vector2(t.localPosition.x, t.localPosition.z) - new Vector2(ember.localPosition.x, ember.localPosition.z)).sqrMagnitude).FirstOrDefault();
                bool onTorch = torch != null && (new Vector2(torch.localPosition.x, torch.localPosition.z) - new Vector2(ember.localPosition.x, ember.localPosition.z)).magnitude < 0.5f;
                if (onTorch)
                {
                    (Bounds shape, _) = LookShape(torch.gameObject);
                    ember.localScale = new Vector3(0.16f, 0.26f, 0.16f);
                    ember.localPosition = new Vector3(ember.localPosition.x, torch.localPosition.y + shape.max.y * torch.localScale.y + 0.08f, ember.localPosition.z);
                }
                else
                {
                    ember.localScale = new Vector3(0.36f, 0.48f, 0.36f);
                    ember.localPosition = new Vector3(ember.localPosition.x, 0.3f, ember.localPosition.z);
                }

                done++;
            }
        });
        return $"Forward Camp: {done} flames";
    }

    /// <summary>The look's level-0 shape in its own space, and the axis it is longest along.</summary>
    private static (Bounds shape, Vector3 longest) LookShape(GameObject look)
    {
        Renderer[] renderers = look.TryGetComponent(out LODGroup group) && group.lodCount > 0
            ? group.GetLODs()[0].renderers.Where(r => r != null).ToArray()
            : look.GetComponentsInChildren<Renderer>(true);
        Bounds shape = default;
        bool any = false;
        foreach (Renderer renderer in renderers)
        {
            if (!(renderer is MeshRenderer) || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null) continue;
            Matrix4x4 toLook = look.transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = toLook.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f)));
                if (!any) { shape = new Bounds(point, Vector3.zero); any = true; }
                else shape.Encapsulate(point);
            }
        }

        Vector3 s = shape.size;
        Vector3 longest = s.x >= s.y && s.x >= s.z ? Vector3.right : s.y >= s.z ? Vector3.up : Vector3.forward;
        return (shape, longest);
    }

    private static Transform Instance(GameObject look, Transform holder, Vector3 position, Quaternion rotation, Vector3 scale)
    {
        var placed = (GameObject)PrefabUtility.InstantiatePrefab(look, holder);
        placed.transform.localPosition = position;
        placed.transform.localRotation = rotation;
        placed.transform.localScale = scale;
        return placed.transform;
    }

    /// <summary>A stand-in goes: what it carried (a light, a particle system) moves to the holder first; one with a
    /// collider or anything else of its own keeps its object and only loses its mesh.</summary>
    private static void Remove(Renderer renderer, Transform holder)
    {
        Transform piece = renderer.transform;
        for (int i = piece.childCount - 1; i >= 0; i--)
        {
            piece.GetChild(i).SetParent(holder, true);
        }

        bool plain = piece.GetComponents<Component>().All(c => c is Transform || c is MeshFilter || c is MeshRenderer);
        if (plain && piece != holder)
        {
            Object.DestroyImmediate(piece.gameObject);
        }
        else
        {
            Object.DestroyImmediate(renderer);
            Object.DestroyImmediate(piece.GetComponent<MeshFilter>());
        }
    }

    /// <summary>The look's highest point in the structure's space (where its chimney's smoke rises).</summary>
    private static Vector3 Highest(GameObject structure, GameObject look)
    {
        Vector3 best = Vector3.zero;
        float height = float.MinValue;
        foreach (MeshFilter filter in structure.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.name != "LOD0" || PrefabUtility.GetCorrespondingObjectFromSource(filter.transform.parent.gameObject) != look)
            {
                continue;
            }

            Matrix4x4 toRoot = structure.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 point = toRoot.MultiplyPoint3x4(vertex);
                if (point.y > height)
                {
                    height = point.y;
                    best = point;
                }
            }
        }

        return best;
    }

    /// <summary>A loot cache: the crate where its "Body" stood, its lid on top ("Lid Closed") or thrown back off the
    /// hinge at the back ("Lid Open"); the objects LootCache shows stay, with what they carry (the full cache's light).</summary>
    private static string Cache(string path, Dictionary<string, GameObject> looks)
    {
        if (!looks.TryGetValue("Cache Crate", out GameObject crate) || !looks.TryGetValue("Cache Lid", out GameObject lid))
        {
            return $"{System.IO.Path.GetFileNameWithoutExtension(path)}: no crate yet";
        }

        int done = 0;
        PrefabBuildUtility.EditPrefab(Load<GameObject>(path), root =>
        {
            foreach (Transform closed in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Lid Closed").ToArray())
            {
                Transform parent = closed.parent, body = parent.Find("Body"), open = parent.Find("Lid Open"), trim = parent.Find("Trim");
                if (body == null || open == null || body.GetComponent<MeshFilter>() == null)
                {
                    continue;
                }

                Vector3 size = body.localScale, foot = body.localPosition - Vector3.up * size.y * 0.5f;
                (Bounds crateShape, _) = LookShape(crate);
                (Bounds lidShape, _) = LookShape(lid);
                float scale = size.x * 1.04f / crateShape.size.x;
                Instance(crate, parent, foot - new Vector3(crateShape.center.x, crateShape.min.y, crateShape.center.z) * scale, Quaternion.identity, Vector3.one * scale).name = "Crate";
                float top = foot.y + crateShape.size.y * scale;
                float depth = crateShape.size.z * scale;
                // The lid laid over the crate's mouth, its own thickness kept.
                var lidScale = new Vector3(crateShape.size.x * scale * 1.03f / lidShape.size.x, scale, depth * 1.03f / lidShape.size.z);
                Vector3 lidCentre = new Vector3(lidShape.center.x, lidShape.min.y, lidShape.center.z);
                foreach ((Transform holder, Vector3 at, Quaternion turn, Vector3 offset) in new[]
                         {
                             (closed, new Vector3(foot.x, top, foot.z), Quaternion.identity, Vector3.zero),
                             // Off the hinge at the back, swung up and past upright.
                             (open, new Vector3(foot.x, top, foot.z - depth * 0.5f), Quaternion.Euler(-105f, 0f, 0f), new Vector3(0f, 0f, depth * 0.5f)),
                         })
                {
                    var carried = new List<(Transform child, Vector3 position, Quaternion rotation)>();
                    foreach (Transform child in holder) carried.Add((child, child.position, child.rotation));
                    Object.DestroyImmediate(holder.GetComponent<MeshRenderer>());
                    Object.DestroyImmediate(holder.GetComponent<MeshFilter>());
                    holder.localPosition = at;
                    holder.localRotation = turn;
                    holder.localScale = Vector3.one;
                    foreach ((Transform child, Vector3 position, Quaternion rotation) in carried)
                    {
                        child.SetPositionAndRotation(position, rotation);
                    }

                    Instance(lid, holder, offset - Vector3.Scale(lidCentre, lidScale), Quaternion.identity, lidScale).name = "Lid";
                }

                Object.DestroyImmediate(body.gameObject);
                if (trim != null) Object.DestroyImmediate(trim.gameObject);
                done++;
            }
        });
        return $"{System.IO.Path.GetFileNameWithoutExtension(path)}: {done} cache(s)";
    }

    // ----- Kit-built buildings -----

    // The game's material for each of the kit's slots (the FBX's material names).
    private static Dictionary<string, Material> KitSlots()
    {
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
        return materials;
    }

    // ----- Marks: what sets a special place apart from the woods -----

    // The user (2026-11-13): "make these sites and special stones emit some light or make it stand out ... they are a bit
    // hard to read from normal world dressing vs special places". Every raid event site and world prop gets a "Mark": a
    // glowing ring on the ground round it (a decal, Tools/LocalSpawn/site_rings.py), a few slow motes rising from it and
    // a soft light. Gold for the event sites (the colour of their beacons), the runes' blue for the props. A site's mark
    // goes out with its beacon once it is done (RaidEventSite.mark); a prop's stays.
    private static readonly Color SiteGold = new Color(1f, 0.8f, 0.45f);
    private static readonly Color RuneBlue = new Color(0.5f, 0.78f, 1f);

    // What the player thinks on coming upon each place (CuriousPlace): how it looks, and what it might be, or be for. The
    // first is said on noticing it; a stone's later ones on each closer look.
    private static readonly Dictionary<string, string[]> Thoughts = new Dictionary<string, string[]>
    {
        ["Asas Farmstead"] = new[] { "Burned down to the posts. The hearth's still standing, and someone saved a bundle from the fire. They meant to come back for it." },
        ["Barrow Hoard"] = new[] { "A barrow, and the door stone's been moved. Not long ago, either. Whoever's in there has been left alone a long time. Until now." },
        ["Bjarnes Forge"] = new[] { "A forge, out here in the woods? The anvil's still good. Someone worked iron here, and didn't want to be found doing it." },
        ["Blighted Tree"] = new[] { "Everything round it is green, and it's dead. Black to the root, and the ground under it too. Something drank the life out of this one." },
        ["Crossroads Stranger"] = new[] { "Paths shouldn't cross like that, out here. And there's someone standing where they meet. I don't like how still they are." },
        ["Deep Patch"] = new[] { "Berries this deep in the woods, and nobody's picked them. Either nobody comes here, or nobody comes back." },
        ["Eiriks Stand"] = new[] { "Stakes, sharpened and driven in all round. Someone held this ring against something. Wolves, by the marks. They did it well." },
        ["Fog Bank"] = new[] { "The fog's gathering here and nowhere else. And there are lights in it. I should keep my wits about me." },
        ["Gathering Place"] = new[] { "Bones hung on a post, and a ring of stones round it. Not a grave. A meeting place. For whatever wears those bones as warnings." },
        ["Gunnars Pit"] = new[] { "A hunter's pit, stakes pointing in. The cover's fallen through. Did it catch something? Or someone?" },
        ["Heartroot Stump"] = new[] { "A stump as wide as a house, split down the middle. And the wood is breathing. Where a heart would be, it's black, as if a fire once burned there." },
        ["Hilds Wall"] = new[] { "A wall in a ring, waist high, and nothing inside it. Who builds a wall round nothing? Unless it was built to keep something in." },
        ["Living Stones"] = new[] { "Three runestones across the path. They weren't here before. I'd swear one of them has moved since I looked." },
        ["Livs Circle"] = new[] { "Standing stones in a ring, and they hum. Old folk said the hidden people dance in circles like this. I can almost hear the music." },
        ["Night Cairn"] = new[] { "A cairn, piled tall with care. Antlers at its foot like an offering. Cairns mark graves, or ways. I wonder which this is." },
        ["Old Bathhouse"] = new[] { "A bathhouse, fallen in. The stove stones are still there. Odd thing to build this far out. Someone wanted to be warm and alone." },
        ["Old Beacon"] = new[] { "A beacon, still laid with dry wood. Lit, it'd be seen for miles. That's the trouble with beacons: everything sees them." },
        ["Open Barrow"] = new[] { "The barrow's slipped open. There's a glint of silver in the dark. Grave goods. The dead don't like to share." },
        ["Overturned Wagon"] = new[] { "A trader's wagon on its side, a wheel in splinters, parcels in the mud. Those claw marks on the boards are fresh." },
        ["Runas Marks"] = new[] { "Marks cut into the bark, pale and careful. Not claw marks. Someone left these on purpose, for someone else to follow." },
        ["Sigrids Horn"] = new[] { "A horn hung on a pole, inside a ring of stakes. Someone meant to call for help from here. Did anybody answer?" },
        ["Silversmiths Rest"] = new[] { "A small grave by an old work bench. His tools are still here, rusting. And there's silver in the grass. Whoever he was, he was good." },
        ["The Elder"] = new[] { "An elder, standing on its own in the clearing like it owns it. The old ones say an elder has a mother living in it. Best to ask before taking." },
        ["Toll Troll"] = new[] { "Footprints the size of a door, and they stop right here. Somewhere close, something is laughing to itself." },
        ["Tors Shelter"] = new[] { "A shelter, and a good one. Too good for out here. Cut wood, a bed of hides, a saw by the trestle. Whoever built it means to stay." },
        ["Trapped Pedlar"] = new[] { "A spruce has come down across the ditch. That's a pack, just under it. And was that a voice?" },
        ["Wainwrights Camp"] = new[] { "A wainwright's camp, gone cold. A wheel against the crates, a book left on the table. He didn't pack up. He left." },
        ["Wild Swarm"] = new[] { "That dark lump on the branch is moving. Bees, a whole swarm of them, hanging in a knot. A skep could take them home." },
        ["Yrsas Fire"] = new[] { "A cookfire with the embers still warm. A pot on the tripod, a bedroll. Someone was here an hour ago. Where did they go?" },
        ["God Post"] = new[]
        {
            "A carved post, an old god's face worn soft by the rain. Somebody still keeps it: there's no moss where hands touch it.",
            "Which god is it? The eyes are deep set, and it looks out at the road. A watcher, then. Or a warning.",
        },
        ["Offering Stone"] = new[]
        {
            "A flat stone with a bowl cut into the top, and someone's left berries and an antler in it. Fresh. Gifts for whoever lives near.",
            "The old folk left the first of every harvest out like this. Leave nothing, and the hidden ones take what they're owed.",
        },
        ["Rune Cairn"] = new[]
        {
            "Stones piled up, and runes on them that glow blue in the shade. Not paint. They're giving off their own light.",
            "The runes are the same on every stone. A name, maybe. Or a warding. Whoever piled these meant them to last.",
        },
        ["Rune Stone"] = new[]
        {
            "A rune stone, taller than me. The runes run round it like a serpent, and they glow. Somebody wanted this remembered.",
            "Raised in memory, the old stones usually say. But I can't read whose. The glow is warm when I stand close.",
        },
        ["Sleeping Troll Rock"] = new[]
        {
            "That rock has a face. A nose, a closed eye, a mouth in the moss. The old stories say trolls turn to stone in the sun.",
            "If it's a troll, it's sleeping. I'll be quiet. Just in case it's only waiting for the dark.",
        },
    };

    /// <summary>Puts a Mark on every raid event site and world prop (`--entry BuildStructureModels.Marks`). Safe to rerun.</summary>
    public static string Marks()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Material gold = RingMaterial("Site Ring"), blue = RingMaterial("Rune Ring");
        Material motes = MoteMaterial();
        var report = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { SitePrefabs.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Load<GameObject>(path).GetComponent<RaidEventSite>() == null) continue;
            report.Add(Mark(path, gold, motes, SiteGold, 1f, true));
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/World/Props" }))
        {
            report.Add(Mark(AssetDatabase.GUIDToAssetPath(guid), blue, motes, RuneBlue, 0.6f, false));
        }

        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    private static string Mark(string path, Material ring, Material motes, Color colour, float strength, bool site)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform old = root.transform.Find("Mark");
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // How wide the place is: its model's renderers, not the beacon's pillar or the trigger.
            Transform model = root.transform.Find("Model");
            if (model == null) model = root.transform;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one * 2f);
            foreach (Renderer part in renderers) bounds.Encapsulate(part.bounds);
            Vector3 centre = root.transform.InverseTransformPoint(bounds.center);
            float across = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) * 1.35f + 1f, 3f, 16f);

            var mark = new GameObject("Mark").transform;
            mark.SetParent(root.transform, false);
            mark.localPosition = new Vector3(centre.x, 0f, centre.z);

            // The ring, pressed onto the ground from above (a decal lands on whatever is in its box).
            var decal = new GameObject("Ring").AddComponent<UnityEngine.Rendering.Universal.DecalProjector>();
            decal.transform.SetParent(mark, false);
            decal.transform.localPosition = new Vector3(0f, 1f, 0f);
            decal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            decal.material = ring;
            decal.size = new Vector3(across, across, 3f);
            decal.pivot = Vector3.zero;
            decal.fadeFactor = 0.9f;
            decal.gameObject.AddComponent<GroundRing>(); // its box fitted to the ground where it is put down, so it paints no one

            // A soft light low over it: the ring and the place glow at night, little by day.
            var light = new GameObject("Glow").AddComponent<Light>();
            light.transform.SetParent(mark, false);
            light.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            light.type = LightType.Point;
            light.color = colour;
            light.intensity = 2.2f * strength;
            light.range = across * 0.75f;
            light.shadows = LightShadows.None;

            // Motes rising slowly off the ring.
            var motesObject = new GameObject("Motes");
            motesObject.transform.SetParent(mark, false);
            motesObject.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var particles = motesObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(colour.r, colour.g, colour.b, 0.9f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = Mathf.Lerp(3f, 8f, Mathf.InverseLerp(3f, 16f, across)) * strength;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = across * 0.42f;
            shape.radiusThickness = 0.25f;
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = 0.25f;
            noise.frequency = 0.4f;
            var renderer = motesObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = motes;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // What the player thinks of it on coming near (CuriousPlace). A site has its own use, so its thoughts sit on the
            // Mark; a stone takes "Look closer" for more.
            string key = root.name;
            if (Thoughts.TryGetValue(key, out string[] lines))
            {
                foreach (CuriousPlace old2 in root.GetComponentsInChildren<CuriousPlace>(true)) Object.DestroyImmediate(old2);
                GameObject holder = site ? mark.gameObject : root;
                var curious = new SerializedObject(holder.AddComponent<CuriousPlace>());
                curious.FindProperty("placeName").stringValue = PlaceName(key);
                SerializedProperty list = curious.FindProperty("thoughts");
                list.arraySize = lines.Length;
                for (int i = 0; i < lines.Length; i++) list.GetArrayElementAtIndex(i).stringValue = lines[i];
                curious.FindProperty("noticeRadius").floatValue = Mathf.Max(6f, across * 0.5f + 3f);
                curious.ApplyModifiedPropertiesWithoutUndo();
                if (!site)
                {
                    Interactable look = root.TryGetComponent(out Interactable found) ? found : root.AddComponent<Interactable>();
                    var use = new SerializedObject(look);
                    use.FindProperty("kind").intValue = (int)InteractionKind.Use;
                    use.FindProperty("duration").floatValue = 0f;
                    use.FindProperty("range").floatValue = Mathf.Max(2.5f, across * 0.4f);
                    use.FindProperty("deactivateOnComplete").boolValue = false;
                    use.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (site && root.TryGetComponent(out RaidEventSite eventSite))
            {
                var data = new SerializedObject(eventSite);
                data.FindProperty("mark").objectReferenceValue = mark.gameObject;
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
            return $"{root.name}: ring {across:0.0} m";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // "Eiriks Stand" -> "Eirik's Stand" for the prompt.
    private static string PlaceName(string key)
    {
        foreach (string owner in new[] { "Asas", "Bjarnes", "Eiriks", "Gunnars", "Hilds", "Livs", "Runas", "Sigrids", "Silversmiths", "Tors", "Wainwrights", "Yrsas" })
        {
            if (key.StartsWith(owner + " ")) return owner.Substring(0, owner.Length - 1) + "'s" + key.Substring(owner.Length);
        }

        return key;
    }

    private static Material RingMaterial(string name)
    {
        string texturePath = "Assets/_Project/Art/Decals/" + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
        string path = "Assets/_Project/Art/Decals/Materials/" + name + ".mat";
        var material = Load<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Shader Graphs/Decal"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("Base_Map", Load<Texture2D>(texturePath));
        material.SetFloat("Normal_Blend", 0f);
        material.SetFloat("_DrawOrder", 2f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // Soft additive motes (as the gear's essence motes, BuildEssence): the particles carry the colour.
    private static Material MoteMaterial()
    {
        string path = SiteArt + "Site Motes.mat";
        var material = Load<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        EditorUtility.SetDirty(material);
        return material;
    }

    // ----- Raid event sites -----

    private const string SiteArt = "Assets/_Project/Art/Raids/Sites/";
    private const string SitePrefabs = "Assets/_Project/Prefabs/Raids/Events/Sites/";

    /// <summary>
    /// The raid event sites kit-built by Tools/Blender/raid_sites.py (`--entry BuildStructureModels.RaidSites`): each
    /// site's look (its levels of detail, the kit's materials) becomes Art/Raids/Sites/&lt;Site&gt;/&lt;Site&gt;.prefab and takes
    /// the place of the primitive shapes under the site prefab's "Model". The site's trigger, beacon and event stay.
    /// </summary>
    public static string RaidSites()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Dictionary<string, Material> materials = KitSlots();
        var report = new List<string>();
        foreach (string folder in AssetDatabase.GetSubFolders(SiteArt.TrimEnd('/')))
        {
            string name = System.IO.Path.GetFileName(folder);
            string fbx = folder + "/" + name + ".fbx", sitePath = SitePrefabs + name + ".prefab";
            if (Load<GameObject>(fbx) == null || Load<GameObject>(sitePath) == null)
            {
                report.Add($"{name}: no model or no site");
                continue;
            }

            Model(fbx, materials);
            var model = Load<GameObject>(fbx);
            var levels = new List<Mesh>();
            var slots = new List<Material[]>();
            foreach (string suffix in new[] { "", " LOD1", " LOD2" })
            {
                Transform part = model.transform.Find(name + suffix);
                if (part == null) continue;
                levels.Add(part.GetComponent<MeshFilter>().sharedMesh);
                slots.Add(part.GetComponent<MeshRenderer>().sharedMaterials);
            }

            // Grass on what is made of turf (a barrow's mound), as on the kit roofs.
            Material[] first = slots[0];
            bool turf = first.Any(m => m != null && m.name == "Kit Turf");
            (Mesh grass, Material grassMaterial) = turf
                ? RoofGrass(folder + "/", name, levels[0], (sub, centre, uv) => sub < first.Length && first[sub] != null && first[sub].name == "Kit Turf")
                : (null, null);
            if (grassMaterial != null) grassMaterial = MoundGrass(grassMaterial);
            if (turf)
            {
                // The mound itself dark earth under its grass, not the Haven's sunlit sod (the woods' pale floor
                // texture read tan without the terrain's tint: tried and dropped).
                Material floor = Soil();
                foreach (Material[] level in slots)
                {
                    for (int i = 0; i < level.Length; i++)
                    {
                        if (level[i] != null && level[i].name == "Kit Turf") level[i] = floor;
                    }
                }
            }
            GameObject look = LookPrefab(folder + "/" + name + ".prefab", name, levels.ToArray(), slots.ToArray(), grass, grassMaterial);
            GameObject site = PrefabUtility.LoadPrefabContents(sitePath);
            try
            {
                Transform holder = site.transform.Find("Model");
                for (int i = holder.childCount - 1; i >= 0; i--) Object.DestroyImmediate(holder.GetChild(i).gameObject);
                PrefabUtility.InstantiatePrefab(look, holder);
                // The Heartroot's stand-in stump stood beside its Model (BuildFirstBoss.DressStump): the look replaces it too.
                Transform stump = site.transform.Find("Stump");
                if (stump != null) Object.DestroyImmediate(stump.gameObject);
                PrefabUtility.SaveAsPrefabAsset(site, sitePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(site);
            }

            report.Add($"{name}: {string.Join("/", levels.Select(TriangleCount))} triangles, {levels[0].bounds.size:F1}");
        }

        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    // The roof grass made for the raids' woods: the Haven's sunny meadow green glowed yellow on a barrow in the dark
    // undergrowth (seen in a launched Misty Woods raid), so the mounds' grass is a deep forest green, with few dry patches.
    private static Material MoundGrass(Material roofGrass)
    {
        string path = SiteArt + "Mound Grass.mat";
        var material = Load<Material>(path);
        if (material == null)
        {
            material = new Material(roofGrass) { name = "Mound Grass" };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.CopyPropertiesFromMaterial(roofGrass);
        }

        material.SetColor("_BottomColor", new Color(0.05f, 0.1f, 0.03f));
        material.SetColor("_TopColor", new Color(0.2f, 0.32f, 0.1f));
        material.SetColor("_DryColor", new Color(0.3f, 0.3f, 0.14f));
        material.SetFloat("_DryAmount", 0.05f);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ----- Decorations (2026-11-14, the user: "more decorations, like benches") -----

    // Modelled by Tools/Blender/haven_decor.py in the kit's materials (its manifest: haven_decor_manifest.json): the asset,
    // its building's id and file, what it says, what it costs, the comfort and renown it brings, the Workshop tier it needs
    // (0: known from the start), and seconds of hammering. Comfort only on what people sit or linger at, so ten new kinds
    // don't stretch Well Rested past a raid's length.
    private static readonly (string asset, string id, string file, string text, (string item, int amount)[] cost, int comfort,
        int renown, int workshop, float work)[] DecorPieces =
    {
        ("Log Bench", "log_bench", "LogBench", "A log sawn flat on two stumps. Somewhere to sit and watch the clearing.",
            new[] { ("OakWood", 4) }, 1, 2, 0, 2f),
        ("Plank Bench", "plank_bench", "PlankBench", "A plank bench with a back to lean on, for the long evenings.",
            new[] { ("OakPlank", 4), ("OakWood", 2) }, 1, 3, 1, 3f),
        ("Trestle Table", "trestle_table", "TrestleTable", "A table and two stools. A meal eaten together is a meal remembered.",
            new[] { ("OakPlank", 6), ("OakWood", 4) }, 1, 4, 1, 4f),
        ("Woodpile", "woodpile", "Woodpile", "Split billets stacked to dry under bark. A home that means to see the winter.",
            new[] { ("OakWood", 8) }, 0, 2, 0, 2f),
        ("Barrels and Crates", "barrels_and_crates", "BarrelsAndCrates", "Barrels, crates and a sack: the clutter of a place where things are made.",
            new[] { ("OakPlank", 6), ("IronIngot", 1), ("Linen", 1) }, 0, 3, 1, 3f),
        ("Carved Post", "carved_post", "CarvedPost", "A guardian post with a stern face, painted red. It watches the edge of the woods.",
            new[] { ("OakWood", 6), ("Berries", 2) }, 0, 5, 2, 4f),
        ("Flower Bed", "flower_bed", "FlowerBed", "Wildflowers and herbs in a ring of stones. Colour that nothing in the woods put there.",
            new[] { ("GreyStone", 6), ("HealingHerb", 2) }, 1, 3, 1, 3f),
        ("Signpost", "signpost", "Signpost", "Three painted arrows. Nobody here needs them, but a village has one.",
            new[] { ("OakWood", 2), ("OakPlank", 3) }, 0, 2, 0, 2f),
        ("Rune Stone", "rune_stone", "RuneStone", "A standing stone cut with a serpent band. Those who made this place, remembered.",
            new[] { ("GreyStone", 10), ("Berries", 2) }, 1, 5, 2, 5f),
        ("Hay Bales", "hay_bales", "HayBales", "Bales of hay and an old cart wheel. It smells of summer.",
            new[] { ("Barley", 6), ("NettleCord", 1), ("OakPlank", 2) }, 0, 2, 1, 2f),
    };

    // The decorations made before these, moved to the same tab.
    private static readonly string[] OlderDecorations = { "LanternPost", "HavenBanner", "HerbPlanter" };

    // A building's torch on its wall (BuildWallTorches puts them up): the kit model, its flame and a light, lit at dusk.
    private const string WallTorchPrefab = "Assets/_Project/Prefabs/Structures/Parts/Wall Torch.prefab";
    // Where the torch's charred tip is, in its own space (the manifest's Flame mark), and the flame above it.
    private static readonly Vector3 TorchTip = new Vector3(0f, 0.31f, 0.253f);

    /// <summary>The decorations (`--entry BuildStructureModels.Decor`): their look prefabs from the FBXs, their building
    /// definitions and prefabs, the build menu's Decorations tab; and the Wall Torch part. Re-runnable.</summary>
    public static string Decor()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Dictionary<string, Material> materials = KitSlots();
        var report = new List<string>();
        var workshop = Load<BuildingDefinition>("Assets/_Project/Data/Buildings/ConstructionTable.asset");
        var planter = Load<BuildingDefinition>("Assets/_Project/Data/Buildings/HerbPlanter.asset");
        int order = 910;
        foreach (var piece in DecorPieces)
        {
            (GameObject look, Mesh[] levels) = DecorLook(piece.asset, materials);
            if (look == null)
            {
                report.Add($"{piece.asset}: no model");
                continue;
            }

            string path = "Assets/_Project/Data/Buildings/" + piece.file + ".asset";
            var building = Load<BuildingDefinition>(path);
            if (building == null)
            {
                building = ScriptableObject.CreateInstance<BuildingDefinition>();
                AssetDatabase.CreateAsset(building, path);
            }

            var data = new SerializedObject(building);
            data.FindProperty("id").stringValue = piece.id;
            data.FindProperty("displayName").stringValue = piece.asset;
            data.FindProperty("description").stringValue = piece.text;
            data.FindProperty("combatStats").objectReferenceValue = planter.CombatStats;
            SerializedProperty items = data.FindProperty("constructionItems");
            items.arraySize = piece.cost.Length;
            for (int i = 0; i < piece.cost.Length; i++)
            {
                var item = Load<ItemDefinition>("Assets/_Project/Data/Items/Materials/" + piece.cost[i].item + ".asset");
                if (item == null) throw new System.Exception("No item " + piece.cost[i].item);
                items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = item;
                items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = piece.cost[i].amount;
            }

            data.FindProperty("knownFromStart").boolValue = true;
            data.FindProperty("showInBuildMenu").boolValue = true;
            data.FindProperty("sortOrder").intValue = order++;
            SerializedProperty requires = data.FindProperty("requires");
            requires.arraySize = piece.workshop > 0 ? 1 : 0;
            if (piece.workshop > 0)
            {
                requires.GetArrayElementAtIndex(0).FindPropertyRelative("building").objectReferenceValue = workshop;
                requires.GetArrayElementAtIndex(0).FindPropertyRelative("tier").intValue = piece.workshop;
            }

            data.FindProperty("decoration").boolValue = true;
            data.FindProperty("comfort").intValue = piece.comfort;
            data.FindProperty("yardDepth").floatValue = 0f;
            data.ApplyModifiedPropertiesWithoutUndo();
            building.ConfigureBuildingUp(piece.work, piece.renown, new BuildingLevel[0]);
            EditorUtility.SetDirty(building);

            if (building.Prefab == null)
            {
                GameObject prefab = BuildingPrefabBuilder.Create(building, look);
                data.Update();
                data.FindProperty("prefab").objectReferenceValue = prefab;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Transform current = building.Prefab.transform.Find(PrefabBuildUtility.ModelChildName);
                if (current == null || PrefabUtility.GetCorrespondingObjectFromSource(current.gameObject) != look)
                {
                    PrefabBuildUtility.ReplaceModel(building.Prefab, look, BuildingPrefabBuilder.Placeholder);
                }

                BuildingPrefabBuilder.Update(building.Prefab, building);
                Refit(AssetDatabase.GetAssetPath(building.Prefab));
            }

            // Hammered up like any building (BuildHaven.AddConstruction gives the older ones theirs).
            PrefabBuildUtility.EditPrefab(building.Prefab, root =>
            {
                if (root.GetComponent<Construction>() == null)
                {
                    root.AddComponent<Construction>();
                }
            });
            AssetDatabase.SaveAssetIfDirty(building);
            report.Add(Budget(piece.asset, levels, 6000, 512));
        }

        foreach (string older in OlderDecorations)
        {
            var building = Load<BuildingDefinition>("Assets/_Project/Data/Buildings/" + older + ".asset");
            if (building == null) continue;
            var data = new SerializedObject(building);
            data.FindProperty("decoration").boolValue = true;
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(building);
        }

        report.Add(WallTorch(materials));
        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    private static (GameObject look, Mesh[] levels) DecorLook(string asset, Dictionary<string, Material> materials)
    {
        string fbx = Art + asset + "/" + asset + ".fbx";
        if (Load<GameObject>(fbx) == null)
        {
            return (null, null);
        }

        Model(fbx, materials);
        var model = Load<GameObject>(fbx);
        var levels = new List<Mesh>();
        var slots = new List<Material[]>();
        foreach (string suffix in new[] { "", " LOD1", " LOD2" })
        {
            Transform part = model.transform.Find(asset + suffix);
            if (part == null && suffix.Length == 0 && model.TryGetComponent(out MeshFilter _))
            {
                part = model.transform;
            }

            if (part == null) continue;
            levels.Add(part.GetComponent<MeshFilter>().sharedMesh);
            slots.Add(part.GetComponent<MeshRenderer>().sharedMaterials);
        }

        if (levels.Count == 0)
        {
            return (null, null);
        }

        GameObject look = LookPrefab(System.IO.Path.ChangeExtension(fbx, ".prefab"), asset, levels.ToArray(), slots.ToArray());
        return (look, levels.ToArray());
    }

    // The Wall Torch part: its model; a Lit child at the tip holding the flame (the Fire shader's sphere, as the standing
    // torch's) and a warm flickering light a hand in front of it that lights the wall and hangs in the mist; NightFlame
    // shows Lit after dusk.
    private static string WallTorch(Dictionary<string, Material> materials)
    {
        (GameObject look, Mesh[] levels) = DecorLook("Wall Torch", materials);
        if (look == null)
        {
            return "Wall Torch: no model";
        }

        string folder = System.IO.Path.GetDirectoryName(WallTorchPrefab).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder).Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        }

        var root = new GameObject("Wall Torch");
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(look, root.transform);
            model.name = "Model";
            var lit = new GameObject("Lit");
            lit.transform.SetParent(root.transform, false);
            lit.transform.localPosition = TorchTip;

            GameObject flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Flame";
            Object.DestroyImmediate(flame.GetComponent<Collider>());
            flame.transform.SetParent(lit.transform, false);
            // As big as the standing torch's flame, near enough: smaller, the Fire shader's tongues were too faint to see
            // from the game's camera (seen in Play, 2026-11-14).
            flame.transform.localPosition = new Vector3(0f, 0.12f, 0.02f);
            flame.transform.localScale = new Vector3(0.24f, 0.42f, 0.24f);
            var flameRenderer = flame.GetComponent<MeshRenderer>();
            flameRenderer.sharedMaterial = Load<Material>("Assets/_Project/Materials/Stations/StationFire.mat");
            flameRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            flameRenderer.receiveShadows = false;

            var glow = new GameObject("Light");
            glow.transform.SetParent(lit.transform, false);
            // Out from the wall, so it lights the wall broadly rather than burning a white patch just above the torch.
            glow.transform.localPosition = new Vector3(0f, 0.15f, 0.32f);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.46f);
            light.intensity = 3f;
            light.range = 6.5f;
            light.shadows = LightShadows.None;
            glow.AddComponent<LightFlicker>().Configure(new Color(1f, 0.52f, 0.2f, 0.4f), 0.02f, false, 0f);
            glow.AddComponent<AtmosphereLight>().Configure(0.8f, 2.5f);

            root.AddComponent<NightFlame>().Configure(lit);
            PrefabUtility.SaveAsPrefabAsset(root, WallTorchPrefab);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return Budget("Wall Torch", levels, 2000, 512);
    }

    private static string KitBuilding(string name, int cap, string modelPrefab, string building)
    {
        string fbx = Art + name + "/" + name + ".fbx";
        Dictionary<string, Material> materials = KitSlots();

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
                    // The kit model is life-size; the building shows it at its ModelScale (half for the big ones, the user
                    // 2026-11-14), with what stands in its space scaled alike (BuildBuildingScale).
                    float size = definition.ModelScale;
                    var modelAsset = Load<GameObject>(modelPrefab);
                    foreach (Transform child in root.transform)
                    {
                        if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) == modelAsset)
                        {
                            child.localScale = definition.ModelSize;
                        }
                    }

                    // The washing line of a lived-in house stands behind the house, and is no part of its collider.
                    Transform lived = root.transform.Find("Lived In");
                    Transform line = lived != null ? lived.Find("Line") : null;
                    if (line != null)
                    {
                        lived.localScale = definition.ModelSize;
                        float behind = (body.min.z - 1.3f) * size;
                        lived.localPosition = new Vector3(lived.localPosition.x, lived.localPosition.y, behind - line.localPosition.z * size);
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
                            data.FindProperty("chimney").vector3Value = Vector3.Scale(hearth.chimney, definition.ModelSize);
                            data.FindProperty("lightAt").vector3Value = Vector3.Scale(hearth.light, definition.ModelSize);
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
