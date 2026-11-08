using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The second pass on base progression (`unity command script run_script -- --file AgentScripts/BuildHavenProgression.cs --entry BuildHavenProgression.Apply`),
/// run after BuildBaseTiers and BuildFoundation. Every upgrade now opens something, takes a chore or answers a raid problem: the Loom
/// and Tanning Rack become the Tailor's Shop and the Hunter's Lodge, later tiers ask for other buildings, the ward opens raid tiers, the
/// portal can be improved, beds help without being required, ranks bring settlers. See Docs/BaseProgression.md. Assets and ids are kept, so
/// saves hold. Safe to run again.
/// </summary>
public static class BuildHavenProgression
{
    private const string Data = "Assets/_Project/Data/";
    private const string BuildingFolder = Data + "Buildings/";
    private const string ItemFolder = Data + "Items/";
    private const string RecipeFolder = Data + "Crafting/Recipes/";
    private const string WeaponData = Data + "Combat/Weapons/";
    private const string Models = "Assets/_Project/Art/Haven/Models/";
    private const string HavenIcons = "Assets/_Project/Art/Haven/Icons/";
    private const string GearIcons = "Assets/_Project/Art/Items/Gear/Icons/";
    private const string GearModels = "Assets/_Project/Art/Items/Gear/Models/";
    private const string GearTints = "Assets/_Project/Art/Items/Gear/Materials/";
    private const string Tints = "Assets/_Project/Materials/Haven/";

    private static readonly Color MoonColor = new Color(0.72f, 0.82f, 0.95f);
    private static readonly Color TrollColor = new Color(0.36f, 0.42f, 0.34f);

    private static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();
    private static readonly Dictionary<string, ResourceDefinition> Resources_ = new Dictionary<string, ResourceDefinition>();
    private static readonly Dictionary<string, BuildingDefinition> Buildings = new Dictionary<string, BuildingDefinition>();
    private static readonly Dictionary<string, RecipeDefinition> Recipes = new Dictionary<string, RecipeDefinition>();

    public static string Apply()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
        {
            return "The open scene has unsaved changes. Nothing done.";
        }

        Load();
        var log = new List<string>
        {
            NewGear(),
            NewProvisions(),
            FieldKits(),
            Packs(),
            Runes(),
            MoveRecipes(),
        };
        Load();
        log.Add(RenamedBuildings());
        log.Add(NewBuildings());
        Load();
        log.Add(Upgrades());
        log.Add(People());
        log.Add(Rules());
        log.Add(Trade());
        Refresh();
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    private static void Load()
    {
        Items.Clear();
        Resources_.Clear();
        Buildings.Clear();
        Recipes.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Items[item.name] = item;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:ResourceDefinition"))
        {
            var resource = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Resources_[resource.name] = resource;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:BuildingDefinition"))
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Buildings[building.name] = building;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:RecipeDefinition"))
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Recipes[recipe.name] = recipe;
        }
    }

    // ----- Gear for the tiers that had none: bows at the Hunter's Lodge, shields at the Woodcutter's Lodge, the rest at the Smithy -----

    private static string NewGear()
    {
        // Damage goes by what the tier's sword does against iron, so a kit's balance (RaidTierTests) holds.
        float moon = WeaponDamage("MoonsilverBlade") / WeaponDamage("IronSword");
        float troll = moon * 1.25f;

        Bow("MoonstrungBow", "Moonstrung Bow", "Elderwood strung with moonsilver wire. It hums after the arrow has gone.", "ElderwoodLongbow", 4, ItemRarity.Rare, 190, 2.2f, 0.9f, 58f, 1.4f, 0.1f, MoonColor,
            CraftingStationType.Tannery, ("Elderwood", 3), ("MoonsilverIngot", 2), ("CuredLeather", 2));
        Bow("TrollhornBow", "Trollhorn Bow", "Horn and trollstone, and no ordinary arm can draw it. Yours can, now.", "ElderwoodLongbow", 5, ItemRarity.Epic, 320, 2.6f, 0.85f, 66f, 1.55f, 0.2f, TrollColor,
            CraftingStationType.Tannery, ("Elderwood", 3), ("Trollstone", 1), ("MoonsilverIngot", 2), ("ThickHide", 2));

        Shield("MoonwoodShield", "Moonwood Shield", "Elderwood faced with moonsilver. Blows slide off it with a sound like rain.", "IronKiteShield", 4, ItemRarity.Rare, 150, 4f, 14f, MoonColor,
            ("Elderwood", 3), ("MoonsilverIngot", 3), ("ThickHide", 1));
        Shield("TrollwallShield", "Trollwall Shield", "A slab of trollstone in an elderwood frame. It stops what would have stopped you.", "IronKiteShield", 5, ItemRarity.Epic, 260, 6.5f, 18f, TrollColor,
            ("Elderwood", 3), ("Trollstone", 1), ("MoonsilverIngot", 2));

        Melee("MoonsilverGreatsword", "Moonsilver Greatsword", "Long enough to keep a draug at bay, and light enough to swing twice.", "IronGreatsword", 4, ItemRarity.Rare, 210, 4f, moon, MoonColor,
            ("MoonsilverIngot", 5), ("HardwoodPlank", 1), ("HuldraSilk", 1));
        Melee("MoonsilverSpear", "Moonsilver Spear", "A narrow head on an elderwood shaft. It goes where it is pointed.", "IronSpear", 4, ItemRarity.Rare, 180, 2.4f, moon, MoonColor,
            ("MoonsilverIngot", 3), ("Elderwood", 2));
        Melee("TrollstoneBlade", "Trollstone Blade", "Grey-green and rough-edged. It doesn't cut so much as insist.", "MoonsilverBlade", 5, ItemRarity.Epic, 300, 3.2f, troll / moon, TrollColor,
            ("Trollstone", 2), ("MoonsilverIngot", 2), ("Elderwood", 1));

        TrollArmor("MoonsilverCirclet", "TrollstoneHelm", "Trollstone Helm", "Heavy as a held grudge. Nothing rings it twice.", ("Trollstone", 1), ("MoonsilverIngot", 2));
        TrollArmor("MoonsilverHauberk", "TrollstoneHauberk", "Trollstone Hauberk", "Plates of trollstone on moonsilver rings. It is the last thing the forest expected of you.", ("Trollstone", 3), ("MoonsilverIngot", 4), ("Elderwood", 2));
        TrollArmor("MoonsilverGloves", "TrollstoneGauntlets", "Trollstone Gauntlets", "Your hands stop feeling the cold of the handle.", ("Trollstone", 1), ("MoonsilverIngot", 2));
        TrollArmor("MoonsilverBoots", "TrollstoneBoots", "Trollstone Boots", "Roots break underfoot, and they remember.", ("Trollstone", 1), ("MoonsilverIngot", 2));
        TrollArmor("MoonsilverSash", "TrollstoneGirdle", "Trollstone Girdle", "A troll's own buckle, cut down to fit.", ("Trollstone", 1), ("MoonsilverIngot", 1));
        return "gear: bows and shields for tiers IV and V, two more moonsilver weapons, a trollstone blade and set";
    }

