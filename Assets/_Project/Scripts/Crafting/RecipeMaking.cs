using System;
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

    public string WhyNot(RecipeDefinition recipe) => StationCrafting.Check(user, queue, recipe);

    public int HowMany(RecipeDefinition recipe) => StationCrafting.QueueableCount(user, queue, recipe);

    public void Make(RecipeDefinition recipe, int count)
    {
        for (int i = 0; i < count && StationCrafting.TryQueue(user, queue, recipe); i++)
        {
        }
    }
}
