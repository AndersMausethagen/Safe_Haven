#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// World props made by LocalSpawn (Tools/LocalSpawn/props_manifest.json: rune stones, a cairn, an offering stone, a god post,
/// a troll asleep in stone) into prefabs to place anywhere (`unity command script run -- --file AgentScripts/BuildWorldProps.cs
/// --entry BuildWorldProps.Apply`), after `generate_gathering.py --group props all` has baked them to Art/World/Props.
/// Like the Haven's generated props (BuildStructureModels): Layered Lit with every map (parallax, moss from the baked
/// coverage, snow and rain), the runes glowing from the baked emission, a LODGroup sized to the top-down view and a convex
/// collider. Prefabs: Prefabs/World/Props/&lt;Name&gt;.prefab, to drag into any scene. Safe to rerun.
/// </summary>
public static class BuildWorldProps
{
    private const string Art = "Assets/_Project/Art/World/Props/";
    private const string Prefabs = "Assets/_Project/Prefabs/World/Props/";
    private const string FieldStone = "Assets/_Project/Models/PolyHaven/stone_01/stone_01.fbx";

    private sealed class Prop
    {
        public string Name;
        public float Parallax = 0.01f;
        public float Moss = 0.25f;
        public Color Tint = Color.white;
        // HDR: how bright the runes are; black for none. Read in the Haven's day and the raids' night alike.
        public Color Glow = Color.black;
        public int TextureSize = 2048;
    }

    private static readonly Prop[] Props =
    {
        new Prop { Name = "Rune Stone", Moss = 0.2f, Glow = new Color(0.55f, 1.1f, 2.6f) },
        new Prop { Name = "Rune Cairn", Moss = 0.3f, Glow = new Color(0.55f, 1.1f, 2.6f) },
        new Prop { Name = "Offering Stone", Moss = 0.35f },
        new Prop { Name = "God Post", Parallax = 0.006f, Moss = 0.15f },
        new Prop { Name = "Sleeping Troll Rock", Moss = 0.45f, Parallax = 0.014f },
    };

