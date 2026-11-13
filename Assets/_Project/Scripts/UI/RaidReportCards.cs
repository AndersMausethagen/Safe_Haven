/// <summary>
/// A raid's haul as pictures (KitHaul, Hearthline 3): on an extraction what came home (and what it is worth) and what was
/// used up; on a fall what was lost and what the safe pocket saved. The words under them are then
/// RaidReportText.Describe(report, true), which leaves those lists out. Shared by the results screen and the Haven's summary.
/// </summary>
public static class RaidReportCards
{
    private const float Gap = 14f;

    /// <summary>Shows the report's haul in <paramref name="first"/> and <paramref name="second"/>, the first at
    /// <paramref name="top"/>; returns the height they take together.</summary>
    public static float Show(RaidReport report, KitHaul first, KitHaul second, float top, int rows = 2)
    {
        UIStyle style = UIStyle.Current;
        bool gainedAny = report.ItemsGained.Count > 0 || report.ResourcesGained.Count > 0;
        bool lostAny = report.ItemsLost.Count > 0 || report.ResourcesLost.Count > 0;
        first.Top = top;
        float height;
        if (report.Extracted)
        {
            height = first.Show("Brought home" + Worth(report.ValueGained, RaidReportText.Gain), "Bags", style.Good, "+",
                report.ItemsGained, report.ResourcesGained, "Nothing new this time.", rows);
            if (!lostAny)
            {
                second.Hide();
                return height;
            }

            second.Top = top + height + Gap;
            return height + Gap + second.Show("Used up", "Hourglass", style.Danger, "-", report.ItemsLost, report.ResourcesLost, null, 1);
        }

        // A lost cart is told above the cards: "nothing was lost" under it would be wrong.
        bool cartLost = !string.IsNullOrEmpty(report.TransportLine) && !report.TransportHome;
        if (lostAny || !cartLost)
        {
            height = first.Show("Lost" + Worth(report.ValueLost, RaidReportText.Loss), "Skull", style.Danger, "-",
                report.ItemsLost, report.ResourcesLost, "You carried nothing, so nothing was lost.", rows);
        }
        else
        {
            first.Hide();
            height = 0f;
        }

        if (!gainedAny)
        {
            second.Hide();
            return height;
        }

        float gap = height > 0f ? Gap : 0f;
        second.Top = top + height + gap;
        return height + gap + second.Show("Saved in your safe pocket", "Lock", style.Good, "+", report.ItemsGained, report.ResourcesGained, null, 1);
    }

    private static string Worth(int value, string colour) =>
        value > 0 ? $"   <size=80%><color={RaidReportText.Muted}>worth</color> <color={colour}>{value}</color></size>" : "";
}
