using UnityEditor;
using UnityEngine;

/// <summary>
/// Seven armor slots (`unity command run_script --file AgentScripts/MergeArmor.cs --entry MergeArmor.Apply`): the body
/// covers the legs, so the Woven Hide Jerkin becomes Woven Hide Armor with the leggings' stats and cost added, and the
/// leggings are retired (no slot, no recipe; old copies are worth a little). Re-running keeps the result.
/// </summary>
public static class MergeArmor
{
    public static string Apply()
    {
        const string items = "Assets/_Project/Data/Items/";
        var jerkin = AssetDatabase.LoadAssetAtPath<ItemDefinition>(items + "WovenHideJerkin.asset");
        var leggings = AssetDatabase.LoadAssetAtPath<ItemDefinition>(items + "WovenHideLeggings.asset");
        if (leggings.ArmorSlot == ArmorSlot.None)
        {
            return "already merged";
        }

        Set(jerkin, so =>
        {
            so.FindProperty("displayName").stringValue = "Woven Hide Armor";
            so.FindProperty("description").stringValue = "Layered hide over a nettle-fibre lining, from shoulders to boots. Takes the edge off claws and teeth.";
            SerializedProperty stats = so.FindProperty("statBonuses");
            stats.ClearArray();
            Add(stats, StatType.Armor, 17f);
            Add(stats, StatType.MaxHealth, 25f);
            Add(stats, (StatType)13, 1f);
            Add(stats, (StatType)10, 0.05f);
        });
        Set(leggings, so =>
        {
            so.FindProperty("armorSlot").intValue = (int)ArmorSlot.None;
            so.FindProperty("category").intValue = (int)ItemCategory.Valuable;
            so.FindProperty("description").stringValue = "Old hide leggings. Armor covers the legs now; worth a little as hide.";
        });

        var jerkinRecipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/WovenHideJerkin.asset");
        Set(jerkinRecipe, so =>
        {
            SerializedProperty cost = so.FindProperty("itemCost");
            cost.GetArrayElementAtIndex(0).FindPropertyRelative("amount").intValue = 3;
            cost.GetArrayElementAtIndex(1).FindPropertyRelative("amount").intValue = 6;
        });

        var legRecipe = AssetDatabase.LoadAssetAtPath<RecipeDefinition>("Assets/_Project/Data/Crafting/Recipes/WovenHideLeggings.asset");
        var book = AssetDatabase.LoadAssetAtPath<RecipeBook>("Assets/_Project/Data/Crafting/RecipeBook.asset");
        Set(book, so =>
        {
            SerializedProperty list = so.FindProperty("recipes");
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == legRecipe)
                {
                    list.DeleteArrayElementAtIndex(i);
                    if (i < list.arraySize && list.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    {
                        list.DeleteArrayElementAtIndex(i);
                    }
                }
            }
        });
        AssetDatabase.SaveAssets();
        return $"{jerkin.DisplayName}: {jerkin.StatBonuses.Count} stats; leggings retired; recipe removed from the book";
    }

    private static void Add(SerializedProperty list, StatType stat, float value)
    {
        list.InsertArrayElementAtIndex(list.arraySize);
        SerializedProperty entry = list.GetArrayElementAtIndex(list.arraySize - 1);
        entry.FindPropertyRelative("stat").intValue = (int)stat;
        entry.FindPropertyRelative("op").intValue = 0;
        entry.FindPropertyRelative("value").floatValue = value;
    }

    private static void Set(Object target, System.Action<SerializedObject> change)
    {
        var so = new SerializedObject(target);
        change(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }
}