    public static string Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/World/Props"))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Prefabs/World", "Props");
        }

        var report = new List<string>();
        foreach (Prop prop in Props)
        {
            string folder = Art + prop.Name + "/";
            string fbx = folder + prop.Name + ".fbx";
            if (Load<GameObject>(fbx) == null)
            {
                report.Add($"{prop.Name}: not baked yet");
                continue;
            }

            Material material = Setup(prop, folder);
            Mesh[] levels = Levels(folder, fbx, prop.Name + " A");
            Mesh hull = MeshIn(fbx, prop.Name + " A Collider");
            Prefab(prop.Name, levels, material, hull);
            report.Add($"{prop.Name}: {string.Join("/", levels.Select(TriangleCount))} triangles, {levels[0].bounds.size.y:0.0} m tall{(hull != null ? "" : ", no hull")}");
        }

        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    private static Material Setup(Prop prop, string folder)
    {
        string prefix = folder + prop.Name;
        Texture(prefix + "_Albedo.png", TextureImporterType.Default, true, prop.TextureSize);
        Texture(prefix + "_Normal.png", TextureImporterType.NormalMap, false, prop.TextureSize);
        Texture(prefix + "_ORM.png", TextureImporterType.Default, false, prop.TextureSize / 2);
        Texture(prefix + "_Height.png", TextureImporterType.Default, false, prop.TextureSize / 2);
        Texture(prefix + "_BlendMask.png", TextureImporterType.Default, false, prop.TextureSize / 2);
        Texture(prefix + "_Emission.png", TextureImporterType.Default, true, prop.TextureSize / 2);
        var model = (ModelImporter)AssetImporter.GetAtPath(prefix + ".fbx");
        model.materialImportMode = ModelImporterMaterialImportMode.None;
        model.importAnimation = false;
        model.animationType = ModelImporterAnimationType.None;
        model.importCameras = false;
        model.importLights = false;
        model.isReadable = false;
        model.SaveAndReimport();

        Material scan = AssetDatabase.LoadAllAssetsAtPath(FieldStone).OfType<Material>().First();
        string path = prefix + ".mat";
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

        material.name = prop.Name;
        material.SetTextureScale("_BaseMap", Vector2.one);
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        material.SetTexture("_BaseMap", Load<Texture2D>(prefix + "_Albedo.png"));
        material.SetColor("_BaseColor", prop.Tint);
        material.SetTexture("_BumpMap", Load<Texture2D>(prefix + "_Normal.png"));
        material.SetFloat("_BumpScale", 1f);
        material.SetTexture("_ORMMap", Load<Texture2D>(prefix + "_ORM.png"));
        material.EnableKeyword("_ORMMAP");
        material.SetFloat("_Roughness", 1f);
        material.SetFloat("_Metallic", 1f); // the ORM's blue is the metallic itself
        material.SetFloat("_OcclusionStrength", 1f);
        material.SetFloat("_L2Amount", 0f);

        var height = Load<Texture2D>(prefix + "_Height.png");
        bool parallax = prop.Parallax > 0f && height != null;
        material.SetTexture("_ParallaxMap", parallax ? height : null);
        material.SetFloat("_Parallax", parallax ? prop.Parallax : 0f);
        SetKeyword(material, "_PARALLAXMAP", parallax);

        var mask = Load<Texture2D>(prefix + "_BlendMask.png");
        material.SetTexture("_BlendMask", mask);
        material.SetFloat("_L1Amount", prop.Moss);
        material.SetFloat("_L1Source", mask != null ? 3f : 4f);
        material.SetFloat("_L1Upward", 0.3f);
        material.SetFloat("_L1Patchiness", 0.5f);
        material.SetFloat("_L1PatchScale", 3f);

        // Stone and old wood get wet in the rain and take snow.
        material.SetFloat("_SeasonSnow", 1f);
        material.EnableKeyword("_SEASON_SNOW");

        var glow = Load<Texture2D>(prefix + "_Emission.png");
        bool glows = glow != null && prop.Glow.maxColorComponent > 0f;
        material.SetTexture("_EmissionMap", glows ? glow : null);
        material.SetColor("_EmissionColor", glows ? prop.Glow : Color.black);
        SetKeyword(material, "_EMISSION", glows);
        material.globalIlluminationFlags = glows ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    // The prefab: the look's levels under a LODGroup, a convex hull to walk into, a little light where the runes glow.
    private static void Prefab(string name, Mesh[] levels, Material material, Mesh hull)
    {
        string path = Prefabs + name + ".prefab";
        bool exists = Load<GameObject>(path) != null;
        GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }

            float[] heights = ScreenBudget.LodHeights(levels[0].bounds, levels.Select(TriangleCount).ToArray());
            var lods = new LOD[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                var part = new GameObject($"LOD{i}");
                part.transform.SetParent(root.transform, false);
                part.AddComponent<MeshFilter>().sharedMesh = levels[i];
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = i < levels.Length - 1 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                lods[i] = new LOD(heights[Mathf.Min(i, heights.Length - 1)], new Renderer[] { renderer });
            }

            LODGroup group = root.TryGetComponent(out LODGroup existing) ? existing : root.AddComponent<LODGroup>();
            group.SetLODs(lods);
            group.fadeMode = LODFadeMode.None;
            group.RecalculateBounds();

            MeshCollider collider = root.TryGetComponent(out MeshCollider existingCollider) ? existingCollider : root.AddComponent<MeshCollider>();
            collider.sharedMesh = hull != null ? hull : levels[levels.Length - 1];
            collider.convex = true;
            root.isStatic = true;
            PrefabUtility.SaveAsPrefabAsset(root, path);
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

    private static Mesh[] Levels(string folder, string fbx, string look)
    {
        Mesh lod0 = MeshIn(fbx, look);
        Mesh lod1 = MeshIn(fbx, look + " LOD1"), lod2 = MeshIn(fbx, look + " LOD2");
        if (lod1 != null && lod2 != null && TriangleCount(lod1) <= TriangleCount(lod0) * 0.5f && TriangleCount(lod2) <= TriangleCount(lod0) * 0.25f)
        {
            return new[] { lod0, lod1, lod2 };
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
                levels.Add(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(lighter, path);
                levels.Add(lighter);
            }
        }

        return levels.ToArray();
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
        importer.streamingMipmaps = true;
        importer.SaveAndReimport();
    }

    private static void SetKeyword(Material material, string keyword, bool on)
    {
        if (on) material.EnableKeyword(keyword);
        else material.DisableKeyword(keyword);
    }

    private static Mesh MeshIn(string model, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().FirstOrDefault(mesh => mesh.name == name);

    private static int TriangleCount(Mesh mesh)
    {
        long indices = 0;
        for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
        return (int)(indices / 3);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
