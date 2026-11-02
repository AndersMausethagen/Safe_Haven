using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Elite creatures for the Misty Woods (`unity command script run --file AgentScripts/BuildRaidElites.cs --entry BuildRaidElites.Apply`): the affixes, how many
/// elites each tier has and what they pay. Each affix has an answer: see Docs/GearScoreAndRaidTiers.md. Run after BuildRaidTiers. Safe to run again.
/// </summary>
public static class BuildRaidElites
{
    private const string Folder = "Assets/_Project/Data/Raids/Elites/";
    private const string RaidPath = "Assets/_Project/Data/Raids/MistyWoods.asset";
    private const string Attacks = "Assets/_Project/Data/Combat/Attacks/";

    public static string Apply()
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            return "the open scene has unsaved changes";
        }

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Raids/Elites"))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Data/Raids", "Elites");
        }

        var chill = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/Combat/Status/Chill.asset");
        AttackDefinition blast = Blast();
        var affixes = new List<EliteAffixDefinition>
        {
            Affix("Ironhide", "Ironhide", "Big, slow and hard to hurt. Answer: use the slowness: keep moving and hit it when it has swung.", new Color(0.72f, 0.78f, 0.9f), 1, 1f, 1.8f, 1f, 0.9f, 1f, 30f, 1.2f),
            Affix("Frenzied", "Frenzied", "Fast and hardly stops between strikes, though it hits no harder and its wind-ups are as long. Answer: dodge on the wind-up, don't trade blows.", new Color(1f, 0.58f, 0.32f), 1, 1f, 1.2f, 1f, 1.35f, 0.65f, 0f, 1f),
            Affix("Regrowing", "Regrowing", "Grows back once it goes unhit for a moment. Answer: stay on it and don't back off to rest.", new Color(0.4f, 0.95f, 0.4f), 2, 1f, 1.4f, 1f, 1f, 1f, 0f, 1f, new RegrowthBehaviour()),
            Affix("Frostbound", "Frostbound", "Its hits chill, slowing the player. Answer: don't get hit twice, keep something warm to cure it, or fight it at range.", new Color(0.45f, 0.85f, 1f), 2, 1f, 1.4f, 1.1f, 1f, 1f, 0f, 1f, Status(chill)),
            Affix("Vengeful", "Vengeful", "Bursts when it dies, after a circle grows on the ground. Answer: kill it, then step out.", new Color(1f, 0.78f, 0.25f), 3, 1f, 1.3f, 1f, 1f, 1f, 0f, 1f, Burst(blast)),
            Affix("Mistcloaked", "Mistcloaked", "Walks wrapped in fog, so it is hard to see until it is close. Answer: back onto open ground and watch for its ring.", new Color(0.72f, 0.6f, 1f), 3, 1f, 1.4f, 1f, 1f, 1f, 0f, 1f, new MistCloakBehaviour()),
            Affix("Rallying", "Rallying", "Everything near it hits harder while it lives, and a faint circle shows how far. Answer: kill it first.", new Color(1f, 0.95f, 0.55f), 4, 1f, 1.5f, 1f, 1f, 1f, 0f, 1f, new RallyingBehaviour()),
        };
        AssetDatabase.SaveAssets();

        var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<RaidDefinition>(RaidPath));
        SerializedProperty pool = so.FindProperty("eliteAffixes");
        pool.arraySize = affixes.Count;
        for (int i = 0; i < affixes.Count; i++)
        {
            pool.GetArrayElementAtIndex(i).objectReferenceValue = affixes[i];
        }

        ResourceDefinition essence = SettlementSettings.Current.CursedEssence;
        Tier(so, 0, new Vector2Int(1, 1), 1, 0f, 0f, essence, 2, 3, ("CopperOre", 3, 5, 1f));
        Tier(so, 1, new Vector2Int(1, 2), 1, 0f, 0.05f, essence, 3, 4, ("IronOre", 3, 5, 1f));
        Tier(so, 2, new Vector2Int(2, 3), 1, 0.15f, 0.1f, essence, 4, 6, ("MoonsilverOre", 2, 4, 1f));
        Tier(so, 3, new Vector2Int(3, 4), 2, 0.25f, 0.15f, essence, 6, 8, ("MoonsilverOre", 3, 5, 1f), ("Trollstone", 1, 1, 0.25f));
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (string monster in new[] { "OldMossTroll", "Rootling" })
        {
            var creature = new SerializedObject(AssetDatabase.LoadAssetAtPath<MonsterDefinition>($"Assets/_Project/Data/Monsters/{monster}.asset"));
            creature.FindProperty("neverElite").boolValue = true;
            creature.ApplyModifiedPropertiesWithoutUndo();
        }

        AssetDatabase.SaveAssets();
        return $"{affixes.Count} affixes; elites by tier: 1, 1-2, 2-3, 3-4 with two affixes each at IV";
    }

    private static AttackDefinition Blast()
    {
        string path = Attacks + "EliteBurst.asset";
        if (AssetDatabase.LoadAssetAtPath<AttackDefinition>(path) == null)
        {
            AssetDatabase.CopyAsset(Attacks + "GuardianSlam.asset", path);
        }

        var attack = AssetDatabase.LoadAssetAtPath<AttackDefinition>(path);
        var so = new SerializedObject(attack);
        so.FindProperty("displayName").stringValue = "Vengeful Burst";
        so.FindProperty("range").floatValue = 3.5f;
        so.FindProperty("arc").floatValue = 360f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return attack;
    }

    private static StatusOnHitBehaviour Status(StatusEffectDefinition effect)
    {
        var behaviour = new StatusOnHitBehaviour();
        typeof(StatusOnHitBehaviour).GetField("effect", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(behaviour, effect);
        return behaviour;
    }

    private static BurstOnDeathBehaviour Burst(AttackDefinition attack)
    {
        var behaviour = new BurstOnDeathBehaviour();
        typeof(BurstOnDeathBehaviour).GetField("attack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(behaviour, attack);
        return behaviour;
    }

    private static EliteAffixDefinition Affix(string file, string label, string description, Color colour, int from, float weight, float health, float damage, float speed, float cooldowns, float armor, float size, params EliteBehaviour[] behaviours)
    {
        string path = Folder + file + ".asset";
        var affix = AssetDatabase.LoadAssetAtPath<EliteAffixDefinition>(path);
        if (affix == null)
        {
            affix = ScriptableObject.CreateInstance<EliteAffixDefinition>();
            AssetDatabase.CreateAsset(affix, path);
        }

        var so = new SerializedObject(affix);
        so.FindProperty("displayName").stringValue = label;
        so.FindProperty("description").stringValue = description;
        so.FindProperty("colour").colorValue = colour;
        so.FindProperty("fromTier").intValue = from;
        so.FindProperty("weight").floatValue = weight;
        so.FindProperty("health").floatValue = health;
        so.FindProperty("damage").floatValue = damage;
        so.FindProperty("speed").floatValue = speed;
        so.FindProperty("cooldowns").floatValue = cooldowns;
        so.FindProperty("armor").floatValue = armor;
        so.FindProperty("size").floatValue = size;
        SerializedProperty list = so.FindProperty("behaviours");
        list.arraySize = behaviours.Length;
        for (int i = 0; i < behaviours.Length; i++)
        {
            list.GetArrayElementAtIndex(i).managedReferenceValue = behaviours[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(affix);
        return affix;
    }

    private static ItemDefinition Item(string name)
    {
        string guid = AssetDatabase.FindAssets("t:ItemDefinition " + name).First(found => System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(found)) == name);
        return AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
    }

    private static void Tier(SerializedObject raid, int index, Vector2Int count, int affixes, float hunterChance, float relicChance, ResourceDefinition essence, int essenceMin, int essenceMax, params (string item, int min, int max, float chance)[] items)
    {
        SerializedProperty row = raid.FindProperty("difficulties").GetArrayElementAtIndex(index);
        row.FindPropertyRelative("eliteCount").vector2IntValue = count;
        row.FindPropertyRelative("eliteAffixes").intValue = affixes;
        row.FindPropertyRelative("hunterEliteChance").floatValue = hunterChance;
        row.FindPropertyRelative("eliteRelicChance").floatValue = relicChance;
        SerializedProperty entries = row.FindPropertyRelative("eliteLoot").FindPropertyRelative("entries");
        entries.arraySize = items.Length + 1;
        for (int i = 0; i < items.Length; i++)
        {
            Entry(entries.GetArrayElementAtIndex(i), Item(items[i].item), null, items[i].min, items[i].max, items[i].chance);
        }

        Entry(entries.GetArrayElementAtIndex(items.Length), null, essence, essenceMin, essenceMax, 1f);
    }

    private static void Entry(SerializedProperty entry, ItemDefinition item, ResourceDefinition resource, int min, int max, float chance)
    {
        entry.FindPropertyRelative("item").objectReferenceValue = item;
        entry.FindPropertyRelative("resource").objectReferenceValue = resource;
        entry.FindPropertyRelative("minQuantity").intValue = min;
        entry.FindPropertyRelative("maxQuantity").intValue = max;
        entry.FindPropertyRelative("probability").floatValue = chance;
    }
}
