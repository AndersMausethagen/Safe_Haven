using UnityEngine;

/// <summary>
/// The offer to take a standing building up a level: what the next level costs and brings, and a button that pays and
/// puts the scaffold up (<see cref="Construction.TryOrderUpgrade"/>), then a wall piece's gate and torch and the building's modules.
/// A station shows it in its window's Upgrade tab (StationWindow, the user 2026-11-21: "Add an Upgrade tab to the buildings"); other
/// buildings, and the build menu's Manage › Upgrade, open it in an ActionMenuWindow. One page for both: <see cref="PageFor"/>.
/// </summary>
public static class ConstructionUpgradeOffer
{
    /// <summary>Opens the offer for <paramref name="building"/> to <paramref name="user"/>, or says why there is none.</summary>
    public static void Show(BuildingObject building, GameObject user)
    {
        if (!building.TryGetComponent(out Construction construction))
        {
            GameMessages.Post($"The {building.Definition.DisplayName} can't be upgraded.", MessageKind.Warning);
            return;
        }

        if (construction.IsBuilding)
        {
            GameMessages.Post(construction.Paid ? $"The {building.Definition.DisplayName} is waiting for you: hold {{key:Hammer}} at its scaffold."
                : $"Walk up to the {building.Definition.DisplayName} and hold {{key:Hammer}} to repair it.");
            return;
        }

        if (!HasChoices(building, construction))
        {
            GameMessages.Post($"The {construction.Title} is as good as it gets.");
            return;
        }

        ActionMenuWindow menu = ActionMenuWindow.Current;
        if (menu != null)
        {
            menu.Open(() => PageFor(building, user), user);
        }
    }

    /// <summary>True when it has a next tier, a wall piece's choices or modules to offer.</summary>
    public static bool HasChoices(BuildingObject building, Construction construction) =>
        building != null && construction != null && building.Definition != null
        && (building.Definition.UpgradeTo(construction.Level + 1) != null || WallUpgrades.HasChoices(building) || BuildingModules.TakesModules(building.Definition));

    /// <summary>
    /// The page as it stands now (its next tier read afresh, so a finished upgrade shows the one after), or null when there is nothing
    /// to offer: no Construction, a scaffold or ruin being worked on, or nothing beyond it.
    /// </summary>
    public static MenuPage PageFor(BuildingObject building, GameObject user)
    {
        if (building == null || user == null || !building.TryGetComponent(out Construction construction) || construction.IsBuilding
            || !HasChoices(building, construction))
        {
            return null;
        }

        return Page(building, construction, building.Definition.UpgradeTo(construction.Level + 1), user);
    }

    private static MenuPage Page(BuildingObject building, Construction construction, BuildingLevel next, GameObject user)
    {
        var page = next == null
            ? new MenuPage
            {
                Title = construction.Title,
                Body = WallUpgrades.HasChoices(building) ? "A piece of wall: make it a gate, or give it a torch for the night." : "At its highest tier.",
            }
            : new MenuPage
            {
                Title = $"{next.title} {building.Definition.DisplayName}",
                Body = $"Level {construction.Level + 1} of {building.Definition.MaxLevel}.\n"
                    + HavenBuildings.WhatTierBrings(building.Definition, construction.Level + 1, next, user.GetComponent<PlayerCrafting>()),
            };
        if (next != null)
        {
            bool affordable = MaterialPool.For(user).CanAfford(next.essence, next.cost, out string missing);
            string cost = next.CostText;
            page.Add(new MenuAction("Upgrade", affordable ? cost : $"{cost}   <color={UIStyle.Current.DangerHex}>{missing}</color>", affordable && BuildingUnlocks.IsOpen(next),
                () => construction.TryOrderUpgrade(user), closesMenu: true));
        }

        // A wall piece: its gate, and a torch (the user, 2026-11-21).
        if (WallUpgrades.HasChoices(building))
        {
            WallUpgrades.AddChoices(page, building, user);
        }

        // Its modules: the slots, what is fitted, and what fits (BuildingModules).
        BuildingModules.AddChoices(page, building, user);

        return page;
    }
}
