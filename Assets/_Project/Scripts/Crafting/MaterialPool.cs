using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The materials a player can spend right now. In a raid that is only what they carry. At home (no raid running) the
/// stash counts too, so crafting, building, repairing and purifying never require hauling things out of storage first
/// (design decision, GDD §12.4), and so does the cargo of any cart or boat standing near them, so a load can be built
/// straight from the cart it came home in. Spending takes from what is carried first, then cargo, then storage.
/// <para>
/// Where it belongs: crafting and every "pay a cost" system (PlayerCrafting, BuildPlacer, repairs, the portal, the Heart
/// Tree, the Purification Altar) go through this class, so the rule lives in one place. Get one with For(player).
/// </para>
/// </summary>
public sealed class MaterialPool
{
    private readonly ResourceStorage carried;
    private readonly ItemContainer bag;
    private readonly ResourceStorage stored;
    private readonly ItemContainer storedItems;
    private readonly IReadOnlyList<ItemContainer> cargo;

    public MaterialPool(ResourceStorage carried, ItemContainer bag, ResourceStorage stored = null, ItemContainer storedItems = null,
        IReadOnlyList<ItemContainer> cargo = null)
    {
        this.carried = carried;
        this.bag = bag;
        this.stored = stored;
        this.storedItems = storedItems;
        this.cargo = cargo;
    }

    /// <summary>True when storage counts as well as what is carried.</summary>
    public bool IncludesStorage => stored != null || storedItems != null;

    /// <summary>The pool for a player: carried things, plus the stash and nearby cargo while at home.</summary>
    public static MaterialPool For(GameObject player)
    {
        ResourceInventory resources = player != null ? player.GetComponent<ResourceInventory>() : null;
        ItemInventory items = player != null ? player.GetComponent<ItemInventory>() : null;
        // In a friend's Haven (co-op) only what you carry counts: their stash and carts are theirs.
        bool atHome = RaidDirector.Current == null && !HavenVisit.IsVisiting;
        return new MaterialPool(
            resources != null ? resources.Storage : null,
            items != null ? items.Container : null,
            atHome ? GameSession.Stash.Resources : null,
            atHome ? GameSession.Stash.Items : null,
            atHome && player != null ? Transport.CargoNear(player.transform.position) : null);
    }

    public int Have(ResourceDefinition resource)
    {
        if (resource == null)
        {
            return 0;
        }

        return (carried != null ? carried.GetAmount(resource) : 0) + (stored != null ? stored.GetAmount(resource) : 0);
    }

    public int Have(ItemDefinition item)
    {
        if (item == null)
        {
            return 0;
        }

        int have = (bag != null ? bag.Count(item) : 0) + (storedItems != null ? storedItems.Count(item) : 0);
        if (cargo != null)
        {
            foreach (ItemContainer load in cargo)
            {
                have += load.Count(item);
            }
        }

        return have;
    }

    /// <summary>True when every cost can be paid. Otherwise names the first thing missing, for the player.</summary>
    public bool CanAfford(IEnumerable<ResourceAmount> resources, IEnumerable<ItemAmount> items, out string missing)
    {
        if (Cheats.FreeCosts)
        {
            missing = null;
            return true;
        }

        if (resources != null)
        {
            foreach (ResourceAmount cost in resources)
            {
                if (cost.Resource != null && Have(cost.Resource) < cost.Amount)
                {
                    missing = $"Not enough {cost.Resource.DisplayName} ({Have(cost.Resource)}/{cost.Amount}).";
                    return false;
                }
            }
        }

        if (items != null)
        {
            foreach (ItemAmount cost in items)
            {
                if (cost.Item != null && Have(cost.Item) < cost.Amount)
                {
                    missing = $"Not enough {cost.Item.DisplayName} ({Have(cost.Item)}/{cost.Amount}).";
                    return false;
                }
            }
        }

        missing = null;
        return true;
    }

    public bool CanAfford(IEnumerable<ResourceAmount> resources, IEnumerable<ItemAmount> items = null)
    {
        return CanAfford(resources, items, out _);
    }

    /// <summary>
    /// Pays every cost, carried things first. Returns false and pays nothing when something is missing, so a cost is
    /// never half paid.
    /// </summary>
    public bool TrySpend(IEnumerable<ResourceAmount> resources, IEnumerable<ItemAmount> items = null) => Pay(resources, items, null);

    /// <summary>
    /// TrySpend, noting in <paramref name="paid"/> what came from where, so a refund can put it back there (GiveBack(Payment)):
    /// a cancelled craft paid from the stash goes back to the stash, not into a bag it then fills.
    /// </summary>
    public bool TrySpend(IEnumerable<ResourceAmount> resources, IEnumerable<ItemAmount> items, out Payment paid)
    {
        paid = new Payment();
        return Pay(resources, items, paid);
    }

    /// <summary>What a payment took, and from where. Empty when costs are free (a cheat).</summary>
    public sealed class Payment
    {
        /// <summary>Resources from what the player carries.</summary>
        public readonly List<ResourceAmount> Carried = new List<ResourceAmount>();
        /// <summary>Resources from the stash.</summary>
        public readonly List<ResourceAmount> Stored = new List<ResourceAmount>();
        /// <summary>Items from the bag.</summary>
        public readonly List<ItemAmount> FromBag = new List<ItemAmount>();
        /// <summary>Items from a cart's cargo or the stash, which go back to the stash (the cart may have gone).</summary>
        public readonly List<ItemAmount> FromStore = new List<ItemAmount>();

