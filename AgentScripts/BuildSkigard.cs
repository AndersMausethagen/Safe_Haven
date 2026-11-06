using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The skigard, the first fence (`unity command script run --file AgentScripts/BuildSkigard.cs --entry BuildSkigard.Apply`):
/// the old Norwegian roundpole fence and its gate, modelled and textured in Blender by Tools/Blender/skigard.py
/// (Art/Haven/Skigard). Makes the material (dark, worn spruce; snow settles on it), the Skigard and Skigard Gate prefabs
/// (from the Palisade's, so they build, save, take damage and block like it) and their buildings: 100 health each,
/// built by the player from the start with sticks and oak, the fence in lines. The gate opens for the Haven's people
/// (FenceGate). Re-runnable.
/// </summary>
public static class BuildSkigard
{
    private const string Art = "Assets/_Project/Art/Haven/Skigard/";
    private const string Structures = "Assets/_Project/Prefabs/Structures/";
    private const string Buildings = "Assets/_Project/Data/Buildings/";

    public static string Apply()
    {
        Textures();
        Material wood = WoodMaterial();
        CombatStatsDefinition stats = Stats();
        BuildingDefinition fence = Definition("Skigard", "skigard", "Skigard",
            "The old roundpole fence: split spruce laid slanting between pairs of posts. Keeps the small beasts out; anything big can break it.",
            stats, 7, true, ("Sticks", 8), ("OakWood", 3));
        BuildingDefinition gate = Definition("Skigard Gate", "skigard_gate", "Skigard Gate",
            "A gate in the skigard. It opens for the Haven's own people and stays shut to the creatures of the woods.",
            stats, 7, true, ("Sticks", 10), ("OakWood", 4)); // in lines, so it carries on from a skigard's end (and bends round with it)
        GameObject fencePrefab = FencePrefab(fence, wood);
        GameObject gatePrefab = GatePrefab(gate, wood);
        Point(fence, fencePrefab);
        Point(gate, gatePrefab);
        int listed = Register(fence) + Register(gate);
        AssetDatabase.SaveAssets();
        return $"skigard ({Bounds(fencePrefab)}), gate; {stats.GetValue(StatType.MaxHealth)} health each; {listed} added to the recipe book";
    }

    // ----- Imports and the material -----

    private static void Textures()
    {
        var albedo = (TextureImporter)AssetImporter.GetAtPath(Art + "Skigard_Albedo.png");
        albedo.textureType = TextureImporterType.Default;
        albedo.sRGBTexture = true;
        albedo.maxTextureSize = 2048;
        albedo.SaveAndReimport();
        var normal = (TextureImporter)AssetImporter.GetAtPath(Art + "Skigard_Normal.png");
        normal.textureType = TextureImporterType.NormalMap;
        normal.maxTextureSize = 2048;
        normal.SaveAndReimport();
        foreach (string model in new[] { "Skigard.fbx", "Skigard Gate.fbx" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Art + model);
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
        }

        foreach (string icon in new[] { "Skigard", "Skigard Gate" })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath($"Assets/_Project/UI/BuildingIcons/{icon}.png");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
    }

