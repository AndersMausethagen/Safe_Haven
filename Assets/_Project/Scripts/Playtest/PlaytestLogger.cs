using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Writes the playtest log: every game event, scene change, death and session start/end of the current run, timed in
/// played minutes, to the run's CSV (see PlaytestLog). It also keeps the run's played time, which the save file carries.
/// A new run (New Game, or Play without loading) starts a new file; loading a save appends to that run's file as a new
/// session. Turned off with PlaytestSettings.LogRuns.
/// </summary>
public sealed class PlaytestLogger : MonoBehaviour, IGameSystem
{
    private const float MinimumRunSeconds = 60f;

    private bool logging;
    private PlaytestRun run;
    private StreamWriter writer;
    private string path;
    private bool createdFile;
    private bool hadGameplay;

    public void Initialise(PlaytestSettings settings)
    {
        logging = settings.LogRuns;
        Cheats.Used += OnCheat;
        if (logging)
        {
            GameEvents.Raised += OnGameEvent;
            ExperienceTracker.LevelReached += OnLevelReached;
            ExperienceTracker.InsightSpent += OnInsightSpent;
            ExperienceTracker.Gained += OnGained;
            Damageable.AnyDamaged += OnAnyDamaged;
        }
    }

    // Every creature's blows taken, written as a Kill row when it dies: how many blows it took and from whom.
    private readonly KillLedger kills = new KillLedger();

    private void OnAnyDamaged(Damageable victim, DamageInfo damage, DamageResult result)
    {
        if (result.Incoming <= 0f || !victim.TryGetComponent(out MonsterAI monster) || monster.Definition == null)
        {
            return;
        }

        Combatant source = damage.Source;
        bool fromPlayer = source != null && Player.Current != null && source.gameObject == Player.Current.gameObject;
        string sourceName = source == null ? "environment"
            : source.TryGetComponent(out Player _) ? "other player"
            : source.TryGetComponent(out MonsterAI attacker) && attacker.Definition != null ? attacker.Definition.name
            : source.name;
        KillLedger.Record done = kills.Note(victim, monster.Definition.name, victim.MaxHealth, fromPlayer, sourceName, damage.IsOverTime,
            result.IsCritical, result.HealthLost, result.Killed, Time.time);
        if (done != null)
        {
            Write(PlaytestLog.Kill, done.Name, done.PlayerHits, done.Detail);
        }
    }

    // Practice gathered over a played minute, written as one row per skill: what the pace is measured from.
    private readonly System.Collections.Generic.Dictionary<string, int> minuteXp = new System.Collections.Generic.Dictionary<string, int>();
    private float nextXpRow = 60f;

    private void OnGained(ProficiencyDefinition skill, int amount)
    {
        if (skill == null || ExperienceTracker.SpendingInsight)
        {
            return;
        }

        minuteXp.TryGetValue(skill.Id, out int sum);
        minuteXp[skill.Id] = sum + amount;
    }

    private void FlushSkillXp()
    {
        foreach (System.Collections.Generic.KeyValuePair<string, int> skill in minuteXp)
        {
            Write(PlaytestLog.SkillXp, skill.Key, skill.Value);
        }

        minuteXp.Clear();
        FlushCombat();
    }

    // How the player fought over the minute (CombatTally), one row per technique: the parry/aggression balance is read from these.
    private readonly CombatTally combat = new CombatTally();
    private Player followed;

    private RaidDirector kitLoggedFor;

    private void FollowPlayer()
    {
        // A raid has begun: the kit the player went in with.
        if (RaidDirector.Current != null && RaidDirector.Current != kitLoggedFor && Player.Current != null)
        {
            kitLoggedFor = RaidDirector.Current;
            WriteKit("start");
        }

        Player current = Player.Current;
        if (current == followed)
        {
            return;
        }

        if (followed != null && followed.Health != null)
        {
            followed.Health.Damaged -= OnPlayerHurt;
        }

        followed = current;
        combat.Follow(current != null ? current.gameObject : null);
        if (followed != null && followed.Health != null)
        {
            followed.Health.Damaged += OnPlayerHurt;
        }
    }

    private void FlushCombat()
    {
        foreach (System.Collections.Generic.KeyValuePair<string, int> count in combat.Counts)
        {
            Write(PlaytestLog.Combat, count.Key, count.Value, combat.Weapon);
        }

        combat.Clear();
    }

    // Levels with the played minute they came at: what the skills' pace is measured from (PlaytestLog.SkillPace).
    private void OnLevelReached(ProficiencyDefinition skill, int level, bool milestone)
    {
        EnsureRun();
        Write(PlaytestLog.SkillLevel, skill.Id, level, ExperienceTracker.SpendingInsight ? "insight" : milestone ? "milestone" : "");
    }

    private void OnInsightSpent(ProficiencyDefinition skill, int amount)
    {
        EnsureRun();
        Write(PlaytestLog.InsightSpent, skill.Id, amount);
    }

    public void OnSceneEntered(Scene scene)
    {
        kills.Clear();
        Write(PlaytestLog.SceneEntered, scene.name);
        if (!string.IsNullOrEmpty(GameOptions.AssistsInUse))
        {
            Write(PlaytestLog.Assist, GameOptions.AssistsInUse);
        }
    }

