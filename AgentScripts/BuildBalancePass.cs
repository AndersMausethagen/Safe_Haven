#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The balance pass of 2026-11-06 (`unity command script run -- --file AgentScripts/BuildBalancePass.cs --entry BuildBalancePass.Apply`).
/// Run it last, after BuildHaven, BuildBaseTiers, BuildFoundation and BuildHavenProgression, as those set building costs too. Every value is
/// absolute, so it is safe to run again. Why: "The balance pass" in Docs/BaseProgression.md and "Night" in Docs/GearScoreAndRaidTiers.md.
///
/// - Buildings: every building and level costs twice what it did (the user's call, after measuring about 13 oak a minute against a Cottage
///   of 24). Trophies from named creatures (Guardian Heartroot, Trollstone, Runestone Fragment) keep their count: doubling them would double the kills.
/// - Charcoal: burnt at the Woodcutter's Lodge from oak (3 for 2) or sticks (10 for 1), and the Smithy's fuel: 2 a copper ingot, 3 an iron and
///   4 a moonsilver (were 1 oak or 1 hardwood). Tor keeps some in the stash. A wood sink that grows with the metal, as Valheim's and Enshrouded's kilns do.
/// - Night: creatures 1.1x health and no more damage (were 1.3x and 1.25x, which stacked with the last stage's +35%).
/// </summary>
public static class BuildBalancePass
{
    private const string Data = "Assets/_Project/Data/";
    private const string BuildingFolder = Data + "Buildings/";
    private const string RecipeFolder = Data + "Crafting/Recipes/";
    private const string CharcoalPath = Data + "Items/Materials/Charcoal.asset";
    private const string CharcoalIcon = "Assets/_Project/UI/ResourceIcons/Charcoal.png";
    private const string NightPath = "Assets/_Project/Resources/NightSettings.asset";

    public const int CostFactor = 2;
    public const float NightHealth = 1.1f;
    public const float NightPower = 1f;

    /// <summary>What a named creature drops once: never multiplied.</summary>
    private static readonly HashSet<string> Trophies = new HashSet<string> { "GuardianHeartroot", "Trollstone", "RunestoneFragment" };

    /// <summary>The fuel each ingot takes, in charcoal, in place of its oak or hardwood.</summary>
    private static readonly (string Recipe, string OldFuel, int Charcoal)[] Smelting =
    {
        ("CopperIngot", "OakWood", 2),
        ("IronIngot", "Hardwood", 3),
        ("MoonsilverIngot", "Hardwood", 4),
    };

