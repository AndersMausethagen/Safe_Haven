#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEngine;

/// <summary>
/// The ward breaches after the first, the ward anchor they wear down and Resources/WardBreachSettings. Run with
/// unity command run_script --file AgentScripts/BuildBreaches.cs --entry BuildBreaches.All
/// Makes objects in the open scene while it works: check the scene is clean after.
/// </summary>
public static class BuildBreaches
{
    private const string Data = "Assets/_Project/Data/Settlement/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string AnchorPath = "Assets/_Project/Prefabs/Systems/Ward Anchor.prefab";
    private const string AnchorStats = "Assets/_Project/Data/Combat/Stats/WardAnchorStats.asset";
    private const string WoodStats = "Assets/_Project/Data/Combat/Stats/WoodenStructureStats.asset";
    private const string SettingsPath = "Assets/_Project/Resources/WardBreachSettings.asset";
    private const float WardHealth = 1800f;

    public static string All()
    {
        var first = AssetDatabase.LoadAssetAtPath<WardBreachDefinition>(Data + "FirstWardBreach.asset");
        WardBreachDefinition hungry = Breach("HungryDarkBreach", "The Hungry Dark",
            "The portal shudders. They have learned where the warmth is, and they are coming for more than you. Mend what you can and set your traps; the marked roads show where they will walk.",
            "They are gathering beyond the ward. When you are ready, I will open it and we will meet them here.",
            "Wolves and rootlings will come",
            "The last of them falls. The ward knits itself closed, and the home stands.",
            "The ward tears. They scatter with what they came for, and the light gutters. Rest, and meet them again.",
            2, 0.75f, 0.8f, 3,
            new[] { new[] { ("ForestBittenWolf", 4) }, new[] { ("ForestBittenWolf", 2), ("Rootling", 4) } });
        WardBreachDefinition bog = Breach("BogAndBoneBreach", "Bog and Bone",
            "The ground itself complains. Something large is rooting at the edge of the ward, and the dead walk behind it. Three roads now, and walls will not hold them all.",
            "The bog has risen against us. Finish your walls. Say the word, and I will let them come.",
            "Boars, spitters and draugar will come",
            "The bog sinks back and the bones lie still. They will remember this home, and so will you.",
            "The ward gives. What came out of the bog goes back to it, for now, and leaves its mark on you.",
            3, 0.85f, 0.85f, 4,
            new[]
            {
                new[] { ("Rootling", 4), ("BlightBoar", 2) },
                new[] { ("BogSpitter", 2), ("ForestBittenWolf", 3) },
                new[] { ("BlightBoar", 2), ("Draug", 2) },
            });
        WardBreachDefinition grudge = Breach("ForestRemembersBreach", "The Forest Remembers",
            "Every root in the clearing turns toward the portal. The forest has not forgotten what was taken from it. This will come again and again, each time harder. Prepare.",
            "The forest remembers us. It will not stop. Stand with me when you are ready.",
            "All the forest's anger will come",
            "It draws back, as a tide does. The portal burns bright in the quiet.",
            "The ward falls dark. The forest takes its due and withdraws to wait.",
            3, 1f, 1f, 6,
            new[]
            {
                new[] { ("ForestBittenWolf", 4), ("BlightBoar", 2) },
                new[] { ("Draug", 2), ("BogSpitter", 2), ("MossShaman", 1) },
                new[] { ("SmallMossTroll", 2), ("Draug", 2), ("Rootling", 3) },
            });

        Damageable anchor = BuildAnchor();
        var settings = AssetDatabase.LoadAssetAtPath<WardBreachSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<WardBreachSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        var so = new SerializedObject(settings);
        so.FindProperty("wardAnchor").objectReferenceValue = anchor;
        SerializedProperty list = so.FindProperty("breaches");
        WardBreachDefinition[] order = { first, hungry, bog, grudge };
        list.arraySize = order.Length;
        for (int i = 0; i < order.Length; i++)
        {
            list.GetArrayElementAtIndex(i).objectReferenceValue = order[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
        return $"{settings.Breaches.Count} breaches, anchor {anchor.name}, ward health {anchor.GetComponent<Combatant>().Stats.Get(StatType.MaxHealth)}";
    }

    private static WardBreachDefinition Breach(string file, string name, string warning, string pending, string hint, string won, string failed,
        int lanes, float health, float power, int orbs, (string enemy, int count)[][] waves)
    {
        string path = Data + file + ".asset";
        var breach = AssetDatabase.LoadAssetAtPath<WardBreachDefinition>(path);
        if (breach == null)
        {
            breach = ScriptableObject.CreateInstance<WardBreachDefinition>();
            AssetDatabase.CreateAsset(breach, path);
        }

        var first = new SerializedObject(AssetDatabase.LoadAssetAtPath<WardBreachDefinition>(Data + "FirstWardBreach.asset"));
        var so = new SerializedObject(breach);
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("warningText").stringValue = warning;
        so.FindProperty("pendingText").stringValue = pending;
        so.FindProperty("startHint").stringValue = hint;
        so.FindProperty("wonText").stringValue = won;
        so.FindProperty("failedText").stringValue = failed;
        so.FindProperty("threatensHome").boolValue = true;
        so.FindProperty("laneCount").intValue = lanes;
        so.FindProperty("secondsBetweenEnemies").floatValue = 1.5f;
        so.FindProperty("enemyHealthMultiplier").floatValue = health;
        so.FindProperty("enemyPowerMultiplier").floatValue = power;
        SerializedProperty reward = so.FindProperty("reward");
        reward.arraySize = 1;
        reward.GetArrayElementAtIndex(0).FindPropertyRelative("resource").objectReferenceValue =
            first.FindProperty("reward").GetArrayElementAtIndex(0).FindPropertyRelative("resource").objectReferenceValue;
        reward.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = orbs;
        SerializedProperty list = so.FindProperty("waves");
        list.arraySize = waves.Length;
        for (int w = 0; w < waves.Length; w++)
        {
            SerializedProperty groups = list.GetArrayElementAtIndex(w).FindPropertyRelative("groups");
            groups.arraySize = waves[w].Length;
            for (int g = 0; g < waves[w].Length; g++)
            {
                SerializedProperty group = groups.GetArrayElementAtIndex(g);
                var enemy = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + waves[w][g].enemy + ".prefab");
                Debug.Assert(enemy != null, waves[w][g].enemy);
                group.FindPropertyRelative("enemy").objectReferenceValue = enemy;
                group.FindPropertyRelative("count").intValue = waves[w][g].count;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(breach);
        return breach;
    }

    private static Damageable BuildAnchor()
    {
        if (!AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(AnchorStats))
        {
            AssetDatabase.CopyAsset(WoodStats, AnchorStats);
        }

        var stats = new SerializedObject(AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(AnchorStats));
        stats.FindProperty("stats").GetArrayElementAtIndex(0).FindPropertyRelative("value").floatValue = WardHealth;
        stats.ApplyModifiedPropertiesWithoutUndo();

        var palisade = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Structures/Palisade.prefab");
        var root = new GameObject("Ward Anchor");
        var capsule = root.AddComponent<CapsuleCollider>();
        capsule.radius = 1.4f;
        capsule.height = 5f;
        capsule.center = Vector3.up * 2.5f;
        var combatant = root.AddComponent<Combatant>();
        var side = new SerializedObject(combatant);
        side.FindProperty("faction").enumValueIndex = (int)Faction.Player;
        side.FindProperty("baseStats").objectReferenceValue = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(AnchorStats);
        side.FindProperty("rules").objectReferenceValue = new SerializedObject(palisade.GetComponent<Combatant>()).FindProperty("rules").objectReferenceValue;
        side.ApplyModifiedPropertiesWithoutUndo();
        var health = new SerializedObject(root.AddComponent<Damageable>());
        health.FindProperty("category").enumValueIndex = (int)TargetCategory.Structure;
        health.FindProperty("destroyOnDeath").boolValue = false;
        health.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, AnchorPath);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<Damageable>();
    }
}
