using System;
using System.Collections.Generic;

/// <summary>
/// What the player marked to keep in the bag (F over it in the bag): the Store key (StashQuickStack) leaves it, as it leaves what is on
/// the quickbar and the skill bar. For what can't go on the quickbar, such as a stack of arrows' feathers kept for the road or the
/// Wood for a camp (the systems review, 2026-11-21: Valheim and Palworld players ask the same of their quick-stack). It holds which
/// items, never the stacks. Owned by ItemInventory; saved with the player.
/// </summary>
public sealed class KeptItems
{
    private readonly List<ItemDefinition> items = new List<ItemDefinition>();

    /// <summary>Raised whenever an item is marked, unmarked or the whole list restored.</summary>
    public event Action Changed;

    public IReadOnlyList<ItemDefinition> Items => items;

    public bool Contains(ItemDefinition item) => item != null && items.Contains(item);

    /// <summary>Marks the item, or unmarks it if it is marked. Returns true if it is marked now.</summary>
    public bool Toggle(ItemDefinition item)
    {
        if (item == null)
        {
            return false;
        }

        bool marked = !items.Remove(item);
        if (marked)
        {
            items.Add(item);
        }

        Changed?.Invoke();
        return marked;
    }

    /// <summary>Puts back a saved list (missing or repeated entries are skipped).</summary>
    public void Restore(IEnumerable<ItemDefinition> saved)
    {
        items.Clear();
        if (saved != null)
        {
            foreach (ItemDefinition item in saved)
            {
                if (item != null && !items.Contains(item))
                {
                    items.Add(item);
                }
            }
        }

        Changed?.Invoke();
    }
}
