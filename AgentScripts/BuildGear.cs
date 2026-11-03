using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Gear content (`unity command run_script --file AgentScripts/BuildGear.cs --entry BuildGear.Apply`): status effects, the
/// new crafting materials, four armour sets (Cured Leather, Copper Scale, Iron Mail, Moonsilver), jewellery for the empty
/// Amulet and Ring slots, the missing tier II weapons and late weapons that use the materials nothing used yet, shields,
/// Bodkin Arrows and their recipes. Armour weight decides the dodge: Leather stays a quickstep, Iron Mail is a shoulder
/// charge. Safe to run again: it updates what it made and keeps icons already painted.
/// </summary>
public static class BuildGear
{
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Materials = Items + "Materials/";
    private const string Armor = Items + "Armor/";
    private const string Jewelry = Items + "Jewelry/";
    private const string Weapons = Items + "Weapons/";
    private const string WeaponData = "Assets/_Project/Data/Combat/Weapons/";
    private const string Attacks = "Assets/_Project/Data/Combat/Attacks/";
    private const string Status = "Assets/_Project/Data/Combat/Status/";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes/";
    private const string Icons = "Assets/_Project/Art/Items/Gear/Icons/";
    private const string Models = "Assets/_Project/Art/Items/Gear/Models/";
    private const string Tints = "Assets/_Project/Art/Items/Gear/Materials/";

    private static readonly Color Copper = new Color(0.78f, 0.47f, 0.25f);
    private static readonly Color Iron = new Color(0.55f, 0.58f, 0.63f);
    private static readonly Color Moonsilver = new Color(0.72f, 0.82f, 0.95f);
    private static readonly Color Leather = new Color(0.5f, 0.33f, 0.2f);
    private static readonly Color Troll = new Color(0.36f, 0.42f, 0.34f);
    private static readonly Color Wood = new Color(0.55f, 0.38f, 0.22f);

    public static string Apply()
    {
        foreach (string folder in new[] { "Assets/_Project/Data/Combat/Status", "Assets/_Project/Data/Items/Armor", "Assets/_Project/Data/Items/Jewelry",
                     "Assets/_Project/Data/Items/Weapons", "Assets/_Project/Art/Items/Gear", Icons.TrimEnd('/'), Models.TrimEnd('/'), Tints.TrimEnd('/') })
        {
            PrefabBuildUtility.EnsureFolder(folder);
        }

        int effects = StatusEffectsData();
        int materials = NewMaterials();
        int armor = ArmorSets();
        int jewelry = Trinkets();
        int weapons = NewWeapons();
        AssetDatabase.SaveAssets();
        Refresh();
        return $"status {effects}, materials {materials}, armour {armor}, jewellery {jewelry}, weapons and shields {weapons}";
    }

    // ----- Status effects -----

    private static int StatusEffectsData()
    {
        Effect("Poison", "Poison", "Venom in the blood. Health drains until it runs its course or an antidote clears it.", new Color(0.55f, 0.85f, 0.35f), 7f, 3f, DamageType.Holy, 1f);
        Effect("Chill", "Chill", "Grave-cold in the limbs. You move slowly; warm food and frost protection help.", new Color(0.55f, 0.8f, 1f), 5f, 0f, DamageType.Frost, 0.65f);
        Effect("Roots", "Roots", "Roots hold your feet. Hit back: you can still fight.", new Color(0.7f, 0.6f, 0.35f), 1.6f, 0f, DamageType.Physical, 0f);
        return 3;
    }

    private static void Effect(string file, string label, string text, Color color, float seconds, float damage, DamageType type, float speed)
    {
        string path = Status + file + ".asset";
        var effect = LoadOrCreate<StatusEffectDefinition>(path);
        Edit(effect, so =>
        {
            so.FindProperty("id").stringValue = file.ToLowerInvariant();
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("color").colorValue = color;
            so.FindProperty("seconds").floatValue = seconds;
            so.FindProperty("damagePerSecond").floatValue = damage;
            so.FindProperty("damageType").intValue = (int)type;
            so.FindProperty("moveSpeed").floatValue = speed;
        });
    }

    // ----- Materials -----

