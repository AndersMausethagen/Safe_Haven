using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the Old Moss Troll, the Misty Woods' apex creature (run with `unity command run_script --file
/// AgentScripts/BuildOldMossTroll.cs --entry BuildOldMossTroll.Assets`): stats, two attacks, the monster definition, a
/// footstep sound and a prefab copied from the Root Guardian's humanoid body, scaled up. Re-running updates them.
/// </summary>
public static class BuildOldMossTroll
{
    private const string Stats = "Assets/_Project/Data/Combat/Stats/OldMossTrollStats.asset";
    private const string Sweep = "Assets/_Project/Data/Combat/Attacks/TrollSweep.asset";
    private const string Stomp = "Assets/_Project/Data/Combat/Attacks/TrollStomp.asset";
    private const string Definition = "Assets/_Project/Data/Monsters/OldMossTroll.asset";
    private const string Step = "Assets/_Project/Audio/Sounds/Library/TrollStep.asset";
    private const string Prefab = "Assets/_Project/Prefabs/Enemies/OldMossTroll.prefab";
    private const float BodyScale = 2.7f;

    public static string Assets()
    {
        var log = new List<string>();
        var stats = CopyOnce<CombatStatsDefinition>("Assets/_Project/Data/Combat/Stats/RootGuardianStats.asset", Stats, log);
        Edit(stats, so =>
        {
            SerializedProperty list = so.FindProperty("stats");
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("stat").intValue == 0)
                {
                    entry.FindPropertyRelative("value").floatValue = 1400f;
                }
            }
        });

        var sweep = CopyOnce<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/GuardianSlam.asset", Sweep, log);
        Edit(sweep, so =>
        {
            so.FindProperty("displayName").stringValue = "Troll Sweep";
            so.FindProperty("range").floatValue = 4.2f;
            so.FindProperty("arc").floatValue = 170f;
            so.FindProperty("maxTargets").intValue = 3;
            so.FindProperty("hitDelay").floatValue = 1f;
            so.FindProperty("recoveryTime").floatValue = 1.9f;
        });

        var stomp = CopyOnce<AttackDefinition>("Assets/_Project/Data/Combat/Attacks/GuardianSlam.asset", Stomp, log);
        Edit(stomp, so =>
        {
            so.FindProperty("displayName").stringValue = "Troll Stomp";
            so.FindProperty("range").floatValue = 3.4f;
            so.FindProperty("arc").floatValue = 360f;
            so.FindProperty("maxTargets").intValue = 5;
            so.FindProperty("hitDelay").floatValue = 1.3f;
            so.FindProperty("recoveryTime").floatValue = 2.4f;
        });

        var step = AssetDatabase.LoadAssetAtPath<SoundDefinition>(Step);
        if (step == null)
        {
            step = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(step, Step);
            log.Add("created step sound");
        }

        Edit(step, so =>
        {
            so.FindProperty("displayName").stringValue = "Troll Step";
            SerializedProperty clips = so.FindProperty("clips");
            clips.ClearArray();
            for (int i = 0; i < 5; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/SFX/Kenney Impact/impactSoft_heavy_00{i}.ogg");
                if (clip != null)
                {
                    clips.InsertArrayElementAtIndex(clips.arraySize);
                    clips.GetArrayElementAtIndex(clips.arraySize - 1).objectReferenceValue = clip;
                }
            }

            so.FindProperty("volume").floatValue = 1f;
            so.FindProperty("volumeVariation").floatValue = 0.1f;
            so.FindProperty("pitchRange").vector2Value = new Vector2(0.35f, 0.45f);
            so.FindProperty("positional").boolValue = true;
            so.FindProperty("fullVolumeDistance").floatValue = 8f;
            so.FindProperty("audibleDistance").floatValue = 55f;
            so.FindProperty("maxAtOnce").intValue = 2;
        });

        var definition = CopyOnce<MonsterDefinition>("Assets/_Project/Data/Monsters/RootGuardian.asset", Definition, log);
        var trollstone = AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/Materials/Trollstone.asset");
        var guardian = AssetDatabase.LoadAssetAtPath<MonsterDefinition>("Assets/_Project/Data/Monsters/RootGuardian.asset");
        Edit(definition, so =>
        {
            so.FindProperty("id").stringValue = "OldMossTroll";
            so.FindProperty("displayName").stringValue = "Old Moss Troll";
            so.FindProperty("description").stringValue =
                "A troll as old as the hills, walking its round through the Misty Woods. Its eyes are weak, but it smells " +
                "you long before it sees you and hears every step. Best avoided; if you must fight, its blows are slow but " +
                "they break bones. It carries trollstone.";
            so.FindProperty("combatStats").objectReferenceValue = stats;
            so.FindProperty("attack").objectReferenceValue = sweep;
            so.FindProperty("attackDamage").floatValue = 70f;
            so.FindProperty("attackCooldown").floatValue = 1.2f;
            so.FindProperty("moveSpeed").floatValue = 3.4f;
            so.FindProperty("detectionRange").floatValue = 9f;
            so.FindProperty("chaseRange").floatValue = 26f;
            so.FindProperty("leashRange").floatValue = 40f;
            so.FindProperty("sightAngle").floatValue = 100f;
            so.FindProperty("closeSenseRange").floatValue = 6f;
            so.FindProperty("hearing").floatValue = 1.8f;
            so.FindProperty("alertness").floatValue = 0.8f;
            so.FindProperty("poise").floatValue = 450f;
            so.FindProperty("unlocksRecipes").ClearArray();

            SerializedProperty drops = so.FindProperty("drops").FindPropertyRelative("entries");
            drops.ClearArray();
            AddDrop(drops, trollstone, 2, 3, 1f);
            SerializedProperty guardianDrops = new SerializedObject(guardian).FindProperty("drops").FindPropertyRelative("entries");
            for (int i = 0; i < guardianDrops.arraySize; i++)
            {
                SerializedProperty source = guardianDrops.GetArrayElementAtIndex(i);
                if (source.FindPropertyRelative("resource").objectReferenceValue != null)
                {
                    // The guardian's essence drop, tripled: an apex kill is worth the risk.
                    drops.InsertArrayElementAtIndex(drops.arraySize);
                    SerializedProperty copy = drops.GetArrayElementAtIndex(drops.arraySize - 1);
                    copy.FindPropertyRelative("item").objectReferenceValue = null;
                    copy.FindPropertyRelative("resource").objectReferenceValue = source.FindPropertyRelative("resource").objectReferenceValue;
                    copy.FindPropertyRelative("minQuantity").intValue = source.FindPropertyRelative("minQuantity").intValue * 3;
                    copy.FindPropertyRelative("maxQuantity").intValue = source.FindPropertyRelative("maxQuantity").intValue * 3;
                    copy.FindPropertyRelative("probability").floatValue = 1f;
                }
            }

            SerializedProperty attacks = so.FindProperty("attacks");
            attacks.ClearArray();
            AddAttack(attacks, sweep, 70f, 0f, 4f, 1, 1.2f);
            AddAttack(attacks, stomp, 95f, 0f, 3f, 2, 7f);
        });

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (prefab == null)
        {
            AssetDatabase.CopyAsset("Assets/_Project/Prefabs/Enemies/RootGuardian.prefab", Prefab);
            log.Add("copied prefab");
        }

        GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            root.name = "OldMossTroll";
            root.transform.Find("Model").localScale = Vector3.one * BodyScale;
            // Rebuilt from the bigger body by the prefab builder; the agent (required by MonsterAI) is sized here.
            Object.DestroyImmediate(root.GetComponent<CapsuleCollider>());
            var agentToSize = root.GetComponent<UnityEngine.AI.NavMeshAgent>();
            agentToSize.radius = 0.8f;
            agentToSize.height = 5.2f;
            Transform bar = root.transform.Find("HealthBar");
            if (bar != null)
            {
                Object.DestroyImmediate(bar.gameObject);
            }

            var footfalls = root.GetComponent<CreatureFootfalls>() ?? root.AddComponent<CreatureFootfalls>();
            var footData = new SerializedObject(footfalls);
            footData.FindProperty("stepSound").objectReferenceValue = step;
            footData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        Edit(definition, so => so.FindProperty("prefab").objectReferenceValue = prefab);
        MonsterPrefabBuilder.Update(prefab, definition);
        // The copy still carries the Root Guardian's prefab id; point it at this prefab.
        prefab.GetComponent<SaveableEntity>().RefreshPrefabId();
        AssetDatabase.SaveAssets();

        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var agent = prefab.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var capsule = prefab.GetComponent<CapsuleCollider>();
        List<string> problems = MonsterPrefabBuilder.FindProblems(prefab, definition);
        log.Add($"agent r={agent.radius:0.00} h={agent.height:0.00} speed={agent.speed}; capsule r={capsule.radius:0.00} h={capsule.height:0.00}; " +
                $"ai={prefab.GetComponent<MonsterAI>().Definition.name}; footfalls={prefab.GetComponent<CreatureFootfalls>() != null}; " +
                $"health bar={prefab.GetComponentInChildren<WorldHealthBar>(true) != null}; attacks={definition.Attacks.Count}; " +
                $"drops={definition.Drops.Entries.Count}; problems={(problems.Count == 0 ? "none" : string.Join(", ", problems))}");
        return string.Join("; ", log);
    }

    private static T CopyOnce<T>(string from, string to, List<string> log) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(to);
        if (existing != null)
        {
            return existing;
        }

        AssetDatabase.CopyAsset(from, to);
        log.Add("created " + System.IO.Path.GetFileNameWithoutExtension(to));
        return AssetDatabase.LoadAssetAtPath<T>(to);
    }

    private static void Edit(Object asset, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(asset);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    private static void AddDrop(SerializedProperty drops, ItemDefinition item, int min, int max, float chance)
    {
        drops.InsertArrayElementAtIndex(drops.arraySize);
        SerializedProperty entry = drops.GetArrayElementAtIndex(drops.arraySize - 1);
        entry.FindPropertyRelative("item").objectReferenceValue = item;
        entry.FindPropertyRelative("resource").objectReferenceValue = null;
        entry.FindPropertyRelative("minQuantity").intValue = min;
        entry.FindPropertyRelative("maxQuantity").intValue = max;
        entry.FindPropertyRelative("probability").floatValue = chance;
    }

    private static void AddAttack(SerializedProperty attacks, AttackDefinition attack, float damage, float min, float max,
        int priority, float cooldown)
    {
        attacks.InsertArrayElementAtIndex(attacks.arraySize);
        SerializedProperty entry = attacks.GetArrayElementAtIndex(attacks.arraySize - 1);
        entry.FindPropertyRelative("attack").objectReferenceValue = attack;
        entry.FindPropertyRelative("kind").enumValueIndex = 0;
        entry.FindPropertyRelative("damage").floatValue = damage;
        entry.FindPropertyRelative("minimumRange").floatValue = min;
        entry.FindPropertyRelative("maximumRange").floatValue = max;
        entry.FindPropertyRelative("priority").intValue = priority;
        entry.FindPropertyRelative("cooldown").floatValue = cooldown;
        entry.FindPropertyRelative("interruptible").boolValue = false;
    }
}
