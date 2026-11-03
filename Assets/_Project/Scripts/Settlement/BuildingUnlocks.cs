using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which buildings the base has opened: each building opens the next (BuildingDefinition.Requires), so Safe Haven grows one new
/// thing at a time. Read live from what stands in the loaded scene; what the player has learned is PlayerCrafting's business.
/// </summary>
public static class BuildingUnlocks
{
    /// <summary>The highest tier standing of a kind of building: 0 when none stands, and scaffolds don't count.</summary>
    public static int HighestTier(BuildingDefinition building)
    {
        int best = 0;
        foreach (BuildingObject placed in BuildingObject.Active)
        {
            if (placed.Definition != building)
            {
                continue;
            }

            best = Mathf.Max(best, placed.TryGetComponent(out Construction construction) ? construction.Level : 1);
        }

        return best;
    }

    public static bool IsOpen(BuildingDefinition building)
    {
        if (Cheats.NoRequirements)
        {
            return true;
        }

        return GameSession.Settlement.HavenRank >= building.RequiredRank && AllStand(building.Requires);
    }

    /// <summary>Whether what an upgrade asks for stands. The level below it is the upgrade's own business.</summary>
    public static bool IsOpen(BuildingLevel level) => Cheats.NoRequirements || level == null || AllStand(level.requires);

    /// <summary>What still has to stand, for the player: "Needs a Smithy." or "Needs a Workshop at Tier II." Empty when open.</summary>
    public static string Missing(BuildingDefinition building)
    {
        List<string> missing = Unmet(building.Requires);
        IReadOnlyList<HavenSettings.Rank> ranks = HavenSettings.Current.Ranks;
        if (GameSession.Settlement.HavenRank < building.RequiredRank && building.RequiredRank < ranks.Count)
        {
            missing.Insert(0, $"Safe Haven to be a {ranks[building.RequiredRank].name}");
        }

        return missing.Count > 0 ? $"Needs {string.Join(" and ", missing)}." : "";
    }

    /// <summary>What an upgrade still waits for, for the player. Empty when open.</summary>
    public static string Missing(BuildingLevel level)
    {
        List<string> missing = level != null ? Unmet(level.requires) : new List<string>();
        return missing.Count > 0 ? $"Needs {string.Join(" and ", missing)}." : "";
    }

    private static bool AllStand(IEnumerable<BuildingRequirement> requirements) => Unmet(requirements).Count == 0;

    private static List<string> Unmet(IEnumerable<BuildingRequirement> requirements)
    {
        var missing = new List<string>();
        foreach (BuildingRequirement requirement in requirements)
        {
            if (requirement.building != null && HighestTier(requirement.building) < requirement.tier)
            {
                missing.Add(Describe(requirement));
            }
        }

        return missing;
    }

    /// <summary>"a Workshop" at tier 1, "a Workshop at Tier II" above it.</summary>
    public static string Describe(BuildingRequirement requirement)
    {
        string name = requirement.building.DisplayName;
        string article = "AEIOU".IndexOf(char.ToUpperInvariant(name[0])) >= 0 ? "an" : "a";
        return requirement.tier > 1 ? $"{article} {name} at Tier {Tiers.ToRoman(requirement.tier)}" : $"{article} {name}";
    }

    /// <summary>The buildings a tier of this one opens, for showing what is worth reaching for.</summary>
    public static List<string> OpenedBy(BuildingDefinition building, int tier, IEnumerable<BuildingDefinition> all)
    {
        var opened = new List<string>();
        foreach (BuildingDefinition other in all)
        {
            if (other == null || !other.ShowInBuildMenu)
            {
                continue;
            }

            foreach (BuildingRequirement requirement in other.Requires)
            {
                if (requirement.building == building && requirement.tier == tier)
                {
                    opened.Add(other.DisplayName);
                    break;
                }
            }
        }

        return opened;
    }
}
