using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What a wall piece's Upgrade page offers besides a level (the user, 2026-11-21): "a wall section can be upgraded to a gate" and "a
/// wall section can be upgraded to hold a torch". Turning a piece into its gate (BuildingDefinition › Gate Version) pays the gate's cost,
/// gives the piece's back (its torch too) and puts the gate's scaffold up in its place, the same length and turn. A torch is fitted to the
/// piece (WallFittings), paid from the bag and the chests, and given back when it is taken down. Refused during a breach and in a friend's
/// Haven, as moving a building is.
/// </summary>
public static class WallUpgrades
{
    /// <summary>True when the piece has something to offer here: a gate it can become, or a torch it can hold.</summary>
    public static bool HasChoices(BuildingObject piece) =>
        piece != null && piece.Definition != null && (piece.Definition.GateVersion != null || (piece.TryGetComponent(out WallFittings f) && f.TakesTorch));

    /// <summary>The choices for a wall piece, added to its Upgrade page.</summary>
    public static void AddChoices(MenuPage page, BuildingObject piece, GameObject user)
    {
        BuildingDefinition wall = piece.Definition;
        MaterialPool pool = MaterialPool.For(user);
        var crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        BuildingDefinition gate = wall.GateVersion;
        if (gate != null)
        {
            bool known = crafting != null && crafting.IsKnown(gate);
            bool affordable = pool.CanAfford(gate.ConstructionCost, gate.ConstructionItems, out string missing);
            string cost = AmountText.Describe(gate.ConstructionItems);
            // What the piece gives back, named, so the swap's price is plain before it is made (the systems review, 2026-11-21).
            var backItems = new System.Collections.Generic.List<ItemAmount>();
            var backEssence = new System.Collections.Generic.List<ResourceAmount>();
            BuildingDemolition.Refund(piece, backEssence, backItems);
            string back = AmountText.Describe(backItems);
            string detail = !known ? $"You don't know the {gate.DisplayName} yet"
                : affordable ? $"{cost}; back: {(back.Length > 0 ? back : "nothing")}" : $"{cost}   <color={UIStyle.Current.DangerHex}>{missing}</color>";
            page.Add(new MenuAction($"Make it a {gate.DisplayName}", detail, known && affordable && WhyNot(piece) == null,
                () => MakeGate(piece, user), closesMenu: true) { Icon = "Fence" });
        }

        if (piece.TryGetComponent(out WallFittings fittings) && fittings.TakesTorch)
        {
            if (fittings.HasTorch)
            {
                page.Add(new MenuAction("Take the torch down", AmountText.Describe(wall.WallTorchCost) + " comes back", true,
                    () => RemoveTorch(piece, user), closesMenu: true) { Icon = "Flame" });
            }
            else
            {
                bool affordable = pool.CanAfford(null, wall.WallTorchCost, out string missing);
                string cost = AmountText.Describe(wall.WallTorchCost);
                page.Add(new MenuAction("Add a torch", affordable ? $"{cost}; burns a Charcoal a night" : $"{cost}   <color={UIStyle.Current.DangerHex}>{missing}</color>",
                    affordable && WhyNot(piece) == null, () => AddTorch(piece, user), closesMenu: true) { Icon = "Flame" });
            }
        }
    }

    /// <summary>Why the piece can't be changed now, or null.</summary>
    public static string WhyNot(BuildingObject piece)
    {
        if (HavenVisit.IsVisiting)
        {
            return "Only your own walls can be changed.";
        }

        return WardBreachDirector.UnderWay ? "Not while the ward is breached." : null;
    }

    /// <summary>Turns the piece into its gate. False, with a message, when it can't be.</summary>
    public static bool MakeGate(BuildingObject piece, GameObject user)
    {
        BuildingDefinition gate = piece != null && piece.Definition != null ? piece.Definition.GateVersion : null;
        string refused = gate == null ? "This can't become a gate." : WhyNot(piece);
        if (refused == null && (user == null || !user.TryGetComponent(out PlayerCrafting crafting) || !crafting.IsKnown(gate)))
        {
            refused = gate != null ? $"You don't know how to build the {gate.DisplayName} yet." : refused;
        }

        if (refused != null)
        {
            GameMessages.Post(refused, MessageKind.Warning);
            return false;
        }

        MaterialPool pool = MaterialPool.For(user);
        if (!pool.TrySpend(gate.ConstructionCost, gate.ConstructionItems))
        {
            GameMessages.Post($"Not enough for the {gate.DisplayName}.", MessageKind.Warning);
            return false;
        }

        var resources = new List<ResourceAmount>();
        var items = new List<ItemAmount>();
        BuildingDemolition.Refund(piece, resources, items);
        Vector3 at = piece.transform.position;
        Quaternion turn = piece.transform.rotation;
        SaveableEntity.RemoveForGood(piece.gameObject);
        pool.GiveBack(resources, items, user.transform);
        GameObject placed = gate.Prefab.TryGetComponent(out SaveableEntity saveable)
            ? SaveableEntity.Spawn(saveable, at, turn).gameObject
            : Object.Instantiate(gate.Prefab, at, turn);
        if (gate.BuildWork > 0f && placed.TryGetComponent(out Construction construction))
        {
            construction.BeginBuilding();
            GameMessages.Post($"The scaffold for the {gate.DisplayName} is up. Hold {{key:Hammer}} at it to build.");
        }
        else
        {
            GameMessages.Post($"Built the {gate.DisplayName}.", MessageKind.Gain);
            GameEvents.Raise(new GameEvent(GameEventKind.Built, gate, 1, user));
        }

        return true;
    }

    public static bool AddTorch(BuildingObject piece, GameObject user)
    {
        string refused = WhyNot(piece);
        if (refused != null || piece == null || !piece.TryGetComponent(out WallFittings fittings) || !fittings.TakesTorch || fittings.HasTorch)
        {
            GameMessages.Post(refused ?? "This can't hold a torch.", MessageKind.Warning);
            return false;
        }

        if (!MaterialPool.For(user).TrySpend(null, piece.Definition.WallTorchCost))
        {
            GameMessages.Post("Not enough for a torch.", MessageKind.Warning);
            return false;
        }

        fittings.AddTorch();
        GameMessages.Post($"A torch on the {piece.Definition.DisplayName}. It burns a Charcoal a night, from your bag or chests.", MessageKind.Gain);
        return true;
    }

    public static bool RemoveTorch(BuildingObject piece, GameObject user)
    {
        if (piece == null || !piece.TryGetComponent(out WallFittings fittings) || !fittings.RemoveTorch())
        {
            return false;
        }

        MaterialPool.For(user).GiveBack(null, piece.Definition.WallTorchCost, user != null ? user.transform : piece.transform);
        GameMessages.Post("Took the torch down.", MessageKind.Info);
        return true;
    }
}
