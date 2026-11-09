using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player keeps safe in Safe Haven: items and raw resources that stay home during raids, so a failed raid can't
/// take them. Plain data owned by the GameSession so it outlives scene loads; the StashWindow moves things in and out.
/// Whatever the player puts in goes through Store, StoreMaterials or StoreResources, which raise Stored (a quest can ask
/// for something to be put away: "A Safe Place").
/// </summary>
public sealed class StashStorage
{
    public const int Columns = 10;
    public const int Rows = 6;
    public const int ResourceCapacity = 9999;

    public StashStorage()
    {
        Items = new ItemContainer(Columns, Rows);
        Resources = new ResourceStorage(ResourceCapacity);
    }

    public ItemContainer Items { get; }
    public ResourceStorage Resources { get; }

    /// <summary>Moves a slot's stack from the bag (or a cart) here, as much as fits, and returns how many arrived.</summary>
    public int Store(ItemContainer from, int slot, GameObject by)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        int moved = stack.IsEmpty ? 0 : from.TransferTo(slot, Items);
        if (moved > 0)
        {
            GameEvents.Raise(new GameEvent(GameEventKind.Stored, stack.Item, moved, by));
        }

        return moved;
    }

    /// <summary>Moves every material in the bag here, leaving food, tools and gear. Returns how many arrived; what doesn't fit stays in the bag.</summary>
    public int StoreMaterials(ItemContainer bag, GameObject by = null)
    {
        int moved = 0;
        for (int i = 0; bag != null && i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.Category == ItemCategory.Material)
            {
                moved += Store(bag, i, by);
            }
        }

        return moved;
    }

    /// <summary>Moves every carried essence here. Returns how many arrived.</summary>
    public int StoreResources(ResourceStorage carried, GameObject by = null)
    {
        int moved = 0;
        foreach (ResourceAmount held in new List<ResourceAmount>(carried != null ? carried.StoredAmounts : new List<ResourceAmount>()))
        {
            int added = Resources.Add(held.Resource, held.Amount);
            if (added <= 0)
            {
                continue;
            }

            carried.Remove(held.Resource, added);
            moved += added;
            GameEvents.Raise(new GameEvent(GameEventKind.Stored, held.Resource, added, by));
        }

        return moved;
    }
}
