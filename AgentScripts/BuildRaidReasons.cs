using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// What raids give that the Haven can't (`unity command run_script --file AgentScripts/BuildRaidReasons.cs --entry BuildRaidReasons.Apply`):
/// relics, the lost expedition's jobs, plans and living things found only out there, the rumours that promise them and
/// the blow against the next breach. Sites are placeholder shapes with a gold beacon. Run BuildRaidEvents and BuildRaidLayout
/// first. Safe to run again: everything is rewritten, except site prefabs and models, which are kept once made.
/// </summary>
public static class BuildRaidReasons
{
    private const string Relics = "Assets/_Project/Data/Essence/Relics/";
    private const string Rumours = "Assets/_Project/Data/Raids/Rumours/";
    private const string Events = "Assets/_Project/Data/Raids/Rumours/Events/";
    private const string Sites = "Assets/_Project/Prefabs/Raids/Events/Sites/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes/";
    private const string Buildings = "Assets/_Project/Data/Buildings/";
    private const string Settlement = "Assets/_Project/Data/Settlement/";
    private const string People = "Assets/_Project/Data/Prologue/Residents/";
    private const string Raid = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string Wood = "Assets/_Project/Art/Forest/Materials/Dead Wood.mat";
    private const string Stone = "Assets/_Project/Data/Prologue/Shelter Stone.mat";
    private const string Traveler = "Assets/_Project/Prefabs/Characters/StrandedTraveler.prefab";

    private delegate void Outcome(SerializedProperty list);

    private sealed class Choice
    {
        public string Label, Detail, Result;
        public Outcome[] Outcomes = new Outcome[0];
    }

    public static string Apply()
    {
        PrefabBuildUtility.EnsureFolder(Relics.TrimEnd('/'));
        PrefabBuildUtility.EnsureFolder(Rumours.TrimEnd('/'));
        PrefabBuildUtility.EnsureFolder(Events.TrimEnd('/'));
        string relics = BuildRelics();
        string people = BuildPeople();
        string finds = BuildFinds();
        AssetDatabase.SaveAssets();
        string rumours = BuildRumours();
        QuietHomeNights();
        MakeRoomInThePlanner();
        AssetDatabase.SaveAssets();
        return $"{relics}; {people}; {finds}; {rumours}";
    }

    // ----- Relics -----

    private static string BuildRelics()
    {
        var chill = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Chill.asset");
        var powers = new List<EnchantmentDefinition>
        {
            Power("WolfFed", "Wolf-Fed", "It takes something from every death, and shares it with you.", EnchantTarget.Weapon, new FedByKillsEffect(), null, new StatBonus(StatType.MaxHealth, -10f)),
            Power("Hushed", "Hushed", "Sound dies around it. So does a little of your breath.", EnchantTarget.Armor | EnchantTarget.Jewelry, new HushedEffect(), null, new StatBonus(StatType.MaxStamina, -10f)),
            Power("TwiceBuried", "Twice-Buried", "Someone has already died in this, and it didn't take.", EnchantTarget.Armor | EnchantTarget.Jewelry, new DeathlessEffect(), null, new StatBonus(StatType.MaxHealth, -5f)),
            Power("Moonbound", "Moonbound", "It sleeps by day.", EnchantTarget.Weapon | EnchantTarget.Jewelry, new MoonboundEffect(), null),
            Power("Harefoot", "Harefoot", "Whoever wore this last never once stood and fought.", EnchantTarget.Armor, new AfterDodgeSpeedEffect(), null),
            Power("FarSeeing", "Far-Seeing", "The mist seems to step aside for it.", EnchantTarget.Armor | EnchantTarget.Jewelry, new FarSeeingEffect(), null),
            Power("Trollblooded", "Trollblooded", "It knits flesh the way moss covers stone: slowly, and without being asked.", EnchantTarget.Armor, new MendingEffect(), null, new StatBonus(StatType.MoveSpeed, -0.06f)),
            Power("RimeTouched", "Rime-Touched", "Cold to hold, and colder to be struck by.", EnchantTarget.Weapon, new StatusOnHitEffect(), effect => effect.FindPropertyRelative("status").objectReferenceValue = chill),
        };

        var gear = new List<ItemDefinition>();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Project/Data/Items" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            bool fishing = item != null && item.IsWeapon && item.name.Contains("Fishing");
            bool torch = item != null && item.name.Contains("Torch");
            if (item != null && (item.IsArmor || item.IsWeapon) && !fishing && !torch)
            {
                gear.Add(item);
            }
        }