    private static int NewMaterials()
    {
        Material("CuredLeather", "Cured Leather", "Hide scraped, smoked and worked until it stops being stiff. Light gear starts here.", 2, 6, 0.6f, 30, IconShape.Hide, Leather, Wood);
        Material("BoarTusk", "Boar Tusk", "A yellowed tusk as long as a hand. Rings and hilts.", 2, 6, 0.3f, 20, IconShape.Fang, new Color(0.9f, 0.85f, 0.65f), Wood);
        Material("BogVenom", "Bog Venom", "A sac of the Bog Spitter's bile. Handle with care; brewed thin, it cures what it causes.", 2, 6, 0.2f, 30, IconShape.Potion, new Color(0.5f, 0.8f, 0.3f), Wood);
        Material("WispEmber", "Wisp Ember", "All that's left of a Marsh Light: a cold spark that won't go out.", 3, 12, 0.1f, 20, IconShape.Orb, new Color(0.6f, 0.85f, 1f), Color.white);
        Material("GraveIron", "Grave Iron", "Iron from a barrow, black with age. It holds an edge and a chill.", 3, 12, 1.5f, 30, IconShape.Ingot, new Color(0.3f, 0.33f, 0.4f), Iron);
        Material("RootlingBark", "Rootling Bark", "Tough living bark. It still twitches a little.", 1, 2, 0.3f, 50, IconShape.Plank, new Color(0.42f, 0.3f, 0.18f), Wood);
        return 6;
    }

    private static ItemDefinition Material(string file, string label, string text, int tier, int value, float weight, int stack, IconShape shape, Color primary, Color secondary)
    {
        return Make(Materials + file + ".asset", null, label, text, ItemCategory.Material, tier, ItemRarity.Common, value, weight, IconFor(file, shape, primary, secondary),
            so => so.FindProperty("maximumStack").intValue = stack);
    }

    // ----- Armour -----