    private static float WeaponDamage(string file) => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WeaponData + file + ".asset").Damage;

    // An arrow carries the damage, so a bow climbs by how it shoots, and by what it adds to the shot.
    private static void Bow(string file, string label, string text, string template, int tier, ItemRarity rarity, int value, float weight, float draw, float speed, float perfect, float bonus, Color tint,
        CraftingStationType station, params (string item, int amount)[] cost)
    {
        WeaponDefinition weapon = CopyWeapon(file, Items[template].Weapon, label);
        Edit(weapon, so =>
        {
            so.FindProperty("drawSeconds").floatValue = draw;
            so.FindProperty("arrowSpeed").floatValue = speed;
            so.FindProperty("perfectReleaseDamage").floatValue = perfect;
        });
        ItemDefinition item = MakeWeaponItem(file, label, text, template, tier, rarity, value, weight, weapon, tint, station, cost);
        Edit(item, so => SetStats(so.FindProperty("statBonuses"), new[] { (StatType.DamageBonus, StatModifierOp.Flat, bonus) }));
    }

    private static void Shield(string file, string label, string text, string template, int tier, ItemRarity rarity, int value, float weight, float armor, Color tint, params (string item, int amount)[] cost)
    {
        WeaponDefinition weapon = CopyWeapon(file, Items[template].Weapon, label);
        ItemDefinition item = MakeWeaponItem(file, label, text, template, tier, rarity, value, weight, weapon, tint, CraftingStationType.Sawbench, cost);
        Edit(item, so => SetStats(so.FindProperty("statBonuses"), new[] { (StatType.Armor, StatModifierOp.Flat, armor) }));
    }

    private static void Melee(string file, string label, string text, string template, int tier, ItemRarity rarity, int value, float weight, float strength, Color tint, params (string item, int amount)[] cost)
    {
        WeaponDefinition source = Items[template].Weapon;
        WeaponDefinition weapon = CopyWeapon(file, source, label);
        float damage = source.Damage * strength;
        Edit(weapon, so => so.FindProperty("damage").floatValue = damage);
        MakeWeaponItem(file, label, text, template, tier, rarity, value, weight, weapon, tint, CraftingStationType.Anvil, cost);
    }

    private static bool File(string path) => AssetDatabase.LoadMainAssetAtPath(path) != null;

    private static WeaponDefinition CopyWeapon(string file, WeaponDefinition template, string label)
    {
        string path = WeaponData + file + ".asset";
        if (!File(path))
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(template), path);
        }

        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        Edit(weapon, so => so.FindProperty("displayName").stringValue = label);
        return weapon;
    }

    private static ItemDefinition MakeWeaponItem(string file, string label, string text, string template, int tier, ItemRarity rarity, int value, float weight, WeaponDefinition weapon, Color tint,
        CraftingStationType station, (string item, int amount)[] cost)
    {
        ItemDefinition source = Items[template];
        GameObject model = source.HeldModel != null ? Tinted(file, source.HeldModel, tint) : null;
        ItemDefinition item = CopyItem(ItemFolder + "Weapons/" + file + ".asset", source, label, text, tier, rarity, value, weight);
        Sprite icon = model != null ? RenderIcon(file, model) : source.Icon;
        Edit(item, so =>
        {
            so.FindProperty("weapon").objectReferenceValue = weapon;
            so.FindProperty("heldModel").objectReferenceValue = model;
            so.FindProperty("icon").objectReferenceValue = icon;
        });
        Recipe(file, item, 1, station, 30f + tier * 2f, 0, cost);
        return item;
    }

    private static void TrollArmor(string template, string file, string label, string text, params (string item, int amount)[] cost)
    {
        ItemDefinition source = Items[template];
        int tierFive = 5;
        ItemDefinition item = CopyItem(ItemFolder + "Armor/" + file + ".asset", source, label, text, tierFive, ItemRarity.Epic, Mathf.RoundToInt(source.Value * 1.6f), source.Weight * 1.8f);
        Sprite icon = PlaceholderIcon(file, source.ArmorSlot, TrollColor);
        Edit(item, so =>
        {
            so.FindProperty("icon").objectReferenceValue = icon;
            SerializedProperty from = new SerializedObject(source).FindProperty("statBonuses");
            SerializedProperty stats = so.FindProperty("statBonuses");
            stats.arraySize = from.arraySize;
            for (int i = 0; i < from.arraySize; i++)
            {
                SerializedProperty a = from.GetArrayElementAtIndex(i);
                SerializedProperty b = stats.GetArrayElementAtIndex(i);
                var stat = (StatType)a.FindPropertyRelative("stat").intValue;
                b.FindPropertyRelative("stat").intValue = (int)stat;
                b.FindPropertyRelative("op").intValue = a.FindPropertyRelative("op").intValue;
                // Tougher than moonsilver: armor and health climb, the rest stays.
                float scale = stat == StatType.Armor || stat == StatType.MaxHealth ? 1.25f : 1f;
                b.FindPropertyRelative("value").floatValue = a.FindPropertyRelative("value").floatValue * scale;
            }
        });
        Recipe(file, item, 1, CraftingStationType.Anvil, 36f, 0, cost);
    }

    // ----- Meals and potions for the raids that had none -----

    private static string NewProvisions()
    {
        Meal("BoarPie", "Boar Pie", "Boar and barley under a crust. It stays with you.", 3, 45, 70f, 10f, 15f, 480f, false, CraftingStationType.Kitchen, 22f,
            new[] { (StatType.MaxHealth, StatModifierOp.Percent, 0.08f) }, ("BoarMeat", 2), ("Barley", 2), ("Honey", 1));
        Meal("TroutInCream", "Trout in Cream", "A cold trout, a warm sauce and a steadier arm.", 3, 40, 55f, 15f, 10f, 480f, false, CraftingStationType.Kitchen, 20f,
            new[] { (StatType.DamageBonus, StatModifierOp.Flat, 0.08f) }, ("BrookTrout", 1), ("Cloudberries", 2), ("Chanterelle", 2));
        Meal("HavenFeast", "Haven Feast", "The whole table, and everyone at it. You carry it out through the portal.", 4, 80, 90f, 40f, 25f, 720f, false, CraftingStationType.Kitchen, 30f,
            new[] { (StatType.MaxHealth, StatModifierOp.Percent, 0.12f), (StatType.Armor, StatModifierOp.Flat, 12f) }, ("BoarMeat", 3), ("Barley", 2), ("Honey", 2), ("WildTurnip", 2));
        Meal("MoonBroth", "Moon Broth", "Char and mushrooms, with a wisp's ember stirred in. It tastes of nothing, and lasts.", 4, 70, 45f, 50f, 15f, 600f, false, CraftingStationType.Kitchen, 26f,
            new[] { (StatType.MaxStamina, StatModifierOp.Percent, 0.15f), (StatType.FrostResistance, StatModifierOp.Flat, 0.1f), (StatType.HolyResistance, StatModifierOp.Flat, 0.1f) }, ("ArcticChar", 1), ("Chanterelle", 3), ("WispEmber", 1));

        Meal("IronbarkDraught", "Ironbark Draught", "Barkskin brewed with deepstone. Your skin stops feeling like yours.", 4, 90, 0f, 0f, 0f, 240f, true, CraftingStationType.AlchemyTable, 16f,
            new[] { (StatType.Armor, StatModifierOp.Flat, 32f), (StatType.PhysicalResistance, StatModifierOp.Flat, 0.05f) }, ("RootlingBark", 3), ("Deepstone", 1));
        Meal("FrostwardDraught", "Frostward Draught", "Warming brew made strong with huldra silk. Winter slides off.", 4, 80, 0f, 0f, 0f, 300f, true, CraftingStationType.AlchemyTable, 14f,
            new[] { (StatType.FrostResistance, StatModifierOp.Flat, 0.3f), (StatType.HolyResistance, StatModifierOp.Flat, 0.15f) }, ("Chanterelle", 2), ("Honey", 1), ("HuldraSilk", 1));
        ItemDefinition radiant = CopyItem(ItemFolder + "RadiantDraught.asset", Items["GreaterHealingDraught"], "Radiant Draught", "The strongest thing the Cauldron can hold. It heals as if it were never hurt.", 4, ItemRarity.Rare, 120, Items["GreaterHealingDraught"].Weight);
        Edit(radiant, so => so.FindProperty("healAmount").floatValue = 180f);
        Recipe("RadiantDraught", radiant, 1, CraftingStationType.AlchemyTable, 16f, 0, new[] { ("RadiantLifeOrb", 2), ("OrbTincture", 1), ("HuldraSilk", 1) });
        return "provisions: tier III and IV meals at the Kitchen, tier IV potions at the Cauldron";
    }

    private static void Meal(string file, string label, string text, int tier, int value, float hunger, float thirst, float heal, float seconds, bool tonic, CraftingStationType station, float craft,
        (StatType stat, StatModifierOp op, float value)[] bonuses, params (string item, int amount)[] cost)
    {
        ItemDefinition template = Items[tonic ? "BarkskinDraught" : "HuntersStew"];
        string folder = AssetDatabase.GetAssetPath(template).Replace(template.name + ".asset", "");
        ItemDefinition item = CopyItem(folder + file + ".asset", template, label, text, tier, ItemRarity.Uncommon, value, template.Weight);
        Sprite icon = tonic ? PlaceholderIconPainter.Paint(IconShape.Potion, new Color(0.55f, 0.75f, 0.85f), new Color(0.25f, 0.2f, 0.15f), GearIcons + file + ".png") : template.Icon;
        Edit(item, so =>
        {
            so.FindProperty("hungerRestore").floatValue = hunger;
            so.FindProperty("thirstRestore").floatValue = thirst;
            so.FindProperty("healAmount").floatValue = heal;
            so.FindProperty("boonSeconds").floatValue = seconds;
            so.FindProperty("tonic").boolValue = tonic;
            so.FindProperty("icon").objectReferenceValue = icon;
            SetStats(so.FindProperty("boonBonuses"), bonuses);
        });
        Recipe(file, item, 1, station, craft, 0, cost);
    }

    // ----- Field kits: the Workshop's upper tiers make what solves problems out there -----

    private static string FieldKits()
    {
        ItemDefinition kit = CopyItem(ItemFolder + "FieldRepairKit.asset", Items["Bandage"], "Field Repair Kit", "Rivets, thread and a little patience. Mends everything you are wearing by a third, out where it counts.", 3, ItemRarity.Common, 30, 1f);
        Sprite kitIcon = PlaceholderIconPainter.Paint(IconShape.Cloth, new Color(0.45f, 0.42f, 0.38f), new Color(0.62f, 0.34f, 0.17f), GearIcons + "FieldRepairKit.png");
        Edit(kit, so =>
        {
            so.FindProperty("healAmount").floatValue = 0f;
            so.FindProperty("gearRepair").floatValue = 0.33f;
            so.FindProperty("icon").objectReferenceValue = kitIcon;
        });
        Recipe("FieldRepairKit", kit, 1, CraftingStationType.Workbench, 18f, 3, new[] { ("IronIngot", 1), ("Linen", 2), ("NettleCord", 2) });

        GameObject camp = CampPrefab();
        ItemDefinition campKit = CopyItem(ItemFolder + "ForwardCampKit.asset", Items["Bandage"], "Forward Camp Kit", "Stakes, a fire-pot and a ring of old charms. Sets down a small camp out in the woods: rest inside it, and the night keeps away.", 4, ItemRarity.Rare, 70, 4f);
        Sprite campIcon = IconRenderer.RenderToSprite(camp, GearIcons + "ForwardCampKit.png");
        Edit(campKit, so =>
        {
            so.FindProperty("healAmount").floatValue = 0f;
            so.FindProperty("gearRepair").floatValue = 0f;
            so.FindProperty("placesInRaid").objectReferenceValue = camp;
            so.FindProperty("icon").objectReferenceValue = campIcon;
        });
        Recipe("ForwardCampKit", campKit, 1, CraftingStationType.Workbench, 24f, 4, new[] { ("HardwoodPlank", 4), ("StoneBlock", 2), ("WispEmber", 1) });

        var exit = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Raids/ExtractionPoint.prefab");
        ItemDefinition shard = CopyItem(ItemFolder + "WaystoneShard.asset", Items["Bandage"], "Waystone Shard", "A sliver of the portal's own stone. Set it down and it opens a way home where you stand, for whoever can hold it.", 5, ItemRarity.Epic, 150, 1f);
        Sprite shardIcon = PlaceholderIconPainter.Paint(IconShape.Rune, new Color(0.5f, 0.75f, 0.9f), new Color(0.2f, 0.25f, 0.35f), GearIcons + "WaystoneShard.png");
        Edit(shard, so =>
        {
            so.FindProperty("healAmount").floatValue = 0f;
            so.FindProperty("gearRepair").floatValue = 0f;
            so.FindProperty("placesInRaid").objectReferenceValue = exit;
            so.FindProperty("icon").objectReferenceValue = shardIcon;
        });
        Recipe("WaystoneShard", shard, 1, CraftingStationType.Workbench, 30f, 5, new[] { ("MoonsilverIngot", 1), ("Deepstone", 1), ("LifeOrb", 3) });
        return "field kits: repair kit (Workshop III), forward camp (IV), waystone shard (V)";
    }

    private static GameObject CampPrefab()
    {
        const string path = "Assets/_Project/Prefabs/Raids/Forward Camp.prefab";
        var root = new GameObject("Forward Camp");
        try
        {
            Box(root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(1.4f, 0.2f, 1.4f), Rock, default, PrimitiveType.Cylinder);
            Box(root.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.7f, 0.1f, 0.7f), Ember, default, PrimitiveType.Cylinder);
            foreach (float angle in new[] { 40f, 160f, 280f })
            {
                Vector3 at = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 2.4f;
                Box(root.transform, at + Vector3.up * 0.6f, new Vector3(0.14f, 1.2f, 0.14f), Timber);
                Box(root.transform, at + Vector3.up * 1.25f, new Vector3(0.2f, 0.2f, 0.2f), Ember);
            }

            var flame = new GameObject("Light").AddComponent<Light>();
            flame.transform.SetParent(root.transform, false);
            flame.transform.localPosition = Vector3.up * 0.9f;
            flame.type = LightType.Point;
            flame.color = new Color(1f, 0.66f, 0.34f);
            flame.intensity = 3f;
            flame.range = 9f;
            flame.shadows = LightShadows.None;
            flame.gameObject.AddComponent<VisibilitySource>().Configure(5.5f, 0.4f, true, 7f);
            root.AddComponent<FieldCamp>();
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ----- Packs: bringing more home -----

    private static string Packs()
    {
        ItemDefinition rucksack = Items["HideRucksack"];
        foreach ((string file, string label, string text, int tier, int weight, int slots, int value, (string, int)[] cost) in new[]
        {
            ("TravellersPack", "Traveller's Pack", "A hardwood frame and a linen sack. It makes a long trip longer.", 3, 60, 20, 60, new[] { ("HardwoodPlank", 2), ("Linen", 4), ("ThickHide", 2) }),
            ("WayfarersFrame", "Wayfarer's Frame", "Silk straps on a light frame. You stop noticing what is on your back.", 4, 90, 24, 130, new[] { ("Elderwood", 2), ("HuldraSilk", 3), ("Linen", 4), ("MoonsilverIngot", 1) }),
        })
        {
            ItemDefinition item = CopyItem(ItemFolder + file + ".asset", rucksack, label, text, tier, tier >= 4 ? ItemRarity.Rare : ItemRarity.Uncommon, value, rucksack.Weight);
            Edit(item, so =>
            {
                so.FindProperty("carryCapacityBonus").intValue = weight;
                so.FindProperty("bagSlotBonus").intValue = slots;
            });
            Recipe(file, item, 1, CraftingStationType.Loom, 24f + tier * 2f, 0, cost);
        }

        return "packs: Traveller's Pack (Tailor III), Wayfarer's Frame (Tailor IV)";
    }

    // ----- Two runes only Liv knows, so finding her is still worth it once the Watch Post teaches the rest -----

    private static string Runes()
    {
        const string folder = Data + "Raids/Modifiers/";
        foreach ((string file, string modifierId, string label, string text, (int stat, float value)[] effects, string itemLabel, string itemText, (string, int)[] cost) in new[]
        {
            ("Hush", "hush", "Hush", "The forest holds its breath: its creatures are weaker, and caches hold less.",
                new[] { (2, -0.3f), (3, -0.2f), (4, -0.2f) }, "Hush Rune", "One of Liv's. Carve it on the portal for a quieter road: weaker creatures, poorer caches.", new[] { ("OrbTincture", 1), ("Bone", 2), ("NettleCord", 1) }),
            ("DeepWoods", "deep_woods", "The Deep Woods", "The way opens further in than it should. Everything there is stronger, and so is what it guards.",
                new[] { (2, 2f), (3, 1f), (4, 1f) }, "Deep Rune", "One of Liv's. Carve it on the portal and go further in than is wise: far fiercer creatures, far richer caches.", new[] { ("OrbTincture", 3), ("Deepstone", 1), ("WolfFang", 2) }),
        })
        {
            string modifierPath = folder + file + ".asset";
            if (!File(modifierPath))
            {
                AssetDatabase.CopyAsset(folder + "BloodMoon.asset", modifierPath);
            }

            var modifier = AssetDatabase.LoadAssetAtPath<RaidModifierDefinition>(modifierPath);
            Edit(modifier, so =>
            {
                so.FindProperty("id").stringValue = modifierId;
                so.FindProperty("displayName").stringValue = label;
                so.FindProperty("description").stringValue = text;
                SerializedProperty list = so.FindProperty("effects");
                list.arraySize = effects.Length;
                for (int i = 0; i < effects.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue = effects[i].stat;
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("op").intValue = (int)StatModifierOp.Flat;
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = effects[i].value;
                }
            });

            string itemFile = file + "Rune";
            ItemDefinition item = CopyItem(ItemFolder + itemFile + ".asset", Items["BloodMoonRune"], itemLabel, itemText, 2, ItemRarity.Rare, 60, 0.2f);
            Edit(item, so => so.FindProperty("raidModifier").objectReferenceValue = modifier);
            Recipe(itemFile, item, 1, CraftingStationType.AlchemyTable, 15f, 0, cost, false);
        }

        return "runes: Hush and Deep Woods, taught only by Liv";
    }

    // ----- Where things are made -----

    private static string MoveRecipes()
    {
        int moved = 0;
        foreach ((string recipe, CraftingStationType station) in new[]
        {
            ("HuntersBow", CraftingStationType.Tannery), ("ElderwoodLongbow", CraftingStationType.Tannery),
            ("HideRucksack", CraftingStationType.Loom),
            ("CopperBoundShield", CraftingStationType.Sawbench), ("IronKiteShield", CraftingStationType.Sawbench),
        })
        {
            Edit(Recipes[recipe], so => so.FindProperty("station").intValue = (int)station);
            moved++;
        }

        // A better yield is what a tier of the Hunter's Lodge opens: the same hide goes further.
        YieldRecipe("BetterCuredLeather", "CuredLeather", 2, 3, CraftingStationType.Tannery, 10f, new[] { ("RoughHide", 2), ("Sticks", 1) });
        YieldRecipe("BetterCuredThickHide", "CuredLeather", 3, 3, CraftingStationType.Tannery, 12f, new[] { ("ThickHide", 1), ("Sticks", 1) });
        return $"recipes: {moved} moved; bows to the Hunter's Lodge, shields to the Woodcutter's Lodge, the rucksack to the Tailor's Shop";
    }

    private static void YieldRecipe(string file, string output, int stationTier, int amount, CraftingStationType station, float seconds, (string, int)[] cost)
    {
        Recipe(file, Items[output], amount, station, seconds, stationTier, cost);
        Edit(Recipes.TryGetValue(file, out RecipeDefinition recipe) ? recipe : AssetDatabase.LoadAssetAtPath<RecipeDefinition>(RecipeFolder + file + ".asset"),
            so => so.FindProperty("displayName").stringValue = $"Cured Leather x{amount}");
    }

    // ----- The Loom and Tanning Rack become whole buildings -----

    private static string RenamedBuildings()
    {
        Whole("TanningRack", "Hunter's Lodge", "Where hides are cured and bows are strung. Hunters know how to make more of a kill, and where to find the next.",
            new[] { ("OakWood", 12), ("NettleCord", 4), ("RoughHide", 2) }, 8f, 8);
        Whole("Loom", "Tailor's Shop", "Linen, leather goods and packs. Everything that goes on your back, and what lets you carry more on it.",
            new[] { ("OakPlank", 10), ("NettleCord", 6), ("OakWood", 4) }, 8f, 8);
        Rebuild("TanningRack", "Hunters Lodge Model", HunterModel);
        Rebuild("Loom", "Tailors Shop Model", TailorModel);
        Edit(Buildings["Sawbench"], so => so.FindProperty("description").stringValue = "Turns logs into planks, and planks into shields. Woodcutters know how to get more from a tree.");
        return "renamed: Hunter's Lodge and Tailor's Shop, with whole models and a part for each tier";
    }

    private static void Whole(string file, string label, string description, (string item, int amount)[] cost, float work, int renown)
    {
        BuildingDefinition building = Buildings[file];
        Edit(building, so =>
        {
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("yardDepth").floatValue = 2f;
            so.FindProperty("constructionCost").ClearArray();
            SetAmounts(so.FindProperty("constructionItems"), cost);
        });
        building.ConfigureBuildingUp(work, renown, new List<BuildingLevel>(building.Upgrades));
        EditorUtility.SetDirty(building);
    }

    // ----- Buildings that open later, so the chain goes on past copper -----

    private static string NewBuildings()
    {
        BuildingDefinition hut = NewBuilding("Hut", "Hut", "A low roof, two bunks and a door that shuts. Nobody needs one to live here, but people work better for a night's sleep.", 12, 6f, 4,
            new[] { ("OakWood", 8), ("Sticks", 6), ("NettleFiber", 4) }, HutModel, Req("ConstructionTable", 1));
        Edit(hut, so =>
        {
            so.FindProperty("beds").intValue = 2;
            so.FindProperty("comfort").intValue = 1;
        });

        BuildingDefinition post = NewBuilding("TradingPost", "Trading Post", "A caravan stops here now that the road is known. It buys what raids turn up and sells what they won't.", 14, 10f, 10,
            new[] { ("HardwoodPlank", 8), ("StoneBlock", 6), ("Linen", 4), ("CopperIngot", 4) }, TradingPostModel, Req("ConstructionTable", 2));
        Edit(post, so => so.FindProperty("requiredRank").intValue = 3);
        AddUse<TradingPost>(post);

        var chill = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(Data + "Combat/Status/Chill.asset");
        BuildingDefinition totem = NewBuilding("WardTotem", "Runic Ward Totem", "Old cold, carved into stone. In a breach, whatever comes near it slows and wears down. Put it where they will come.", 21, 8f, 8,
            new[] { ("GraniteBlock", 4), ("Deepstone", 2), ("RunestoneFragment", 1) }, TotemModel, Req("WatchPost", 3), Req("PurificationAltar", 1));
        EditPrefab(totem, root =>
        {
            WardTotem component = root.GetComponent<WardTotem>() ?? root.AddComponent<WardTotem>();
            Edit(component, so => so.FindProperty("effect").objectReferenceValue = chill);
        });

        BuildingDefinition stone = CopyBuilding("StoneWall", "Palisade", "Stone Wall", "Coursed stone, chest high. Attackers have to go round it or spend a long time trying.", 8, Req("ConstructionTable", 3),
            new[] { ("StoneBlock", 4), ("GraniteBlock", 1) });
        CombatStatsDefinition stats = StrongerStats("StoneStructureStats", "WoodenStructureStats", 3f);
        Edit(stone, so => so.FindProperty("combatStats").objectReferenceValue = stats);
        EditPrefab(stone, root => TintEverything(root, Rock));

        BuildingDefinition trap = CopyBuilding("IronTrap", "SpikeTrap", "Iron Trap", "Forged jaws under a thin cover. It bites far harder than a stake, and your people know where to step.", 4, Req("Forge", 3),
            new[] { ("IronIngot", 2), ("HardwoodPlank", 2) });
        EditPrefab(trap, root =>
        {
            Edit(root.GetComponent<SpikeTrap>(), so =>
            {
                so.FindProperty("damage").floatValue = 75f;
                so.FindProperty("rearmSeconds").floatValue = 3f;
            });
            TintEverything(root, Iron);
        });

        // Someone up on the Watch Post with a bow from its second tier.
        EditPrefab(Buildings["WatchPost"], root =>
        {
            if (root.GetComponent<WatchPostArcher>() == null)
            {
                root.AddComponent<WatchPostArcher>();
            }
        });
        return "new buildings: Hut, Trading Post, Runic Ward Totem, Stone Wall, Iron Trap; the Watch Post shoots in a breach";
    }

    private static BuildingRequirement Req(string building, int tier) => new BuildingRequirement(Buildings[building], tier);

    private static BuildingDefinition NewBuilding(string file, string label, string text, int order, float work, int renown, (string item, int amount)[] cost, Action<Transform> shape, params BuildingRequirement[] requires)
    {
        string path = BuildingFolder + file + ".asset";
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(building, path);
        }

        Edit(building, so =>
        {
            so.FindProperty("id").stringValue = file.ToLowerInvariant();
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("knownFromStart").boolValue = true;
            so.FindProperty("showInBuildMenu").boolValue = true;
            so.FindProperty("sortOrder").intValue = order;
            so.FindProperty("yardDepth").floatValue = 1.5f;
            so.FindProperty("combatStats").objectReferenceValue = Buildings["Well"].CombatStats;
            so.FindProperty("constructionCost").ClearArray();
            SetAmounts(so.FindProperty("constructionItems"), cost);
            SetRequirements(so.FindProperty("requires"), requires);
        });
        building.ConfigureBuildingUp(work, renown, new List<BuildingLevel>(building.Upgrades));
        EditorUtility.SetDirty(building);

        string modelName = label.Replace("'", "") + " Model";
        GameObject model = SaveModel(modelName, shape);
        if (building.Prefab == null)
        {
            GameObject prefab = BuildingPrefabBuilder.Create(building, model);
            Edit(building, so => so.FindProperty("prefab").objectReferenceValue = prefab);
            Buildings[file] = building;
        }

        // Levels, the scaffold and the raised model all hang on this.
        EditPrefab(building, root =>
        {
            if (root.GetComponent<Construction>() == null)
            {
                root.AddComponent<Construction>();
            }
        });
        BuildingPrefabBuilder.Update(building.Prefab, building);

        Sprite icon = IconRenderer.RenderToSprite(model, HavenIcons + label.Replace("'", "") + ".png");
        Edit(building, so => so.FindProperty("icon").objectReferenceValue = icon);
        return building;
    }

    private static BuildingDefinition CopyBuilding(string file, string template, string label, string text, int renown, BuildingRequirement requires, (string item, int amount)[] cost)
    {
        string path = BuildingFolder + file + ".asset";
        BuildingDefinition source = Buildings[template];
        if (!File(path))
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path);
        }

        string prefabPath = "Assets/_Project/Prefabs/Structures/" + label + ".prefab";
        if (!File(prefabPath))
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.Prefab), prefabPath);
        }

        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Edit(building, so =>
        {
            so.FindProperty("id").stringValue = file.ToLowerInvariant();
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("renown").intValue = renown;
            so.FindProperty("prefab").objectReferenceValue = prefab;
            SetAmounts(so.FindProperty("constructionItems"), cost);
            so.FindProperty("constructionCost").ClearArray();
            SetRequirements(so.FindProperty("requires"), new[] { requires });
        });
        Buildings[file] = building;
        BuildingPrefabBuilder.Update(prefab, building);
        return building;
    }

    private static CombatStatsDefinition StrongerStats(string file, string template, float scale)
    {
        string path = Data + "Combat/Stats/" + file + ".asset";
        if (!File(path))
        {
            AssetDatabase.CopyAsset(Data + "Combat/Stats/" + template + ".asset", path);
        }

        var stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(path);
        var source = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(Data + "Combat/Stats/" + template + ".asset");
        Edit(stats, so =>
        {
            SerializedProperty to = so.FindProperty("stats");
            SerializedProperty from = new SerializedObject(source).FindProperty("stats");
            for (int i = 0; i < to.arraySize; i++)
            {
                if (to.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue == (int)StatType.MaxHealth)
                {
                    to.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = from.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue * scale;
                }
            }
        });
        return stats;
    }

    private static void TintEverything(GameObject root, Material material)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is ParticleSystemRenderer))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            }
        }
    }

    private static void AddUse<T>(BuildingDefinition building) where T : MonoBehaviour
    {
        EditPrefab(building, root =>
        {
            if (root.GetComponent<Interactable>() == null)
            {
                root.AddComponent<Interactable>().Configure(InteractionAnimation.None, 0.3f, 2.5f, false);
            }

            if (root.GetComponent<T>() == null)
            {
                root.AddComponent<T>();
            }
        });
    }

    private static void EditPrefab(BuildingDefinition building, Action<GameObject> change)
    {
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

    // ----- Every level brings something. Numbers were set against raids in Docs/BaseProgression.md; none is playtested yet. -----

    private static BuildingLevel Level(string title, string description, float work, int renown, (string item, int amount)[] cost, int essence = 0, float speed = 0f, int queue = 0, int output = 0,
        int comfort = 0, int logs = 0, int beds = 0, float yield = 0f, int meals = 0, int game = 0, float light = 0f, int storage = 0, params BuildingRequirement[] requires)
    {
        var level = new BuildingLevel { title = title, description = description, work = work, renown = renown, craftSpeed = speed, extraQueue = queue, extraOutput = output,
            comfort = comfort, extraLogs = logs, beds = beds, purifyYield = yield, extraMealSlots = meals, extraGame = game, extraLight = light, extraStorage = storage };
        foreach ((string item, int amount) in cost)
        {
            level.cost.Add(new ItemAmount(Items[item], amount));
        }

        if (essence > 0)
        {
            level.essence.Add(new ResourceAmount(SettlementSettings.Current.PurifiedEssence, essence));
        }

        level.requires.AddRange(requires);
        return level;
    }

    private static void Set(string file, params BuildingLevel[] levels)
    {
        BuildingDefinition building = Buildings[file];
        building.ConfigureBuildingUp(building.BuildWork, building.Renown, levels);
        EditorUtility.SetDirty(building);
    }

    private static void Needs(string file, int level, params BuildingRequirement[] requires)
    {
        BuildingLevel upgrade = Buildings[file].UpgradeTo(level);
        upgrade.requires.Clear();
        upgrade.requires.AddRange(requires);
        EditorUtility.SetDirty(Buildings[file]);
    }

    private static string Upgrades()
    {
        Set("Campfire", Level("Stone Hearth", "A ring of stones that holds the heat, and the light reaches further into the dark.", 7.5f, 3, new[] { ("GreyStone", 6) }, speed: 0.25f, light: 4f));

        Set("TanningRack",
            Level("Drying Racks", "Racks in the yard and a place to hang a bow: Hunter's Bows, better leather from the same hide, and more game at home.", 8f, 4, new[] { ("OakPlank", 8), ("RoughHide", 4), ("CopperIngot", 2) }, speed: 0.2f, queue: 1, game: 1),
            Level("Smokehouse", "It keeps the flies off and the deer coming: Elderwood Longbows and more leather from thick hide.", 10f, 6, new[] { ("HardwoodPlank", 6), ("IronIngot", 3), ("ThickHide", 2) }, speed: 0.2f),
            Level("Moon Racks", "Moonsilver wire on the racks: Moonstrung Bows, and game wanders close.", 12f, 8, new[] { ("Elderwood", 6), ("MoonsilverIngot", 3), ("HuldraSilk", 2) }, speed: 0.2f, game: 1, requires: Req("Forge", 3)),
            Level("Troll Hall", "Horn over the door, and nothing here is afraid any more: Trollhorn Bows.", 14f, 12, new[] { ("Elderwood", 4), ("Trollstone", 2) }, 10, speed: 0.25f, requires: Req("Forge", 4)));

        Set("Loom",
            Level("Cutting Table", "A proper table and good shears: the leather set, and the Hide Rucksack.", 8f, 4, new[] { ("OakPlank", 6), ("NettleCord", 4), ("Linen", 2) }, speed: 0.3f),
            Level("Pattern Wall", "Patterns for what goes on a long road: the Traveller's Pack.", 9f, 6, new[] { ("HardwoodPlank", 6), ("Linen", 4), ("ThickHide", 2) }, speed: 0.3f, queue: 2),
            Level("Silk Frames", "Frames fine enough for huldra silk: the Wayfarer's Frame.", 11f, 8, new[] { ("HuldraSilk", 4), ("MoonsilverIngot", 2), ("Linen", 6) }, speed: 0.3f, requires: Req("Forge", 3)));

        Set("Kitchen",
            Level("Stocked Larder", "Shelves for what you've learned to keep: Boar Pie and Trout in Cream, meals that scale with you.", 8f, 4, new[] { ("OakPlank", 8), ("StoneBlock", 4), ("CopperIngot", 2) }, speed: 0.3f, queue: 1, requires: Req("FarmPlot", 1)),
            Level("The Long Table", "Everyone eats together: the Haven Feast and Moon Broth, and you can carry one more meal into the woods.", 10f, 6, new[] { ("HardwoodPlank", 6), ("IronIngot", 4), ("MoonsilverIngot", 2) }, speed: 0.3f, queue: 2, meals: 1, requires: Req("TanningRack", 3)));

        Set("Cauldron",
            Level("Drying Hooks", "A hook to hang herbs to dry: Greater Healing Draughts and Swiftfoot Tonic.", 8f, 4, new[] { ("OakWood", 6), ("GreyStone", 6) }, speed: 0.3f),
            Level("Master's Shelf", "What the deep woods ask for: Radiant Draught, Ironbark and Frostward.", 9f, 6, new[] { ("HardwoodPlank", 6), ("IronIngot", 2) }, 8, speed: 0.3f, queue: 2, requires: Req("PurificationAltar", 2)));

        Set("PurificationAltar",
            Level("Carved", "Runes cut deep around the basin: a quarter more Purified Essence from the same curse.", 9f, 6, new[] { ("StoneBlock", 12), ("GraniteBlock", 2) }, speed: 0.25f, yield: 0.25f),
            Level("Radiant", "It hums softly at night, and hardly anything is wasted.", 10f, 8, new[] { ("GraniteBlock", 6), ("HardwoodPlank", 3) }, 8, speed: 0.25f, yield: 0.25f));

        Set("Cottage", Level("Whitewashed", "Lime on the walls, smoke from the chimney and two more beds.", 9f, 6, new[] { ("StoneBlock", 4), ("OakPlank", 6) }, comfort: 2, beds: 2));
        Edit(Buildings["Cottage"], so =>
        {
            so.FindProperty("beds").intValue = 4;
            so.FindProperty("description").stringValue = "A real roof over someone's head, and room for four. It makes Safe Haven a village, and its people work the better for it.";
        });
        Set("Hut", Level("Thatched", "A proper roof, and a third bunk.", 6f, 2, new[] { ("OakPlank", 6), ("NettleCord", 4) }, comfort: 1, beds: 1));

        // Later tiers ask for other buildings, so the whole haven climbs together rather than one line.
        Needs("Forge", 3, Req("TanningRack", 1), Req("Loom", 1));
        Needs("Forge", 4, Req("PurificationAltar", 2));
        Needs("Forge", 5, Req("ConstructionTable", 4));
        Needs("ConstructionTable", 3, Req("Sawbench", 2));
        Needs("ConstructionTable", 4, Req("Forge", 3));
        Needs("ConstructionTable", 5, Req("Forge", 4));
        Needs("Sawbench", 4, Req("Forge", 3));
        Needs("Sawbench", 5, Req("Forge", 4));
        Needs("WatchPost", 3, Req("Forge", 2));

        // A Tailor's Shop needs the Hunter's Lodge and the Trading Post a road to it; the rest of the chain is unchanged.
        Edit(Buildings["Loom"], so => SetRequirements(so.FindProperty("requires"), new[] { Req("TanningRack", 1) }));
        Edit(Buildings["TanningRack"], so => SetRequirements(so.FindProperty("requires"), new[] { Req("ConstructionTable", 1) }));
        return "upgrades: no level is speed alone; Smithy III wants the Lodge and Shop, IV wants the Altar, the Workshop follows the Smithy";
    }

    // ----- People -----

    private static string People()
    {
        ResidentDefinition Settler(string file, string label, string text, string arrival, string model, string[] jobs)
        {
            string path = Data + "Settlement/" + file + ".asset";
            var resident = AssetDatabase.LoadAssetAtPath<ResidentDefinition>(path);
            if (resident == null)
            {
                resident = ScriptableObject.CreateInstance<ResidentDefinition>();
                AssetDatabase.CreateAsset(resident, path);
            }

            Edit(resident, so =>
            {
                so.FindProperty("id").stringValue = "settler." + file.ToLowerInvariant();
                so.FindProperty("displayName").stringValue = label;
                so.FindProperty("description").stringValue = text;
                so.FindProperty("arrivalLine").stringValue = arrival;
                so.FindProperty("model").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Characters/People/" + model + ".prefab");
                SerializedProperty list = so.FindProperty("jobs");
                list.arraySize = jobs.Length;
                for (int i = 0; i < jobs.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<JobDefinition>(Data + "Settlement/" + jobs[i] + ".asset");
                }
            });
            return resident;
        }

        Job("Mason", "job.mason", "Mason", "Runs the Workshop's stone bench: keeps grey stone and granite turning into blocks while you are away, and the work goes faster.", "\"Stone wants to be square. It just needs telling.\"", "ConstructionTable",
            ("StoneBlock", 30, 8), ("GraniteBlock", 20, 4));
        Job("Farmer", "job.farmer", "Farmer", "Tends the Farm Plot: keeps what you have planted growing from the stash's seed, and it grows faster.", "\"Leave me the seed. The rest takes care of itself, with a bit of shouting.\"", "FarmPlot",
            ("FarmBarley", 30, 2), ("FarmTurnips", 30, 2), ("FarmFlax", 20, 2), ("FarmHealingHerb", 20, 2));
        Job("Weaver", "job.weaver", "Weaver", "Runs the Tailor's Shop: keeps flax turning into linen while you are away, and the work goes faster.", "\"Good cloth takes as long as it takes.\"", "Loom",
            ("Linen", 20, 3));
        Job("Hunter", "job.hunter", "Hunter", "Runs the Hunter's Lodge: keeps hides turning into leather while you are away, and the work goes faster.", "\"You bring the hides. I'll bring the patience.\"", "TanningRack",
            ("CuredLeather", 24, 2), ("CuredThickHide", 24, 1));
        Job("Cook", "job.cook", "Cook", "Runs the Kitchen: keeps bread and stew in the stash for you to take on the road, and the work goes faster.", "\"Sit. Eat. Then you can go and get yourself killed.\"", "Kitchen",
            ("BarleyBread", 10, 4), ("HuntersStew", 6, 2));

        ResidentDefinition hild = Settler("Hild", "Hild", "A mason who followed the smoke. She has opinions about your footings.", "Whoever laid this hearth had never met a level. I'll stay.", "Sigrid", new[] { "Mason" });
        ResidentDefinition asa = Settler("Asa", "Asa", "A farmer from a village that isn't there any more. She brought the seed, and little else.", "There's good dirt under all this moss. Who's been using it?", "Runa", new[] { "Farmer", "Weaver" });
        ResidentDefinition gunnar = Settler("Gunnar", "Gunnar", "A hunter who has been walking these woods for years, alone by choice, until the choice ran out.", "You've been frightening my deer. Least you can do is give me a roof.", "Bjarne", new[] { "Hunter" });
        ResidentDefinition yrsa = Settler("Yrsa", "Yrsa", "She cooked for a jarl once, and tells you so. People came from three valleys for her stew.", "Show me your kitchen. No, on second thought, show me what you eat.", "Ingrid", new[] { "Cook" });

        // Liv keeps what only she knows; the Leader's watch is worth a real stretch of the clock.
        var scholar = AssetDatabase.LoadAssetAtPath<JobDefinition>(Data + "Settlement/RuneScholar.asset");
        Edit(scholar, so =>
        {
            so.FindProperty("description").stringValue = "Knows the old marks: she teaches runes nobody else can, Hush and the Deep Rune, along with the rest, and work at the cauldron goes faster.";
            SerializedProperty list = so.FindProperty("teachesRecipes");
            foreach (string recipe in new[] { "HushRune", "DeepWoodsRune" })
            {
                AddOnce(list, AssetDatabase.LoadAssetAtPath<RecipeDefinition>(RecipeFolder + recipe + ".asset"));
            }
        });
        Edit(AssetDatabase.LoadAssetAtPath<JobDefinition>(Data + "Settlement/Leader.asset"), so =>
        {
            so.FindProperty("description").stringValue = "Keeps the portal's watch: two more minutes in every raid, and everyone knows where they stand.";
            so.FindProperty("raidSeconds").floatValue = 120f;
        });
        JobOrders("Smith", ("MoonsilverIngot", 16, 3), ("IronIngot", 20, 5), ("CopperIngot", 30, 5));

        Edit(HavenSettings.Current, so =>
        {
            so.FindProperty("restedSecondsPerComfort").floatValue = 60f;
            SerializedProperty bonuses = so.FindProperty("restedBonuses");
            SetStats(bonuses, new[] { (StatType.MaxStamina, StatModifierOp.Percent, 0.15f), (StatType.MaxHealth, StatModifierOp.Percent, 0.1f) });
            SerializedProperty ranks = so.FindProperty("ranks");
            int[] renown = { 0, 25, 90, 180, 300 };
            // Nobody arrives with a rank (playtest 2026-11-08: Hild turned up unexplained). Everyone comes home from a raid.
            for (int i = 0; i < ranks.arraySize && i < renown.Length; i++)
            {
                ranks.GetArrayElementAtIndex(i).FindPropertyRelative("renown").intValue = renown[i];
                ranks.GetArrayElementAtIndex(i).FindPropertyRelative("settler").objectReferenceValue = null;
            }
        });
        return "people: nobody comes with a rank; Bjarne smelts moonsilver; Liv keeps two runes of her own; Sigrid adds 2 minutes";
    }

    private static void Job(string file, string id, string label, string description, string accept, string workplace, params (string recipe, int stock, int keep)[] orders)
    {
        string path = Data + "Settlement/" + file + ".asset";
        var job = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
        if (job == null)
        {
            job = ScriptableObject.CreateInstance<JobDefinition>();
            AssetDatabase.CreateAsset(job, path);
        }

        Edit(job, so =>
        {
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("acceptLine").stringValue = accept;
            so.FindProperty("workplace").objectReferenceValue = Buildings[workplace];
        });
        JobOrders(file, orders);
    }

    private static void JobOrders(string file, params (string recipe, int stock, int keep)[] orders)
    {
        Edit(AssetDatabase.LoadAssetAtPath<JobDefinition>(Data + "Settlement/" + file + ".asset"), so =>
        {
            SerializedProperty list = so.FindProperty("workOrders");
            list.arraySize = orders.Length;
            for (int i = 0; i < orders.Length; i++)
            {
                SerializedProperty order = list.GetArrayElementAtIndex(i);
                order.FindPropertyRelative("recipe").objectReferenceValue = Recipes[orders[i].recipe];
                order.FindPropertyRelative("stockTarget").intValue = orders[i].stock;
                order.FindPropertyRelative("keepInput").intValue = orders[i].keep;
            }
        });
    }

    // ----- The rules that hold it together -----

    private static string Rules()
    {
        Edit(SettlementSettings.Current, so =>
        {
            SerializedProperty levels = so.FindProperty("wardLevels");
            levels.arraySize = 3;
            string[] opens = { "Opens tier II raids.", "Opens tier III raids.", "Opens tier IV raids." };
            for (int i = 0; i < 3; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("opens").stringValue = opens[i];
                level.FindPropertyRelative("extraRadius").floatValue = 6f;
                level.FindPropertyRelative("raidSeconds").floatValue = 90f;
            }

            SetResources(levels.GetArrayElementAtIndex(2).FindPropertyRelative("cost"), ("PurifiedEssence", 20), ("LifeOrb", 6));

            SerializedProperty portal = so.FindProperty("portalLevels");
            portal.arraySize = 3;
            (string title, string text, (string, int)[] resources, (string, int)[] items, int pockets, float cut)[] wanted =
            {
                ("Steadied", "A second safe pocket: what you put there comes home even from a lost raid.", new[] { ("LifeOrb", 4) }, new[] { ("CopperIngot", 4), ("StoneBlock", 4) }, 1, 0f),
                ("Bound", "Iron ties in the stones: every way home opens 20% faster.", new[] { ("LifeOrb", 6) }, new[] { ("IronIngot", 4), ("GraniteBlock", 4) }, 0, 0.2f),
                ("Moonlit", "A third safe pocket, and the way home opens faster again.", new[] { ("LifeOrb", 8), ("PurifiedEssence", 10) }, new[] { ("MoonsilverIngot", 4), ("Deepstone", 2) }, 1, 0.2f),
            };
            for (int i = 0; i < wanted.Length; i++)
            {
                SerializedProperty level = portal.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("title").stringValue = wanted[i].title;
                level.FindPropertyRelative("description").stringValue = wanted[i].text;
                SetResources(level.FindPropertyRelative("cost"), wanted[i].resources);
                SetAmounts(level.FindPropertyRelative("items"), wanted[i].items);
                level.FindPropertyRelative("extraPocketSlots").intValue = wanted[i].pockets;
                level.FindPropertyRelative("extractionCut").floatValue = wanted[i].cut;
            }
        });

        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(Data + "Raids/MistyWoods.asset");
        Edit(raid, so =>
        {
            SerializedProperty tiers = so.FindProperty("difficulties");
            for (int i = 0; i < tiers.arraySize; i++)
            {
                SerializedProperty tier = tiers.GetArrayElementAtIndex(i);
                tier.FindPropertyRelative("wardLevelNeeded").intValue = tier.FindPropertyRelative("tier").intValue - 1;
            }
        });

        var folio = AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>(Data + "Raids/Rumours/Folio.asset");
        Edit(folio, so =>
        {
            so.FindProperty("needsBuilding").objectReferenceValue = Buildings["Forge"];
            so.FindProperty("needsTier").intValue = 3;
            so.FindProperty("urgent").boolValue = true;
            so.FindProperty("afterExtractions").intValue = 0;
        });
        return "rules: ward I, II, III open raid tiers II, III, IV; three portal improvements; the Folio is sought as soon as the Smithy can use it";
    }

    private static string Trade()
    {
        const string path = "Assets/_Project/Resources/TradeSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<TradeSettings>(path);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<TradeSettings>();
            AssetDatabase.CreateAsset(settings, path);
        }

        (string item, int amount, int price, int stock, int smithy)[] offers =
        {
            ("CopperOre", 6, 2, 3, 0), ("Granite", 4, 2, 2, 1), ("Hardwood", 3, 2, 2, 1), ("Flax", 3, 2, 2, 0),
            ("IronOre", 6, 4, 3, 2), ("ThickHide", 1, 4, 2, 2), ("MoonsilverOre", 3, 10, 2, 3),
            ("Deepstone", 1, 10, 1, 3), ("Elderwood", 1, 10, 1, 3), ("HuldraSilk", 1, 10, 1, 3), ("Trollstone", 1, 35, 1, 4),
        };
        (string item, int coins)[] sales = { ("WovenHideLeggings", 1), ("RunestoneFragment", 6), ("GildedTrollIdol", 15), ("HeartOfTheForest", 40) };
        Edit(settings, so =>
        {
            so.FindProperty("coin").objectReferenceValue = Items["OldSilverPenny"];
            SerializedProperty sold = so.FindProperty("sales");
            sold.arraySize = sales.Length;
            for (int i = 0; i < sales.Length; i++)
            {
                sold.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Items[sales[i].item];
                sold.GetArrayElementAtIndex(i).FindPropertyRelative("coins").intValue = sales[i].coins;
            }

            SerializedProperty list = so.FindProperty("offers");
            list.arraySize = offers.Length;
            for (int i = 0; i < offers.Length; i++)
            {
                SerializedProperty offer = list.GetArrayElementAtIndex(i);
                offer.FindPropertyRelative("item").objectReferenceValue = Items[offers[i].item];
                offer.FindPropertyRelative("amount").intValue = offers[i].amount;
                offer.FindPropertyRelative("price").intValue = offers[i].price;
                offer.FindPropertyRelative("stock").intValue = offers[i].stock;
                offer.FindPropertyRelative("smithyTier").intValue = offers[i].smithy;
            }
        });
        return "trade: valuables sell for pennies; pennies buy a little of what raids can leave you short of";
    }

    // ----- Models: placeholders from primitives, with a part for each tier, like the Workshop's -----

    private static void HunterModel(Transform root)
    {
        const float width = 5.6f;
        const float depth = 4.2f;
        Box(root, new Vector3(0f, 0.06f, 0f), new Vector3(width + 0.2f, 0.12f, depth + 0.2f), Plank);
        House(root, width, depth, 2.3f, 1.2f, Plank, Thatch, true);
        // A stretching frame with a hide on it, and a rack of antlers over the door.
        Box(root, new Vector3(1.6f, 0.9f, 0.4f), new Vector3(0.1f, 1.8f, 0.1f), Timber);
        Box(root, new Vector3(0.2f, 0.9f, 0.4f), new Vector3(0.1f, 1.8f, 0.1f), Timber);
        Box(root, new Vector3(0.9f, 1.75f, 0.4f), new Vector3(1.6f, 0.1f, 0.1f), Timber);
        Box(root, new Vector3(0.9f, 1f, 0.4f), new Vector3(1.2f, 1.3f, 0.04f), Hide);
        Box(root, new Vector3(0f, 2.15f, depth * 0.5f + 0.12f), new Vector3(0.7f, 0.1f, 0.1f), Bone, new Vector3(0f, 0f, 22f));
        Box(root, new Vector3(0f, 2.15f, depth * 0.5f + 0.12f), new Vector3(0.7f, 0.1f, 0.1f), Bone, new Vector3(0f, 0f, -22f));

        Transform two = Tier(root, 2);
        Box(two, new Vector3(-1.5f, 1f, -1.6f), new Vector3(1.8f, 0.08f, 0.5f), Timber);
        Box(two, new Vector3(-1.5f, 1.2f, -1.6f), new Vector3(1.5f, 0.4f, 0.06f), Hide);

        Transform three = Tier(root, 3);
        Box(three, new Vector3(width * 0.5f + 0.9f, 1.1f, -0.6f), new Vector3(1.4f, 2.2f, 1.4f), Plank);
        Box(three, new Vector3(width * 0.5f + 0.9f, 2.4f, -0.6f), new Vector3(1.7f, 0.2f, 1.7f), Shingle);
        Lantern(three, new Vector3(width * 0.5f - 0.3f, 1.9f, depth * 0.5f));

        Transform four = Tier(root, 4);
        Box(four, new Vector3(0.9f, 1.82f, 0.4f), new Vector3(1.7f, 0.06f, 0.14f), Moonsilver);
        Box(four, new Vector3(0f, 2.3f + 1.2f + 0.16f, 0f), new Vector3(0.36f, 0.1f, depth + 0.75f), Moonsilver);

        Transform five = Tier(root, 5);
        Box(five, new Vector3(0f, 2.55f, depth * 0.5f + 0.2f), new Vector3(0.5f, 0.6f, 0.18f), TrollRock);
        Banner(five, new Vector3(-width * 0.5f - 0.05f, 0f, depth * 0.5f + 0.05f), Cloth);
    }

    private static void TailorModel(Transform root)
    {
        const float width = 5.6f;
        const float depth = 4.4f;
        Box(root, new Vector3(0f, 0.06f, 0f), new Vector3(width + 0.2f, 0.12f, depth + 0.2f), Plank);
        House(root, width, depth, 2.5f, 1.3f, Plank, Shingle, false);
        // The loom itself stands out under a lean-to, with bolts of cloth beside it.
        Box(root, new Vector3(width * 0.5f + 0.9f, 2.2f, 0f), new Vector3(1.8f, 0.12f, 3f), Thatch);
        foreach (float z in new[] { -1.3f, 1.3f })
        {
            Box(root, new Vector3(width * 0.5f + 1.7f, 1.1f, z), new Vector3(0.14f, 2.2f, 0.14f), Timber);
        }

        Box(root, new Vector3(width * 0.5f + 0.9f, 0.9f, 0f), new Vector3(0.1f, 1.8f, 1.4f), Timber);
        Box(root, new Vector3(width * 0.5f + 0.95f, 0.95f, 0f), new Vector3(0.03f, 1.2f, 1.2f), Linen);
        Box(root, new Vector3(0f, 2.05f, depth * 0.5f + 0.1f), new Vector3(1f, 0.22f, 0.08f), Cloth);

        Transform two = Tier(root, 2);
        Box(two, new Vector3(width * 0.5f + 0.9f, 0.5f, 1.1f), new Vector3(0.9f, 0.08f, 0.9f), Plank);
        Box(two, new Vector3(width * 0.5f + 0.9f, 0.62f, 1.1f), new Vector3(0.7f, 0.14f, 0.5f), Linen);

        Transform three = Tier(root, 3);
        for (int i = 0; i < 3; i++)
        {
            Box(three, new Vector3(width * 0.5f + 0.5f + i * 0.34f, 0.2f, -1.1f), new Vector3(0.28f, 0.6f, 0.28f), i == 1 ? Cloth : Linen, new Vector3(90f, 0f, 0f), PrimitiveType.Cylinder);
        }

        Lantern(three, new Vector3(width * 0.5f - 0.3f, 2.1f, depth * 0.5f));

        Transform four = Tier(root, 4);
        Box(four, new Vector3(width * 0.5f + 0.95f, 0.95f, 0f), new Vector3(0.04f, 1.2f, 1.2f), Moonsilver);
        Banner(four, new Vector3(-width * 0.5f - 0.05f, 0f, depth * 0.5f + 0.05f), Cloth);
    }

    private static void HutModel(Transform root)
    {
        House(root, 3.6f, 3f, 1.7f, 1f, Plank, Thatch, false);
        Transform two = Tier(root, 2);
        Box(two, new Vector3(0f, 1.7f + 1f + 0.12f, 0f), new Vector3(0.3f, 0.12f, 3.6f), Thatch);
        Lantern(two, new Vector3(1.5f, 1.5f, 1.5f));
    }

    private static void TradingPostModel(Transform root)
    {
        const float width = 5f;
        const float depth = 3.6f;
        Box(root, new Vector3(0f, 0.06f, 0f), new Vector3(width + 0.2f, 0.12f, depth + 0.2f), Rock);
        House(root, width, depth, 2.4f, 1.2f, Plank, Shingle, true);
        Box(root, new Vector3(0f, 0.6f, depth * 0.5f - 0.3f), new Vector3(width - 0.8f, 1.1f, 0.5f), Plank);
        foreach (float x in new[] { -1.4f, 0f, 1.4f })
        {
            Box(root, new Vector3(x, 0.4f, -depth * 0.5f + 0.6f), new Vector3(0.9f, 0.8f, 0.9f), Timber);
        }

        Box(root, new Vector3(0f, 2.75f, depth * 0.5f + 0.5f), new Vector3(width + 0.6f, 0.08f, 1.3f), Cloth, new Vector3(16f, 0f, 0f));
        Lantern(root, new Vector3(width * 0.5f - 0.3f, 2f, depth * 0.5f));
    }

    private static void TotemModel(Transform root)
    {
        Box(root, new Vector3(0f, 0.2f, 0f), new Vector3(1.4f, 0.4f, 1.4f), Rock);
        Box(root, new Vector3(0f, 1.5f, 0f), new Vector3(0.7f, 2.4f, 0.7f), Rock);
        foreach (float y in new[] { 0.9f, 1.5f, 2.1f })
        {
            Box(root, new Vector3(0f, y, 0.36f), new Vector3(0.36f, 0.14f, 0.03f), Frost);
        }

        Box(root, new Vector3(0f, 2.85f, 0f), new Vector3(0.42f, 0.42f, 0.42f), Frost, new Vector3(45f, 0f, 45f));
    }

    private static Material Tint(string name, Color color, Color? glow = null)
    {
        string path = Tints + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.15f);
        if (glow.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", glow.Value);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material Timber => Tint("Base Timber", new Color(0.24f, 0.15f, 0.09f));
    private static Material Plank => Tint("Base Plank", new Color(0.42f, 0.29f, 0.17f));
    private static Material Thatch => Tint("Base Thatch", new Color(0.46f, 0.38f, 0.2f));
    private static Material Shingle => Tint("Base Shingle", new Color(0.2f, 0.17f, 0.15f));
    private static Material Rock => Tint("Base Stone", new Color(0.38f, 0.37f, 0.35f));
    private static Material Iron => Tint("Base Iron", new Color(0.13f, 0.13f, 0.14f));
    private static Material Moonsilver => Tint("Base Moonsilver", new Color(0.72f, 0.78f, 0.86f), new Color(0.15f, 0.2f, 0.28f));
    private static Material Ember => Tint("Base Ember", new Color(0.9f, 0.35f, 0.1f), new Color(3f, 1f, 0.2f));
    private static Material Cloth => Tint("Base Cloth", new Color(0.46f, 0.14f, 0.1f));
    private static Material TrollRock => Tint("Base Trollstone", new Color(0.2f, 0.27f, 0.2f));
    private static Material Hide => Tint("Base Hide", new Color(0.5f, 0.36f, 0.24f));
    private static Material Bone => Tint("Base Bone", new Color(0.8f, 0.76f, 0.64f));
    private static Material Linen => Tint("Base Linen", new Color(0.78f, 0.74f, 0.62f));
    private static Material Frost => Tint("Base Frost", new Color(0.5f, 0.78f, 0.92f), new Color(0.3f, 0.7f, 1f));

    private static Transform Box(Transform parent, Vector3 position, Vector3 size, Material material, Vector3 tilt = default, PrimitiveType type = PrimitiveType.Cube)
    {
        GameObject piece = GameObject.CreatePrimitive(type);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.name = material.name.Replace("Base ", "");
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localEulerAngles = tilt;
        piece.transform.localScale = size;
        piece.GetComponent<Renderer>().sharedMaterial = material;
        return piece.transform;
    }

    private static Transform Tier(Transform parent, int tier)
    {
        var part = new GameObject("Tier " + tier).transform;
        part.SetParent(parent, false);
        part.gameObject.SetActive(false);
        return part;
    }

    private static void House(Transform parent, float width, float depth, float wall, float pitch, Material walls, Material roof, bool openFront)
    {
        float x = width * 0.5f;
        float z = depth * 0.5f;
        foreach (Vector3 corner in new[] { new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z), new Vector3(-x, 0f, z), new Vector3(x, 0f, z) })
        {
            Box(parent, corner + Vector3.up * wall * 0.5f, new Vector3(0.28f, wall, 0.28f), Timber);
        }

        Box(parent, new Vector3(0f, wall * 0.5f, -z), new Vector3(width, wall, 0.16f), walls);
        Box(parent, new Vector3(-x, wall * 0.5f, 0f), new Vector3(0.16f, wall, depth), walls);
        Box(parent, new Vector3(x, wall * 0.5f, 0f), new Vector3(0.16f, wall, depth), walls);
        if (openFront)
        {
            Box(parent, new Vector3(0f, wall - 0.14f, z), new Vector3(width, 0.28f, 0.2f), Timber);
        }
        else
        {
            Box(parent, new Vector3(-x * 0.6f, wall * 0.5f, z), new Vector3(width * 0.4f, wall, 0.16f), walls);
            Box(parent, new Vector3(x * 0.6f, wall * 0.5f, z), new Vector3(width * 0.4f, wall, 0.16f), walls);
            Box(parent, new Vector3(0f, wall - 0.3f, z), new Vector3(width * 0.2f + 0.02f, 0.6f, 0.16f), walls);
        }

        float slope = Mathf.Sqrt(x * x + pitch * pitch) + 0.35f;
        float angle = Mathf.Atan2(pitch, x) * Mathf.Rad2Deg;
        Box(parent, new Vector3(-x * 0.5f, wall + pitch * 0.5f, 0f), new Vector3(slope, 0.14f, depth + 0.6f), roof, new Vector3(0f, 0f, angle));
        Box(parent, new Vector3(x * 0.5f, wall + pitch * 0.5f, 0f), new Vector3(slope, 0.14f, depth + 0.6f), roof, new Vector3(0f, 0f, -angle));
        Box(parent, new Vector3(0f, wall + pitch, 0f), new Vector3(0.24f, 0.24f, depth + 0.7f), Timber);
        for (int row = 0; row < 3; row++)
        {
            float rise = pitch * (row + 0.5f) / 3f;
            float span = width * (1f - (row + 0.5f) / 3f);
            Box(parent, new Vector3(0f, wall + rise, -z), new Vector3(span, pitch / 3f + 0.02f, 0.14f), walls);
            Box(parent, new Vector3(0f, wall + rise, z), new Vector3(span, pitch / 3f + 0.02f, 0.14f), walls);
        }
    }

    private static void Lantern(Transform parent, Vector3 at)
    {
        Box(parent, at, new Vector3(0.18f, 0.28f, 0.18f), Ember);
        Box(parent, at + Vector3.up * 0.2f, new Vector3(0.26f, 0.06f, 0.26f), Iron);
    }

    private static void Banner(Transform parent, Vector3 at, Material cloth)
    {
        Box(parent, at + Vector3.up * 1.6f, new Vector3(0.1f, 3.2f, 0.1f), Timber);
        Box(parent, at + new Vector3(0.45f, 2.6f, 0f), new Vector3(0.8f, 1.1f, 0.04f), cloth);
    }

    private static GameObject SaveModel(string modelName, Action<Transform> shape)
    {
        PrefabBuildUtility.EnsureFolder(Models.TrimEnd('/'));
        var made = new GameObject(modelName);
        try
        {
            shape(made.transform);
            return PrefabUtility.SaveAsPrefabAsset(made, Models + modelName + ".prefab");
        }
        finally
        {
            Object.DestroyImmediate(made);
        }
    }

    // Swaps the model on a building's existing prefab, keeping its save id and everything else on it.
    private static void Rebuild(string file, string modelName, Action<Transform> shape)
    {
        BuildingDefinition building = Buildings[file];
        GameObject model = SaveModel(modelName, shape);
        string prefabPath = AssetDatabase.GetAssetPath(building.Prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = root.transform.GetChild(i);
                bool isOverlay = child.GetComponentInChildren<Canvas>(true) != null || child.GetComponentInChildren<WorldHealthBar>(true) != null
                    || child.GetComponentInChildren<DamageNumberSpawner>(true) != null;
                if (!isOverlay && child.GetComponentInChildren<Renderer>(true) != null)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            if (root.TryGetComponent(out UnityEngine.AI.NavMeshObstacle obstacle))
            {
                Object.DestroyImmediate(obstacle);
            }

            foreach (BoxCollider box in root.GetComponents<BoxCollider>())
            {
                Object.DestroyImmediate(box);
            }

            if (root.GetComponent<Construction>() == null)
            {
                root.AddComponent<Construction>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        BuildingPrefabBuilder.Update(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), building);
        Sprite icon = IconRenderer.RenderToSprite(model, HavenIcons + building.DisplayName.Replace("'", "") + ".png");
        Edit(building, so => so.FindProperty("icon").objectReferenceValue = icon);
    }

    // ----- Helpers -----

    private static ItemDefinition CopyItem(string path, ItemDefinition template, string label, string text, int tier, ItemRarity rarity, int value, float weight)
    {
        if (!File(path))
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(template), path);
        }

        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        Edit(item, so =>
        {
            so.FindProperty("id").stringValue = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("tier").intValue = tier;
            so.FindProperty("rarity").intValue = (int)rarity;
            so.FindProperty("value").intValue = value;
            so.FindProperty("weight").floatValue = weight;
        });
        Items[item.name] = item;
        return item;
    }

    private static GameObject Tinted(string file, GameObject source, Color tint)
    {
        string path = GearModels + file + " Model.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Metallic", tint.grayscale > 0.45f ? 0.8f : 0.35f);
        material.SetFloat("_Smoothness", 0.45f);
        AssetDatabase.CreateAsset(material, GearTints + file + ".mat");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            }

            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static Sprite RenderIcon(string file, GameObject model)
    {
        string path = GearIcons + file + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return existing != null ? existing : IconRenderer.RenderToSprite(model, path);
    }

    private static Sprite PlaceholderIcon(string file, ArmorSlot slot, Color color)
    {
        string path = GearIcons + file + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return existing != null ? existing : PlaceholderIconPainter.Paint(PlaceholderIconPainter.Guess(file, "", ToolType.None, false, false, slot), color, new Color(0.2f, 0.25f, 0.2f), path);
    }

    // A cost's names may be items or resources: both are looked up here.
    private static void Recipe(string file, ItemDefinition output, int amount, CraftingStationType station, float seconds, int stationTier, (string item, int amount)[] cost, bool known = true)
    {
        string path = RecipeFolder + file + ".asset";
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            AssetDatabase.CreateAsset(recipe, path);
        }

        Edit(recipe, so =>
        {
            so.FindProperty("id").stringValue = file.ToLowerInvariant();
            so.FindProperty("output").objectReferenceValue = output;
            so.FindProperty("outputResource").objectReferenceValue = null;
            so.FindProperty("outputAmount").intValue = amount;
            so.FindProperty("station").intValue = (int)station;
            so.FindProperty("craftSeconds").floatValue = seconds;
            so.FindProperty("knownFromStart").boolValue = known;
            so.FindProperty("minimumStationTier").intValue = stationTier;
            so.FindProperty("sortOrder").intValue = 100 + output.Tier * 10;
            (string, int)[] items = cost.Where(entry => Items.ContainsKey(entry.item)).Select(entry => (entry.item, entry.amount)).ToArray();
            (string, int)[] resources = cost.Where(entry => !Items.ContainsKey(entry.item)).Select(entry => (entry.item, entry.amount)).ToArray();
            SetAmounts(so.FindProperty("itemCost"), items);
            SetResources(so.FindProperty("resourceCost"), resources);
        });
        Recipes[file] = recipe;
    }

    private static void SetAmounts(SerializedProperty list, (string item, int amount)[] cost)
    {
        list.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Items[cost[i].item];
            list.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }
    }

    private static void SetResources(SerializedProperty list, params (string resource, int amount)[] cost)
    {
        list.arraySize = cost.Length;
        for (int i = 0; i < cost.Length; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("resource").objectReferenceValue = Resources_[cost[i].resource];
            list.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
        }
    }

    private static void SetRequirements(SerializedProperty list, BuildingRequirement[] requires)
    {
        list.arraySize = requires.Length;
        for (int i = 0; i < requires.Length; i++)
        {
            list.GetArrayElementAtIndex(i).FindPropertyRelative("building").objectReferenceValue = requires[i].building;
            list.GetArrayElementAtIndex(i).FindPropertyRelative("tier").intValue = requires[i].tier;
        }
    }

    private static void SetStats(SerializedProperty list, (StatType stat, StatModifierOp op, float value)[] stats)
    {
        list.arraySize = stats.Length;
        for (int i = 0; i < stats.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("stat").intValue = (int)stats[i].stat;
            entry.FindPropertyRelative("op").intValue = (int)stats[i].op;
            entry.FindPropertyRelative("value").floatValue = stats[i].value;
        }
    }

    private static void AddOnce(SerializedProperty list, Object value)
    {
        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
            {
                return;
            }
        }

        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = value;
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
    }

    private static void Edit(Object target, Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
