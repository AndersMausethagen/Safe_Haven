using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How much Safe Haven is worth, added up from what stands in it (HavenSettings has the numbers): every finished
/// building's renown plus its upgrades (another of a kind already standing brings only part of it, so variety pays),
/// the share of overgrowth cleared, and the residents living there. Pure reading of the loaded scene and
/// GameSession.Here; the parts are shown in the Haven window (HavenOverview).
/// </summary>
public static class HavenRenown
{
    public readonly struct Part
    {
        public readonly string Label;
        public readonly int Renown;

        public Part(string label, int renown)
        {
            Label = label;
            Renown = renown;
        }
    }

    /// <summary>The total, with what it is made of when <paramref name="parts"/> is given.</summary>
    public static int Compute(List<Part> parts = null)
    {
        HavenSettings settings = HavenSettings.Current;
        parts?.Clear();
        int total = 0;
        // Each kind's best (with its upgrades) counts in full and the others at the repeat share, whatever order they stand in: by list
        // order, moving a level-3 house (off and on again, so last) behind a level-1 one lowered the renown.
        var kinds = new Dictionary<BuildingDefinition, List<(float worth, string title)>>();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            BuildingDefinition definition = building.Definition;
            if (definition == null || definition.Faction != Faction.Player)
            {
                continue;
            }

            Construction construction = building.GetComponent<Construction>();
            if (construction != null && !construction.IsStanding)
            {
                continue;
            }

            if (!kinds.TryGetValue(definition, out List<(float worth, string title)> standing))
            {
                kinds[definition] = standing = new List<(float worth, string title)>();
            }

            standing.Add((definition.Renown + (construction != null ? construction.Sum(level => level.renown) : 0f),
                construction != null ? construction.Title : definition.DisplayName));
        }

        foreach (List<(float worth, string title)> standing in kinds.Values)
        {
            standing.Sort((a, b) => b.worth.CompareTo(a.worth));
            for (int i = 0; i < standing.Count; i++)
            {
                int renown = Mathf.RoundToInt(i == 0 ? standing[i].worth : standing[i].worth * settings.RepeatShare);
                if (renown > 0)
                {
                    total += renown;
                    parts?.Add(new Part(standing[i].title, renown));
                }
            }
        }

        int clearing = Mathf.RoundToInt(Overgrowth.ClearedShare * settings.ClearingRenown);
        if (Overgrowth.All.Count > 0)
        {
            total += clearing;
            parts?.Add(new Part("Clearing the overgrowth", clearing));
        }

        // The Haven is known by its people, its keeper among them.
        LevelingSettings leveling = LevelingSettings.Current;
        int totalLevel = GameSession.Progress.TotalLevel(leveling);
        int skilled = leveling.RenownFor(totalLevel);
        if (skilled > 0)
        {
            total += skilled;
            parts?.Add(new Part($"Your skills (total level {totalLevel})", skilled));
        }

        int orders = GameSession.Here.OrderRenown;
        if (orders > 0)
        {
            total += orders;
            parts?.Add(new Part("Good work for its people", orders));
        }

        int lands = Territory.Renown;
        if (lands > 0)
        {
            total += lands;
            parts?.Add(new Part($"The Haven's lands ({GameSession.Here.Territory.ClaimedCount} held)", lands));
        }

        // The best home, once, and each kind of yard, once: a town laid out with care is talked of.
        HomeLifeSettings.Level home = HomeLife.Best(out BuildingObject bestHouse);
        if (home != null && home.renown > 0)
        {
            total += home.renown;
            parts?.Add(new Part($"A {home.name.ToLowerInvariant()} home ({HavenHomes.Describe(bestHouse)})", home.renown));
        }

        int yardRenown = Yards.Renown;
        if (yardRenown > 0)
        {
            total += yardRenown;
            parts?.Add(new Part("Its yards", yardRenown));
        }

        int residents = GameSession.Here.Residents.Count * settings.ResidentRenown;
        if (residents > 0)
        {
            total += residents;
            parts?.Add(new Part($"Residents ({GameSession.Here.Residents.Count})", residents));
        }

        return total;
    }
}
