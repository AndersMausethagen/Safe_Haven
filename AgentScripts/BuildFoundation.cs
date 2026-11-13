using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// The foundation of tiers I and II (`unity command script run --file AgentScripts/BuildFoundation.cs --entry BuildFoundation.Apply`),
/// run after BuildBaseTiers: what the Workshop makes on day one, food and game at home, what a raid really holds, caches that keep to
/// the ladder, quests that match the costs, and the people the first buildings are for: Tor and Bjarne, sought once they have
/// somewhere to work, and working on their own. The numbers were measured from a launched raid; see Docs/BaseProgression.md.
/// Safe to run again.
/// </summary>
public static class BuildFoundation
{
    private const string HomeScene = "Assets/_Project/Scenes/SafeHaven.unity";
    private const string WeaponItems = "Assets/_Project/Data/Items/Weapons/";
    private const string WeaponData = "Assets/_Project/Data/Combat/Weapons/";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes/";
    private const string GearIcons = "Assets/_Project/Art/Items/Gear/Icons/";
    private const string GearModels = "Assets/_Project/Art/Items/Gear/Models/";
    private const string GearTints = "Assets/_Project/Art/Items/Gear/Materials/";
    private const string Rumours = "Assets/_Project/Data/Raids/Rumours/";
    private const string Sites = "Assets/_Project/Prefabs/Raids/Events/Sites/";
    private static readonly Color Wood = new Color(0.42f, 0.28f, 0.15f);

    public static string Apply()
    {
        var log = new List<string>();
        log.Add(DayOneGear());
        log.Add(RaidResources());
        log.Add(Caches());
        log.Add(Boars());
        log.Add(Quests());
        log.Add(Workers());
        log.Add(Bjarne());
        AssetDatabase.SaveAssets();
        log.Add(Home());
        return string.Join("\n", log);
    }

    // ----- Workshop I: things to make from what lies around at home -----