    /// <summary>
    /// Every building's cost before this pass, as BuildHaven, BuildBaseTiers, BuildFoundation and BuildHavenProgression leave it: building,
    /// level (0 is construction, n the n-th upgrade), whether these are essences (resources) rather than items, and the amounts.
    /// </summary>
    private static readonly (string Building, int Level, bool Essence, (string Name, int Amount)[] Cost)[] BaseCosts =
    {
        ("Beehive", 0, false, new[] { ("OakPlank", 3), ("NettleCord", 2) }),
        ("Campfire", 0, false, new[] { ("Sticks", 4), ("SmallRocks", 6) }),
        ("Campfire", 1, false, new[] { ("GreyStone", 6) }),
        ("Cauldron", 0, false, new[] { ("OakPlank", 4) }),
        ("Cauldron", 0, true, new[] { ("LifeMote", 4) }),
        ("Cauldron", 1, false, new[] { ("OakWood", 6), ("GreyStone", 6) }),
        ("Cauldron", 2, false, new[] { ("HardwoodPlank", 6), ("IronIngot", 2) }),
        ("Cauldron", 2, true, new[] { ("PurifiedEssence", 8) }),
        ("ConstructionTable", 0, false, new[] { ("OakWood", 12), ("GreyStone", 6), ("Sticks", 6) }),
        ("ConstructionTable", 1, false, new[] { ("OakPlank", 8), ("StoneBlock", 4), ("CopperIngot", 2) }),
        ("ConstructionTable", 2, false, new[] { ("HardwoodPlank", 6), ("GraniteBlock", 6), ("IronIngot", 4) }),
        ("ConstructionTable", 3, false, new[] { ("HardwoodPlank", 6), ("MoonsilverIngot", 4), ("GuardianHeartroot", 2) }),
        ("ConstructionTable", 3, true, new[] { ("PurifiedEssence", 8) }),
        ("ConstructionTable", 4, false, new[] { ("Elderwood", 6), ("MoonsilverIngot", 6), ("Trollstone", 2) }),
        ("ConstructionTable", 4, true, new[] { ("PurifiedEssence", 15) }),
        ("Cottage", 0, false, new[] { ("OakPlank", 12), ("StoneBlock", 6), ("NettleCord", 4) }),
        ("Cottage", 1, false, new[] { ("StoneBlock", 4), ("OakPlank", 6) }),
        ("ElderTree", 0, false, new[] { ("Hardwood", 4) }),
        ("FarmPlot", 0, false, new[] { ("OakWood", 4), ("Sticks", 4) }),
        ("FarmPlot", 1, false, new[] { ("GreyStone", 6), ("Sticks", 6) }),
        ("Forge", 0, false, new[] { ("GreyStone", 12), ("StoneBlock", 4), ("OakPlank", 4) }),
        ("Forge", 1, false, new[] { ("CopperIngot", 6), ("StoneBlock", 4), ("OakPlank", 4) }),
        ("Forge", 2, false, new[] { ("IronIngot", 8), ("GraniteBlock", 6), ("HardwoodPlank", 4) }),
        ("Forge", 3, false, new[] { ("MoonsilverIngot", 6), ("GraniteBlock", 4), ("Deepstone", 2), ("GuardianHeartroot", 1) }),
        ("Forge", 3, true, new[] { ("PurifiedEssence", 8) }),
        ("Forge", 4, false, new[] { ("MoonsilverIngot", 6), ("Trollstone", 3) }),
        ("Forge", 4, true, new[] { ("PurifiedEssence", 20) }),
        ("Furnace", 0, false, new[] { ("IronOre", 4), ("StoneBlock", 4), ("GreyStone", 12) }),
        ("Furnace", 1, false, new[] { ("GreyStone", 10), ("StoneBlock", 2) }),
        ("Furnace", 2, false, new[] { ("StoneBlock", 6), ("GraniteBlock", 2) }),
        ("HavenBanner", 0, false, new[] { ("OakWood", 2), ("NettleCord", 2) }),
        ("HerbPlanter", 0, false, new[] { ("OakWood", 3), ("NettleFiber", 2) }),
        ("Hut", 0, false, new[] { ("OakWood", 8), ("Sticks", 6), ("NettleFiber", 4) }),
        ("Hut", 1, false, new[] { ("OakPlank", 6), ("NettleCord", 4) }),
        ("IronTrap", 0, false, new[] { ("IronIngot", 2), ("HardwoodPlank", 2) }),
        ("Kitchen", 0, false, new[] { ("OakPlank", 10), ("StoneBlock", 6), ("CopperIngot", 2) }),
        ("Kitchen", 1, false, new[] { ("OakPlank", 8), ("StoneBlock", 4), ("CopperIngot", 2) }),
        ("Kitchen", 2, false, new[] { ("HardwoodPlank", 6), ("IronIngot", 4), ("MoonsilverIngot", 2) }),
        ("LanternPost", 0, false, new[] { ("OakWood", 2), ("Sticks", 2) }),
        ("Loom", 0, false, new[] { ("OakPlank", 10), ("NettleCord", 6), ("OakWood", 4) }),
        ("Loom", 1, false, new[] { ("OakPlank", 6), ("NettleCord", 4), ("Linen", 2) }),
        ("Loom", 2, false, new[] { ("HardwoodPlank", 6), ("Linen", 4), ("ThickHide", 2) }),
        ("Loom", 3, false, new[] { ("HuldraSilk", 4), ("MoonsilverIngot", 2), ("Linen", 6) }),
        ("Palisade", 0, false, new[] { ("OakWood", 6) }),
        ("PurificationAltar", 0, false, new[] { ("StoneBlock", 8) }),
        ("PurificationAltar", 0, true, new[] { ("LifeOrb", 3) }),
        ("PurificationAltar", 1, false, new[] { ("StoneBlock", 12), ("GraniteBlock", 2) }),
        ("PurificationAltar", 2, false, new[] { ("GraniteBlock", 6), ("HardwoodPlank", 3) }),
        ("PurificationAltar", 2, true, new[] { ("PurifiedEssence", 8) }),
        ("Sauna", 0, false, new[] { ("OakPlank", 10), ("GreyStone", 8) }),
        ("Sawbench", 0, false, new[] { ("OakWood", 10), ("GreyStone", 4) }),
        ("Sawbench", 1, false, new[] { ("OakPlank", 8), ("HardwoodPlank", 4) }),
        ("Sawbench", 2, false, new[] { ("HardwoodPlank", 6), ("IronIngot", 3), ("NettleCord", 4) }),
        ("Sawbench", 3, false, new[] { ("Elderwood", 6), ("MoonsilverIngot", 2), ("GuardianHeartroot", 1) }),
        ("Sawbench", 4, false, new[] { ("Elderwood", 6), ("Trollstone", 1) }),
        ("Sawbench", 4, true, new[] { ("PurifiedEssence", 10) }),
        ("SmallChest", 0, false, new[] { ("OakWood", 4), ("Sticks", 6) }),
        ("SmallChest", 1, false, new[] { ("OakWood", 4), ("SmallRocks", 4) }),
        ("SpikeTrap", 0, false, new[] { ("OakWood", 3), ("Sticks", 4) }),
        ("StoneWall", 0, false, new[] { ("StoneBlock", 4), ("GraniteBlock", 1) }),
        ("StorageShed", 0, false, new[] { ("GreyStone", 6), ("OakWood", 4), ("OakPlank", 10) }),
        ("StorageShed", 1, false, new[] { ("OakPlank", 8), ("GreyStone", 6) }),
        ("StorageShed", 2, false, new[] { ("OakPlank", 14), ("StoneBlock", 4) }),
        ("TanningRack", 0, false, new[] { ("OakWood", 12), ("NettleCord", 4), ("RoughHide", 2) }),
        ("TanningRack", 1, false, new[] { ("OakPlank", 8), ("RoughHide", 4), ("CopperIngot", 2) }),
        ("TanningRack", 2, false, new[] { ("HardwoodPlank", 6), ("IronIngot", 3), ("ThickHide", 2) }),
        ("TanningRack", 3, false, new[] { ("Elderwood", 6), ("MoonsilverIngot", 3), ("HuldraSilk", 2) }),
        ("TanningRack", 4, false, new[] { ("Elderwood", 4), ("Trollstone", 2) }),
        ("TanningRack", 4, true, new[] { ("PurifiedEssence", 10) }),
        ("Torch", 0, false, new[] { ("Sticks", 2), ("NettleFiber", 1) }),
        ("TradingPost", 0, false, new[] { ("HardwoodPlank", 8), ("StoneBlock", 6), ("Linen", 4), ("CopperIngot", 4) }),
        ("WainwrightsYard", 0, false, new[] { ("OakPlank", 10), ("OakWood", 8), ("NettleCord", 4) }),
        ("WainwrightsYard", 1, false, new[] { ("OakPlank", 8), ("IronIngot", 2) }),
        ("WardTotem", 0, false, new[] { ("GraniteBlock", 4), ("Deepstone", 2), ("RunestoneFragment", 1) }),
        ("WatchPost", 0, false, new[] { ("OakWood", 6), ("OakPlank", 4), ("NettleCord", 2) }),
        ("WatchPost", 1, false, new[] { ("OakPlank", 8), ("CopperIngot", 2) }),
        ("WatchPost", 2, false, new[] { ("HardwoodPlank", 6), ("IronIngot", 3) }),
        ("Well", 0, false, new[] { ("GreyStone", 8), ("OakPlank", 4) }),
    };

