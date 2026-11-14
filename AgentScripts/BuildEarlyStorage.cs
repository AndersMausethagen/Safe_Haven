#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Early storage (`unity command run_script --file AgentScripts/BuildEarlyStorage.cs --entry BuildEarlyStorage.All`): a
/// Small Chest building (known from the start, cheap, 12 stash slots; the Storage Shed opens all 60) and a Bark Pack,
/// a small backpack crafted by hand from raw materials before the Woven Satchel. Re-running updates in place.
/// </summary>
public static class BuildEarlyStorage
{
    private const string ShedPrefab = "Assets/_Project/Prefabs/Structures/StorageShed.prefab";
    private const string ChestPrefab = "Assets/_Project/Prefabs/Structures/SmallChest.prefab";
    private const string ChestModel = "Assets/_Project/Prefabs/Raids/StashChest.prefab";
    private const string ShedDefinition = "Assets/_Project/Data/Buildings/StorageShed.asset";
    private const string ChestDefinition = "Assets/_Project/Data/Buildings/SmallChest.asset";
    private const string Materials = "Assets/_Project/Data/Items/Materials/";
    private const int ChestSlots = 12;

    public static string All()
    {
        string chest = SmallChest();
        string pack = BarkPack();
        AssetDatabase.SaveAssets();
        return chest + "\n" + pack;
    }

    private static string SmallChest()
    {
        bool made = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefab) == null;
        if (made && !AssetDatabase.CopyAsset(ShedPrefab, ChestPrefab))
        {
            return "couldn't copy the shed prefab";
        }

        if (AssetDatabase.LoadAssetAtPath<BuildingDefinition>(ChestDefinition) == null && !AssetDatabase.CopyAsset(ShedDefinition, ChestDefinition))
        {
            return "couldn't copy the shed definition";
        }

        var definition = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(ChestDefinition);
        GameObject root = PrefabUtility.LoadPrefabContents(ChestPrefab);
        try
        {
            if (made)
            {
                Transform model = root.transform.Find("Model");
                foreach (Transform child in model.Cast<Transform>().ToArray())
                {
                    Object.DestroyImmediate(child.gameObject);
                }

                var look = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ChestModel));
                PrefabUtility.UnpackPrefabInstance(look, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                foreach (Transform part in look.transform.Cast<Transform>().ToArray())
                {
                    part.SetParent(model, false);
                }

                Object.DestroyImmediate(look);
                model.localScale = Vector3.one * 0.65f;
            }

            Bounds bounds = root.GetComponentsInChildren<Renderer>().Where(r => r.GetComponentInParent<Canvas>() == null)
                .Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            var box = root.GetComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(bounds.center);
            box.size = bounds.size;
            if (root.TryGetComponent(out UnityEngine.AI.NavMeshObstacle obstacle))
            {
                obstacle.center = box.center;
                obstacle.size = box.size;
            }

