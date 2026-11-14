#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEngine;

/// <summary>The data behind how creatures live and fight, companions speak and residents spend the day (`--entry BuildBetterAI.Run`).
/// All of it is editable afterwards: Monsters › Idle Life and In a Fight, the pack prefabs, Companions, Residents and Jobs, Settlement Settings › Daily Life.</summary>
public static class BuildBetterAI
{
    private const string Monsters = "Assets/_Project/Data/Monsters/";
    private const string Emotes = "Assets/_Project/Data/Characters/Emotes/";

    public static string Run()
    {
        // Wander radius, graze, sleep, walk share, flank; animals graze and doze, the dead just shift about, great beasts keep their own business.
        foreach ((string file, float wander, float graze, float sleep, float walk, float flank) in new[]
        {
            ("BlightBoar", 8f, 0.6f, 0.1f, 0.4f, 0.4f),
            ("ForestBittenWolf", 10f, 0f, 0.3f, 0.45f, 1f),
            ("Rootling", 3f, 0f, 0.5f, 0.5f, 0.7f),
            ("SmallMossTroll", 6f, 0f, 0.25f, 0.5f, 0f),
            ("BarrowShieldbearer", 5f, 0f, 0f, 0.4f, 0.4f),
            ("Draug", 6f, 0f, 0f, 0.35f, 0.5f),
            ("BogSpitter", 4f, 0f, 0.3f, 0.4f, 0f),
            ("MarshLight", 9f, 0f, 0f, 0.5f, 0f),
            ("Mistwing", 12f, 0f, 0f, 0.5f, 0f),
            ("MossShaman", 5f, 0f, 0f, 0.4f, 0f),
        })
        {
            var so = new SerializedObject(Load<MonsterDefinition>(Monsters + file + ".asset"));
            so.FindProperty("idleWanderRadius").floatValue = wander;
            so.FindProperty("idleGrazeChance").floatValue = graze;
            so.FindProperty("idleSleepChance").floatValue = sleep;
            so.FindProperty("idleWalkSpeed").floatValue = walk;
            so.FindProperty("flankSpread").floatValue = flank;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(so.targetObject);
        }

        // Only some aim ahead, and not by much: walking sideways should still beat most things.
        Lead("SmallMossTroll", MonsterAttackKind.Projectile, 0.4f);
        Lead("BogSpitter", MonsterAttackKind.Projectile, 0.4f);
        Lead("MossShaman", MonsterAttackKind.Projectile, 0.3f);
        Lead("ForestBittenWolf", MonsterAttackKind.Charge, 0.35f);
        Lead("Mistwing", MonsterAttackKind.Charge, 0.25f);

        // Wolves have nerve only as a pack. A swarm of rootlings has none to lose.
        GameObject pack = PrefabUtility.LoadPrefabContents("Assets/_Project/Prefabs/Enemies/WolfPack.prefab");
        var packData = new SerializedObject(pack.GetComponentInChildren<MonsterPack>(true));
        packData.FindProperty("breaksWithItsLeader").boolValue = true;
        packData.FindProperty("breaksAtLosses").floatValue = 0.6f;
        packData.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(pack, "Assets/_Project/Prefabs/Enemies/WolfPack.prefab");
        PrefabUtility.UnloadPrefabContents(pack);

        Lines("Assets/_Project/Data/Companions/Fighter.asset", "fightLines",
            new[] { "Here they come!", "Weapons up!" },
            new[] { "Behind you!", "Watch your back!" },
            new[] { "I can't take much more of this.", "I'm hurt!" },
            new[] { "That's the last of them.", "Clear. Get your breath." });
        Lines("Assets/_Project/Data/Companions/Archer.asset", "fightLines",
            new[] { "Contact! I've got the range.", "Moving, there!" },
            new[] { "Behind you! Turn!", "On your back!" },
            new[] { "They're too close, I need room!", "I'm bleeding!" },
            new[] { "Nothing else moving.", "All clear." });
        Lines("Assets/_Project/Data/Prologue/Residents/Sigrid.asset", "fightLines",
            new[] { "Stand together! Nobody runs!", "Form up on me!" },
            new[] { "Behind you! Move!" },
            new[] { "Still standing. Barely." },
            new[] { "Count heads. Everyone still here?", "Good. Move before more come." });
        Lines("Assets/_Project/Data/Prologue/Residents/Eirik.asset", "fightLines",
            new[] { "Finally, something I can hit.", "Stay behind my shield, if you can find it." },
            new[] { "Behind you, you fool!" },
            new[] { "Twenty winters, and it still hurts." },
            new[] { "Is that all? I was only warming up.", "Clean your blade. It's a bad habit to forget." });
        Lines("Assets/_Project/Data/Prologue/Residents/Runa.asset", "fightLines",
            new[] { "There! In the trees!", "I see them. Take the near ones." },
            new[] { "Behind you! Turn around!" },
            new[] { "Too close, too close!" },
            new[] { "Nothing else on the wind.", "That's all of them. I think." });

        // The day: what they do at each job and at the fire.
        foreach ((string job, string gesture) in new[]
        {
            ("Smith", "Forging"), ("Carpenter", "Forging"), ("Mason", "Forging"), ("Alchemist", "Use"), ("Cook", "Use"), ("Farmer", "Use"),
            ("Hunter", "Use"), ("Weaver", "Use"), ("RuneScholar", "Read"), ("Tracker", "Read"), ("Leader", "Talk"), ("Warden", "Talk2H"),
        })
        {
            foreach (string guid in AssetDatabase.FindAssets(job + " t:JobDefinition"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != job) continue;
                var so = new SerializedObject(Load<JobDefinition>(path));
                so.FindProperty("workGesture").objectReferenceValue = Load<EmoteDefinition>(Emotes + gesture + ".asset");
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(so.targetObject);
            }
        }

        var settings = new SerializedObject(SettlementSettings.Current);
        settings.FindProperty("residentGatheringPlace").objectReferenceValue = Load<BuildingDefinition>("Assets/_Project/Data/Buildings/Campfire.asset");
        settings.FindProperty("residentMealGesture").objectReferenceValue = Load<EmoteDefinition>(Emotes + "SitGround.asset");
        settings.FindProperty("residentSleepGesture").objectReferenceValue = Load<EmoteDefinition>(Emotes + "Sleep.asset");
        SerializedProperty fire = settings.FindProperty("residentFireGestures");
        string[] atTheFire = { "SitGround", "Talk", "Laugh", "TalkQuestion" };
        fire.arraySize = atTheFire.Length;
        for (int i = 0; i < atTheFire.Length; i++)
        {
            fire.GetArrayElementAtIndex(i).objectReferenceValue = Load<EmoteDefinition>(Emotes + atTheFire[i] + ".asset");
        }

        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings.targetObject);
        AssetDatabase.SaveAssets();
        return "creatures, companions and residents set";
    }

    private static void Lead(string monster, MonsterAttackKind kind, float lead)
    {
        var so = new SerializedObject(Load<MonsterDefinition>(Monsters + monster + ".asset"));
        SerializedProperty attacks = so.FindProperty("attacks");
        for (int i = 0; i < attacks.arraySize; i++)
        {
            SerializedProperty attack = attacks.GetArrayElementAtIndex(i);
            if (attack.FindPropertyRelative("kind").intValue == (int)kind)
            {
                attack.FindPropertyRelative("leadTarget").floatValue = lead;
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(so.targetObject);
    }

    private static void Lines(string path, string field, string[] spotted, string[] behind, string[] hurt, string[] won)
    {
        var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
        SerializedProperty lines = so.FindProperty(field);
        Fill(lines.FindPropertyRelative("spotted"), spotted);
        Fill(lines.FindPropertyRelative("behindYou"), behind);
        Fill(lines.FindPropertyRelative("hurt"), hurt);
        Fill(lines.FindPropertyRelative("won"), won);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(so.targetObject);
    }

    private static void Fill(SerializedProperty list, string[] values)
    {
        list.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            list.GetArrayElementAtIndex(i).stringValue = values[i];
        }
    }

    private static T Load<T>(string path) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        return asset != null ? asset : throw new System.Exception("Missing: " + path);
    }
}
