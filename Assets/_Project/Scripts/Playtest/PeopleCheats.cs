using UnityEngine;

/// <summary>
/// The F1 menu's People tab (Docs/PeopleSystemReview_2026-11-23.md §2.9): reach the people's states a playtest must see quickly (inspired,
/// a pick owed, a mark, friends and rivals, a rank) without weeks of play. Each is a cheat: the run's log marks it (Cheats.Used).
/// Only one's own Haven, as the people are its.
/// </summary>
public static class PeopleCheats
{
    private const string NotHome = "Only in your own Haven.";

    private static SettlementState Home => Place.InOwnHaven ? GameSession.Home : null;

    public static string InspireEveryone()
    {
        SettlementState home = Home;
        if (home == null)
        {
            return NotHome;
        }

        foreach (ResidentRecord record in home.Residents)
        {
            record.Inspiration = Inspirations.Choose(record, Random.value);
            record.InspiredOn = home.DaysPassed;
        }

        return Cheats.Done("People: inspire", $"{home.Residents.Count} inspired.");
    }

    public static string MorningNow()
    {
        SettlementState home = Home;
        if (home == null)
        {
            return NotHome;
        }

        PeopleDay.MorningNow(home);
        return Cheats.Done("People: morning", "A people's morning: friendships near work, Bright ones may wake inspired.");
    }

    public static string JobLevels(int levels)
    {
        SettlementState home = Home;
        if (home == null)
        {
            return NotHome;
        }

        LevelingSettings rules = LevelingSettings.Current;
        foreach (ResidentRecord record in home.Residents)
        {
            ProficiencyDefinition skill = ResidentTalents.JobSkill(record);
            if (skill != null)
            {
                int to = Mathf.Min(rules.MaxLevel, ResidentTalents.LevelOf(record, skill) + levels);
                record.Talents.SetExperience(skill.Id, rules.TotalExperienceFor(to));
            }
        }

        return Cheats.Done("People: levels", $"+{levels} levels in everyone's job skill.");
    }

    public static string PickEach()
    {
        SettlementState home = Home;
        if (home == null)
        {
            return NotHome;
        }

        foreach (ResidentRecord record in home.Residents)
        {
            record.Talents.SetPicksOwed(record.Talents.PicksOwed + 1);
        }

        return Cheats.Done("People: picks", "Each has a passion to grow: talk to them, or their window's Talents.");
    }

    public static string BondEach(float amount)
    {
        SettlementState home = Home;
        if (home == null)
        {
            return NotHome;
        }

        foreach (ResidentRecord record in home.Residents)
        {
            PeopleBond.Grow(record, amount, "cheat");
        }

        return Cheats.Done("People: bond", $"Everyone's bond +{amount:0.#}.");
    }

    public static string MarkEach(string markId)
    {
        SettlementState home = Home;
        MarkDefinition mark = ResidentMarks.Find(markId);
        if (home == null || mark == null)
        {
            return home == null ? NotHome : $"No mark {markId}.";
        }

        foreach (ResidentRecord record in home.Residents)
        {
            ResidentMarks.Give(record, mark);
        }

        return Cheats.Done("People: mark", $"Everyone carries {mark.DisplayName}.");
    }

    public static string HealEveryMark()
    {
        SettlementState home = Home;
        if (home == null)
        {
            return NotHome;
        }

        int healed = 0;
        foreach (ResidentRecord record in home.Residents)
        {
            foreach (MarkDefinition mark in ResidentMarks.Of(record))
            {
                if (!mark.Good && record.RemoveMark(mark.Id))
                {
                    healed++;
                }
            }
        }

        return Cheats.Done("People: heal", $"{healed} hard marks gone.");
    }

    /// <summary>Every standing pushed to its end: friends (Friendly or better) become Friends, frictions (Cool or worse) Rivals.</summary>
    public static string Extremes()
    {
        SettlementState home = Home;
        if (home == null)
        {
            return NotHome;
        }

        int changed = 0;
        foreach (ResidentRecord a in home.Residents)
        {
            foreach (ResidentRecord b in home.Residents)
            {
                if (a == b || string.CompareOrdinal(a.Resident.Id, b.Resident.Id) > 0)
                {
                    continue;
                }

                int level = PeopleStandings.LevelOf(home, a, b);
                if (level != 0)
                {
                    home.SetStanding(a.Resident.Id, b.Resident.Id, level > 0 ? 2f : -2f);
                    changed++;
                }
            }
        }

        return Cheats.Done("People: standings", $"{changed} pairs are Friends or Rivals now.");
    }

    public static string SceneSoon()
    {
        if (Home == null || SmallScenes.Current == null)
        {
            return NotHome;
        }

        SmallScenes.Current.Soon();
        return Cheats.Done("People: scene", "A small scene starts as soon as two who may are free.");
    }
}
