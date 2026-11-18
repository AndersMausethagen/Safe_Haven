using UnityEngine;

/// <summary>
/// Quick stack (the user, 2026-11-15): inside the portal's ward, one key (Stack on Stored, V) sends everything in the bag
/// that is already kept in a chest to that chest, onto its stacks (StashStorage.StackOnStored). What is on the skill bar
/// or the quickbar stays with the player, as do gear and anything no chest holds yet. Pressed by PlayerInputHandler, or
/// from the controller's action wheel.
/// </summary>
public static class StashQuickStack
{
    /// <summary>True when the player is at home, inside the ward, with a chest standing.</summary>
    public static bool CanStack(GameObject player)
    {
        return player != null && HomePortal.Current != null && HomePortal.Current.Contains(player.transform.position)
            && StashChest.SlotsStanding() >= 0 && Place.StashUsable;
    }

    /// <summary>Stacks the bag onto what the chests hold and says what happened. Returns how many items moved.</summary>
    public static int Stack(GameObject player)
    {
        if (player == null || !player.TryGetComponent(out ItemInventory inventory))
        {
            return 0;
        }

        if (!CanStack(player))
        {
            GameMessages.Post(HomePortal.Current == null || StashChest.SlotsStanding() < 0
                ? "There is no chest at home to stack onto."
                : "Stand inside the ward to stack your bag onto your chests.", MessageKind.Warning);
            return 0;
        }

        PlayerSkills skills = player.GetComponent<PlayerSkills>();
        int moved = GameSession.Stash.StackOnStored(inventory.Container,
            stack => inventory.Quick.Contains(stack.Item) || (skills != null && skills.Loadout.IndexOf(stack.Item) >= 0), player);
        GameMessages.Post(moved > 0
            ? $"Stacked {moved} item{(moved == 1 ? "" : "s")} onto what your chests hold."
            : "Nothing in your bag is kept in a chest yet (or those chests are full).", moved > 0 ? MessageKind.Info : MessageKind.Warning);
        return moved;
    }
}
