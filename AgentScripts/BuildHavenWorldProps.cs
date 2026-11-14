#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CC0 Poly Haven models for the Haven world (`unity command script run --file AgentScripts/BuildHavenWorldProps.cs --entry
/// BuildHavenWorldProps.Apply`), from Art/Vegetation/Haven World/Downloaded (see its LICENSE.txt):
/// <list type="bullet">
/// <item>Every download gets a source material (its base colour and GL normal map) and a mask the mesh shader reads (metallic R,
/// occlusion G, smoothness A) made from Poly Haven's ARM map (occlusion R, roughness G, metallic B).</item>
/// <item>Rock faces and scanned rocks become props on the tessellated mesh shader (VegetationPackConverter.Prop), set as the recipe's
/// mountain rocks.</item>
/// <item>The pier's straight sections and end platform become the Boat Landing: a low deck, solid to walk on, running out along +Z.</item>
/// <item>The stone fire pit, with the Misty Woods' logs and stumps, becomes the Woodland Camp, a point of interest.</item>
/// </list>
/// Re-runnable: assets already converted are kept unless VegetationPackConverter.Rebuild is set.
/// </summary>
public static class BuildHavenWorldProps
{
    private const string Downloaded = "Assets/_Project/Art/Vegetation/Haven World/Downloaded";
    private const string Folder = "Assets/_Project/Art/Vegetation/Haven World";
    private const string Prefabs = "Assets/_Project/Prefabs/World";
    private const string Woods = "Assets/_Project/Art/Vegetation/Misty Woods";

    // Download, its name in the game, the import scale (the scanned rocks are pebbles until scaled) and how it is made solid.
    private static readonly (string source, string name, float scale)[] Rocks =
    {
        ("rock_face_01", "Mountain Face A", 1f),
        ("rock_face_02", "Mountain Face B", 1f),
        ("rock_07", "Mountain Boulder", 11f),
        ("rock_09", "Mountain Slab", 18f),
    };

    public static string Apply()
    {
        var report = new List<string>();
        report.Add(Materials());
        var rocks = new List<GameObject>();
        foreach ((string source, string name, float scale) in Rocks)
        {
            Scale(source, scale);
            GameObject rock = VegetationPackConverter.Prop(Source(source), $"{Folder}/Rocks", name, solid: true);
            FitMeshCollider(rock);
            rocks.Add(rock);
        }

        report.Add($"{rocks.Count} mountain rocks");
        GameObject pit = VegetationPackConverter.Prop(Source("stone_fire_pit"), $"{Folder}/Props", "Stone Fire Pit", solid: false);
        report.Add(Landing());
        report.Add(Camp(pit));

        var recipe = AssetDatabase.LoadAssetAtPath<HavenWorldRecipe>(HavenWorldGenerator.RecipePath);
        if (recipe != null)
        {
            recipe.mountainRocks = rocks;
            recipe.landing = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Boat Landing.prefab");
            if (!recipe.pointsOfInterest.Exists(poi => poi.name == "Woodland Camp"))
            {
                recipe.pointsOfInterest.Add(new HavenPointOfInterest
                {
                    name = "Woodland Camp",
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Woodland Camp.prefab"),
                    distance = new Vector2(170f, 330f),
                    count = 2,
                    clearing = 9f,
                });
            }

            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssetIfDirty(recipe);
            report.Add("recipe: mountain rocks, landing and the camp set");
        }

        return string.Join("; ", report);
    }

    /// <summary>Source materials and masks for every download, and its model's materials remapped to them.</summary>
    public static string Materials()
    {
        int made = 0;
        foreach (string folder in AssetDatabase.GetSubFolders(Downloaded))
        {
            var materials = new Dictionary<string, Material>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                if (!file.EndsWith("_basecolor"))
                {
                    continue;
                }

                string prefix = file.Substring(0, file.Length - "_basecolor".Length);
                materials[prefix] = Material(folder, prefix, path);
                made++;
            }

            string model = AssetDatabase.FindAssets("t:Model", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            if (model == null || !(AssetImporter.GetAtPath(model) is ModelImporter importer))
            {
                continue;
            }

            bool changed = false;
            foreach (Renderer renderer in AssetDatabase.LoadAssetAtPath<GameObject>(model).GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material used in renderer.sharedMaterials)
                {
                    if (used != null && materials.TryGetValue(used.name, out Material material) && used != material)
                    {
                        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), used.name), material);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        return $"{made} source materials";
    }

    private static Material Material(string folder, string prefix, string baseColour)
    {
        string normalPath = $"{folder}/{prefix}_nor_gl.jpg";
        if (AssetImporter.GetAtPath(normalPath) is TextureImporter normal && normal.textureType != TextureImporterType.NormalMap)
        {
            normal.textureType = TextureImporterType.NormalMap;
            normal.SaveAndReimport();
        }

        string maskPath = $"{folder}/{prefix}_mask.png";
        string armPath = $"{folder}/{prefix}_arm.jpg";
        if (AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath) == null && File.Exists(armPath))
        {
            // Read straight from the file, as stored: the importer would treat it as colour.
            var arm = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            arm.LoadImage(File.ReadAllBytes(armPath));
            Color32[] pixels = arm.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                pixels[i] = new Color32(p.b, p.r, 0, (byte)(255 - p.g));
            }