    private static int ArmorSets()
    {
        int made = 0;
        void Piece(string file, string label, string text, ArmorSlot slot, int tier, ItemRarity rarity, int value, float weight, Color color, Color trim, CraftingStationType station,
            float seconds, (string item, int amount)[] cost, params StatBonus[] stats)
        {
            ItemDefinition item = Make(Armor + file + ".asset", null, label, text, ItemCategory.Equipment, tier, rarity, value, weight,
                IconFor(file, PlaceholderIconPainter.Guess(label, "", ToolType.None, false, false, slot), color, trim),
                so =>
                {
                    so.FindProperty("armorSlot").intValue = (int)slot;
                    SetStats(so, stats);
                });
            Recipe(file, item, 1, station, seconds, cost);
            made++;
        }

        StatBonus A(float v) => new StatBonus(StatType.Armor, v);
        StatBonus H(float v) => new StatBonus(StatType.MaxHealth, v);
        StatBonus S(float v) => new StatBonus(StatType.Strength, v);

        // Cured Leather: light (stays a quickstep), for moving and shooting.
        CraftingStationType loom = CraftingStationType.Loom;
        Piece("CuredLeatherCap", "Cured Leather Cap", "Soft leather that sits close and makes no sound.", ArmorSlot.Head, 2, ItemRarity.Common, 18, 0.8f, Leather, Wood, loom, 12f,
            new[] { ("CuredLeather", 2), ("NettleCord", 1) }, A(6), H(8));
        Piece("CuredLeatherJack", "Cured Leather Jack", "Layers of boiled leather. Stops a claw, not a club, and lets you run.", ArmorSlot.Chest, 2, ItemRarity.Common, 30, 2.4f, Leather, Wood, loom, 18f,
            new[] { ("CuredLeather", 5), ("Linen", 2), ("NettleCord", 2) }, A(22), H(25), new StatBonus(StatType.MoveSpeed, 0.02f));
        Piece("CuredLeatherGloves", "Archer's Gloves", "Three stiffened fingers. The string comes back faster.", ArmorSlot.Hands, 2, ItemRarity.Common, 16, 0.4f, Leather, Wood, loom, 10f,
            new[] { ("CuredLeather", 2), ("Linen", 1) }, A(4), new StatBonus(StatType.DrawSpeed, 0.1f));
        Piece("CuredLeatherBoots", "Soft-Soled Boots", "Thick enough for roots, thin enough to feel them.", ArmorSlot.Feet, 2, ItemRarity.Common, 18, 0.9f, Leather, Wood, loom, 12f,
            new[] { ("CuredLeather", 3), ("NettleCord", 1) }, A(7), new StatBonus(StatType.MoveSpeed, 0.03f));
        Piece("CuredLeatherBelt", "Forager's Belt", "Loops and pouches for everything. It takes the weight off your back.", ArmorSlot.Waist, 2, ItemRarity.Common, 14, 0.4f, Leather, Wood, loom, 10f,
            new[] { ("CuredLeather", 2), ("Bone", 1) }, A(3), H(12), new StatBonus(StatType.MaxStamina, 10f));

        // Copper Scale: the first metal, made at the Construction Table so it comes before the Forge.
        CraftingStationType table = CraftingStationType.Workbench;
        Color trim = new Color(0.4f, 0.26f, 0.16f);
        Piece("CopperScaleHelm", "Copper Scale Helm", "Scales riveted to a leather cap. It rings when hit, and it gets hit.", ArmorSlot.Head, 2, ItemRarity.Common, 24, 1.6f, Copper, trim, table, 16f,
            new[] { ("CopperIngot", 2), ("RoughHide", 1) }, A(9), H(12));
        Piece("CopperScaleHauberk", "Copper Scale Hauberk", "Hundreds of copper scales. A good night's work for a good smith.", ArmorSlot.Chest, 2, ItemRarity.Common, 44, 4.5f, Copper, trim, table, 26f,
            new[] { ("CopperIngot", 5), ("RoughHide", 3), ("NettleCord", 3) }, A(34), H(30));
        Piece("CopperScaleGauntlets", "Copper Gauntlets", "Heavy on the knuckles. Your swings land harder.", ArmorSlot.Hands, 2, ItemRarity.Common, 20, 1.2f, Copper, trim, table, 14f,
            new[] { ("CopperIngot", 2), ("RoughHide", 1) }, A(7), S(2));
        Piece("CopperScaleGreaves", "Copper Greaves", "Plates on the shin, for things that bite low.", ArmorSlot.Feet, 2, ItemRarity.Common, 22, 1.8f, Copper, trim, table, 14f,
            new[] { ("CopperIngot", 2), ("RoughHide", 2) }, A(8), H(8));
        Piece("CopperScaleGirdle", "Copper Girdle", "A broad belt with a heavy buckle.", ArmorSlot.Waist, 2, ItemRarity.Common, 16, 0.9f, Copper, trim, table, 10f,
            new[] { ("CopperIngot", 1), ("RoughHide", 2) }, A(4), H(10), S(2));

        // Iron Mail: heavy, the wearer's dodge is a shoulder charge. Forge work.
        CraftingStationType forge = CraftingStationType.Anvil;
        trim = new Color(0.25f, 0.27f, 0.3f);
        Piece("IronMailHelm", "Iron Nasal Helm", "A plain iron helm with a bar over the nose. It has seen worse than you.", ArmorSlot.Head, 3, ItemRarity.Uncommon, 45, 2.5f, Iron, trim, forge, 20f,
            new[] { ("IronIngot", 2), ("CuredLeather", 1) }, A(15), H(20));
        Piece("IronMailHauberk", "Iron Mail Hauberk", "Twenty thousand rings. Wolves give up on it. It's a lot to carry.", ArmorSlot.Chest, 3, ItemRarity.Uncommon, 90, 8f, Iron, trim, forge, 34f,
            new[] { ("IronIngot", 6), ("CuredLeather", 3), ("ThickHide", 2) }, A(55), H(50), new StatBonus(StatType.PhysicalResistance, 0.05f));
        Piece("IronMailGauntlets", "Iron Gauntlets", "Hinged plates. Everything you hit, you hit with iron.", ArmorSlot.Hands, 3, ItemRarity.Uncommon, 38, 2f, Iron, trim, forge, 18f,
            new[] { ("IronIngot", 2), ("CuredLeather", 1) }, A(11), S(3));
        Piece("IronMailBoots", "Iron-Shod Boots", "Heavy steps, and everything hears them.", ArmorSlot.Feet, 3, ItemRarity.Uncommon, 40, 3f, Iron, trim, forge, 18f,
            new[] { ("IronIngot", 2), ("ThickHide", 1) }, A(13), H(15));
        Piece("IronMailBelt", "Iron-Studded Belt", "It holds the mail's weight off your shoulders.", ArmorSlot.Waist, 3, ItemRarity.Uncommon, 30, 1.5f, Iron, trim, forge, 14f,
            new[] { ("IronIngot", 1), ("ThickHide", 1) }, A(6), H(15), new StatBonus(StatType.PhysicalResistance, 0.03f));

        // Moonsilver: the end of what the Misty Woods can give. Light for its strength, and good against what isn't flesh.
        trim = new Color(0.4f, 0.45f, 0.62f);
        Piece("MoonsilverCirclet", "Moonsilver Circlet", "It stays cold on the brow. Things in the mist look away.", ArmorSlot.Head, 4, ItemRarity.Rare, 120, 0.8f, Moonsilver, trim, forge, 26f,
            new[] { ("MoonsilverIngot", 2), ("HuldraSilk", 1) }, A(12), H(20), new StatBonus(StatType.SpellPower, 6f));
        Piece("MoonsilverHauberk", "Moonsilver Hauberk", "Rings so fine it pours like water. No one living remembers how it's done, but you worked it out.", ArmorSlot.Chest, 4, ItemRarity.Rare, 240, 3.5f, Moonsilver, trim, forge, 42f,
            new[] { ("MoonsilverIngot", 6), ("HuldraSilk", 3), ("Deepstone", 2) }, A(40), H(50),
            new StatBonus(StatType.FrostResistance, 0.12f), new StatBonus(StatType.HolyResistance, 0.12f));
        Piece("MoonsilverGloves", "Moonsilver Gloves", "Silk lined with silver thread. Runes come easily.", ArmorSlot.Hands, 4, ItemRarity.Rare, 110, 0.5f, Moonsilver, trim, forge, 24f,
            new[] { ("MoonsilverIngot", 2), ("HuldraSilk", 2) }, A(8), new StatBonus(StatType.SpellPower, 6f), new StatBonus(StatType.PhysicalCritChance, 0.04f));
        Piece("MoonsilverBoots", "Moonsilver Boots", "You leave almost no track.", ArmorSlot.Feet, 4, ItemRarity.Rare, 110, 1f, Moonsilver, trim, forge, 24f,
            new[] { ("MoonsilverIngot", 2), ("CuredLeather", 2) }, A(12), H(20), new StatBonus(StatType.MoveSpeed, 0.03f));
        Piece("MoonsilverSash", "Moonsilver Sash", "Huldra silk with a silver clasp. Breath comes easier.", ArmorSlot.Waist, 4, ItemRarity.Rare, 90, 0.4f, Moonsilver, trim, forge, 20f,
            new[] { ("MoonsilverIngot", 1), ("HuldraSilk", 2) }, A(6), H(20), new StatBonus(StatType.MaxStamina, 15f));
        return made;
    }

