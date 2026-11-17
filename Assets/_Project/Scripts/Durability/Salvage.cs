using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Breaking things down at home (Docs/LootAndDropsResearch_2026-11-17.md §7; the user: "we can break down gear and junk finds, for new
/// resources ... like Arc raiders"). A junk find gives what its item Breaks Into (a broken sickle its iron, barrow goods their silver
/// and grave iron). Gear and tools without that give a share of the recipe that makes them (DurabilitySettings › Salvage Share), less
/// as they wear, so found gear, unwanted relics and outgrown kit have a way out. What it gives is always shown before anything is
/// broken (SalvageBench). Whatever comes out goes to the bag, then the stash, then the floor (ItemDelivery).
/// </summary>
public static class Salvage
{
    /// <summary>What breaking one of the stack's items down gives. Empty when it can't be broken down.</summary>
    public static List<ItemAmount> YieldOf(ItemStack stack, IReadOnlyList<RecipeDefinition> recipes, DurabilitySettings settings)
    {
        var yield = new List<ItemAmount>();
        if (stack.IsEmpty)
        {
            return yield;
        }

        ItemDefinition item = stack.Item;
        if (item.BreaksInto.Count > 0)
        {
            foreach (ItemAmount part in item.BreaksInto)
            {
                if (part.Item != null && part.Amount > 0)
                {
                    yield.Add(part);
                }
            }

            return yield;
        }

        RecipeDefinition recipe = IsGear(item) && settings != null ? Repairs.RecipeFor(item, recipes) : null;
        if (recipe == null)
        {
            return yield;
        }

        float share = settings.SalvageShare * Mathf.Clamp01(stack.Durability) / Mathf.Max(1, recipe.OutputAmount);
        foreach (ItemAmount part in recipe.ItemCost)
        {
            // Half a plank rounds up: a piece's smallest part still comes back.
            int amount = part.Item != null ? Mathf.FloorToInt(part.Amount * share + 0.5f) : 0;
            if (amount > 0)
            {
                yield.Add(new ItemAmount(part.Item, amount));
            }
        }

        return yield;
    }

    /// <summary>What is lost with the piece besides its materials, in words ("its Frostbite enchantment, +2"), or null.</summary>
    public static string LostWith(ItemStack stack)
    {
        var lost = new List<string>();
        if (stack.Enchantment != null)
        {
            lost.Add($"its {stack.Enchantment.DisplayName}");
        }

        if (stack.Empower > 0)
        {
            lost.Add($"+{stack.Empower}");
        }

        return lost.Count == 0 ? null : string.Join(", ", lost);
    }

    /// <summary>
    /// Breaks <paramref name="count"/> of the stack in a slot down. Returns false, with a message, when nothing comes of it.
    /// </summary>
    public static bool TryBreakDown(GameObject player, ItemContainer container, int slot, int count, IReadOnlyList<RecipeDefinition> recipes)
    {
        ItemStack stack = container != null ? container.GetSlot(slot) : ItemStack.Empty;
        List<ItemAmount> yield = YieldOf(stack, recipes, DurabilitySettings.Current);
        count = Mathf.Min(count, stack.Count);
        if (yield.Count == 0 || count <= 0)
        {
            GameMessages.Post("Nothing useful would come of breaking that.", MessageKind.Warning);
            return false;
        }

        container.RemoveAt(slot, count);
        var bag = player != null ? player.GetComponent<ItemInventory>() : null;
        var got = new List<ItemAmount>();
        foreach (ItemAmount part in yield)
        {
            var made = new ItemAmount(part.Item, part.Amount * count);
            ItemDelivery.Give(bag, new ItemStack(made.Item, made.Amount, 1f), newFind: false, toStash: true);
            got.Add(made);
        }

        string what = count > 1 ? $"{count} {stack.Item.DisplayName}" : stack.Item.DisplayName;
        GameMessages.Post($"Broke down {what}: {AmountText.Describe(got)}.", MessageKind.Gain);
        return true;
    }

    private static bool IsGear(ItemDefinition item) => item.Category == ItemCategory.Equipment || item.Category == ItemCategory.Tool;
}