    public static string Apply()
    {
        var log = new StringBuilder();
        int oakBefore = OakForEverything();
        ItemDefinition charcoal = Charcoal();
        Dictionary<string, ItemDefinition> items = All<ItemDefinition>().GroupBy(item => item.name).ToDictionary(group => group.Key, group => group.First());
        Dictionary<string, ResourceDefinition> resources = All<ResourceDefinition>().GroupBy(resource => resource.name).ToDictionary(group => group.Key, group => group.First());

        int changed = 0;
        foreach ((string building, int level, bool essence, (string name, int amount)[] cost) in BaseCosts)
        {
            string problem = SetCost(building, level, essence, cost, items, resources);
            if (problem != null)
            {
                log.AppendLine(problem);
            }
            else
            {
                changed++;
            }
        }

        log.AppendLine($"Building costs x{CostFactor}: {changed} of {BaseCosts.Length} cost lists set");
        log.AppendLine(FirstCampQuest());
        log.AppendLine(CharcoalRecipes(charcoal, items));
        log.AppendLine(SmeltWithCharcoal(charcoal));
        log.AppendLine(TorBurnsCharcoal());
        log.AppendLine($"Oak for every building and level once: {oakBefore} -> {OakForEverything()} (a plank is 2 oak)");
        log.AppendLine(Night());
        Refresh();
        AssetDatabase.SaveAssets();
        return log.ToString();
    }

    // ----- Building costs -----