        const string settingsPath = "Assets/_Project/Resources/RelicSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<RelicSettings>(settingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RelicSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);
        }

        settings.Configure(powers, gear);
        EditorUtility.SetDirty(settings);

        // The great troll at the end of the map always has something worth the fight.
        var troll = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "OldMossTroll.prefab");
        if (troll != null && troll.TryGetComponent(out LootOnDeath loot))
        {
            var data = new SerializedObject(loot);
            data.FindProperty("relicChance").floatValue = 1f;
            data.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SavePrefabAsset(troll);
        }

        return $"{powers.Count} relic powers over {gear.Count} pieces of gear";
    }

    private static EnchantmentDefinition Power(string file, string label, string text, EnchantTarget targets, RelicEffect effect, Action<SerializedProperty> fill, params StatBonus[] bonuses)
    {
        string path = Relics + file + ".asset";
        var power = AssetDatabase.LoadAssetAtPath<EnchantmentDefinition>(path);
        if (power == null)
        {
            power = ScriptableObject.CreateInstance<EnchantmentDefinition>();
            AssetDatabase.CreateAsset(power, path);
        }

        var so = new SerializedObject(power);
        so.FindProperty("id").stringValue = "relic." + file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        so.FindProperty("school").intValue = (int)EssenceSchool.Relic;
        so.FindProperty("targets").intValue = (int)targets;
        SerializedProperty stats = so.FindProperty("bonuses");
        stats.arraySize = bonuses.Length;
        for (int i = 0; i < bonuses.Length; i++)
        {
            stats.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue = (int)bonuses[i].stat;
            stats.GetArrayElementAtIndex(i).FindPropertyRelative("op").intValue = (int)bonuses[i].op;
            stats.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = bonuses[i].value;
        }

        SerializedProperty effects = so.FindProperty("effects");
        effects.ClearArray();
        effects.InsertArrayElementAtIndex(0);
        SerializedProperty entry = effects.GetArrayElementAtIndex(0);
        entry.managedReferenceValue = effect;
        fill?.Invoke(entry);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(power);
        return power;
    }

    // ----- The lost expedition (GDD §21.0): who they are at home -----

    private static string BuildPeople()
    {
        var runes = new[] { "RuneOfPlenty", "WayfinderRune", "BloodMoonRune", "NornsHourglass" };
        foreach (string rune in runes)
        {
            Unknown(rune);
        }

        Person("Tor", Job("Carpenter", "Carpenter", "Sees to what the forest breaks: mending buildings after a breach costs half, and work at the sawbench goes faster.",
                "\"I'll have that lot square by morning.\"", Find<BuildingDefinition>(Buildings, "Sawbench"), buildingRepair: 0.5f),
            "Still measuring. I knew someone would come, and I wanted something to show for it.", "It's built crooked, you know. All of it. Give me a week.", "My tools. Leave them, if you have to. Not me.");
        Person("Eirik", Job("Warden", "Warden", "Stands the watch: in a breach the ward holds half as long again.",
                "\"Nothing comes through while I can stand.\"", Find<BuildingDefinition>(Buildings, "WatchPost"), wardHealth: 0.5f),
            "Took you long enough. How many are behind you?", "Show me the wall. Then show me where it's weakest.", "Go. I've held worse.");
        Person("Runa", Job("Tracker", "Tracker", "Goes out at dawn and reads the ground: one more rumour in every raid's briefing.",
                "\"I'll tell you what's out there before you go.\"", null, rumours: 1),
            "I've watched you for two days. You leave a trail like a cart.", "This place can be found. Good. So can everything else, by the same road.", "I know the way. Go on. I'll catch you up.");
        Person("Liv", Job("RuneScholar", "Rune Scholar", "Knows the old marks: she teaches the raid runes, and work at the cauldron goes faster.",
                "\"These aren't spells. They're promises, and the forest keeps them.\"", Find<BuildingDefinition>(Buildings, "Cauldron"), teaches: runes),
            "The stones broke along a line. A line! Do you understand what that means?", "Your ward is a promise too. Did anyone tell you?", "Write it down. Somebody has to write it down.");
        Person("Sigrid", Job("Leader", "Leader", "Keeps the portal's watch: 30 more seconds in every raid, and everyone knows where they stand.",
                "\"We go out together, and we come back together.\"", Find<BuildingDefinition>(Buildings, "HavenBanner"), raidSeconds: 30f),
            "You came back. I told them you would.", "Seven went in. Tell me who's home.", "Don't you stop for me. That's an order.");
        Person("Bjarne", Job("Smith", "Smith", "Works the forge: repairs to gear cost half, and the forge goes faster.",
                "\"Bring me iron. I don't care how.\"", Find<BuildingDefinition>(Buildings, "Forge"), gearRepair: 0.5f),
            "You took your time. Mind the hammer.", "Where's the anvil? No, don't tell me. I'll find it.", "Tell Sigrid I kept it lit.");
        return "6 jobs for the expedition";
    }

    private static JobDefinition Job(string file, string label, string text, string accept, BuildingDefinition workplace, int rumours = 0,
        float gearRepair = 0f, float buildingRepair = 0f, float wardHealth = 0f, float raidSeconds = 0f, string[] teaches = null)
    {
        string path = Settlement + file + ".asset";
        var job = AssetDatabase.LoadAssetAtPath<JobDefinition>(path);
        if (job == null)
        {
            job = ScriptableObject.CreateInstance<JobDefinition>();
            AssetDatabase.CreateAsset(job, path);
        }

        var so = new SerializedObject(job);
        so.FindProperty("id").stringValue = "job." + file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        so.FindProperty("acceptLine").stringValue = accept;
        so.FindProperty("workplace").objectReferenceValue = workplace;
        so.FindProperty("extraRumours").intValue = rumours;
        so.FindProperty("gearRepairDiscount").floatValue = gearRepair;
        so.FindProperty("buildingRepairDiscount").floatValue = buildingRepair;
        so.FindProperty("wardHealth").floatValue = wardHealth;
        so.FindProperty("raidSeconds").floatValue = raidSeconds;
        SerializedProperty recipes = so.FindProperty("teachesRecipes");
        recipes.arraySize = teaches?.Length ?? 0;
        for (int i = 0; teaches != null && i < teaches.Length; i++)
        {
            recipes.GetArrayElementAtIndex(i).objectReferenceValue = Find<RecipeDefinition>(Recipes, teaches[i]);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(job);
        return job;
    }

    private static void Person(string file, JobDefinition job, string rescue, string arrival, string fallen)
    {
        var resident = Find<ResidentDefinition>(People, file);
        var so = new SerializedObject(resident);
        so.FindProperty("rescueLine").stringValue = rescue;
        so.FindProperty("arrivalLine").stringValue = arrival;
        so.FindProperty("fallenLine").stringValue = fallen;
        SerializedProperty jobs = so.FindProperty("jobs");
        jobs.arraySize = 1;
        jobs.GetArrayElementAtIndex(0).objectReferenceValue = job;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(resident);
    }

    // ----- What only raids hold: plans and living things -----

    private static string BuildFinds()
    {
        Unknown("MoonsilverIngot");
        var moonsilver = new[] { "MoonsilverIngot", "MoonsilverBlade", "MoonsilverBoots", "MoonsilverCirclet", "MoonsilverGloves", "MoonsilverHauberk", "MoonsilverRing", "MoonsilverSash" };
        foreach (string recipe in moonsilver)
        {
            Unknown(recipe);
        }

        Crop("FarmCloudberries", "Cloudberries", Find<ItemDefinition>(Items + "Materials/", "Cloudberries"), 3, 300f);
        Crop("FarmChanterelles", "Chanterelles", Find<ItemDefinition>(Items + "Materials/", "Chanterelle"), 3, 300f);
        Crop("FarmElderwood", "Elderwood", Find<ItemDefinition>(Items + "Materials/", "Elderwood"), 1, 900f);
        BuildingDefinition elder = ElderTree();
        Gate("WainwrightsYard");
        Gate("Sauna");
        Gate("Beehive");

        NewFind("WainwrightsLedger", "Wainwright's Ledger", "Measurements for barrows and handcarts, and a boat, in a careful hand. Read it at home.", ItemRarity.Rare, 40, 0.5f, null,
            new[] { Find<BuildingDefinition>(Buildings, "WainwrightsYard") }, new RecipeDefinition[0]);
        NewFind("BathhousePlans", "Bathhouse Plans", "Someone drew a sauna on birch bark, and meant it. Read it at home.", ItemRarity.Rare, 40, 0.5f, null,
            new[] { Find<BuildingDefinition>(Buildings, "Sauna") }, new RecipeDefinition[0]);
        var folio = new List<RecipeDefinition>();
        foreach (string recipe in moonsilver)
        {
            folio.Add(Find<RecipeDefinition>(Recipes, recipe));
        }

        NewFind("MoonsilverFolio", "Moonsilver Folio", "How to work the metal that won't take a hammer by daylight. Read it at home.", ItemRarity.Epic, 120, 1f, null,
            new BuildingDefinition[0], folio.ToArray());
        NewFind("SwarmInASkep", "Swarm in a Skep", "A live swarm in a straw basket, furious. It will settle if it reaches a garden. Use it at home.", ItemRarity.Rare, 40, 3f, "Honey",
            new[] { Find<BuildingDefinition>(Buildings, "Beehive") }, new RecipeDefinition[0]);
        NewFind("PouchOfWildSeed", "Pouch of Wild Seed", "Cloudberry and chanterelle spawn from the deep woods, kept damp. Use it at home.", ItemRarity.Rare, 30, 0.5f, "Cloudberries",
            new BuildingDefinition[0], new[] { Find<RecipeDefinition>(Recipes, "FarmCloudberries"), Find<RecipeDefinition>(Recipes, "FarmChanterelles") });
        NewFind("ElderSapling", "Elder Sapling", "A hand's height of something very old, roots and all. It wants a home. Use it at home.", ItemRarity.Epic, 80, 6f, "Elderwood",
            new[] { elder }, new[] { Find<RecipeDefinition>(Recipes, "FarmElderwood") });
        return "6 finds";
    }

    private static void Unknown(string recipe)
    {
        var so = new SerializedObject(Find<RecipeDefinition>(Recipes, recipe));
        so.FindProperty("knownFromStart").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Gate(string building)
    {
        var so = new SerializedObject(Find<BuildingDefinition>(Buildings, building));
        so.FindProperty("knownFromStart").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Crop(string file, string label, ItemDefinition output, int amount, float seconds)
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
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("output").objectReferenceValue = output;
        so.FindProperty("outputAmount").intValue = amount;
        so.FindProperty("station").intValue = (int)CraftingStationType.Farm;
        so.FindProperty("craftSeconds").floatValue = seconds;
        so.FindProperty("knownFromStart").boolValue = false;
        so.FindProperty("sortOrder").intValue = 100 + output.Tier * 10;
        SerializedProperty items = so.FindProperty("itemCost");
        items.arraySize = 1;
        items.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = output;
        items.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(recipe);
    }

    private static ItemDefinition NewFind(string file, string label, string text, ItemRarity rarity, int value, float weight, string iconFrom, BuildingDefinition[] buildings, RecipeDefinition[] recipes)
    {
        string path = Items + file + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(Items + "ForgeBlueprint.asset", path);
        }

        var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        var so = new SerializedObject(item);
        so.FindProperty("id").stringValue = "find." + file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = text;
        so.FindProperty("rarity").intValue = (int)rarity;
        so.FindProperty("value").intValue = value;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("learnedAtHome").boolValue = true;
        if (iconFrom != null)
        {
            so.FindProperty("icon").objectReferenceValue = Find<ItemDefinition>(Items + "Materials/", iconFrom).Icon;
        }

        SerializedProperty taught = so.FindProperty("teachesBuildings");
        taught.arraySize = buildings.Length;
        for (int i = 0; i < buildings.Length; i++)
        {
            taught.GetArrayElementAtIndex(i).objectReferenceValue = buildings[i];
        }

        SerializedProperty known = so.FindProperty("teachesRecipes");
        known.arraySize = recipes.Length;
        for (int i = 0; i < recipes.Length; i++)
        {
            known.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        return item;
    }

    // Grown from the sapling carried home: a tree that stands in the Haven for good.
    private static BuildingDefinition ElderTree()
    {
        string path = Buildings + "ElderTree.asset";
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(path);
        if (building == null)
        {
            building = ScriptableObject.CreateInstance<BuildingDefinition>();
            AssetDatabase.CreateAsset(building, path);
        }

        var so = new SerializedObject(building);
        so.FindProperty("id").stringValue = "eldertree";
        so.FindProperty("displayName").stringValue = "Elder Tree";
        so.FindProperty("description").stringValue = "Grown from a sapling brought through the portal. Nothing else here is this old.";
        SerializedProperty items = so.FindProperty("constructionItems");
        items.arraySize = 1;
        items.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = Find<ItemDefinition>(Items + "Materials/", "Hardwood");
        items.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 4;
        so.FindProperty("knownFromStart").boolValue = false;
        so.FindProperty("showInBuildMenu").boolValue = true;
        so.FindProperty("sortOrder").intValue = 910;
        so.ApplyModifiedPropertiesWithoutUndo();
        building.ConfigureBuildingUp(4f, 12, new BuildingLevel[0]);
        if (building.Prefab == null)
        {
            GameObject shape = ElderModel();
            try
            {
                GameObject model = PrefabUtility.SaveAsPrefabAsset(shape, "Assets/_Project/Art/Haven/Models/ElderTree Model.prefab");
                GameObject prefab = BuildingPrefabBuilder.Create(building, model);
                so.Update();
                so.FindProperty("prefab").objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            finally
            {
                Object.DestroyImmediate(shape);
            }
        }

        // BuildHaven gives every building its Construction; this one came later.
        string prefabPath = AssetDatabase.GetAssetPath(building.Prefab);
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        if (contents.GetComponent<Construction>() == null)
        {
            contents.AddComponent<Construction>();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
        }

        PrefabUtility.UnloadPrefabContents(contents);
        EditorUtility.SetDirty(building);
        return building;
    }

    private static GameObject ElderModel()
    {
        var root = new GameObject("Elder Tree Model");
        Material bark = Mat(Wood);
        Material leaves = Tint("Elder Leaves", new Color(0.26f, 0.42f, 0.2f));
        Piece(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.3f, 0f), new Vector3(0.5f, 1.3f, 0.5f), bark);
        Piece(root.transform, PrimitiveType.Sphere, new Vector3(0f, 3.4f, 0f), new Vector3(3.4f, 2.8f, 3.4f), leaves);
        Piece(root.transform, PrimitiveType.Sphere, new Vector3(0.9f, 2.7f, 0.4f), new Vector3(2f, 1.7f, 2f), leaves);
        Piece(root.transform, PrimitiveType.Sphere, new Vector3(-0.8f, 2.8f, -0.5f), new Vector3(2.2f, 1.8f, 2.2f), leaves);
        return root;
    }

    // ----- Rumours -----

    private static string BuildRumours()
    {
        var pool = new List<RaidRumourDefinition>();
        var traveler = AssetDatabase.LoadAssetAtPath<GameObject>(Traveler).GetComponent<RescuableTraveler>();

        Outcome Rescue(string who) => list => Add(list, new RescueResidentOutcome(), e =>
        {
            e.FindPropertyRelative("resident").objectReferenceValue = Find<ResidentDefinition>(People, who);
            e.FindPropertyRelative("traveler").objectReferenceValue = traveler;
        });

        // People: the expedition, each found in a way that suits them.
        pool.Add(Rumour("Tor", "A saw was heard in the deep woods, where nobody saws.", Event("TorsShelter", "A Shelter, Built Too Well", Site("Tors Shelter", Shelter),
                "A lean-to of split logs, every joint square. A man sits under it with a broken leg, still measuring.",
                new Choice { Label = "Cut him out", Detail = "45 seconds, and the sawing will carry.", Result = "He hands you his rule before he lets you lift him.", Outcomes = new[] { Time(-45f), Noise(25f), Rescue("Tor") } }),
            2f, seeks: "Tor", after: 1));
        pool.Add(Rumour("Eirik", "Someone has been holding a clearing against wolves for days. Nobody knows how.", Event("EiriksStand", "The Last Stand", Site("Eiriks Stand", Stand),
                "A ring of dead wolves, and a man in the middle of it who has not sat down in three days.",
                new Choice { Label = "Stand with him", Detail = "They are coming back, and you will have to fight them.", Result = "\"About time,\" he says, and turns his back to yours.", Outcomes = new[] { Spawn(new[] { "WolfPack" }, 1, 14f, true), Rescue("Eirik") } }),
            2f, seeks: "Eirik", after: 1));
        pool.Add(Rumour("Runa", "Marks have been cut on the trees, but they can only be seen at night.", Event("RunasMarks", "Marks on the Trees", Site("Runas Marks", Marks),
                "The marks lead here and stop. Someone drops out of the branches beside you without a sound.",
                new Choice { Label = "Follow her out", Detail = "She knows these woods. She shows you some of them.", Result = "She draws you a map in the dirt with a stick.", Outcomes = new[] { Reveal(70f, true), Rescue("Runa") } }),
            2f, hour: RumourHour.Dark, seeks: "Runa", after: 1));
        pool.Add(Rumour("Liv", "A ring of old stones is said to be humming. There is someone inside it.", Event("LivsCircle", "The Humming Stones", Site("Livs Circle", Circle),
                "Liv sits in a ring of runestones, writing. She can't step out, and doesn't seem to have noticed.",
                new Choice { Label = "Break the ring", Detail = "Loud, and whatever is listening will hear it.", Result = "The hum stops. Every bird in the forest goes up at once.", Outcomes = new[] { Noise(45f), Rescue("Liv") } },
                new Choice { Label = "Lift the stones aside, one by one", Detail = "Slow: a minute and a half. Quiet.", Result = "She doesn't look up until the last one is moved.", Outcomes = new[] { Time(-90f), Rescue("Liv") } }),
            2f, seeks: "Liv", after: 1));
        pool.Add(Rumour("Sigrid", "A horn has been heard at dusk, calling the expedition's signal.", Event("SigridsHorn", "The Horn at Dusk", Site("Sigrids Horn", Stand),
                "Sigrid leans on a broken spear, a horn in her other hand, hurt worse than she'll say.",
                new Choice { Label = "Take her weight", Detail = "It will cost you: you take a wound getting her up.", Result = "\"Who's home?\" is the first thing she says.", Outcomes = new[] { Health(-0.25f), Rescue("Sigrid") } }),
            2f, seeks: "Sigrid", after: 3));

        // Relics.
        pool.Add(Rumour("BarrowHoard", "There's a barrow open that wasn't open yesterday, and something shining in it.", Event("BarrowHoard", "The Open Hoard", Site("Barrow Hoard", Hoard),
                "Old grave goods under fallen stone. One of them looks back at you.",
                new Choice { Label = "Take it", Detail = "Its keepers will wake.", Result = "Something under the stone begins to move.", Outcomes = new[] { Relic(), Spawn(new[] { "RootGuardian" }, 1, 10f, true) } },
                new Choice { Label = "Leave it", Detail = "Walk away with what you came for.", Result = "Some things are better left." }),
            1.5f));
        pool.Add(Rumour("NightCairn", "A cairn stands in the woods after dark. By day there is nothing there.", Event("NightCairn", "The Night Cairn", Site("Night Cairn", Cairn),
                "Stones piled higher than a man, cold enough to hurt. There is something at the top, and it wants carrying.",
                new Choice { Label = "Climb and take it", Detail = "It takes a little of your strength for a while.", Result = "It is much heavier than it looks.", Outcomes = new[] { Relic(), Boon("Cairn-Cold", 180f, new StatBonus(StatType.MaxStamina, -20f)) } }),
            1.5f, hour: RumourHour.Dark));

        // Lost knowledge.
        pool.Add(Rumour("Ledger", "A wainwright's camp was abandoned somewhere out there. His books may still be dry.", Event("WainwrightsCamp", "The Wainwright's Camp", Site("Wainwrights Camp", Camp),
                "A half-built wheel, a cold fire and a chest nobody came back for.",
                new Choice { Label = "Take the ledger", Result = "Barrows, carts, a boat. All of it measured.", Outcomes = new[] { Loot(("WainwrightsLedger", 1, 1, 1f)) } }),
            1f, teaches: "WainwrightsLedger"));
        pool.Add(Rumour("Bathhouse", "Someone once built a bathhouse in the woods. It's said the plans were cut into the door.", Event("OldBathhouse", "The Old Bathhouse", Site("Old Bathhouse", Camp),
                "Four walls and half a roof, and a door with a drawing cut into it.",
                new Choice { Label = "Copy the plans", Detail = "Half a minute with a bit of birch bark.", Result = "It's better made than anything at home.", Outcomes = new[] { Time(-30f), Loot(("BathhousePlans", 1, 1, 1f)) } }),
            1f, teaches: "BathhousePlans"));
        pool.Add(Rumour("Folio", "A dead silversmith's workshop lies out there, and something still guards his notes.", Event("SilversmithsRest", "The Silversmith's Rest", Site("Silversmiths Rest", Hoard),
                "A bench, a bare skeleton in a good coat and a folio wrapped in oilcloth.",
                new Choice { Label = "Take the folio", Detail = "What guards it will know.", Result = "The woods go quiet behind you.", Outcomes = new[] { Loot(("MoonsilverFolio", 1, 1, 1f)), Spawn(new[] { "RootGuardian" }, 1, 12f, true) } }),
            0.8f, teaches: "MoonsilverFolio", after: 4));

        // Living things.
        pool.Add(Rumour("Bees", "Wild bees are swarming in the woods this week. A skep could take them.", Event("WildSwarm", "The Wild Swarm", Site("Wild Swarm", Swarm),
                "A swarm the size of a man hangs from a dead limb, and the whole clearing hums.",
                new Choice { Label = "Take the swarm", Detail = "You will be stung, and it's heavy.", Result = "It settles. Mostly.", Outcomes = new[] { Health(-0.15f), Loot(("SwarmInASkep", 1, 1, 1f)) } }),
            1.2f, teaches: "SwarmInASkep"));
        pool.Add(Rumour("Seed", "Cloudberries are fruiting in the deep woods, and the mushrooms with them.", Event("DeepPatch", "The Deep Patch", Site("Deep Patch", Patch),
                "Cloudberry and chanterelle grow thick here, as they never would at home.",
                new Choice { Label = "Gather seed and spawn", Detail = "Half a minute of careful work.", Result = "Enough to start a bed at home.", Outcomes = new[] { Time(-30f), Loot(("PouchOfWildSeed", 1, 1, 1f)) } }),
            1.2f, teaches: "PouchOfWildSeed"));
        pool.Add(Rumour("Sapling", "An elder is standing somewhere out there, and it has seeded.", Event("TheElder", "The Elder", Site("The Elder", Patch),
                "The oldest tree you have ever seen, and at its foot a sapling no taller than your hand.",
                new Choice { Label = "Dig it up, roots and all", Detail = "A minute. The Elder will not thank you.", Result = "The forest draws a long breath.", Outcomes = new[] { Time(-60f), Noise(30f), Loot(("ElderSapling", 1, 1, 1f)) } }),
            1f, teaches: "ElderSapling", after: 2));

        // The source of the next breach.
        pool.Add(Rumour("BreachSource", "Something is gathering in the woods against the Haven. Strike it first and it will come weaker.", Event("BreachSource", "The Gathering Place", Site("Gathering Place", Totem),
                "Bones and antlers tied to a dead tree, and tracks all around it, all pointing towards home.",
                new Choice { Label = "Tear it down", Detail = "What gathered here will come for you instead. The next breach comes weaker.", Result = "It comes apart in your hands. Far away, something howls.", Outcomes = new[] { Weaken(0.3f), Spawn(new[] { "WolfPack" }, 1, 12f, true) } }),
            3f, breach: true));

        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(Raid);
        RaidRumoursSystem system = raid.GetSystem<RaidRumoursSystem>();
        if (system == null)
        {
            system = new RaidRumoursSystem();
            raid.EditorSystems.Add(system);
        }

        system.Configure(pool, 2);
        EditorUtility.SetDirty(raid);
        return $"{pool.Count} rumours";
    }

    private static RaidRumourDefinition Rumour(string file, string line, RaidEventDefinition site, float weight, RumourHour hour = RumourHour.Any,
        string seeks = null, string teaches = null, bool breach = false, int after = 0)
    {
        string path = Rumours + file + ".asset";
        var rumour = AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>(path);
        if (rumour == null)
        {
            rumour = ScriptableObject.CreateInstance<RaidRumourDefinition>();
            AssetDatabase.CreateAsset(rumour, path);
        }

        var so = new SerializedObject(rumour);
        so.FindProperty("id").stringValue = "rumour." + file.ToLowerInvariant();
        so.FindProperty("briefing").stringValue = line;
        so.FindProperty("site").objectReferenceValue = site;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("hour").intValue = (int)hour;
        so.FindProperty("seeks").objectReferenceValue = seeks != null ? Find<ResidentDefinition>(People, seeks) : null;
        so.FindProperty("teaches").objectReferenceValue = teaches != null ? Find<ItemDefinition>(Items, teaches) : null;
        so.FindProperty("breachComing").boolValue = breach;
        so.FindProperty("afterExtractions").intValue = after;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rumour);
        return rumour;
    }

    private static RaidEventDefinition Event(string file, string label, GameObject site, string siteText, params Choice[] choices)
    {
        string path = Events + file + ".asset";
        var definition = AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<RaidEventDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        var so = new SerializedObject(definition);
        so.FindProperty("id").stringValue = "rumour." + file.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("weight").floatValue = 0f; // Never drawn at random: a rumour places it.
        so.FindProperty("sitePrefab").objectReferenceValue = site;
        so.FindProperty("siteText").stringValue = siteText;
        SerializedProperty list = so.FindProperty("choices");
        list.ClearArray();
        foreach (Choice choice in choices)
        {
            list.InsertArrayElementAtIndex(list.arraySize);
            SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            entry.FindPropertyRelative("label").stringValue = choice.Label;
            entry.FindPropertyRelative("detail").stringValue = choice.Detail ?? "";
            entry.FindPropertyRelative("result").stringValue = choice.Result ?? "";
            entry.FindPropertyRelative("cost").ClearArray();
            SerializedProperty outcomes = entry.FindPropertyRelative("outcomes");
            outcomes.ClearArray();
            foreach (Outcome outcome in choice.Outcomes)
            {
                outcome(outcomes);
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static void Add(SerializedProperty list, RaidEventOutcome outcome, Action<SerializedProperty> fill = null)
    {
        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.managedReferenceValue = outcome;
        fill?.Invoke(entry);
    }

    private static Outcome Relic() => list => Add(list, new GiveRelicOutcome());
    private static Outcome Weaken(float share) => list => Add(list, new WeakenBreachOutcome(), e => e.FindPropertyRelative("share").floatValue = share);
    private static Outcome Time(float seconds) => list => Add(list, new ChangeTimeOutcome(), e => e.FindPropertyRelative("seconds").floatValue = seconds);
    private static Outcome Health(float share) => list => Add(list, new ChangeHealthOutcome(), e => e.FindPropertyRelative("share").floatValue = share);
    private static Outcome Noise(float radius) => list => Add(list, new MakeNoiseOutcome(), e => e.FindPropertyRelative("radius").floatValue = radius);

    private static Outcome Reveal(float radius, bool exits) => list => Add(list, new RevealMapOutcome(), e =>
    {
        e.FindPropertyRelative("radius").floatValue = radius;
        e.FindPropertyRelative("showExits").boolValue = exits;
    });

    private static Outcome Boon(string name, float seconds, params StatBonus[] bonuses) => list => Add(list, new GiveBoonOutcome(), e =>
    {
        e.FindPropertyRelative("name").stringValue = name;
        e.FindPropertyRelative("seconds").floatValue = seconds;
        SerializedProperty stats = e.FindPropertyRelative("bonuses");
        stats.arraySize = bonuses.Length;
        for (int i = 0; i < bonuses.Length; i++)
        {
            stats.GetArrayElementAtIndex(i).FindPropertyRelative("stat").intValue = (int)bonuses[i].stat;
            stats.GetArrayElementAtIndex(i).FindPropertyRelative("op").intValue = (int)bonuses[i].op;
            stats.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue = bonuses[i].value;
        }
    });

    private static Outcome Spawn(string[] prefabs, int count, float radius, bool hunt) => list => Add(list, new SpawnCreaturesOutcome(), e =>
    {
        SerializedProperty rows = e.FindPropertyRelative("prefabs");
        rows.arraySize = prefabs.Length;
        for (int i = 0; i < prefabs.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + prefabs[i] + ".prefab");
            rows.GetArrayElementAtIndex(i).objectReferenceValue = prefab != null ? prefab : throw new Exception("No enemy " + prefabs[i]);
        }

        e.FindPropertyRelative("count").intValue = count;
        e.FindPropertyRelative("radius").floatValue = radius;
        e.FindPropertyRelative("hunt").boolValue = hunt;
    });

    private static Outcome Loot(params (string what, int min, int max, float chance)[] rows) => list => Add(list, new GiveLootOutcome(), e =>
    {
        SerializedProperty entries = e.FindPropertyRelative("loot").FindPropertyRelative("entries");
        entries.arraySize = rows.Length;
        for (int i = 0; i < rows.Length; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("item").objectReferenceValue = Find<ItemDefinition>(Items, rows[i].what);
            entry.FindPropertyRelative("resource").objectReferenceValue = null;
            entry.FindPropertyRelative("minQuantity").intValue = rows[i].min;
            entry.FindPropertyRelative("maxQuantity").intValue = rows[i].max;
            entry.FindPropertyRelative("probability").floatValue = rows[i].chance;
        }
    });

    private static T Find<T>(string folder, string file) where T : Object
    {
        var found = AssetDatabase.LoadAssetAtPath<T>(folder + file + ".asset");
        return found != null ? found : throw new Exception($"No {typeof(T).Name} {folder}{file}");
    }

    // ----- Home -----

    private static void QuietHomeNights()
    {
        var so = new SerializedObject(NightSettings.Current);
        so.FindProperty("nightfallText").stringValue = "Night falls. What walks the dark beyond the ward is stronger now.";
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(NightSettings.Current);
    }

    // The briefing now tells the run's hour, forest and rumours: the box grows, and the modifier list gives up the room.
    private static void MakeRoomInThePlanner()
    {
        const string path = "Assets/_Project/Prefabs/UI/RaidPreparationWindow.prefab";
        const float briefing = 214f;
        const float modifiers = 150f;
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform panel = contents.transform.Find("Panel");
            void Place(string child, float top, float height = -1f)
            {
                var rect = (RectTransform)panel.Find(child);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top);
                if (height >= 0f)
                {
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
                }
            }

            float below = -92f - briefing - 10f;
            Place("Description", -92f, briefing);
            Place("Modifiers Title", below);
            Place("Modifiers Hint", below - 26f);
            Place("Modifier Scroll", below - 52f, modifiers);
            float rules = below - 52f - modifiers - 14f;
            Place("Rules Title", rules);
            Place("Rules", rules - 28f);
            Place("Risk", rules - 58f);
            Place("Sparks Row", rules - 136f);
            var rect2 = (RectTransform)panel;
            rect2.sizeDelta = new Vector2(rect2.sizeDelta.x, -(rules - 136f) + 32f + 84f);
            PrefabUtility.SaveAsPrefabAsset(contents, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    // ----- Sites (placeholder shapes) -----

    private static void Shelter(Transform root)
    {
        Piece(root, PrimitiveType.Cube, new Vector3(0f, 1f, 0f), new Vector3(3f, 0.15f, 2.4f), Mat(Wood)).localRotation = Quaternion.Euler(28f, 0f, 0f);
        Piece(root, PrimitiveType.Cylinder, new Vector3(-1.3f, 0.7f, 0.8f), new Vector3(0.12f, 0.7f, 0.12f), Mat(Wood));
        Piece(root, PrimitiveType.Cylinder, new Vector3(1.3f, 0.7f, 0.8f), new Vector3(0.12f, 0.7f, 0.12f), Mat(Wood));
    }

    private static void Stand(Transform root)
    {
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI / 3f;
            Piece(root, PrimitiveType.Cylinder, new Vector3(Mathf.Cos(angle) * 2f, 0.5f, Mathf.Sin(angle) * 2f), new Vector3(0.1f, 0.6f, 0.1f), Mat(Wood)).localRotation = Quaternion.Euler(14f, i * 60f, 0f);
        }
    }

    private static void Marks(Transform root)
    {
        Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.4f, 1.6f, 0.4f), Mat(Wood));
        Piece(root, PrimitiveType.Cube, new Vector3(0f, 1.8f, 0.36f), new Vector3(0.3f, 0.06f, 0.05f), Tint("Pale Mark", new Color(0.85f, 0.85f, 0.7f)));
        Piece(root, PrimitiveType.Cube, new Vector3(0f, 1.5f, 0.36f), new Vector3(0.06f, 0.5f, 0.05f), Tint("Pale Mark", new Color(0.85f, 0.85f, 0.7f)));
    }

    private static void Circle(Transform root)
    {
        for (int i = 0; i < 7; i++)
        {
            float angle = i * Mathf.PI * 2f / 7f;
            Piece(root, PrimitiveType.Cube, new Vector3(Mathf.Cos(angle) * 2.2f, 0.7f, Mathf.Sin(angle) * 2.2f), new Vector3(0.5f, 1.4f, 0.3f), Mat(Stone)).localRotation = Quaternion.Euler(0f, -i * 51f, 0f);
        }
    }

    private static void Hoard(Transform root)
    {
        Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.2f, 0f), new Vector3(4f, 1.8f, 3.2f), Mat(Stone));
        Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.9f, 0.9f), new Vector3(0.8f, 0.5f, 0.6f), Tint("Old Gold", new Color(0.8f, 0.6f, 0.2f)));
    }

    private static void Cairn(Transform root)
    {
        for (int i = 0; i < 4; i++)
        {
            Piece(root, PrimitiveType.Sphere, new Vector3(0f, 0.4f + i * 0.7f, 0f), Vector3.one * (2.4f - i * 0.5f), Mat(Stone));
        }
    }

    private static void Camp(Transform root)
    {
        Piece(root, PrimitiveType.Cube, new Vector3(0f, 0.4f, 0f), new Vector3(1.4f, 0.8f, 0.9f), Mat(Wood));
        Piece(root, PrimitiveType.Cylinder, new Vector3(1.4f, 0.6f, 0.6f), new Vector3(1.2f, 0.06f, 1.2f), Mat(Wood)).localRotation = Quaternion.Euler(80f, 0f, 0f);
    }

    private static void Swarm(Transform root)
    {
        Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 1.6f, 0f), new Vector3(0.3f, 1.6f, 0.3f), Mat(Wood));
        Piece(root, PrimitiveType.Sphere, new Vector3(0.3f, 2.3f, 0f), new Vector3(0.7f, 1f, 0.7f), Tint("Bee Swarm", new Color(0.25f, 0.18f, 0.06f)));
    }

    private static void Patch(Transform root)
    {
        for (int i = 0; i < 5; i++)
        {
            Piece(root, PrimitiveType.Sphere, new Vector3(Mathf.Cos(i * 1.3f) * 1.5f, 0.3f, Mathf.Sin(i * 1.3f) * 1.5f), new Vector3(1.1f, 0.6f, 1.1f), Tint("Elder Leaves", new Color(0.26f, 0.42f, 0.2f)));
        }
    }

    private static void Totem(Transform root)
    {
        Piece(root, PrimitiveType.Cylinder, new Vector3(0f, 1.5f, 0f), new Vector3(0.35f, 1.5f, 0.35f), Mat(Wood));
        Piece(root, PrimitiveType.Cube, new Vector3(0f, 2.3f, 0f), new Vector3(1.8f, 0.12f, 0.12f), Tint("Old Bone", new Color(0.8f, 0.76f, 0.62f))).localRotation = Quaternion.Euler(0f, 0f, 14f);
        Piece(root, PrimitiveType.Cube, new Vector3(0f, 2.8f, 0f), new Vector3(1.2f, 0.1f, 0.1f), Tint("Old Bone", new Color(0.8f, 0.76f, 0.62f))).localRotation = Quaternion.Euler(0f, 90f, -10f);
    }

    private static GameObject Site(string name, Action<Transform> shape)
    {
        string path = Sites + name + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        var root = new GameObject(name);
        try
        {
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            shape(model);

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1f, 0f);
            box.size = new Vector3(2f, 2f, 2f);

            var beacon = new GameObject("Beacon").transform;
            beacon.SetParent(root.transform, false);
            Piece(beacon, PrimitiveType.Cylinder, new Vector3(0f, 12f, 0f), new Vector3(0.6f, 12f, 0.6f), GoldBeam());
            var light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(beacon, false);
            light.transform.localPosition = new Vector3(0f, 3f, 0f);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.8f, 0.45f);
            light.intensity = 3f;
            light.range = 12f;
            light.shadows = LightShadows.None;
            light.gameObject.AddComponent<AtmosphereLight>();

            var site = root.AddComponent<RaidEventSite>();
            var siteData = new SerializedObject(site);
            siteData.FindProperty("beacon").objectReferenceValue = beacon.gameObject;
            siteData.ApplyModifiedPropertiesWithoutUndo();
            var interactable = root.AddComponent<Interactable>();
            var data = new SerializedObject(interactable);
            data.FindProperty("kind").intValue = (int)InteractionKind.Use;
            data.FindProperty("deactivateOnComplete").boolValue = false;
            data.FindProperty("range").floatValue = 3f;
            data.ApplyModifiedPropertiesWithoutUndo();
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

    private static Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

    private static Material Tint(string name, Color color)
    {
        string path = Sites + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }

    // A rumour's beam is gold, where a random event's is pale: the player can tell what they came for.
    private static Material GoldBeam()
    {
        string path = Sites + "Rumour Beacon.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(Sites + "Event Beacon.mat"));
            Color pale = material.GetColor("_BaseColor");
            material.SetColor("_BaseColor", new Color(1f, 0.78f, 0.35f, pale.a));
            AssetDatabase.CreateAsset(material, path);
        }

        return material;
    }
}
