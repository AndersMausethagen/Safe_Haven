using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thriftier ways to make a thing, opened by a module (Docs/BuildingUpgradesResearch_2026-11-21.md §3.3: a cost cut is a variant recipe,
/// not a percentage, so a cancelled craft gives back exactly what it took and ore never gets cheaper). A recipe with Replaces set is a
/// variant of that recipe; where it can be made (its Needs Module fitted, the station's tier high enough) the station lists it in place of
/// the other (StationCrafting.KnownRecipesFor) and workers make it instead (WorkerProduction).
/// </summary>
public static class RecipeVariants
{
    private static Dictionary<RecipeDefinition, List<RecipeDefinition>> variants;

    // Entering Play doesn't reload scripts in this project: the map is made again from the content each session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => variants = null;

    /// <summary>True when <paramref name="recipe"/> can be made at <paramref name="queue"/>'s station: its tier and its module.</summary>
    public static bool IsOpenAt(RecipeDefinition recipe, StationQueue queue) =>
        recipe != null && queue != null && recipe.RequiredStationTier <= queue.Tier && queue.HasModule(recipe.NeedsModule);

    /// <summary>The variant of <paramref name="recipe"/> to make at <paramref name="queue"/>'s station, or the recipe itself when none is open.</summary>
    public static RecipeDefinition OpenAt(RecipeDefinition recipe, StationQueue queue)
    {
        if (recipe == null || queue == null || !Map().TryGetValue(recipe, out List<RecipeDefinition> better))
        {
            return recipe;
        }

        foreach (RecipeDefinition variant in better)
        {
            if (variant.Station == recipe.Station && IsOpenAt(variant, queue))
            {
                return variant;
            }
        }

        return recipe;
    }

    /// <summary>The variants of <paramref name="recipe"/>; empty when it has none.</summary>
    public static IReadOnlyList<RecipeDefinition> Of(RecipeDefinition recipe) =>
        recipe != null && Map().TryGetValue(recipe, out List<RecipeDefinition> better) ? better : (IReadOnlyList<RecipeDefinition>)System.Array.Empty<RecipeDefinition>();

    // Made once a play session from the content's recipes; in the Editor outside Play every time, so edited assets count at once.
    private static Dictionary<RecipeDefinition, List<RecipeDefinition>> Map()
    {
        if (variants != null && Application.isPlaying)
        {
            return variants;
        }

        var map = new Dictionary<RecipeDefinition, List<RecipeDefinition>>();
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        foreach (RecipeDefinition recipe in content != null ? content.Recipes : (IReadOnlyList<RecipeDefinition>)System.Array.Empty<RecipeDefinition>())
        {
            if (recipe == null || recipe.Replaces == null)
            {
                continue;
            }

            if (!map.TryGetValue(recipe.Replaces, out List<RecipeDefinition> better))
            {
                map[recipe.Replaces] = better = new List<RecipeDefinition>();
            }

            better.Add(recipe);
        }

        variants = map;
        return map;
    }
}
