using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Companions (`unity command run_script --file AgentScripts/BuildCompanions.cs --entry BuildCompanions.Apply`): the
/// companion kinds (Data/Companions: Fighter and Archer), their health (Data/Combat/Stats/CompanionStats) and, in the
/// prologue, a Companion on every expedition member: Ally faction, a solid body enemies can see and hit, knocked down
/// instead of killed, their old place in the formation. Rescuable travellers in the raid become Ally as well. Safe to run
/// again. Reopens the scene that was open.
/// </summary>
public static class BuildCompanions
{
    private const string Folder = "Assets/_Project/Data/Companions";
    private const string StatsPath = "Assets/_Project/Data/Combat/Stats/CompanionStats.asset";
    private const string PrologueScene = "Assets/_Project/Scenes/Prologue_MistyWoods.unity";
    private const string RaidScene = "Assets/_Project/Scenes/Raids/Raid_MistyWoods.unity";

    // Where each walked behind the player before (PrologueCompanion's formation offsets).
    private static readonly Dictionary<string, Vector3> Formation = new Dictionary<string, Vector3>
    {
        ["Sigrid"] = new Vector3(-2f, 0f, -2f),
        ["Eirik"] = new Vector3(0f, 0f, -3.8f),
        ["Runa"] = new Vector3(2f, 0f, -3.8f),
        ["Ingrid"] = new Vector3(0f, 0f, -2f),
        ["Bjarne"] = new Vector3(2f, 0f, -2f),
        ["Liv"] = new Vector3(-2f, 0f, -5.6f),
        ["Tor"] = new Vector3(-2f, 0f, -3.8f),
    };

    public static string Apply()
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty) return "the open scene has unsaved changes";
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/_Project/Data", "Companions");

        Stats();
        var bow = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Project/Data/Combat/Weapons/CrudeWoodenBow.asset");
        Definition("Fighter", "MonsterStrike", null, 9f, 1.6f, null);
        Definition("Archer", "BowShot",
            AssetDatabase.LoadAssetAtPath<ItemDefinition>("Assets/_Project/Data/Items/CrudeStoneArrow.asset"), 0f, 2.2f,
            bow != null ? bow.SwingSound : null);
        AssetDatabase.SaveAssets();

        var report = new StringBuilder();
        Scene prologue = EditorSceneManager.OpenScene(PrologueScene);
        // Opening a scene unloads assets nothing references, so load the new ones only now.
        var stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(StatsPath);
        var fighter = AssetDatabase.LoadAssetAtPath<CompanionDefinition>($"{Folder}/Fighter.asset");
        var archer = AssetDatabase.LoadAssetAtPath<CompanionDefinition>($"{Folder}/Archer.asset");
        foreach (PrologueCompanion member in Object.FindObjectsByType<PrologueCompanion>(FindObjectsInactive.Include))
        {
            GameObject body = member.gameObject;
            string name = member.Resident != null ? member.Resident.DisplayName : body.name;
            Ally(body, stats);
            Companion companion = Ensure<Companion>(body);
            var data = new SerializedObject(companion);
            data.FindProperty("definition").objectReferenceValue = name == "Runa" ? archer : fighter;
            data.FindProperty("resident").objectReferenceValue = member.Resident;
            data.FindProperty("formationOffset").vector3Value = Formation.TryGetValue(name, out Vector3 offset) ? offset : Vector3.zero;
            data.ApplyModifiedPropertiesWithoutUndo();
            report.Append(name).Append(name == "Runa" ? " (archer) " : " (fighter) ");
        }

        EditorSceneManager.MarkSceneDirty(prologue);
        EditorSceneManager.SaveScene(prologue);

        Scene raid = EditorSceneManager.OpenScene(RaidScene);
        int travellers = 0;
        foreach (RescuableTraveler traveller in Object.FindObjectsByType<RescuableTraveler>(FindObjectsInactive.Include))
        {
            if (traveller.TryGetComponent(out Combatant combatant))
            {
                var data = new SerializedObject(combatant);
                data.FindProperty("faction").intValue = (int)Faction.Ally;
                data.ApplyModifiedPropertiesWithoutUndo();
                travellers++;
            }
        }

        if (travellers > 0)
        {
            EditorSceneManager.MarkSceneDirty(raid);
            EditorSceneManager.SaveScene(raid);
        }

        if (!string.IsNullOrEmpty(previous) && previous != SceneManager.GetActiveScene().path)
        {
            EditorSceneManager.OpenScene(previous);
        }

        return $"companions: {report}; travellers made Ally: {travellers}";
    }

    // Ally faction, health that falls to knocked-down rather than destroyed, a body enemies can see and hit.
    private static void Ally(GameObject body, CombatStatsDefinition stats)
    {
        var combatant = new SerializedObject(Ensure<Combatant>(body));
        combatant.FindProperty("faction").intValue = (int)Faction.Ally;
        combatant.FindProperty("baseStats").objectReferenceValue = stats;
        combatant.ApplyModifiedPropertiesWithoutUndo();

        var health = new SerializedObject(Ensure<Damageable>(body));
        health.FindProperty("destroyOnDeath").boolValue = false;
        health.ApplyModifiedPropertiesWithoutUndo();

        foreach (CapsuleCollider collider in body.GetComponents<CapsuleCollider>())
        {
            collider.isTrigger = false;
        }
    }

    private static CombatStatsDefinition Stats()
    {
        var stats = AssetDatabase.LoadAssetAtPath<CombatStatsDefinition>(StatsPath);
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<CombatStatsDefinition>();
            AssetDatabase.CreateAsset(stats, StatsPath);
        }

        var data = new SerializedObject(stats);
        SerializedProperty list = data.FindProperty("stats");
        list.ClearArray();
        Stat(list, StatType.MaxHealth, 120f);
        Stat(list, StatType.PhysicalCritChance, 0.05f);
        Stat(list, StatType.PhysicalCritDamage, 0.5f);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(stats);
        return stats;
    }

    private static void Stat(SerializedProperty list, StatType stat, float value)
    {
        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("stat").intValue = (int)stat;
        entry.FindPropertyRelative("value").floatValue = value;
    }

    private static CompanionDefinition Definition(string name, string attack, ItemDefinition arrow, float weaponDamage,
        float interval, SoundDefinition sound)
    {
        string path = $"{Folder}/{name}.asset";
        var definition = AssetDatabase.LoadAssetAtPath<CompanionDefinition>(path);
        if (definition == null)
        {
            definition = ScriptableObject.CreateInstance<CompanionDefinition>();
            AssetDatabase.CreateAsset(definition, path);
        }

        var data = new SerializedObject(definition);
        data.FindProperty("attack").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<AttackDefinition>($"Assets/_Project/Data/Combat/Attacks/{attack}.asset");
        data.FindProperty("weaponDamage").floatValue = weaponDamage;
        data.FindProperty("attackInterval").floatValue = interval;
        data.FindProperty("arrow").objectReferenceValue = arrow;
        data.FindProperty("attackSound").objectReferenceValue = sound;
        // The prologue's rally points (camp, portal) are Stay orders, so they fight across the whole clearing.
        data.FindProperty("holdRadius").floatValue = 12f;
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        return definition;
    }

    private static T Ensure<T>(GameObject body) where T : Component =>
        body.TryGetComponent(out T existing) ? existing : body.AddComponent<T>();
}
