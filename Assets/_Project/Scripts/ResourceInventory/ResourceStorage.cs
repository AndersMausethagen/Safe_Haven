using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime essence quantities (GDD §12.1): each resource has its own stored amount and nothing occupies slots. Essences
/// are weightless, so the only limit is the per-resource capacity: every resource shares one base capacity, raised by
/// capacity bonuses that each apply up to a resource tier, unless the resource has a per-resource override.
/// Lowering a limit never destroys stored resources: the amount stays above the new limit until it is spent,
/// and nothing more can be added in the meantime.
/// The storage also remembers which resources have ever been collected, so the UI can reveal them as they are found.
/// Resource definitions are only ever read, so no authored data changes while playing.
/// Every method that changes quantities returns how much actually changed, so callers can handle partial results.
/// </summary>
public sealed class ResourceStorage
{
    public const int DefaultBaseCapacity = 10;

    private readonly struct CapacityBonus
    {
        public readonly int Amount;
        public readonly int MaxTier;

        public CapacityBonus(int amount, int maxTier)
        {
            Amount = amount;
            MaxTier = maxTier;
        }
    }

    private readonly Dictionary<ResourceDefinition, int> amounts = new Dictionary<ResourceDefinition, int>();
    private readonly Dictionary<ResourceDefinition, int> capacityOverrides = new Dictionary<ResourceDefinition, int>();
    private readonly Dictionary<object, CapacityBonus> capacityBonuses = new Dictionary<object, CapacityBonus>();
    private readonly HashSet<ResourceDefinition> discovered = new HashSet<ResourceDefinition>();
    private int baseCapacity;

    public ResourceStorage(int baseCapacity = DefaultBaseCapacity)
    {
        this.baseCapacity = Mathf.Max(0, baseCapacity);
    }

    /// <summary>Raised after a resource's quantity or per-resource capacity changes.</summary>
    public event Action<ResourceChange> Changed;

    /// <summary>Raised after the base capacity or a capacity bonus changes, which can change every resource's capacity.</summary>
    public event Action CapacitiesChanged;

    /// <summary>Raised the first time a resource is collected.</summary>
    public event Action<ResourceDefinition> Discovered;

    /// <summary>The capacity of every resource that has no override, before bonuses.</summary>
    public int BaseCapacity => baseCapacity;

    /// <summary>Every resource with a stored amount above zero.</summary>
    public IEnumerable<ResourceAmount> StoredAmounts
    {
        get
        {
            foreach (KeyValuePair<ResourceDefinition, int> entry in amounts)
            {
                yield return new ResourceAmount(entry.Key, entry.Value);
            }
        }
    }

    /// <summary>Every resource that has been collected at least once.</summary>
    public IReadOnlyCollection<ResourceDefinition> DiscoveredResources => discovered;

    public int GetAmount(ResourceDefinition resource)
    {
        return resource != null && amounts.TryGetValue(resource, out int amount) ? amount : 0;
    }

    public int GetCapacity(ResourceDefinition resource)
    {
        if (resource == null)
        {
            return 0;
        }

        if (capacityOverrides.TryGetValue(resource, out int capacity))
        {
            return capacity;
        }

        return baseCapacity + GetCapacityBonus(resource.Tier);
    }

    /// <summary>The total of every capacity bonus that applies to resources of the tier.</summary>
    public int GetCapacityBonus(int tier)
    {
        int bonus = 0;
        foreach (CapacityBonus entry in capacityBonuses.Values)
        {
            if (tier <= entry.MaxTier)
            {
                bonus += entry.Amount;
            }
        }

        return bonus;
    }

    /// <summary>How many more units fit under the resource's capacity.</summary>
    public int GetFreeSpace(ResourceDefinition resource)
    {
        return Mathf.Max(0, GetCapacity(resource) - GetAmount(resource));
    }

    /// <summary>True when the resource is at (or above) its capacity.</summary>
    public bool IsAtCapacity(ResourceDefinition resource)
    {
        return resource != null && GetAmount(resource) >= GetCapacity(resource);
    }

    public bool IsFull(ResourceDefinition resource)
    {
        return resource != null && GetFreeSpace(resource) == 0;
    }

    /// <summary>True when at least the given amount is stored.</summary>
    public bool Contains(ResourceDefinition resource, int amount)
    {
        return resource != null && amount >= 0 && GetAmount(resource) >= amount;
    }

    /// <summary>True once the resource has been collected at least once.</summary>
    public bool IsDiscovered(ResourceDefinition resource)
    {
        return resource != null && discovered.Contains(resource);
    }

    /// <summary>Reveals a resource without collecting it, for example when loading a save.</summary>
    public void MarkDiscovered(ResourceDefinition resource)
    {
        if (resource != null && discovered.Add(resource))
        {
            Discovered?.Invoke(resource);
        }
    }

    /// <summary>Adds as much as fits and returns the amount actually added.</summary>
    public int Add(ResourceDefinition resource, int amount)
    {
        if (resource == null || amount <= 0)
        {
            return 0;
        }

        int previous = GetAmount(resource);
        int capacity = GetCapacity(resource);
        int added = Mathf.Clamp(GetFreeSpace(resource), 0, amount);
        if (added > 0)
        {
            Store(resource, previous, previous + added, capacity);
            MarkDiscovered(resource);
        }

        return added;
    }

