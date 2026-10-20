/// <summary>Describes one resource's quantity or capacity changing in a ResourceStorage.</summary>
public readonly struct ResourceChange
{
    public ResourceChange(ResourceDefinition resource, int previousAmount, int amount, int capacity)
    {
        Resource = resource;
        PreviousAmount = previousAmount;
        Amount = amount;
        Capacity = capacity;
    }

    public ResourceDefinition Resource { get; }
    public int PreviousAmount { get; }
    public int Amount { get; }
    /// <summary>The capacity in effect after the change.</summary>
    public int Capacity { get; }
    /// <summary>Quantity added (positive) or removed (negative). Zero when only the capacity changed.</summary>
    public int Delta => Amount - PreviousAmount;
}