        public bool IsEmpty => Carried.Count == 0 && Stored.Count == 0 && FromBag.Count == 0 && FromStore.Count == 0;
    }

    private bool Pay(IEnumerable<ResourceAmount> resources, IEnumerable<ItemAmount> items, Payment record)
    {
        if (!CanAfford(resources, items))
        {
            return false;
        }

        if (Cheats.FreeCosts)
        {
            return true;
        }

        if (resources != null)
        {
            foreach (ResourceAmount cost in resources)
            {
                Spend(cost.Resource, cost.Amount, record);
            }
        }

        if (items != null)
        {
            foreach (ItemAmount cost in items)
            {
                if (cost.Item == null || cost.Amount <= 0)
                {
                    continue;
                }

                int fromBag = bag != null ? bag.Remove(cost.Item, cost.Amount) : 0;
                int taken = fromBag;
                for (int i = 0; cargo != null && i < cargo.Count && taken < cost.Amount; i++)
                {
                    taken += cargo[i].Remove(cost.Item, cost.Amount - taken);
                }

                if (taken < cost.Amount && storedItems != null)
                {
                    taken += storedItems.Remove(cost.Item, cost.Amount - taken);
                }

                Note(record?.FromBag, cost.Item, fromBag);
                Note(record?.FromStore, cost.Item, taken - fromBag);
            }
        }

        return true;
    }

    private void Spend(ResourceDefinition resource, int amount, Payment record)
    {
        if (resource == null || amount <= 0)
        {
            return;
        }

        int taken = carried != null ? carried.Remove(resource, amount) : 0;
        int fromStore = taken < amount && stored != null ? stored.Remove(resource, amount - taken) : 0;
        if (record != null)
        {
            if (taken > 0)
            {
                record.Carried.Add(new ResourceAmount(resource, taken));
            }

            if (fromStore > 0)
            {
                record.Stored.Add(new ResourceAmount(resource, fromStore));
            }
        }
    }

    private static void Note(List<ItemAmount> into, ItemDefinition item, int amount)
    {
        if (into != null && amount > 0)
        {
            into.Add(new ItemAmount(item, amount));
        }
    }

    /// <summary>
    /// Puts back what <paramref name="paid"/> took, each where it came from: carried resources to what is carried and stored ones
    /// to the stash (the other when that is full or out of reach), bag items to the bag, cargo and stash items to the stash; what
    /// fits nowhere falls at <paramref name="dropAt"/> (ItemDelivery).
    /// </summary>
    public void GiveBack(Payment paid, Transform dropAt)
    {
        if (paid == null)
        {
            return;
        }

        foreach (ResourceAmount amount in paid.Carried)
        {
            int back = carried != null ? carried.Add(amount.Resource, amount.Amount) : 0;
            if (back < amount.Amount && stored != null)
            {
                stored.Add(amount.Resource, amount.Amount - back);
            }
        }

        foreach (ResourceAmount amount in paid.Stored)
        {
            int back = stored != null ? stored.Add(amount.Resource, amount.Amount) : 0;
            if (back < amount.Amount && carried != null)
            {
                carried.Add(amount.Resource, amount.Amount - back);
            }
        }

        foreach (ItemAmount amount in paid.FromBag)
        {
            ItemDelivery.Give(bag, new ItemStack(amount.Item, amount.Amount), storedItems, dropAt);
        }

        foreach (ItemAmount amount in paid.FromStore)
        {
            // Away from home (a save loaded elsewhere) the stash is out of reach: the bag, then the ground.
            ItemDelivery.Give(storedItems ?? bag, new ItemStack(amount.Item, amount.Amount), storedItems != null ? bag : null, dropAt);
        }
    }

    /// <summary>
    /// Adds resources to what the player carries; at home, what doesn't fit (cap or weight) goes to storage. Returns how
    /// many found a place.
    /// </summary>
    public int Add(ResourceDefinition resource, int amount)
    {
        if (resource == null || amount <= 0)
        {
            return 0;
        }

        int added = carried != null ? carried.Add(resource, amount) : 0;
        if (added < amount && stored != null)
        {
            added += stored.Add(resource, amount - added);
        }

        return added;
    }

    /// <summary>
    /// Gives back what a payment took (a decision the host's game turned down, HavenDecisions): resources as <see cref="Add"/>
    /// puts them, items into the bag; what no longer fits is dropped at <paramref name="dropAt"/>.
    /// </summary>
    public void GiveBack(IEnumerable<ResourceAmount> resources, IEnumerable<ItemAmount> items, Transform dropAt)
    {
        if (Cheats.FreeCosts)
        {
            return;
        }

        foreach (ResourceAmount amount in resources ?? System.Array.Empty<ResourceAmount>())
        {
            Add(amount.Resource, amount.Amount);
        }

        foreach (ItemAmount amount in items ?? System.Array.Empty<ItemAmount>())
        {
            if (amount.Item == null || amount.Amount <= 0)
            {
                continue;
            }

            // Into the bag, then the stash at home, then the ground (water runs off: ItemDelivery).
            ItemDelivery.Give(bag, new ItemStack(amount.Item, amount.Amount), storedItems, dropAt);
        }
    }

    /// <summary>How many of a resource can still be added by Add.</summary>
    public int FreeSpaceFor(ResourceDefinition resource)
    {
        if (resource == null)
        {
            return 0;
        }

        return (carried != null ? carried.GetFreeSpace(resource) : 0) + (stored != null ? stored.GetFreeSpace(resource) : 0);
    }
}
