using System.Collections.Generic;

/// <summary>
/// What the player would lose if the raid failed right now (GDD §18.3, §8.3): everything carried except knowledge, the
/// safe pocket and the deployed equipment. Shown on the raid HUD so pressing on is a visible gamble.
/// </summary>
public readonly struct RaidRisk
{
    public readonly int Items;
    public readonly int Value;
    public readonly int Resources;

    public RaidRisk(int items, int value, int resources)
    {
        Items = items;
        Value = value;
        Resources = resources;
    }

    public bool IsEmpty => Items == 0 && Resources == 0;

    /// <param name="carried">What the player holds now.</param>
    /// <param name="loadout">What the player carried into the raid, which decides the equipment that is kept.</param>
    public static RaidRisk Of(PlayerSnapshot carried, PlayerSnapshot loadout)
    {
        if (carried == null)
        {
            return default;
        }

        PlayerSnapshot kept = carried.AfterFailedRaid(loadout, 0f);
        Dictionary<ItemDefinition, int> keptItems = kept.CountItems();
        int items = 0;
        int value = 0;
        foreach (KeyValuePair<ItemDefinition, int> entry in carried.CountItems())
        {
            keptItems.TryGetValue(entry.Key, out int keep);
            int lost = entry.Value - keep;
            if (lost > 0 && entry.Key != null)
            {
                items += lost;
                value += lost * entry.Key.Value;
            }
        }

        Dictionary<ResourceDefinition, int> keptResources = kept.CountResources();
        int resources = 0;
        foreach (KeyValuePair<ResourceDefinition, int> entry in carried.CountResources())
        {
            keptResources.TryGetValue(entry.Key, out int keep);
            resources += System.Math.Max(0, entry.Value - keep);
        }

        return new RaidRisk(items, value, resources);
    }
}
