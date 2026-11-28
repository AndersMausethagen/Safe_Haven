using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Everything lying in camp, for the Haven window: the stash, what the stations have finished and the cargo of carts and boats,
/// added up per kind and shelved by what it is for. What the player carries isn't in camp; the bag shows that.
/// </summary>
public static class HavenStores
{
    private const string StashName = "Stash";
    private const string EssenceShelf = "Essences";

    private static readonly (ItemCategory Category, string Title)[] Shelves =
    {
        (ItemCategory.Material, "Materials"),
        (ItemCategory.Consumable, "Supplies"),
        (ItemCategory.Equipment, "Gear"),
        (ItemCategory.Tool, "Tools"),
        (ItemCategory.Valuable, "Valuables"),
        (ItemCategory.Quest, "Keepsakes"),
    };

    private sealed class Tally
    {
        public Object Definition;
        public int Total;
        public readonly List<(string Place, int Amount)> Places = new List<(string, int)>();

        public void Add(string place, int amount)
        {
            Total += amount;
            int known = Places.FindIndex(entry => entry.Place == place);
            if (known >= 0)
            {
                Places[known] = (place, Places[known].Amount + amount);
            }
            else
            {
                Places.Add((place, amount));
            }
        }
    }

    /// <summary>The camp as it is: the session's stash and workshops, and every transport in the scene.</summary>
    public static List<HavenShelf> Read()
    {
        var cargo = new List<(string, ItemContainer)>();
        foreach (Transport transport in Transport.All)
        {
            cargo.Add((transport.Definition != null ? transport.Definition.DisplayName : transport.name, transport.Cargo));
        }

        return Read(GameSession.Stash, GameSession.Here.Workshops, cargo);
    }

    public static List<HavenShelf> Read(StashStorage stash, WorkshopState workshops, IEnumerable<(string Place, ItemContainer Items)> cargo)
    {
        var tallies = new Dictionary<Object, Tally>();
        AddContainer(tallies, StashName, stash.Items);
        foreach (ResourceAmount stored in stash.Resources.StoredAmounts)
        {
            Add(tallies, stored.Resource, StashName, stored.Amount);
        }

        foreach ((string place, ItemContainer items) in cargo)
        {
            AddContainer(tallies, place, items);
        }

        foreach (StationQueue queue in workshops.Queues)
        {
            Add(tallies, queue.Output, queue.Type.DisplayName(), queue.OutputAmount);
        }

        var shelves = new List<HavenShelf>();
        foreach ((ItemCategory category, string title) in Shelves)
        {
            var shelf = new HavenShelf(title);
            shelf.Entries.AddRange(tallies.Values
                .Where(tally => tally.Definition is ItemDefinition item && item.Category == category)
                .OrderByDescending(tally => ((ItemDefinition)tally.Definition).Tier)
                .ThenBy(tally => ((ItemDefinition)tally.Definition).DisplayName)
                .Select(tally => ItemEntry(tally, (ItemDefinition)tally.Definition)));
            shelves.Add(shelf);
        }

        var essences = new HavenShelf(EssenceShelf);
        essences.Entries.AddRange(tallies.Values
            .Where(tally => tally.Definition is ResourceDefinition)
            .OrderByDescending(tally => ((ResourceDefinition)tally.Definition).Tier)
            .ThenBy(tally => ((ResourceDefinition)tally.Definition).DisplayName)
            .Select(tally => ResourceEntry(tally, (ResourceDefinition)tally.Definition)));
        shelves.Add(essences);
        return shelves;
    }

    private static void AddContainer(Dictionary<Object, Tally> tallies, string place, ItemContainer items)
    {
        if (items == null)
        {
            return;
        }

        // Slots past the unlocked ones count too: what a lost chest closed off is still there.
        for (int i = 0; i < items.SlotCount; i++)
        {
            ItemStack stack = items.GetSlot(i);
            if (!stack.IsEmpty)
            {
                Add(tallies, stack.Item, place, stack.Count);
            }
        }
    }

    private static void Add(Dictionary<Object, Tally> tallies, Object definition, string place, int amount)
    {
        if (definition == null || amount <= 0)
        {
            return;
        }

        if (!tallies.TryGetValue(definition, out Tally tally))
        {
            tally = new Tally { Definition = definition };
            tallies.Add(definition, tally);
        }

        tally.Add(place, amount);
    }

    private static string Places(Tally tally) => tally.Places.Count == 1 ? tally.Places[0].Place
        : string.Join(", ", tally.Places.Select(entry => $"{entry.Place} {entry.Amount}"));

    private static HavenEntry ItemEntry(Tally tally, ItemDefinition item) => Entry(tally, item.DisplayName, item.Icon, item.Description);

    private static HavenEntry ResourceEntry(Tally tally, ResourceDefinition resource) =>
        Entry(tally, resource.DisplayName, resource.Icon, resource.Description);

    private static HavenEntry Entry(Tally tally, string name, Sprite icon, string description)
    {
        var entry = new HavenEntry
        {
            Key = tally.Definition,
            Name = name,
            Icon = icon,
            Line = Places(tally),
            Badge = tally.Total.ToString(),
            State = HavenEntryState.Good,
            Status = $"{tally.Total} in camp",
            Description = description,
        };
        entry.Details.Add(new HavenDetail("Where it is", string.Join("\n", tally.Places.Select(place => $"{place.Place}: {place.Amount}"))));
        AddUses(entry, tally.Definition);
        return entry;
    }

    // What it goes into and where it is made, from the recipes the player knows (the UX pass of 2026-11-28: the pane said only where
    // it lay, so whether to keep it or trade it away needed the crafting window open beside it).
    private static void AddUses(HavenEntry entry, Object definition)
    {
        PlayerCrafting crafting = Player.Current != null ? Player.Current.GetComponent<PlayerCrafting>() : null;
        if (crafting == null)
        {
            return;
        }

        var usedIn = new SortedSet<string>();
        var madeAt = new SortedSet<string>();
        foreach (RecipeDefinition recipe in crafting.KnownRecipes)
        {
            if (recipe == null || recipe.IsThriftierCopy)
            {
                continue;
            }

            if (recipe.OutputDefinition == definition)
            {
                madeAt.Add(recipe.Station.DisplayName());
            }

            bool uses = recipe.ResourceCost.Any(part => part.Resource == definition) || recipe.ItemCost.Any(part => part.Item == definition);
            if (uses && recipe.HasOutput)
            {
                usedIn.Add(recipe.DisplayName);
            }
        }

        if (madeAt.Count > 0)
        {
            entry.Details.Add(new HavenDetail("Made at", string.Join(", ", madeAt)));
        }

        if (usedIn.Count > 0)
        {
            const int most = 6;
            string list = string.Join(", ", usedIn.Take(most)) + (usedIn.Count > most ? $" and {usedIn.Count - most} more" : "");
            entry.Details.Add(new HavenDetail($"Goes into ({usedIn.Count})", list));
        }
    }
}