            var mask = new Texture2D(arm.width, arm.height, TextureFormat.RGBA32, false, true);
            mask.SetPixels32(pixels);
            File.WriteAllBytes(maskPath, mask.EncodeToPNG());
            Object.DestroyImmediate(arm);
            Object.DestroyImmediate(mask);
            AssetDatabase.ImportAsset(maskPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(maskPath);
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
            AssetDatabase.DeleteAsset(armPath);
        }

        string path = $"{folder}/{prefix}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = prefix };
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(baseColour));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
        material.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    // The model under a plain root: the converter resets its source's root, and a single-mesh FBX keeps its centimetre scale and
    // Blender's turn there, so without the wrapper it comes out a hundredth of its size and on its side.
    private static string Source(string source)
    {
        string path = $"{Downloaded}/{source}/{source} Source.prefab";
        var root = new GameObject(source);
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Downloaded}/{source}/{source}.fbx"), root.transform);
            model.transform.localPosition = Vector3.zero;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return path;
    }

    private static void Scale(string source, float scale)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath($"{Downloaded}/{source}/{source}.fbx");
        if (!Mathf.Approximately(importer.globalScale, scale))
        {
            importer.globalScale = scale;
            importer.SaveAndReimport();
        }
    }

    // A rock face is a curved skin, not a lump: its own shape keeps the player off it where a capsule would wall off its hollow.
    private static void FitMeshCollider(GameObject prefab)
    {
        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (CapsuleCollider capsule in root.GetComponents<CapsuleCollider>())
            {
                Object.DestroyImmediate(capsule);
            }

            if (!root.TryGetComponent(out MeshCollider collider))
            {
                collider = root.AddComponent<MeshCollider>();
            }

            MeshFilter drawn = root.GetComponentsInChildren<MeshFilter>().OrderByDescending(f => f.sharedMesh != null ? f.sharedMesh.vertexCount : 0).First();
            collider.sharedMesh = LowestLod(root) ?? drawn.sharedMesh;
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Mesh LowestLod(GameObject root)
    {
        if (!root.TryGetComponent(out LODGroup group) || group.lodCount == 0)
        {
            return null;
        }

        Renderer last = group.GetLODs()[group.lodCount - 1].renderers.FirstOrDefault(r => r != null);
        return last != null && last.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
    }

    // ----- Boat Landing -----

    // The pier's three straight sections in a row (the one with a mooring post out at the water end) and its gate arch at the shore,
    // lowered from the model's high deck to a landing's, the posts running on down into the lake bed.
    private const float PierDeck = 2.65f;
    private const float DeckHeight = 0.55f;
    private const float PierShoreZ = -8.93f;
    private static readonly string[] PierParts = { "modular_wooden_pier_section_01", "modular_wooden_pier_section_02", "modular_wooden_pier_section_03", "modular_wooden_pier_section_05" };

    private static string Landing()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{Downloaded}/modular_wooden_pier/modular_wooden_pier.fbx");
        var root = new GameObject("Boat Landing");
        try
        {
            var pier = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(pier, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            pier.name = "Pier";
            foreach (Transform part in pier.transform.Cast<Transform>().ToList())
            {
                if (!PierParts.Contains(part.name))
                {
                    Object.DestroyImmediate(part.gameObject);
                }
            }

            pier.transform.SetParent(root.transform, false);
            pier.transform.localPosition = new Vector3(0f, DeckHeight - PierDeck, -PierShoreZ);
            float length = -PierShoreZ + 0.43f;
            var deck = root.AddComponent<BoxCollider>();
            deck.center = new Vector3(0f, DeckHeight - 0.06f, length * 0.5f);
            deck.size = new Vector3(2.3f, 0.12f, length);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/Boat Landing.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return $"Boat Landing rebuilt from the pier ({-PierShoreZ + 0.43f:0.0} m)";
    }

    // ----- Woodland Camp -----

    // A cold fire pit with logs and a stump to sit on, kindling and a mossy boulder to mine: someone camped here once.
    private static string Camp(GameObject pit)
    {
        var root = new GameObject("Woodland Camp");
        try
        {
            Place(root, pit, Vector3.zero, 0f);
            Place(root, Load($"{Woods}/Rocks and Logs/Rotten Log A.prefab"), new Vector3(2.3f, 0f, 0.4f), 78f);
            Place(root, Load($"{Woods}/Rocks and Logs/Mossy Log.prefab"), new Vector3(-0.5f, 0f, -3.1f), 8f);
            Place(root, Load($"{Woods}/Rocks and Logs/Stump A.prefab"), new Vector3(-2.1f, 0f, 1.2f), 40f);
            Place(root, Load($"{Woods}/Undergrowth/Branch Pile.prefab"), new Vector3(1.4f, 0f, 2.8f), 120f);
            Place(root, Load($"{Woods}/Undergrowth/Twig Pile.prefab"), new Vector3(-1.2f, 0f, 2.6f), 15f);
            Place(root, Load($"{Woods}/Rocks and Logs/Mossy Boulder A.prefab"), new Vector3(-4.2f, 0f, -2.8f), 200f);
            PrefabUtility.SaveAsPrefabAsset(root, $"{Prefabs}/Woodland Camp.prefab");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }

        return "Woodland Camp built";
    }

    private static GameObject Load(string path) =>
        AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new FileNotFoundException("No prefab at " + path);

    private static void Place(GameObject root, GameObject prefab, Vector3 at, float yaw)
    {
        var made = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
        made.transform.localPosition = at;
        made.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
