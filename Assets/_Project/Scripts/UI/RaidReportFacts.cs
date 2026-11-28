using System.Collections.Generic;
using UnityEngine;

/// <summary>How a fact on the results screen reads: good news, bad news, or plain.</summary>
public enum FactTone
{
    Plain = 0,
    Good = 1,
    Bad = 2,
}

/// <summary>One line of the results screen's right-hand column: a picture or kit icon, a short title, at most a short line under it.</summary>
public struct ReportFact
{
    public string Group;
    public string Icon;
    public Sprite Picture;
    public string Title;
    public string Line;
    public FactTone Tone;
}

/// <summary>
/// The raid report as short facts for the results screen's right-hand column (the user, 2026-11-26: "veldig mye tekst i høyre del av
/// skjermen hvor man må scrolle ... del opp tekst med ikoner"): what happened to the people, what to see to at home, and how the raid
/// went, each a picture and a few words. The haul and what was learned are not here: they are pictures in the left-hand column
/// (RaidReportCards). Plain C#, for tests; the words are RaidReportText's.
/// </summary>
public static class RaidReportFacts
{
    public const string People = "What happened";
    public const string AtHome = "To see to at home";
    public const string OnTheWay = "On the way";

    // At most this many worn things are listed one by one; the rest are counted.
    private const int MostWorn = 3;

    /// <summary>The facts in reading order, grouped. <paramref name="eventPicture"/> finds an event's painting from its line ("The Troll
    /// Wedding: Watch unseen"), or null.</summary>
    // The face of what brought the player down, by the name the report keeps (a creature's; a named one, a Lost One, has none here).
    private static Sprite PortraitOf(string broughtDownBy)
    {
        foreach (MonsterDefinition creature in Resources.FindObjectsOfTypeAll<MonsterDefinition>())
        {
            if (creature != null && creature.Portrait != null && string.Equals(creature.DisplayName, broughtDownBy, System.StringComparison.OrdinalIgnoreCase))
            {
                return creature.Portrait;
            }
        }

        return null;
    }

    public static List<ReportFact> For(RaidReport report, System.Func<string, Sprite> eventPicture = null)
    {
        var facts = new List<ReportFact>();
        if (report == null)
        {
            return facts;
        }

        // ----- The people, and the fall -----
        if (!string.IsNullOrEmpty(report.BroughtDownBy))
        {
            string line = !string.IsNullOrEmpty(report.KillerAdvice) ? report.KillerAdvice
                : report.LastBlows != null && report.LastBlows.Count > 0 ? "The last blows: " + string.Join("; ", report.LastBlows) : "";
            facts.Add(new ReportFact { Group = People, Icon = "Skull", Picture = PortraitOf(report.BroughtDownBy),
                Title = $"Brought down by {CreatureRecords.ByWhom(report.BroughtDownBy)}", Line = line, Tone = FactTone.Bad });
        }

        if (!report.Extracted)
        {
            facts.Add(new ReportFact { Group = People, Icon = "Lock", Title = "You woke at the portal", Line = "Safe pocket, recipes, discoveries and storage are kept.", Tone = FactTone.Plain });
        }

        if (report.Companion != null)
        {
            facts.Add(report.CompanionLost
                ? new ReportFact { Group = People, Icon = "Bond", Picture = report.Companion.Portrait, Title = $"{report.Companion.DisplayName} did not come home", Line = $"\"{report.Companion.FallenLine}\"", Tone = FactTone.Bad }
                : new ReportFact
                {
                    Group = People, Icon = "Bond", Picture = report.Companion.Portrait,
                    Title = $"{report.Companion.DisplayName} made it home" + (report.CompanionNewRank != null ? $", {report.CompanionNewRank} now" : ""),
                    Line = report.CompanionShownAsCards ? "" : CompanionWords(report), Tone = FactTone.Good,
                });
        }

        if (report.FoundAgain != null)
        {
            facts.Add(new ReportFact { Group = People, Icon = "Bond", Picture = report.FoundAgain.Portrait, Title = string.Format(SettlementSettings.Current.FoundReportLine, report.FoundAgain.DisplayName), Tone = FactTone.Good });
        }

        if (report.LeftBehind != null)
        {
            facts.Add(new ReportFact { Group = People, Icon = "Bond", Picture = report.LeftBehind.Portrait, Title = string.Format(SettlementSettings.Current.LeftBehindLine, report.LeftBehind.DisplayName), Tone = FactTone.Bad });
        }

        foreach (ResidentDefinition rescued in report.Rescued)
        {
            facts.Add(new ReportFact
            {
                Group = People, Icon = "House", Picture = rescued.Portrait, Title = $"{rescued.DisplayName} came home with you",
                Line = ResidentQuests.RescueToHandIn(rescued) != null ? $"Speak to {rescued.DisplayName} at home." : $"\"{rescued.ArrivalLine}\"", Tone = FactTone.Good,
            });
        }

        if (!string.IsNullOrEmpty(report.TransportLine))
        {
            facts.Add(new ReportFact { Group = People, Icon = "Wheel", Title = report.TransportLine, Tone = report.TransportHome ? FactTone.Good : FactTone.Bad });
        }

        // ----- What to see to at home -----
        int leftBehind = Count(report.ItemsLeftBehind) + Count(report.ResourcesLeftBehind);
        if (leftBehind > 0)
        {
            facts.Add(new ReportFact { Group = AtHome, Icon = "Bags", Title = $"Left behind: {leftBehind} you couldn't carry", Line = CarrierWords(report.Carrier), Tone = FactTone.Bad });
        }

        foreach (UnusedFind find in report.NoUseYet)
        {
            string how = "";
            foreach (HavenAnswer answer in find.Answers)
            {
                how = answer.HowTo();
                if (how.Length > 0)
                {
                    break;
                }
            }

            facts.Add(new ReportFact
            {
                Group = AtHome, Icon = "Book", Picture = (find.What as ItemDefinition)?.Icon, Title = $"{find.Amount} {(find.What is ItemDefinition item ? item.DisplayName : find.Name)}: no use yet",
                Line = how.Length > 0 ? how : "Nobody at home knows its use yet.", Tone = FactTone.Plain,
            });
        }

        int worn = 0;
        foreach (EquipmentWear wear in report.EquipmentDamaged)
        {
            if (worn++ >= MostWorn)
            {
                break;
            }

            string state = wear.After <= 0.0001f ? "broken" : $"{wear.After:P0}";
            facts.Add(new ReportFact { Group = AtHome, Icon = "Hammer", Picture = wear.Item.Icon, Title = $"{wear.Item.DisplayName}: {wear.Before:P0} → {state}", Tone = wear.After <= 0.0001f ? FactTone.Bad : FactTone.Plain });
        }

        if (report.EquipmentDamaged.Count > MostWorn)
        {
            facts.Add(new ReportFact { Group = AtHome, Icon = "Hammer", Title = $"{report.EquipmentDamaged.Count - MostWorn} more worn", Tone = FactTone.Plain });
        }

        // ----- How the raid went -----
        if (report.Events != null)
        {
            foreach (string happened in report.Events)
            {
                facts.Add(new ReportFact { Group = OnTheWay, Icon = "Path", Picture = eventPicture?.Invoke(happened), Title = happened, Tone = FactTone.Plain });
            }
        }

        GatheringStats gathering = report.Gathering;
        if (gathering != null && gathering.Swings > 0)
        {
            string line = !gathering.HasAnnoyance ? ""
                : gathering.Wraths > 0 ? $"The forest's wrath came {(gathering.Wraths == 1 ? "once" : gathering.Wraths + " times")}."
                : gathering.PeakAnnoyance < 0.3f ? "You kept the forest quiet." : $"The forest's anger peaked at {gathering.PeakAnnoyance:P0}.";
            facts.Add(new ReportFact
            {
                Group = OnTheWay, Icon = "Axe", Title = $"{gathering.Swings} swings, {gathering.CriticalRate:P0} critical" + (gathering.Glancing > 0 ? $", {gathering.Glancing} glancing" : ""),
                Line = line, Tone = gathering.Wraths > 0 ? FactTone.Bad : FactTone.Plain,
            });
        }

        if (report.Modifiers.Count > 0 || report.SparksUsed > 0)
        {
            var names = new List<string>();
            foreach (RaidModifierDefinition modifier in report.Modifiers)
            {
                names.Add(modifier.DisplayName);
            }

            if (report.SparksUsed > 0)
            {
                names.Add($"{report.SparksUsed} Portal Spark{(report.SparksUsed == 1 ? "" : "s")}");
            }

            facts.Add(new ReportFact { Group = OnTheWay, Icon = "Spark", Title = string.Join(", ", names), Tone = FactTone.Plain });
        }

        return facts;
    }

