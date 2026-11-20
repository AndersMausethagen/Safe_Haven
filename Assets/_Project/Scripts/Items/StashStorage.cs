using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player keeps safe in Safe Haven: items and raw resources that stay home during raids, so a failed raid can't
/// take them. Plain data owned by the GameSession so it outlives scene loads; the StashWindow moves things in and out.
/// Whatever the player puts in goes through Store, StoreMaterials or StoreResources, which raise Stored (a quest can ask
/// for something to be put away: "A Safe Place").
/// <para>
/// Each chest has its own items (the user, 2026-11-15; until then every chest opened one shared stash): Items is cut into
/// blocks of <see cref="ChestSlots"/>, one per chest (StashChest.Block), and a chest's window shows and fills only its
/// block. Everything that treats the Haven's storage as one (crafting at a station, workers, trading, quests, the co-op
/// share, the save) still sees all of Items. Only the blocks of chests standing now are open (StashChest.RefreshStash);
/// a chest's items stay in its block when it is gone, out of reach until a chest takes that block again. Saves from
/// before blocks had the whole stash in block 0, which the first chest to stand takes.
/// </para>
/// </summary>
public sealed class StashStorage
{
    public const int Columns = 10;
    public const int Rows = 6;
    /// <summary>One chest's block: the most a single chest (a Storage Shed) holds.</summary>
    public const int ChestSlots = Columns * Rows;
    /// <summary>How many chests can each have a block.</summary>
    public const int MaxChests = 16;
    public const int ResourceCapacity = 9999;

    public StashStorage()
    {
        Items = new ItemContainer(Columns, Rows * MaxChests);
        Resources = new ResourceStorage(ResourceCapacity);
    }

    /// <summary>The first slot of a chest's block.</summary>
    public static int BlockStart(int block) => Mathf.Clamp(block, 0, MaxChests - 1) * ChestSlots;

    public ItemContainer Items { get; }
    public ResourceStorage Resources { get; }

    /// <summary>Moves a slot's stack from the bag (or a cart) here, as much as fits, and returns how many arrived.</summary>
    public int Store(ItemContainer from, int slot, GameObject by) => Store(from, slot, by, -1);

    /// <summary>The same, into one chest's block only (-1: any open slot of any chest).</summary>
    public int Store(ItemContainer from, int slot, GameObject by, int block)
    {
        ItemStack stack = from != null ? from.GetSlot(slot) : ItemStack.Empty;
        int moved = stack.IsEmpty ? 0
            : block >= 0 ? from.TransferTo(slot, Items, BlockStart(block), ChestSlots)
            : from.TransferTo(slot, Items);
        if (moved > 0)
        {
            GameEvents.Raise(new GameEvent(GameEventKind.Stored, stack.Item, moved, by));
        }

        return moved;
    }

    /// <summary>Moves every material in the bag here, leaving food, tools and gear. Returns how many arrived; what doesn't fit stays in the bag.</summary>
    public int StoreMaterials(ItemContainer bag, GameObject by = null, int block = -1) => StoreMaterials(bag, null, by, block);

    /// <summary>The same, leaving what <paramref name="keep"/> says to keep (what is on the skill bar or the quickbar).</summary>
    public int StoreMaterials(ItemContainer bag, System.Func<ItemStack, bool> keep, GameObject by, int block = -1)
    {
        int moved = 0;
        for (int i = 0; bag != null && i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (!stack.IsEmpty && stack.Item.Category == ItemCategory.Material && (keep == null || !keep(stack)))
            {
                moved += Store(bag, i, by, block);
            }
        }

        return moved;
    }

    /// <summary>
    /// Whether <see cref="StackOnStored"/> or <see cref="StoreMaterials(ItemContainer, System.Func{ItemStack, bool}, GameObject, int)"/>
    /// would move anything from <paramref name="bag"/>: a material, or a stack an open chest already holds. Room isn't checked.
    /// </summary>
    public bool HasSomethingToStore(ItemContainer bag, System.Func<ItemStack, bool> keep)
    {
        for (int i = 0; bag != null && i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (stack.IsEmpty || !stack.Item.Stackable || (keep != null && keep(stack)))
            {
                continue;
            }

            if (stack.Item.Category == ItemCategory.Material)
            {
                return true;
            }

            for (int block = 0; block < MaxChests; block++)
            {
                if (BlockHolds(block, stack.Item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Quick stack: every stack in the bag whose item already has a stack in an open chest goes to that chest, topping up its
    /// stacks first and then into the chest's empty slots. Gear, food or drink on the skill bar or quickbar, and what is
    /// worn stay with the player (<paramref name="keep"/> says what to leave). Returns how many items moved.
    /// </summary>
    public int StackOnStored(ItemContainer bag, System.Func<ItemStack, bool> keep, GameObject by = null)
    {
        int moved = 0;
        for (int i = 0; bag != null && i < bag.SlotCount; i++)
        {
            ItemStack stack = bag.GetSlot(i);
            if (stack.IsEmpty || !stack.Item.Stackable || (keep != null && keep(stack)))
            {
                continue;
            }

            // Every chest that holds some, in turn, until the stack is gone or they are all full.
            for (int block = 0; block < MaxChests && !bag.GetSlot(i).IsEmpty; block++)
            {
                if (BlockHolds(block, stack.Item))
                {
                    moved += Store(bag, i, by, block);
                }
            }
        }

        return moved;
    }

    private bool BlockHolds(int block, ItemDefinition item)
    {
        int start = BlockStart(block);
        for (int i = start; i < start + ChestSlots; i++)
        {
            if (Items.IsUnlocked(i) && Items.GetSlot(i).Item == item)
            {
                return true;
            }
        }

        return false;
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
