using UnityEngine;

/// <summary>
/// The item stack being dragged, shared by every item window so a stack can be dragged from one window to another (bag
/// to character screen and back). The window where the drag starts calls Begin and, when it ends, End; the window it is
/// dropped on reads Source and Index and does the move.
/// </summary>
public static class ItemDrag
{
    public static ItemContainer Source { get; private set; }
    public static int Index { get; private set; } = -1;
    public static bool Active => Source != null && Index >= 0;

    public static void Begin(ItemContainer source, int index)
    {
        Source = source;
        Index = index;
    }

    public static void End()
    {
        Source = null;
        Index = -1;
    }

    /// <summary>
    /// Drops the dragged stack onto a slot: moves, merges or swaps. Explains why when a gear slot refuses the item.
    /// Returns true when something moved.
    /// </summary>
    public static bool DropOn(ItemContainer target, int index)
    {
        if (!Active || target == null)
        {
            return false;
        }

        ItemContainer source = Source;
        int from = Index;
        if (!target.IsUnlocked(index))
        {
            GameMessages.Post("That slot is locked. Carry a bigger backpack to use it.", MessageKind.Warning);
            return false;
        }

        ItemStack moving = source.GetSlot(from);
        if (!target.CanHold(index, moving.Item))
        {
            GameMessages.Post($"{moving.Item.DisplayName} doesn't go in that slot.", MessageKind.Warning);
            return false;
        }

        if (moving.Item != null && moving.Item.HasDurability && moving.IsBroken && target.SlotRule != null)
        {
            GameMessages.Post($"{moving.Item.DisplayName} is broken. Repair it at a Workshop first.", MessageKind.Warning);
            return false;
        }

        ItemStack other = target.GetSlot(index);
        if (!other.IsEmpty && !source.CanHold(from, other.Item))
        {
            GameMessages.Post($"{other.Item.DisplayName} can't be swapped into that slot. Drop it on an empty bag slot.", MessageKind.Warning);
            return false;
        }

        return source.MoveTo(from, target, index);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => End();
}