            Set(root.GetComponent<StashChest>(), so => so.FindProperty("stashSlots").intValue = ChestSlots);
            Set(root.GetComponent<SaveableEntity>(), so => so.FindProperty("prefabId").stringValue = AssetDatabase.AssetPathToGUID(ChestPrefab));
            var building = root.GetComponent("BuildingObject");
            Set(building, so => so.FindProperty("definition").objectReferenceValue = definition);
            Transform bar = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "HealthBar");
            if (bar != null)
            {
                bar.localPosition = new Vector3(bar.localPosition.x, bounds.max.y - root.transform.position.y + 0.4f, bar.localPosition.z);
            }

            PrefabUtility.SaveAsPrefabAsset(root, ChestPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChestPrefab);
        Set(definition, so =>
        {
            so.FindProperty("id").stringValue = "smallchest";
            so.FindProperty("displayName").stringValue = "Small Chest";
            so.FindProperty("description").stringValue =
                $"A rough chest of planks and sticks: room for {ChestSlots} stacks of the stash. What is kept here stays home " +
                "during raids. A Storage Shed holds the whole stash.";
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.FindProperty("knownFromStart").boolValue = true;
            so.FindProperty("showInBuildMenu").boolValue = true;
            so.FindProperty("sortOrder").intValue = 1;
            so.FindProperty("yardDepth").floatValue = 0.5f;
            SerializedProperty items = so.FindProperty("constructionItems");
            items.ClearArray();
            Add(items, Materials + "OakWood.asset", 4);
            Add(items, Materials + "Sticks.asset", 6);
        });
        if (definition.Icon == null || AssetDatabase.GetAssetPath(definition.Icon).Contains("StorageShed"))
        {
            Sprite icon = IconRenderer.RenderToSprite(prefab, "Assets/_Project/UI/BuildingIcons/SmallChest.png");
            if (icon != null)
            {
                Set(definition, so => so.FindProperty("icon").objectReferenceValue = icon);
            }
        }

        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        if (!book.Buildings.Contains(definition))
        {
            Set(book, so =>
            {
                SerializedProperty list = so.FindProperty("buildings");
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
            });
        }

        var shed = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(ShedDefinition);
        Set(shed, so => so.FindProperty("description").stringValue =
            "Keeps your things safe at home: the whole stash. What is stored here never goes on a raid, and building and crafting at home can use it.");
        return $"small chest: {ChestSlots} slots, icon={definition.Icon != null}";
    }

    private static string BarkPack()
    {
        const string path = "Assets/_Project/Data/Items/BarkPack.asset";
        const string recipePath = "Assets/_Project/Data/Crafting/Recipes/BarkPack.asset";
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
            Set(item, so =>
            {
                so.FindProperty("id").stringValue = "bark_pack";
                so.FindProperty("displayName").stringValue = "Bark Pack";
                so.FindProperty("description").stringValue =
                    "Strips of bark tied to a frame of sticks with nettle fiber. Not much, but it carries a little more than your arms.";
                so.FindProperty("category").intValue = (int)ItemCategory.Equipment;
                so.FindProperty("tier").intValue = 1;
                so.FindProperty("value").intValue = 4;
                so.FindProperty("stackable").boolValue = false;
                so.FindProperty("weight").floatValue = 0.4f;
                so.FindProperty("carryCapacityBonus").intValue = 10;
                so.FindProperty("bagSlotBonus").intValue = 4;
            });
        }

        if (item.Icon == null)
        {
            Sprite icon = TintedIcon("Assets/_Project/UI/ItemIcons/FiberPack.png", "Assets/_Project/UI/ItemIcons/BarkPack.png", new Color(0.72f, 0.55f, 0.38f));
            Set(item, so => so.FindProperty("icon").objectReferenceValue = icon);
        }

        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(recipePath);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, recipePath);
            Set(recipe, so =>
            {
                so.FindProperty("id").stringValue = "barkpack";
                so.FindProperty("output").objectReferenceValue = item;
                so.FindProperty("outputAmount").intValue = 1;
                so.FindProperty("station").intValue = (int)CraftingStationType.None;
                so.FindProperty("knownFromStart").boolValue = true;
                so.FindProperty("sortOrder").intValue = 13;
                SerializedProperty items = so.FindProperty("itemCost");
                Add(items, Materials + "Sticks.asset", 6);
                Add(items, Materials + "NettleFiber.asset", 4);
            });
        }

        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        Set(book, so =>
        {
            SerializedProperty list = so.FindProperty("recipes");
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == recipe)
                {
                    return;
                }
            }

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = recipe;
        });
        return $"bark pack: +{item.CarryCapacityBonus} carry, +{item.BagSlotBonus} slots, icon={item.Icon != null}";
    }

    // The satchel's icon, recoloured: a cheaper pack that reads as a cousin of it.
    private static Sprite TintedIcon(string from, string to, Color tint)
    {
        if (!File.Exists(to))
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(from));
            Color[] pixels = source.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                float grey = (p.r + p.g + p.b) / 3f;
                pixels[i] = new Color(grey * tint.r * 1.3f, grey * tint.g * 1.3f, grey * tint.b * 1.3f, p.a);
            }

            source.SetPixels(pixels);
            File.WriteAllBytes(to, source.EncodeToPNG());
            Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(to);
            var importer = (TextureImporter)AssetImporter.GetAtPath(to);
            var original = (TextureImporter)AssetImporter.GetAtPath(from);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = original.maxTextureSize;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(to);
    }

    private static void Add(SerializedProperty list, string itemPath, int amount)
    {
        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath);
        entry.FindPropertyRelative("amount").intValue = amount;
    }

    private static void Set(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