    private static string SetCost(string file, int level, bool essence, (string name, int amount)[] cost,
        Dictionary<string, ItemDefinition> items, Dictionary<string, ResourceDefinition> resources)
    {
        var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(BuildingFolder + file + ".asset");
        if (building == null)
        {
            return $"MISSING building {file}";
        }

        var so = new SerializedObject(building);
        string listName = level == 0 ? (essence ? "constructionCost" : "constructionItems") : (essence ? "essence" : "cost");
        string field = essence ? "resource" : "item";
        SerializedProperty list;
        if (level == 0)
        {
            list = so.FindProperty(listName);
        }
        else
        {
            SerializedProperty upgrades = so.FindProperty("upgrades");
            if (upgrades.arraySize < level)
            {
                return $"MISSING {file} level {level}";
            }

            list = upgrades.GetArrayElementAtIndex(level - 1).FindPropertyRelative(listName);
        }

        foreach ((string name, int amount) in cost)
        {
            Object wanted = null;
            if (essence && resources.TryGetValue(name, out ResourceDefinition resource))
            {
                wanted = resource;
            }
            else if (!essence && items.TryGetValue(name, out ItemDefinition item))
            {
                wanted = item;
            }

            if (wanted == null)
            {
                return $"MISSING {(essence ? "resource" : "item")} {name} for {file}";
            }

            SerializedProperty entry = null;
            for (int e = 0; e < list.arraySize; e++)
            {
                if (list.GetArrayElementAtIndex(e).FindPropertyRelative(field).objectReferenceValue == wanted)
                {
                    entry = list.GetArrayElementAtIndex(e);
                    break;
                }
            }

            if (entry == null)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                entry = list.GetArrayElementAtIndex(list.arraySize - 1);
                entry.FindPropertyRelative(field).objectReferenceValue = wanted;
            }

            entry.FindPropertyRelative("amount").intValue = Trophies.Contains(name) ? amount : amount * CostFactor;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(building);
        return null;
    }

    // A Light in the Wood gathers exactly what the Workshop costs, so it doubles with it.
    private static string FirstCampQuest()
    {
        var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(Data + "Quests/FirstCamp.asset");
        if (quest == null)
        {
            return "MISSING quest FirstCamp";
        }

        (string name, int amount)[] workshop = BaseCosts.First(row => row.Building == "ConstructionTable" && row.Level == 0 && !row.Essence).Cost;
        var set = new List<string>();
        Edit(quest, so =>
        {
            SerializedProperty objectives = so.FindProperty("objectives");
            for (int i = 0; i < objectives.arraySize; i++)
            {
                SerializedProperty objective = objectives.GetArrayElementAtIndex(i);
                Object item = objective.FindPropertyRelative("item").objectReferenceValue;
                foreach ((string name, int amount) in workshop)
                {
                    if (item != null && item.name == name)
                    {
                        objective.FindPropertyRelative("amount").intValue = amount * CostFactor;
                        set.Add($"{name} {amount * CostFactor}");
                    }
                }
            }
        });
        return "A Light in the Wood gathers " + string.Join(", ", set);
    }

    // ----- Charcoal -----

    private static ItemDefinition Charcoal()
    {
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(CharcoalPath) == null)
        {
            AssetDatabase.CopyAsset(Data + "Items/Materials/GreyStone.asset", CharcoalPath);
        }

        var charcoal = AssetDatabase.LoadAssetAtPath<ItemDefinition>(CharcoalPath);
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(CharcoalIcon);
        if (icon == null)
        {
            icon = PlaceholderIconPainter.Paint(IconShape.Rock, new Color(0.16f, 0.15f, 0.14f), new Color(0.85f, 0.42f, 0.16f), CharcoalIcon);
        }

