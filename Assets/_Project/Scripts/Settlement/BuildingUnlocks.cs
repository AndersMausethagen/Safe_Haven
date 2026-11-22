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

        return GameSession.Here.HavenRank >= building.RequiredRank && AllStand(building.Requires) && HasRoom(building);
    }

    /// <summary>How many of <paramref name="building"/> stand in the loaded scene: scaffolds and plans too.</summary>
    public static int Standing(BuildingDefinition building)
    {
        int count = 0;
        foreach (BuildingObject placed in BuildingObject.Active)
        {
            count += placed.Definition == building ? 1 : 0;
        }

        return count;
    }

    /// <summary>True while the Haven's rank allows another (BuildingDefinition › Limit By Rank).</summary>
    public static bool HasRoom(BuildingDefinition building) =>
        building == null || !building.IsLimited || Standing(building) < building.LimitAt(GameSession.Here.HavenRank);

    /// <summary>Whether what an upgrade asks for stands. The level below it is the upgrade's own business.</summary>
    public static bool IsOpen(BuildingLevel level) => Cheats.NoRequirements || level == null
        || (AllStand(level.requires) && GameSession.Here.BreachesWon >= level.breachesWon);

    /// <summary>What still has to stand, for the player: "Needs a Smithy." or "Needs a Workshop at Tier II." Empty when open.</summary>
    public static string Missing(BuildingDefinition building)
    {
        List<string> missing = Unmet(building.Requires);
        IReadOnlyList<HavenSettings.Rank> ranks = HavenSettings.Current.Ranks;
        if (GameSession.Here.HavenRank < building.RequiredRank && building.RequiredRank < ranks.Count)
        {
            missing.Insert(0, $"Safe Haven to be a {ranks[building.RequiredRank].name}");
        }

        // Full for the rank: what the next rank that allows more is called, and how many it allows.
        if (!HasRoom(building))
        {
            int rank = GameSession.Here.HavenRank;
            int now = building.LimitAt(rank);
            string next = null;
            for (int i = rank + 1; i < ranks.Count && next == null; i++)
            {
                if (building.LimitAt(i) > now)
                {
                    next = $" (a {ranks[i].name}: {building.LimitAt(i)})";
                }
            }

            string name = rank < ranks.Count ? ranks[rank].name : "Haven";
            return $"A {name} has room for {now} {building.DisplayName}{(now == 1 ? "" : "s")}{next ?? ""}." + (missing.Count > 0 ? $" Needs {string.Join(" and ", missing)}." : "");
        }

        return missing.Count > 0 ? $"Needs {string.Join(" and ", missing)}." : "";
    }

    /// <summary>What an upgrade still waits for, for the player. Empty when open.</summary>
    public static string Missing(BuildingLevel level)
    {
        List<string> missing = level != null ? Unmet(level.requires) : new List<string>();
        int toWin = level != null ? level.breachesWon - GameSession.Here.BreachesWon : 0;
        if (toWin > 0)
        {
            missing.Add(toWin == 1 ? "one more breach held off at the portal" : $"{toWin} more breaches held off at the portal");
        }

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

    /// <summary>The upgrades a won breach has just opened ("the Watch Post to Lookout"): those that ask exactly this many
    /// breaches held off and nothing else still missing.</summary>
    public static List<string> OpenedByBreaches(int won, IEnumerable<BuildingDefinition> all)
    {
        var opened = new List<string>();
        foreach (BuildingDefinition building in all)
        {
            if (building == null)
            {
                continue;
            }

            for (int tier = 2; tier <= building.MaxLevel; tier++)
            {
                BuildingLevel level = building.UpgradeTo(tier);
                if (level != null && level.breachesWon == won && AllStand(level.requires))
                {
                    opened.Add($"the {building.DisplayName} to {level.title}");
                }
            }
        }

        return opened;
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
