#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the Haven's Reach (Docs/Territory.md): the Pact Rune, the boundary stone, Resources/TerritorySettings with what each
/// region is like, and the raid side: the Rot's Heart rumour and its event (only while the forest is pushing), added to every
/// raid's rumours, and the rune in the supply caches. Run with `unity command script run --file AgentScripts/BuildTerritory.cs
/// --entry BuildTerritory.Apply` after BuildHavenAtlas. Re-running rewrites the regions' words and the parts it made; the
/// settings' numbers are left as tuned.
/// </summary>
public static class BuildTerritory
{
    private const string RunePath = "Assets/_Project/Data/Items/PactRune.asset";
    private const string StonePath = "Assets/_Project/Prefabs/World/Boundary Stone.prefab";
    private const string SettingsPath = "Assets/_Project/Resources/TerritorySettings.asset";
    private const string EventPath = "Assets/_Project/Data/Raids/Rumours/Events/RotsHeart.asset";
    private const string RumourPath = "Assets/_Project/Data/Raids/Rumours/RotsHeart.asset";
    private const float CacheChance = 0.05f;

    // ----- What each region is like: name, words, danger, what it gives, its task, its tithe when Secure -----

    private static readonly (string region, string description, RegionDanger danger, string gives, RegionTaskKind task, int count, (string item, int amount)[] tithe)[] Regions =
    {
        ("Ravenwood", "Old pines north of the meadows, where the ravens sit and watch the road. The tarn lies at its edge.",
            RegionDanger.Wary, "Softwood, berries, game", RegionTaskKind.Hunt, 6, new[] { ("OakWood", 4), ("Berries", 2) }),
        ("Eastwood", "Open woods between the lake and the hills, crossed by trappers' paths. The old quarries are here.",
            RegionDanger.Wary, "Softwood, stone, hides", RegionTaskKind.Hunt, 6, new[] { ("OakWood", 3), ("GreyStone", 3) }),
        ("The Hollow Pines", "Tall, dead-straight pines south of the Haven with nothing growing under them. Sound carries strangely here.",
            RegionDanger.Dangerous, "Hardwood, mushrooms, iron", RegionTaskKind.Gather, 10, new[] { ("Hardwood", 2), ("Chanterelle", 2) }),
        ("The Wolfwood", "Where the cursed packs den: the Wolf Den is here, and the Old Barrow. Few go in, and fewer come out the same.",
            RegionDanger.Deadly, "Fangs, thick hides, grave iron", RegionTaskKind.Hunt, 8, new[] { ("WolfFang", 1), ("ThickHide", 1) }),
        ("Westwood", "Quiet cedar woods west of the meadows, with the old watchtowers on their ridges and the mere among them.",
            RegionDanger.Wary, "Hardwood, healing herbs", RegionTaskKind.Gather, 10, new[] { ("Hardwood", 2), ("HealingHerb", 2) }),
        ("The Grey Pines", "A thin strip of pines under Greyfell's cliffs, littered with fallen stone and the stumps of old cuttings.",
            RegionDanger.Wary, "Softwood, stone", RegionTaskKind.Gather, 8, new[] { ("GreyStone", 3), ("OakWood", 2) }),
        ("Greyfell", "The mountain: bare benches of rock climbing to the highest ground in the land. Ore in the cliffs, and the wind.",
            RegionDanger.Dangerous, "Stone, granite, copper and iron ore", RegionTaskKind.Gather, 10, new[] { ("GreyStone", 4), ("CopperOre", 2) }),
        ("The Drowning Fen", "The bog: black pools between hummocks, and a causeway to the Drowned Stones at its heart. It keeps what it takes.",
            RegionDanger.Deadly, "Bog venom, herbs, cloudberries", RegionTaskKind.Hunt, 6, new[] { ("BogVenom", 1), ("Cloudberries", 2) }),
        ("Stillwater", "The great lake below the Haven, with the Island Stones out on the water and the landing on its shore.",
            RegionDanger.Calm, "Fish, flax", RegionTaskKind.Fish, 4, new[] { ("Perch", 2), ("Flax", 2) }),
        ("Hightarn", "A cold tarn in the northern hills, its water black and very still. The fish there are old and wary.",
            RegionDanger.Wary, "Fish", RegionTaskKind.Fish, 3, new[] { ("ArcticChar", 1) }),
        ("Alder Mere", "A reedy mere in the south-west woods, ringed with alders and quiet as a held breath.",
            RegionDanger.Calm, "Fish, flax, reeds", RegionTaskKind.Fish, 3, new[] { ("BrookTrout", 1), ("Flax", 2) }),
    };

