using System;
using System.Collections.Generic;

/// <summary>
/// The player's quickbar: up to <see cref="Capacity"/> favourite items, picked in the bag (hover, F) and used from the
/// wheel that holding F opens. Each keeps its place on the wheel, and stays there with a count of 0 when none are left,
/// so the hand learns where things are (the way item wheels work in Witcher 3 or Red Dead Redemption 2). It holds which
/// items, never the stacks: those stay in the bag. Owned by ItemInventory; saved with the player.
/// </summary>
public sealed class QuickItems
{
    public const int Capacity = 8;

    private readonly List<ItemDefinition> items = new List<ItemDefinition>();

    /// <summary>Raised whenever an item is added, removed or the whole list restored.</summary>
    public event Action Changed;

    public IReadOnlyList<ItemDefinition> Items => items;

    public bool IsFull => items.Count >= Capacity;

    public bool Contains(ItemDefinition item) => item != null && items.Contains(item);

    /// <summary>Only things the bag can use go on the quickbar: something to eat, drink, read or put on.</summary>
    public static bool Accepts(ItemDefinition item) => item != null && item.IsUsable;

    /// <summary>Adds the item, or takes it off if it is there already. Returns true if it is on the quickbar now.</summary>
    public bool Toggle(ItemDefinition item)
    {
        if (!Accepts(item))
        {
            return false;
        }

        if (items.Remove(item))
        {
            Changed?.Invoke();
            return false;
        }

        if (IsFull)
        {
            return false;
        }

        items.Add(item);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Puts back a saved quickbar, in its order (unusable or repeated entries are skipped).</summary>
    public void Restore(IEnumerable<ItemDefinition> saved)
    {
        items.Clear();
        if (saved != null)
        {
            foreach (ItemDefinition item in saved)
            {
                if (Accepts(item) && !items.Contains(item) && items.Count < Capacity)
                {
                    items.Add(item);
                }
            }
        }

        Changed?.Invoke();
    }
}