    // ----- Jewellery -----

    private static int Trinkets()
    {
        int made = 0;
        void Trinket(string file, string label, string text, ArmorSlot slot, int tier, ItemRarity rarity, int value, Color color, Color stone, CraftingStationType station, float seconds,
            (string item, int amount)[] cost, params StatBonus[] stats)
        {
            ItemDefinition item = Make(Jewelry + file + ".asset", null, label, text, ItemCategory.Equipment, tier, rarity, value, 0.1f,
                IconFor(file, slot == ArmorSlot.Neck ? IconShape.Amulet : IconShape.Ring, color, stone),
                so =>
                {
                    so.FindProperty("armorSlot").intValue = (int)slot;
                    SetStats(so, stats);
                });
            Recipe(file, item, 1, station, seconds, cost);
            made++;
        }

        CraftingStationType table = CraftingStationType.Workbench;
        CraftingStationType forge = CraftingStationType.Anvil;
        Color bone = new Color(0.88f, 0.84f, 0.72f);
        Trinket("BoneCharm", "Bone Charm", "Knucklebones on a cord. For luck, and luck is mostly staying alive.", ArmorSlot.Neck, 1, ItemRarity.Common, 8, bone, Wood, CraftingStationType.None, 1f,
            new[] { ("Bone", 2), ("NettleCord", 1) }, new StatBonus(StatType.MaxHealth, 10f));
        Trinket("FangNecklace", "Fang Necklace", "Wolf fangs, one for each that didn't get you.", ArmorSlot.Neck, 2, ItemRarity.Uncommon, 24, bone, new Color(0.6f, 0.2f, 0.15f), table, 12f,
            new[] { ("WolfFang", 3), ("NettleCord", 1) }, new StatBonus(StatType.PhysicalCritChance, 0.06f));
        Trinket("WispPendant", "Wisp Pendant", "A Marsh Light's ember, caged in copper wire. It leans toward the unseen.", ArmorSlot.Neck, 3, ItemRarity.Rare, 70, Copper, new Color(0.6f, 0.85f, 1f), table, 16f,
            new[] { ("WispEmber", 2), ("CopperIngot", 1) }, new StatBonus(StatType.SpellPower, 8f), new StatBonus(StatType.FrostResistance, 0.08f));
        Trinket("HeartrootAmulet", "Heartroot Amulet", "A slice of Guardian heartroot in an iron setting. It beats, very slowly.", ArmorSlot.Neck, 3, ItemRarity.Rare, 90, Iron, new Color(0.5f, 0.3f, 0.15f), forge, 20f,
            new[] { ("GuardianHeartroot", 1), ("IronIngot", 1) }, new StatBonus(StatType.MaxHealth, 30f));
        Trinket("CopperBand", "Copper Band", "A plain ring that turns the finger green.", ArmorSlot.Finger, 2, ItemRarity.Common, 12, Copper, Copper, table, 8f,
            new[] { ("CopperIngot", 1) }, new StatBonus(StatType.Strength, 2f));
        Trinket("TuskRing", "Tusk Ring", "Carved from a boar's tusk. Stubborn, like its first owner.", ArmorSlot.Finger, 2, ItemRarity.Uncommon, 26, new Color(0.9f, 0.85f, 0.65f), Wood, table, 12f,
            new[] { ("BoarTusk", 2), ("CopperIngot", 1) }, new StatBonus(StatType.MaxStamina, 15f));
        Trinket("DeepstoneSignet", "Deepstone Signet", "A black stone set in iron. Heavy for its size.", ArmorSlot.Finger, 3, ItemRarity.Rare, 60, Iron, new Color(0.15f, 0.16f, 0.22f), forge, 18f,
            new[] { ("IronIngot", 1), ("Deepstone", 1) }, new StatBonus(StatType.Armor, 8f), new StatBonus(StatType.Strength, 1f));
        Trinket("MoonsilverRing", "Moonsilver Ring", "Cold, bright and never quite the same size twice.", ArmorSlot.Finger, 4, ItemRarity.Rare, 110, Moonsilver, Color.white, forge, 22f,
            new[] { ("MoonsilverIngot", 1), ("HuldraSilk", 1) }, new StatBonus(StatType.SpellPower, 10f), new StatBonus(StatType.PhysicalCritChance, 0.04f));
        return made;
    }

