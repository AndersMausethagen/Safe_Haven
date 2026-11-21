using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Taking down a building the player built (the user, 2026-11-20: "Allow us to destroy placed buildings, refunding materials"): picked
/// from the build menu's Manage tab (BuildPlacer), asked once in an ActionMenuWindow, then gone for good (SaveableEntity.RemoveForGood)
/// with everything it cost back, its upgrades too: into the bag, then the chests, then the ground (MaterialPool.GiveBack).
/// <para>
/// Refused, with the reason, when taking it down would lose something: a chest with items in it (as Palworld does), a station still
/// working or with something waiting in it, a ruin the Haven came with (nobody paid for it), during a breach, or in a friend's Haven.
/// </para>
/// </summary>
public static class BuildingDemolition
{
    /// <summary>Why <paramref name="building"/> can't be taken down now, or null when it can.</summary>
    public static string WhyNot(BuildingObject building)
    {
        BuildingDefinition definition = building != null ? building.Definition : null;
        if (definition == null || !definition.ShowInBuildMenu)
        {
            return "That can't be taken down.";
        }

        if (HavenVisit.IsVisiting)
        {
            return "Only your own buildings can be taken down.";
        }

        if (!HavenTravel.IsOpen)
        {
            return "Not while the ward is breached.";
        }

        if (building.TryGetComponent(out Construction construction) && construction.StartsRuined)
        {
            return $"The {definition.DisplayName} was here before you: it can be repaired, not taken down.";
        }

        if (building.TryGetComponent(out StashChest chest) && ItemsIn(chest) > 0)
        {
            return $"Empty the {definition.DisplayName} first: {ItemsIn(chest)} item(s) are still in it.";
        }

        if (building.TryGetComponent(out CraftingStation station))
        {
            StationQueue queue = station.Queue;
            if (queue != null && (!queue.IsIdle || queue.OutputAmount > 0))
            {
                return $"The {definition.DisplayName} is still working, or has something waiting in it: take it out first.";
            }
        }

        return null;
    }

    /// <summary>Everything that was paid for it: the building, each level it was raised to, and an upgrade paid for and not finished.</summary>
    public static void Refund(BuildingObject building, List<ResourceAmount> resources, List<ItemAmount> items)
    {
        resources.Clear();
        items.Clear();
        BuildingDefinition definition = building != null ? building.Definition : null;
        if (definition == null)
        {
            return;
        }

        foreach (ResourceAmount cost in definition.ConstructionCost)
        {
            AddTo(resources, cost.Resource, cost.Amount);
        }

        foreach (ItemAmount cost in definition.ConstructionItems)
        {
            AddTo(items, cost.Item, cost.Amount);
        }

        // A wall piece's torch comes back with it (WallFittings).
        if (building.TryGetComponent(out WallFittings fittings) && fittings.HasTorch)
        {
            foreach (ItemAmount cost in definition.WallTorchCost)
            {
                AddTo(items, cost.Item, cost.Amount);
            }
        }

        if (!building.TryGetComponent(out Construction construction))
        {
            return;
        }

        // Levels 2 and up finished, and the one being worked towards when it was paid for.
        int top = construction.IsBuilding && construction.Paid && construction.Level >= 1 ? construction.Target : construction.Level;
        for (int level = 2; level <= top; level++)
        {
            BuildingLevel paid = definition.UpgradeTo(level);
            if (paid == null)
            {
                continue;
            }

            foreach (ItemAmount cost in paid.cost)
            {
                AddTo(items, cost.Item, cost.Amount);
            }

            foreach (ResourceAmount cost in paid.essence)
            {
                AddTo(resources, cost.Resource, cost.Amount);
            }
        }
    }

