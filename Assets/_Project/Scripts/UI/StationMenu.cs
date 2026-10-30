using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The page a crafting station shows in the ActionMenuWindow: what it is working on, its output slot, then actions:
/// extensions first (Plan a raid, Repair gear), Collect, one Craft action per known recipe for this station, and Cancel.
/// Rebuilt by the window twice a second, so progress counts down live.
/// </summary>
public static class StationMenu
{
    private const string ProblemColor = "#FF7B6B";

    public static MenuPage Build(CraftingStation station, GameObject user)
    {
        if (station == null)
        {
            return null;
        }

        StationQueue queue = station.Queue;
        var page = new MenuPage { Title = station.Type.DisplayName() };
        foreach (IStationMenuExtension extension in station.GetComponents<IStationMenuExtension>())
        {
            extension.AddActions(page, user);
        }

        page.Body = Describe(queue);
        if (queue.Output != null)
        {
            page.Add(new MenuAction($"Collect {queue.OutputAmount} {NameOf(queue.Output)}", null, true,
                () => StationCrafting.Collect(user, queue)));
        }

        List<RecipeDefinition> recipes = StationCrafting.KnownRecipesFor(user, station.Type);
        foreach (RecipeDefinition recipe in recipes)
        {
            string problem = StationCrafting.Check(user, queue, recipe);
            string detail = $"{Costs(recipe)}   {recipe.CraftSeconds:0.#}s" + (problem != null ? $"   <color={ProblemColor}>{problem}</color>" : "");
            RecipeDefinition chosen = recipe;
            page.Add(new MenuAction($"Craft {Makes(recipe)}", detail, problem == null, () => StationCrafting.TryQueue(user, queue, chosen)));
        }

        if (queue.CanCancelLast)
        {
            RecipeDefinition last = queue.Jobs[queue.Jobs.Count - 1].Recipe;
            page.Add(new MenuAction($"Cancel one {last.DisplayName}", "Materials are given back", true, () => StationCrafting.CancelLast(user, queue)));
        }

        if (recipes.Count == 0)
        {
            page.Body += "\n\nYou know no recipes for this station yet.";
        }

        return page;
    }

    private static string Describe(StationQueue queue)
    {
        var body = new StringBuilder();
        if (queue.IsIdle)
        {
            body.Append("Idle. Queue a craft: materials are taken now and the work goes on while you are away.");
        }
        else
        {
            RecipeDefinition current = queue.Current;
            body.Append(queue.Blocked
                ? $"<color={ProblemColor}>Stopped: the output is full. Collect it to continue.</color>"
                : $"Making {Makes(current)}: {queue.CurrentRemaining:0}s left ({queue.Progress / current.CraftSeconds:P0})");
            body.Append($"\nQueue: {queue.QueuedCount}/{queue.MaxQueued}");
            if (queue.Jobs.Count > 1 || queue.Jobs[0].Count > 1)
            {
                var waiting = new List<string>();
                foreach (StationQueue.Job job in queue.Jobs)
                {
                    waiting.Add($"{job.Count} × {job.Recipe.DisplayName}");
                }

                body.Append("  (").Append(string.Join(", ", waiting)).Append(')');
            }
        }

        body.Append(queue.Output != null
            ? $"\nOutput: {queue.OutputAmount}/{queue.Capacity} {NameOf(queue.Output)}"
            : $"\nOutput: empty (holds {queue.Capacity}). Refined materials go straight to storage while it has room.");
        return body.ToString();
    }

    private static string Makes(RecipeDefinition recipe)
    {
        return recipe.OutputAmount > 1 ? $"{recipe.OutputAmount} {recipe.OutputName}" : recipe.OutputName;
    }

    private static string Costs(RecipeDefinition recipe)
    {
        string resources = AmountText.Describe(recipe.ResourceCost);
        string items = AmountText.Describe(recipe.ItemCost);
        if (string.IsNullOrEmpty(items))
        {
            return string.IsNullOrEmpty(resources) ? "free" : resources;
        }

        return string.IsNullOrEmpty(resources) ? items : resources + ", " + items;
    }

    private static string NameOf(Object definition) =>
        definition is IDisplayName named ? named.DisplayName : definition != null ? definition.name : "";
}
