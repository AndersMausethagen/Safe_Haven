using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gives a GameObject (the player, for now) an essence inventory. It owns the runtime ResourceStorage and applies the
/// capacities and starting amounts configured here. Gameplay and UI talk to Storage. Essences are weightless; the carry
/// budget belongs to the bag (ItemInventory).
/// <para>
/// Balancing: the per-resource cap is set on the PlayerArmature prefab and shown on the Carrying page of the Systems
/// Editor.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class ResourceInventory : MonoBehaviour
{
    [Serializable]
    public struct CapacityOverride
    {
        public ResourceDefinition resource;
        [Min(0)] public int capacity;
    }

    [Tooltip("How much of each resource this inventory can carry. Backpacks, upgrades and skills raise it later.")]
    [SerializeField, Min(0)] private int baseCapacity = ResourceStorage.DefaultBaseCapacity;
    [Tooltip("Capacities that replace the base capacity for individual resources.")]
    [SerializeField] private List<CapacityOverride> capacityOverrides = new List<CapacityOverride>();
    [Tooltip("Amounts stored when the game starts. Useful for testing.")]
    [SerializeField] private List<ResourceAmount> startingResources = new List<ResourceAmount>();

    private ResourceStorage storage;

    public int BaseCapacity => baseCapacity;

    // Created on first use so other components can reach it regardless of Awake order.
    public ResourceStorage Storage
    {
        get
        {
            if (storage == null)
            {
                storage = CreateStorage();
            }

            return storage;
        }
    }

    private void Awake()
    {
        _ = Storage;
    }

    private ResourceStorage CreateStorage()
    {
        var created = new ResourceStorage(baseCapacity);

        foreach (CapacityOverride capacityOverride in capacityOverrides)
        {
            created.SetCapacityOverride(capacityOverride.resource, capacityOverride.capacity);
        }

        foreach (ResourceAmount starting in startingResources)
        {
            created.Add(starting.Resource, starting.Amount);
        }

        return created;
    }
}