    // ----- Weapons and shields -----

    private static int NewWeapons()
    {
        var poison = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(Status + "Poison.asset");
        AttackDefinition stabA = FastAttack("Dagger Stab A", "SwordSlash", 0.85f, poison);
        AttackDefinition stabB = FastAttack("Dagger Stab B", "Combos/Sword Slash B", 0.9f, poison);
        AttackDefinition stabC = FastAttack("Dagger Stab C", "Combos/Sword Finisher", 1.3f, poison);
        AttackDefinition hammerHeavy = CopyAttack("Hammer Heavy", "Combos/Axe Heavy", so =>
        {
            so.FindProperty("displayName").stringValue = "Hammer Heavy";
            so.FindProperty("poiseDamage").floatValue = 80f;
            so.FindProperty("weaponDamageRatio").floatValue = 2.2f;
        });

        int made = 0;
        const string sword = "IronSword", axe = "WoodsmansAxe", spear = "IronSpear", twoHand = "BeardedGreataxe", bow = "HuntersBow";
        void Melee(string file, string label, string text, string template, string weaponTemplate, int tier, ItemRarity rarity, int value, float weight, float damage, Color tint,
            CraftingStationType station, float seconds, (string item, int amount)[] cost, Action<SerializedObject> weaponEdit = null, string modelFrom = null, float scale = 0f, params StatBonus[] stats)
        {
            WeaponDefinition weapon = CopyWeapon(file, weaponTemplate, label, damage, weaponEdit);
            ItemDefinition from = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + template + ".asset");
            GameObject source = from.HeldModel;
            if (modelFrom != null)
            {
                source = FindModel(modelFrom) ?? source;
            }

            GameObject model = Tinted(file, source, tint);
            ItemDefinition item = Make(Weapons + file + ".asset", Items + template + ".asset", label, text, ItemCategory.Equipment, tier, rarity, value, weight, RenderIcon(file, model), so =>
            {
                so.FindProperty("weapon").objectReferenceValue = weapon;
                so.FindProperty("heldModel").objectReferenceValue = model;
                if (scale > 0f)
                {
                    so.FindProperty("heldScale").floatValue = scale;
                }

                so.FindProperty("toolType").intValue = 0;
                so.FindProperty("toolTier").intValue = 0;
                SetStats(so, stats);
            });
            Recipe(file, item, 1, station, seconds, cost);
            made++;
        }

