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
            ResourceSource.Harvested += OnHarvested;
            HavenTravel.Arrived += OnTravelled;
            // Boss fights: a Boss row at each moment, a BossTry row as each try ends.
            BossTryLog.Listen();
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
        FlushFrames();
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
            WriteLoadout(Player.Current);
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
        sceneName = scene.name;
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
        // Measured in game time: a pause (or a cinematic that holds the game still) is not a hold running long. In unscaled time every
        // pause during a dodge's moment of grace, and the prologue's ending, read as Stuck rows (the user's build, 2026-11-25).
        foreach (string leak in leases.Look(Time.time))
        {
            Debug.LogWarning("[LeaseWatch] " + leak);
            if (logging)
            {
                Write("Stuck", leak.Split(' ')[0], 0, leak);
            }
        }
    }

    // ----- How the game ran (PlaytestLog.Perf, Machine; Docs/QualityLevers_2026-11-24.md §2) -----

    private readonly FrameTally frames = new FrameTally();
    private readonly FrameTiming[] timings = new FrameTiming[1];
    // Where the frames being tallied were drawn: a raid's id, "home", or the scene's name (the prologue, a friend's Haven).
    private string framesAt;
    // Frames from a scene change until this time aren't counted: the load's own long frames are a loading screen, not play.
    private float settledAt;
    private bool wasLoading;
    // The display settings last written in a Machine row: a change (resolution, quality, frame cap) writes a new one.
    private string machineWritten;
    private static float averageFrame = 1f / 60f;
    // The active scene's name, read when a scene is entered (GetActiveScene().name makes a string a call: it was read every frame).
    private string sceneName = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetFrameRate() => averageFrame = 1f / 60f;

    /// <summary>The frame rate of the last second or so of real frames, in every build: what F8's report says. Time.smoothDeltaTime
    /// follows the time scale, so a report written while paused said 10000 fps (2026-11-21).</summary>
    public static int RecentFps => Mathf.RoundToInt(1f / Mathf.Max(0.0001f, averageFrame));

    private static void TrackFrameRate()
    {
        float frame = Time.unscaledDeltaTime;
        if (frame > 0f && frame < 1f)
        {
            averageFrame = Mathf.Lerp(averageFrame, frame, 0.05f);
        }
    }

    private void TallyFrame()
    {
        bool loading = SceneTransition.IsBusy;
        if (loading || wasLoading)
        {
            settledAt = Time.unscaledTime + 1f;
        }

        wasLoading = loading;
        // Paused or away, then the frame after: one long frame of coming back is not a hitch (review 2026-11-24: alt-tab set "worst").
        if (GamePause.IsPaused || !Application.isFocused)
        {
            settledAt = Mathf.Max(settledAt, Time.unscaledTime + 0.25f);
        }

        if (loading || Time.unscaledTime < settledAt)
        {
            return;
        }

        string at = FramesPlace();
        if (at != framesAt)
        {
            FlushFrames();
            framesAt = at;
        }

        frames.Add(Time.unscaledDeltaTime * 1000f);
        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
        {
            frames.AddSplit(timings[0].cpuMainThreadFrameTime, timings[0].gpuFrameTime);
        }
    }

    private string FramesPlace()
    {
        RaidDirector raid = RaidDirector.Current;
        if (raid != null && raid.Session != null && raid.Session.Definition != null)
        {
            return raid.Session.Definition.Id;
        }

        return Place.InAHaven && Place.AtHome ? "home" : sceneName;
    }

    // A minute's frames (or fewer, at a change of place) as one Perf row: subject the place, amount the 95th percentile in whole ms.
    private void FlushFrames()
    {
        if (frames.Frames >= 30 && framesAt != null)
        {
            WriteMachine();
            Write(PlaytestLog.Perf, framesAt, Mathf.RoundToInt(frames.Percentile(0.95f)), frames.Detail());
        }

        frames.Clear();
    }

    // The machine and its display settings: once a session, and again when the settings change.
    private void WriteMachine()
    {
        string display = $"screen {Screen.width}x{Screen.height} {Screen.fullScreenMode} quality {QualitySettings.names[QualitySettings.GetQualityLevel()]} " +
                         $"vsync {QualitySettings.vSyncCount} cap {Application.targetFrameRate}";
        if (display == machineWritten)
        {
            return;
        }

        machineWritten = display;
        Write(PlaytestLog.Machine, SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize,
            $"{display} api {SystemInfo.graphicsDeviceType} cpu {SystemInfo.processorType} x{SystemInfo.processorCount} ram {SystemInfo.systemMemorySize} os {SystemInfo.operatingSystem}");
    }

    private void Update()
    {
        LookForLeaks();
        TrackFrameRate();
        if (!logging)
        {
            return;
        }

        PlaytestRun current = EnsureRun();
        FollowPlayer();
        TallyFrame();
        // Played time: not paused, and the game in front (time in another window isn't play: Docs/CampaignAudit_2026-11-23.md §4.5).
        if (!GamePause.IsPaused && Application.isFocused)
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
    // Walking is judged over a quarter of a second, not a frame: physics moves the player on some frames only, so a frame's step read
    // most walking as standing still (1% walking in the 2026-11-21 run).
    private Vector3 walkFrom;
    private float walkSince = -1f;
    private bool walking;
    private const float WalkSample = 0.25f;
    private const float WalkSpeed = 0.6f;
    // "window:" + each window's id, made once (written every frame at home).
    private readonly System.Collections.Generic.Dictionary<UIWindowId, string> windowKeys = new System.Collections.Generic.Dictionary<UIWindowId, string>();
    private PlayerInteractor interactor;

    private void TrackHome()
    {
        bool raid = Place.OnRaid;
        if (raid && !wasRaid)
        {
            minedThisRaid.Clear();
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
        if (GamePause.IsPaused || !Application.isFocused || player == null || !Place.InAHaven || !Place.AtHome)
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
        float now = Time.unscaledTime;
        if (walkSince < 0f || now - walkSince >= WalkSample)
        {
            walking = walkSince >= 0f && (at - walkFrom).magnitude / Mathf.Max(0.01f, now - walkSince) > WalkSpeed;
            walkFrom = at;
            walkSince = now;
        }

        if (KeeperView.IsOpen)
        {
            return "keeper";
        }

        // Which window (a conversation is one too): what in the home's time is spent in menus.
        if (UIStateManager.Current != null && UIStateManager.Current.IsAnyWindowOpen)
        {
            UIWindow front = UIStateManager.Current.Front;
            if (front == null)
            {
                return "window";
            }

            if (!windowKeys.TryGetValue(front.Id, out string key))
            {
                key = "window:" + front.Id;
                windowKeys[front.Id] = key;
            }

            return key;
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

        return walking ? "walk" : "idle";
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
    // What the player gathered in this raid, by node (ResourceSource.Harvested), written as it ends (NodesMined).
    private readonly System.Collections.Generic.Dictionary<string, int> minedThisRaid = new System.Collections.Generic.Dictionary<string, int>();

    private void OnHarvested(GameObject who, ResourceSource source)
    {
        GatheringNodeDefinition node = source != null ? source.Node : null;
        if (node != null && source.CountsAsNode && Place.OnRaid && who != null && Player.IsPlayer(who))
        {
            minedThisRaid.TryGetValue(node.name, out int n);
            minedThisRaid[node.name] = n + 1;
        }
    }

    private void WriteRaidEnd()
    {
        var standing = new System.Collections.Generic.Dictionary<string, int>();
        foreach (ResourceNode node in ResourceNode.Active)
        {
            if (node != null && node.Definition != null && !node.IsDepleted)
            {
                standing.TryGetValue(node.Definition.name, out int n);
                standing[node.Definition.name] = n + 1;
            }
        }

        foreach (var mined in minedThisRaid)
        {
            standing.TryGetValue(mined.Key, out int left);
            Write(PlaytestLog.NodesMined, mined.Key, mined.Value, $"standing {left}");
        }

        minedThisRaid.Clear();
        Player player = Player.Current;
        if (player != null && player.TryGetComponent(out ItemInventory bag))
        {
            ItemContainer items = bag.Container;
            int used = 0;
            for (int i = 0; i < items.UnlockedSlots && i < items.SlotCount; i++)
            {
                used += items.GetSlot(i).IsEmpty ? 0 : 1;
            }

            int weight = items.HasWeightLimit && items.WeightCapacity > 0f ? Mathf.RoundToInt(items.CurrentWeight / items.WeightCapacity * 100f) : 0;
            Write(PlaytestLog.BagAtEnd, "bag", weight, $"slots {Mathf.RoundToInt(used * 100f / Mathf.Max(1, items.UnlockedSlots))}%");
        }
    }

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
            // How the last seconds went: every blow, small ones and ticks too (the Hurt rows keep only 5% or more).
            Damageable dying = followed != null ? followed.Health : null;
            int crowd = dying != null ? MonsterAI.CountAt(dying, dying.transform.position, 8f) : 0;
            Write(PlaytestLog.DeathRecap, NameOf(gameEvent.Actor), Mathf.RoundToInt(recap.LostWithin(Time.time)), recap.Describe(Time.time, crowd));
            recap.Clear();
            return;
        }

        bool raid = gameEvent.Kind == GameEventKind.RaidFinished;
        RaidSession session = raid && RaidDirector.Current != null ? RaidDirector.Current.Session : null;
        if (raid)
        {
            WriteKit("end");
            WriteRaidEnd();
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
        // A new session's file says again what it runs on.
        machineWritten = null;
        frames.Clear();
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

    // A blow worth noting: what struck the player, how hard, with what attack, and the health it left ("health N/M", appended so old
    // readers keep their columns). Every blow, small ones and ticks too, is kept a while for the death recap.
    private void OnPlayerHurt(DamageInfo damage, DamageResult result)
    {
        Damageable health = followed != null ? followed.Health : null;
        if (health == null)
        {
            return;
        }

        string by = damage.Source != null ? (damage.Source.TryGetComponent(out MonsterAI monster) && monster.Definition != null ? monster.Definition.name : damage.Source.name) : "unknown";
        recap.Note(Time.time, by, result.HealthLost, health.CurrentHealth + result.HealthLost, health.MaxHealth, damage.IsOverTime);
        if (result.Damage < health.MaxHealth * 0.05f || damage.IsOverTime)
        {
            return;
        }

        Write(PlaytestLog.Hurt, by, Mathf.RoundToInt(result.Damage), $"{(damage.Attack != null ? damage.Attack.name : "-")} {damage.Type}{(result.Blocked ? " blocked" : "")}"
            + WhereFrom(damage, health) + $" health {health.CurrentHealth:0}/{health.MaxHealth:0}");
    }

    private readonly DeathRecap recap = new DeathRecap();

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

    // The skill bar a raid begins with (Docs/SkillChoiceResearch_2026-11-27.md 3.7): which skills are chosen, against the weapon held.
    private void WriteLoadout(Player player)
    {
        PlayerSkills skills = player != null ? player.Skills : null;
        if (skills == null)
        {
            return;
        }

        PlayerCombat fighter = player.GetComponent<PlayerCombat>();
        Write(PlaytestLog.Loadout, PlaytestLog.LoadoutSubject(skills.Loadout), PlaytestLog.SkillsIn(skills.Loadout),
            fighter != null && fighter.Weapon != null ? fighter.Weapon.name : "");
    }

    // The old ways or Go there: how far it saved walking, and whether the horse came along.
    private void OnTravelled(Vector3 from, Vector3 to, bool rode) =>
        Write(PlaytestLog.Travel, rode ? "rode" : "on foot", Mathf.RoundToInt(new Vector2(to.x - from.x, to.z - from.z).magnitude));

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
        ResourceSource.Harvested -= OnHarvested;
        HavenTravel.Arrived -= OnTravelled;
        FlushSkillXp();
        combat.Unfollow();
        EndSession();
    }

    private static string NameOf(UnityEngine.Object subject) => subject != null ? subject.name : "";
}
