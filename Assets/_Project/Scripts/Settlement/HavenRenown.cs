using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// How much Safe Haven is worth, added up from what stands in it (HavenSettings has the numbers): every finished
/// building's renown plus its upgrades (another of a kind already standing brings only part of it, so variety pays),
/// the share of overgrowth cleared, and the residents living there. Pure reading of the loaded scene and
/// GameSession.Settlement; the parts and ideas for what to do next are shown on the Heart Tree's Haven page (Page).
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
            parts?.Add(new Part($"Clearing the overgrowth ({Overgrowth.ClearedShare * 100f:0}%)", clearing));
        }

        int residents = GameSession.Settlement.Residents.Count * settings.ResidentRenown;
        if (residents > 0)
        {
            total += residents;
            parts?.Add(new Part($"Residents ({GameSession.Settlement.Residents.Count})", residents));
        }

        return total;
    }

    /// <summary>The overview for the Heart Tree's menu: rank and the next, what renown is made of, what to do next.</summary>
    public static MenuPage Page(GameObject user)
    {
        HavenSettings settings = HavenSettings.Current;
        var parts = new List<Part>();
        int renown = Compute(parts);
        int rank = Mathf.Max(GameSession.Settlement.HavenRank, settings.RankFor(renown));
        var body = new StringBuilder();
        if (settings.Ranks.Count > 0)
        {
            HavenSettings.Rank now = settings.Ranks[rank];
            body.Append($"<color=#E8C46A><b>{now.name}</b></color>  ·  {renown} renown\n<alpha=#AA>{now.line}<alpha=#FF>\n");
            if (rank + 1 < settings.Ranks.Count)
            {
                HavenSettings.Rank next = settings.Ranks[rank + 1];
                body.Append($"Next: <b>{next.name}</b> at {next.renown} renown" +
                            (next.wardBonus > now.wardBonus ? $": the ward reaches {next.wardBonus - now.wardBonus:0} m further." : ".") + "\n");
            }
        }

        body.Append($"\n<color=#E8C46A>Renown</color>  <size=80%><alpha=#99>(a second of a kind counts {settings.RepeatShare * 100f:0}%: variety is worth more)<alpha=#FF></size>\n");
        foreach (Part part in parts.OrderByDescending(p => p.Renown).Take(8))
        {
            body.Append($"+{part.Renown}  {part.Label}\n");
        }

        body.Append("\n<color=#E8C46A>What next?</color>\n");
        int left = Overgrowth.All.Count(o => !o.IsCleared);
        if (left > 0)
        {
            body.Append($"• Clear the overgrowth: {left} left. Big stumps and boulders need better tools.\n");
        }

        int ideas = 0;
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (ideas >= 3 || !building.TryGetComponent(out Construction construction))
            {
                continue;
            }

            if (construction.IsBuilding && !construction.Paid)
            {
                construction.Cost(out IReadOnlyList<ResourceAmount> _, out IReadOnlyList<ItemAmount> items);
                body.Append($"• Repair the ruined {building.Definition.DisplayName}: hold E at it ({AmountText.Describe(items)}).\n");
                ideas++;
            }
            else if (construction.IsBuilding)
            {
                body.Append($"• Finish the {building.Definition.DisplayName}: hold E at its scaffold.\n");
                ideas++;
            }
            else if (construction.CanUpgrade)
            {
                BuildingLevel next = building.Definition.UpgradeTo(construction.Level + 1);
                body.Append($"• Upgrade the {construction.Title} (+{next.renown}): {AmountText.Describe(next.cost)} (G › Upgrade)\n");
                ideas++;
            }
        }

        PlayerCrafting crafting = user != null ? user.GetComponent<PlayerCrafting>() : null;
        if (crafting != null)
        {
            var standing = new HashSet<BuildingDefinition>(BuildingObject.Active.Select(b => b.Definition));
            string[] missing = crafting.Buildings.Where(b => b != null && b.ShowInBuildMenu && crafting.IsKnown(b) && !standing.Contains(b) && b.Renown > 0)
                .Select(b => $"{b.DisplayName} (+{b.Renown})").Take(5).ToArray();
            if (missing.Length > 0)
            {
                body.Append($"• Build something new (G): {string.Join(", ", missing)}\n");
            }
        }

        return new MenuPage { Title = "Safe Haven", Body = body.ToString() };
    }
}