        CraftingStationType table = CraftingStationType.Workbench;
        CraftingStationType forge = CraftingStationType.Anvil;
        Melee("CopperSword", "Copper Sword", "Soft metal, sharp edge. Keep it honed and it does the work.", sword, "IronSword", 2, ItemRarity.Common, 26, 3f, 15f, Copper, table, 16f,
            new[] { ("CopperIngot", 3), ("OakPlank", 1), ("NettleCord", 1) });
        Melee("CopperHandAxe", "Copper Hand Axe", "A hand axe that grew up. It bites deeper than the sword and reaches less.", axe, "WoodsmansAxe", 2, ItemRarity.Common, 26, 2.6f, 16f, Copper, table, 16f,
            new[] { ("CopperIngot", 3), ("OakPlank", 2) });
        Melee("CopperSpear", "Copper Spear", "A long oak shaft with a leaf of copper. Keeps wolves at arm's length, and then some.", spear, "Spear", 2, ItemRarity.Common, 24, 2.8f, 14f, Copper, table, 16f,
            new[] { ("CopperIngot", 2), ("OakPlank", 3) });
        Melee("FangDagger", "Venom Fang", "A wolf fang set in bone and steeped in Bog Venom. Three quick cuts, and a third of them leave poison behind.", sword, "IronSword", 2, ItemRarity.Uncommon, 40, 0.9f, 11f,
            new Color(0.85f, 0.82f, 0.68f), table, 18f, new[] { ("WolfFang", 3), ("BogVenom", 2), ("Bone", 1) },
            so =>
            {
                so.FindProperty("basicAttack").objectReferenceValue = stabA;
                SetChain(so, stabA, stabB, stabC);
            }, "Human_Dagger", 0f, new StatBonus(StatType.PhysicalCritChance, 0.05f));
        Melee("IronGreatsword", "Iron Greatsword", "As long as you are tall. Slow to start and hard to stop.", twoHand, "Greataxe", 3, ItemRarity.Uncommon, 70, 5.5f, 30f, Iron, forge, 26f,
            new[] { ("IronIngot", 5), ("HardwoodPlank", 1), ("CuredLeather", 1) }, null, "Human_Greatsword");
        Melee("IronWarhammer", "Iron War Hammer", "It doesn't cut, it arrives. Held down, its heavy blow breaks the poise of almost anything.", twoHand, "Greataxe", 3, ItemRarity.Uncommon, 70, 6f, 26f, Iron, forge, 26f,
            new[] { ("IronIngot", 4), ("HardwoodPlank", 2), ("GraniteBlock", 1) },
            so => so.FindProperty("heavyAttack").objectReferenceValue = hammerHeavy, "Human_Hammer", 1.4f);
        Melee("GraveBlade", "Grave Blade", "Barrow iron, reforged. It's cold in the hand and it never quite warms.", sword, "IronSword", 3, ItemRarity.Rare, 110, 3.2f, 25f, new Color(0.3f, 0.33f, 0.4f), forge, 28f,
            new[] { ("GraveIron", 3), ("IronIngot", 1), ("CuredLeather", 1) }, null, null, 0f, new StatBonus(StatType.FrostResistance, 0.08f));
        Melee("TrollstoneMaul", "Trollstone Maul", "A lump of trollstone on an elderwood haft. A fair weight for a troll.", twoHand, "Greataxe", 4, ItemRarity.Rare, 160, 9f, 40f, Troll, forge, 34f,
            new[] { ("Trollstone", 2), ("Elderwood", 3), ("Deepstone", 2), ("IronIngot", 2) },
            so => so.FindProperty("heavyAttack").objectReferenceValue = hammerHeavy, "Human_Hammer", 1.8f, new StatBonus(StatType.Strength, 4f));
        Melee("MoonsilverBlade", "Moonsilver Blade", "It rings when drawn, faintly, for longer than it should.", sword, "IronSword", 4, ItemRarity.Rare, 190, 2.4f, 30f, Moonsilver, forge, 34f,
            new[] { ("MoonsilverIngot", 3), ("HuldraSilk", 1), ("HardwoodPlank", 1) }, null, null, 0f, new StatBonus(StatType.SpellPower, 8f), new StatBonus(StatType.PhysicalCritChance, 0.05f));

        // A longbow: slow to draw for what it gives, the best reach in the game.
        Melee("ElderwoodLongbow", "Elderwood Longbow", "Taller than a person and steady as a held breath. Arrows from it arrive before the sound.", bow, "HuntersBow", 3, ItemRarity.Rare, 90, 2.2f, 0f, Wood, table, 30f,
            new[] { ("Elderwood", 3), ("Linen", 2), ("CuredLeather", 1) },
            so =>
            {
                so.FindProperty("drawSeconds").floatValue = 1f;
                so.FindProperty("reloadSeconds").floatValue = 0.55f;
                so.FindProperty("arrowSpeed").floatValue = 50f;
                so.FindProperty("spread").floatValue = 0.8f;
            });

