#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// The prologue's boar, made gentler (`unity command script run --file AgentScripts/BuildPrologueBoar.cs --entry BuildPrologueBoar.Apply`,
/// with Prologue_MistyWoods open). The user found the three blight boar in the meadow too tough for the start of the game:
/// - Data/Monsters/Boar: an ordinary boar, the Blight Boar's body and habits at about half its strength (BoarStats: 70
///   health, 2 armour; 6 damage, 40 poise), plain dark eyes (Eye Boar Plain) instead of the blight's green.
/// - The prologue's boar prefab (ExpeditionBoar: the meadow's and the road's running boars) is that boar.
/// Listed in the journal and the cheat menu like the others. Re-runnable.
/// Since 2026-11-14 the hunt itself (Meadow Boars spawning one, Hunt for the Pot asking for one, Runa's lines) belongs to
/// BuildPrologueMeadow (Data and Moments): two builders writing the same quest let a Data run put the blight boar back.
/// </summary>
public static class BuildPrologueBoar
{
    private const string Blight = "Assets/_Project/Data/Monsters/BlightBoar.asset";
    private const string BoarPath = "Assets/_Project/Data/Monsters/Boar.asset";
    private const string BlightStats = "Assets/_Project/Data/Combat/Stats/BlightBoarStats.asset";
    private const string BoarStatsPath = "Assets/_Project/Data/Combat/Stats/BoarStats.asset";
    private const string BlightEyes = "Assets/_Project/Art/Enemies/Downloaded/Gear/Eye Boar.mat";
    private const string PlainEyes = "Assets/_Project/Art/Enemies/Downloaded/Gear/Eye Boar Plain.mat";
    private const string Prefab = "Assets/_Project/Data/Prologue/ExpeditionBoar.prefab";
    private const string Quest = "Assets/_Project/Data/Prologue/Quests/HuntForThePot.asset";

    public static string Apply()
    {
        var report = new System.Collections.Generic.List<string>();
        MonsterDefinition boar = Definition(report);
        Material eyes = Eyes();
        Prefabs(boar, eyes, report);
        List(boar, "Assets/_Project/Resources/JournalSettings.asset", report);
        List(boar, "Assets/_Project/Resources/CheatSettings.asset", report);
        AssetDatabase.SaveAssets();
        return string.Join("; ", report);
    }

    private static MonsterDefinition Definition(System.Collections.Generic.List<string> report)
    {
        if (AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(BoarStatsPath) == null)
        {
            AssetDatabase.CopyAsset(BlightStats, BoarStatsPath);
        }

        var stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(BoarStatsPath);
        var statsSo = new SerializedObject(stats);
        SerializedProperty list = statsSo.FindProperty("stats");
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            var stat = (StatType)entry.FindPropertyRelative("stat").intValue;
            if (stat == StatType.MaxHealth) entry.FindPropertyRelative("value").floatValue = 70f;
            if (stat == StatType.Armor) entry.FindPropertyRelative("value").floatValue = 2f;
        }

        statsSo.ApplyModifiedPropertiesWithoutUndo();

        if (AssetDatabase.LoadAssetAtPath<MonsterDefinition>(BoarPath) == null)
        {
            AssetDatabase.CopyAsset(Blight, BoarPath);
        }

        var boar = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(BoarPath);
        var so = new SerializedObject(boar);
        so.FindProperty("id").stringValue = "Boar";
        so.FindProperty("displayName").stringValue = "Boar";
        so.FindProperty("description").stringValue = "A wild boar from the edge of the woods. It will let you pass at a distance. Step too close and it comes straight at you, and stands blown and open when the charge is spent.";
        so.FindProperty("journalLore").stringValue = "An ordinary boar, the kind that roots along the woods' edge. Good meat, a bad temper.";
        so.FindProperty("journalAdvice").stringValue = "It leaves you alone at a distance. Step too close and it charges straight at you, then stands blown and open when the charge is spent. Roll aside, and strike it then.";
        so.FindProperty("journalHabitat").stringValue = "Meadows and the woods' edge";
        so.FindProperty("combatStats").objectReferenceValue = stats;
        so.FindProperty("attackDamage").floatValue = 6f;
        so.FindProperty("poise").floatValue = 40f;
        so.FindProperty("moveSpeed").floatValue = 4.2f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(boar);
        report.Add("Boar: 70 health, 2 armour, 6 damage");
        return boar;
    }

    private static Material Eyes()
    {
        if (AssetDatabase.LoadAssetAtPath<Material>(PlainEyes) == null)
        {
            AssetDatabase.CopyAsset(BlightEyes, PlainEyes);
        }

        var eyes = AssetDatabase.LoadAssetAtPath<Material>(PlainEyes);
        eyes.SetColor("_BaseColor", new Color(0.06f, 0.045f, 0.035f, 1f));
        eyes.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(eyes);
        return eyes;
    }

    private static void Prefabs(MonsterDefinition boar, Material eyes, System.Collections.Generic.List<string> report)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var ai = root.GetComponent<MonsterAI>();
            var so = new SerializedObject(ai);
            so.FindProperty("definition").objectReferenceValue = boar;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Health and armour come from the Combatant (the variant inherits BlightBoarStats from BlightBoar.prefab).
            var combatant = new SerializedObject(root.GetComponent<Combatant>());
            combatant.FindProperty("baseStats").objectReferenceValue = boar.CombatStats;
            combatant.ApplyModifiedPropertiesWithoutUndo();
            int painted = 0;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.name == "Eye")
                {
                    renderer.sharedMaterials = System.Array.ConvertAll(renderer.sharedMaterials, m => eyes);
                    painted++;
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            report.Add($"prologue boar prefab is a Boar, {painted} plain eyes");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }



    // Adds the boar next to the Blight Boar in any list of creatures on a settings asset.
    private static void List(MonsterDefinition boar, string path, System.Collections.Generic.List<string> report)
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        var blight = AssetDatabase.LoadAssetAtPath<MonsterDefinition>(Blight);
        var so = new SerializedObject(asset);
        SerializedProperty it = so.GetIterator();
        while (it.NextVisible(true))
        {
            if (!it.isArray || it.propertyType != SerializedPropertyType.Generic || it.arrayElementType != "PPtr<$MonsterDefinition>")
            {
                continue;
            }

            bool hasBlight = false, hasBoar = false;
            for (int i = 0; i < it.arraySize; i++)
            {
                Object value = it.GetArrayElementAtIndex(i).objectReferenceValue;
                hasBlight |= value == blight;
                hasBoar |= value == boar;
            }

            if (hasBlight && !hasBoar)
            {
                it.InsertArrayElementAtIndex(it.arraySize);
                it.GetArrayElementAtIndex(it.arraySize - 1).objectReferenceValue = boar;
                report.Add($"listed in {asset.name}.{it.name}");
            }
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }
}