    private static string DayOneGear()
    {
        float axe = Load<WeaponDefinition>(WeaponData + "WoodsmansAxe.asset").Damage;
        GameObject hammer = FindPrefab("Human_Hammer");
        ItemDefinition club = Weapon("KnottedClub", "Knotted Club", "A length of oak with the knots left on. It stuns more than it cuts.", "CopperHandAxe", "CopperHandAxe", axe, hammer, 4, 1.2f);
        ItemDefinition spear = Weapon("FireHardenedSpear", "Fire-Hardened Spear", "A straight pole, its point hardened in the fire. It keeps a wolf at arm's length.", "CopperSpear", "CopperSpear", axe * 0.85f, null, 5, 1.5f);
        ItemDefinition shield = Weapon("WickerShield", "Wicker Shield", "Woven withies on a wooden frame. Better than an open hand.", "RoundShield", "RoundShield", 3f, null, 3, 1.2f);
        Recipe("KnottedClub", club, 1, 10f, ("OakWood", 5), ("NettleFiber", 2));
        Recipe("FireHardenedSpear", spear, 1, 10f, ("OakWood", 4), ("Sticks", 3), ("NettleCord", 1));
        Recipe("WickerShield", shield, 1, 12f, ("OakWood", 3), ("Sticks", 6), ("NettleFiber", 3));

        const string bandagePath = "Assets/_Project/Data/Items/Bandage.asset";
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(bandagePath) == null)
        {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(Item("HealingSalve")), bandagePath);
        }

        var bandage = Load<ItemDefinition>(bandagePath);
        string iconPath = GearIcons + "Bandage.png";
        Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath) ?? PlaceholderIconPainter.Paint(IconShape.Cloth, new Color(0.82f, 0.78f, 0.66f), new Color(0.55f, 0.2f, 0.16f), iconPath);
        Edit(bandage, so =>
        {
            so.FindProperty("id").stringValue = "bandage";
            so.FindProperty("displayName").stringValue = "Bandage";
            so.FindProperty("description").stringValue = "Nettle cloth, boiled clean. It stops the bleeding and not much more.";
            so.FindProperty("healAmount").floatValue = 15f;
            so.FindProperty("value").intValue = 1;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("prefab").objectReferenceValue = null;
        });
        Recipe("Bandage", bandage, 2, 6f, ("NettleFiber", 4));
        Register(new[] { club, spear, shield, bandage }, new[] { "KnottedClub", "FireHardenedSpear", "WickerShield", "Bandage" });
        return "workshop: club, spear, wicker shield, bandage";
    }

    private static ItemDefinition Weapon(string file, string label, string text, string itemTemplate, string weaponTemplate, float damage, GameObject modelSource, int value, float weight)
    {
        string weaponPath = WeaponData + file + ".asset";
        if (AssetDatabase.LoadAssetAtPath<WeaponDefinition>(weaponPath) == null)
        {
            AssetDatabase.CopyAsset(WeaponData + weaponTemplate + ".asset", weaponPath);
        }

        WeaponDefinition weapon = Load<WeaponDefinition>(weaponPath);
        Edit(weapon, so =>
        {
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("damage").floatValue = damage;
        });

        string itemPath = WeaponItems + file + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(itemPath) == null)
        {
            AssetDatabase.CopyAsset(WeaponItems + itemTemplate + ".asset", itemPath);
        }

        var template = Load<ItemDefinition>(WeaponItems + itemTemplate + ".asset");
        GameObject model = Tinted(file, modelSource != null ? modelSource : template.HeldModel);
        string iconPath = GearIcons + file + ".png";
        Sprite icon = IconRenderer.RenderToSprite(model, iconPath);
        ItemDefinition item = Load<ItemDefinition>(itemPath);
        Edit(item, so =>
        {
            so.FindProperty("id").stringValue = file.ToLowerInvariant();
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = text;
            so.FindProperty("tier").intValue = 1;
            so.FindProperty("value").intValue = value;
            so.FindProperty("weight").floatValue = weight;
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("weapon").objectReferenceValue = weapon;
            so.FindProperty("heldModel").objectReferenceValue = model;
            so.FindProperty("prefab").objectReferenceValue = null;
        });
        return item;
    }

    private static GameObject Tinted(string file, GameObject source)
    {
        string path = GearModels + file + " Model.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
        {
            return existing;
        }

        string materialPath = GearTints + file + ".mat";
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", Wood);
        material.SetFloat("_Smoothness", 0.2f);
        AssetDatabase.CreateAsset(material, materialPath);
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

    private static void Recipe(string file, ItemDefinition output, int amount, float seconds, params (string item, int amount)[] cost)
    {
        string path = Recipes + file + ".asset";
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
            so.FindProperty("outputAmount").intValue = amount;
            so.FindProperty("station").intValue = (int)CraftingStationType.Workbench;
            so.FindProperty("craftSeconds").floatValue = seconds;
            so.FindProperty("knownFromStart").boolValue = true;
            so.FindProperty("sortOrder").intValue = 105;
            SerializedProperty items = so.FindProperty("itemCost");
            items.arraySize = cost.Length;
            for (int i = 0; i < cost.Length; i++)
            {
                items.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue = Item(cost[i].item);
                items.GetArrayElementAtIndex(i).FindPropertyRelative("amount").intValue = cost[i].amount;
            }
        });
    }

    private static void Register(ItemDefinition[] items, string[] recipes)
    {
        var book = Load<RecipeBook>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:RecipeBook")[0]));
        Edit(book, so =>
        {
            foreach (string recipe in recipes)
            {
                AddOnce(so.FindProperty("recipes"), Load<RecipeDefinition>(Recipes + recipe + ".asset"));
            }
        });
        var database = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        Edit(database, so =>
        {
            foreach (ItemDefinition item in items)
            {
                AddOnce(so.FindProperty("items"), item);
            }
        });
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

    // ----- What a raid holds. Weights are close to node counts: the list places about 110. -----

    private static string RaidResources()
    {
        (string prefab, float weight, int max)[] wanted =
        {
            ("OakWoodNode", 30f, 0), ("CopperOreNode", 10f, 0), ("IronOreNode", 6f, 0), ("GraniteNode", 4f, 0), ("HardwoodNode", 8f, 0), ("FlaxNode", 6f, 0),
            ("MoonsilverOreNode", 2f, 3), ("DeepstoneNode", 1f, 2), ("ElderwoodNode", 1f, 2), ("HuldraSilkNode", 1f, 2),
            ("SticksNode", 6f, 0), ("SmallRocksNode", 6f, 0), ("BerriesNode", 4f, 0), ("NettleFiberNode", 8f, 0),
            ("HealingHerbNode", 7f, 0), ("LifeMoteNode", 4f, 0), ("LifeOrbNode", 8f, 0), ("ChanterellesNode", 6f, 0), ("WildTurnipsNode", 4f, 0), ("WildBarleyNode", 4f, 0), ("CloudberryPatchNode", 2f, 0),
        };
        Edit(Raid(), so =>
        {
            SerializedProperty lists = so.FindProperty("spawnLists");
            for (int i = 0; i < lists.arraySize; i++)
            {
                SerializedProperty list = lists.GetArrayElementAtIndex(i);
                if (list.FindPropertyRelative("name").stringValue != "Resources")
                {
                    continue;
                }

                list.FindPropertyRelative("count").vector2IntValue = new Vector2Int(124, 140);
                SerializedProperty entries = list.FindPropertyRelative("entries");
                entries.arraySize = wanted.Length;
                for (int e = 0; e < wanted.Length; e++)
                {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(e);
                    entry.FindPropertyRelative("prefab").objectReferenceValue = FindPrefab(wanted[e].prefab);
                    entry.FindPropertyRelative("weight").floatValue = wanted[e].weight;
                    entry.FindPropertyRelative("maxPerGroup").intValue = wanted[e].max;
                    entry.FindPropertyRelative("minPerGroup").intValue = 0;
                }
            }
        });
        return "raid: about 10 copper, 6 iron, 8 hardwood, 6 flax, 4 granite nodes; moonsilver, deepstone, elderwood and silk are rare";
    }

    // Copper and iron from caches help; they shouldn't outrun the ore.
    private static string Caches()
    {
        string path = AssetDatabase.GetAssetPath(FindPrefab("SupplyCache"));
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            LootCache cache = root.GetComponentInChildren<LootCache>(true);
            var so = new SerializedObject(cache);
            SerializedProperty entries = so.FindProperty("drops").FindPropertyRelative("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                Object item = entry.FindPropertyRelative("item").objectReferenceValue;
                if (item == Item("CopperIngot"))
                {
                    entry.FindPropertyRelative("maxQuantity").intValue = 2;
                    entry.FindPropertyRelative("probability").floatValue = 0.3f;
                }
                else if (item == Item("IronIngot"))
                {
                    entry.FindPropertyRelative("maxQuantity").intValue = 2;
                    entry.FindPropertyRelative("probability").floatValue = 0.2f;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return "caches: less metal; nothing a tier beyond the Smithy's next";
    }

    private static string Boars()
    {
        MonsterDefinition boar = Find<MonsterDefinition>("BlightBoar");
        Edit(boar, so =>
        {
            SerializedProperty entries = so.FindProperty("drops").FindPropertyRelative("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == Item("Bone"))
                {
                    return;
                }
            }

            entries.arraySize++;
            SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = Item("Bone");
            entry.FindPropertyRelative("resource").objectReferenceValue = null;
            entry.FindPropertyRelative("minQuantity").intValue = 1;
            entry.FindPropertyRelative("maxQuantity").intValue = 2;
            entry.FindPropertyRelative("probability").floatValue = 0.6f;
        });
        return "boars: drop bone too";
    }

    // ----- Quests match what the buildings cost -----

    private static string Quests()
    {
        Edit(Find<QuestDefinition>("SettingIn_", "SettlingIn"), so =>
        {
            SerializedProperty objectives = so.FindProperty("objectives");
            objectives.arraySize = 3;
            SetGather(objectives.GetArrayElementAtIndex(0), "OakWood", 12);
            SetGather(objectives.GetArrayElementAtIndex(1), "GreyStone", 6);
            SetGather(objectives.GetArrayElementAtIndex(2), "Sticks", 6);
            so.FindProperty("description").stringValue = "A hearth starts small. Craft a stone hatchet and pickaxe from your bag (B), then gather what the clearing offers: enough for a fire and a workshop.";
        });
        return "quests: Gifts of the Clearing gathers what a workshop costs (the first weapon has its own quest: BuildOpeningOrder)";
    }

    private static void SetGather(SerializedProperty objective, string item, int amount)
    {
        objective.FindPropertyRelative("description").stringValue = "";
        objective.FindPropertyRelative("amount").intValue = amount;
        objective.FindPropertyRelative("item").objectReferenceValue = Item(item);
        objective.FindPropertyRelative("resource").objectReferenceValue = null;
        objective.FindPropertyRelative("countAlreadyOwned").boolValue = true;
    }

    // ----- People at work -----

    private static string Workers()
    {
        SetJob("Carpenter", "Runs the Woodcutter's Lodge: keeps logs turning into planks while you are away, the work goes faster, and mending buildings after a breach costs half.",
            ("OakPlank", 40, 10), ("HardwoodPlank", 20, 4));
        SetJob("Smith", "Runs the Smithy: keeps ore turning into ingots while you are away, the work goes faster, and repairs to gear cost half.",
            ("IronIngot", 20, 5), ("CopperIngot", 30, 5));
        return "workers: Tor keeps planks coming, Bjarne ingots, from what is in the stash";
    }

    private static void SetJob(string job, string description, params (string output, int stock, int keep)[] orders)
    {
        Edit(Find<JobDefinition>(job), so =>
        {
            so.FindProperty("description").stringValue = description;
            SerializedProperty list = so.FindProperty("workOrders");
            list.arraySize = orders.Length;
            for (int i = 0; i < orders.Length; i++)
            {
                ItemDefinition output = Item(orders[i].output);
                RecipeDefinition recipe = AssetDatabase.FindAssets("t:RecipeDefinition").Select(guid => Load<RecipeDefinition>(AssetDatabase.GUIDToAssetPath(guid))).First(one => one.Output == output);
                SerializedProperty order = list.GetArrayElementAtIndex(i);
                order.FindPropertyRelative("recipe").objectReferenceValue = recipe;
                order.FindPropertyRelative("stockTarget").intValue = orders[i].stock;
                order.FindPropertyRelative("keepInput").intValue = orders[i].keep;
            }
        });
    }

    // Bjarne's rumour and rescue. Who is sought when, and what each opens, is BuildRescueOrder's (Docs/RescueOrder.md): a
    // rescue opens the workplace now, so neither rumour waits for a building.
    private static string Bjarne()
    {
        Edit(Load<RaidRumourDefinition>(Rumours + "Tor.asset"), so =>
        {
            so.FindProperty("needsBuilding").objectReferenceValue = null;
            so.FindProperty("urgent").boolValue = true;
        });

        string eventPath = Rumours + "Events/BjarnesForge.asset";
        if (AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(eventPath) == null)
        {
            AssetDatabase.CopyAsset(Rumours + "Events/TorsShelter.asset", eventPath);
        }

        ResidentDefinition bjarne = Find<ResidentDefinition>("Bjarne");
        GameObject site = ForgeSite();
        var raidEvent = Load<RaidEventDefinition>(eventPath);
        Edit(raidEvent, so =>
        {
            so.FindProperty("id").stringValue = "rumour.bjarnesforge";
            so.FindProperty("displayName").stringValue = "A Cold Forge";
            so.FindProperty("siteText").stringValue = "A forge built from what the woods left lying. A big man sits against it with one arm bound to his chest, hammer across his knees.";
            so.FindProperty("sitePrefab").objectReferenceValue = site;
            SerializedProperty choice = so.FindProperty("choices").GetArrayElementAtIndex(0);
            choice.FindPropertyRelative("label").stringValue = "Help him up";
            choice.FindPropertyRelative("detail").stringValue = "45 seconds, and he won't leave the hammer behind.";
            choice.FindPropertyRelative("result").stringValue = "\"You took your time,\" he says, and leans on you harder than he means to.";
            SerializedProperty outcomes = choice.FindPropertyRelative("outcomes");
            for (int i = 0; i < outcomes.arraySize; i++)
            {
                SerializedProperty resident = outcomes.GetArrayElementAtIndex(i).FindPropertyRelative("resident");
                if (resident != null)
                {
                    resident.objectReferenceValue = bjarne;
                }
            }
        });

        string rumourPath = Rumours + "Bjarne.asset";
        if (AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>(rumourPath) == null)
        {
            AssetDatabase.CopyAsset(Rumours + "Tor.asset", rumourPath);
        }

        var rumour = Load<RaidRumourDefinition>(rumourPath);
        Edit(rumour, so =>
        {
            so.FindProperty("id").stringValue = "rumour.bjarne";
            so.FindProperty("briefing").stringValue = "A hammer rings in the deep woods at dusk, slow and unevenly, as if the smith is one-armed.";
            so.FindProperty("site").objectReferenceValue = raidEvent;
            so.FindProperty("seeks").objectReferenceValue = bjarne;
            so.FindProperty("needsBuilding").objectReferenceValue = null;
            so.FindProperty("urgent").boolValue = true;
        });

        Edit(Raid(), so =>
        {
            SerializedProperty systems = so.FindProperty("systems");
            for (int i = 0; i < systems.arraySize; i++)
            {
                SerializedProperty system = systems.GetArrayElementAtIndex(i);
                if (system.managedReferenceValue is RaidRumoursSystem)
                {
                    AddOnce(system.FindPropertyRelative("pool"), rumour);
                }
            }
        });
        return "bjarne: rumour and rescue added (the order is BuildRescueOrder's)";
    }

    private static GameObject ForgeSite()
    {
        // Tor's shelter with another model: an event site also needs its collider, beacon, site and Interactable.
        string path = Sites + "Bjarnes Forge.prefab";
        // Kit-built by Tools/Blender/raid_sites.py (BuildStructureModels.RaidSites): kept as it is.
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Raids/Sites/Bjarnes Forge/Bjarnes Forge.fbx") != null)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(Load<RaidEventDefinition>(Rumours + "Events/TorsShelter.asset").SitePrefab), path);
        Material stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Haven/Base Stone.mat");
        Material iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Haven/Base Iron.mat");
        Material timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Haven/Base Timber.mat");
        Material ember = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Haven/Base Ember.mat");
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform model = root.transform.Find("Model");
            for (int i = model.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(model.GetChild(i).gameObject);
            }

            Piece(model, new Vector3(0f, 0.5f, -0.8f), new Vector3(1.6f, 1f, 1.2f), stone);
            Piece(model, new Vector3(0f, 1.03f, -0.8f), new Vector3(1f, 0.06f, 0.7f), ember);
            Piece(model, new Vector3(0f, 1.7f, -1.15f), new Vector3(0.9f, 2.4f, 0.5f), stone);
            Piece(model, new Vector3(1.3f, 0.28f, 0.4f), new Vector3(0.6f, 0.56f, 0.6f), timber);
            Piece(model, new Vector3(1.3f, 0.66f, 0.4f), new Vector3(0.7f, 0.2f, 0.3f), iron);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static void Piece(Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(piece.GetComponent<Collider>());
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = position;
        piece.transform.localScale = size;
        piece.GetComponent<Renderer>().sharedMaterial = material;
    }

    // ----- At home: forage beyond the ward, and game beyond the light -----

    private static string Home()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != HomeScene)
        {
            return "home: open SafeHaven and run again";
        }

        if (scene.isDirty)
        {
            return "home: the open scene has unsaved changes, nothing placed";
        }

        var root = GameObject.Find("Wild Forage");
        if (root != null)
        {
            Object.DestroyImmediate(root);
        }

        root = new GameObject("Wild Forage");
        Vector3 centre = Object.FindAnyObjectByType<HomePortal>().transform.position;
        var random = new System.Random(4711);
        int placed = 0;
        foreach ((string prefab, int count) in new[] { ("WildBarleyNode", 5), ("WildTurnipsNode", 4), ("ChanterellesNode", 5) })
        {
            GameObject source = FindPrefab(prefab);
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 30; attempt++)
                {
                    double angle = random.NextDouble() * Math.PI * 2.0;
                    float radius = 19f + (float)random.NextDouble() * 15f;
                    Vector3 wanted = centre + new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)) * radius;
                    if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                    {
                        continue;
                    }

                    var node = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
                    node.transform.position = hit.position;
                    node.transform.rotation = Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);
                    placed++;
                    break;
                }
            }
        }

        var hunting = GameObject.Find("Home Hunting");
        if (hunting == null)
        {
            hunting = new GameObject("Home Hunting");
        }

        var component = hunting.GetComponent<HomeHunting>() ?? hunting.AddComponent<HomeHunting>();
        Edit(component, so =>
        {
            // Game, not the blight's boars: neutral and timid (AgentScripts/BuildGame.cs), so what is seen by day can be hunted.
            SerializedProperty game = so.FindProperty("game");
            string[] kinds = { "Deer", "Deer", "Stag", "WildBoar" };
            game.arraySize = kinds.Length;
            for (int i = 0; i < kinds.Length; i++)
            {
                game.GetArrayElementAtIndex(i).objectReferenceValue = FindPrefab(kinds[i]);
            }
        });
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"home: {placed} forage nodes beyond the ward; boars by day beyond the light";
    }

    // ----- Helpers -----

    private static RaidDefinition Raid() => Find<RaidDefinition>("MistyWoods");

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            throw new InvalidOperationException("Missing " + path);
        }

        return asset;
    }

    private static ItemDefinition Item(string name) => Find<ItemDefinition>(name);

    private static T Find<T>(params string[] names) where T : Object
    {
        foreach (string name in names)
        {
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name} {name}"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.name == name)
                {
                    return asset;
                }
            }
        }

        throw new InvalidOperationException($"No {typeof(T).Name} named {string.Join("/", names)}");
    }

    private static GameObject FindPrefab(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:Prefab {name}"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
            {
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }

        throw new InvalidOperationException("No prefab " + name);
    }

    private static void Edit(Object target, Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
