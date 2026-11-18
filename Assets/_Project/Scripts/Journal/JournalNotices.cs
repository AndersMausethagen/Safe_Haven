using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tells the player when the journal has something new, finds torn pages in raids and writes each raid down. The journal's window only reads what this leaves
/// behind, so all of this goes on with the window closed. A page of how things work that opens becomes a tutorial (Tutorials): the first time a skill is
/// tried (Tried, from practice and from the systems that start one: a swing at a node, a cast) opens the pages that wait for it.
/// </summary>
public static class JournalNotices
{
    // After SubsystemRegistration, where GameEvents clears Raised: Unity doesn't order hooks within one phase, and one run
    // after this one would leave the journal deaf for the whole session.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Listen()
    {
        GameEvents.Raised -= OnGameEvent;
        GameEvents.Raised += OnGameEvent;
        ExperienceTracker.Practised -= OnPractised;
        ExperienceTracker.Practised += OnPractised;
        Player.Spawned -= Follow;
        Player.Spawned += Follow;
        MonsterAI.AnyStaggered -= OnAnyStaggered;
        MonsterAI.AnyStaggered += OnAnyStaggered;
    }

    // ----- Fighting: each combat page opens the first time what it is about comes up (JournalPage.unlockedByTechnique) -----

    /// <summary>The keys a page can wait for.</summary>
    public static readonly string[] Techniques = { "guard", "parry", "dodge", "stagger", "stamina", "hurt" };

    public static string TechniqueKey(string technique) => "technique:" + technique;

    private static PlayerGuard guard;
    private static PlayerDodge dodge;
    private static PlayerStamina stamina;
    private static PlayerCombat combat;
    private static Damageable health;

    /// <summary>The player has just done this in a fight: the first time, the pages that wait for it open.</summary>
    public static void TriedTechnique(string technique)
    {
        if (!string.IsNullOrEmpty(technique) && GameSession.Progress.Journal.NoteTried(TechniqueKey(technique)))
        {
            CheckPages();
        }
    }

    private static void Follow(Player player)
    {
        if (guard != null) { guard.Blocked -= OnBlocked; guard.Parried -= OnParried; }
        if (dodge != null) dodge.Dodged -= OnDodged;
        if (stamina != null) stamina.SpendRefused -= OnStaminaRefused;
        if (combat != null) combat.SwingStarted -= OnSwingStarted;
        if (health != null) health.Damaged -= OnHurt;
        guard = player != null ? player.GetComponent<PlayerGuard>() : null;
        dodge = player != null ? player.GetComponent<PlayerDodge>() : null;
        stamina = player != null ? player.Stamina : null;
        combat = player != null ? player.GetComponent<PlayerCombat>() : null;
        health = player != null ? player.Health : null;
        if (guard != null) { guard.Blocked += OnBlocked; guard.Parried += OnParried; }
        if (dodge != null) dodge.Dodged += OnDodged;
        if (stamina != null) stamina.SpendRefused += OnStaminaRefused;
        if (combat != null) combat.SwingStarted += OnSwingStarted;
        if (health != null) health.Damaged += OnHurt;
    }

    private static void OnBlocked(DamageInfo damage) => TriedTechnique("guard");
    private static void OnParried(DamageInfo damage) { TriedTechnique("guard"); TriedTechnique("parry"); }
    private static void OnDodged(Vector3 direction) => TriedTechnique("dodge");
    private static void OnStaminaRefused() => TriedTechnique("stamina");
    private static void OnSwingStarted(SwingInfo swing) { if (swing.Kind == SwingKind.Art) TriedTechnique("stamina"); }

    private static void OnHurt(DamageInfo damage, DamageResult result)
    {
        if (!result.Blocked && result.Damage > 0f && !damage.IsOverTime && damage.Source != null)
        {
            TriedTechnique("hurt");
        }
    }

    // A creature staggering close to the player: the player's doing, near enough.
    private static void OnAnyStaggered(MonsterAI monster, float seconds)
    {
        if (combat != null && monster != null && (monster.transform.position - combat.transform.position).sqrMagnitude < 64f)
        {
            TriedTechnique("stagger");
        }
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
            Post($"New in your journal: {fresh[0]} ({{key:Journal}})");
        }
        else if (fresh.Count > 1)
        {
            Post($"{fresh.Count} new pages in your journal ({{key:Journal}})");
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
        Post(firstTime ? $"New in your journal: {creature.DisplayName} ({{key:Journal}})" : $"You understand {name} better ({{key:Journal}})");
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
                Post($"Among the rest, something written: {page.title} ({{key:Journal}})");
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

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        combat = null;
        dodge = null;
        guard = null;
        health = null;
        stamina = null;
    }
}
