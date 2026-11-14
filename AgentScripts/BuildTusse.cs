#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The tusse, a thief (`unity command script run -- --file AgentScripts/BuildTusse.cs --entry BuildTusse.Apply`), then
/// `BuildCreatureModels.ForestTrolls` for its body. The user asked (2026-11-13) for a monster made of their forest troll "that
/// runs up to the player to steal items from wagons, inventory etc and running away. Only attacking while cornered."
/// From Norwegian folklore: the tusser are the small hidden folk under the hills, who take what is left unwatched.
/// <para>Makes Data/Monsters/Tusse.asset and Prefabs/Enemies/Tusse.prefab from the small moss troll's (once), sets them up
/// (MonsterDefinition › Game › Thief; MonsterAI.Thief does the stealing), and adds it to the Misty Woods roamers. Safe to
/// run again.</para>
/// </summary>
public static class BuildTusse
{
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";

    public static string Apply()
    {
        var log = new List<string>();
        string definitionPath = Monsters + "Tusse.asset", prefabPath = Enemies + "Tusse.prefab";
        if (AssetDatabase.LoadAssetAtPath<MonsterDefinition>(definitionPath) == null)
        {
            AssetDatabase.CopyAsset(Monsters + "SmallMossTroll.asset", definitionPath);
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
        {
            AssetDatabase.CopyAsset(Enemies + "SmallMossTroll.prefab", prefabPath);
        }

        var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(definitionPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var so = new SerializedObject(definition);
        so.FindProperty("id").stringValue = "Tusse";
        so.FindProperty("displayName").stringValue = "Tusse";
        so.FindProperty("description").stringValue = "One of the small hidden folk. It takes what is not held tight, and runs.";
        so.FindProperty("prefab").objectReferenceValue = prefab;
        so.FindProperty("thief").boolValue = true;
        so.FindProperty("stealReach").floatValue = 1.5f;
        so.FindProperty("escapeDistance").floatValue = 40f;
        so.FindProperty("corneredSeconds").floatValue = 4f;
        // Quicker than a walking player, slower than a running one: it can be caught, with effort.
        so.FindProperty("moveSpeed").floatValue = 4.4f;
        so.FindProperty("detectionRange").floatValue = 16f;
        so.FindProperty("closeSenseRange").floatValue = 3f;
        so.FindProperty("journalLore").stringValue = "The tusser live under the hills and in the roots of old trees, and they have always taken what was left out. " +
            "Grandmothers set out porridge to keep them sweet. Nobody sets out porridge now, so they come for the rest.";
        so.FindProperty("journalAdvice").stringValue = "It runs for your bag or a cart, snatches one thing and is off. Kill it and it drops what it took; let it get " +
            "far enough and that is gone. It only fights when it has nowhere to run, and then not for long: corner it against rock or water.";
        so.FindProperty("journalHabitat").stringValue = "Roots, hollows and old stone, wherever travellers stop";
        log.Add(Claw(so));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);

        // The prefab's own mind reads the new definition.
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        foreach (MonsterAI ai in root.GetComponentsInChildren<MonsterAI>(true))
        {
            var data = new SerializedObject(ai);
            data.FindProperty("definition").objectReferenceValue = definition;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        root.name = "Tusse";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        log.Add(Roams(prefab));
        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        database.CollectAll();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssetIfDirty(definition);
        AssetDatabase.SaveAssetIfDirty(database);
        return $"tusse: {string.Join("; ", log)}; problems: {string.Join(", ", ProblemsOf(definition))}";
    }

    // Up close with its hands, weakly: it is a thief, not a fighter. The plain monster strike.
    private static string Claw(SerializedObject so)
    {
        AttackDefinition claw = AssetDatabase.FindAssets("MonsterStrike t:AttackDefinition").Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<AttackDefinition>).FirstOrDefault(a => a != null && a.name == "MonsterStrike");
        SerializedProperty attacks = so.FindProperty("attacks");
        attacks.arraySize = 1;
        SerializedProperty first = attacks.GetArrayElementAtIndex(0);
        first.FindPropertyRelative("attack").objectReferenceValue = claw;
        first.FindPropertyRelative("kind").enumValueIndex = (int)MonsterAttackKind.Melee;
        first.FindPropertyRelative("damage").floatValue = 4f;
        first.FindPropertyRelative("minimumRange").floatValue = 0f;
        first.FindPropertyRelative("maximumRange").floatValue = 1.7f;
        first.FindPropertyRelative("cooldown").floatValue = 1.4f;
        return "strike " + (claw != null ? claw.name : "none");
    }

    private static string Roams(GameObject prefab)
    {
        var table = AssetDatabase.LoadAssetAtPath<SpawnTable>("Assets/_Project/Data/Raids/SpawnTables/MistyWoods_Roamers.asset");
        var so = new SerializedObject(table);
        SerializedProperty entries = so.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab)
            {
                return "already roams the Misty Woods";
            }
        }

        entries.arraySize++;
        SerializedProperty added = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        added.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        added.FindPropertyRelative("weight").floatValue = 1f;
        added.FindPropertyRelative("maxPerGroup").intValue = 1;
        added.FindPropertyRelative("minPerGroup").intValue = 0;
        added.FindPropertyRelative("fromTier").intValue = 0;
        added.FindPropertyRelative("toTier").intValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(table);
        return "roams the Misty Woods (weight 1, alone)";
    }

    private static IEnumerable<string> ProblemsOf(MonsterDefinition definition)
    {
        if (!definition.Thief) yield return "not a thief";
        if (definition.Prefab == null) yield return "no prefab";
    }
}
