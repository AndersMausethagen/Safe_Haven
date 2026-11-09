using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The base progression (`unity command run_script --file AgentScripts/BuildBaseTiers.cs --entry BuildBaseTiers.Apply`): the Workshop,
/// Woodcutter's Lodge and Smithy as whole buildings with tiers I to V, which building opens which, the balance pass on tiers, tools,
/// nodes and recipes, and what the base does for raids. See Docs/BaseProgression.md for the reasoning. Replaces the Construction
/// Table, Sawbench, Forge and Furnace in place: assets and ids are kept, so saves and references hold. Safe to run again.
/// </summary>
public static class BuildBaseTiers
{
    private const string Models = "Assets/_Project/Art/Haven/Models/";
    private const string Icons = "Assets/_Project/Art/Haven/Icons/";
    private const string Tints = "Assets/_Project/Materials/Haven/";

    private static readonly Dictionary<string, ItemDefinition> Items = new Dictionary<string, ItemDefinition>();
    private static readonly Dictionary<string, BuildingDefinition> Buildings = new Dictionary<string, BuildingDefinition>();
    private static readonly List<RecipeDefinition> Recipes = new List<RecipeDefinition>();

    public static string Apply()
    {
        Load();
        var log = new List<string>();
        Tiers(log);
        Nodes(log);
        RecipeMoves(log);
        Plans(log);
        Upgrades(log);
        Chain(log);
        RaidBonuses(log);
        Story(log);
        Models_(log);
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    private static void Load()
    {
        Items.Clear();
        Buildings.Clear();
        Recipes.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition"))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Items[item.name] = item;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:BuildingDefinition"))
        {
            var building = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Buildings[building.name] = building;
        }