    /// <summary>The painting of the event a report line tells of: the raid's own pool, by the event's name at the line's start.</summary>
    public static Sprite EventPicture(RaidDefinition raid, string line)
    {
        RaidEventSettings settings = raid != null ? RaidEventSettings.Of(raid) : null;
        if (settings == null || string.IsNullOrEmpty(line))
        {
            return null;
        }

        RaidEventDefinition best = null;
        foreach (RaidEventDefinition definition in settings.pool)
        {
            // The longest name that starts the line: "The Pedlar's Brother" before "The Pedlar".
            if (definition != null && line.StartsWith(definition.DisplayName, System.StringComparison.Ordinal)
                && (best == null || definition.DisplayName.Length > best.DisplayName.Length))
            {
                best = definition;
            }
        }

        return best != null ? best.Artwork : null;
    }

    private static int Count(IReadOnlyList<ItemAmount> items)
    {
        int total = 0;
        foreach (ItemAmount item in items)
        {
            total += item.Amount;
        }

        return total;
    }

    private static int Count(IReadOnlyList<ResourceAmount> resources)
    {
        int total = 0;
        foreach (ResourceAmount resource in resources)
        {
            total += resource.Amount;
        }

        return total;
    }

    private static string CarrierWords(HavenAnswer carrier)
    {
        if (carrier == null || carrier.Carrier == null)
        {
            return "";
        }

        string how = carrier.HowTo();
        return how.Length > 0 ? $"A {carrier.Carrier.DisplayName} would have carried it. {how}" : $"A {carrier.Carrier.DisplayName} would have carried it.";
    }

    private static string CompanionWords(RaidReport report)
    {
        var parts = new List<string>();
        if (report.CompanionSkill != null && report.CompanionLevelAfter > report.CompanionLevelBefore)
        {
            parts.Add($"{report.CompanionSkill.DisplayName} {report.CompanionLevelBefore} → {report.CompanionLevelAfter}");
        }

        foreach (MarkDefinition mark in report.CompanionMarks)
        {
            parts.Add(mark.Good ? mark.DisplayName : $"carries {mark.DisplayName.ToLowerInvariant()}");
        }

        return string.Join(", ", parts);
    }
}
