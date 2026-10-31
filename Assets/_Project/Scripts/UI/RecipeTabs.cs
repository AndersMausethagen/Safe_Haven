using System;
using System.Collections.Generic;

/// <summary>
/// Sorts recipes into the tabs a crafting window shows, by what they make: Tools, Gear, Consumables, Materials,
/// Resources (essences), Valuables, Quest. An "All" tab comes first when there is more than one kind; a station that
/// only makes one kind gets that single tab. Recipes keep their order inside each tab.
/// </summary>
public static class RecipeTabs
{
    public const string All = "All";
    public const string Resources = "Resources";
    /// <summary>The bag's extra tab: known recipes made somewhere else.</summary>
    public const string Stations = "Stations";

    public sealed class Tab
    {
        public Tab(string label)
        {
            Label = label;
        }

        public string Label { get; }
        public List<RecipeDefinition> Recipes { get; } = new List<RecipeDefinition>();
    }

    private static readonly string[] Order = { "Tools", "Gear", "Consumables", "Materials", Resources, "Valuables", "Quest" };

    /// <summary>The tab a recipe belongs to, from its output's item category (resource outputs go to Resources).</summary>
    public static string TabOf(RecipeDefinition recipe)
    {
        if (recipe == null || recipe.Output == null)
        {
            return Resources;
        }

        switch (recipe.Output.Category)
        {
            case ItemCategory.Tool:
                return "Tools";
            case ItemCategory.Equipment:
                return "Gear";
            case ItemCategory.Consumable:
                return "Consumables";
            case ItemCategory.Material:
                return "Materials";
            case ItemCategory.Valuable:
                return "Valuables";
            default:
                return "Quest";
        }
    }

    public static List<Tab> Build(IEnumerable<RecipeDefinition> recipes)
    {
        var all = new Tab(All);
        var byLabel = new Dictionary<string, Tab>();
        foreach (RecipeDefinition recipe in recipes)
        {
            if (recipe == null)
            {
                continue;
            }

            string label = TabOf(recipe);
            if (!byLabel.TryGetValue(label, out Tab tab))
            {
                tab = new Tab(label);
                byLabel.Add(label, tab);
            }

            tab.Recipes.Add(recipe);
            all.Recipes.Add(recipe);
        }

        var tabs = new List<Tab>();
        if (byLabel.Count > 1)
        {
            tabs.Add(all);
        }

        foreach (string label in Order)
        {
            if (byLabel.TryGetValue(label, out Tab tab))
            {
                tabs.Add(tab);
            }
        }

        return tabs;
    }

    /// <summary>The recipes that can be made, keeping their order.</summary>
    public static List<RecipeDefinition> OnlyMakeable(IEnumerable<RecipeDefinition> recipes, Predicate<RecipeDefinition> canMake)
    {
        var result = new List<RecipeDefinition>();
        foreach (RecipeDefinition recipe in recipes)
        {
            if (canMake(recipe))
            {
                result.Add(recipe);
            }
        }

        return result;
    }

    /// <summary>
    /// Splits an output slot's amount into stacks for a grid of the given number of cells: full stacks of stackSize
    /// first, the remainder last. When there are more stacks than cells the last cell holds everything left over.
    /// </summary>
    public static int[] SplitIntoCells(int amount, int stackSize, int cells)
    {
        var result = new int[System.Math.Max(0, cells)];
        if (result.Length == 0 || amount <= 0)
        {
            return result;
        }

        stackSize = System.Math.Max(1, stackSize);
        int left = amount;
        for (int i = 0; i < result.Length && left > 0; i++)
        {
            bool last = i == result.Length - 1;
            result[i] = last ? left : System.Math.Min(stackSize, left);
            left -= result[i];
        }

        return result;
    }
}
