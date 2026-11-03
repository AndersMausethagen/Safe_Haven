using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Heartroot, the first boss (GDD §16.6, "early forest guardian"): an old guardian grown too large, asleep in Guardian's Hollow. The
/// briefing speaks of it from the third raid, the HUD points the way, and the player chooses to wake it. Beating it teaches Heartroot's
/// Grasp for good. Numbers are first estimates, to be played. Run after BuildSetPieces.
/// Run: unity command script run -- --file AgentScripts/BuildFirstBoss.cs --entry BuildFirstBoss.Run
/// </summary>
public static class BuildFirstBoss
{
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const string Stats = "Assets/_Project/Data/Combat/Stats/";
    private const string Attacks = "Assets/_Project/Data/Combat/Attacks/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string Rumours = "Assets/_Project/Data/Raids/Rumours/";
    private const string Sites = "Assets/_Project/Prefabs/Raids/Events/Sites/";

    public static string Run()
    {
        SkillDefinition skill = MakeSkill();
        BossDefinition boss = MakeBoss(skill);
        MonsterDefinition monster = MakeMonster();
        GameObject prefab = MakePrefab(monster, boss);
        Edit(monster, so => so.FindProperty("prefab").objectReferenceValue = prefab);
        RaidRumourDefinition rumour = MakeRumour(prefab, boss);
        AddToPool(rumour);
        Edit(Load<RaidSetPieceDefinition>("Assets/_Project/Data/Raids/SetPieces/Guardian's Hollow.asset"), so => so.FindProperty("alwaysThere").boolValue = true);
        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            database.CollectAll();
            EditorUtility.SetDirty(database);
        }

