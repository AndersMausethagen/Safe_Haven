#error Retired 2026-11-14: this script rebuilt content from its own stored copy and could silently overwrite newer work (AgentScripts/RETIRED.md). The scenes and assets are the source of truth: make a small targeted edit with a before/after diff, and restore earlier states from git.
using UnityEditor;
using UnityEngine;

/// <summary>
/// The Haul Sack: far more carried than the Hide Rucksack and quick to make at the Loom, but few bag slots, slower, and it rattles
/// with every step. Puts PlayerBurden on the player prefab and makes the sack's sound, after Tools/Audio/work.py.
/// Run: unity command script run -- --file AgentScripts/BuildHaulSack.cs --entry BuildHaulSack.Run
/// </summary>
public static class BuildHaulSack
{
    private const string Items = "Assets/_Project/Data/Items/";
    private const string Recipes = "Assets/_Project/Data/Crafting/Recipes/";

    public static string Run()
    {
        AssetDatabase.Refresh();
        const string prefabPath = "Assets/_Project/Prefabs/Characters/PlayerArmature.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        bool added = root.GetComponent<PlayerBurden>() == null;
        if (added)
        {
            root.AddComponent<PlayerBurden>();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }

        PrefabUtility.UnloadPrefabContents(root);

        SoundDefinition rattle = Rattle();
        var rucksack = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "HideRucksack.asset");
        var sack = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "HaulSack.asset");
        if (sack == null)
        {
            sack = Object.Instantiate(rucksack);
            AssetDatabase.CreateAsset(sack, Items + "HaulSack.asset");
        }

        Edit(sack, so =>
        {
            so.FindProperty("id").stringValue = "haul_sack";
            so.FindProperty("displayName").stringValue = "Haul Sack";
            so.FindProperty("description").stringValue = "A great lumpy sack on a rope. Holds a cartload, and tells the whole forest you are coming.";
            so.FindProperty("rarity").intValue = 0;
            so.FindProperty("value").intValue = 12;
            so.FindProperty("carryCapacityBonus").intValue = 90;
            so.FindProperty("bagSlotBonus").intValue = 8;
            so.FindProperty("carryNoise").floatValue = 5f;
            so.FindProperty("carrySpeedScale").floatValue = 0.92f;
            so.FindProperty("carrySound").objectReferenceValue = rattle;
        });

        var pattern = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(Recipes + "HideRucksack.asset");
        var recipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(Recipes + "HaulSack.asset");
        if (recipe == null)
        {
            recipe = Object.Instantiate(pattern);
            AssetDatabase.CreateAsset(recipe, Recipes + "HaulSack.asset");
        }

        Edit(recipe, so =>
        {
            so.FindProperty("id").stringValue = "haul_sack";
            so.FindProperty("output").objectReferenceValue = sack;
            so.FindProperty("craftSeconds").floatValue = 12f;
            so.FindProperty("knownFromStart").boolValue = true;
            so.FindProperty("sortOrder").intValue = 11;
            SerializedProperty cost = so.FindProperty("itemCost");
            cost.arraySize = 2;
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "Linen.asset");
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 4;
            cost.GetArrayElementAtIndex(1).FindPropertyRelative("item").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Items + "NettleCord.asset");
            cost.GetArrayElementAtIndex(1).FindPropertyRelative("amount").intValue = 3;
        });

        foreach (string guid in AssetDatabase.FindAssets("t:ContentDatabase"))
        {
            var database = AssetDatabase.LoadAssetAtPath<ContentDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            database.CollectAll();
            EditorUtility.SetDirty(database);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:RecipeBook"))
        {
            var book = AssetDatabase.LoadAssetAtPath<RecipeBook>(AssetDatabase.GUIDToAssetPath(guid));
            book.CollectAll();
            EditorUtility.SetDirty(book);
        }

        AssetDatabase.SaveAssets();
        return $"burden {(added ? "added" : "already there")}; sack carries {sack.CarryCapacityBonus}, rucksack {rucksack.CarryCapacityBonus}";
    }

    private static SoundDefinition Rattle()
    {
        const string path = "Assets/_Project/Audio/Sounds/Library/Sack Rattle.asset";
        var sound = AssetDatabase.LoadAssetAtPath<SoundDefinition>(path);
        if (sound == null)
        {
            sound = ScriptableObject.CreateInstance<SoundDefinition>();
            AssetDatabase.CreateAsset(sound, path);
        }

        Edit(sound, so =>
        {
            so.FindProperty("displayName").stringValue = "Sack Rattle";
            SerializedProperty clips = so.FindProperty("clips");
            clips.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/_Project/Audio/SFX/Safe Haven Work/Sack Rattle {i + 1}.wav");
                clips.GetArrayElementAtIndex(i).objectReferenceValue = clip != null ? clip : throw new System.Exception("No rattle " + i);
            }

            so.FindProperty("channel").intValue = (int)AudioChannel.Effects;
            so.FindProperty("volume").floatValue = 0.34f;
            so.FindProperty("fullVolumeDistance").floatValue = 3f;
            so.FindProperty("audibleDistance").floatValue = 16f;
            so.FindProperty("maxAtOnce").intValue = 2;
        });
        return sound;
    }

    private static void Edit(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
