using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>
/// Food and home content (`unity command run_script --file AgentScripts/BuildFood.cs --entry BuildFood.Apply`): ingredients
/// and the wild plants they come from, meals with lasting bonuses (GDD §11.4), tonics and antidotes, the stations they are
/// made at and the rest of the buildings of GDD §14.2: Kitchen, Tanning Rack, Farm Plot (a seed crop queued at the station
/// grows on its own, during raids too), Beehive, Well, Sauna, Watch Post, Spike Trap and Cottage. Also puts PlayerBoons on
/// the player and PlayerConditionsView on the vitals HUD. Run BuildGear first. Safe to run again.
/// </summary>
public static class BuildFood
{
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Materials = Items + "Materials/";
    private const string Food = Items + "Food/";
    private const string Status = "Assets/_Project/Data/Combat/Status/";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes/";
    private const string Icons = "Assets/_Project/Art/Items/Gear/Icons/";
    private const string Gathering = "Assets/_Project/Data/Gathering/";
    private const string Buildings = "Assets/_Project/Data/Buildings/";
    private const string Models = "Assets/_Project/Art/Haven/Models/";
    private const string Tints = "Assets/_Project/Materials/Haven/";
    private const string DeadWood = "Assets/_Project/Art/Forest/Materials/Dead Wood.mat";
    private const string Stone = "Assets/_Project/Data/Prologue/Shelter Stone.mat";

    public static string Apply()
    {
        PrefabBuildUtility.EnsureFolder(Food.TrimEnd('/'));
        int ingredients = Ingredients();
        int nodes = Nodes();
        int meals = Meals();
        int tonics = Tonics();
        int station = StationRecipes();
        AssetDatabase.SaveAssets();
        int buildings = NewBuildings();
        string wired = WirePlayer();
        AssetDatabase.SaveAssets();
        Refresh();
        return $"ingredients {ingredients}, nodes {nodes}, meals {meals}, tonics {tonics}, station recipes {station}, buildings {buildings}; {wired}";
    }

    // ----- Ingredients -----

    private static int Ingredients()
    {
        Ingredient("Chanterelle", "Chanterelle", "Golden mushrooms from the mossy shade. They smell faintly of apricots.", 1, 2, 0.2f, 4f, 0f, IconShape.Mushroom, new Color(0.92f, 0.7f, 0.25f), new Color(0.95f, 0.85f, 0.6f));
        Ingredient("WildTurnip", "Wild Turnip", "Tough and sweet. Plant one at a Farm Plot and it gives you more.", 1, 2, 0.5f, 5f, 0f, IconShape.Turnip, new Color(0.85f, 0.75f, 0.85f), new Color(0.35f, 0.55f, 0.25f));
        Ingredient("Barley", "Barley", "Wild barley heads. Bread, broth and mead start here; sow some at a Farm Plot.", 1, 2, 0.2f, 0f, 0f, IconShape.Grain, new Color(0.85f, 0.72f, 0.35f), Color.white);
        Ingredient("Cloudberries", "Cloudberries", "Amber berries from the wet ground. Worth getting your boots wet for.", 2, 5, 0.2f, 8f, 2f, IconShape.Ore, new Color(0.45f, 0.55f, 0.3f), new Color(0.95f, 0.6f, 0.2f));
        Ingredient("Honey", "Honey", "From your own hives, or a wild comb if you're quick.", 2, 6, 0.4f, 6f, 2f, IconShape.Jar, new Color(0.9f, 0.65f, 0.15f), new Color(0.55f, 0.38f, 0.22f));
        Ingredient("BoarMeat", "Boar Meat", "Dark, lean meat from a Blight Boar. Cook it well.", 1, 3, 0.8f, 6f, 0f, IconShape.Meat, new Color(0.65f, 0.3f, 0.25f), new Color(0.9f, 0.85f, 0.75f));
        return 6;
    }

    private static void Ingredient(string file, string label, string text, int tier, int value, float weight, float hunger, float thirst, IconShape shape, Color primary, Color secondary)
    {
        Make(Materials + file + ".asset", label, text, ItemCategory.Material, tier, ItemRarity.Common, value, weight, IconFor(file, shape, primary, secondary), so =>
        {
            so.FindProperty("hungerRestore").floatValue = hunger;
            so.FindProperty("thirstRestore").floatValue = thirst;
            so.FindProperty("maximumStack").intValue = 50;
        });
    }

    // ----- Wild plants -----

