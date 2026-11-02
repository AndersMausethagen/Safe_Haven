using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where a RecipeBrowser makes things: what stops a recipe, how many can be made, and making them. By hand from the bag
/// (HandMaker) or queued at a station (StationMaker); the browser doesn't need to know which.
/// </summary>
public interface IRecipeMaker
{
    /// <summary>The word on the make button: "Craft", "Queue".</summary>
    string Verb { get; }

    /// <summary>Whose materials the costs are counted against.</summary>
    MaterialPool Pool { get; }

    /// <summary>Why the recipe can't be made right now, in words for the player, or null when it can.</summary>
    string WhyNot(RecipeDefinition recipe);

    /// <summary>The player whose things these are.</summary>
    GameObject User { get; }

    /// <summary>How many crafts of the recipe are already on their way.</summary>
    int Waiting(RecipeDefinition recipe);

    /// <summary>Real seconds one craft takes here.</summary>
    float Seconds(RecipeDefinition recipe);

    /// <summary>How many times the recipe can be made right now.</summary>
    int HowMany(RecipeDefinition recipe);

    void Make(RecipeDefinition recipe, int count);
}

public static class RecipeMaking
{
    /// <summary>How many times the costs fit into what is held. A recipe that costs nothing has no limit.</summary>
    public static int Batches(RecipeDefinition recipe, Func<ResourceDefinition, int> haveResource, Func<ItemDefinition, int> haveItem)
    {
        int batches = int.MaxValue;
        foreach (ResourceAmount cost in recipe.ResourceCost)
        {
            if (cost.Resource != null && cost.Amount > 0)
            {
                batches = Mathf.Min(batches, haveResource(cost.Resource) / cost.Amount);
            }
        }

        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item != null && cost.Amount > 0)
            {
                batches = Mathf.Min(batches, haveItem(cost.Item) / cost.Amount);
            }
        }

        return batches;
    }

    /// <summary>"Need 2 Sticks, 3 Grey Stone": all that is still missing, or null when there is enough of everything.</summary>
    public static string Shortfall(RecipeDefinition recipe, Func<ResourceDefinition, int> haveResource, Func<ItemDefinition, int> haveItem)
    {
        var resources = new List<ResourceAmount>();
        foreach (ResourceAmount cost in recipe.ResourceCost)
        {
            if (cost.Resource != null && haveResource(cost.Resource) < cost.Amount)
            {
                resources.Add(new ResourceAmount(cost.Resource, cost.Amount - haveResource(cost.Resource)));
            }
        }

        var items = new List<ItemAmount>();
        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item != null && haveItem(cost.Item) < cost.Amount)
            {
                items.Add(new ItemAmount(cost.Item, cost.Amount - haveItem(cost.Item)));
            }
        }

        string first = AmountText.Describe(resources);
        string second = AmountText.Describe(items);
        string missing = first.Length > 0 && second.Length > 0 ? $"{first}, {second}" : first + second;
        return missing.Length > 0 ? "Need " + missing : null;
    }

    /// <summary>
    /// Why the recipe can't be made, short enough for a row. <paramref name="materials"/> tells a shortage, the everyday
    /// state of most of a list, from something that is truly in the way: a full queue, a station's tier.
    /// </summary>
    public static string Problem(IRecipeMaker maker, RecipeDefinition recipe, out bool materials)
    {
        string problem = maker.WhyNot(recipe);
        MaterialPool pool = maker.Pool;
        materials = problem != null && !pool.CanAfford(recipe.ResourceCost, recipe.ItemCost, out string missing) && problem == missing;
        return materials ? Shortfall(recipe, pool.Have, pool.Have) : problem;
    }

    /// <summary>How many of what the recipe makes the player already owns: carried, stored and worn.</summary>
    public static int Owned(IRecipeMaker maker, RecipeDefinition recipe)
    {
        if (recipe.Output == null)
        {
            return maker.Pool.Have(recipe.OutputResource);
        }

        int worn = maker.User != null && maker.User.TryGetComponent(out PlayerEquipment equipment) ? equipment.Gear.Slots.Count(recipe.Output) : 0;
        return maker.Pool.Have(recipe.Output) + worn;
    }
}

/// <summary>Crafting by hand from the bag: instant, one PlayerCrafting.TryCraft at a time.</summary>
public sealed class HandMaker : IRecipeMaker
{
    private readonly PlayerCrafting crafting;

    public HandMaker(PlayerCrafting crafting)
    {
        this.crafting = crafting;
    }

    public string Verb => "Craft";
    public MaterialPool Pool => crafting.Pool;
    public GameObject User => crafting.gameObject;
    public int Waiting(RecipeDefinition recipe) => 0;
    public float Seconds(RecipeDefinition recipe) => recipe.CraftSeconds;

    public string WhyNot(RecipeDefinition recipe)
    {
        PlayerCrafting.CraftCheck check = crafting.Check(recipe);
        return check.CanCraft ? null : check.Problem;
    }

    public int HowMany(RecipeDefinition recipe) => crafting.CraftableCount(recipe);

    public void Make(RecipeDefinition recipe, int count)
    {
        for (int i = 0; i < count && crafting.TryCraft(recipe); i++)
        {
        }
    }
}

/// <summary>Crafting at a station: each make pays at once and joins the station's queue (StationCrafting).</summary>
public sealed class StationMaker : IRecipeMaker
{
    private readonly GameObject user;
    private readonly StationQueue queue;

    public StationMaker(GameObject user, StationQueue queue)
    {
        this.user = user;
        this.queue = queue;
    }

    public string Verb => "Queue";
    public MaterialPool Pool => MaterialPool.For(user);
    public GameObject User => user;
    public int Waiting(RecipeDefinition recipe) => queue.CountOf(recipe);
    public float Seconds(RecipeDefinition recipe) => recipe.CraftSeconds / Mathf.Max(0.01f, queue.SpeedMultiplier);

    public string WhyNot(RecipeDefinition recipe) => StationCrafting.Check(user, queue, recipe);

    public int HowMany(RecipeDefinition recipe) => StationCrafting.QueueableCount(user, queue, recipe);

    public void Make(RecipeDefinition recipe, int count)
    {
        for (int i = 0; i < count && StationCrafting.TryQueue(user, queue, recipe); i++)
        {
        }
    }
}
