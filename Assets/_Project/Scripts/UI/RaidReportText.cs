using System.Collections.Generic;
using System.Text;

/// <summary>
/// Turns a RaidReport into the words shown to the player: a title, a subtitle and a rich-text body listing what was brought
/// home or lost. Shared by the full-screen results (RaidResultsScreen) and the in-game summary window.
/// </summary>
public static class RaidReportText
{
    public const string Gain = "#8BE08B";
    public const string Loss = "#FF7B6B";
    public const string Muted = "#AAB0BC";

    public static string Title(RaidReport report)
    {
        return report.Extracted ? "Extracted!" : report.Outcome == RaidOutcome.Died ? "You Died" : "Raid Collapsed";
    }

    public static string Subtitle(RaidReport report)
    {
        return $"{report.RaidName}  ·  {RaidStats.FormatTime(report.TimeSpent)} in the raid";
    }

    public static string Describe(RaidReport report)
    {
        var builder = new StringBuilder();
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

        string gathering = Gathering(report.Gathering);
        if (gathering != null)
        {
            builder.Append(gathering).Append("\n\n");
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

        foreach (ResidentDefinition rescued in report.Rescued)
        {
            builder.Append($"<b><color={Gain}>{rescued.DisplayName} came home with you.</color></b>\n<i>\"{rescued.ArrivalLine}\"</i>\n\n");
        }

        if (!string.IsNullOrEmpty(report.TransportLine))
        {
            builder.Append($"<b><color={(report.TransportHome ? Gain : Loss)}>{report.TransportLine}</color></b>\n\n");
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

                builder.Append($"\n<color={Muted}>Repair it at a Workshop.</color>");
            }

            if (gainedAny)
            {
                builder.Append("\n\n<b>Saved in your safe pocket</b>\n").Append(List(report.ItemsGained, report.ResourcesGained, "+", Gain));
            }

            builder.Append($"\n\n<color={Muted}>You woke at the portal. Your safe pocket, recipes and discoveries are kept. Your storage is untouched.</color>");
        }

        return builder.ToString();
    }

    /// <summary>The gathering line: crit rate, glancing blows and how angry the forest got. Null when nothing was gathered.</summary>
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
