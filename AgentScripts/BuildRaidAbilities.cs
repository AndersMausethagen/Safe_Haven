using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// What higher raid tiers do differently (`unity command script run --file AgentScripts/BuildRaidAbilities.cs --entry BuildRaidAbilities.Apply`):
/// new actions for creatures from a raid tier up (MonsterAttack.fromTier), hunters that fit the tier, and a tier I that isn't mostly wolves.
/// Run after BuildRaidTiers, which resets the spawn lists' tier columns. Safe to run again.
/// </summary>
public static class BuildRaidAbilities
{
    private const string Attacks = "Assets/_Project/Data/Combat/Attacks/";
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";
    private const string RaidPath = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string RoamersPath = "Assets/_Project/Data/Raids/SpawnTables/MistyWoods_Roamers.asset";

    public static string Apply()
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            return "the open scene has unsaved changes";
        }

        var log = new List<string> { TierTwo(), TierThree(), TierFour(), TierOneMix(), Hunters(), Roamers() };
        AssetDatabase.SaveAssets();
        return string.Join("\n", log);
    }

    private static AttackDefinition Attack(string file, string template, string label, float delay, float recovery, float range, float arc)
    {
        string path = Attacks + file + ".asset";
        if (AssetDatabase.LoadAssetAtPath<AttackDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(Attacks + template + ".asset", path);
        }

        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
        var so = new SerializedObject(attack);
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("hitDelay").floatValue = delay;
        so.FindProperty("recoveryTime").floatValue = recovery;
        so.FindProperty("range").floatValue = range;
        so.FindProperty("arc").floatValue = arc;
        so.ApplyModifiedPropertiesWithoutUndo();
        return attack;
    }

    // One action, found again by its attack and tier when run a second time.
    private static SerializedProperty Action(SerializedObject monster, AttackDefinition attack, MonsterAttackKind kind, int tier, float damage, float min, float max, int priority, float cooldown)
    {
        SerializedProperty list = monster.FindProperty("attacks");
        SerializedProperty entry = null;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty candidate = list.GetArrayElementAtIndex(i);
            if (candidate.FindPropertyRelative("attack").objectReferenceValue == attack && candidate.FindPropertyRelative("fromTier").intValue == tier)
            {
                entry = candidate;
            }
        }

        if (entry == null)
        {
            list.arraySize++;
            entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        }

        entry.FindPropertyRelative("attack").objectReferenceValue = attack;
        entry.FindPropertyRelative("kind").intValue = (int)kind;
        entry.FindPropertyRelative("fromTier").intValue = tier;
        entry.FindPropertyRelative("damage").floatValue = damage;
        entry.FindPropertyRelative("minimumRange").floatValue = min;
        entry.FindPropertyRelative("maximumRange").floatValue = max;
        entry.FindPropertyRelative("priority").intValue = priority;
        entry.FindPropertyRelative("cooldown").floatValue = cooldown;
        entry.FindPropertyRelative("interruptible").boolValue = false;
        entry.FindPropertyRelative("packLeaderOnly").boolValue = false;
        entry.FindPropertyRelative("leapHeight").floatValue = 0f;
        entry.FindPropertyRelative("burstCount").intValue = 1;
        return entry;
    }

    private static void Charge(SerializedProperty entry, float speed, float distance, float exposed, float leap = 0f)
    {
        entry.FindPropertyRelative("chargeSpeed").floatValue = speed;
        entry.FindPropertyRelative("chargeDistance").floatValue = distance;
        entry.FindPropertyRelative("exposedSeconds").floatValue = exposed;
        entry.FindPropertyRelative("leapHeight").floatValue = leap;
    }

    private static SerializedObject Monster(string name) => new SerializedObject(AssetDatabase.LoadAssetAtPath<MonsterDefinition>(Monsters + name + ".asset"));

    private static GameObject Enemy(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + name + ".prefab");

    // Its normal shot, fired in threes on a long cooldown: a thing to step out of, not more damage all the time.
    private static void Volley(string monster, string attackName, int tier, float damage, float cooldown, int priorityAbove)
    {
        SerializedObject so = Monster(monster);
        SerializedProperty list = so.FindProperty("attacks");
        SerializedProperty shot = null;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty candidate = list.GetArrayElementAtIndex(i);
            Object attack = candidate.FindPropertyRelative("attack").objectReferenceValue;
            if (attack != null && attack.name == attackName && candidate.FindPropertyRelative("fromTier").intValue == 0)
            {
                shot = candidate;
            }
        }

        var source = (AttackDefinition)shot.FindPropertyRelative("attack").objectReferenceValue;
        Object prefab = shot.FindPropertyRelative("projectilePrefab").objectReferenceValue;
        float min = shot.FindPropertyRelative("minimumRange").floatValue, max = shot.FindPropertyRelative("maximumRange").floatValue;
        float speed = shot.FindPropertyRelative("projectileSpeed").floatValue, radius = shot.FindPropertyRelative("projectileRadius").floatValue;
        int priority = shot.FindPropertyRelative("priority").intValue + priorityAbove;
        SerializedProperty volley = Action(so, source, MonsterAttackKind.Projectile, tier, damage, min, max, priority, cooldown);
        volley.FindPropertyRelative("projectilePrefab").objectReferenceValue = prefab;
        volley.FindPropertyRelative("projectileSpeed").floatValue = speed;
        volley.FindPropertyRelative("projectileRadius").floatValue = radius;
        volley.FindPropertyRelative("burstCount").intValue = 3;
        volley.FindPropertyRelative("burstInterval").floatValue = 0.28f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string TierTwo()
    {
        // Short wind-up to read it by, a fast leap, and a moment on the ground after to punish it.
        AttackDefinition pounce = Attack("WolfPounce", "MistwingCharge", "Pounce", 0.4f, 0.7f, 1.3f, 50f);
        SerializedObject wolf = Monster("ForestBittenWolf");
        Charge(Action(wolf, pounce, MonsterAttackKind.Charge, 2, 18f, 3.5f, 10f, 15, 7f), 18f, 10.5f, 0.9f, 1.4f);
        wolf.ApplyModifiedPropertiesWithoutUndo();
        Volley("BogSpitter", "VenomSpit", 2, 5f, 9f, 5);
        Volley("MarshLight", "WispBolt", 2, 7f, 8f, 5);
        return "tier II: wolves pounce, bog spitters and marsh lights fire volleys";
    }

    private static string TierThree()
    {
        AttackDefinition trample = Attack("BoarTrample", "BoarGore", "Trample", 0.6f, 1.1f, 2.8f, 150f);
        SerializedObject boar = Monster("BlightBoar");
        Action(boar, trample, MonsterAttackKind.Melee, 3, 22f, 0f, 2.4f, 15, 6f);
        boar.ApplyModifiedPropertiesWithoutUndo();

        // A plain attack is never used once a creature has actions, so the Guardian's own slam comes along as one.
        var guardianDefinition = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(Monsters + "RootGuardian.asset");
        SerializedObject guardian = Monster("RootGuardian");
        Action(guardian, guardianDefinition.Attack, MonsterAttackKind.Melee, 0, guardianDefinition.AttackDamage, 0f, guardianDefinition.Attack.Range * 0.85f, 10, guardianDefinition.AttackCooldown);
        AttackDefinition slam = Attack("GuardianGroundSlam", "GuardianSlam", "Ground Slam", 1.15f, 1.9f, 4.5f, 360f);
        Action(guardian, slam, MonsterAttackKind.Melee, 3, 52f, 0f, 3.6f, 15, 11f);
        guardian.ApplyModifiedPropertiesWithoutUndo();

        AttackDefinition rush = Attack("TrollRush", "BoarCharge", "Troll Rush", 0.7f, 0.95f, 1.5f, 40f);
        SerializedObject small = Monster("SmallMossTroll");
        Charge(Action(small, rush, MonsterAttackKind.Charge, 3, 12f, 3f, 8f, 15, 9f), 10f, 8f, 1.4f);
        small.ApplyModifiedPropertiesWithoutUndo();

        Volley("MossShaman", "ShamanStone", 3, 6f, 10f, 5);

        AttackDefinition charge = Attack("OldTrollCharge", "BoarCharge", "Troll Charge", 1f, 1.2f, 2.2f, 60f);
        SerializedObject troll = Monster("OldMossTroll");
        Charge(Action(troll, charge, MonsterAttackKind.Charge, 3, 80f, 4f, 11f, 15, 13f), 11f, 11f, 2.5f);
        troll.ApplyModifiedPropertiesWithoutUndo();
        return "tier III: boars trample, Guardians slam the ground, small trolls rush, Shamans fire volleys, the great troll charges";
    }

    private static string TierFour()
    {
        AttackDefinition cry = Attack("DraugWarCry", "WolfHowl", "War Cry", 0.8f, 1.3f, 1f, 360f);
        SerializedObject draug = Monster("Draug");
        SerializedProperty war = Action(draug, cry, MonsterAttackKind.Howl, 4, 0f, 0f, 9f, 25, 22f);
        war.FindPropertyRelative("buffRadius").floatValue = 13f;
        war.FindPropertyRelative("buffSeconds").floatValue = 10f;
        war.FindPropertyRelative("damageBonus").floatValue = 0.15f;
        war.FindPropertyRelative("interruptible").boolValue = true;
        draug.ApplyModifiedPropertiesWithoutUndo();

        var shaman = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(Monsters + "MossShaman.asset");
        MonsterAttack calling = shaman.Attacks.First(action => action.kind == MonsterAttackKind.Summon);
        AttackDefinition rise = Attack("GuardianCallRoots", calling.attack.name, "Call Roots", 0.9f, 1.4f, 1f, 360f);
        SerializedObject guardian = Monster("RootGuardian");
        SerializedProperty roots = Action(guardian, rise, MonsterAttackKind.Summon, 4, 0f, 2f, 11f, 22, 20f);
        roots.FindPropertyRelative("summonPrefab").objectReferenceValue = calling.summonPrefab;
        roots.FindPropertyRelative("summonCount").intValue = 2;
        roots.FindPropertyRelative("summonRadius").floatValue = 3f;
        roots.FindPropertyRelative("summonLimit").intValue = 4;
        roots.FindPropertyRelative("interruptible").boolValue = true;
        guardian.ApplyModifiedPropertiesWithoutUndo();

        AttackDefinition kin = Attack("OldTrollCallKin", calling.attack.name, "Calls Its Kin", 1.2f, 1.8f, 1f, 360f);
        SerializedObject troll = Monster("OldMossTroll");
        SerializedProperty call = Action(troll, kin, MonsterAttackKind.Summon, 4, 0f, 0f, 14f, 25, 32f);
        call.FindPropertyRelative("summonPrefab").objectReferenceValue = Enemy("SmallMossTroll");
        call.FindPropertyRelative("summonCount").intValue = 2;
        call.FindPropertyRelative("summonRadius").floatValue = 4f;
        call.FindPropertyRelative("summonLimit").intValue = 3;
        troll.ApplyModifiedPropertiesWithoutUndo();
        return "tier IV: Draugs rally with a war cry, Guardians call rootlings, the great troll calls its kin";
    }

    private static SerializedProperty List(SerializedObject raid, string name)
    {
        SerializedProperty lists = raid.FindProperty("spawnLists");
        for (int i = 0; i < lists.arraySize; i++)
        {
            SerializedProperty list = lists.GetArrayElementAtIndex(i);
            if (list.FindPropertyRelative("name").stringValue == name)
            {
                return list;
            }
        }

        throw new System.InvalidOperationException("No spawn list called " + name);
    }

    private static void Row(SerializedProperty entries, int index, GameObject prefab, float weight, int from, int to, int minimum = 0)
    {
        SerializedProperty row = entries.GetArrayElementAtIndex(index);
        row.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        row.FindPropertyRelative("weight").floatValue = weight;
        row.FindPropertyRelative("fromTier").intValue = from;
        row.FindPropertyRelative("toTier").intValue = to;
        row.FindPropertyRelative("minPerGroup").intValue = minimum;
        row.FindPropertyRelative("maxPerGroup").intValue = 0;
    }

    // Wolf packs took nearly half of a tier I raid once the stronger creatures were gone from it. They get a smaller share there.
    private static string TierOneMix()
    {
        var raid = new SerializedObject(AssetDatabase.LoadAssetAtPath<RaidDefinition>(RaidPath));
        SerializedProperty entries = List(raid, "Creatures").FindPropertyRelative("entries");
        GameObject pack = Enemy("WolfPack");
        int first = -1, second = -1;
        for (int i = 0; i < entries.arraySize; i++)
        {
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == pack)
            {
                if (first < 0)
                {
                    first = i;
                }
                else
                {
                    second = i;
                }
            }
        }

        if (second < 0)
        {
            entries.arraySize++;
            second = entries.arraySize - 1;
        }

        Row(entries, first, pack, 11f, 1, 1);
        Row(entries, second, pack, 22f, 2, 0);
        raid.ApplyModifiedPropertiesWithoutUndo();
        return "tier I: wolf packs at half their share";
    }

    private static string Hunters()
    {
        GameObject wolf = Enemy("ForestBittenWolf"), boar = Enemy("BlightBoar"), guardian = Enemy("RootGuardian"), draug = Enemy("Draug");
        (string list, (GameObject prefab, float weight, int from, int to)[] rows)[] wanted =
        {
            ("Stirring Prowlers", new[] { (wolf, 40f, 0, 2), (boar, 30f, 2, 3), (guardian, 20f, 3, 0), (draug, 10f, 4, 0) }),
            ("Hunters", new[] { (wolf, 50f, 0, 3), (guardian, 25f, 2, 2), (draug, 25f, 3, 0) }),
            ("Collapse Hunters", new[] { (wolf, 40f, 0, 2), (guardian, 30f, 2, 3), (draug, 30f, 3, 0) }),
            ("East Beacon Alarm", new[] { (wolf, 40f, 0, 3), (boar, 20f, 2, 2), (guardian, 20f, 3, 0), (draug, 20f, 4, 0) }),
        };
        var raid = new SerializedObject(AssetDatabase.LoadAssetAtPath<RaidDefinition>(RaidPath));
        foreach ((string name, (GameObject prefab, float weight, int from, int to)[] rows) in wanted)
        {
            SerializedProperty entries = List(raid, name).FindPropertyRelative("entries");
            entries.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++)
            {
                Row(entries, i, rows[i].prefab, rows[i].weight, rows[i].from, rows[i].to);
            }
        }

        raid.ApplyModifiedPropertiesWithoutUndo();
        return "hunters by tier: wolves at I, then boars and Guardians, then Draugs";
    }

    private static string Roamers()
    {
        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<SpawnTable>(RoamersPath));
        SerializedProperty entries = so.FindProperty("entries");
        foreach ((GameObject prefab, float weight, int from) in new[] { (Enemy("BlightBoar"), 1f, 2), (Enemy("RootGuardian"), 1f, 3), (Enemy("Draug"), 1f, 3) })
        {
            int found = -1;
            for (int i = 0; i < entries.arraySize; i++)
            {
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("prefab").objectReferenceValue == prefab)
                {
                    found = i;
                }
            }

            if (found < 0)
            {
                entries.arraySize++;
                found = entries.arraySize - 1;
            }

            Row(entries, found, prefab, weight, from, 0);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return "roamers: boars from II, Guardians and Draugs from III";
    }
}
