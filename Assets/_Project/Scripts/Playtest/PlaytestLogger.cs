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
    // The highest Haven rank written in this run (OnGameEvent).
    private int rankWritten;

    private static PlaytestLogger instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => instance = null;

    /// <summary>Writes a row of the game's own (a place found, a region entered, a lookout used), when runs are logged.</summary>
    public static void Note(string eventName, string subject, int amount = 0, string detail = "")
    {
        if (instance != null)
        {
            instance.Write(eventName, subject, amount, detail);
        }
    }

    public void Initialise(PlaytestSettings settings)
    {
        instance = this;
        // F8 writes a playtest report in every build and scene (PlaytestReportWindow).
        PlaytestReportWindow.Listen();
        // The two-copy co-op test (CoopSmokeTest) isn't a playtest: its runs would read as two short human ones.
        logging = settings.LogRuns && !CoopSmokeTest.IsRunning;
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
    // Seconds between position samples in a raid (heatmaps).
    private const float PositionEvery = 2f;
    private const float HomePositionEvery = 5f;
    private float nextPosition;

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

    // What is held and never let go (LeaseWatch), looked at every few seconds, in every build: a warning in the player's log, and a
    // "Stuck" row when the run is logged.
    private readonly LeaseWatch leases = new LeaseWatch();
    private float nextLeaseLook;

    private void LookForLeaks()
    {
        if (Time.unscaledTime < nextLeaseLook)
        {
            return;
        }

        nextLeaseLook = Time.unscaledTime + 5f;
        foreach (string leak in leases.Look(Time.unscaledTime))
        {
            Debug.LogWarning("[LeaseWatch] " + leak);
            if (logging)
            {
                Write("Stuck", leak.Split(' ')[0], 0, leak);
            }
        }
    }

    private void Update()
    {
        LookForLeaks();
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

        // Where the player walks: every 2 s on a raid, every 5 s at home (Docs/HavenExplorationReview_2026-11-19.md §0: nothing said
        // where anyone went in the Haven).
        if (Player.Current != null && current.PlayedSeconds >= nextPosition)
        {
            bool raid = RaidDirector.Current != null;
            nextPosition = current.PlayedSeconds + (raid ? PositionEvery : HomePositionEvery);
            Vector3 at = Player.Current.transform.position;
            Write(PlaytestLog.Position, $"{at.x:0};{at.z:0}", 0, raid ? RaidDirector.Current.Session.Definition.Id : "home");
        }

        if (current.PlayedSeconds >= nextXpRow)
        {
            nextXpRow = current.PlayedSeconds + 60f;
            FlushSkillXp();
        }

        TrackHome();
    }

    // ----- The economy in numbers: home time, the stock on leaving, raid nodes (PlaytestLog.HomeStay, Stock, RaidNodes) -----

    private readonly System.Collections.Generic.Dictionary<string, float> homeSeconds = new System.Collections.Generic.Dictionary<string, float>();
    private bool wasRaid;
    private float countNodesAt = -1f;
    private Vector3 lastAt;
    private PlayerInteractor interactor;

    private void TrackHome()
    {
        bool raid = Place.OnRaid;
        if (raid && !wasRaid)
        {
            FlushHome();
            WriteStock();
            countNodesAt = Time.unscaledTime + 8f;
        }

        wasRaid = raid;
        if (raid)
        {
            if (countNodesAt > 0f && Time.unscaledTime >= countNodesAt)
            {
                countNodesAt = -1f;
                CountNodes();
            }

            return;
        }

        Player player = Player.Current;
        // One's own Haven: a portal stands here (not the prologue's camp), no raid, no visit.
        if (GamePause.IsPaused || player == null || !Place.InAHaven || !Place.AtHome)
        {
            return;
        }

        string doing = Doing(player);
        homeSeconds.TryGetValue(doing, out float seconds);
        homeSeconds[doing] = seconds + Time.unscaledDeltaTime;
    }

    private string Doing(Player player)
    {
        Vector3 at = player.transform.position;
        float moved = (at - lastAt).sqrMagnitude;
        lastAt = at;
        if (KeeperView.IsOpen)
        {
            return "keeper";
        }

        if (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen)
        {
            return "window";
        }

        if (interactor == null || interactor.gameObject != player.gameObject)
        {
            interactor = player.GetComponent<PlayerInteractor>();
        }

        Interactable working = interactor != null ? interactor.Current : null;
        if (working != null)
        {
            return working.GetComponentInParent<Construction>() != null ? "build" : working.GetComponentInParent<CraftingStation>() != null ? "craft" : "gather";
        }

        float speed = Time.unscaledDeltaTime > 0f ? Mathf.Sqrt(moved) / Time.unscaledDeltaTime : 0f;
        return speed > 0.6f ? "walk" : "idle";
    }

    // A stay at home is over (a raid began, or the session ends): the seconds at each thing. Written straight to the file of the
    // session it belongs to (WriteRow): from EndSession, Write would ask EnsureRun again, which ends the session again (a stack overflow,
    // SceneSmokeLiveTests 2026-11-22).
    private void FlushHome()
    {
        foreach (var spent in homeSeconds)
        {
            if (spent.Value >= 1f)
            {
                WriteRow(PlaytestLog.HomeStay, spent.Key, Mathf.RoundToInt(spent.Value), "");
            }
        }

        homeSeconds.Clear();
    }

    // The plain stock taken along in the chests, as a raid begins (at a session's end the stash may already be the next game's).
    private void WriteStock()
    {
        KeeperViewSettings view = KeeperViewSettings.Current;
        if (view == null || GameSession.Stash == null)
        {
            return;
        }

        foreach (ItemDefinition item in view.Stores)
        {
            if (item != null)
            {
                WriteRow(PlaytestLog.Stock, item.Id, GameSession.Stash.Items.Count(item), "");
            }
        }
    }

    // What a raid holds: every gathering node standing a few seconds in, by kind.
    private void CountNodes()
    {
        var counts = new System.Collections.Generic.Dictionary<string, int>();
        foreach (ResourceNode node in ResourceNode.Active)
        {
            if (node != null && node.Definition != null)
            {
                counts.TryGetValue(node.Definition.name, out int n);
                counts[node.Definition.name] = n + 1;
            }
        }

        foreach (var kind in counts)
        {
            Write(PlaytestLog.RaidNodes, kind.Key, kind.Value, RaidDirector.Current != null ? RaidDirector.Current.Session.Definition.Id : "");
        }
    }

    private void OnGameEvent(GameEvent gameEvent)
    {
        EnsureRun();
        // The Haven's rank is told again every 2 s (HavenProgress, so a quest begun after it was reached still finishes): written once
        // a rank. Every telling was a row, 1,800 an hour at home (219 in one session of 2026-11-15), padding the logs read for balance.
        if (gameEvent.Kind == GameEventKind.HavenRankReached)
        {
            if (gameEvent.Amount <= rankWritten)
            {
                return;
            }

            rankWritten = gameEvent.Amount;
        }

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
        rankWritten = 0;
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
        Write(PlaytestLog.Hurt, by, Mathf.RoundToInt(result.Damage), $"{(damage.Attack != null ? damage.Attack.name : "-")} {damage.Type}{(result.Blocked ? " blocked" : "")}"
            + WhereFrom(damage, health));
    }

    /// <summary>Where a blow came from (Docs/EnemyAIReview_2026-11-19.md §0), appended to the Hurt row so old readers still work: " from
    /// front|side|back" (front within the guard's 70° either side of facing, back beyond 110°), " offscreen" when the striker was out of
    /// view, and " crowd N", the creatures after the player within 8 m.</summary>
    public static string WhereFrom(in DamageInfo damage, Damageable victim)
    {
        if (damage.Source == null || victim == null)
        {
            return "";
        }

        Vector3 to = damage.Source.transform.position - victim.transform.position;
        to.y = 0f;
        Vector3 facing = victim.transform.forward;
        facing.y = 0f;
        string side = to.sqrMagnitude < 0.0001f ? "front" : FromSide(Vector3.Angle(facing, to));
        bool offscreen = damage.Source.TryGetComponent(out MonsterAI monster) && monster.IsOffScreenForLocalPlayer;
        int crowd = MonsterAI.CountAt(victim, victim.transform.position, 8f);
        return $" from {side}{(offscreen ? " offscreen" : "")} crowd {crowd}";
    }

    /// <summary>front, side or back, by the angle between the player's facing and the striker.</summary>
    public static string FromSide(float angle) => angle <= 70f ? "front" : angle >= 110f ? "back" : "side";

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

        // The stay at home under way when the session ends (quitting, loading another save) is written too.
        FlushHome();
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
        if (instance == this)
        {
            instance = null;
        }

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
