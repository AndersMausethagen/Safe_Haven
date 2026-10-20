using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hands looted or rewarded resources to someone: into their ResourceInventory, as far as caps and carry weight allow,
/// raising ResourceGathered for what arrived so quests and the HUD notice. Shared by monster loot, supply caches and
/// rewards, so every "you got resources" path behaves the same.
/// </summary>
public static class LootGrant
{
    /// <summary>Stores what fits and returns what didn't, so the caller can keep it (a cache) or report it.</summary>
    public static List<ResourceAmount> GiveResources(GameObject recipient, IReadOnlyList<ResourceAmount> resources)
    {
        var overflow = new List<ResourceAmount>();
        if (resources == null || resources.Count == 0)
        {
            return overflow;
        }

        ResourceInventory inventory = recipient != null ? recipient.GetComponentInParent<ResourceInventory>() : null;
        foreach (ResourceAmount entry in resources)
        {
            if (entry.Resource == null || entry.Amount <= 0)
            {
                continue;
            }

            int stored = inventory != null ? inventory.Storage.Add(entry.Resource, entry.Amount) : 0;
            if (stored > 0)
            {
                GameEvents.Raise(new GameEvent(GameEventKind.ResourceGathered, entry.Resource, stored, inventory.gameObject));
            }

            if (stored < entry.Amount)
            {
                overflow.Add(new ResourceAmount(entry.Resource, entry.Amount - stored));
            }
        }

        return overflow;
    }
}
