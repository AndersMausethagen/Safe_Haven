using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tells the player when the journal has something new, finds torn pages in raids and writes each raid down. The journal's window only reads what this leaves
/// behind, so all of this goes on with the window closed. A page of how things work that opens becomes a tutorial (Tutorials): the first time a skill is
/// tried (Tried, from practice and from the systems that start one: a swing at a node, a cast) opens the pages that wait for it.
/// </summary>
public static class JournalNotices
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Listen()
    {
        GameEvents.Raised -= OnGameEvent;
        GameEvents.Raised += OnGameEvent;
        ExperienceTracker.Practised -= OnPractised;
        ExperienceTracker.Practised += OnPractised;
    }

    private static void OnPractised(ProficiencyDefinition skill, float amount) => Tried(skill);

    /// <summary>The player has just tried a skill: the first time, the pages that wait for it open.</summary>
    public static void Tried(ProficiencyDefinition skill)
    {
        if (skill != null && GameSession.Progress.Journal.NoteTried(skill.Id))
        {
            CheckPages();
        }
    }

    /// <summary>Tried for the skill a kind of practice trains (fishing, melee...).</summary>
    public static void Tried(ProficiencyTraining training) => Tried(LevelingSettings.Current.SkillTrainedBy(training));

    private static void OnGameEvent(GameEvent happened)
    {
        if (happened.Kind == GameEventKind.QuestStarted)
        {
            CheckPages();
        }
    }

    /// <summary>Announces every page that has opened since the last look, once: its name when there is one, a count when there are more.</summary>
    public static void CheckPages()
    {
        JournalSettings settings = JournalSettings.Current;
        QuestLog log = GameSession.Quests;
        CharacterProgress progress = GameSession.Progress;
        var fresh = new List<string>();
        Collect(settings.Backstory, log, progress, fresh);
        // How things work is told by the tutorial notice, not in words.
        foreach (JournalPage page in settings.Mechanics)
        {
            if (JournalContent.IsUnlocked(page, log, progress) && progress.Journal.Announce(page.title))
            {
                Tutorials.Add(page);
            }
        }

        if (fresh.Count == 1)
        {
            Post($"New in your journal: {fresh[0]} (J)");
        }
        else if (fresh.Count > 1)
        {
            Post($"{fresh.Count} new pages in your journal (J)");
        }
    }

    private static void Collect(IReadOnlyList<JournalPage> pages, QuestLog log, CharacterProgress progress, List<string> fresh)
    {
        foreach (JournalPage page in pages)
        {
            if (JournalContent.IsUnlocked(page, log, progress) && progress.Journal.Announce(page.title))
            {
                fresh.Add(page.title);
            }
        }
    }

    /// <summary>Tells of a creature met for the first time, or understood better after a kill.</summary>
    public static void CreatureWritten(MonsterDefinition creature, bool firstTime)
    {
        // "The Heartroot" has its article already.
        string name = creature.DisplayName.StartsWith("The ", System.StringComparison.Ordinal) ? creature.DisplayName : "the " + creature.DisplayName;
        Post(firstTime ? $"New in your journal: {creature.DisplayName} (J)" : $"You understand {name} better (J)");
        CheckPages();
    }

    /// <summary>A searched cache may hold a page nobody has found yet, in the settings' order.</summary>
    public static void RollForFind()
    {
        JournalSettings settings = JournalSettings.Current;
        if (Random.value > settings.FindChance)
        {
            return;
        }

        JournalRecord record = GameSession.Progress.Journal;
        foreach (JournalPage page in settings.Backstory)
        {
            if (page.foundInRaids && !record.HasFound(page.title))
            {
                record.NoteFound(page.title);
                record.Announce(page.title);
                Post($"Among the rest, something written: {page.title} (J)");
                return;
            }
        }
    }

    /// <summary>Writes a finished raid down, while its report still exists.</summary>
    public static void WriteDown(RaidReport report, string broughtDownBy)
    {
        var raid = new RaidRecord
        {
            Raid = report.RaidName,
            Outcome = RaidReportText.Title(report),
            Seconds = Mathf.RoundToInt(report.TimeSpent),
            Value = report.Extracted ? report.ValueGained : report.ValueLost,
            BroughtDownBy = broughtDownBy,
        };

        if (report.Events != null)
        {
            raid.Lines.AddRange(report.Events);
        }

        foreach (ResidentDefinition rescued in report.Rescued)
        {
            raid.Lines.Add($"{rescued.DisplayName} came home with you.");
        }

        if (report.Companion != null)
        {
            raid.Lines.Add(report.CompanionLost ? $"{report.Companion.DisplayName} did not come home." : $"{report.Companion.DisplayName} went with you and came home.");
        }

        GameSession.Progress.Journal.AddRaid(raid);
    }

    private static void Post(string message) => GameMessages.Post(GameInput.WithRebinds(message));
}
