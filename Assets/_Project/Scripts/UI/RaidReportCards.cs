using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A raid's haul as pictures (KitHaul, Hearthline 3): on an extraction what came home (and what it is worth) and what was
/// used up; on a fall what was lost and what the safe pocket saved. The words under them are then
/// RaidReportText.Describe(report, true), which leaves those lists out. Under them, what was learned, a card a skill (KitCardRow:
/// its icon, its name and levels, the experience in the corner), where there was a line a skill. Shared by the results screen and
/// the Haven's summary.
/// </summary>
public static class RaidReportCards
{
    private const float Gap = 14f;

    /// <summary>Shows the report's haul in <paramref name="first"/> and <paramref name="second"/>, the first at
    /// <paramref name="top"/>; returns the height they take together.</summary>
    public static float Show(RaidReport report, KitHaul first, KitHaul second, float top, int rows = 2) =>
        ShowHaul(report, first, second, top, rows);

    /// <summary>
    /// As Show, with what was learned as one row of cards in <paramref name="lessons"/> under the haul, when it fits within
    /// <paramref name="room"/> with the haul. <paramref name="lessonsShown"/> says whether it did: if not, the words keep the lessons
    /// (RaidReportText.Describe), so a long haul never squeezes the report out (the review of 2026-11-21).
    /// </summary>
    public static float Show(RaidReport report, KitHaul first, KitHaul second, KitCardRow lessons, float top, float room, out bool lessonsShown, int rows = 2)
    {
        float haul = ShowHaul(report, first, second, top, rows);
        lessonsShown = false;
        if (lessons == null)
        {
            return haul;
        }

        // Every lesson a card, or none: what a "more" card hid would show nowhere (the words leave the lessons out when they are cards).
        // The companion's cards (what the raid made of them) follow the player's lessons in the same row.
        List<KitCardFace> along = CompanionFaces(report);
        int count = report.Lessons.Count + along.Count;
        // The companion's cards give way first: the player's own lessons stay pictures where they fit alone.
        if (count > 0 && along.Count > 0 && (!lessons.Fits(count) || haul + Gap + lessons.HeightFor(count) > room))
        {
            along.Clear();
            count = report.Lessons.Count;
        }

        if (count == 0 || !lessons.Fits(count) || haul + Gap + lessons.HeightFor(count) > room)
        {
            lessons.Hide();
            return haul;
        }

        report.CompanionShownAsCards = along.Count > 0;

        var faces = new List<KitCardFace>();
        UIStyle style = UIStyle.Current;
        foreach (SkillLesson lesson in report.Lessons)
        {
            if (lesson.Skill == null)
            {
                continue;
            }

            bool rose = lesson.LevelAfter > lesson.LevelBefore;
            int total = lesson.Experience + (lesson.BonusKept ? lesson.Bonus : 0);
            faces.Add(new KitCardFace
            {
                Name = rose ? $"{lesson.Skill.DisplayName} {lesson.LevelBefore} → {lesson.LevelAfter}" : $"{lesson.Skill.DisplayName} {lesson.LevelAfter}",
                Picture = lesson.Skill.Icon, Icon = "Star", Badge = $"+{total}", Rim = rose ? style.Good : (Color?)null,
            });
        }

        faces.AddRange(along);
        float gap = haul > 0f && faces.Count > 0 ? Gap : 0f;
        lessons.Top = top + haul + gap;
        string heading = report.Lessons.Count > 0 ? RaidReportText.LessonsHeading(report) : $"{report.Companion.DisplayName}, beside you";
        float learned = lessons.Show(heading, "Star", style.Accent, faces);
        lessonsShown = learned > 0f;
        return haul + (learned > 0f ? gap + learned : 0f);
    }

    /// <summary>What the raid made of the one who went along, as cards: their fighting skill, each mark, their weapon's wear, a passion
    /// owed (Docs/PeopleSystemReview_2026-11-23.md §2.3). None when nobody came home with the player.</summary>
    public static List<KitCardFace> CompanionFaces(RaidReport report)
    {
        var faces = new List<KitCardFace>();
        if (report?.Companion == null || report.CompanionLost || report.CompanionSkill == null)
        {
            return faces;
        }

        UIStyle style = UIStyle.Current;
        string who = report.Companion.DisplayName;
        bool rose = report.CompanionLevelAfter > report.CompanionLevelBefore;
        faces.Add(new KitCardFace
        {
            Name = rose ? $"{who}: {report.CompanionSkill.DisplayName} {report.CompanionLevelBefore} → {report.CompanionLevelAfter}"
                : $"{who}: {report.CompanionSkill.DisplayName} {report.CompanionLevelAfter}",
            Picture = report.CompanionSkill.Icon, Icon = "Sword", Rim = rose ? style.Good : (Color?)null,
        });
        foreach (MarkDefinition mark in report.CompanionMarks)
        {
            faces.Add(new KitCardFace { Name = $"{who}: {mark.DisplayName}", Icon = mark.Icon, Rim = mark.Good ? style.Good : style.Warning });
        }

        if (!report.CompanionWeapon.IsEmpty)
        {
            faces.Add(new KitCardFace
            {
                Name = report.CompanionWeapon.DisplayName, Picture = report.CompanionWeapon.Item.Icon, Icon = "Sword",
                Badge = $"{Mathf.RoundToInt(report.CompanionWeapon.Durability * 100f)}%",
                Rim = report.CompanionWeapon.Durability < 0.25f ? style.Warning : (Color?)null,
            });
        }

        if (report.CompanionNewRank != null)
        {
            faces.Add(new KitCardFace { Name = $"{who} is {report.CompanionNewRank}: a passion to grow", Icon = ResidentTalents.PassionIcon, Rim = style.Good });
        }

        return faces;
    }

    private static float ShowHaul(RaidReport report, KitHaul first, KitHaul second, float top, int rows)
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

        // What was kept first, then what was lost (Darkest Dungeon, Hades: "you still have this" reads before the loss).
        float kept = 0f;
        if (gainedAny)
        {
            kept = first.Show("Saved in your safe pocket", "Lock", style.Good, "+", report.ItemsGained, report.ResourcesGained, null, 1) + Gap;
        }
        else
        {
            first.Hide();
        }

        // A lost cart is told with the facts: "nothing was lost" under it would be wrong.
        bool cartLost = !string.IsNullOrEmpty(report.TransportLine) && !report.TransportHome;
        if (!lostAny && cartLost)
        {
            second.Hide();
            return Mathf.Max(0f, kept - Gap);
        }

        second.Top = top + kept;
        return kept + second.Show("Lost" + Worth(report.ValueLost, RaidReportText.Loss), "Skull", style.Danger, "-",
            report.ItemsLost, report.ResourcesLost, "You carried nothing, so nothing was lost.", rows);
    }

    private static string Worth(int value, string colour) =>
        value > 0 ? $"   <size=80%><color={RaidReportText.Muted}>worth</color> <color={colour}>{value}</color></size>" : "";
}
