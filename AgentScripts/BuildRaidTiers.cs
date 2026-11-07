using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gear score and the Misty Woods' difficulty tiers (`unity command script run --file AgentScripts/BuildRaidTiers.cs --entry BuildRaidTiers.Apply`):
/// the settings asset, what each tier asks and holds, the armor of the Moonsilver set, which didn't beat Iron, and the tier row in the raid
/// planner. See Docs/GearScoreAndRaidTiers.md for why these numbers. Safe to run again.
/// </summary>
public static class BuildRaidTiers
{
    private const string RaidPath = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string MapPath = "Assets/_Project/Data/Raids/Maps/MistyWoods Map.asset";
    private const string SettingsPath = "Assets/_Project/Resources/GearScoreSettings.asset";
    private const string PlannerPath = "Assets/_Project/Prefabs/UI/RaidPreparationWindow.prefab";

    public static string Apply()
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            return "the open scene has unsaved changes";
        }

        var log = new List<string> { Settings(), Overrides(), Moonsilver(), Tiers_(), Entries(), Trees() };
        AssetDatabase.SaveAssets();
        log.Add(Planner());
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    private static string Settings()
    {
        if (AssetDatabase.LoadAssetAtPath<GearScoreSettings>(SettingsPath) == null)
        {
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GearScoreSettings>(), SettingsPath);
        }

        return "settings";
    }

    private static ItemDefinition Item(string name)
    {
        string guid = AssetDatabase.FindAssets("t:ItemDefinition " + name).FirstOrDefault(found => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(found)) == name);
        return guid != null ? AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid)) : null;
    }

    // A torch and a fishing rod fill the hand, but nobody's power comes from them.
    private static string Overrides()
    {
        foreach (string name in new[] { "FishingRod", "HandTorch" })
        {
            var so = new SerializedObject(Item(name));
            so.FindProperty("gearScoreOverride").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return "torch and rod count for 1";
    }

    // The set is a caster's, with spell power and resistances on top, but it still has to protect better than the iron it replaces.
    private static string Moonsilver()
    {
        (string item, float armor)[] wanted =
        {
            ("MoonsilverCirclet", 26f), ("MoonsilverHauberk", 86f), ("MoonsilverGloves", 18f), ("MoonsilverSash", 14f), ("MoonsilverBoots", 26f),
        };
        foreach ((string item, float armor) in wanted)
        {
            var so = new SerializedObject(Item(item));
            SerializedProperty bonuses = so.FindProperty("statBonuses");
            for (int i = 0; i < bonuses.arraySize; i++)
            {
                SerializedProperty bonus = bonuses.GetArrayElementAtIndex(i);
                if (bonus.FindPropertyRelative("stat").enumValueIndex == (int)StatType.Armor || bonus.FindPropertyRelative("stat").intValue == (int)StatType.Armor)
                {
                    bonus.FindPropertyRelative("value").floatValue = armor;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return "moonsilver armor 78 -> 170";
    }

    private static string Tiers_()
    {
        (int tier, int recommended, int minimum, float health, float power, float density, float loot, string briefing)[] tiers =
        {
            (1, 10, 0, 1f, 1f, 1f, 1f, "Wolves, boars and bog things. Copper and hardwood for those who can carry them."),
            (2, 30, 15, 1.5f, 1.35f, 1.15f, 1.1f, "Wolves pounce from a distance and bog things spit in volleys. Root Guardians wake, Moss Shamans call them on. Iron, deepstone and elderwood lie deeper in."),
            (3, 50, 30, 2.3f, 2.6f, 1.3f, 1.2f, "The dead walk: Draugs among the trees. Boars trample, Guardians slam the ground and the great troll charges. Moonsilver can be found here."),
            (4, 70, 45, 3f, 3.4f, 1.45f, 1.35f, "The woods at their worst. Draugs rally with a war cry, Guardians call up roots and the great troll calls its kin. Its Trollstone is worth taking."),
        };
        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<RaidDefinition>(RaidPath));
        SerializedProperty list = so.FindProperty("difficulties");
        list.arraySize = tiers.Length;
        for (int i = 0; i < tiers.Length; i++)
        {
            SerializedProperty row = list.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("tier").intValue = tiers[i].tier;
            row.FindPropertyRelative("recommendedGearScore").intValue = tiers[i].recommended;
            row.FindPropertyRelative("minimumGearScore").intValue = tiers[i].minimum;
            row.FindPropertyRelative("enemyHealth").floatValue = tiers[i].health;
            row.FindPropertyRelative("enemyPower").floatValue = tiers[i].power;
            row.FindPropertyRelative("enemyDensity").floatValue = tiers[i].density;
            row.FindPropertyRelative("loot").floatValue = tiers[i].loot;
            row.FindPropertyRelative("dropsUpToTier").intValue = 0;
            row.FindPropertyRelative("briefing").stringValue = tiers[i].briefing;
        }

        // The briefing names who can be met at the tier picked, so the list holds everyone.
        SerializedProperty known = so.FindProperty("knownEnemies");
        foreach (string name in new[] { "Draug", "MossShaman" })
        {
            Object monster = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:MonsterDefinition " + name).First()));
            bool listed = false;
            for (int i = 0; i < known.arraySize; i++)
            {
                listed |= known.GetArrayElementAtIndex(i).objectReferenceValue == monster;
            }

            if (!listed)
            {
                known.arraySize++;
                known.GetArrayElementAtIndex(known.arraySize - 1).objectReferenceValue = monster;
            }
        }

        so.FindProperty("description").stringValue = "The nearest wild forest beyond the portal, where mist hangs between old pines. Cursed things hunt here, and what lies deeper grows worse the harder you go.";
        so.ApplyModifiedPropertiesWithoutUndo();
        return $"{tiers.Length} tiers";
    }

    // A raid of tier N hands over materials of tier N+1, so each node comes in a tier before its material's. Each tier brings something new.
    private static string Entries()
    {
        var from = new Dictionary<string, int>
        {
            { "RootGuardian", 2 }, { "MossShaman", 2 }, { "Draug", 3 },
            { "IronOreNode", 2 }, { "DeepstoneNode", 2 }, { "ElderwoodNode", 2 }, { "HuldraSilkNode", 2 }, { "MoonsilverOreNode", 3 },
        };
        var to = new Dictionary<string, int> { { "CopperOreNode", 3 } };
        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<RaidDefinition>(RaidPath));
        SerializedProperty lists = so.FindProperty("spawnLists");
        int set = 0;
        for (int l = 0; l < lists.arraySize; l++)
        {
            SerializedProperty entries = lists.GetArrayElementAtIndex(l).FindPropertyRelative("entries");
            for (int e = 0; e < entries.arraySize; e++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(e);
                Object prefab = entry.FindPropertyRelative("prefab").objectReferenceValue;
                if (prefab == null)
                {
                    continue;
                }

                from.TryGetValue(prefab.name, out int first);
                to.TryGetValue(prefab.name, out int last);
                entry.FindPropertyRelative("fromTier").intValue = first;
                entry.FindPropertyRelative("toTier").intValue = last;

                // Rare by weight, but the briefing promises them, so a tier that brings them always has one.
                entry.FindPropertyRelative("minPerGroup").intValue = prefab.name == "Draug" || prefab.name == "MossShaman" ? 1 : entry.FindPropertyRelative("minPerGroup").intValue;
                set += first > 0 || last > 0 ? 1 : 0;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return $"{set} spawn entries limited by tier";
    }

    private static string Trees()
    {
        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<RaidMapDefinition>(MapPath));
        SerializedProperty trees = so.FindProperty("trees");
        for (int i = 0; i < trees.arraySize; i++)
        {
            SerializedProperty tree = trees.GetArrayElementAtIndex(i);
            Object prefab = tree.FindPropertyRelative("prefab").objectReferenceValue;
            tree.FindPropertyRelative("fromTier").intValue = prefab != null && prefab.name.StartsWith("Dead Spruce") ? 2 : 0;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return "dead spruce from tier II";
    }

    // A copy of the sparks row, as it already has the look and the two buttons, set just above it.
    private static string Planner()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlannerPath);
        try
        {
            // The planner builds itself in code now (BuildRaidPlanner), so there is no prefab row left to copy.
            if (root.GetComponentsInChildren<Transform>(true).All(found => found.name != "Sparks Row"))
            {
                return "planner: builds its own tier row";
            }

            var window = root.GetComponentInChildren<RaidPreparationWindow>(true);
            Transform sparks = root.GetComponentsInChildren<Transform>(true).First(found => found.name == "Sparks Row");
            Transform row = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(found => found.name == "Tier Row");
            if (row == null)
            {
                row = Object.Instantiate(sparks, sparks.parent);
                row.name = "Tier Row";
                row.SetSiblingIndex(sparks.GetSiblingIndex());
            }

            var so = new SerializedObject(window);
            var sparksLabel = so.FindProperty("sparksLabel").objectReferenceValue as Component;
            var fewer = so.FindProperty("fewerSparksButton").objectReferenceValue as Component;
            var more = so.FindProperty("moreSparksButton").objectReferenceValue as Component;
            Component Twin(Component original)
            {
                Transform twin = row.Find(original.transform.GetSiblingIndex().ToString()) ?? row.GetChild(original.transform.GetSiblingIndex());
                return twin.GetComponent(original.GetType());
            }

            so.FindProperty("tierLabel").objectReferenceValue = Twin(sparksLabel);
            so.FindProperty("lowerTierButton").objectReferenceValue = Twin(fewer);
            so.FindProperty("higherTierButton").objectReferenceValue = Twin(more);
            so.ApplyModifiedPropertiesWithoutUndo();
            Twin(sparksLabel).gameObject.name = "Tier Label";
            LayOut((RectTransform)sparks.parent, (RectTransform)row);
            PrefabUtility.SaveAsPrefabAsset(root, PlannerPath);
            return "planner: tier row under " + sparks.parent.name + $" ({sparks.parent.childCount} rows)";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The panel is laid out by hand, so the row gets room under the raid's name and all below it moves down, the panel growing to match.
    private static void LayOut(RectTransform panel, RectTransform row)
    {
        const float room = 40f;
        (string name, float y)[] below =
        {
            ("Description", -92f), ("Modifiers Title", -316f), ("Modifiers Hint", -342f), ("Modifier Scroll", -368f),
            ("Rules Title", -532f), ("Rules", -560f), ("Risk", -590f), ("Sparks Row", -668f),
        };
        foreach ((string name, float y) in below)
        {
            var child = (RectTransform)panel.Find(name);
            child.anchoredPosition = new Vector2(child.anchoredPosition.x, y - room);
        }

        row.anchoredPosition = new Vector2(row.anchoredPosition.x, -92f);
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, 784f + room);
    }
}
