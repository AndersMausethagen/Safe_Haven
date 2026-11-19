using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pack as last time (Docs/SystemsGapReview_2026-11-19.md §3): what the player set out with on their last raid (the draughts, food,
/// bandages and arrows in the bag, and whatever is on the quickbar) is remembered at launch, and at home one button in the stash takes
/// the same again from the stash into the bag, as far as the stash holds it. Gear is never moved (it is worn and kept), the quickbar
/// keeps its items (it remembers them by kind), and what the stash can't give is said, so it can be made. A raid's kit was put
/// together by hand from the stash every time: cut the chore, keep the choice. The moves are plain transfers, not arrivals, so no
/// quest counts them.
/// </summary>
public static class PackForRaid
{
    /// <summary>Whether a stack belongs to the kit a raid set out with: anything to use (a consumable), or on the quickbar.</summary>
    public static bool IsKit(ItemStack stack, QuickItems quick) =>
        !stack.IsEmpty && stack.Item != null && !stack.Item.IsEquippable && !stack.Item.IsBlueprint
        && (stack.Item.Category == ItemCategory.Consumable || (quick != null && quick.Contains(stack.Item)));

    /// <summary>The kit in a bag: item id to how many.</summary>
    public static Dictionary<string, int> KitOf(ItemContainer bag, QuickItems quick)
    {
        var kit = new Dictionary<string, int>();
        if (bag == null)
        {
            return kit;
        }

        for (int i = 0; i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (IsKit(stack, quick))
            {
                kit[stack.Item.Id] = (kit.TryGetValue(stack.Item.Id, out int had) ? had : 0) + stack.Count;
            }
        }

        return kit;
    }

    /// <summary>Remembers what the player sets out with (RaidLauncher, before the raid's save).</summary>
    public static void Remember(GameObject player)
    {
        if (player != null && player.TryGetComponent(out ItemInventory inventory) && GameSession.Progress != null)
        {
            GameSession.Progress.SetOutKit(KitOf(inventory.Container, inventory.Quick));
        }
    }

    /// <summary>Takes from <paramref name="stash"/> into <paramref name="bag"/> what the kit has more of than the bag holds now. Returns
    /// how many moved; <paramref name="shortfall"/> what the stash couldn't give, by name.</summary>
    public static int Fill(IReadOnlyDictionary<string, int> kit, ItemContainer bag, ItemContainer stash, ContentDatabase content, QuickItems quick,
        List<string> shortfall)
    {
        int moved = 0;
        if (kit == null || bag == null || stash == null)
        {
            return 0;
        }

        Dictionary<string, int> carried = KitOf(bag, quick);
        foreach (KeyValuePair<string, int> wanted in kit)
        {
            ItemDefinition item = content != null ? content.FindItem(wanted.Key) : null;
            if (item == null)
            {
                continue;
            }

            int missing = wanted.Value - (carried.TryGetValue(wanted.Key, out int have) ? have : bag.Count(item));
            for (int i = 0; i < stash.SlotCount && missing > 0; i++)
            {
                ItemStack stack = stash.GetSlot(i);
                if (stack.IsEmpty || stack.Item != item)
                {
                    continue;
                }

                ItemStack piece = stack.WithCount(Mathf.Min(stack.Count, missing));
                int placed = bag.AddStackWithin(piece, 0, bag.SlotCount);
                if (placed <= 0)
                {
                    break;
                }

                stash.RemoveAt(i, placed);
                missing -= placed;
                moved += placed;
            }

            if (missing > 0)
            {
                shortfall?.Add($"{item.DisplayName} ×{missing}");
            }
        }

        return moved;
    }

    /// <summary>The button (the stash window, the action wheel): packs the last kit and says what moved and what was short.</summary>
    public static int Pack(GameObject player)
    {
        IReadOnlyDictionary<string, int> kit = GameSession.Progress != null ? GameSession.Progress.SetOut : null;
        if (player == null || !player.TryGetComponent(out ItemInventory inventory))
        {
            return 0;
        }

        if (kit == null || kit.Count == 0)
        {
            GameMessages.Post("Nothing to pack yet: what you take on a raid is remembered for next time.", MessageKind.Info);
            return 0;
        }

        if (!Place.StashUsable || GameSession.Stash == null)
        {
            GameMessages.Post("Your stash is at home.", MessageKind.Warning);
            return 0;
        }

        var shortfall = new List<string>();
        int moved = Fill(kit, inventory.Container, GameSession.Stash.Items, Resources.Load<ContentDatabase>(ContentDatabase.ResourceName), inventory.Quick, shortfall);
        string said = moved > 0 ? $"Packed {moved} as last time." : "Your bag already holds last time's kit.";
        if (shortfall.Count > 0)
        {
            said += $" Short: {string.Join(", ", shortfall)}.";
        }

        GameMessages.Post(said, moved > 0 ? MessageKind.Gain : MessageKind.Info);
        return moved;
    }
}