    private static Material WoodMaterial()
    {
        string path = Art + "Skigard Wood.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Safe Haven/Lit (See-Through)"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = Shader.Find("Safe Haven/Lit (See-Through)");
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Skigard_Albedo.png"));
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "Skigard_Normal.png"));
        material.SetFloat("_BumpScale", 1f);
        material.EnableKeyword("_NORMALMAP");
        material.SetFloat("_Smoothness", 0.1f);
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_SeasonSnow", 1f);
        material.EnableKeyword("_SEASON_SNOW");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static CombatStatsDefinition Stats()
    {
        string path = "Assets/_Project/Data/Combat/Stats/SkigardStats.asset";
        var stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(path);
        if (stats == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Data/Combat/Stats/WoodenStructureStats.asset", path);
            stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(path);
        }

        var data = new SerializedObject(stats);
        SerializedProperty list = data.FindProperty("stats");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("stat").enumValueIndex == (int)StatType.MaxHealth)
            {
                entry.FindPropertyRelative("value").floatValue = 100f;
            }
        }

        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stats);
        return stats;
    }

    // ----- Buildings -----

    private static BuildingDefinition Definition(string file, string id, string name, string description, CombatStatsDefinition stats,
        int order, bool inLines, params (string item, int amount)[] cost)
    {
        string path = Buildings + file + ".asset";
        var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (definition == null)
        {
            AssetDatabase.CopyAsset(Buildings + "Palisade.asset", path);
            definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        }

        var data = new SerializedObject(definition);
        data.FindProperty("id").stringValue = id;
        data.FindProperty("displayName").stringValue = name;
        data.FindProperty("description").stringValue = description;
        data.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/UI/BuildingIcons/{file}.png");
        data.FindProperty("combatStats").objectReferenceValue = stats;
        data.FindProperty("knownFromStart").boolValue = true;
        data.FindProperty("showInBuildMenu").boolValue = true;
        data.FindProperty("sortOrder").intValue = order;
        data.FindProperty("placeInLines").boolValue = inLines;
        data.FindProperty("requiredRank").intValue = 0;
        data.FindProperty("requires").arraySize = 0; // built by the player from the start, no workshop needed
        data.FindProperty("renown").intValue = 1;
        SerializedProperty items = data.FindProperty("constructionItems");
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            SerializedProperty entry = items.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("item").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ItemDefinition>($"Assets/_Project/Data/Items/Materials/{cost[i].item}.asset");
            entry.FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        data.FindProperty("constructionCost").arraySize = 0;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    // The build menu and saves know buildings through the recipe book (buildings aren't added to it by themselves).
    private static int Register(BuildingDefinition definition)
    {
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        if (book == null || book.Buildings.Contains(definition))
        {
            return 0;
        }

        var data = new SerializedObject(book);
        SerializedProperty list = data.FindProperty("buildings");
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(book);
        return 1;
    }

    private static void Point(BuildingDefinition definition, GameObject prefab)
    {
        var data = new SerializedObject(definition);
        data.FindProperty("prefab").objectReferenceValue = prefab;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    // ----- Prefabs -----

    private static Mesh MeshIn(string model, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(Art + model).OfType<Mesh>().FirstOrDefault(mesh => mesh.name == name);

    private static GameObject Start(string file)
    {
        string path = Structures + file + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            AssetDatabase.CopyAsset(Structures + "Palisade.prefab", path);
        }

        return PrefabUtility.LoadPrefabContents(path);
    }

    private static GameObject Save(GameObject root, string file)
    {
        // Its own save identity: copied from the Palisade, it would otherwise come back from a save as a Palisade.
        var saveable = root.GetComponent<SaveableEntity>();
        var data = new SerializedObject(saveable);
        data.FindProperty("prefabId").stringValue = AssetDatabase.AssetPathToGUID(Structures + file + ".prefab");
        data.FindProperty("id").stringValue = "";
        data.ApplyModifiedPropertiesWithoutUndo();
        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, Structures + file + ".prefab");
        PrefabUtility.UnloadPrefabContents(root);
        return saved;
    }

    private static void Common(GameObject root, BuildingDefinition definition, Bounds bounds)
    {
        var building = root.GetComponent<BuildingObject>();
        var data = new SerializedObject(building);
        data.FindProperty("definition").objectReferenceValue = definition;
        data.ApplyModifiedPropertiesWithoutUndo();
        // Its health comes from the Combatant's stats (100), not the Palisade's it was copied from.
        var fighter = new SerializedObject(root.GetComponent<Combatant>());
        fighter.FindProperty("baseStats").objectReferenceValue = definition.CombatStats;
        fighter.ApplyModifiedPropertiesWithoutUndo();
        Transform bar = root.transform.Find("HealthBar");
        if (bar != null)
        {
            bar.localPosition = new Vector3(bar.localPosition.x, bounds.max.y + 0.5f, bar.localPosition.z);
        }
    }

    private static void Show(Transform model, Mesh mesh, Material material)
    {
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        model.GetComponent<MeshFilter>().sharedMesh = mesh;
        model.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static GameObject FencePrefab(BuildingDefinition definition, Material material)
    {
        GameObject root = Start("Skigard");
        root.name = "Skigard";
        Mesh mesh = MeshIn("Skigard.fbx", "Skigard");
        Transform model = root.transform.Find("Model");
        Show(model, mesh, material);
        Bounds bounds = mesh.bounds;
        var box = model.GetComponent<BoxCollider>();
        box.center = new Vector3(bounds.center.x, bounds.center.y, bounds.center.z);
        box.size = new Vector3(bounds.size.x, bounds.size.y, Mathf.Max(0.3f, bounds.size.z));
        var obstacle = root.GetComponent<NavMeshObstacle>();
        obstacle.center = box.center;
        obstacle.size = new Vector3(bounds.size.x, bounds.size.y, 0.35f);
        Common(root, definition, bounds);
        return Save(root, "Skigard");
    }

    private static GameObject GatePrefab(BuildingDefinition definition, Material material)
    {
        GameObject root = Start("Skigard Gate");
        root.name = "Skigard Gate";
        Mesh frameMesh = MeshIn("Skigard Gate.fbx", "Gate Frame");
        Mesh leafMesh = MeshIn("Skigard Gate.fbx", "Gate Leaf");
        Transform model = root.transform.Find("Model");
        Show(model, frameMesh, material);
        Bounds bounds = frameMesh.bounds;
        // The frame blocks only either side of the opening; the root's own obstacle would close the way through.
        Object.DestroyImmediate(model.GetComponent<BoxCollider>());
        Object.DestroyImmediate(root.GetComponent<NavMeshObstacle>());
        foreach (Transform old in root.transform.Cast<Transform>().Where(t => t.name.StartsWith("Side") || t.name == "Leaf").ToList())
        {
            Object.DestroyImmediate(old.gameObject);
        }

        const float opening = 1.2f;
        foreach (float side in new[] { -1f, 1f })
        {
            var stub = new GameObject(side < 0 ? "Side Left" : "Side Right");
            stub.layer = model.gameObject.layer;
            stub.transform.SetParent(root.transform, false);
            float from = opening - 0.08f, to = bounds.max.x;
            stub.transform.localPosition = new Vector3(side * (from + to) / 2f, 0f, 0f);
            var collider = stub.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, bounds.center.y, 0f);
            collider.size = new Vector3(to - from, bounds.size.y, 0.35f);
            var carve = stub.AddComponent<NavMeshObstacle>();
            carve.shape = NavMeshObstacleShape.Box;
            carve.center = collider.center;
            carve.size = new Vector3(collider.size.x, collider.size.y, 0.4f);
            carve.carving = true;
            carve.carveOnlyStationary = true;
        }

        // The leaf, on its hinge (where the FBX put its pivot).
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "Skigard Gate.fbx");
        Transform pivot = source.transform.Cast<Transform>().FirstOrDefault(t => t.name == "Gate Leaf");
        var leaf = new GameObject("Leaf");
        leaf.layer = model.gameObject.layer;
        leaf.transform.SetParent(root.transform, false);
        leaf.transform.localPosition = pivot != null ? pivot.localPosition : new Vector3(-1.13f, 0f, 0f);
        leaf.AddComponent<MeshFilter>().sharedMesh = leafMesh;
        leaf.AddComponent<MeshRenderer>().sharedMaterial = material;
        var leafBox = leaf.AddComponent<BoxCollider>();
        leafBox.center = leafMesh.bounds.center;
        leafBox.size = new Vector3(leafMesh.bounds.size.x, leafMesh.bounds.size.y, 0.25f);
        var leafObstacle = leaf.AddComponent<NavMeshObstacle>();
        leafObstacle.shape = NavMeshObstacleShape.Box;
        leafObstacle.center = leafBox.center;
        leafObstacle.size = leafBox.size;
        leafObstacle.carving = false; // residents still plan their way through; it opens as they come

        if (!root.TryGetComponent(out FenceGate gate))
        {
            gate = root.AddComponent<FenceGate>();
        }

        gate.Configure(leaf.transform, 100f, Creak());
        Common(root, definition, bounds);
        return Save(root, "Skigard Gate");
    }

    // A wooden creak for the gate's hinges.
    private static SoundDefinition Creak()
    {
        foreach (string guid in AssetDatabase.FindAssets("Creak t:SoundDefinition"))
        {
            var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (sound != null && sound.name.Contains("Tree") || sound != null && sound.name.Contains("Wood"))
            {
                return sound;
            }
        }

        return null;
    }

    private static string Bounds(GameObject prefab)
    {
        var filter = prefab.GetComponentInChildren<MeshFilter>();
        return filter != null && filter.sharedMesh != null ? $"{filter.sharedMesh.bounds.size.x:0.0} x {filter.sharedMesh.bounds.size.y:0.0} m" : "?";
    }
}
