using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Fishing content (`unity command run_script --file AgentScripts/BuildFishing.cs --entry BuildFishing.Apply`): the Fishing
/// Rod (a weapon of kind Fishing Rod, crafted by hand), Grubs for bait (in rotten logs and stumps of the overgrowth), four
/// fish and their cooked dishes (campfire recipes), simple drawn icons, Resources/FishingSettings (fish table, bait, the
/// Nøkken), PlayerFishing on the player prefab and, in Safe Haven, a still tarn north-west of the base. Safe to run again.
/// Reopens the scene that was open.
/// </summary>
public static class BuildFishing
{
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Fish = "Assets/_Project/Data/Items/Fish/";
    private const string Icons = "Assets/_Project/Art/Haven/Icons/";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes/";

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";
        Folder("Assets/_Project/Data/Items", "Fish");
        Folder("Assets/_Project/Art/Haven", "Icons");
        Folder("Assets/_Project/Data", "Fishing");

        ItemDefinition grubs = Item("Grubs", Items + "Materials/Grubs.asset", Items + "Materials/Berries.asset", "grubs",
            "Fat white grubs from rotten wood. Fish can't resist them.", 2, 0.1f, 0f, 0f, Icon("Grubs", Grub));
        var made = new List<(ItemDefinition raw, ItemDefinition cooked, FishWater water, float weight, float fight, Vector2 size)>
        {
            FishPair("BrookTrout", "Brook Trout", "Speckled and quick, at home in running water.", new Color(0.55f, 0.6f, 0.45f), "Grilled Trout", FishWater.Flowing, 3f, 0.35f, new Vector2(0.3f, 1.4f)),
            FishPair("Perch", "Perch", "A striped perch. Bites anywhere, fights little.", new Color(0.6f, 0.62f, 0.3f), "Grilled Perch", FishWater.Anywhere, 4f, 0.2f, new Vector2(0.2f, 0.8f)),
            FishPair("Pike", "Pike", "A long, green ambusher from still water, all teeth.", new Color(0.35f, 0.5f, 0.3f), "Pike Steak", FishWater.Still, 1.5f, 0.75f, new Vector2(1.5f, 6f)),
            FishPair("ArcticChar", "Arctic Char", "A red-bellied char from cold, deep water. Rare and wary.", new Color(0.8f, 0.4f, 0.3f), "Smoked Char", FishWater.Still, 0.8f, 0.55f, new Vector2(0.5f, 2.5f)),
        };

        ItemDefinition rod = Rod();
        Recipe("FishingRod", rod, CraftingStationType.None, (Mat("Sticks"), 3), (Mat("NettleCord"), 2));
        foreach (var pair in made)
        {
            Recipe(pair.cooked.name, pair.cooked, CraftingStationType.Campfire, (pair.raw, 1));
        }

        AddGrubsToOvergrowth(grubs);
        Settings(grubs, made.Select(m => (m.raw, m.water, m.weight, m.fight, m.size)).ToList());
        string player = AddToPlayer();
        AssetDatabase.SaveAssets();
        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            database.CollectAll();
            EditorUtility.SetDirty(database);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:RecipeBook"))
        {
            var book = AssetDatabase.LoadAssetAtPath<RecipeBook>(AssetDatabase.GUIDToAssetPath(guid));
            book.CollectAll();
            EditorUtility.SetDirty(book);
        }

