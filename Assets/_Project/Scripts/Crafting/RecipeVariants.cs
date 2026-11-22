using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thriftier ways to make a thing, opened by a module (Docs/BuildingUpgradesResearch_2026-11-21.md §3.3: a cost cut is a variant recipe,
/// not a percentage, so a cancelled craft gives back exactly what it took and ore never gets cheaper). A recipe with Replaces set is a
/// variant of that recipe; where it can be made (its Needs Module fitted, the station's tier high enough) the station lists it in place of
/// the other (StationCrafting.KnownRecipesFor) and workers make it instead (WorkerProduction).
/// <para>A module can also cut one ingredient from every recipe at its station that needs two or more of it (BuildingModuleDefinition ›
/// One Fewer: the Herb Rack and herbs, the user 2026-11-22: "We do not need separate recipes for this effect"): those variants are made
/// here at runtime from the recipe itself (<see cref="ThriftierOf"/>), one copy for each recipe and module, kept for the session.</para>
/// </summary>
public static class RecipeVariants
{
    private static Dictionary<RecipeDefinition, List<RecipeDefinition>> variants;

    // The thriftier copies made so far, by the recipe and the module: one each, so a queued craft and its worker see the same one.
    private static readonly Dictionary<(RecipeDefinition, BuildingModuleDefinition), RecipeDefinition> copies =
        new Dictionary<(RecipeDefinition, BuildingModuleDefinition), RecipeDefinition>();

    // Entering Play doesn't reload scripts in this project: the map is made again from the content each session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        variants = null;
        foreach (RecipeDefinition copy in copies.Values)
        {
            if (copy != null)
            {
                Object.DestroyImmediate(copy);
            }
        }

        copies.Clear();
    }

    /// <summary>
    /// The copy of <paramref name="plain"/> that <paramref name="module"/>'s One Fewer makes cheaper, made once and kept; the recipe
    /// itself when the module cuts nothing from it.
    /// </summary>
    public static RecipeDefinition ThriftierOf(RecipeDefinition plain, BuildingModuleDefinition module)
    {
        if (plain == null || module == null || plain.IsThriftierCopy || !plain.CheaperWith(module))
        {
            return plain;
        }

        if (!copies.TryGetValue((plain, module), out RecipeDefinition copy) || copy == null)
        {
            copy = ScriptableObject.CreateInstance<RecipeDefinition>();
            copies[(plain, module)] = copy;
        }

        // Made again from the recipe each time outside Play, so an edited asset counts at once; in Play once.
        if (!Application.isPlaying || !copy.IsThriftierCopy)
        {
            RecipeDefinition.MakeThriftierCopy(copy, plain, module);
        }

        return copy;
    }

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

    // Whether the module fits a building whose station makes the recipe.
    private static bool FitsStationOf(BuildingModuleDefinition module, RecipeDefinition recipe, ContentDatabase content)
    {
        foreach (BuildingDefinition building in content.Buildings)
        {
            if (building != null && building.Station == recipe.Station && module.Fits(building))
            {
                return true;
            }
        }

        return false;
    }

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

        // A module's One Fewer, on every recipe at the stations it fits that it makes cheaper (after the variant assets, which win).
        foreach (BuildingModuleDefinition module in content != null ? content.Modules : (IReadOnlyList<BuildingModuleDefinition>)System.Array.Empty<BuildingModuleDefinition>())
        {
            if (module == null || module.OneFewer.Count == 0)
            {
                continue;
            }

            foreach (RecipeDefinition recipe in content.Recipes)
            {
                if (recipe == null || recipe.Replaces != null || recipe.NeedsModule != null || !FitsStationOf(module, recipe, content)
                    || !recipe.CheaperWith(module))
                {
                    continue;
                }

                if (!map.TryGetValue(recipe, out List<RecipeDefinition> better))
                {
                    map[recipe] = better = new List<RecipeDefinition>();
                }

                better.Add(ThriftierOf(recipe, module));
            }
        }

        variants = map;
        return map;
    }
}