        foreach (string guid in AssetDatabase.FindAssets("t:RecipeDefinition"))
        {
            Recipes.Add(AssetDatabase.LoadAssetAtPath<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid)));
        }
    }

    // ----- Tiers: a material's tier is the tier of the gear made from it. Tools: Stone I, Copper II, Iron III. -----

    private static void Tiers(List<string> log)
    {
        foreach ((string item, int tier) in new[]
        {
            ("IronOre", 3), ("IronIngot", 3), ("ThickHide", 3),
            ("MoonsilverOre", 4), ("MoonsilverIngot", 4),
            ("Trollstone", 5), ("TrollstoneMaul", 5),
        })
        {
            Edit(Items[item], so => so.FindProperty("tier").intValue = tier);
        }

        foreach ((string item, int tier) in new[]
        {
            ("StoneHatchet", 1), ("StonePickaxe", 1), ("StoneSickle", 1),
            ("CopperHatchet", 2), ("CopperPickaxe", 2), ("CopperSickle", 2),
            ("IronHatchet", 3), ("IronPickaxe", 3), ("IronSickle", 3),
        })
        {
            Edit(Items[item], so => so.FindProperty("toolTier").intValue = tier);
        }

        log.Add("tiers: iron III, moonsilver IV, trollstone V; tools stone I, copper II, iron III");
    }

    private static void Nodes(List<string> log)
    {
        // Name, node tier, tool tier needed, yield of the main entry (0: leave it).
        foreach ((string node, int tier, int tool, int min, int max) in new[]
        {
            ("GreyStone", 1, 1, 2, 3),
            ("CopperOre", 2, 1, 3, 5),
            ("Flax", 2, 1, 0, 0),
            ("Granite", 2, 2, 3, 5),
            ("Hardwood", 2, 2, 2, 3),
            ("IronOre", 3, 2, 3, 4),
            ("Deepstone", 3, 3, 0, 0),
            ("Elderwood", 3, 3, 0, 0),
            ("HuldraSilk", 3, 3, 0, 0),
            ("MoonsilverOre", 4, 3, 3, 5),
            ("GreatBoulder", 2, 2, 0, 0),
            ("OldStump", 2, 2, 0, 0),
        })
        {
            GatheringNodeDefinition definition = FindNode(node);
            Edit(definition, so =>
            {
                so.FindProperty("tier").intValue = tier;
                so.FindProperty("requiredToolTier").intValue = tool;
                if (max > 0)
                {
                    SerializedProperty entry = so.FindProperty("yields").FindPropertyRelative("entries").GetArrayElementAtIndex(0);
                    entry.FindPropertyRelative("minQuantity").intValue = min;
                    entry.FindPropertyRelative("maxQuantity").intValue = max;
                }
            });
        }

        log.Add("nodes: copper needs a stone pick, iron copper, moonsilver iron");
    }

    private static GatheringNodeDefinition FindNode(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:GatheringNodeDefinition " + name))
        {
            var node = AssetDatabase.LoadAssetAtPath<GatheringNodeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (node.name == name)
            {
                return node;
            }
        }

        throw new InvalidOperationException("No node " + name);
    }

    // ----- Recipes: metal at the Smithy, wood at the Lodge, the rest at the Workshop. -----

    private static void RecipeMoves(List<string> log)
    {
        int moved = 0;
        moved += Move(CraftingStationType.Anvil, "CopperIngot", "IronIngot", "MoonsilverIngot", "BodkinArrow", "CopperBand", "CopperBoundShield", "CopperHandAxe",
            "CopperScaleGauntlets", "CopperScaleGirdle", "CopperScaleGreaves", "CopperScaleHauberk", "CopperScaleHelm", "CopperSpear", "CopperSword");
        moved += Move(CraftingStationType.Workbench, "WovenHideBoots", "WovenHideCap", "WovenHideGloves", "WovenHideJerkin", "WovenCordBelt", "WovenSatchel", "BoneCharm");
        moved += Move(CraftingStationType.Sawbench, "RoundShield", "HuntersBow", "ElderwoodLongbow");

        SetCost("CopperIngot", ("CopperOre", 3), ("OakWood", 1));
        SetCost("IronIngot", ("IronOre", 3), ("Hardwood", 1));
        SetCost("MoonsilverIngot", ("MoonsilverOre", 3), ("Hardwood", 1));
        log.Add($"recipes: {moved} moved; ingots take 3 ore");
    }

    private static int Move(CraftingStationType station, params string[] outputs)
    {
        int moved = 0;
        foreach (string output in outputs)
        {
            foreach (RecipeDefinition recipe in Recipes.Where(one => one.Output == Items[output]))
            {
                Edit(recipe, so => so.FindProperty("station").intValue = (int)station);
                moved++;
            }
        }

        return moved;
    }

    private static void SetCost(string output, params (string item, int amount)[] cost)
    {
        foreach (RecipeDefinition recipe in Recipes.Where(one => one.Output == Items[output]))
        {
            Edit(recipe, so => SetAmounts(so.FindProperty("itemCost"), cost));
        }
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

    // The Forge needs no blueprint now; the plans in the caches teach the one thing that is still rare.
    private static void Plans(List<string> log)
    {
        RecipeDefinition maul = Recipes.First(one => one.Output == Items["TrollstoneMaul"]);
        Edit(maul, so => so.FindProperty("knownFromStart").boolValue = false);
        Edit(Items["ForgeBlueprint"], so =>
        {
            so.FindProperty("displayName").stringValue = "Trollsmith's Plans";
            so.FindProperty("description").stringValue = "How to bind trollstone to an elderwood haft. Read it to learn the Trollstone Maul.";
            so.FindProperty("teachesBuildings").ClearArray();
            SerializedProperty recipes = so.FindProperty("teachesRecipes");
            recipes.arraySize = 1;
            recipes.GetArrayElementAtIndex(0).objectReferenceValue = maul;
        });

        Edit(Buildings["Forge"], so => so.FindProperty("knownFromStart").boolValue = true);
        Edit(Buildings["Furnace"], so =>
        {
            so.FindProperty("showInBuildMenu").boolValue = false;
            so.FindProperty("station").intValue = (int)CraftingStationType.Anvil;
        });
        foreach (string job in new[] { "Smith", "Carpenter" })
        {
            foreach (string guid in AssetDatabase.FindAssets("t:JobDefinition " + job))
            {
                Edit(AssetDatabase.LoadAssetAtPath<JobDefinition>(AssetDatabase.GUIDToAssetPath(guid)), so => so.FindProperty("teachesBuildings").ClearArray());
            }
        }

        log.Add("plans: the Forge Blueprint teaches the Trollstone Maul; the Furnace is out of the menu and works as a small Smithy in old saves");
    }

    // ----- Upgrades -----

    private static BuildingLevel Level(string title, string description, float work, int renown, (string item, int amount)[] cost, int essence = 0,
        float speed = 0f, int queue = 0, int output = 0, int rumours = 0, int comfort = 0, int logs = 0, string[] teaches = null)
    {
        var level = new BuildingLevel
        {
            title = title,
            description = description,
            work = work,
            renown = renown,
            craftSpeed = speed,
            extraQueue = queue,
            extraOutput = output,
            extraRumours = rumours,
            comfort = comfort,
            extraLogs = logs,
        };
        foreach ((string item, int amount) in cost)
        {
            level.cost.Add(new ItemAmount(Items[item], amount));
        }

        if (essence > 0)
        {
            level.essence.Add(new ResourceAmount(SettlementSettings.Current.PurifiedEssence, essence));
        }

        foreach (string recipe in teaches ?? Array.Empty<string>())
        {
            level.teachesRecipes.Add(Recipes.First(one => one.name == recipe));
        }

        return level;
    }

    private static void Upgrades(List<string> log)
    {
        Whole("ConstructionTable", "Workshop", "Where everything starts. Basic gear, utilities and stone work are made here, and worn gear is repaired.",
            new[] { ("OakWood", 12), ("GreyStone", 6), ("Sticks", 6) }, 8f, 8, new[]
            {
                Level("Joined", "Proper joints and a vice. It makes what tier II asks.", 8f, 4, new[] { ("OakPlank", 8), ("StoneBlock", 4), ("CopperIngot", 2) }, speed: 0.15f, queue: 1),
                Level("Iron-Bound", "Iron straps, and every tool has its place on the wall.", 10f, 6, new[] { ("HardwoodPlank", 6), ("GraniteBlock", 6), ("IronIngot", 4) }, speed: 0.15f),
                Level("Master's", "Heartroot set in the bench: the work comes easier here.", 12f, 8, new[] { ("HardwoodPlank", 6), ("MoonsilverIngot", 4), ("GuardianHeartroot", 2) }, 8, speed: 0.2f, queue: 1, output: 5),
                Level("Haven's Own", "The best-kept workshop this side of the portal.", 14f, 12, new[] { ("Elderwood", 6), ("MoonsilverIngot", 6), ("Trollstone", 2) }, 15, speed: 0.25f, queue: 1),
            });

        Whole("Sawbench", "Woodcutter's Lodge", "Turns logs into planks, and planks into shields and bows. Woodcutters know how to get more from a tree.",
            new[] { ("OakWood", 10), ("GreyStone", 4) }, 6f, 6, new[]
            {
                Level("Sharpened", "A heavier blade that bites deeper.", 8f, 4, new[] { ("OakPlank", 8), ("HardwoodPlank", 4) }, speed: 0.2f, queue: 1),
                Level("Treadle", "A treadle saw, and every tree at home gives more.", 9f, 6, new[] { ("HardwoodPlank", 6), ("IronIngot", 3), ("NettleCord", 4) }, speed: 0.2f, logs: 1),
                Level("Waterwheel", "A wheel that never tires.", 11f, 7, new[] { ("Elderwood", 6), ("MoonsilverIngot", 2), ("GuardianHeartroot", 1) }, speed: 0.2f, output: 5),
                Level("Haven's Own", "No log leaves this yard wasted.", 13f, 10, new[] { ("Elderwood", 6), ("Trollstone", 1) }, 10, speed: 0.25f, logs: 1),
            });

        Whole("Forge", "Smithy", "Smelts ore into ingots and works metal into tools, weapons and armor. A tier smelts what its next upgrade costs.",
            new[] { ("GreyStone", 12), ("StoneBlock", 4), ("OakPlank", 4) }, 10f, 10, new[]
            {
                Level("Copper Fire", "A hotter fire and a first real anvil: copper tools and gear.", 9f, 6, new[] { ("CopperIngot", 6), ("StoneBlock", 4), ("OakPlank", 4) }, speed: 0.15f, queue: 1),
                Level("Iron Fire", "Bellows that breathe on their own: iron tools and gear.", 11f, 8, new[] { ("IronIngot", 8), ("GraniteBlock", 6), ("HardwoodPlank", 4) }, speed: 0.15f),
                Level("Moon Fire", "A fire that burns white: moonsilver gear.", 13f, 10, new[] { ("MoonsilverIngot", 6), ("GraniteBlock", 4), ("Deepstone", 2), ("GuardianHeartroot", 1) }, 8, speed: 0.2f, queue: 1, output: 5),
                Level("Trollfire", "An anvil of trollstone, and nothing out there it can't work.", 15f, 14, new[] { ("MoonsilverIngot", 6), ("Trollstone", 3) }, 20, speed: 0.25f, queue: 1),
            });

        Buildings["WatchPost"].ConfigureBuildingUp(Buildings["WatchPost"].BuildWork, Buildings["WatchPost"].Renown, new[]
        {
            Level("Lookout", "Someone up there at dusk and dawn. It hears what moves, and the runes of a safer road.", 8f, 4, new[] { ("OakPlank", 8), ("CopperIngot", 2) }, rumours: 1, teaches: new[] { "RuneOfPlenty", "NornsHourglass" }),
            Level("Beacon", "A fire you can see from the woods. Now you can choose to go in harder, for more.", 10f, 6, new[] { ("HardwoodPlank", 6), ("IronIngot", 3) }, rumours: 1, teaches: new[] { "BloodMoonRune" }),
        });
        EditorUtility.SetDirty(Buildings["WatchPost"]);
        Edit(Buildings["WatchPost"], so => SetTeaches(so, "WayfinderRune"));

        Buildings["Cottage"].ConfigureBuildingUp(Buildings["Cottage"].BuildWork, Buildings["Cottage"].Renown, new[]
        {
            Level("Whitewashed", "Lime on the walls and smoke from the chimney.", 9f, 6, new[] { ("StoneBlock", 4), ("OakPlank", 6) }, comfort: 2),
        });
        EditorUtility.SetDirty(Buildings["Cottage"]);
        log.Add("upgrades: Workshop, Lodge and Smithy have tiers I to V; Watch Post and Cottage feed raids");
    }

    private static void SetTeaches(SerializedObject so, params string[] recipes)
    {
        SerializedProperty list = so.FindProperty("teachesRecipes");
        list.arraySize = recipes.Length;
        for (int i = 0; i < recipes.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = Recipes.First(one => one.name == recipes[i]);
        }
    }

    private static void Whole(string file, string label, string description, (string item, int amount)[] cost, float work, int renown, BuildingLevel[] levels)
    {
        BuildingDefinition building = Buildings[file];
        Edit(building, so =>
        {
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = description;
            so.FindProperty("yardDepth").floatValue = 2f;
            SetAmounts(so.FindProperty("constructionItems"), cost);
            so.FindProperty("constructionCost").ClearArray();
        });
        building.ConfigureBuildingUp(work, renown, levels);
        EditorUtility.SetDirty(building);
    }

    // ----- Which building opens which -----

    private static void Chain(List<string> log)
    {
        const string workshop = "ConstructionTable";
        const string lodge = "Sawbench";
        const string smithy = "Forge";
        foreach ((string building, string needs, int tier) in new[]
        {
            ("Sawbench", workshop, 1), ("SmallChest", workshop, 1), ("FarmPlot", workshop, 1), ("Well", workshop, 1),
            ("TanningRack", workshop, 1), ("SpikeTrap", workshop, 1), ("HavenBanner", workshop, 1), ("HerbPlanter", workshop, 1),
            ("Forge", lodge, 1), ("Palisade", lodge, 1), ("StorageShed", lodge, 1), ("Cauldron", lodge, 1), ("WatchPost", lodge, 1),
            ("Loom", "TanningRack", 1), ("Kitchen", smithy, 1), ("Cottage", workshop, 2),
        })
        {
            Edit(Buildings[building], so =>
            {
                SerializedProperty list = so.FindProperty("requires");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).FindPropertyRelative("building").objectReferenceValue = Buildings[needs];
                list.GetArrayElementAtIndex(0).FindPropertyRelative("tier").intValue = tier;
            });
        }

        // Cost changes that follow from the chain: the Kitchen comes after the first copper.
        log.Add("chain: Workshop > Lodge > Smithy; everything else hangs off them");
    }

    // ----- What the base does for raids -----

    private static void RaidBonuses(List<string> log)
    {
        foreach ((string building, int comfort) in new[]
        {
            ("Cottage", 4), ("Sauna", 3), ("Kitchen", 2), ("Well", 1), ("Campfire", 1), ("LanternPost", 1), ("HavenBanner", 1), ("HerbPlanter", 1),
        })
        {
            Edit(Buildings[building], so => so.FindProperty("comfort").intValue = comfort);
        }

        Edit(SettlementSettings.Current, so =>
        {
            SerializedProperty levels = so.FindProperty("wardLevels");
            for (int i = 0; i < levels.arraySize; i++)
            {
                levels.GetArrayElementAtIndex(i).FindPropertyRelative("raidSeconds").floatValue = 90f;
            }
        });
        Edit(HavenSettings.Current, so =>
        {
            so.FindProperty("restedSecondsPerComfort").floatValue = 60f;
            SerializedProperty bonuses = so.FindProperty("restedBonuses");
            bonuses.arraySize = 2;
            SetBonus(bonuses.GetArrayElementAtIndex(0), StatType.MaxStamina, 20f);
            SetBonus(bonuses.GetArrayElementAtIndex(1), StatType.MaxHealth, 15f);
        });
        log.Add("raids: ward levels +90 s each, comfort gives Well Rested, Watch Post gives rumours and runes");
    }

    private static void SetBonus(SerializedProperty bonus, StatType stat, float value)
    {
        bonus.FindPropertyRelative("stat").intValue = (int)stat;
        bonus.FindPropertyRelative("op").intValue = (int)StatModifierOp.Flat;
        bonus.FindPropertyRelative("value").floatValue = value;
    }

    // ----- Words and quests -----

    private static void Story(List<string> log)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:QuestDefinition"))
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            Edit(quest, so =>
            {
                SerializedProperty it = so.GetIterator();
                while (it.NextVisible(true))
                {
                    if (it.propertyType == SerializedPropertyType.ObjectReference && it.objectReferenceValue == Buildings["Furnace"])
                    {
                        it.objectReferenceValue = Buildings["Forge"];
                    }
                    else if (it.propertyType == SerializedPropertyType.String)
                    {
                        it.stringValue = it.stringValue
                            .Replace("set a table beside it to work at", "raise a workshop beside it to work at")
                            .Replace("Tor will ask for a sawbench before he asks for supper, Bjarne for a furnace", "Tor will ask for a woodcutter's lodge before supper, Bjarne for a smithy");
                    }
                }
            });
        }

        log.Add("story: the quests ask for the new buildings");
    }

    // ----- Models: placeholders from primitives, like the other generated buildings. Each tier adds parts. -----

    private static void Models_(List<string> log)
    {
        Rebuild("ConstructionTable", "Workshop Model", WorkshopModel);
        Rebuild("Sawbench", "Woodcutters Lodge Model", LodgeModel);
        Rebuild("Forge", "Smithy Model", SmithyModel);
        BuildingPrefabBuilder.Update(Buildings["Furnace"].Prefab, Buildings["Furnace"]);
        log.Add("models: Workshop, Lodge and Smithy rebuilt with parts for each tier");
    }

    private static void Rebuild(string file, string modelName, Action<Transform> shape)
    {
        BuildingDefinition building = Buildings[file];
        string modelPath = Models + modelName + ".prefab";
        PrefabBuildUtility.EnsureFolder(Models.TrimEnd('/'));
        // A model made for real (AgentScripts/BuildStructureModels.cs: it has levels of detail) is kept: only a
        // stand-in of primitives is drawn again.
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null || model.GetComponentInChildren<LODGroup>(true) == null)
        {
            var made = new GameObject(modelName);
            try
            {
                shape(made.transform);
                model = PrefabUtility.SaveAsPrefabAsset(made, modelPath);
            }
            finally
            {
                Object.DestroyImmediate(made);
            }
        }

        string prefabPath = AssetDatabase.GetAssetPath(building.Prefab);
        if (!prefabPath.StartsWith("Assets/_Project/"))
        {
            // The old Forge borrowed a prefab from a third-party pack. It stays as it is for old saves; the Smithy gets its own.
            GameObject created = BuildingPrefabBuilder.Create(building, model);
            Edit(building, so => so.FindProperty("prefab").objectReferenceValue = created);
            prefabPath = AssetDatabase.GetAssetPath(created);
        }

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
            // Fitted again to the new model by the prefab builder below.
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
        string iconPath = Icons + building.DisplayName.Replace("'", "") + ".png";
        Sprite icon = IconRenderer.RenderToSprite(model, iconPath);
        Edit(building, so => so.FindProperty("icon").objectReferenceValue = icon);
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
    private static Material Copper => Tint("Base Copper", new Color(0.62f, 0.34f, 0.17f));
    private static Material Moonsilver => Tint("Base Moonsilver", new Color(0.72f, 0.78f, 0.86f), new Color(0.15f, 0.2f, 0.28f));
    private static Material Ember => Tint("Base Ember", new Color(0.9f, 0.35f, 0.1f), new Color(3f, 1f, 0.2f));
    private static Material Cloth => Tint("Base Cloth", new Color(0.46f, 0.14f, 0.1f));
    private static Material TrollRock => Tint("Base Trollstone", new Color(0.2f, 0.27f, 0.2f));

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

    /// <summary>Posts, walls on three sides (the front, +Z, is left open when asked) and a pitched roof.</summary>
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

    private static void Footing(Transform parent, float width, float depth, Material material)
    {
        Box(parent, new Vector3(0f, 0.12f, 0f), new Vector3(width + 0.4f, 0.24f, depth + 0.4f), material);
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

    private static void WorkshopModel(Transform root)
    {
        const float width = 6f;
        const float depth = 4.6f;
        Box(root, new Vector3(0f, 0.06f, 0f), new Vector3(width + 0.2f, 0.12f, depth + 0.2f), Plank);
        House(root, width, depth, 2.5f, 1.3f, Plank, Thatch, true);
        Box(root, new Vector3(-1f, 0.55f, -1.1f), new Vector3(2.8f, 0.14f, 0.9f), Plank);
        Box(root, new Vector3(-2.2f, 0.27f, -1.1f), new Vector3(0.18f, 0.55f, 0.7f), Timber);
        Box(root, new Vector3(0.2f, 0.27f, -1.1f), new Vector3(0.18f, 0.55f, 0.7f), Timber);
        Box(root, new Vector3(1.9f, 0.42f, -1.2f), new Vector3(0.9f, 0.7f, 0.9f), Timber, new Vector3(0f, 18f, 0f));
        Box(root, new Vector3(-1f, 0.72f, -1.2f), new Vector3(0.5f, 0.18f, 0.2f), Rock);

        Transform two = Tier(root, 2);
        Footing(two, width, depth, Rock);
        Box(two, new Vector3(0f, 1.7f, -depth * 0.5f + 0.14f), new Vector3(2.8f, 0.8f, 0.05f), Timber);
        for (int i = 0; i < 4; i++)
        {
            Box(two, new Vector3(-1f + i * 0.65f, 1.65f, -depth * 0.5f + 0.2f), new Vector3(0.08f, 0.62f, 0.06f), Iron);
        }

        Transform three = Tier(root, 3);
        Box(three, new Vector3(-width * 0.5f + 0.25f, 0.75f, 1.2f), new Vector3(0.5f, 1.5f, 1.6f), Plank);
        foreach (float z in new[] { -depth * 0.25f, depth * 0.25f })
        {
            Box(three, new Vector3(width * 0.5f + 0.09f, 1.25f, z), new Vector3(0.05f, 2.4f, 0.2f), Iron);
        }

        Lantern(three, new Vector3(width * 0.5f - 0.3f, 2.1f, depth * 0.5f));
        Lantern(three, new Vector3(-width * 0.5f + 0.3f, 2.1f, depth * 0.5f));

        Transform four = Tier(root, 4);
        Box(four, new Vector3(0f, 2.5f + 1.3f + 0.16f, 0f), new Vector3(0.36f, 0.1f, depth + 0.75f), Moonsilver);
        Box(four, new Vector3(1.4f, 0.9f, -1.2f), new Vector3(0.5f, 0.3f, 0.5f), Moonsilver);

        Transform five = Tier(root, 5);
        Banner(five, new Vector3(-width * 0.5f - 0.05f, 0f, depth * 0.5f + 0.05f), Cloth);
        Box(five, new Vector3(0f, 2.5f + 1.3f + 0.55f, depth * 0.5f + 0.1f), new Vector3(0.5f, 0.8f, 0.2f), TrollRock);
    }

    private static void LodgeModel(Transform root)
    {
        const float width = 6f;
        const float depth = 4.4f;
        Box(root, new Vector3(0f, 0.04f, 0f), new Vector3(width + 0.2f, 0.08f, depth + 0.2f), Plank);
        float x = width * 0.5f;
        float z = depth * 0.5f;
        foreach (Vector3 corner in new[] { new Vector3(-x, 0f, -z), new Vector3(x, 0f, -z), new Vector3(-x, 0f, z), new Vector3(x, 0f, z), new Vector3(0f, 0f, -z), new Vector3(0f, 0f, z) })
        {
            Box(root, corner + Vector3.up * 1.2f, new Vector3(0.3f, 2.4f, 0.3f), Timber);
        }

        Box(root, new Vector3(0f, 1.2f, -z), new Vector3(width, 2.4f, 0.14f), Plank);
        Box(root, new Vector3(0f, 2.9f, 0f), new Vector3(width + 0.7f, 0.14f, depth + 0.8f), Shingle, new Vector3(-14f, 0f, 0f));
        Box(root, new Vector3(0f, 2.4f, z), new Vector3(width, 0.26f, 0.22f), Timber);

        // The sawhorse, a log on it and the first stack of planks.
        Box(root, new Vector3(-0.6f, 0.5f, 0.2f), new Vector3(2.2f, 0.18f, 0.5f), Timber);
        foreach (float px in new[] { -1.4f, 0.2f })
        {
            Box(root, new Vector3(px, 0.26f, 0.2f), new Vector3(0.16f, 0.5f, 0.8f), Timber);
        }

        Box(root, new Vector3(-0.6f, 0.8f, 0.2f), new Vector3(0.42f, 1.1f, 0.42f), Plank, new Vector3(0f, 0f, 90f), PrimitiveType.Cylinder);
        for (int i = 0; i < 3; i++)
        {
            Box(root, new Vector3(1.9f, 0.14f + i * 0.14f, -1f), new Vector3(1.5f, 0.1f, 0.9f), Plank);
        }

        Transform two = Tier(root, 2);
        for (int row = 0; row < 3; row++)
        {
            for (int i = 0; i < 3 - row; i++)
            {
                Box(two, new Vector3(-2.2f + i * 0.46f + row * 0.23f, 0.26f + row * 0.4f, -1.2f), new Vector3(0.42f, 0.8f, 0.42f), Timber, new Vector3(90f, 0f, 0f), PrimitiveType.Cylinder);
            }
        }

        Transform three = Tier(root, 3);
        Box(three, new Vector3(0.5f, 0.55f, 0.2f), new Vector3(1f, 0.08f, 1f), Iron, new Vector3(90f, 0f, 0f), PrimitiveType.Cylinder);
        Box(three, new Vector3(0.5f, 0.1f, 0.8f), new Vector3(0.7f, 0.1f, 0.3f), Timber);
        Lantern(three, new Vector3(x - 0.1f, 2.05f, z));

        Transform four = Tier(root, 4);
        Box(four, new Vector3(x + 0.15f, 1.1f, 0.2f), new Vector3(1.9f, 0.14f, 1.9f), Timber, new Vector3(0f, 0f, 90f), PrimitiveType.Cylinder);
        Box(four, new Vector3(x + 0.15f, 1.1f, 0.2f), new Vector3(0.3f, 0.22f, 0.3f), Moonsilver, new Vector3(0f, 0f, 90f), PrimitiveType.Cylinder);

        Transform five = Tier(root, 5);
        Banner(five, new Vector3(-x - 0.05f, 0f, z + 0.05f), Cloth);
        Box(five, new Vector3(1.9f, 0.7f, 1.2f), new Vector3(0.7f, 1.3f, 0.7f), TrollRock);
    }

    private static void SmithyModel(Transform root)
    {
        const float width = 6f;
        const float depth = 4.8f;
        Box(root, new Vector3(0f, 0.06f, 0f), new Vector3(width + 0.2f, 0.12f, depth + 0.2f), Rock);
        House(root, width, depth, 2.5f, 1.3f, Rock, Shingle, true);

        // The hearth and chimney against the back wall, and an anvil on a stump.
        Box(root, new Vector3(-1.6f, 0.55f, -1.6f), new Vector3(1.8f, 1.1f, 1.2f), Rock);
        Box(root, new Vector3(-1.6f, 1.12f, -1.5f), new Vector3(1.2f, 0.06f, 0.8f), Ember);
        Box(root, new Vector3(-1.6f, 2.6f, -1.9f), new Vector3(1.1f, 3.6f, 0.9f), Rock);
        Box(root, new Vector3(0.9f, 0.3f, 0.2f), new Vector3(0.6f, 0.3f, 0.6f), Timber, default, PrimitiveType.Cylinder);
        Box(root, new Vector3(0.9f, 0.7f, 0.2f), new Vector3(0.75f, 0.22f, 0.32f), Iron);

        Transform two = Tier(root, 2);
        Box(two, new Vector3(0.9f, 0.86f, 0.2f), new Vector3(0.3f, 0.08f, 0.18f), Copper);
        Box(two, new Vector3(-0.3f, 0.55f, -1.5f), new Vector3(0.7f, 0.5f, 0.9f), Plank, new Vector3(0f, 0f, 16f));
        Box(two, new Vector3(2.2f, 0.28f, -1.5f), new Vector3(0.9f, 0.45f, 0.9f), Copper);

        Transform three = Tier(root, 3);
        Box(three, new Vector3(2.1f, 0.3f, 1.2f), new Vector3(1.3f, 0.5f, 0.7f), Timber);
        Box(three, new Vector3(2.1f, 0.56f, 1.2f), new Vector3(1.1f, 0.04f, 0.5f), Tint("Base Water", new Color(0.1f, 0.18f, 0.22f)));
        Box(three, new Vector3(-1.6f, 4.45f, -1.9f), new Vector3(1.25f, 0.2f, 1.05f), Iron);
        Lantern(three, new Vector3(width * 0.5f - 0.3f, 2.1f, depth * 0.5f));

        Transform four = Tier(root, 4);
        Box(four, new Vector3(-1.6f, 1.16f, -1.5f), new Vector3(1.15f, 0.04f, 0.75f), Moonsilver);
        Box(four, new Vector3(0f, 2.5f + 1.3f + 0.16f, 0f), new Vector3(0.36f, 0.1f, depth + 0.75f), Moonsilver);

        Transform five = Tier(root, 5);
        Box(five, new Vector3(0.9f, 0.7f, 0.2f), new Vector3(0.95f, 0.34f, 0.5f), TrollRock);
        Banner(five, new Vector3(-width * 0.5f - 0.05f, 0f, depth * 0.5f + 0.05f), Cloth);
    }

    private static void Edit(Object target, Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
