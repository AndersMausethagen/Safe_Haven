using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every recipe and every buildable structure in the game. The crafting UI lists recipes from it and the build menu lists
/// its buildings; in the Editor new ones are added automatically (see ResourceCatalogPostprocessor). Which recipes and
/// buildings a player actually knows is runtime state, kept by PlayerCrafting.
/// </summary>
[CreateAssetMenu(fileName = "RecipeBook", menuName = "Safe Haven/Crafting/Recipe Book")]
public sealed class RecipeBook : ScriptableObject
{
    [SerializeField] private List<RecipeDefinition> recipes = new List<RecipeDefinition>();
    [SerializeField] private List<BuildingDefinition> buildings = new List<BuildingDefinition>();

    public IReadOnlyList<RecipeDefinition> Recipes => recipes;
    public IReadOnlyList<BuildingDefinition> Buildings => buildings;

    /// <summary>Recipes in display order: by station, then tier, then sort order, then name.</summary>
    public static int CompareForDisplay(RecipeDefinition a, RecipeDefinition b)
    {
        int result = a.Station.CompareTo(b.Station);
        if (result == 0)
        {
            result = a.Tier.CompareTo(b.Tier);
        }

        if (result == 0)
        {
            result = a.SortOrder.CompareTo(b.SortOrder);
        }

        return result != 0 ? result : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Buildings in build-menu order: sort order, then name.</summary>
    public static int CompareForDisplay(BuildingDefinition a, BuildingDefinition b)
    {
        int result = a.SortOrder.CompareTo(b.SortOrder);
        return result != 0 ? result : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Editor only: adds every recipe and building in the project that isn't listed and drops deleted ones. Returns how
    /// many were added.
    /// </summary>
    public int CollectAll()
    {
        int removed = recipes.RemoveAll(entry => entry == null) + buildings.RemoveAll(entry => entry == null);
        int added = Collect(recipes) + Collect(buildings);
        if (added > 0 || removed > 0)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }

        return added;
    }

    private static int Collect<T>(List<T> into) where T : ScriptableObject
    {
        int added = 0;
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null && !into.Contains(asset))
            {
                into.Add(asset);
                added++;
            }
        }

        return added;
    }
#endif
}