        Edit(charcoal, so =>
        {
            so.FindProperty("id").stringValue = "charcoal";
            so.FindProperty("displayName").stringValue = "Charcoal";
            so.FindProperty("description").stringValue = "Oak burnt slow under turf until it is black and light. The Smithy's fire wants it: wood alone never gets hot enough for metal.";
            so.FindProperty("tier").intValue = 1;
            so.FindProperty("value").intValue = 1;
            so.FindProperty("maximumStack").intValue = 99;
            so.FindProperty("weight").floatValue = 0.4f;
            so.FindProperty("icon").objectReferenceValue = icon;
        });
        return charcoal;
    }

    private static string CharcoalRecipes(ItemDefinition charcoal, Dictionary<string, ItemDefinition> items)
    {
        Recipe("Charcoal", "charcoal", "Charcoal", 2, 8f, 5, ("OakWood", 3));
        Recipe("CharcoalFromSticks", "charcoal_sticks", "Charcoal from Sticks", 1, 8f, 6, ("Sticks", 10));
        return "Woodcutter's Lodge: Charcoal (3 oak for 2), Charcoal from Sticks (10 for 1)";

        void Recipe(string file, string id, string label, int amount, float seconds, int order, params (string item, int amount)[] cost)
        {
            string path = RecipeFolder + file + ".asset";
            if (AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path) == null)
            {
                AssetDatabase.CopyAsset(RecipeFolder + "OakPlank.asset", path); // the Lodge, known from the start
            }

            Edit(AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path), so =>
            {
                so.FindProperty("id").stringValue = id;
                so.FindProperty("displayName").stringValue = label;
                so.FindProperty("output").objectReferenceValue = charcoal;
                so.FindProperty("outputAmount").intValue = amount;
                so.FindProperty("craftSeconds").floatValue = seconds;
                so.FindProperty("sortOrder").intValue = order;
                SerializedProperty list = so.FindProperty("itemCost");
                list.arraySize = cost.Length;
                for (int i = 0; i < cost.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = items[cost[i].item];
                    list.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
                }
            });
        }
    }

    private static string SmeltWithCharcoal(ItemDefinition charcoal)
    {
        var done = new List<string>();
        foreach ((string file, string oldFuel, int amount) in Smelting)
        {
            var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(RecipeFolder + file + ".asset");
            if (recipe == null)
            {
                done.Add($"MISSING recipe {file}");
                continue;
            }

            Edit(recipe, so =>
            {
                SerializedProperty list = so.FindProperty("itemCost");
                SerializedProperty fuel = null;
                for (int i = 0; i < list.arraySize; i++)
                {
                    Object item = list.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue;
                    if (item == charcoal || (item != null && item.name == oldFuel))
                    {
                        fuel = list.GetArrayElementAtIndex(i);
                    }
                }

                if (fuel == null)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    fuel = list.GetArrayElementAtIndex(list.arraySize - 1);
                }

                fuel.FindPropertyRelative("item").objectReferenceValue = charcoal;
                fuel.FindPropertyRelative("amount").intValue = amount;
            });
            done.Add($"{file} {amount}");
        }

        return "Smelted with charcoal: " + string.Join(", ", done);
    }

    // Tor keeps planks first, then charcoal, so Bjarne's ingots don't wait on the player.
    private static string TorBurnsCharcoal()
    {
        var job = AssetDatabase.LoadAssetAtPath<JobDefinition>(Data + "Settlement/Carpenter.asset");
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(RecipeFolder + "Charcoal.asset");
        if (job == null || recipe == null)
        {
            return "MISSING Carpenter job or Charcoal recipe";
        }

        Edit(job, so =>
        {
            SerializedProperty list = so.FindProperty("workOrders");
            SerializedProperty order = null;
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).FindPropertyRelative("recipe").objectReferenceValue == recipe)
                {
                    order = list.GetArrayElementAtIndex(i);
                }
            }

            if (order == null)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                order = list.GetArrayElementAtIndex(list.arraySize - 1);
                order.FindPropertyRelative("recipe").objectReferenceValue = recipe;
            }

            order.FindPropertyRelative("stockTarget").intValue = 40;
            order.FindPropertyRelative("keepInput").intValue = 10;
        });
        return "Tor keeps 40 charcoal, leaving 10 oak";
    }

    // ----- Night -----

    private static string Night()
    {
        var night = AssetDatabase.LoadAssetAtPath<NightSettings>(NightPath);
        if (night == null)
        {
            return "MISSING NightSettings";
        }

        string was = "";
        Edit(night, so =>
        {
            was = $"{so.FindProperty("creatureHealth").floatValue} / {so.FindProperty("creaturePower").floatValue}";
            so.FindProperty("creatureHealth").floatValue = NightHealth;
            so.FindProperty("creaturePower").floatValue = NightPower;
        });
        return $"Night creatures health / damage: {was} -> {NightHealth} / {NightPower}";
    }

    // ----- Helpers -----

    /// <summary>Raw oak to build every building and every level once, planks counted as the two oak they are sawn from.</summary>
    private static int OakForEverything()
    {
        int oak = 0;
        foreach (BuildingDefinition building in All<BuildingDefinition>())
        {
            foreach (ItemAmount amount in building.ConstructionItems.Concat(building.Upgrades.SelectMany(level => level.cost)))
            {
                string name = amount.Item != null ? amount.Item.name : "";
                oak += name == "OakWood" ? amount.Amount : name == "OakPlank" ? amount.Amount * 2 : 0;
            }
        }

        return oak;
    }

    private static List<T> All<T>() where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { Data })
        .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset != null).ToList();

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

    private static void Edit(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