        // Shields go in the off hand beside a one-handed weapon; their Guard Let Through decides what a block stops.
        void Shield(string file, string label, string text, int tier, ItemRarity rarity, int value, float weight, float letThrough, Color tint, CraftingStationType station, float seconds,
            (string item, int amount)[] cost, params StatBonus[] stats)
        {
            WeaponDefinition weapon = CopyWeapon(file, "WoodsmansAxe", label, 5f, so =>
            {
                so.FindProperty("kind").intValue = (int)WeaponKind.Shield;
                so.FindProperty("stance").intValue = (int)WeaponStance.OneHanded;
                so.FindProperty("guardLetThrough").floatValue = letThrough;
            });
            GameObject model = Tinted(file, FindModel("Human_Shield"), tint);
            ItemDefinition item = Make(Weapons + file + ".asset", Items + "WoodsmansAxe.asset", label, text, ItemCategory.Equipment, tier, rarity, value, weight, RenderIcon(file, model), so =>
            {
                so.FindProperty("weapon").objectReferenceValue = weapon;
                so.FindProperty("heldModel").objectReferenceValue = model;
                so.FindProperty("prefab").objectReferenceValue = null;
                so.FindProperty("heldPosition").vector3Value = Vector3.zero;
                so.FindProperty("heldRotation").vector3Value = Vector3.zero;
                so.FindProperty("heldScale").floatValue = 1f;
                so.FindProperty("toolType").intValue = 0;
                so.FindProperty("toolTier").intValue = 0;
                SetStats(so, stats);
            });
            Recipe(file, item, 1, station, seconds, cost);
            made++;
        }

        Shield("RoundShield", "Round Shield", "Oak planks and a leather rim. Blocks most of a blow; guard, then attack to bash.", 1, ItemRarity.Common, 18, 2.5f, 0.22f, Wood, table, 14f,
            new[] { ("OakPlank", 4), ("RoughHide", 1) }, new StatBonus(StatType.Armor, 2f));
        Shield("CopperBoundShield", "Copper-Bound Shield", "A round shield with a copper rim and boss. It takes the shine off a troll's stone.", 2, ItemRarity.Common, 34, 3.5f, 0.15f, Copper, table, 18f,
            new[] { ("OakPlank", 3), ("CopperIngot", 2), ("RoughHide", 1) }, new StatBonus(StatType.Armor, 5f));
        Shield("IronKiteShield", "Iron Kite Shield", "Heavy, tall and worth it. Very little gets past.", 3, ItemRarity.Uncommon, 70, 6f, 0.08f, Iron, forge, 24f,
            new[] { ("IronIngot", 4), ("HardwoodPlank", 2), ("ThickHide", 1) }, new StatBonus(StatType.Armor, 10f));

