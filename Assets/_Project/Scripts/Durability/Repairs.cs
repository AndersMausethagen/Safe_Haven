using System.Collections.Generic;
using UnityEngine;

/// <summary>What work on gear costs, a repair or empowering it: material items and essences. Both lists are empty when free.</summary>
public sealed class RepairCost
{
    public readonly List<ItemAmount> Items = new List<ItemAmount>();
    public readonly List<ResourceAmount> Resources = new List<ResourceAmount>();

    public bool IsFree => Items.Count == 0 && Resources.Count == 0;

    /// <summary>"3 Oak Wood, 1 Grey Stone", or "free".</summary>
    public string Describe()
    {
        if (IsFree)
        {
            return "free";
        }

        string items = AmountText.Describe(Items);
        string resources = AmountText.Describe(Resources);
        return items.Length > 0 && resources.Length > 0 ? items + ", " + resources : items + resources;
    }
}

/// <summary>
/// Repairing worn gear (GDD §13.4): what a repair costs and doing it. A repair restores an item to full durability and
/// costs its recipe's materials times DurabilitySettings.FullRepairCostShare times the share of durability missing,
/// rounded up, at least the minimum per material. Items without a recipe repair for free. Costs are paid from the
/// MaterialPool, so at home storage counts.
/// <para>
/// Used by the repair section of the crafting window (ItemInventoryWindow) and tested in DurabilityTests.
/// </para>
/// </summary>
public static class Repairs
{
    /// <summary>The recipe that makes an item, or null.</summary>
    public static RecipeDefinition RecipeFor(ItemDefinition item, IReadOnlyList<RecipeDefinition> recipes)
    {
        if (item == null || recipes == null)
        {
            return null;
        }

        foreach (RecipeDefinition recipe in recipes)
        {
            if (recipe != null && recipe.Output == item)
            {
                return recipe;
            }
        }

        return null;
    }

    /// <summary>What repairing the stack to full costs. Empty for undamaged stacks and items without a recipe.</summary>
    public static RepairCost Cost(ItemStack stack, RecipeDefinition recipe, DurabilitySettings settings)
    {
        var cost = new RepairCost();
        if (!stack.IsDamaged || recipe == null || settings == null)
        {
            return cost;
        }

        // A smith makes repairs cheaper.
        float discount = Mathf.Clamp01(GameSession.Here.JobBonus(job => job.GearRepairDiscount));
        float share = settings.FullRepairCostShare * (1f - discount) * (1f - stack.Durability) / Mathf.Max(1, recipe.OutputAmount);
        foreach (ItemAmount part in recipe.ItemCost)
        {
            int amount = Share(part.Item != null ? part.Amount : 0, share, settings);
            if (amount > 0)
            {
                cost.Items.Add(new ItemAmount(part.Item, amount));
            }
        }

        foreach (ResourceAmount part in recipe.ResourceCost)
        {
            int amount = Share(part.Resource != null ? part.Amount : 0, share, settings);
            if (amount > 0)
            {
                cost.Resources.Add(new ResourceAmount(part.Resource, amount));
            }
        }

        return cost;
    }

    private static int Share(int recipeAmount, float share, DurabilitySettings settings)
    {
        return recipeAmount <= 0 ? 0 : Mathf.Max(settings.MinimumCostPerMaterial, Mathf.CeilToInt(recipeAmount * share - 0.0001f));
    }

    /// <summary>Why the stack in a bag slot can't be repaired now, in words for the player, or null when it can.</summary>
    public static string Check(GameObject player, ItemContainer container, int slot, IReadOnlyList<RecipeDefinition> recipes)
    {
        ItemStack stack = container != null ? container.GetSlot(slot) : ItemStack.Empty;
        if (!stack.IsDamaged)
        {
            return "Nothing to repair.";
        }

        DurabilitySettings settings = DurabilitySettings.Current;
        var crafting = player != null ? player.GetComponent<PlayerCrafting>() : null;
        if (crafting != null && !crafting.IsNearStation(settings.RepairStation))
        {
            return $"Repairs need {settings.RepairStation.WithArticle()} nearby.";
        }

        RepairCost cost = Cost(stack, RecipeFor(stack.Item, recipes), settings);
        return MaterialPool.For(player).CanAfford(cost.Resources, cost.Items, out string missing) ? null : missing;
    }

    /// <summary>Repairs the stack in a slot to full, paying the cost. Returns false, with a message, when it can't.</summary>
    public static bool TryRepair(GameObject player, ItemContainer container, int slot, IReadOnlyList<RecipeDefinition> recipes) =>
        TryRepair(player, container, slot, recipes, quiet: false);

    private static bool TryRepair(GameObject player, ItemContainer container, int slot, IReadOnlyList<RecipeDefinition> recipes, bool quiet)
    {
        string problem = Check(player, container, slot, recipes);
        if (problem != null)
        {
            if (!quiet)
            {
                GameMessages.Post(problem, MessageKind.Warning);
            }

            return false;
        }

        ItemStack stack = container.GetSlot(slot);
        RepairCost cost = Cost(stack, RecipeFor(stack.Item, recipes), DurabilitySettings.Current);
        if (!MaterialPool.For(player).TrySpend(cost.Resources, cost.Items))
        {
            return false;
        }

        container.SetDurability(slot, 1f);
        if (!quiet)
        {
            GameMessages.Post($"{stack.Item.DisplayName} repaired.", MessageKind.Gain);
        }

        GameEvents.Raise(new GameEvent(GameEventKind.Repaired, stack.Item, 1, player));
        return true;
    }

    /// <summary>The worn things a player has, in the order they are mended: what is worn on the body first, then the bag, then the safe
    /// pocket.</summary>
    public static List<(ItemContainer container, int slot)> Worn(GameObject player)
    {
        var worn = new List<(ItemContainer, int)>();
        if (player == null)
        {
            return worn;
        }

        PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
        ItemInventory inventory = player.GetComponent<ItemInventory>();
        foreach (ItemContainer container in new[] { equipment != null ? equipment.Gear.Slots : null, inventory != null ? inventory.Container : null,
                     inventory != null ? inventory.SafePocket : null })
        {
            if (container == null)
            {
                continue;
            }

            for (int i = 0; i < container.SlotCount; i++)
            {
                if (container.GetSlot(i).IsDamaged)
                {
                    worn.Add((container, i));
                }
            }
        }

        return worn;
    }

    /// <summary>Mends everything worn, in the order worn, as far as the materials go (Docs/SystemsGapReview_2026-11-19.md §3: the
    /// bench listed pieces one by one). Says how many were mended; returns that.</summary>
    public static int MendAll(GameObject player, IReadOnlyList<RecipeDefinition> recipes)
    {
        List<(ItemContainer container, int slot)> worn = Worn(player);
        int mended = 0;
        foreach ((ItemContainer container, int slot) in worn)
        {
            if (TryRepair(player, container, slot, recipes, quiet: true))
            {
                mended++;
            }
        }

        GameMessages.Post(mended == worn.Count ? $"Mended all {mended}." : $"Mended {mended} of {worn.Count}: the rest wants more materials.",
            mended > 0 ? MessageKind.Gain : MessageKind.Warning);
        return mended;
    }
}
