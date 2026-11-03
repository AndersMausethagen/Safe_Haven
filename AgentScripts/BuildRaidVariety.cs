using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// More than wolves on a first raid. Playtest logs showed wolves as 38 of 50 kills in raids: on tier I every list of hunters held nothing else,
/// as the others started at tier II, and a pack counts three times. Hunters now come as boars, swarms and mistwings too, all creatures tier I
/// already meets. Safe to run again.
/// Run: unity command script run -- --file AgentScripts/BuildRaidVariety.cs --entry BuildRaidVariety.Run
/// </summary>
public static class BuildRaidVariety
{
    private const string Enemies = "Assets/_Project/Prefabs/Enemies/";

    public static string Run()
    {
        var raid = AssetDatabase.LoadAssetAtPath<RaidDefinition>("Assets/_Project/Data/Raids/MistyWoods.asset");
        var so = new SerializedObject(raid);
        SerializedProperty lists = so.FindProperty("spawnLists");
        string log = "";
        for (int i = 0; i < lists.arraySize; i++)
        {
            SerializedProperty list = lists.GetArrayElementAtIndex(i);
            string name = list.FindPropertyRelative("name").stringValue;
            SerializedProperty entries = list.FindPropertyRelative("entries");
            switch (name)
            {
                case "Creatures":
                    // A pack is three bodies against one: fewer of them, so the rest of the forest gets a look in.
                    SetWeight(entries, "WolfPack", 7f, 1);
                    break;
                case "Stirring Prowlers":
                    Add(entries, "BlightBoar", 30f, 1, 1);
                    Add(entries, "RootlingSwarm", 25f, 1, 2);
                    break;
                case "Hunters":
                    Add(entries, "BlightBoar", 30f, 1, 1);
                    Add(entries, "Mistwing", 20f, 1, 2);
                    break;
                case "Collapse Hunters":
                    Add(entries, "RootlingSwarm", 30f, 1, 2);
                    Add(entries, "BlightBoar", 25f, 1, 1);
                    break;
                case "East Beacon Alarm":
                    Add(entries, "BlightBoar", 30f, 1, 1);
                    Add(entries, "Mistwing", 15f, 1, 2);
                    break;
                default:
                    continue;
            }

            log += $"{name}: {entries.arraySize}; ";
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(raid);
        AssetDatabase.SaveAssets();
        return log;
    }

    private static void SetWeight(SerializedProperty entries, string prefab, float weight, int tier)
    {
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            if (entry.FindPropertyRelative("prefab").objectReferenceValue?.name == prefab && entry.FindPropertyRelative("fromTier").intValue == tier)
            {
                entry.FindPropertyRelative("weight").floatValue = weight;
            }
        }
    }

    // A row for these tiers, or the same row again if it is already there.
    private static void Add(SerializedProperty entries, string prefab, float weight, int fromTier, int toTier)
    {
        SerializedProperty entry = null;
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty one = entries.GetArrayElementAtIndex(i);
            if (one.FindPropertyRelative("prefab").objectReferenceValue?.name == prefab && one.FindPropertyRelative("fromTier").intValue == fromTier
                && one.FindPropertyRelative("toTier").intValue == toTier)
            {
                entry = one;
            }
        }

        if (entry == null)
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
            entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
            entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>($"{Enemies}{prefab}.prefab") ?? throw new System.Exception("No creature: " + prefab);
            entry.FindPropertyRelative("maxPerGroup").intValue = 0;
            entry.FindPropertyRelative("minPerGroup").intValue = 0;
        }

        entry.FindPropertyRelative("weight").floatValue = weight;
        entry.FindPropertyRelative("fromTier").intValue = fromTier;
        entry.FindPropertyRelative("toTier").intValue = toTier;
    }
}