        BodkinArrow();
        return made + 1;
    }

    private static void BodkinArrow()
    {
        const string arrowPath = "Assets/_Project/Data/Combat/Arrows/BodkinArrow.asset";
        if (AssetDatabase.LoadAssetAtPath<ArrowDefinition>(arrowPath) == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Data/Combat/Arrows/CrudeStoneArrow.asset", arrowPath);
        }

        var arrow = AssetDatabase.LoadAssetAtPath<ArrowDefinition>(arrowPath);
        Edit(arrow, so =>
        {
            so.FindProperty("displayName").stringValue = "Bodkin Arrow";
            so.FindProperty("damage").floatValue = 22f;
            so.FindProperty("recoverChance").floatValue = 0.6f;
        });
        ItemDefinition item = Make(Weapons + "BodkinArrow.asset", Items + "CrudeStoneArrow.asset", "Bodkin Arrow", "A narrow metal head that finds the gaps in hide and bark.", ItemCategory.Equipment, 2,
            ItemRarity.Common, 2, 0.06f, null, so => so.FindProperty("arrow").objectReferenceValue = arrow);
        Recipe("BodkinArrow", item, 5, CraftingStationType.Workbench, 8f, new[] { ("CopperIngot", 1), ("Sticks", 3), ("Bone", 1) });
    }

    private static AttackDefinition FastAttack(string file, string template, float ratio, StatusEffectDefinition status)
    {
        return CopyAttack(file, template, so =>
        {
            so.FindProperty("displayName").stringValue = file;
            so.FindProperty("weaponDamageRatio").floatValue = ratio;
            so.FindProperty("range").floatValue = 1.9f;
            so.FindProperty("hitDelay").floatValue *= 0.75f;
            so.FindProperty("recoveryTime").floatValue *= 0.75f;
            so.FindProperty("animationSpeed").floatValue *= 1.35f;
            so.FindProperty("poiseDamage").floatValue *= 0.6f;
            so.FindProperty("appliesStatus").objectReferenceValue = status;
            so.FindProperty("statusChance").floatValue = 0.34f;
        });
    }

    private static AttackDefinition CopyAttack(string file, string template, Action<SerializedObject> change)
    {
        string path = Attacks + "Combos/" + file + ".asset";
        // A fresh copy each run, so scaling by a factor doesn't compound.
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CopyAsset(Attacks + template + ".asset", path);
        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
        Edit(attack, change);
        return attack;
    }

    private static WeaponDefinition CopyWeapon(string file, string template, string label, float damage, Action<SerializedObject> change)
    {
        string path = WeaponData + file + ".asset";
        if (AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(WeaponData + template + ".asset", path);
        }

        var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        Edit(weapon, so =>
        {
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("damage").floatValue = damage;
            change?.Invoke(so);
        });
        return weapon;
    }

    private static void SetChain(SerializedObject so, params AttackDefinition[] chain)
    {
        SerializedProperty combo = so.FindProperty("combo");
        combo.arraySize = chain.Length;
        for (int i = 0; i < chain.Length; i++)
        {
            combo.GetArrayElementAtIndex(i).objectReferenceValue = chain[i];
        }
    }

    // A tinted copy of a prop, so copper, iron and moonsilver look different in the hand and on their icons.
    private static GameObject Tinted(string file, GameObject source, Color tint)
    {
        string path = Models + file + " Model.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        string materialPath = Tints + file + ".mat";
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", tint);
        material.SetFloat("_Metallic", tint.grayscale > 0.45f ? 0.8f : 0.35f);
        material.SetFloat("_Smoothness", 0.45f);
        AssetDatabase.CreateAsset(material, materialPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
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

    private static GameObject FindModel(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name && (path.EndsWith(".fbx") || path.EndsWith(".prefab")))
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        throw new Exception("No model " + name);
    }

    // ----- Shared helpers -----

    private static Sprite IconFor(string file, IconShape shape, Color primary, Color secondary)
    {
        string path = Icons + file + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return existing != null ? existing : PlaceholderIconPainter.Paint(shape, primary, secondary, path);
    }

    private static Sprite RenderIcon(string file, GameObject model)
    {
        string path = Icons + file + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        return existing != null ? existing : IconRenderer.RenderToSprite(model, path);
    }

    private static ItemDefinition Make(string path, string template, string label, string text, ItemCategory category, int tier, ItemRarity rarity, int value, float weight, Sprite icon,
        Action<SerializedObject> extra = null)
    {
        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        if (item == null)
        {
            if (template != null)
            {
                AssetDatabase.CopyAsset(template, path);
                item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            }
            else
            {
                item = ScriptableObject.CreateInstance<ItemDefinition>();
                AssetDatabase.CreateAsset(item, path);
            }
        }

        Edit(item, so =>
        {
            so.FindProperty("id").stringValue = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("category").intValue = (int)category;
            so.FindProperty("tier").intValue = tier;
            so.FindProperty("rarity").intValue = (int)rarity;
            so.FindProperty("value").intValue = value;
            so.FindProperty("weight").floatValue = weight;
            if (icon != null)
            {
                so.FindProperty("icon").objectReferenceValue = icon;
            }

            extra?.Invoke(so);
        });
        return item;
    }

    private static void SetStats(SerializedObject so, StatBonus[] stats)
    {
        SerializedProperty list = so.FindProperty("statBonuses");
        list.arraySize = stats.Length;
        for (int i = 0; i < stats.Length; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("stat").intValue = (int)stats[i].stat;
            entry.FindPropertyRelative("op").intValue = (int)stats[i].op;
            entry.FindPropertyRelative("value").floatValue = stats[i].value;
        }
    }

    private static void Recipe(string file, ItemDefinition output, int amount, CraftingStationType station, float seconds, (string item, int amount)[] cost)
    {
        string path = Recipes + file + ".asset";
        var recipe = LoadOrCreate<RecipeDefinition>(path);
        Edit(recipe, so =>
        {
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
                items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Find(cost[i].item);
                items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
            }
        });
    }

    private static ItemDefinition Find(string file)
    {
        var found = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Materials + file + ".asset") ?? AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + file + ".asset");
        return found != null ? found : throw new Exception("No item " + file);
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }

        return asset;
    }

    private static void Edit(Object asset, Action<SerializedObject> change)
    {
        var so = new SerializedObject(asset);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
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