    private void Update()
    {
        if (!logging)
        {
            return;
        }

        PlaytestRun current = EnsureRun();
        FollowPlayer();
        if (!GamePause.IsPaused)
        {
            current.PlayedSeconds += Time.unscaledDeltaTime;
        }

        if (current.PlayedSeconds >= nextXpRow)
        {
            nextXpRow = current.PlayedSeconds + 60f;
            FlushSkillXp();
        }
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        EnsureRun();
        hadGameplay = true;
        if (gameEvent.Kind == GameEventKind.Killed && gameEvent.Origin != null && Player.IsPlayer(gameEvent.Origin))
        {
            Write(PlaytestLog.PlayerDied, NameOf(gameEvent.Actor));
            return;
        }

        bool raid = gameEvent.Kind == GameEventKind.RaidFinished;
        RaidSession session = raid && RaidDirector.Current != null ? RaidDirector.Current.Session : null;
        if (raid)
        {
            WriteKit("end");
        }

        if (session != null && session.Difficulty != null)
        {
            Write(PlaytestLog.RaidTier, NameOf(gameEvent.Subject), session.Tier,
                PlaytestLog.TierDetail(session.GearScoreAtEntry, session.Difficulty.recommendedGearScore, session.TimeOfDay != null ? session.TimeOfDay.DisplayName : "scene hour"));
        }

        Write(gameEvent.Kind.ToString(), NameOf(gameEvent.Subject),
            raid ? Mathf.RoundToInt(gameEvent.Seconds) : gameEvent.Amount,
            raid ? gameEvent.Outcome.ToString() : "");
    }

    /// <summary>Follows GameSession's run: a different one means New Game or a load, so the file and session change.</summary>
    private PlaytestRun EnsureRun()
    {
        PlaytestRun current = GameSession.Playtest;
        if (current == run)
        {
            return current;
        }

        EndSession();
        run = current;
        hadGameplay = false;
        try
        {
            Directory.CreateDirectory(PlaytestLog.Folder);
            path = PlaytestLog.PathFor(run.Id);
            createdFile = !File.Exists(path);
            if (!createdFile)
            {
                // Loading an older save of this run again must not reuse a session number already in the file.
                foreach (PlaytestEntry entry in PlaytestLog.Read(path))
                {
                    run.Sessions = Mathf.Max(run.Sessions, entry.Session);
                }
            }

            writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) { AutoFlush = true };
            if (createdFile)
            {
                writer.WriteLine(PlaytestLog.Header);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            Debug.LogWarning("Playtest log disabled: " + exception.Message);
            logging = false;
            writer = null;
            return current;
        }

        run.Sessions++;
        WriteRow(PlaytestLog.SessionStart, Application.version, 0, run.Sessions == 1 ? "new run" : "continued");
        return current;
    }

    // The player's kit now: max health in the amount; armour, damage share, toughness and boons in the detail.
    private void WriteKit(string when)
    {
        Player player = Player.Current;
        if (player == null || player.Health == null || !player.TryGetComponent(out Combatant self))
        {
            return;
        }

        FightMath.Totals totals = FightMath.Of(self.Stats, player.Health.MaxHealth);
        var boons = player.TryGetComponent(out PlayerBoons active) ? active.Capture() : null;
        string names = boons != null && boons.Count > 0 ? string.Join("+", boons.ConvertAll(boon => boon.Name.Replace(';', ',').Replace(' ', '_'))) : "none";
        Write(PlaytestLog.Kit, when, Mathf.RoundToInt(player.Health.MaxHealth),
            $"armour {totals.Armor:0} damage x{totals.DamageShare(self.Rules):0.00} toughness {totals.Toughness(self.Rules):0} boons {names}");
    }

    // A blow worth noting: what struck the player, how hard, with what attack.
    private void OnPlayerHurt(DamageInfo damage, DamageResult result)
    {
        Damageable health = followed != null ? followed.Health : null;
        if (health == null || result.Damage < health.MaxHealth * 0.05f || damage.IsOverTime)
        {
            return;
        }

        string by = damage.Source != null ? (damage.Source.TryGetComponent(out MonsterAI monster) && monster.Definition != null ? monster.Definition.name : damage.Source.name) : "unknown";
        Write(PlaytestLog.Hurt, by, Mathf.RoundToInt(result.Damage), $"{(damage.Attack != null ? damage.Attack.name : "-")} {damage.Type}{(result.Blocked ? " blocked" : "")}");
    }

    private void Write(string eventName, string subject, int amount = 0, string detail = "")
    {
        if (!logging)
        {
            return;
        }

        EnsureRun();
        WriteRow(eventName, subject, amount, detail);
    }

    private void WriteRow(string eventName, string subject, int amount, string detail)
    {
        if (writer == null)
        {
            return;
        }

        writer.WriteLine(PlaytestLog.Format(new PlaytestEntry
        {
            PlayedSeconds = run.PlayedSeconds,
            Utc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            Session = run.Sessions,
            Scene = SceneManager.GetActiveScene().name,
            Event = eventName,
            Subject = subject,
            Amount = amount,
            Detail = detail,
        }));
    }

    private void EndSession()
    {
        if (writer == null)
        {
            return;
        }

        WriteRow(PlaytestLog.SessionEnd, "", 0, "");
        writer.Dispose();
        writer = null;

        // A run that never got going (such as the main menu before a load) leaves no file behind.
        if (createdFile && !hadGameplay && run.PlayedSeconds < MinimumRunSeconds)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }

    // A run helped along says so, so it isn't read as how the game plays.
    private void OnCheat(string cheat) => Write(PlaytestLog.Cheat, cheat);

    private void OnDestroy()
    {
        Cheats.Used -= OnCheat;
        GameEvents.Raised -= OnGameEvent;
        ExperienceTracker.LevelReached -= OnLevelReached;
        ExperienceTracker.InsightSpent -= OnInsightSpent;
        ExperienceTracker.Gained -= OnGained;
        Damageable.AnyDamaged -= OnAnyDamaged;
        FlushSkillXp();
        combat.Unfollow();
        EndSession();
    }

    private static string NameOf(UnityEngine.Object subject) => subject != null ? subject.name : "";
}
