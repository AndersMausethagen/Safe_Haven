using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a player does at a crafting station: queue a known recipe (its costs are paid at once from the MaterialPool),
/// cancel the last craft that hasn't started (refunded to what is carried, overflow to storage) and collect the output
/// slot (items to the bag, resources to what is carried; whatever doesn't fit stays in the slot).
/// <para>
/// The rules live here so the station menu, tests and future UIs share them. The queue itself is StationQueue.
/// </para>
/// </summary>
public static class StationCrafting
{
    /// <summary>Why the recipe can't be queued right now, in words for the player, or null when it can.</summary>
    public static string Check(GameObject user, StationQueue queue, RecipeDefinition recipe)
    {
        PlayerCrafting crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (queue == null || recipe == null || !recipe.HasOutput)
        {
            return "This recipe makes nothing.";
        }

        if (recipe.Station != queue.Type)
        {
            return $"Made at {recipe.Station.WithArticle()}.";
        }

        if (crafting == null || !crafting.IsKnown(recipe))
        {
            return "Not learned yet.";
        }

        if (recipe.RequiredStationTier > queue.Tier && !Cheats.NoRequirements)
        {
            return $"Needs {queue.Type.DisplayName()} Tier {Tiers.ToRoman(recipe.RequiredStationTier)}.";
        }

        if (queue.FreeQueueSlots <= 0)
        {
            return "The queue is full.";
        }

        return MaterialPool.For(user).CanAfford(recipe.ResourceCost, recipe.ItemCost, out string missing) ? null : missing;
    }

    /// <summary>How many crafts of the recipe can be queued right now, with what is held and the room in the queue.</summary>
    public static int QueueableCount(GameObject user, StationQueue queue, RecipeDefinition recipe)
    {
        if (Check(user, queue, recipe) != null)
        {
            return 0;
        }

        MaterialPool pool = MaterialPool.For(user);
        return Mathf.Min(queue.FreeQueueSlots, RecipeMaking.Batches(recipe, pool.Have, pool.Have));
    }

    /// <summary>Pays for one craft and puts it at the end of the queue. Tells the player why not when it can't.</summary>
    public static bool TryQueue(GameObject user, StationQueue queue, RecipeDefinition recipe)
    {
        if (HavenVisit.LeftToHost())
        {
            return false;
        }

        string problem = Check(user, queue, recipe);
        if (problem != null)
        {
            GameMessages.Post(problem, MessageKind.Warning);
            return false;
        }

        if (!MaterialPool.For(user).TrySpend(recipe.ResourceCost, recipe.ItemCost))
        {
            return false;
        }

        queue.Enqueue(recipe);
        return true;
    }

    /// <summary>Takes the last unstarted craft off the queue and gives its costs back. Returns false when nothing could be cancelled.</summary>
    public static bool CancelLast(GameObject user, StationQueue queue)
    {
        RecipeDefinition recipe = queue != null ? queue.CancelLast() : null;
        if (recipe == null)
        {
            return false;
        }

        Refund(user, recipe);
        GameMessages.Post($"Cancelled {recipe.DisplayName}; its materials are back.");
        return true;
    }

    private static void Refund(GameObject user, RecipeDefinition recipe)
    {
        MaterialPool pool = MaterialPool.For(user);
        foreach (ResourceAmount cost in recipe.ResourceCost)
        {
            int added = pool.Add(cost.Resource, cost.Amount);
            if (added < cost.Amount && cost.Resource != null)
            {
                GameSession.Stash.Resources.Add(cost.Resource, cost.Amount - added);
            }
        }

        ItemInventory bag = user != null ? user.GetComponent<ItemInventory>() : null;
        foreach (ItemAmount cost in recipe.ItemCost)
        {
            if (cost.Item == null || cost.Amount <= 0)
            {
                continue;
            }

            int given = bag != null ? bag.Give(cost.Item, cost.Amount) : 0;
            if (given < cost.Amount)
            {
                GameSession.Stash.Items.Add(cost.Item, cost.Amount - given);
            }
        }
    }

    /// <summary>Moves what fits from the output slot to the player. Returns how many were taken.</summary>
    public static int Collect(GameObject user, StationQueue queue)
    {
        if (queue == null || queue.Output == null || user == null)
        {
            return 0;
        }

        Object output = queue.Output;
        int wanted = queue.OutputAmount;
        int taken;
        if (output is ItemDefinition item)
        {
            ItemInventory bag = user.GetComponent<ItemInventory>();
            taken = bag != null ? Mathf.Min(wanted, bag.Container.SpaceFor(item)) : 0;
            if (taken > 0)
            {
                // Gear comes out as the one collecting it made it: its quality and condition from their crafting skill.
                if (item.HasDurability)
                {
                    StatSheet maker = user.TryGetComponent(out Combatant combatant) ? combatant.Stats : null;
                    taken = CraftedWork.GiveTo(bag, item, taken, maker);
                    queue.TakeOutput(taken);
                }
                else
                {
                    bag.Give(item, queue.TakeOutput(taken));
                }
            }
        }
        else if (output is ResourceDefinition resource)
        {
            taken = MaterialPool.For(user).Add(resource, wanted);
            queue.TakeOutput(taken);
            if (taken > 0)
            {
                GameMessages.Post($"+{taken} {resource.DisplayName}", MessageKind.Gain);
            }
        }
        else
        {
            return 0;
        }

        if (taken < wanted)
        {
            GameMessages.Post(taken == 0 ? "No room to take any of it." : "No room for the rest; it stays at the station.", MessageKind.Warning);
        }

        return taken;
    }

    /// <summary>The known recipes made at the station type, in recipe-book order.</summary>
    public static List<RecipeDefinition> KnownRecipesFor(GameObject user, CraftingStationType type)
    {
        var recipes = new List<RecipeDefinition>();
        PlayerCrafting crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (crafting == null)
        {
            return recipes;
        }

        foreach (RecipeDefinition recipe in crafting.Recipes)
        {
            if (recipe != null && recipe.Station == type && crafting.IsKnown(recipe))
            {
                recipes.Add(recipe);
            }
        }

        return recipes;
    }
}