    /// <summary>Removes as much as is stored, up to the amount requested, and returns the amount actually removed.</summary>
    public int Remove(ResourceDefinition resource, int amount)
    {
        if (resource == null || amount <= 0)
        {
            return 0;
        }

        int previous = GetAmount(resource);
        int removed = Mathf.Min(previous, amount);
        if (removed > 0)
        {
            Store(resource, previous, previous - removed, GetCapacity(resource));
        }

        return removed;
    }

    /// <summary>Offers a batch of resources and reports what was stored and what did not fit.</summary>
    public ResourceGrantResult Add(IReadOnlyList<ResourceAmount> offered)
    {
        var result = new ResourceGrantResult();
        for (int i = 0; i < offered.Count; i++)
        {
            ResourceAmount entry = offered[i];
            result.Record(entry.Resource, entry.Amount, Add(entry.Resource, entry.Amount));
        }

        return result;
    }

    /// <summary>
    /// Sets the stored amount exactly, ignoring capacity, for restoring saved state (a scene change, a load). Like a
    /// lowered capacity, an amount above the limit stays until it is spent. Use Add and Remove for gameplay.
    /// </summary>
    public void SetAmount(ResourceDefinition resource, int amount)
    {
        if (resource == null)
        {
            return;
        }

        amount = Mathf.Max(0, amount);
        int previous = GetAmount(resource);
        if (amount != previous)
        {
            Store(resource, previous, amount, GetCapacity(resource));
        }

        if (amount > 0)
        {
            MarkDiscovered(resource);
        }
    }

    /// <summary>Moves every stored resource into another storage, as much as it has room for. Returns how many units moved.</summary>
    public int TransferAllTo(ResourceStorage target)
    {
        if (target == null || target == this)
        {
            return 0;
        }

        int moved = 0;
        // Copied first because removing changes the dictionary being read.
        foreach (ResourceAmount stored in new List<ResourceAmount>(StoredAmounts))
        {
            int added = target.Add(stored.Resource, stored.Amount);
            if (added > 0)
            {
                Remove(stored.Resource, added);
                moved += added;
            }
        }

        return moved;
    }

    /// <summary>Sets the capacity of every resource that has no override, before bonuses.</summary>
    public void SetBaseCapacity(int capacity)
    {
        capacity = Mathf.Max(0, capacity);
        if (capacity == baseCapacity)
        {
            return;
        }

        baseCapacity = capacity;
        NotifyCapacitiesChanged();
    }

    /// <summary>
    /// Adds or replaces the capacity bonus from one source (a backpack, a skill) for resources up to maxTier. Several
    /// sources add up.
    /// </summary>
    public void SetCapacityBonus(object source, int amount, int maxTier)
    {
        if (source == null)
        {
            return;
        }

        var bonus = new CapacityBonus(Mathf.Max(0, amount), maxTier);
        if (capacityBonuses.TryGetValue(source, out CapacityBonus current) && current.Amount == bonus.Amount && current.MaxTier == bonus.MaxTier)
        {
            return;
        }

        capacityBonuses[source] = bonus;
        NotifyCapacitiesChanged();
    }

    public void RemoveCapacityBonus(object source)
    {
        if (source != null && capacityBonuses.Remove(source))
        {
            NotifyCapacitiesChanged();
        }
    }

    /// <summary>Replaces one resource's capacity, regardless of the base capacity and bonuses.</summary>
    public void SetCapacityOverride(ResourceDefinition resource, int capacity)
    {
        if (resource == null)
        {
            return;
        }

        capacityOverrides[resource] = Mathf.Max(0, capacity);
        NotifyCapacityChanged(resource);
    }

    /// <summary>Goes back to the base capacity for this resource.</summary>
    public void ClearCapacityOverride(ResourceDefinition resource)
    {
        if (resource != null && capacityOverrides.Remove(resource))
        {
            NotifyCapacityChanged(resource);
        }
    }

    private void NotifyCapacityChanged(ResourceDefinition resource)
    {
        int amount = GetAmount(resource);
        Notify(new ResourceChange(resource, amount, amount, GetCapacity(resource)));
    }

    private void Store(ResourceDefinition resource, int previous, int amount, int capacity)
    {
        if (amount > 0)
        {
            amounts[resource] = amount;
        }
        else
        {
            amounts.Remove(resource);
        }

        Notify(new ResourceChange(resource, previous, amount, capacity));
    }

    // Each listener is invoked in isolation so a faulty one (a UI bug, say) can't break gathering or the others.
    private void Notify(ResourceChange change)
    {
        Action<ResourceChange> handlers = Changed;
        if (handlers == null)
        {
            return;
        }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<ResourceChange>)handler)(change);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private void NotifyCapacitiesChanged()
    {
        Action handlers = CapacitiesChanged;
        if (handlers == null)
        {
            return;
        }

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
