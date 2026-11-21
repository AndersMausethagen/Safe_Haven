using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Taking down a building the player built (the user, 2026-11-20: "Allow us to destroy placed buildings, refunding materials"): picked
/// from the build menu's Manage tab (BuildPlacer), asked once in an ActionMenuWindow, then gone for good (SaveableEntity.RemoveForGood)
/// with everything it cost back, its upgrades too: into the bag, then the chests, then the ground (MaterialPool.GiveBack).
/// A damaged building keeps back what mending it would cost (BuildingRepairs), so taking it down and putting it up again isn't a free
/// repair; with Free Costs on (the cheat) nothing comes back, as nothing was paid (the review of 2026-11-21). Taking it down raises
/// TakenDown, so putting the same building up again isn't counted as new by a Build quest (QuestLog).
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

        // Anything else that holds things (a stable's feed trough, a cart shed): empty it first.
        foreach (ICargoHold hold in building.GetComponents<ICargoHold>())
        {
            ItemContainer cargo = hold.Cargo;
            for (int slot = 0; cargo != null && slot < cargo.SlotCount; slot++)
            {
                if (!cargo.GetSlot(slot).IsEmpty)
                {
                    return $"Empty the {hold.CargoTitle} first.";
                }
            }
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

    /// <summary>
    /// Everything that was paid for it: the building, each level it was raised to, and an upgrade paid for and not finished; less what
    /// mending its damage would cost, as the clearing's mending charges a share of the building's own cost.
    /// </summary>
    public static void Refund(BuildingObject building, List<ResourceAmount> resources, List<ItemAmount> items)
    {
        BuildingDefinition definition = building != null ? building.Definition : null;
        bool torch = building != null && building.TryGetComponent(out WallFittings fittings) && fittings.HasTorch;
        // Levels 2 and up finished, and the one being worked towards when it was paid for.
        int top = 1;
        if (building != null && building.TryGetComponent(out Construction construction))
        {
            top = construction.IsBuilding && construction.Paid && construction.Level >= 1 ? construction.Target : construction.Level;
        }

        Refund(definition, Damage(building), top, torch, resources, items);
    }

    /// <summary>
    /// What comes back for a <paramref name="definition"/> with <paramref name="damage"/> of its health lost (0 to 1), raised to
    /// <paramref name="topLevel"/>, with a wall torch or not: its own cost less the mending share of the damage, each level from 2 in
    /// full, the torch in full. Nothing with Free Costs on.
    /// </summary>
    public static void Refund(BuildingDefinition definition, float damage, int topLevel, bool torch, List<ResourceAmount> resources, List<ItemAmount> items)
    {
        resources.Clear();
        items.Clear();
        if (definition == null || Cheats.FreeCosts)
        {
            return;
        }

        float share = damage > 0f ? BuildingRepairs.CurrentShare : 0f;
        foreach (ResourceAmount cost in definition.ConstructionCost)
        {
            AddTo(resources, cost.Resource, cost.Amount - BuildingRepairs.Share(cost.Amount, damage, share));
        }

        foreach (ItemAmount cost in definition.ConstructionItems)
        {
            AddTo(items, cost.Item, cost.Amount - BuildingRepairs.Share(cost.Amount, damage, share));
        }

        // A wall piece's torch comes back with it (WallFittings).
        if (torch)
        {
            foreach (ItemAmount cost in definition.WallTorchCost)
            {
                AddTo(items, cost.Item, cost.Amount);
            }
        }

        for (int level = 2; level <= topLevel; level++)
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

    /// <summary>How much of its health it has lost, 0 to 1 (a scaffold never finished, or a building without health: 0).</summary>
    public static float Damage(BuildingObject building)
    {
        if (building == null || !building.TryGetComponent(out Damageable health) || health.MaxHealth <= 0f)
        {
            return 0f;
        }

        if (building.TryGetComponent(out Construction construction) && construction.IsBuilding && construction.Level < 1)
        {
            return 0f;
        }

        return Mathf.Clamp01(1f - health.HealthFraction);
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
        string body = Cheats.FreeCosts ? "Free building is on: it cost nothing, so nothing comes back."
            : back.Length == 0 ? "It cost nothing, so nothing comes back."
            : Damage(building) > 0.001f ? $"What it cost comes back, less what mending its damage would take:\n<b>{back}</b>"
            : $"Everything it cost comes back:\n<b>{back}</b>";
        var page = new MenuPage
        {
            Title = $"Take down the {name}?",
            Body = body,
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
        BuildingDefinition definition = building.Definition;
        string name = definition.DisplayName;
        Vector3 at = building.transform.position;
        // Only what stood (or was being raised a tier) was counted as built; a scaffold never finished wasn't Built.
        bool counted = !building.TryGetComponent(out Construction construction) || construction.Level >= 1;
        MaterialPool.For(user).GiveBack(resources, items, user.transform);
        SaveableEntity.RemoveForGood(building.gameObject);
        if (counted)
        {
            GameEvents.Raise(new GameEvent(GameEventKind.TakenDown, definition, 1, user));
        }
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
