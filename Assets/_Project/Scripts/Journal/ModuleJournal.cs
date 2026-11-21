using System.Linq;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The journal's Modules page (the systems review, 2026-11-21: modules were found only in each building's Upgrade tab, one building at
/// a time; Dragonwilds and Valheim keep a list of what can be built, the unknown ones shown dark with a hint). Every module, by the
/// buildings it fits: known ones with their painting, what they do, their tier, cost and where to fit them, and how many stand
/// fitted at home; unknown ones as dark cards that say who teaches them (a quest or a building's tier), and nothing more.
/// </summary>
public static class ModuleJournal
{
    public static List<HavenShelf> Shelves(out string summary)
    {
        var known = new HavenShelf("Known");
        var unknown = new HavenShelf("Not learned yet");
        int fitted = 0;
        // Fitted is read from the Haven's buildings, once: away from home there are none to read, and the page doesn't guess.
        bool home = Place.AtHome;
        Dictionary<BuildingModuleDefinition, int> atHome = home ? FittedAtHome() : null;
        var modules = new List<BuildingModuleDefinition>(BuildingModules.All);
        modules.RemoveAll(module => module == null);
        // By tier, then name, so the page reads as a ladder.
        modules.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.DisplayName, b.DisplayName));
        foreach (BuildingModuleDefinition module in modules)
        {
            if (BuildingModules.IsKnown(module))
            {
                int standing = atHome != null && atHome.TryGetValue(module, out int count) ? count : 0;
                fitted += standing > 0 ? 1 : 0;
                known.Entries.Add(EntryOf(module, standing));
            }
            else
            {
                unknown.Entries.Add(new HavenEntry
                {
                    Key = module,
                    Name = JournalContent.UnknownName,
                    Line = $"Tier {Tiers.ToRoman(module.Tier)}, for {module.FitsText()}",
                    Icon = module.Icon,
                    Silhouette = true,
                    State = HavenEntryState.Locked,
                    Status = "Not learned yet",
                    Description = $"A module for {module.FitsText()}. {Teacher(module)}",
                });
            }
        }

        summary = $"{known.Entries.Count} of {known.Entries.Count + unknown.Entries.Count} known" + (home ? $", {fitted} fitted at home" : "");
        return new List<HavenShelf> { known, unknown };
    }

    private static HavenEntry EntryOf(BuildingModuleDefinition module, int standing)
    {
        var entry = new HavenEntry
        {
            Key = module,
            Name = module.DisplayName,
            Line = standing > 0 ? $"Fitted to {standing} building{(standing == 1 ? "" : "s")}" : $"Tier {Tiers.ToRoman(module.Tier)}, for {module.FitsText()}",
            Icon = module.Icon,
            State = standing > 0 ? HavenEntryState.Good : HavenEntryState.Ready,
            Status = module.Effect(),
            Description = module.Description,
        };
        entry.Details.Add(new HavenDetail("Fits", $"{Capital(module.FitsText())}, at Tier {Tiers.ToRoman(module.Tier)} or above. Fit it from {BuildingModules.WhereToFit(module)}."));
        string cost = AmountText.Describe(new List<ItemAmount>(module.Cost));
        if (module.Essence.Count > 0)
        {
            cost += (cost.Length > 0 ? ", " : "") + AmountText.Describe(new List<ResourceAmount>(module.Essence));
        }

        entry.Details.Add(new HavenDetail("Costs", cost.Length > 0 ? cost : "Nothing"));
        if (module.Staffed)
        {
            entry.Details.Add(new HavenDetail("Works", "Only while someone works the building."));
        }

        return entry;
    }

    /// <summary>Who teaches a module, in a sentence: a quest, a building's tier, or a find.</summary>
    public static string Teacher(BuildingModuleDefinition module)
    {
        ContentDatabase content = Resources.Load<ContentDatabase>(ContentDatabase.ResourceName);
        if (content != null)
        {
            foreach (QuestDefinition quest in content.Quests)
            {
                if (quest != null && quest.UnlocksModules.Contains(module))
                {
                    return $"Taught by the quest \"{quest.DisplayName}\".";
                }
            }

            foreach (BuildingDefinition building in content.Buildings)
            {
                for (int level = 2; building != null && level <= 10; level++)
                {
                    BuildingLevel tier = building.UpgradeTo(level);
                    if (tier != null && tier.unlocksModules.Contains(module))
                    {
                        return $"Learned by raising the {building.DisplayName} to Tier {Tiers.ToRoman(level)}.";
                    }
                }
            }
        }

        return "Found, not taught: one may turn up in a raid.";
    }

    // How many buildings at home have each module fitted now.
    private static Dictionary<BuildingModuleDefinition, int> FittedAtHome()
    {
        var counts = new Dictionary<BuildingModuleDefinition, int>();
        foreach (Construction construction in Object.FindObjectsByType<Construction>(FindObjectsInactive.Exclude))
        {
            foreach (BuildingModuleDefinition fitted in construction.Fitted.Distinct())
            {
                if (fitted != null)
                {
                    counts[fitted] = counts.TryGetValue(fitted, out int count) ? count + 1 : 1;
                }
            }
        }

        return counts;
    }

    private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
}
