using UnityEngine;

/// <summary>
/// The offer to take a standing building up a level: what the next level costs and brings, and a button that pays and
/// puts the scaffold up (<see cref="Construction.TryOrderUpgrade"/>). Opened from a station's menu and when the player
/// picks a building to upgrade in the build menu.
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

        BuildingLevel next = building.Definition.UpgradeTo(construction.Level + 1);
        bool wall = WallUpgrades.HasChoices(building);
        if (next == null && !wall)
        {
            GameMessages.Post($"The {construction.Title} is as good as it gets.");
            return;
        }

        ActionMenuWindow menu = ActionMenuWindow.Current;
        if (menu != null)
        {
            menu.Open(() => Page(building, construction, next, user), user);
        }
    }

    private static MenuPage Page(BuildingObject building, Construction construction, BuildingLevel next, GameObject user)
    {
        var page = next == null
            ? new MenuPage { Title = building.Definition.DisplayName, Body = "A piece of wall: make it a gate, or give it a torch for the night." }
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

        return page;
    }
}
