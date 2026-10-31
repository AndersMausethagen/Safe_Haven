using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How much Safe Haven is worth, added up from what stands in it (HavenSettings has the numbers): every finished
/// building's renown plus its upgrades (another of a kind already standing brings only part of it, so variety pays),
/// the share of overgrowth cleared, and the residents living there. Pure reading of the loaded scene and
/// GameSession.Settlement; the parts are shown in the Haven window (HavenOverview).
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
        var seen = new Dictionary<BuildingDefinition, int>();
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

            seen.TryGetValue(definition, out int before);
            seen[definition] = before + 1;
            float worth = definition.Renown + (construction != null ? construction.Sum(level => level.renown) : 0f);
            int renown = Mathf.RoundToInt(before == 0 ? worth : worth * settings.RepeatShare);
            if (renown <= 0)
            {
                continue;
            }

            total += renown;
            parts?.Add(new Part(construction != null ? construction.Title : definition.DisplayName, renown));
        }

        int clearing = Mathf.RoundToInt(Overgrowth.ClearedShare * settings.ClearingRenown);
        if (Overgrowth.All.Count > 0)
        {
            total += clearing;
            parts?.Add(new Part("Clearing the overgrowth", clearing));
        }

        int residents = GameSession.Settlement.Residents.Count * settings.ResidentRenown;
        if (residents > 0)
        {
            total += residents;
            parts?.Add(new Part($"Residents ({GameSession.Settlement.Residents.Count})", residents));
        }

        return total;
    }
}
