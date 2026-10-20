using System;
using UnityEngine;

/// <summary>A quantity of one resource: an inventory entry, a drop result, a recipe cost.</summary>
[Serializable]
public struct ResourceAmount
{
    [SerializeField] private ResourceDefinition resource;
    [SerializeField, Min(0)] private int amount;

    public ResourceAmount(ResourceDefinition resource, int amount)
    {
        this.resource = resource;
        this.amount = amount;
    }

    public ResourceDefinition Resource => resource;
    public int Amount => amount;
}
