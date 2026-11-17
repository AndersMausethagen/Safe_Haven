using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What an item is for, worked out from the game's recipes and buildings rather than written on each item (ARC Raiders' lesson in
/// Docs/LootAndDropsResearch_2026-11-17.md §6: "every drop needs a 'for what', shown on the item"): the recipes that take it and the
/// buildings and building levels that cost it, plus what breaking it down gives. Shown on item tooltips (ItemTooltipView). Built once
/// for a list of recipes and buildings and kept, since those don't change while playing.
/// </summary>
public static class ItemUses
{
    private static object builtFor;
    private static readonly Dictionary<ItemDefinition, List<string>> Uses = new Dictionary<ItemDefinition, List<string>>();

    /// <summary>The recipes and buildings that use the item, by name ("Moss Poultice", "Hunter's Lodge level 2"). Empty when none.</summary>
    public static IReadOnlyList<string> Of(ItemDefinition item, IReadOnlyList<RecipeDefinition> recipes, IReadOnlyList<BuildingDefinition> buildings)
    {
        if (item == null || recipes == null)
        {
            return System.Array.Empty<string>();
        }

        if (!ReferenceEquals(builtFor, recipes))
        {
            Build(recipes, buildings);
            builtFor = recipes;
        }

        return Uses.TryGetValue(item, out List<string> found) ? found : (IReadOnlyList<string>)System.Array.Empty<string>();
    }

    /// <summary>"Used for: A, B, C and 4 more", or null when nothing uses it.</summary>
    public static string Describe(IReadOnlyList<string> uses, int most = 4)
    {
        if (uses == null || uses.Count == 0)
        {
            return null;
        }

        int shown = Mathf.Min(most, uses.Count);
        var names = new List<string>(shown);
        for (int i = 0; i < shown; i++)
        {
            names.Add(uses[i]);
        }

        string more = uses.Count > shown ? $" and {uses.Count - shown} more" : "";
        return "Used for: " + string.Join(", ", names) + more;
    }

    private static void Build(IReadOnlyList<RecipeDefinition> recipes, IReadOnlyList<BuildingDefinition> buildings)
    {
        Uses.Clear();
        // Buildings first: a building level is the use a player plans around.
        if (buildings != null)
        {
            foreach (BuildingDefinition building in buildings)
            {
                if (building == null)
                {
                    continue;
                }

                foreach (ItemAmount cost in building.ConstructionItems)
                {
                    Add(cost.Item, building.DisplayName);
                }

                for (int level = 0; level < building.Upgrades.Count; level++)
                {
                    BuildingLevel upgrade = building.Upgrades[level];
                    foreach (ItemAmount cost in upgrade != null ? upgrade.cost : (IReadOnlyList<ItemAmount>)System.Array.Empty<ItemAmount>())
                    {
                        Add(cost.Item, $"{building.DisplayName} level {level + 2}");
                    }
                }
            }
        }

        foreach (RecipeDefinition recipe in recipes)
        {
            if (recipe == null)
            {
                continue;
            }

            foreach (ItemAmount cost in recipe.ItemCost)
            {
                Add(cost.Item, recipe.DisplayName);
            }
        }
    }

    private static void Add(ItemDefinition item, string use)
    {
        if (item == null || string.IsNullOrEmpty(use))
        {
            return;
        }

        if (!Uses.TryGetValue(item, out List<string> list))
        {
            Uses[item] = list = new List<string>();
        }

        if (!list.Contains(use))
        {
            list.Add(use);
        }
    }
}
