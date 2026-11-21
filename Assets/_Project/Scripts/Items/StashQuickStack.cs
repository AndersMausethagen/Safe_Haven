using UnityEngine;

/// <summary>
/// Store in chests (the user, 2026-11-15, and 2026-11-20 "the same way Palworld does it"): inside the portal's ward, one key (Stack on
/// Stored, V) sends the bag to the chests, from anywhere in the Haven, without opening one. First everything a chest already holds goes
/// onto that chest's stacks (StashStorage.StackOnStored, Palworld's rule), then every material left goes into any chest with room, so
/// a new chest fills too. What is on the skill bar or the quickbar stays with the player, as do gear, food and tools no chest holds.
/// Anything marked to keep (KeptItems, F over it in the bag) stays too, and a chest set to be passed over (StashChest.SkipsStore) takes nothing.
/// Pressed by PlayerInputHandler, the bag window's Store button or the controller's action wheel; HavenHud shows the key in the
/// corner whenever there is something to store.
/// </summary>
public static class StashQuickStack
{
    /// <summary>True when the player is at home, inside the ward, with a chest standing.</summary>
    public static bool CanStack(GameObject player)
    {
        return player != null && HomePortal.Current != null && HomePortal.Current.Contains(player.transform.position)
            && StashChest.SlotsStanding() >= 0 && Place.StashUsable;
    }

    /// <summary>True when <see cref="CanStack"/> and the bag holds something it would store: what the HUD's prompt asks.</summary>
    public static bool HasSomethingToStore(GameObject player)
    {
        if (!CanStack(player) || !player.TryGetComponent(out ItemInventory inventory))
        {
            return false;
        }

        PlayerSkills skills = player.GetComponent<PlayerSkills>();
        return GameSession.Stash.HasSomethingToStore(inventory.Container, stack => Keeps(inventory, skills, stack), StashChest.BlockSkipsStore);
    }

    /// <summary>How many things the bag would put in the chests now (0 where it can't), for the Haven's prompt.</summary>
    public static int CountToStore(GameObject player)
    {
        if (!CanStack(player) || !player.TryGetComponent(out ItemInventory inventory))
        {
            return 0;
        }

        PlayerSkills skills = player.GetComponent<PlayerSkills>();
        return GameSession.Stash.CountToStore(inventory.Container, stack => Keeps(inventory, skills, stack), StashChest.BlockSkipsStore);
    }

    /// <summary>Stores the bag in the chests and says what happened. Returns how many items moved.</summary>
    public static int Stack(GameObject player)
    {
        if (player == null || !player.TryGetComponent(out ItemInventory inventory))
        {
            return 0;
        }

        if (!CanStack(player))
        {
            GameMessages.Post(HomePortal.Current == null || StashChest.SlotsStanding() < 0
                ? "There is no chest at home to store things in."
                : "Stand inside the ward to store your bag in your chests.", MessageKind.Warning);
            return 0;
        }

        PlayerSkills skills = player.GetComponent<PlayerSkills>();
        bool Keep(ItemStack stack) => Keeps(inventory, skills, stack);
        int moved = GameSession.Stash.StackOnStored(inventory.Container, Keep, player, StashChest.BlockSkipsStore);
        moved += GameSession.Stash.StoreMaterials(inventory.Container, Keep, player, -1, StashChest.BlockSkipsStore);
        GameMessages.Post(moved > 0
            ? $"Stored {moved} item{(moved == 1 ? "" : "s")} in your chests."
            : "Nothing to store: no materials in your bag, nothing your chests already hold (or the chests are full).", moved > 0 ? MessageKind.Info : MessageKind.Warning);
        return moved;
    }

    // What the player carries on purpose stays: the quickbar, the skill bar, and what they marked to keep (F over it in the bag).
    private static bool Keeps(ItemInventory inventory, PlayerSkills skills, ItemStack stack) =>
        inventory.Quick.Contains(stack.Item) || inventory.Kept.Contains(stack.Item) || (skills != null && skills.Loadout.IndexOf(stack.Item) >= 0);
}
