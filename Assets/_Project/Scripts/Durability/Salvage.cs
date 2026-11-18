using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Breaking things down at home (Docs/LootAndDropsResearch_2026-11-17.md §7; the user: "we can break down gear and junk finds, for new
/// resources ... like Arc raiders"). A junk find gives what its item Breaks Into (a broken sickle its iron, barrow goods their silver
/// and grave iron). Gear and tools without that give a share of the recipe that makes them (DurabilitySettings › Salvage Share), less
/// as they wear, so found gear, unwanted relics and outgrown kit have a way out. What it gives is always shown before anything is
/// broken (SalvageBench). Whatever comes out goes to the bag, then the stash (at home only: not in a friend's Haven), then the floor
/// (ItemDelivery).
/// </summary>
public static class Salvage
{
    /// <summary>What breaking one of the stack's items down gives. Empty when it can't be broken down.</summary>
    public static List<ItemAmount> YieldOf(ItemStack stack, IReadOnlyList<RecipeDefinition> recipes, DurabilitySettings settings) =>
        YieldOf(stack, 1, recipes, settings);

    /// <summary>
    /// What breaking <paramref name="count"/> of the stack's items down gives, worked out on the whole count and rounded down, so a piece
    /// never gives back its full recipe (2026-11-17 review: rounding a single ingredient's half up refunded rings and amulets in full, a
    /// free re-roll of their quality and their skill experience).
    /// </summary>
    public static List<ItemAmount> YieldOf(ItemStack stack, int count, IReadOnlyList<RecipeDefinition> recipes, DurabilitySettings settings)
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
                    yield.Add(new ItemAmount(part.Item, part.Amount * count));
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
            int amount = part.Item != null ? Mathf.FloorToInt(part.Amount * share * count + 0.0001f) : 0;
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
            lost.Add($"+{stack.Empower} (a quarter of its essence comes back)");
        }

        return lost.Count == 0 ? null : string.Join(", ", lost);
    }

    /// <summary>
    /// Breaks <paramref name="count"/> of the stack in a slot down. Returns false, with a message, when nothing comes of it.
    /// </summary>
    public static bool TryBreakDown(GameObject player, ItemContainer container, int slot, int count, IReadOnlyList<RecipeDefinition> recipes)
    {
        ItemStack stack = container != null ? container.GetSlot(slot) : ItemStack.Empty;
        count = Mathf.Min(count, stack.Count);
        List<ItemAmount> yield = YieldOf(stack, count, recipes, DurabilitySettings.Current);
        if (yield.Count == 0 || count <= 0)
        {
            GameMessages.Post("Nothing useful would come of breaking that.", MessageKind.Warning);
            return false;
        }

        container.RemoveAt(slot, count);
        var bag = player != null ? player.GetComponent<ItemInventory>() : null;
        var got = new List<ItemAmount>();
        // What the bag can't take goes to the stash at home only, as quest rewards do: in a friend's Haven the stash is the host's
        // (review 2026-11-17, R7), so it lands at the player's feet there.
        bool atHome = Place.AtHome;
        bool fell = false;
        foreach (ItemAmount part in yield)
        {
            fell |= ItemDelivery.Give(bag, new ItemStack(part.Item, part.Amount, 1f), newFind: false, toStash: atHome).Fell;
            got.Add(part);
        }

        // A quarter of the essence put into its empowering comes back (Docs/SystemDesignReview_2026-11-17.md K9): empowering a piece
        // that will be outgrown was a pure loss, so essence had no pull until the last tier.
        int refund = EmpowerRefund(stack, count);
        ResourceDefinition purified = SettlementSettings.Current != null ? SettlementSettings.Current.PurifiedEssence : null;
        if (refund > 0 && purified != null && player != null)
        {
            MaterialPool.For(player).Add(purified, refund);
            GameMessages.Post($"{refund} {purified.DisplayName} drawn back out of its empowering.", MessageKind.Gain);
        }

        string what = count > 1 ? $"{count} {stack.Item.DisplayName}" : stack.Item.DisplayName;
        GameMessages.Post($"Broke down {what}: {AmountText.Describe(got)}.", MessageKind.Gain);
        if (fell)
        {
            GameMessages.Post("No room for all of it: the rest lies at your feet.", MessageKind.Warning);
        }

        return true;
    }

    /// <summary>The share of an empowering's essence that breaking a piece down gives back.</summary>
    public const float EmpowerRefundShare = 0.25f;

    /// <summary>Purified Essence back from breaking <paramref name="count"/> of an empowered piece down: a share of what its levels cost.</summary>
    public static int EmpowerRefund(ItemStack stack, int count)
    {
        EssenceSettings settings = EssenceSettings.Current;
        if (stack.Item == null || stack.Empower <= 0 || settings == null)
        {
            return 0;
        }

        int spent = 0;
        for (int level = 1; level <= stack.Empower && level <= settings.EmpowerLevels.Count; level++)
        {
            spent += settings.EmpowerLevels[level - 1].purified + (stack.Item.Tier - 1) * settings.PurifiedPerTier;
        }

        return Mathf.FloorToInt(spent * EmpowerRefundShare * Mathf.Max(1, count));
    }

    private static bool IsGear(ItemDefinition item) => item.Category == ItemCategory.Equipment || item.Category == ItemCategory.Tool;
}
