/// <summary>
/// The kind of gathering tool an item is, and the kind a resource node asks for. Values are serialized as numbers, so
/// add new kinds at the end and never renumber.
/// </summary>
public enum ToolType
{
    /// <summary>Not a tool, or (on a resource) gathered by hand.</summary>
    None = 0,
    /// <summary>Fells trees: Oak Wood, Hardwood, Elderwood.</summary>
    Axe = 1,
    /// <summary>Breaks rock and ore.</summary>
    Pickaxe = 2,
    /// <summary>Cuts plants for fiber.</summary>
    Sickle = 3,
}
