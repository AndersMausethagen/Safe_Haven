using System.Collections.Generic;
using System.Text;

/// <summary>
/// Turns a RaidReport into the words shown to the player: a title, a subtitle and a rich-text body listing what was brought
/// home or lost. Shared by the full-screen results (RaidResultsScreen) and the in-game summary window.
/// </summary>
public static class RaidReportText
{
    public static string Gain => UIStyle.Current.GoodHex;
    public static string Loss => UIStyle.Current.DangerHex;
    public static string Muted => UIStyle.Current.MutedHex;

    public static string Title(RaidReport report)
    {
        return report.Extracted ? "Extracted!" : report.Outcome == RaidOutcome.Died ? "You Died" : "Raid Collapsed";
    }

    public static string Subtitle(RaidReport report)
    {
        return $"{report.RaidName}  ·  {RaidStats.FormatTime(report.TimeSpent)} in the raid";
    }

    public static string Describe(RaidReport report) => Describe(report, false);

    /// <param name="listsShownApart">The haul is shown as pictures beside the words (RaidReportCards): what came home,
    /// was used up, lost or saved, and what it was worth, are left out.</param>
    public static string Describe(RaidReport report, bool listsShownApart)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrEmpty(report.BroughtDownBy))
        {
            builder.Append($"<b><color={Loss}>Brought down by a {report.BroughtDownBy}.</color></b>\n");
            // How to face it, from its journal (research G3): a death that teaches.
            if (!string.IsNullOrEmpty(report.KillerAdvice))
            {
                builder.Append($"<i>{report.KillerAdvice}</i>\n");
            }

            builder.Append('\n');
        }

        if (report.Companion != null)
        {
            builder.Append(report.CompanionLost
                ? $"<b><color={Loss}>{report.Companion.DisplayName} did not come home.</color></b>\n<i>\"{report.Companion.FallenLine}\"</i>\n{SettlementSettings.Current.LostReportLine}\n\n"
                : $"<b><color={Gain}>{report.Companion.DisplayName} made it home{(report.CompanionNewRank != null ? $", and is {report.CompanionNewRank} now" : "")}.</color></b>\n\n");
        }

        if (report.FoundAgain != null)
        {
            builder.Append($"<b><color={Gain}>{string.Format(SettlementSettings.Current.FoundReportLine, report.FoundAgain.DisplayName)}</color></b>\n\n");
        }

        if (report.LeftBehind != null)
        {
            builder.Append($"<b><color={Loss}>{string.Format(SettlementSettings.Current.LeftBehindLine, report.LeftBehind.DisplayName)}</color></b>\n\n");
        }

        foreach (ResidentDefinition rescued in report.Rescued)
        {
            builder.Append($"<b><color={Gain}>{rescued.DisplayName} came home with you.</color></b>\n<i>\"{rescued.ArrivalLine}\"</i>\n");
            string plans = PlansOf(rescued);
            builder.Append(plans.Length > 0 ? $"<color={Muted}>{rescued.DisplayName} knows how to build: {plans}.</color>\n\n" : "\n");
        }

        if (!string.IsNullOrEmpty(report.TransportLine))
        {
            builder.Append($"<b><color={(report.TransportHome ? Gain : Loss)}>{report.TransportLine}</color></b>\n\n");
        }

        if (listsShownApart)
        {
            return WithoutLists(builder, report);
        }

        bool gainedAny = report.ItemsGained.Count > 0 || report.ResourcesGained.Count > 0;
        bool lostAny = report.ItemsLost.Count > 0 || report.ResourcesLost.Count > 0;
        if (report.Extracted)
        {
            if (report.ValueGained > 0)
            {
                builder.Append($"<size=22><b>Haul value: <color={Gain}>{report.ValueGained}</color></b></size>\n\n");
            }

            builder.Append("<b>Brought home</b>\n");
            builder.Append(gainedAny ? List(report.ItemsGained, report.ResourcesGained, "+", Gain) : $"<color={Muted}>Nothing new this time.</color>");
            if (lostAny)
            {
                builder.Append("\n\n<b>Used up</b>\n").Append(List(report.ItemsLost, report.ResourcesLost, "-", Loss));
            }

            string problems = Problems(report);
            if (problems.Length > 0)
            {
                builder.Append("\n\n").Append(problems);
            }
        }
        else
        {
            if (report.ValueLost > 0)
            {
                builder.Append($"<size=22><b>Value lost: <color={Loss}>{report.ValueLost}</color></b></size>\n\n");
            }

            // A lost cart was just reported above: "nothing was lost" right under it would be wrong.
            bool cartLost = !string.IsNullOrEmpty(report.TransportLine) && !report.TransportHome;
            if (lostAny)
            {
                builder.Append("<b>Lost</b>\n").Append(List(report.ItemsLost, report.ResourcesLost, "-", Loss));
            }
            else if (!cartLost)
            {
                builder.Append($"<b>Lost</b>\n<color={Muted}>You carried nothing, so nothing was lost.</color>");
            }

            // Shown apart from the losses (GDD §8.3): this gear came home, only worn.
            if (report.EquipmentDamaged.Count > 0)
            {
                builder.Append("\n\n<b>Gear kept, but damaged</b>");
                foreach (EquipmentWear wear in report.EquipmentDamaged)
                {
                    string state = wear.After <= 0.0001f ? $"<color={Loss}>broken</color>" : $"{wear.After:P0}";
                    builder.Append($"\n{Rarity.Rich(wear.Item.DisplayName, wear.Item.Rarity)}  {wear.Before:P0} → {state}");
                }

                builder.Append($"\n<color={Muted}>{RepairAdvice(report)}</color>");
            }

            if (gainedAny)
            {
                builder.Append("\n\n<b>Saved in your safe pocket</b>\n").Append(List(report.ItemsGained, report.ResourcesGained, "+", Gain));
            }

            builder.Append($"\n\n<color={Muted}>You woke at the portal. Your safe pocket, recipes and discoveries are kept. Your storage is untouched.</color>");
        }

        // What came home and what held the player back come first; how the raid went follows, for whoever scrolls on.
        string details = Details(report);
        if (details.Length > 0)
        {
            builder.Append("\n\n").Append(details);
        }

        return builder.ToString().TrimEnd();
    }

    // The rest of the report when its haul is shown as pictures: what held the player back, the gear worn on a fall, and
    // how the raid went.
    private static string WithoutLists(StringBuilder builder, RaidReport report)
    {
        if (report.Extracted)
        {
            Paragraph(builder, Problems(report));
        }
        else
        {
            if (report.EquipmentDamaged.Count > 0)
            {
                var gear = new StringBuilder("<b>Gear kept, but damaged</b>");
                foreach (EquipmentWear wear in report.EquipmentDamaged)
                {
                    string state = wear.After <= 0.0001f ? $"<color={Loss}>broken</color>" : $"{wear.After:P0}";
                    gear.Append($"\n{Rarity.Rich(wear.Item.DisplayName, wear.Item.Rarity)}  {wear.Before:P0} → {state}");
                }

                gear.Append($"\n<color={Muted}>{RepairAdvice(report)}</color>");
                Paragraph(builder, gear.ToString());
            }

            Paragraph(builder, $"<color={Muted}>You woke at the portal. Your safe pocket, recipes and discoveries are kept. Your storage is untouched.</color>");
        }

        Paragraph(builder, Details(report));
        return builder.ToString().Trim();
    }

    // Adds a paragraph a blank line under what is there.
    private static void Paragraph(StringBuilder builder, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        int trailing = 0;
        while (trailing < builder.Length && builder[builder.Length - 1 - trailing] == '\n')
        {
            trailing++;
        }

        if (builder.Length > 0)
        {
            builder.Append('\n', System.Math.Max(0, 2 - trailing));
        }

        builder.Append(text);
    }

    // Lessons, the way there, how the gathering went, and the modifiers and sparks spent.
    private static string Details(RaidReport report)
    {
        var builder = new StringBuilder();
        string lessons = Lessons(report);
        if (lessons != null)
        {
            builder.Append(lessons).Append("\n\n");
        }

        if (report.Events != null && report.Events.Count > 0)
        {
            builder.Append("<b>On the way</b>\n");
            foreach (string happened in report.Events)
            {
                builder.Append($"<color={Muted}>{happened}</color>\n");
            }

            builder.Append('\n');
        }

        string gathering = Gathering(report.Gathering);
        if (gathering != null)
        {
            builder.Append(gathering).Append("\n\n");
        }

        if (report.Modifiers.Count > 0)
        {
            var names = new List<string>();
            foreach (RaidModifierDefinition modifier in report.Modifiers)
            {
                names.Add(modifier.DisplayName);
            }

            builder.Append($"<color={Muted}>Modifiers used: {string.Join(", ", names)}</color>\n\n");
        }

        if (report.SparksUsed > 0)
        {
            builder.Append($"<color={Muted}>Portal Sparks used: {report.SparksUsed}</color>\n\n");
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// What held the player back on an extraction, each with who at home answers it (HavenAnswers): the load left behind, finds
    /// nobody can use yet, and gear worn on the way. Empty when nothing did.
    /// </summary>
    public static string Problems(RaidReport report)
    {
        var text = new StringBuilder();
        if (report.ItemsLeftBehind.Count > 0 || report.ResourcesLeftBehind.Count > 0)
        {
            float weight = report.WeightLeftBehind;
            text.Append($"<b>Left behind</b>  <color={Muted}>no room to carry it{(weight >= 0.5f ? $", {weight:0} kg" : "")}</color>\n");
            text.Append(List(report.ItemsLeftBehind, report.ResourcesLeftBehind, "-", Loss));
            string carrier = CarrierAdvice(report.Carrier);
            if (carrier.Length > 0)
            {
                text.Append($"\n<color={Muted}>{carrier}</color>");
            }
        }

        if (report.NoUseYet.Count > 0)
        {
            text.Append(text.Length > 0 ? "\n\n" : "").Append("<b>Nobody at home can use these yet</b>");
            foreach (UnusedFind find in report.NoUseYet)
            {
                var ways = new List<string>();
                foreach (HavenAnswer answer in find.Answers)
                {
                    string how = answer.HowTo();
                    if (how.Length > 0)
                    {
                        ways.Add(how);
                    }
                }

                string name = find.What is ItemDefinition item ? Rarity.RichName(item) : find.Name;
                text.Append($"\n{find.Amount} {name}  <color={Muted}>{(ways.Count > 0 ? string.Join(" Or: ", ways) : "Nobody knows its use yet.")}</color>");
            }
        }

        if (report.EquipmentDamaged.Count > 0)
        {
            text.Append(text.Length > 0 ? "\n\n" : "").Append("<b>Worn on the way</b>");
            foreach (EquipmentWear wear in report.EquipmentDamaged)
            {
                string state = wear.After <= 0.0001f ? $"<color={Loss}>broken</color>" : $"{wear.After:P0}";
                text.Append($"\n{Rarity.Rich(wear.Item.DisplayName, wear.Item.Rarity)}  {wear.Before:P0} → {state}");
            }

            text.Append($"\n<color={Muted}>{RepairAdvice(report)}</color>");
        }

        return text.ToString();
    }

    // "Repair it at the Workshop.", and who would do it for less while nobody at home does.
    private static string RepairAdvice(RaidReport report)
    {
        DurabilitySettings settings = DurabilitySettings.Current;
        string advice = $"Repair it at the {(settings != null ? settings.RepairStation.DisplayName() : "Workshop")}.";
        HavenAnswer mender = report.Mender;
        if (mender != null && mender.Person != null && mender.Job != null)
        {
            advice += $" A {mender.Job.DisplayName.ToLowerInvariant()} would do it for {mender.Job.GearRepairDiscount:P0} less. {mender.HowTo()}";
        }

        return advice;
    }

    // "A Wheelbarrow would have carried it. Bring Tor home: the Carpenter knows how."
    private static string CarrierAdvice(HavenAnswer carrier)
    {
        if (carrier == null || carrier.Carrier == null)
        {
            return "";
        }

        string how = carrier.HowTo();
        if (how.Length > 0)
        {
            return $"A {carrier.Carrier.DisplayName} would have carried it. {how}";
        }

        string where = carrier.Building != null ? $" from the {carrier.Building.DisplayName}" : "";
        return $"A {carrier.Carrier.DisplayName}{where} would have carried it.";
    }

    // The workplaces a person shows the player how to build: what every job they can do teaches.
    private static string PlansOf(ResidentDefinition resident)
    {
        var names = new List<string>();
        foreach (JobDefinition job in resident.Jobs)
        {
            if (job == null)
            {
                continue;
            }

            foreach (BuildingDefinition building in job.TeachesBuildings)
            {
                if (building != null && !names.Contains(building.DisplayName))
                {
                    names.Add(building.DisplayName);
                }
            }
        }

        return string.Join(", ", names);
    }

    /// <summary>The gathering line: crit rate, glancing blows and how angry the forest got. Null when nothing was gathered.</summary>
    /// <summary>"What you learned": each skill's levels and experience, and the raid bonus brought home or lost. Null when none.</summary>
    public static string Lessons(RaidReport report)
    {
        if (report.Lessons.Count == 0)
        {
            return null;
        }

        var text = new StringBuilder("<b>What you learned</b>");
        int bonus = 0;
        bool kept = false;
        foreach (SkillLesson lesson in report.Lessons)
        {
            string levels = lesson.LevelAfter > lesson.LevelBefore
                ? $"<color={Gain}>{lesson.LevelBefore} → {lesson.LevelAfter}</color>"
                : lesson.LevelAfter.ToString();
            int total = lesson.Experience + (lesson.BonusKept ? lesson.Bonus : 0);
            text.Append($"\n{lesson.Skill.DisplayName} {levels}  <color={Muted}>+{total}</color>");
            bonus += lesson.Bonus;
            kept |= lesson.BonusKept;
        }

        if (bonus > 0)
        {
            text.Append(kept
                ? $"\n<color={Gain}>Raid bonus brought home: +{bonus}</color>"
                : $"\n<color={Loss}>Raid bonus lost: {bonus}. It comes home only with you.</color>");
        }

        return text.ToString();
    }

    public static string Gathering(GatheringStats stats)
    {
        if (stats == null || stats.Swings == 0)
        {
            return null;
        }

        var text = new StringBuilder($"<b>Gathering</b>  {stats.Swings} swings  ·  <color={Gain}>{stats.CriticalRate:P0} critical</color>");
        if (stats.Glancing > 0)
        {
            text.Append($"  ·  <color={Loss}>{stats.Glancing} glancing</color>");
        }

        if (stats.HasAnnoyance)
        {
            string color = stats.Wraths > 0 ? Loss : stats.PeakAnnoyance < 0.3f ? Gain : Muted;
            text.Append($"\n<color={color}>Forest annoyance peaked at {stats.PeakAnnoyance:P0}");
            text.Append(stats.Wraths > 0 ? $" – its wrath was unleashed {(stats.Wraths == 1 ? "once" : stats.Wraths + " times")}.</color>"
                : stats.PeakAnnoyance < 0.3f ? " – you kept quiet.</color>" : ".</color>");
        }

        return text.ToString();
    }

    // Items are named in their rarity's color, so a rare find stands out; the sign carries the gain or loss color.
    private static string List(IReadOnlyList<ItemAmount> items, IReadOnlyList<ResourceAmount> resources, string sign, string color)
    {
        var entries = new List<string>();
        foreach (ItemAmount item in items)
        {
            entries.Add($"<color={color}>{sign}{item.Amount}</color> {Rarity.RichName(item.Item)}");
        }

        foreach (ResourceAmount resource in resources)
        {
            entries.Add($"<color={color}>{sign}{resource.Amount} {resource.Resource.DisplayName}</color>");
        }

        return string.Join(",  ", entries);
    }
}