    public static string Apply()
    {
        var log = new List<string>();
        ItemDefinition rune = Rune(log);
        GameObject stone = Stone(log);
        Settings(stone, rune, log);
        RaidEventDefinition heart = HeartEvent(rune, log);
        RaidRumourDefinition rumour = Rumour(heart, log);
        AddToRaids(rumour, log);
        AddToCaches(rune, log);
        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            database.CollectAll();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);
        }

        return "Territory: " + string.Join("; ", log);
    }

    private static ItemDefinition Rune(List<string> log)
    {
        if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(RunePath) == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Data/Items/RunestoneFragment.asset", RunePath);
        }

        var rune = AssetDatabase.LoadAssetAtPath<ItemDefinition>(RunePath);
        var so = new SerializedObject(rune);
        so.FindProperty("id").stringValue = "pact_rune";
        so.FindProperty("displayName").stringValue = "Pact Rune";
        so.FindProperty("description").stringValue =
            "A rune of the old pact, cut in grey stone and warm in the hand. Offered at a boundary stone, it claims a region for the Haven or holds it firmer.";
        so.FindProperty("rarity").intValue = 3;
        so.FindProperty("value").intValue = 90;
        so.FindProperty("maximumStack").intValue = 10;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(rune);
        log.Add("Pact Rune");
        return rune;
    }

    // The boundary stone: the runic ward totem's carved stone, with something to touch and a light for its runes.
    private static GameObject Stone(List<string> log)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Haven/Models/Runic Ward Totem Model.prefab");
        Directory.CreateDirectory(Path.GetDirectoryName(StonePath));
        var root = new GameObject("Boundary Stone");
        try
        {
            var body = (GameObject)PrefabUtility.InstantiatePrefab(model);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = Vector3.one * 1.15f;
            var hit = root.AddComponent<CapsuleCollider>();
            hit.center = new Vector3(0f, 1.2f, 0f);
            hit.height = 2.6f;
            hit.radius = 0.7f;
            var interactable = root.AddComponent<Interactable>();
            interactable.Configure(InteractionAnimation.GatherLow, 0.8f, 2.6f, false);
            root.AddComponent<BoundaryStone>();
            var lamp = new GameObject("Rune Light").AddComponent<Light>();
            lamp.transform.SetParent(root.transform, false);
            lamp.transform.localPosition = new Vector3(0f, 1.8f, 0.6f);
            lamp.type = LightType.Point;
            lamp.range = 9f;
            lamp.intensity = 1.4f;
            lamp.color = new Color(0.5f, 0.75f, 1f);
            lamp.shadows = LightShadows.None;
            lamp.enabled = false;
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, StonePath);
            log.Add("boundary stone");
            return saved;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Settings(GameObject stone, ItemDefinition rune, List<string> log)
    {
        var settings = AssetDatabase.LoadAssetAtPath<TerritorySettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<TerritorySettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        var regions = new List<TerritorySettings.RegionInfo>();
        WorldAtlas atlas = Resources.Load<WorldAtlas>("Atlases/SafeHaven");
        foreach (var r in Regions)
        {
            if (atlas != null && atlas.Regions.All(a => a.name != r.region))
            {
                log.Add($"({r.region} is not in the atlas)");
            }

            var info = new TerritorySettings.RegionInfo
            {
                region = r.region, description = r.description, danger = r.danger, gives = r.gives, task = r.task, taskCount = r.count,
                tithe = r.tithe.Select(t => new ItemAmount(Item(t.item), t.amount)).Where(t => t.Item != null).ToList(),
            };
            regions.Add(info);
        }

        if (atlas != null)
        {
            foreach (WorldAtlas.Region region in atlas.Regions.Where(a => a.stoneAt != Vector2.zero && regions.All(r => r.region != a.name)))
            {
                log.Add($"({region.name} has no words)");
            }
        }

        settings.Configure(stone, rune, regions);
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        log.Add($"{regions.Count} regions described");
    }

    // The rot's heart: what feeds the forest's push, burned for a rune and a weaker push. It stands at the blighted tree's site.
    private static RaidEventDefinition HeartEvent(ItemDefinition rune, List<string> log)
    {
        if (AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(EventPath) == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Data/Raids/Rumours/Events/BreachSource.asset", EventPath);
        }

        var heart = AssetDatabase.LoadAssetAtPath<RaidEventDefinition>(EventPath);
        var tree = AssetDatabase.LoadAssetAtPath<RaidEventDefinition>("Assets/_Project/Data/Raids/Events/CursedTree.asset");
        var so = new SerializedObject(heart);
        so.FindProperty("id").stringValue = "RotsHeart";
        so.FindProperty("displayName").stringValue = "The Rot's Heart";
        so.FindProperty("siteText").stringValue =
            "A knot of black wood as big as a man, half sunk in the ground, and it beats. Every vein of blight in this part of the woods runs to it.";
        so.FindProperty("sitePrefab").objectReferenceValue = new SerializedObject(tree).FindProperty("sitePrefab").objectReferenceValue;
        so.FindProperty("artwork").objectReferenceValue = null;
        SerializedProperty choices = so.FindProperty("choices");
        choices.ClearArray();
        choices.InsertArrayElementAtIndex(0);
        SerializedProperty burn = choices.GetArrayElementAtIndex(0);
        burn.FindPropertyRelative("label").stringValue = "Burn out the heart";
        burn.FindPropertyRelative("detail").stringValue = "Costs 20 seconds, and what guards it will wake. The forest's push falters, and the heart gives up a Pact Rune.";
        burn.FindPropertyRelative("result").stringValue = "It burns slowly, then all at once. Something in the stone at its core is warm and clean.";
        burn.FindPropertyRelative("cost").ClearArray();
        SerializedProperty outcomes = burn.FindPropertyRelative("outcomes");
        outcomes.ClearArray();
        Add(outcomes, new ChangeTimeOutcome(), e => e.FindPropertyRelative("seconds").floatValue = -20f);
        Add(outcomes, new SpawnCreaturesOutcome(), e =>
        {
            SerializedProperty prefabs = e.FindPropertyRelative("prefabs");
            prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemies/RootlingSwarm.prefab");
            e.FindPropertyRelative("count").intValue = 1;
            e.FindPropertyRelative("radius").floatValue = 6f;
            e.FindPropertyRelative("hunt").boolValue = true;
        });
        Add(outcomes, new WeakenIncursionOutcome());
        Add(outcomes, new GiveLootOutcome(), e =>
        {
            SerializedProperty entries = e.FindPropertyRelative("loot").FindPropertyRelative("entries");
            entries.ClearArray();
            entries.InsertArrayElementAtIndex(0);
            SerializedProperty entry = entries.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative("item").objectReferenceValue = rune;
            entry.FindPropertyRelative("resource").objectReferenceValue = null;
            entry.FindPropertyRelative("minQuantity").intValue = 1;
            entry.FindPropertyRelative("maxQuantity").intValue = 1;
            entry.FindPropertyRelative("probability").floatValue = 1f;
        });
        choices.InsertArrayElementAtIndex(1);
        SerializedProperty leave = choices.GetArrayElementAtIndex(1);
        leave.FindPropertyRelative("label").stringValue = "Leave it beating";
        leave.FindPropertyRelative("detail").stringValue = "The push on the Haven's land comes on as it was.";
        leave.FindPropertyRelative("result").stringValue = "You leave it. The beat follows you a long way.";
        leave.FindPropertyRelative("cost").ClearArray();
        leave.FindPropertyRelative("outcomes").ClearArray();
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(heart);
        log.Add("the rot's heart");
        return heart;
    }

    private static RaidRumourDefinition Rumour(RaidEventDefinition heart, List<string> log)
    {
        if (AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>(RumourPath) == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Data/Raids/Rumours/BreachSource.asset", RumourPath);
        }

        var rumour = AssetDatabase.LoadAssetAtPath<RaidRumourDefinition>(RumourPath);
        var so = new SerializedObject(rumour);
        so.FindProperty("id").stringValue = "rumour.rotsheart";
        so.FindProperty("briefing").stringValue =
            "The forest is gathering against {0}. Something out here feeds it: a heart of black wood, Runa says. Burn it out and the push on {0} falters.";
        so.FindProperty("site").objectReferenceValue = heart;
        so.FindProperty("weight").floatValue = 3f;
        so.FindProperty("breachComing").boolValue = false;
        so.FindProperty("forestPushing").boolValue = true;
        so.FindProperty("urgent").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(rumour);
        log.Add("its rumour");
        return rumour;
    }

    private static void AddToRaids(RaidRumourDefinition rumour, List<string> log)
    {
        int added = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:RaidDefinition"))
        {
            var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            var so = new SerializedObject(raid);
            SerializedProperty systems = so.FindProperty("systems");
            for (int i = 0; i < systems.arraySize; i++)
            {
                SerializedProperty system = systems.GetArrayElementAtIndex(i);
                if (!(system.managedReferenceValue is RaidRumoursSystem))
                {
                    continue;
                }

                SerializedProperty pool = system.FindPropertyRelative("pool");
                bool has = false;
                for (int k = 0; k < pool.arraySize; k++)
                {
                    has |= pool.GetArrayElementAtIndex(k).objectReferenceValue == rumour;
                }

                if (!has)
                {
                    pool.InsertArrayElementAtIndex(pool.arraySize);
                    pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = rumour;
                    added++;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(raid);
        }

        log.Add($"rumour in {added} raids' pools");
    }

    // A Pact Rune now and then in a supply cache: about one raid in two finds one.
    private static void AddToCaches(ItemDefinition rune, List<string> log)
    {
        const string path = "Assets/_Project/Prefabs/Raids/SupplyCache.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            LootCache cache = root.GetComponentInChildren<LootCache>(true);
            var so = new SerializedObject(cache);
            SerializedProperty entries = so.FindProperty("drops").FindPropertyRelative("entries");
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("item").objectReferenceValue == rune)
                {
                    entries.GetArrayElementAtIndex(i).FindPropertyRelative("probability").floatValue = CacheChance;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add("cache chance kept");
                    return;
                }
            }

            entries.InsertArrayElementAtIndex(entries.arraySize);
            SerializedProperty entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("item").objectReferenceValue = rune;
            entry.FindPropertyRelative("resource").objectReferenceValue = null;
            entry.FindPropertyRelative("minQuantity").intValue = 1;
            entry.FindPropertyRelative("maxQuantity").intValue = 1;
            entry.FindPropertyRelative("probability").floatValue = CacheChance;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            log.Add("rune in the supply caches");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void Add(SerializedProperty list, RaidEventOutcome outcome, System.Action<SerializedProperty> fill = null)
    {
        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.managedReferenceValue = outcome;
        fill?.Invoke(entry);
    }

    private static ItemDefinition Item(string assetName)
    {
        foreach (string guid in AssetDatabase.FindAssets(assetName + " t:ItemDefinition"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == assetName)
            {
                return AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            }
        }

        Debug.LogWarning("No item " + assetName);
        return null;
    }
}
