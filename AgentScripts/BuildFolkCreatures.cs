using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Three monsters from Norwegian folk tales (the user, 2026-11-13: "a few new monster models ... inspired by nordic fairy
/// tales ... give them traits that fit their look"), made by LocalSpawn and rigged as Humanoids (Tools/LocalSpawn/
/// creatures_manifest.json, Tools/Blender/folk_creatures.py). Run `--entry BuildFolkCreatures.Apply`, then
/// `BuildCreatureModels.FolkBodies` for their bodies. Each starts from the creature closest to it in kind (its prefab,
/// definition and stats copied once) and is then set up here, so it is safe to run again.
/// <list type="bullet">
/// <item><b>Fossegrim</b>, the waterfall's fiddler, from the Moss Shaman: keeps its distance, flings the falls' cold spray,
/// and its tune rallies what lives by the water. Frail up close. By rivers and meres in the Misty Woods.</item>
/// <item><b>Trollkjerring</b>, the troll hag, from the Moss Shaman: hurls stones, rallies and calls up rootlings from the
/// ground; tougher than the shaman. With the trolls.</item>
/// <item><b>Berg-jutul</b>, the mountain giant, from the Old Troll: slow, enormous, armoured in stone (blades do little),
/// stamps, sweeps and charges. Rare, alone.</item>
/// </list>
/// </summary>
public static class BuildFolkCreatures
{
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string Stats = "Assets/_Project/Data/Combat/Stats/";
    private const string Tables = "Assets/_Project/Data/Raids/SpawnTables/";

    private sealed class Folk
    {
        public string Id, Name, From, StatsFrom, Table, Description, Lore, Advice, Habitat;
        public float Health, Armor, MoveSpeed, Weight;
        public float DamageScale = 1f;
        public int PerGroup = 1;
    }

    private static readonly Folk[] All =
    {
        new Folk
        {
            Id = "Fossegrim", Name = "Fossegrim", From = "MossShaman", StatsFrom = "MossShamanStats", Table = "MistyWoods_Roamers",
            Health = 0.75f, Armor = 0f, MoveSpeed = 3.4f, Weight = 0.5f, DamageScale = 0.9f,
            Description = "The spirit of the falls. It keeps its distance and plays.",
            Lore = "Every waterfall has one, the old folk say: a grey man sitting in the spray with a fiddle, playing a tune that makes the trees dance. " +
                   "Bring him a fat ram and he will teach you to play. Bring him nothing and you will hear the tune anyway, and follow it into the water.",
            Advice = "It keeps away and flings the cold spray of the falls, and its tune stirs the creatures near it. It is all bone and wet hair: close in and it breaks easily.",
            Habitat = "Waterfalls, rivers and still meres",
        },
        new Folk
        {
            Id = "Trollkjerring", Name = "Trollkjerring", From = "MossShaman", StatsFrom = "MossShamanStats", Table = "MistyWoods_Trolls",
            Health = 1.5f, Armor = 10f, MoveSpeed = 2.8f, Weight = 0.6f, DamageScale = 1.2f,
            Description = "A troll hag. She stays back, throws stones and calls the roots up.",
            Lore = "Where the trolls go, an old one goes with them: the trollkjerring, who brews in a pot under the hill and remembers every slight. " +
                   "The small trolls fear her more than they fear you.",
            Advice = "She hangs back behind the others, throwing stones, rallying them and calling rootlings out of the ground. Reach her first: without her the rest scatter.",
            Habitat = "Troll ground, behind the others",
        },
        new Folk
        {
            Id = "Bergjutul", Name = "Berg-jutul", From = "OldMossTroll", StatsFrom = "OldMossTrollStats", Table = "MistyWoods_Roamers",
            Health = 0.6f, Armor = 70f, MoveSpeed = 2.1f, Weight = 0.25f, DamageScale = 1.1f,
            Description = "A giant of the mountain, made of its stone.",
            Lore = "The jutuls were here before the trees. They sleep for a hundred years at a time and wake up cross, with moss on their shoulders, " +
                   "and walk until they find whatever woke them.",
            Advice = "Slow, and stone all over: blades skid off it. Stay out of its stamp and sweep and hit it as it recovers. Blunt weapons and fire do better than edges.",
            Habitat = "Old stone and high ground, alone",
        },
    };

    public static string Apply()
    {
        var log = new List<string>();
        foreach (Folk folk in All)
        {
            log.Add(Make(folk));
        }

        var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>("Assets/_Project/Resources/ContentDatabase.asset");
        database.CollectAll();
        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssetIfDirty(database);
        return string.Join("; ", log);
    }

    private static string Make(Folk folk)
    {
        string definitionPath = Monsters + folk.Id + ".asset", prefabPath = Enemies + folk.Id + ".prefab", statsPath = Stats + folk.Id + "Stats.asset";
        Copy(Monsters + folk.From + ".asset", definitionPath);
        Copy(Enemies + folk.From + ".prefab", prefabPath);
        Copy(Stats + folk.StatsFrom + ".asset", statsPath);
        var definition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(definitionPath);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var stats = AssetDatabase.LoadAssetAtPath<ScriptableObject>(statsPath);
        var template = AssetDatabase.LoadAssetAtPath<ScriptableObject>(Stats + folk.StatsFrom + ".asset");

        // Health as a share of the template's, armour set outright.
        var statsData = new SerializedObject(stats);
        var templateData = new SerializedObject(template);
        SerializedProperty list = statsData.FindProperty("stats"), from = templateData.FindProperty("stats");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            int stat = entry.FindPropertyRelative("stat").enumValueIndex;
            float original = from.GetArrayElementAtIndex(i).FindPropertyRelative("value").floatValue;
            if (entry.FindPropertyRelative("stat").intValue == (int)StatType.MaxHealth)
            {
                entry.FindPropertyRelative("value").floatValue = Mathf.Round(original * folk.Health);
            }
            else if (entry.FindPropertyRelative("stat").intValue == 7)
            {
                entry.FindPropertyRelative("value").floatValue = folk.Armor;
            }
        }

        statsData.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stats);

        var so = new SerializedObject(definition);
        so.FindProperty("id").stringValue = folk.Id;
        so.FindProperty("displayName").stringValue = folk.Name;
        so.FindProperty("description").stringValue = folk.Description;
        so.FindProperty("combatStats").objectReferenceValue = stats;
        so.FindProperty("prefab").objectReferenceValue = prefab;
        so.FindProperty("moveSpeed").floatValue = folk.MoveSpeed;
        so.FindProperty("journalLore").stringValue = folk.Lore;
        so.FindProperty("journalAdvice").stringValue = folk.Advice;
        so.FindProperty("journalHabitat").stringValue = folk.Habitat;
        so.FindProperty("portrait").objectReferenceValue = null; // painted later (journal); none rather than another's
        SerializedProperty attacks = so.FindProperty("attacks");
        for (int i = attacks.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty attack = attacks.GetArrayElementAtIndex(i);
            var kind = (MonsterAttackKind)attack.FindPropertyRelative("kind").enumValueIndex;
            // The Fossegrim calls nothing out of the ground: the falls are its, not the roots.
            if (folk.Id == "Fossegrim" && kind == MonsterAttackKind.Summon)
            {
                attacks.DeleteArrayElementAtIndex(i);
                continue;
            }

            SerializedProperty damage = attack.FindPropertyRelative("damage");
            damage.floatValue = Mathf.Round(damage.floatValue * folk.DamageScale * 10f) / 10f;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(definition);
        AssetDatabase.SaveAssetIfDirty(stats);

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        foreach (MonsterAI ai in root.GetComponentsInChildren<MonsterAI>(true))
        {
            var data = new SerializedObject(ai);
            data.FindProperty("definition").objectReferenceValue = definition;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // A copy of a boss-like creature must not bring its boss parts along.
        foreach (BossEncounter boss in root.GetComponentsInChildren<BossEncounter>(true))
        {
            Object.DestroyImmediate(boss);
        }

        root.name = folk.Id;
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        return $"{folk.Name}: {attacks.arraySize} attacks, {Spawn(folk, prefab)}";
    }

    private static void Copy(string from, string to)
    {
        if (AssetDatabase.LoadMainAssetAtPath(to) == null)
        {
            AssetDatabase.CopyAsset(from, to);
        }
    }

    private static string Spawn(Folk folk, GameObject prefab)
    {
        var table = AssetDatabase.LoadAssetAtPath<SpawnTable>(Tables + folk.Table + ".asset");
        var so = new SerializedObject(table);
        SerializedProperty entries = so.FindProperty("entries");
        SerializedProperty entry = null;
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab)
            {
                entry = entries.GetArrayElementAtIndex(i);
            }
        }

        if (entry == null)
        {
            entries.arraySize++;
            entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            entry.FindPropertyRelative("minPerGroup").intValue = 0;
            entry.FindPropertyRelative("fromTier").intValue = 0;
            entry.FindPropertyRelative("toTier").intValue = 0;
        }

        entry.FindPropertyRelative("weight").floatValue = folk.Weight;
        entry.FindPropertyRelative("maxPerGroup").intValue = folk.PerGroup;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(table);
        return $"in {folk.Table} (weight {folk.Weight})";
    }
}