    /// <summary>Asks the player whether to take <paramref name="building"/> down, showing what comes back, or says why it can't be.</summary>
    public static void Ask(BuildingObject building, GameObject user)
    {
        string refused = WhyNot(building);
        if (refused != null)
        {
            GameMessages.Post(refused, MessageKind.Warning);
            return;
        }

        ActionMenuWindow menu = ActionMenuWindow.Current;
        if (menu == null)
        {
            return;
        }

        menu.Open(() => Page(building, user), user, building.transform, 30f);
    }

    private static MenuPage Page(BuildingObject building, GameObject user)
    {
        var resources = new List<ResourceAmount>();
        var items = new List<ItemAmount>();
        Refund(building, resources, items);
        string back = Describe(resources, items);
        string name = building != null && building.Definition != null ? building.Definition.DisplayName : "building";
        var page = new MenuPage
        {
            Title = $"Take down the {name}?",
            Body = back.Length > 0 ? $"Everything it cost comes back:\n<b>{back}</b>" : "It cost nothing, so nothing comes back.",
        };
        string refused = building != null ? WhyNot(building) : "It is gone.";
        page.Add(new MenuAction("Take it down", refused ?? "Into your bag, then your chests", refused == null,
            () => TakeDown(building, user), closesMenu: true) { Icon = "Hammer" });
        page.Add(new MenuAction("Keep it", "", true, null, closesMenu: true));
        return page;
    }

    /// <summary>Takes the building down for good and gives back what it cost. False, with a message, when it can't be.</summary>
    public static bool TakeDown(BuildingObject building, GameObject user)
    {
        string refused = WhyNot(building);
        if (refused != null || user == null)
        {
            GameMessages.Post(refused ?? "Nobody to take it down.", MessageKind.Warning);
            return false;
        }

        var resources = new List<ResourceAmount>();
        var items = new List<ItemAmount>();
        Refund(building, resources, items);
        string name = building.Definition.DisplayName;
        Vector3 at = building.transform.position;
        MaterialPool.For(user).GiveBack(resources, items, user.transform);
        SaveableEntity.RemoveForGood(building.gameObject);
        AudioManager.Play(building.Definition.DeathSound, at);
        string back = Describe(resources, items);
        GameMessages.Post(back.Length > 0 ? $"Took down the {name}. Back: {back}." : $"Took down the {name}.", MessageKind.Gain);
        return true;
    }

    private static int ItemsIn(StashChest chest)
    {
        if (chest == null || chest.Block < 0 || GameSession.Stash == null)
        {
            return 0;
        }

        ItemContainer stash = GameSession.Stash.Items;
        int count = 0;
        for (int slot = chest.FirstSlot; slot < chest.FirstSlot + StashStorage.ChestSlots && slot < stash.SlotCount; slot++)
        {
            ItemStack stack = stash.GetSlot(slot);
            count += stack.IsEmpty ? 0 : stack.Count;
        }

        return count;
    }

    private static string Describe(List<ResourceAmount> resources, List<ItemAmount> items)
    {
        string things = AmountText.Describe(items);
        string essence = AmountText.Describe(resources);
        return things.Length > 0 && essence.Length > 0 ? things + ", " + essence : things + essence;
    }

    private static void AddTo(List<ItemAmount> into, ItemDefinition item, int amount)
    {
        if (item == null || amount <= 0)
        {
            return;
        }

        for (int i = 0; i < into.Count; i++)
        {
            if (into[i].Item == item)
            {
                into[i] = new ItemAmount(item, into[i].Amount + amount);
                return;
            }
        }

        into.Add(new ItemAmount(item, amount));
    }

    private static void AddTo(List<ResourceAmount> into, ResourceDefinition resource, int amount)
    {
        if (resource == null || amount <= 0)
        {
            return;
        }

        for (int i = 0; i < into.Count; i++)
        {
            if (into[i].Resource == resource)
            {
                into[i] = new ResourceAmount(resource, into[i].Amount + amount);
                return;
            }
        }

        into.Add(new ResourceAmount(resource, amount));
    }
}
