using UnityEngine;

/// <summary>
/// What items and essences (ResourceDefinition) have in common for code that handles either: a recipe's output, a
/// station's queue, a saved id. Where an amount of one goes differs (the bag, or the essence storage), so giving stays
/// with the inventories.
/// </summary>
public interface IStackDefinition : IDisplayName
{
    /// <summary>Its id in saves.</summary>
    string Id { get; }

    Sprite Icon { get; }

    /// <summary>How many fit in one slot of a grid that shows it; essences have no slots, so any number.</summary>
    int StackSize { get; }

    /// <summary>The colour of a slot holding it, or null for a plain slot (common items).</summary>
    Color? SlotTint { get; }
}