    private static int Nodes()
    {
        Material leaf = Tint("Plant Leaf", new Color(0.26f, 0.42f, 0.2f));
        Material golden = Tint("Chanterelle Cap", new Color(0.9f, 0.66f, 0.2f));
        Material stem = Tint("Pale Stem", new Color(0.88f, 0.8f, 0.6f));
        Material straw = Tint("Barley Straw", new Color(0.78f, 0.66f, 0.32f));
        Material amber = Tint("Cloudberry", new Color(0.95f, 0.58f, 0.18f));
        Material root = Tint("Turnip Root", new Color(0.82f, 0.7f, 0.82f));

        Node("Chanterelles", "Chanterelles", "Chanterelle", 2, 4, ("Chanterelle Patch", root2 =>
        {
            for (int i = 0; i < 5; i++)
            {
                float angle = i * 2.4f;
                var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.12f + i * 0.05f);
                float size = 0.8f + (i % 3) * 0.2f;
                Piece(root2, PrimitiveType.Cylinder, at + Vector3.up * 0.06f * size, new Vector3(0.05f, 0.06f, 0.05f) * size, stem);
                Piece(root2, PrimitiveType.Sphere, at + Vector3.up * 0.13f * size, new Vector3(0.2f, 0.09f, 0.2f) * size, golden);
            }
        }));
        Node("WildTurnips", "Wild Turnips", "WildTurnip", 1, 3, ("Wild Turnip Clump", root2 =>
        {
            for (int i = 0; i < 3; i++)
            {
                var at = new Vector3(-0.2f + i * 0.2f, 0f, i % 2 == 0 ? 0.08f : -0.08f);
                Piece(root2, PrimitiveType.Sphere, at + Vector3.up * 0.06f, new Vector3(0.2f, 0.17f, 0.2f), root);
                Piece(root2, PrimitiveType.Cube, at + new Vector3(0.03f, 0.25f, 0f), new Vector3(0.08f, 0.32f, 0.02f), leaf).localRotation = Quaternion.Euler(0f, 0f, -12f);
                Piece(root2, PrimitiveType.Cube, at + new Vector3(-0.03f, 0.25f, 0f), new Vector3(0.08f, 0.32f, 0.02f), leaf).localRotation = Quaternion.Euler(0f, 0f, 12f);
            }
        }));
        Node("WildBarley", "Wild Barley", "Barley", 2, 4, ("Wild Barley Stand", root2 =>
        {
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 2.2f;
                var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.08f + i * 0.04f);
                Piece(root2, PrimitiveType.Cylinder, at + Vector3.up * 0.4f, new Vector3(0.018f, 0.4f, 0.018f), straw);
                Piece(root2, PrimitiveType.Sphere, at + Vector3.up * 0.84f, new Vector3(0.05f, 0.11f, 0.05f), straw);
            }
        }));
        Node("CloudberryPatch", "Cloudberry Patch", "Cloudberries", 1, 3, ("Cloudberry Patch", root2 =>
        {
            Piece(root2, PrimitiveType.Sphere, Vector3.up * 0.07f, new Vector3(0.62f, 0.16f, 0.62f), leaf);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * 2.4f;
                var at = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.08f + i * 0.04f);
                Piece(root2, PrimitiveType.Sphere, at + Vector3.up * 0.18f, Vector3.one * 0.08f, amber);
            }
        }));
        return 4;
    }

    private static void Node(string file, string label, string yield, int min, int max, (string name, Action<Transform> shape) model)
    {
        string path = Gathering + file + ".asset";
        if (AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(Gathering + "Berries.asset", path);
        }

        var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(path);
        GameObject modelAsset = SaveModel(model.name, model.shape);
        var so = new SerializedObject(node);
        so.FindProperty("id").stringValue = file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("tier").intValue = 1;
        SerializedProperty entries = so.FindProperty("yields").FindPropertyRelative("entries");
        entries.arraySize = 1;
        SerializedProperty entry = entries.GetArrayElementAtIndex(0);
        entry.FindPropertyRelative("item").objectReferenceValue = FindItem(yield);
        entry.FindPropertyRelative("resource").objectReferenceValue = null;
        entry.FindPropertyRelative("minQuantity").intValue = min;
        entry.FindPropertyRelative("maxQuantity").intValue = max;
        entry.FindPropertyRelative("probability").floatValue = 1f;
        so.FindProperty("worldModelPrefab").objectReferenceValue = modelAsset;
        so.FindProperty("modelVariants").ClearArray();
        bool needsPrefab = so.FindProperty("worldPrefab").objectReferenceValue == null
                           || AssetDatabase.GetAssetPath(so.FindProperty("worldPrefab").objectReferenceValue).Contains("Berries");
        so.FindProperty("worldPrefab").objectReferenceValue = needsPrefab ? null : so.FindProperty("worldPrefab").objectReferenceValue;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(node);
        if (needsPrefab)
        {
            GameObject prefab = ResourceNodePrefabBuilder.Create(node, modelAsset);
            so.Update();
            so.FindProperty("worldPrefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ----- Meals: simple food fills you up, cooked dishes also carry you through a raid. Two meal slots, so it's a choice. -----

    private static int Meals()
    {
        int made = 0;
        void Dish(string file, string label, string text, int tier, int value, IconShape shape, Color color, Color trim, CraftingStationType station, float seconds, int amount,
            float heal, float hunger, float thirst, float boonSeconds, StatBonus[] boon, params (string item, int amount)[] cost)
        {
            ItemDefinition item = Make(Food + file + ".asset", label, text, ItemCategory.Consumable, tier, ItemRarity.Common, value, 0.5f, IconFor(file, shape, color, trim), so =>
            {
                so.FindProperty("healAmount").floatValue = heal;
                so.FindProperty("hungerRestore").floatValue = hunger;
                so.FindProperty("thirstRestore").floatValue = thirst;
                so.FindProperty("maximumStack").intValue = 10;
                so.FindProperty("boonSeconds").floatValue = boonSeconds;
                so.FindProperty("tonic").boolValue = false;
                SetBonuses(so, "boonBonuses", boon);
            });
            Recipe(file, item, amount, station, seconds, cost);
            made++;
        }

        var none = new StatBonus[0];
        var soup = new Color(0.75f, 0.5f, 0.25f);
        var fire = CraftingStationType.Campfire;
        var kitchen = CraftingStationType.Kitchen;
        Dish("MushroomSkewer", "Mushroom Skewer", "Chanterelles on a stick, charred at the edges.", 1, 3, IconShape.Mushroom, new Color(0.7f, 0.45f, 0.15f), new Color(0.55f, 0.38f, 0.22f), fire, 6f, 1,
            10f, 25f, 0f, 0f, none, ("Chanterelle", 3), ("Sticks", 1));
        Dish("BakedTurnip", "Baked Turnip", "Black outside, sweet inside. Nobody ever wrote a song about it.", 1, 3, IconShape.Turnip, new Color(0.65f, 0.45f, 0.3f), new Color(0.3f, 0.3f, 0.2f), fire, 6f, 1,
            5f, 30f, 0f, 0f, none, ("WildTurnip", 2));
        Dish("RoastBoar", "Roast Boar", "Fat spitting in the fire. You feel it in your arms for a while.", 1, 8, IconShape.Meat, new Color(0.55f, 0.3f, 0.15f), new Color(0.9f, 0.85f, 0.75f), fire, 10f, 1,
            20f, 55f, 0f, 300f, new[] { new StatBonus(StatType.Strength, 3f) }, ("BoarMeat", 2));
        Dish("BarleyBread", "Barley Bread", "Dense, dark and good for the long walk.", 1, 6, IconShape.Bread, new Color(0.62f, 0.42f, 0.22f), Color.white, kitchen, 14f, 2,
            5f, 40f, 0f, 420f, new[] { new StatBonus(StatType.MaxStamina, 15f) }, ("Barley", 2), ("Waterskin", 1));
        Dish("HuntersStew", "Hunter's Stew", "Everything from the pack in one pot. It sits heavy and it keeps you standing.", 2, 14, IconShape.Bowl, soup, new Color(0.5f, 0.3f, 0.15f), kitchen, 18f, 1,
            15f, 70f, 20f, 480f, new[] { new StatBonus(StatType.MaxHealth, 25f) }, ("BoarMeat", 1), ("WildTurnip", 1), ("Chanterelle", 2), ("Waterskin", 1));
        Dish("FishSoup", "Fish Soup", "Perch, turnip and a hot bowl to hold. Warmth that lasts.", 2, 12, IconShape.Bowl, new Color(0.9f, 0.82f, 0.62f), new Color(0.85f, 0.8f, 0.6f), kitchen, 16f, 1,
            20f, 50f, 25f, 480f, new[] { new StatBonus(StatType.FrostResistance, 0.15f) }, ("Perch", 1), ("WildTurnip", 1), ("Waterskin", 1));
        Dish("CloudberryCream", "Cloudberry Cream", "Tart and golden. Your feet feel lighter than your pack.", 2, 16, IconShape.Bowl, new Color(0.95f, 0.65f, 0.2f), new Color(0.98f, 0.9f, 0.7f), kitchen, 12f, 1,
            0f, 25f, 5f, 300f, new[] { new StatBonus(StatType.MoveSpeed, 0.06f) }, ("Cloudberries", 3), ("Honey", 1));
        Dish("ForagersBroth", "Forager's Broth", "Thin but clever: you notice what you'd have walked past.", 1, 8, IconShape.Bowl, new Color(0.7f, 0.65f, 0.35f), new Color(0.5f, 0.55f, 0.3f), kitchen, 12f, 1,
            0f, 30f, 20f, 480f, new[] { new StatBonus(StatType.GatherYield, 0.15f) }, ("Chanterelle", 3), ("Barley", 1), ("Waterskin", 1));
        Dish("HoneyCake", "Honey Cake", "Sticky, filling and impossible to be afraid while eating.", 2, 14, IconShape.Bread, new Color(0.85f, 0.6f, 0.2f), Color.white, kitchen, 16f, 2,
            10f, 35f, 0f, 420f, new[] { new StatBonus(StatType.Armor, 8f) }, ("Barley", 2), ("Honey", 1), ("Berries", 2));
        Dish("SmokedBoar", "Smoked Boar", "Keeps for weeks; so does what it does for you.", 2, 16, IconShape.Meat, new Color(0.4f, 0.22f, 0.15f), new Color(0.9f, 0.85f, 0.75f), kitchen, 20f, 2,
            10f, 60f, 0f, 600f, new[] { new StatBonus(StatType.Strength, 2f), new StatBonus(StatType.MaxHealth, 10f) }, ("BoarMeat", 3), ("OakWood", 1));
        Dish("HoneyMead", "Honey Mead", "Courage, at a price: you hit harder and care less about being hit.", 2, 18, IconShape.Mug, new Color(0.85f, 0.65f, 0.2f), Color.white, kitchen, 16f, 1,
            0f, 5f, 40f, 240f, new[] { new StatBonus(StatType.DamageBonus, 0.1f), new StatBonus(StatType.Armor, -5f) }, ("Honey", 2), ("Barley", 1), ("Waterskin", 1));
        return made;
    }

    // ----- Tonics: from the Cauldron, for the fight rather than the road. They take no meal slot. -----

    private static int Tonics()
    {
        var poison = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(Status + "Poison.asset");
        var chill = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(Status + "Chill.asset");
        int made = 0;
        void Tonic(string file, string label, string text, int tier, ItemRarity rarity, int value, Color color, float heal, float seconds, StatBonus[] boon, StatusEffectDefinition cure,
            params (string item, int amount)[] cost)
        {
            ItemDefinition item = Make(Food + file + ".asset", label, text, ItemCategory.Consumable, tier, rarity, value, 0.3f, IconFor(file, IconShape.Potion, color, Color.white), so =>
            {
                so.FindProperty("healAmount").floatValue = heal;
                so.FindProperty("maximumStack").intValue = 10;
                so.FindProperty("boonSeconds").floatValue = seconds;
                so.FindProperty("tonic").boolValue = true;
                SetBonuses(so, "boonBonuses", boon);
                SerializedProperty cures = so.FindProperty("cures");
                cures.arraySize = cure != null ? 1 : 0;
                if (cure != null)
                {
                    cures.GetArrayElementAtIndex(0).objectReferenceValue = cure;
                }
            });
            Recipe(file, item, 1, CraftingStationType.AlchemyTable, 10f, cost);
            made++;
        }

        var none = new StatBonus[0];
        Tonic("Antidote", "Antidote", "Bog Venom brewed thin with healing herbs. Clears poison from the blood.", 2, ItemRarity.Common, 10, new Color(0.55f, 0.85f, 0.4f), 10f, 0f, none, poison,
            ("BogVenom", 1), ("HealingHerb", 2));
        Tonic("WarmingBrew", "Warming Brew", "Mushrooms and honey, hot. Thaws a chill and keeps the next one out for a while.", 2, ItemRarity.Common, 12, new Color(0.9f, 0.55f, 0.2f), 0f, 240f,
            new[] { new StatBonus(StatType.FrostResistance, 0.12f) }, chill, ("Chanterelle", 2), ("Honey", 1));
        Tonic("BearTonic", "Bear Tonic", "Bitter, thick and strong. You could run all day; you'll pay for it tomorrow.", 2, ItemRarity.Uncommon, 18, new Color(0.65f, 0.35f, 0.2f), 0f, 180f,
            new[] { new StatBonus(StatType.MaxStamina, 25f) }, null, ("HealingHerb", 2), ("Honey", 1));
        Tonic("BarkskinDraught", "Barkskin Draught", "Rootling bark in hot water. Your skin feels thick and a little woody.", 2, ItemRarity.Uncommon, 18, new Color(0.5f, 0.38f, 0.2f), 0f, 180f,
            new[] { new StatBonus(StatType.Armor, 14f) }, null, ("RootlingBark", 3), ("Granite", 1));
        Tonic("SwiftfootTonic", "Swiftfoot Tonic", "A Wisp Ember dissolved in cloudberry syrup. The ground seems to pull you along.", 3, ItemRarity.Rare, 34, new Color(0.6f, 0.85f, 1f), 0f, 120f,
            new[] { new StatBonus(StatType.MoveSpeed, 0.12f) }, null, ("WispEmber", 1), ("Cloudberries", 2));
        return made;
    }

    // ----- Stations: tanning, farming, bees -----

    private static int StationRecipes()
    {
        int made = 0;
        void Station(string file, string output, int amount, CraftingStationType station, float seconds, params (string item, int amount)[] cost)
        {
            Recipe(file, FindItem(output), amount, station, seconds, cost);
            made++;
        }

        Station("CuredLeather", "CuredLeather", 1, CraftingStationType.Tannery, 10f, ("RoughHide", 2), ("Sticks", 1));
        Station("CuredThickHide", "CuredLeather", 2, CraftingStationType.Tannery, 12f, ("ThickHide", 1), ("Sticks", 1));
        // Crops take their time, and a queue keeps growing while the player is out on a raid.
        Station("FarmTurnips", "WildTurnip", 4, CraftingStationType.Farm, 240f, ("WildTurnip", 1));
        Station("FarmBarley", "Barley", 4, CraftingStationType.Farm, 300f, ("Barley", 1));
        Station("FarmFlax", "Flax", 3, CraftingStationType.Farm, 300f, ("Flax", 1));
        Station("FarmHealingHerb", "HealingHerb", 3, CraftingStationType.Farm, 360f, ("HealingHerb", 1));
        Station("HiveHoney", "Honey", 2, CraftingStationType.Beehive, 240f, ("Berries", 3));
        return made;
    }

    // ----- Buildings -----

    private static int NewBuildings()
    {
        Material soil = Tint("Tilled Soil", new Color(0.22f, 0.15f, 0.1f));
        Material leaf = Tint("Plant Leaf", new Color(0.26f, 0.42f, 0.2f));
        Material hide = Tint("Stretched Hide", new Color(0.55f, 0.42f, 0.28f));
        Material thatch = Tint("Thatch", new Color(0.62f, 0.52f, 0.3f));
        Material iron = Tint("Dark Iron", new Color(0.2f, 0.21f, 0.24f));
        Material hive = Tint("Hive Straw", new Color(0.75f, 0.6f, 0.3f));
        Material wood = AssetDatabase.LoadAssetAtPath<Material>(DeadWood);
        Material stone = AssetDatabase.LoadAssetAtPath<Material>(Stone);

        Building("Kitchen", "Kitchen", "Proper hearth, proper pots. Cooked here, food gives lasting bonuses.", 12, CraftingStationType.Kitchen, 8f, 8, 2f, false,
            new[] { ("OakPlank", 10), ("StoneBlock", 6), ("CopperIngot", 2) }, root =>
            {
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.9f, 1.1f), stone);
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f), new Vector3(2.3f, 0.08f, 1.2f), wood);
                Piece(root, PrimitiveType.Cube, new Vector3(-0.75f, 1.75f, 0.1f), new Vector3(0.5f, 1.6f, 0.5f), stone);
                Piece(root, PrimitiveType.Cylinder, new Vector3(0.5f, 1.12f, 0f), new Vector3(0.5f, 0.17f, 0.5f), iron);
            },
            Level("Sturdy", "Shelves for what you've learned to keep.", 4, ("OakPlank", 6), ("GreyStone", 6), 0.3f, 1),
            Level("Master's", "Smoke hooks, a salt box and a cook's patience.", 6, ("OakPlank", 8), ("IronIngot", 2), 0.3f, 2));
        Building("TanningRack", "Tanning Rack", "A frame for stretching hides. Rough hide goes in, Cured Leather comes out.", 13, CraftingStationType.Tannery, 4f, 4, 1.5f, false,
            new[] { ("OakWood", 6), ("NettleCord", 4) }, root =>
            {
                Piece(root, PrimitiveType.Cube, new Vector3(-0.8f, 0.9f, 0f), new Vector3(0.1f, 1.8f, 0.1f), wood);
                Piece(root, PrimitiveType.Cube, new Vector3(0.8f, 0.9f, 0f), new Vector3(0.1f, 1.8f, 0.1f), wood);
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 1.7f, 0f), new Vector3(1.8f, 0.1f, 0.1f), wood);
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f), new Vector3(1.4f, 1.1f, 0.04f), hide);
            },
            Level("Sturdy", "Better pegs, tighter stretch.", 3, ("OakPlank", 4), ("NettleCord", 2), 0.3f, 1));
        Building("FarmPlot", "Farm Plot", "Turned earth. Plant a turnip or barley from the wild here and it grows while you're away, even during raids.", 14, CraftingStationType.Farm, 3f, 4, 0f, false,
            new[] { ("OakWood", 4), ("Sticks", 4) }, root =>
            {
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.08f, 0f), new Vector3(3f, 0.16f, 2.2f), soil);
                for (int row = 0; row < 3; row++)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Piece(root, PrimitiveType.Sphere, new Vector3(-1.1f + i * 0.55f, 0.22f, -0.7f + row * 0.7f), new Vector3(0.2f, 0.24f, 0.2f), leaf);
                    }
                }
            },
            Level("Tended", "Stone edging and compost.", 2, ("GreyStone", 6), ("Sticks", 6), 0.3f, 1));
        Building("Beehive", "Beehive", "A straw skep. Feed the bees berries and they make honey, whether you're home or not.", 15, CraftingStationType.Beehive, 3f, 3, 0f, false,
            new[] { ("OakPlank", 3), ("NettleCord", 2) }, root =>
            {
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.3f, 0f), new Vector3(0.9f, 0.6f, 0.9f), wood);
                Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.95f, 0f), new Vector3(0.8f, 0.8f, 0.8f), hive);
            });
        Building("Well", "Well", "Clean water inside the ward: drink, and fill your waterskins without the walk to the stream.", 16, CraftingStationType.None, 6f, 5, 1.5f, false,
            new[] { ("GreyStone", 8), ("OakPlank", 4) }, root =>
            {
                Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 0.45f, 0f), new Vector3(1.5f, 0.45f, 1.5f), stone);
                Piece(root, PrimitiveType.Cube, new Vector3(-0.7f, 1.2f, 0f), new Vector3(0.1f, 1.5f, 0.1f), wood);
                Piece(root, PrimitiveType.Cube, new Vector3(0.7f, 1.2f, 0f), new Vector3(0.1f, 1.5f, 0.1f), wood);
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 2f, 0f), new Vector3(1.8f, 0.1f, 0.9f), wood).localRotation = Quaternion.Euler(0f, 0f, 0f);
            });
        Building("Sauna", "Sauna", "Hot stones and birch smoke. Sit a while before a raid: you go out warmer and harder to kill.", 17, CraftingStationType.None, 8f, 8, 1.5f, false,
            new[] { ("OakPlank", 10), ("GreyStone", 8) }, root =>
            {
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(2.6f, 2f, 2.2f), wood);
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 2.25f, 0f), new Vector3(3f, 0.5f, 2.6f), thatch);
                Piece(root, PrimitiveType.Cube, new Vector3(0.9f, 2.5f, 0.6f), new Vector3(0.4f, 1.2f, 0.4f), stone);
            });
        Building("WatchPost", "Watch Post", "A raised fire on stilts. It lights the ground around it, and what hunts at night keeps out of the light.", 18, CraftingStationType.None, 6f, 5, 0f, false,
            new[] { ("OakWood", 6), ("OakPlank", 4), ("NettleCord", 2) }, root =>
            {
                for (int i = 0; i < 4; i++)
                {
                    var corner = new Vector3(i < 2 ? -0.6f : 0.6f, 1.5f, i % 2 == 0 ? -0.6f : 0.6f);
                    Piece(root, PrimitiveType.Cube, corner, new Vector3(0.14f, 3f, 0.14f), wood);
                }

                Piece(root, PrimitiveType.Cube, new Vector3(0f, 3f, 0f), new Vector3(1.6f, 0.12f, 1.6f), wood);
                Transform fire = Piece(root, PrimitiveType.Sphere, new Vector3(0f, 3.3f, 0f), Vector3.one * 0.5f, AssetDatabase.LoadAssetAtPath<Material>(Tints + "Lantern Glow.mat"));
                var light = new GameObject("Light").AddComponent<Light>();
                light.transform.SetParent(fire, false);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.68f, 0.35f);
                light.intensity = 3f;
                light.range = 14f;
                light.shadows = LightShadows.None;
                light.gameObject.AddComponent<LightFlicker>();
                light.gameObject.AddComponent<AtmosphereLight>();
                light.gameObject.AddComponent<VisibilitySource>().Configure(11f, 0.5f, true, 10f);
            });
        Building("SpikeTrap", "Spike Trap", "Sharpened stakes under a thin cover. Enemies that walk over it get hurt; your people know where to step.", 19, CraftingStationType.None, 0f, 1, 0f, false,
            new[] { ("OakWood", 3), ("Sticks", 4) }, root =>
            {
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(1.8f, 0.06f, 1.8f), soil);
                for (int x = -1; x <= 1; x++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        Piece(root, PrimitiveType.Cylinder, new Vector3(x * 0.55f, 0.2f, z * 0.55f), new Vector3(0.05f, 0.22f, 0.05f), wood);
                    }
                }
            });
        Building("Cottage", "Cottage", "A real roof over someone's head. It makes Safe Haven a village, and its Renown shows.", 20, CraftingStationType.None, 10f, 12, 1f, false,
            new[] { ("OakPlank", 12), ("StoneBlock", 6), ("NettleCord", 4) }, root =>
            {
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 1.1f, 0f), new Vector3(4f, 2.2f, 3f), wood);
                Transform left = Piece(root, PrimitiveType.Cube, new Vector3(0f, 2.75f, -0.8f), new Vector3(4.4f, 0.14f, 2.2f), thatch);
                left.localRotation = Quaternion.Euler(-38f, 0f, 0f);
                Transform right = Piece(root, PrimitiveType.Cube, new Vector3(0f, 2.75f, 0.8f), new Vector3(4.4f, 0.14f, 2.2f), thatch);
                right.localRotation = Quaternion.Euler(38f, 0f, 0f);
                Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.75f, 1.51f), new Vector3(0.8f, 1.5f, 0.06f), iron);
            },
            Level("Whitewashed", "Lime on the walls and smoke from the chimney.", 6, ("StoneBlock", 4), ("OakPlank", 6), 0f, 0));
        FinishSpecialBuildings();
        return 9;
    }

    private static BuildingLevel Level(string title, string text, int renown, (string item, int amount) a, (string item, int amount) b, float speed, int queue)
    {
        return new BuildingLevel
        {
            title = title,
            description = text,
            renown = renown,
            work = 6f + renown * 0.5f,
            craftSpeed = speed,
            extraQueue = queue,
            cost = new List<ItemAmount> { new ItemAmount(FindItem(a.item), a.amount), new ItemAmount(FindItem(b.item), b.amount) },
        };
    }

    private static void Building(string file, string label, string text, int sort, CraftingStationType station, float work, int renown, float yard, bool lines,
        (string item, int amount)[] cost, Action<Transform> shape, params BuildingLevel[] levels)
    {
        string path = Buildings + file + ".asset";
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(building, path);
        }

        var so = new SerializedObject(building);
        so.FindProperty("id").stringValue = file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        so.FindProperty("combatStats").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>("Assets/_Project/Data/Combat/Stats/WoodenStructureStats.asset");
        SerializedProperty items = so.FindProperty("constructionItems");
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = FindItem(cost[i].item);
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.FindProperty("station").intValue = (int)station;
        so.FindProperty("knownFromStart").boolValue = true;
        so.FindProperty("showInBuildMenu").boolValue = true;
        so.FindProperty("sortOrder").intValue = sort;
        so.FindProperty("yardDepth").floatValue = yard;
        so.FindProperty("placeInLines").boolValue = lines;
        if (so.FindProperty("icon").objectReferenceValue == null)
        {
            so.FindProperty("icon").objectReferenceValue = BuildingIcon(file, shape);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        building.ConfigureBuildingUp(work, renown, levels);
        EditorUtility.SetDirty(building);
        if (building.Prefab == null)
        {
            GameObject model = SaveModel(file + " Model", shape);
            GameObject prefab = BuildingPrefabBuilder.Create(building, model);
            so.Update();
            so.FindProperty("prefab").objectReferenceValue = prefab;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AddConstruction(building);
    }

    private static Sprite BuildingIcon(string file, Action<Transform> shape)
    {
        string path = "Assets/_Project/Art/Haven/Icons/" + file + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return existing != null ? existing : IconRenderer.RenderToSprite(SaveModel(file + " Model", shape), path);
    }

    private static void AddConstruction(BuildingDefinition building)
    {
        string path = AssetDatabase.GetAssetPath(building.Prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<Construction>() == null)
            {
                root.AddComponent<Construction>();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // What the builders don't know about: the well's water, the sauna's heat, and a trap creatures must be able to walk onto.
    private static void FinishSpecialBuildings()
    {
        EditBuildingPrefab("Well", root =>
        {
            Usable(root, 2.2f);
            WaterSource water = root.GetComponent<WaterSource>() != null ? root.GetComponent<WaterSource>() : root.AddComponent<WaterSource>();
            var so = new SerializedObject(water);
            so.FindProperty("sourceName").stringValue = "Well";
            so.FindProperty("fillItem").objectReferenceValue = FindItem("Waterskin");
            so.FindProperty("fillUpTo").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();
        });
        EditBuildingPrefab("Sauna", root =>
        {
            Usable(root, 2.2f);
            BoonSource boon = root.GetComponent<BoonSource>() != null ? root.GetComponent<BoonSource>() : root.AddComponent<BoonSource>();
            var so = new SerializedObject(boon);
            so.FindProperty("boonName").stringValue = "Warmed Through";
            so.FindProperty("verb").stringValue = "Sit in the steam";
            so.FindProperty("seconds").floatValue = 600f;
            SetBonuses(so, "bonuses", new[] { new StatBonus(StatType.MaxHealth, 20f), new StatBonus(StatType.FrostResistance, 0.1f) });
            so.ApplyModifiedPropertiesWithoutUndo();
        });
        EditBuildingPrefab("SpikeTrap", root =>
        {
            if (root.GetComponent<SpikeTrap>() == null)
            {
                root.AddComponent<SpikeTrap>();
            }

            NavMeshObstacle obstacle = root.GetComponent<NavMeshObstacle>();
            if (obstacle != null)
            {
                Object.DestroyImmediate(obstacle, true);
            }

            foreach (Collider collider in root.GetComponents<Collider>())
            {
                collider.isTrigger = true;
            }
        });
    }

    private static void Usable(GameObject root, float range)
    {
        Interactable interactable = root.GetComponent<Interactable>() != null ? root.GetComponent<Interactable>() : root.AddComponent<Interactable>();
        var so = new SerializedObject(interactable);
        so.FindProperty("kind").intValue = (int)InteractionKind.Use;
        so.FindProperty("deactivateOnComplete").boolValue = false;
        so.FindProperty("range").floatValue = range;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EditBuildingPrefab(string file, Action<GameObject> change)
    {
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(Buildings + file + ".asset");
        string path = AssetDatabase.GetAssetPath(building.Prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            change(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ----- The player -----

    private static string WirePlayer()
    {
        var made = new List<string>();
        const string player = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(player);
        try
        {
            if (root.GetComponent<PlayerBoons>() == null)
            {
                root.AddComponent<PlayerBoons>();
                PrefabUtility.SaveAsPrefabAsset(root, player);
                made.Add("PlayerBoons on the player");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        const string vitals = "Assets/_Project/Prefabs/UI/PlayerVitalsHUD.prefab";
        root = PrefabUtility.LoadPrefabContents(vitals);
        try
        {
            if (root.GetComponent<PlayerConditionsView>() == null)
            {
                root.AddComponent<PlayerConditionsView>();
                PrefabUtility.SaveAsPrefabAsset(root, vitals);
                made.Add("PlayerConditionsView on the vitals HUD");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return made.Count > 0 ? string.Join(", ", made) : "player already wired";
    }

    // ----- Shared helpers -----

    private static GameObject SaveModel(string name, Action<Transform> shape)
    {
        string path = Models + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        PrefabBuildUtility.EnsureFolder(Models.TrimEnd('/'));
        var root = new GameObject(name);
        try
        {
            shape(root.transform);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static Transform Piece(Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = scale;
        piece.GetComponent<Renderer>().sharedMaterial = material;
        return piece.transform;
    }

    private static Material Tint(string name, Color color)
    {
        string path = Tints + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    private static Sprite IconFor(string file, IconShape shape, Color primary, Color secondary)
    {
        string path = Icons + file + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return existing != null ? existing : PlaceholderIconPainter.Paint(shape, primary, secondary, path);
    }

    private static ItemDefinition Make(string path, string label, string text, ItemCategory category, int tier, ItemRarity rarity, int value, float weight, Sprite icon, Action<SerializedObject> extra)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            item = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(item, path);
        }

        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        so.FindProperty("category").intValue = (int)category;
        so.FindProperty("tier").intValue = tier;
        so.FindProperty("rarity").intValue = (int)rarity;
        so.FindProperty("value").intValue = value;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("icon").objectReferenceValue = icon;
        extra(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    private static void SetBonuses(SerializedObject so, string field, StatBonus[] bonuses)
    {
        SerializedProperty list = so.FindProperty(field);
        list.arraySize = bonuses.Length;
        for (int i = 0; i < bonuses.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("stat").intValue = (int)bonuses[i].stat;
            entry.FindPropertyRelative("op").intValue = (int)bonuses[i].op;
            entry.FindPropertyRelative("value").floatValue = bonuses[i].value;
        }
    }

    private static void Recipe(string file, ItemDefinition output, int amount, CraftingStationType station, float seconds, params (string item, int amount)[] cost)
    {
        string path = Recipes + file + ".asset";
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        var so = new SerializedObject(recipe);
        so.FindProperty("id").stringValue = file.ToLowerInvariant();
        so.FindProperty("output").objectReferenceValue = output;
        so.FindProperty("outputAmount").intValue = amount;
        so.FindProperty("station").intValue = (int)station;
        so.FindProperty("craftSeconds").floatValue = seconds;
        so.FindProperty("knownFromStart").boolValue = true;
        so.FindProperty("sortOrder").intValue = 100 + output.Tier * 10;
        SerializedProperty items = so.FindProperty("itemCost");
        items.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = FindItem(cost[i].item);
            items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }

    private static ItemDefinition FindItem(string file)
    {
        foreach (string folder in new[] { Materials, Items, Items + "Fish/", Food })
        {
            var found = AssetDatabase.LoadAssetAtPath<ItemDefinition>(folder + file + ".asset");
            if (found != null)
            {
                return found;
            }
        }

        throw new Exception("No item " + file);
    }

    private static void Refresh()
    {
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
    }
}