        AssetDatabase.SaveAssets();
        return $"{monster.DisplayName}: {prefab.GetComponent<Damageable>().MaxHealth:0} health; {monster.Attacks.Count} attacks; rumour {rumour.name}; reward {skill.name}";
    }

    private static SkillDefinition MakeSkill()
    {
        var pattern = Load<SkillDefinition>("Assets/_Project/Data/Skills/FrostRune.asset");
        var patternAttack = (AttackDefinition)new SerializedObject(pattern).FindProperty("attack").objectReferenceValue;
        AttackDefinition attack = Copy(patternAttack, Attacks + "HeartrootGrasp.asset");
        Edit(attack, so =>
        {
            SerializedProperty status = so.FindProperty("appliesStatus");
            if (status != null)
            {
                status.objectReferenceValue = Load<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Roots.asset");
            }
        });

        SkillDefinition skill = Copy(pattern, "Assets/_Project/Data/Skills/HeartrootsGrasp.asset");
        Edit(skill, so =>
        {
            so.FindProperty("displayName").stringValue = "Heartroot's Grasp";
            so.FindProperty("description").stringValue = "What the old guardian knew of the forest's hold. Roots break from the ground along the line ahead and take what stands there.";
            so.FindProperty("attack").objectReferenceValue = attack;
            SerializedProperty id = so.FindProperty("id");
            if (id != null)
            {
                id.stringValue = "heartroots_grasp";
            }
        });
        return skill;
    }

    private static BossDefinition MakeBoss(SkillDefinition skill)
    {
        const string path = Monsters + "Boss The Heartroot.asset";
        var boss = Load<BossDefinition>(path);
        if (boss == null)
        {
            boss = ScriptableObject.CreateInstance<BossDefinition>();
            AssetDatabase.CreateAsset(boss, path);
        }

        var horn = Load<SoundDefinition>("Assets/_Project/Audio/Sounds/Library/Tree Crack.asset");
        Edit(boss, so =>
        {
            so.FindProperty("id").stringValue = "boss_heartroot";
            so.FindProperty("displayName").stringValue = "The Heartroot";
            so.FindProperty("title").stringValue = "Guardian of the Hollow";
            so.FindProperty("rewardSkill").objectReferenceValue = skill;
            so.FindProperty("defeatLine").stringValue = "The Heartroot lies still. What it knew of the forest's hold is yours now: you have learned Heartroot's Grasp.";
            SerializedProperty phases = so.FindProperty("phases");
            phases.arraySize = 2;
            Phase(phases.GetArrayElementAtIndex(0), 0.66f, "The ground shakes. Roots begin to tear free.", 1.8f, 0.1f, horn);
            Phase(phases.GetArrayElementAtIndex(1), 0.33f, "The old heart splits, and what is in it is angry.", 2.2f, 0.2f, horn);
        });
        return boss;
    }

    private static void Phase(SerializedProperty entry, float below, string line, float reel, float damage, SoundDefinition sound)
    {
        entry.FindPropertyRelative("belowHealth").floatValue = below;
        entry.FindPropertyRelative("line").stringValue = line;
        entry.FindPropertyRelative("reelSeconds").floatValue = reel;
        entry.FindPropertyRelative("damageBonus").floatValue = damage;
        entry.FindPropertyRelative("sound").objectReferenceValue = sound;
    }

    private static MonsterDefinition MakeMonster()
    {
        CombatStatsDefinitionCopy(out Object stats);
        var pattern = Load<MonsterDefinition>(Monsters + "RootGuardian.asset");
        MonsterDefinition monster = Copy(pattern, Monsters + "TheHeartroot.asset");
        // From the pattern each time, so running this again doesn't add its rows twice.
        EditorUtility.CopySerialized(pattern, monster);
        monster.name = "TheHeartroot";
        Edit(monster, so =>
        {
            so.FindProperty("id").stringValue = "the_heartroot";
            so.FindProperty("displayName").stringValue = "The Heartroot";
            so.FindProperty("description").stringValue = "The first of the guardians, grown old and vast under its hollow. It was set here to keep the forest. Nobody has told it the forest is gone.";
            so.FindProperty("combatStats").objectReferenceValue = stats;
            so.FindProperty("experience").intValue = 400;
            so.FindProperty("neverElite").boolValue = true;
            so.FindProperty("poise").floatValue = 320f;
            so.FindProperty("detectionRange").floatValue = 40f;
            so.FindProperty("chaseRange").floatValue = 70f;
            so.FindProperty("leashRange").floatValue = 70f;
            SerializedProperty rows = so.FindProperty("attacks");
            // Row 0 the slam, row 1 the wide blow, row 2 the call to the roots.
            Row(rows, 0, 40f, 1.8f, 0);
            Row(rows, 1, 54f, 7f, 1);
            Row(rows, 2, 0f, 16f, 1);
            // Near the end, the slam comes again and again.
            rows.InsertArrayElementAtIndex(0);
            Row(rows, 0, 46f, 1.1f, 2);
            rows.GetArrayElementAtIndex(0).FindPropertyRelative("priority").intValue = 12;
        });
        return monster;
    }

    private static void Row(SerializedProperty rows, int index, float damage, float cooldown, int phase)
    {
        SerializedProperty row = rows.GetArrayElementAtIndex(index);
        row.FindPropertyRelative("damage").floatValue = damage;
        row.FindPropertyRelative("cooldown").floatValue = cooldown;
        row.FindPropertyRelative("fromTier").intValue = 0;
        row.FindPropertyRelative("fromPhase").intValue = phase;
    }

    private static void CombatStatsDefinitionCopy(out Object stats)
    {
        string path = Stats + "TheHeartrootStats.asset";
        if (AssetDatabase.LoadMainAssetAtPath(path) == null)
        {
            AssetDatabase.CopyAsset(Stats + "RootGuardianStats.asset", path);
        }

        stats = AssetDatabase.LoadMainAssetAtPath(path);
        Edit(stats, so =>
        {
            SerializedProperty list = FirstOf(so, "stats", "entries", "baseStats");
            for (int i = 0; list != null && i < list.arraySize; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("stat").intValue == (int)StatType.MaxHealth)
                {
                    entry.FindPropertyRelative("value").floatValue = 1100f;
                }
            }
        });
    }

    private static GameObject MakePrefab(MonsterDefinition monster, BossDefinition boss)
    {
        string path = Enemies + "TheHeartroot.prefab";
        if (Load<GameObject>(path) == null)
        {
            AssetDatabase.CopyAsset(Enemies + "RootGuardian.prefab", path);
        }

        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        contents.transform.localScale = Vector3.one * 1.55f;
        var ai = new SerializedObject(contents.GetComponent<MonsterAI>());
        ai.FindProperty("definition").objectReferenceValue = monster;
        ai.ApplyModifiedPropertiesWithoutUndo();
        if (contents.TryGetComponent(out Combatant combatant))
        {
            var fighter = new SerializedObject(combatant);
            SerializedProperty stats = FirstOf(fighter, "stats", "statsDefinition", "baseStats", "definition");
            if (stats != null && stats.propertyType == SerializedPropertyType.ObjectReference)
            {
                stats.objectReferenceValue = monster.CombatStats;
                fighter.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        if (!contents.TryGetComponent(out BossEncounter encounter))
        {
            encounter = contents.AddComponent<BossEncounter>();
        }

        var boss0 = new SerializedObject(encounter);
        boss0.FindProperty("definition").objectReferenceValue = boss;
        boss0.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
        return Load<GameObject>(path);
    }

    private static RaidRumourDefinition MakeRumour(GameObject bossPrefab, BossDefinition boss)
    {
        // What stands in the middle of the hollow, until it is woken.
        string sitePath = Sites + "Heartroot Stump.prefab";
        if (Load<GameObject>(sitePath) == null)
        {
            AssetDatabase.CopyAsset(Sites + "Old Beacon.prefab", sitePath);
        }

        DressStump(sitePath);
        RaidEventDefinition site = Copy<RaidEventDefinition>(null, "Assets/_Project/Data/Raids/Rumours/Events/TheHeartroot.asset");
        Edit(site, so =>
        {
            so.FindProperty("id").stringValue = "rumour.theheartroot";
            so.FindProperty("displayName").stringValue = "The Heartroot's Hollow";
            so.FindProperty("weight").floatValue = 0f;
            so.FindProperty("sitePrefab").objectReferenceValue = Load<GameObject>(sitePath);
            so.FindProperty("siteText").stringValue = "A stump as wide as a house, split down the middle, and breathing. Dead trees stand round it in a ring, and nothing grows inside. There will be no hiding once it wakes.";
            SerializedProperty choices = so.FindProperty("choices");
            choices.ClearArray();
            choices.InsertArrayElementAtIndex(0);
            SerializedProperty wake = choices.GetArrayElementAtIndex(0);
            wake.FindPropertyRelative("label").stringValue = "Wake it";
            wake.FindPropertyRelative("detail").stringValue = "A fight to the end, in three stages. Beating it teaches you what it knows, for good.";
            wake.FindPropertyRelative("result").stringValue = "The stump opens. Something stands up out of it.";
            wake.FindPropertyRelative("cost").ClearArray();
            SerializedProperty outcomes = wake.FindPropertyRelative("outcomes");
            outcomes.ClearArray();
            outcomes.InsertArrayElementAtIndex(0);
            SerializedProperty spawn = outcomes.GetArrayElementAtIndex(0);
            spawn.managedReferenceValue = new SpawnCreaturesOutcome();
            SerializedProperty prefabs = spawn.FindPropertyRelative("prefabs");
            prefabs.arraySize = 1;
            prefabs.GetArrayElementAtIndex(0).objectReferenceValue = bossPrefab;
            spawn.FindPropertyRelative("count").intValue = 1;
            spawn.FindPropertyRelative("radius").floatValue = 2.5f;
            spawn.FindPropertyRelative("hunt").boolValue = true;
        });

        RaidRumourDefinition rumour = Copy<RaidRumourDefinition>(null, Rumours + "TheHeartroot.asset");
        Edit(rumour, so =>
        {
            so.FindProperty("id").stringValue = "rumour_heartroot";
            so.FindProperty("briefing").stringValue = "Something old is breathing under the roots, deep in the woods. The portal has felt it turn over.";
            so.FindProperty("site").objectReferenceValue = site;
            so.FindProperty("setPiece").objectReferenceValue = Load<RaidSetPieceDefinition>("Assets/_Project/Data/Raids/SetPieces/Guardian's Hollow.asset");
            so.FindProperty("weight").floatValue = 1f;
            so.FindProperty("afterExtractions").intValue = 2;
            so.FindProperty("urgent").boolValue = true;
            so.FindProperty("untilBeaten").objectReferenceValue = boss;
        });
        return rumour;
    }

    // A trunk as wide as a house, split down the middle, round the beacon that shows where it is.
    private static void DressStump(string path)
    {
        GameObject contents = PrefabUtility.LoadPrefabContents(path);
        Transform old = contents.transform.Find("Stump");
        if (old != null)
        {
            Object.DestroyImmediate(old.gameObject);
        }

        var stump = new GameObject("Stump").transform;
        stump.SetParent(contents.transform, false);
        var bark = Load<Material>("Assets/_Project/Art/Forest/Materials/Bark.mat");
        var cut = Load<Material>("Assets/_Project/Art/Forest/Materials/Log Cut Face.mat");
        foreach ((Vector3 at, Vector3 tilt, Vector3 size, Material look) in new[]
        {
            (new Vector3(-1.05f, 0.9f, 0f), new Vector3(0f, 0f, 7f), new Vector3(1.9f, 1f, 3.2f), bark),
            (new Vector3(1.05f, 0.75f, 0.1f), new Vector3(0f, 0f, -9f), new Vector3(1.9f, 0.85f, 3f), bark),
            (new Vector3(-1.05f, 1.86f, 0f), new Vector3(0f, 0f, 7f), new Vector3(1.8f, 0.03f, 3f), cut),
            (new Vector3(1.05f, 1.56f, 0.1f), new Vector3(0f, 0f, -9f), new Vector3(1.8f, 0.03f, 2.8f), cut),
        })
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.transform.SetParent(stump, false);
            part.transform.localPosition = at;
            part.transform.localRotation = Quaternion.Euler(tilt);
            part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = look;
            Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        PrefabUtility.SaveAsPrefabAsset(contents, path);
        PrefabUtility.UnloadPrefabContents(contents);
    }

    private static void AddToPool(RaidRumourDefinition rumour)
    {
        var raid = Load<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        RaidRumoursSystem system = raid.GetSystem<RaidRumoursSystem>();
        var pool = new List<RaidRumourDefinition>(system.Pool);
        if (!pool.Contains(rumour))
        {
            pool.Add(rumour);
            system.Configure(pool, system.Count);
            EditorUtility.SetDirty(raid);
        }
    }

    // ----- Helpers -----

    private static T Copy<T>(T pattern, string path) where T : ScriptableObject
    {
        var made = Load<T>(path);
        if (made == null)
        {
            made = pattern != null ? Object.Instantiate(pattern) : ScriptableObject.CreateInstance<T>();
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(made, path);
        }

        return made;
    }

    private static SerializedProperty FirstOf(SerializedObject so, params string[] names)
    {
        foreach (string name in names)
        {
            SerializedProperty found = so.FindProperty(name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static void Edit(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
}
