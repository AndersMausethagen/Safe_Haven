using System.Collections.Generic;

/// <summary>
/// What happened when a batch of resources was offered to a storage: the part that fit and the part that did not.
/// Overflow is handed back instead of being discarded, so pickups, mail or other overflow rules can use it later.
/// </summary>
public sealed class ResourceGrantResult
{
    private readonly List<ResourceAmount> stored = new List<ResourceAmount>();
    private readonly List<ResourceAmount> overflow = new List<ResourceAmount>();

    public IReadOnlyList<ResourceAmount> Stored => stored;
    public IReadOnlyList<ResourceAmount> Overflow => overflow;
    public bool HasOverflow => overflow.Count > 0;

    public void Record(ResourceDefinition resource, int offered, int accepted)
    {
        if (accepted > 0)
        {
            stored.Add(new ResourceAmount(resource, accepted));
        }

        if (offered > accepted)
        {
            overflow.Add(new ResourceAmount(resource, offered - accepted));
        }
    }
}
