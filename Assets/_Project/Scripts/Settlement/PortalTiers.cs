using System.Collections.Generic;

/// <summary>
/// The portal's tier: the one ladder the Haven shows its progress on (decision 8.3 of the design review, 2026-11-14, after
/// the spec's §4.3). It is made of what already exists: 0 Dormant (not repaired), 1 Awakened (repaired), then one per
/// ward level: 2 Stabilized, 3 Restored, 4 Reclaimed. Raising it costs the ward level's materials and asks one or two
/// more things (a building at a tier, a resident at home, a boss beaten: SettlementSettings.WardLevel). The other ladders
/// (renown, building tiers, gear) stay, shown as what the next tier needs. Names and lines are data (SettlementSettings).
/// </summary>
public static class PortalTiers
{
    /// <summary>The portal's tier now.</summary>
    public static int Current => Of(GameSession.Settlement);

    /// <summary>The tier a settlement's portal stands at.</summary>
    public static int Of(SettlementState settlement) => settlement == null || !settlement.PortalRepaired ? 0 : 1 + settlement.WardLevel;

    /// <summary>The highest tier there is.</summary>
    public static int Most => 1 + SettlementSettings.Current.WardLevels.Count;

    public static string Name(int tier) => SettlementSettings.Current.PortalTierName(tier);

    /// <summary>"Tier II · Stabilized".</summary>
    public static string Title(int tier) => $"Tier {Tiers.ToRoman(tier)} · {Name(tier)}";

    public static string Line(int tier) => SettlementSettings.Current.PortalTierLine(tier);

    /// <summary>The ward level that raises the portal past this tier, or null (not repaired yet, or the last tier).</summary>
    public static SettlementSettings.WardLevel Next(SettlementState settlement)
    {
        IReadOnlyList<SettlementSettings.WardLevel> levels = SettlementSettings.Current.WardLevels;
        return settlement != null && settlement.PortalRepaired && settlement.WardLevel < levels.Count ? levels[settlement.WardLevel] : null;
    }

    /// <summary>What still has to happen before a ward level can be raised, beyond its materials, for the player:
    /// "Needs a Smithy at Tier II and Yrsa living here." Empty when nothing is missing.</summary>
    public static string Missing(SettlementSettings.WardLevel level, SettlementState settlement)
    {
        if (level == null || Cheats.NoRequirements)
        {
            return "";
        }

        var missing = new List<string>();
        foreach (BuildingRequirement requirement in level.requires)
        {
            if (requirement.building != null && BuildingUnlocks.HighestTier(requirement.building) < requirement.tier)
            {
                missing.Add(BuildingUnlocks.Describe(requirement));
            }
        }

        foreach (ResidentDefinition resident in level.residents)
        {
            if (resident != null && settlement.Find(resident) == null)
            {
                missing.Add($"{resident.DisplayName} living here");
            }
        }

        int beaten = settlement.DefeatedBosses.Count;
        if (beaten < level.bossesBeaten)
        {
            missing.Add(level.bossesBeaten - beaten == 1 ? "a great foe of the woods beaten" : $"{level.bossesBeaten - beaten} more great foes beaten");
        }

        return missing.Count > 0 ? $"Needs {string.Join(" and ", missing)}." : "";
    }
}