        AssetDatabase.SaveAssets();
        string tarn = Tarn();
        if (!string.IsNullOrEmpty(previous) && previous != SceneManager.GetActiveScene().path) EditorSceneManager.OpenScene(previous);
        return $"{made.Count} fish, rod, grubs, recipes; {player}; {tarn}";
    }

    // ----- Items -----

    private static (ItemDefinition, ItemDefinition, FishWater, float, float, Vector2) FishPair(string file, string label, string text, Color colour,
        string cookedLabel, FishWater water, float weight, float fight, Vector2 size)
    {
        ItemDefinition raw = Item(label, Fish + file + ".asset", Items + "Materials/Berries.asset", file.ToLowerInvariant(), text, 2, 0.6f, 6f, 0f,
            Icon(file, (x, y) => FishShape(x, y, colour)));
        string cookedFile = cookedLabel.Replace(" ", "");
        ItemDefinition cooked = Item(cookedLabel, Fish + cookedFile + ".asset", Items + "StewedBerries.asset", cookedFile.ToLowerInvariant(),
            $"{label}, cooked over the fire. Hearty and warm.", 1, 0.5f, 45f + fight * 20f, 15f, Icon(cookedFile, (x, y) => FishShape(x, y, new Color(0.65f, 0.4f, 0.2f))));
        return (raw, cooked, water, weight, fight, size);
    }

    private static ItemDefinition Item(string label, string path, string template, string id, string text, int category, float weight, float hunger, float heal, Sprite icon)
    {
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) == null) AssetDatabase.CopyAsset(template, path);
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = id;
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        so.FindProperty("category").intValue = category;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("hungerRestore").floatValue = hunger;
        so.FindProperty("healAmount").floatValue = heal;
        so.FindProperty("thirstRestore").floatValue = 0f;
        so.FindProperty("icon").objectReferenceValue = icon;
        so.FindProperty("stackable").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static ItemDefinition Rod()
    {
        const string weaponPath = "Assets/_Project/Data/Combat/Weapons/FishingRod.asset";
        if (AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath) == null) AssetDatabase.CopyAsset("Assets/_Project/Data/Combat/Weapons/WoodsmansAxe.asset", weaponPath);
        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath);
        var weaponData = new SerializedObject(weapon);
        weaponData.FindProperty("displayName").stringValue = "Fishing Rod";
        weaponData.FindProperty("kind").intValue = (int)WeaponKind.FishingRod;
        weaponData.FindProperty("stance").intValue = (int)WeaponStance.OneHanded;
        weaponData.FindProperty("damage").floatValue = 2f;
        weaponData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weapon);

        string path = Items + "FishingRod.asset";
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) == null) AssetDatabase.CopyAsset(Items + "WoodsmansAxe.asset", path);
        var rod = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        var so = new SerializedObject(rod);
        so.FindProperty("id").stringValue = "fishing_rod";
        so.FindProperty("displayName").stringValue = "Fishing Rod";
        so.FindProperty("description").stringValue = "A springy birch rod with a nettle-cord line. Hold the attack button to swing back, let go to cast. Needs bait.";
        so.FindProperty("weapon").objectReferenceValue = weapon;
        so.FindProperty("toolType").intValue = 0;
        so.FindProperty("toolTier").intValue = 0;
        so.FindProperty("icon").objectReferenceValue = Icon("FishingRod", RodShape);
        so.FindProperty("heldModel").objectReferenceValue = RodModel();
        so.FindProperty("heldPosition").vector3Value = Vector3.zero;
        so.FindProperty("heldRotation").vector3Value = Vector3.zero;
        so.FindProperty("heldScale").floatValue = 1f;
        so.FindProperty("statBonuses").ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rod);
        return rod;
    }

    private static GameObject RodModel()
    {
        const string path = "Assets/_Project/Art/Haven/Models/Fishing Rod Model.prefab";
        var root = new GameObject("Fishing Rod Model");
        try
        {
            Material wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Forest/Materials/Dead Wood.mat");
            GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(pole.GetComponent<Collider>());
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            pole.transform.localScale = new Vector3(0.03f, 0.85f, 0.03f);
            pole.GetComponent<Renderer>().sharedMaterial = wood;
            GameObject reel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(reel.GetComponent<Collider>());
            reel.transform.SetParent(root.transform, false);
            reel.transform.localPosition = new Vector3(0.04f, 0.2f, 0f);
            reel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            reel.transform.localScale = new Vector3(0.07f, 0.02f, 0.07f);
            reel.GetComponent<Renderer>().sharedMaterial = wood;
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Recipe(string file, ItemDefinition output, CraftingStationType station, params (ItemDefinition item, int amount)[] cost)
    {
        string path = Recipes + file + ".asset";
        if (AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path) == null) AssetDatabase.CopyAsset(Recipes + "StewedBerries.asset", path);
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
        var so = new SerializedObject(recipe);
        so.FindProperty("id").stringValue = file.ToLowerInvariant();
        so.FindProperty("output").objectReferenceValue = output;
        so.FindProperty("outputAmount").intValue = 1;
        SerializedProperty items = so.FindProperty("itemCost");
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = cost[i].item;
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.FindProperty("station").intValue = (int)station;
        so.FindProperty("craftSeconds").floatValue = station == CraftingStationType.None ? 0f : 8f;
        so.FindProperty("knownFromStart").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }

    private static void AddGrubsToOvergrowth(ItemDefinition grubs)
    {
        foreach (string file in new[] { "FallenLog", "OldStump" })
        {
            var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>($"Assets/_Project/Data/Gathering/Overgrowth/{file}.asset");
            if (node == null) continue;
            var so = new SerializedObject(node);
            SerializedProperty entries = so.FindProperty("yields").FindPropertyRelative("entries");
            bool has = Enumerable.Range(0, entries.arraySize).Any(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == grubs);
            if (has) continue;
            entries.InsertArrayElementAtIndex(entries.arraySize);
            SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = grubs;
            entry.FindPropertyRelative("resource").objectReferenceValue = null;
            entry.FindPropertyRelative("minQuantity").intValue = 1;
            entry.FindPropertyRelative("maxQuantity").intValue = 3;
            entry.FindPropertyRelative("probability").floatValue = 0.8f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(node);
        }
    }

    private static void Settings(ItemDefinition grubs, List<(ItemDefinition item, FishWater water, float weight, float fight, Vector2 size)> fish)
    {
        const string path = "Assets/_Project/Resources/FishingSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<FishingSettings>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<FishingSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        // How long each kind runs (FishSizes: 10-200 cm, most 20-60): a perch small, a pike big.
        float LengthOf(ItemDefinition item) => item == null ? 1f : item.name == "Perch" ? 0.75f : item.name == "Pike" ? 1.5f : item.name == "ArcticChar" ? 1.1f : 1f;
        settings.Configure(fish.Select(f => new FishingSettings.Fish { item = f.item, water = f.water, weight = f.weight, fight = f.fight, size = f.size, lengthScale = LengthOf(f.item) }),
            new[]
            {
                // Grubs bring the big ones on and take some of their fight; berries only pass the time.
                new FishingSettings.Bait { item = grubs, lure = 1.4f, ease = 0.35f },
                new FishingSettings.Bait { item = Mat("Berries"), lure = 0.8f, ease = 0f },
            }, 0.06f);

        const string eventPath = "Assets/_Project/Data/Fishing/MetTheNokken.asset";
        var met = AssetDatabase.LoadAssetAtPath<GameEventDefinition>(eventPath);
        if (met == null)
        {
            met = ScriptableObject.CreateInstance<GameEventDefinition>();
            AssetDatabase.CreateAsset(met, eventPath);
        }

        var metData = new SerializedObject(met);
        metData.FindProperty("id").stringValue = "fishing.met_the_nokken";
        metData.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(settings);
        so.FindProperty("nokkenModel").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/Huldra.prefab");
        so.FindProperty("nokkenScream").objectReferenceValue = Sound("Thunder");
        so.FindProperty("nokkenSplash").objectReferenceValue = Sound("Footstep Water");
        so.FindProperty("metNokken").objectReferenceValue = met;
        so.FindProperty("castSound").objectReferenceValue = Sound("Swing");
        so.FindProperty("biteSound").objectReferenceValue = Sound("Footstep Water");
        so.FindProperty("catchSound").objectReferenceValue = Sound("Crafted");
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        EditorUtility.SetDirty(met);
    }

    private static string AddToPlayer()
    {
        const string path = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<PlayerFishing>() != null) return "player can fish";
            root.AddComponent<PlayerFishing>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return "PlayerFishing on the player";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ----- The tarn -----

    private static string Tarn()
    {
        Scene scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/SafeHaven.unity");
        GameObject old = GameObject.Find("Tarn");
        if (old != null) return "the tarn is already there";
        Terrain terrain = Object.FindObjectsByType<Terrain>().First();
        // In the open north-west of the clearing, past the portal: outside the ward, clear of the groves.
        var centre = new Vector3(-44f, 0f, 30f);
        centre.y = terrain.SampleHeight(centre) + terrain.transform.position.y;
        WaterSetup.Set set = WaterSetup.EnsureAssets();
        GameObject water = WaterSetup.CreateWater("Tarn", set.Still, centre + Vector3.down * 0.45f, new Vector3(1.8f, 1f, 1.3f));
        water.GetComponent<WaterSurface>().IsStill = true;
        WaterSetup.Carve(terrain, water.transform, 2.2f, 3.5f, null, true);
        GrassSetup.ClearUnderWater(terrain);
        WorldBuilder.BuildChanged(true);
        // Baked into its own asset (as BuildHavenWorld's HavenNavMesh does), so the scene stays text.
        const string navMesh = "Assets/_Project/Art/Terrain/SafeHaven/SafeHavenTerrainNavMesh.asset";
        foreach (NavMeshSurface surface in Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include))
        {
            surface.BuildNavMesh();
            UnityEngine.AI.NavMeshData data = surface.navMeshData;
            AssetDatabase.DeleteAsset(navMesh);
            AssetDatabase.CreateAsset(data, navMesh);
            surface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(navMesh);
            EditorUtility.SetDirty(surface);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"tarn at {centre:F0}";
    }

    // ----- Icons (drawn, until real art) -----

    private delegate Color Shape(float x, float y);

    private static Sprite Icon(string name, Shape shape)
    {
        string path = Icons + name + ".png";
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, shape((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f));
            }
        }

        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Color FishShape(float x, float y, Color colour)
    {
        float body = (x * x) / 0.42f + (y * y) / 0.09f;
        float tail = x > 0.55f && x < 0.92f && Mathf.Abs(y) < (x - 0.55f) * 1.1f ? 1f : 0f;
        bool eye = (x + 0.42f) * (x + 0.42f) + (y - 0.06f) * (y - 0.06f) < 0.004f;
        if (eye) return new Color(0.05f, 0.05f, 0.05f, 1f);
        if (body <= 1f || tail > 0f)
        {
            float shade = 0.75f + 0.35f * Mathf.Clamp01(y + 0.3f);
            return new Color(colour.r * shade, colour.g * shade, colour.b * shade, 1f);
        }

        return Color.clear;
    }

    private static Color Grub(float x, float y)
    {
        for (int i = 0; i < 3; i++)
        {
            float cx = -0.4f + i * 0.4f, cy = (i - 1) * 0.15f;
            float d = (x - cx) * (x - cx) / 0.09f + (y - cy) * (y - cy) / 0.03f;
            if (d <= 1f) return new Color(0.92f, 0.88f, 0.75f) * (0.85f + 0.15f * Mathf.Sin((x + y) * 30f));
        }

        return Color.clear;
    }

    private static Color RodShape(float x, float y)
    {
        float along = (x + y) * 0.7071f;
        float across = (x - y) * 0.7071f;
        if (Mathf.Abs(across) < 0.05f && along > -0.9f && along < 0.9f) return new Color(0.55f, 0.38f, 0.2f);
        if (Mathf.Abs(across + 0.12f) < 0.08f && Mathf.Abs(along + 0.5f) < 0.08f) return new Color(0.4f, 0.4f, 0.42f);
        if (Mathf.Abs(x - 0.62f) < 0.01f && y > -0.6f && y < 0.62f) return new Color(0.9f, 0.9f, 0.85f);
        return Color.clear;
    }

    private static ItemDefinition Mat(string name) =>
        AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "Materials/" + name + ".asset") ?? AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + name + ".asset");

    private static SoundDefinition Sound(string name) => AssetDatabase.LoadAssetAtPath<SoundDefinition>($"Assets/_Project/Audio/Sounds/Library/{name}.asset");

    private static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }
}
