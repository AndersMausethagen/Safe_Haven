using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs a raid scene. It starts the clock with the rules the raid was launched with, strengthens the enemies when
/// modifiers say so, warns as time runs low, and ends the raid when the player extracts (see ExtractionPoint), dies or
/// runs out of time. Extracting takes everything the player carries back home. Failing (dying or running out of time)
/// keeps knowledge, the safe pocket and the deployed equipment with durability damage, and loses everything else (GDD
/// §8.3, PlayerSnapshot.AfterFailedRaid); the player then wakes at the portal. After a short pause the player is taken
/// to the full-screen results (RaidResultsScreen) and from there home. The clock only starts once the scene is generated
/// and the loading screen lifts (SceneTransition.WhenReady). A scene played straight from the
/// editor runs a test raid from the fallback definition. Systems in the raid read its rules through <see cref="Current"/>.
/// Its parts: <see cref="RaidCreatures"/> (the creatures' strength, hunting and waking), <see cref="RaidCompanions"/>
/// (who came along and the lost one) and <see cref="RaidLostOnes"/> (one of the Lost walking the raid).
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-50)]
public sealed class RaidDirector : MonoBehaviour
{
    [Tooltip("Played when this scene is started without going through a raid portal, for testing.")]
    [SerializeField] private RaidDefinition fallbackRaid;
    [Tooltip("Where a test raid sends the player when it ends.")]
    [SerializeField] private string fallbackReturnScene = "SafeHaven";
    [Tooltip("Seconds left at which the player is warned that time is running out.")]
    [SerializeField] private List<float> warningTimes = new List<float> { 120f, 60f, 30f, 10f };
    [Tooltip("Seconds the outcome is shown before the player is sent back.")]
    [SerializeField, Min(0f)] private float returnDelay = 4f;
    [Tooltip("The full-screen raid report shown between the raid and home. When it isn't in Build Settings, the player " +
             "goes straight home and sees the report there.")]
    [SerializeField] private string resultsScene = "RaidResults";

    // A boss encounter's one warning before its hidden limit (the research: "The mist is closing in").
    private const float EncounterLastWarning = 120f;

    private readonly List<float> pendingWarnings = new List<float>();
    private bool turnedBack;
    private GameObject player;
    private Damageable playerHealth;
    private RaidGenerator generator;
    private bool running;
    private bool testRaid;
    private bool firstVisitChecked;
    private float calmUntil = -1f;

    /// <summary>The raid running in the loaded scene, or null outside raids.</summary>
    public static RaidDirector Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        Current = null;
        Application.wantsToQuit -= LeaveRaidOnQuit;
        Application.wantsToQuit += LeaveRaidOnQuit;
    }

    // The game closed mid-raid (the window's X, Alt+F4, Steam's quit) ends the raid as a fall, saved before the game goes, as quitting
    // from the pause menu does (Abandon). It undid the raid for free: the next load was the launch save, bag full, gear unworn, and on
    // Harsh it dodged "a fall takes everything" (review 2026-11-27). In a build only: stopping Play in the Editor must never write a
    // save. A crash still comes back to the launch save.
    private static bool LeaveRaidOnQuit()
    {
        if (!Application.isEditor && Place.InRaidScene && !Current.IsDecided)
        {
            try
            {
                Current.Abandon();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        return true;
    }

    /// <summary>The people with the player: the resident who came along and the lost one found out here.</summary>
    public RaidCompanions Companions { get; private set; }

    /// <summary>The raid's creatures readied: strength, hunting, waiting groups woken.</summary>
    public RaidCreatures Creatures { get; private set; }

    /// <summary>The one of the Lost walking this raid, if any, and what becomes of it.</summary>
    public RaidLostOnes LostOnes { get; private set; }

    public RaidSession Session { get; private set; }
    public RaidParameters Parameters => Session.Parameters;
    public GameObject Player => player;

    /// <summary>True from when the clock starts until the raid ends.</summary>
    public bool IsRunning => running && Session != null && !Session.IsOver;

    /// <summary>
    /// True for a few seconds after a first-visit cinematic (RaidDefinition › Calm After Cinematic): creatures notice
    /// nobody, so the player can take in where they are. One the player strikes still fights back.
    /// </summary>
    public static bool IsArrivalCalm => Current != null && Current.calmUntil > Time.time;

    /// <summary>The report of the raid once it has ended (also handed to GameSession); null while it runs.</summary>
    public RaidReport Report { get; private set; }

    /// <summary>Raised once when the raid ends, after the outcome has been recorded.</summary>
    public event Action<RaidSession> Ended;

    /// <summary>How many danger stages have begun (RaidDefinition.Stages): 0 is the calm before the first.</summary>
    public int StageIndex { get; private set; }

    /// <summary>The danger stage the raid is in, or null before the first.</summary>
    public RaidStage CurrentStage => RaidStages.Get(Session?.Definition.Stages, StageIndex);

    /// <summary>How much more the woods give now for staying on (the danger stage's Loot Bonus): 1 in the calm.</summary>
    public float StageLoot => 1f + (CurrentStage != null ? CurrentStage.LootBonus : 0f);

    /// <summary>What every node, tree and stone gives now, and how much more tools wear for it (RaidStage › Gather Bonus).</summary>
    public float StageGather => 1f + (CurrentStage != null ? CurrentStage.GatherBonus : 0f);

    /// <summary>The stage's gather now, 1 outside a raid.</summary>
    public static float StageGatherNow => Current != null ? Current.StageGather : 1f;

    /// <summary>The extraction hold's length now (RaidStage › Hold Scale).</summary>
    public float HoldScale => CurrentStage != null ? CurrentStage.HoldScale : 1f;

    /// <summary>True when this raid opens portals at its stones as the clock runs out (not a boss encounter, overtime set).</summary>
    public bool OvertimeAllowed => Session != null && !Session.Definition.IsEncounter && RaidPacingSettings.Current.OvertimeSeconds > 0f;

    /// <summary>The clock has run out and the portals stand open (RaidPacingSettings › Overtime).</summary>
    public bool InOvertime => OvertimeAllowed && !Session.IsOver && Session.TimeRemaining <= 0f;

    /// <summary>Seconds before the portals close, in overtime.</summary>
    public float OvertimeLeft => InOvertime ? Mathf.Max(0f, RaidPacingSettings.Current.OvertimeSeconds - Session.Overtime) : 0f;

    /// <summary>How hard the mist grips the player now: the stage's Mist-touched and the overtime steps (0: not at all).</summary>
    public int MistLevel => (CurrentStage != null && CurrentStage.MistPenalties.Count > 0 ? StageIndex : 0)
                            + (InOvertime ? RaidPacingSettings.Current.MistStepsAt(Session.Overtime) : 0);

    /// <summary>What the mist takes now, for the HUD's words.</summary>
    public IReadOnlyList<StatBonus> MistNow => mistNow;

    private readonly List<StatBonus> mistNow = new List<StatBonus>();
    private readonly LeaseKey mistSource = new LeaseKey("Mist-touched");
    private int mistShown = -1;
    private bool toldOvertime;
    private float nextOvertimeWarning = 60f;

    /// <summary>Raised as the portals open (overtime begins).</summary>
    public event Action OvertimeBegan;

    /// <summary>Raised when Mist-touched changes (a stage, an overtime step).</summary>
    public event Action MistChanged;

    // Those freed and sent home ahead through a stone (RescuableTraveler.SendHome): listed with the rescued when the raid ends.
    private readonly List<ResidentDefinition> sentHome = new List<ResidentDefinition>();

    /// <summary>Notes someone who went home ahead, for the results.</summary>
    public void NoteSentHome(ResidentDefinition resident)
    {
        if (resident != null && !sentHome.Contains(resident))
        {
            sentHome.Add(resident);
        }
    }

    /// <summary>The stage loot of the scene's raid, or 1 outside a raid.</summary>
    public static float StageLootNow => Current != null ? Current.StageLoot : 1f;

    /// <summary>Raised when a new danger stage begins, after enemies are strengthened and its spawn groups woken.</summary>
    public event Action<int, RaidStage> StageChanged;

    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Debug.LogError("Only one RaidDirector can run at a time.", this);
            enabled = false;
            Destroy(this);
            return;
        }

        // "global::" because this class has its own Player property (the player's GameObject).
        player = global::Player.Current != null ? global::Player.Current.gameObject : null;
        testRaid = GameSession.ActiveRaid == null;
        Session = GameSession.ActiveRaid ?? StartTestRaid();
        if (Session == null)
        {
            Debug.LogError("RaidDirector has no raid to run. Enter through a raid portal, or set a fallback raid to test the scene.", this);
            enabled = false;
            return;
        }

        Current = this;
        Companions = new RaidCompanions(this);
        Creatures = new RaidCreatures(this);
        LostOnes = new RaidLostOnes(this);

        // The raid's own systems (roaming spawns and the like, set up in the Systems Editor) are added here; every raid
        // scene also sleeps far idle creatures (global raid rules).
        foreach (RaidSystem system in Session.Definition.Systems)
        {
            if (system != null && system.Enabled)
            {
                system.Install(this);
            }
        }

        if (!TryGetComponent(out CreatureSleep _))
        {
            gameObject.AddComponent<CreatureSleep>();
        }

        if (!TryGetComponent(out GatheringNoise _))
        {
            gameObject.AddComponent<GatheringNoise>();
        }

        if (!TryGetComponent(out StageReveal _))
        {
            gameObject.AddComponent<StageReveal>();
        }

        if (!TryGetComponent(out RaidPlacesSeen _))
        {
            gameObject.AddComponent<RaidPlacesSeen>();
        }
    }

    private void Start()
    {
        if (Session == null)
        {
            return;
        }

        if (player != null && player.TryGetComponent(out playerHealth))
        {
            playerHealth.Died += OnPlayerDied;
        }
        else
        {
            Debug.LogWarning("RaidDirector found no Player with a Damageable, so dying won't end the raid.", this);
        }

        PlayerGathering.Swung += OnSwung;
        GameEvents.Raised += OnGameEvent;
        // Thresholds the raid starts below (a short test raid) would all fire at once, so they are skipped. A boss encounter has no
        // raid clock to warn of: its clock is the boss's enrage (EncounterArena), and its raid time only a hidden safety net.
        foreach (float time in Session.Definition.IsEncounter ? new[] { EncounterLastWarning } : (IEnumerable<float>)warningTimes)
        {
            if (time > 0f && time < Session.TimeRemaining)
            {
                pendingWarnings.Add(time);
            }
        }

        pendingWarnings.Sort((a, b) => b.CompareTo(a));
        // Like the warnings, stages a short test raid starts inside are already under way, without fanfare or waves.
        StageIndex = RaidStages.IndexAt(Session.Definition.Stages, Session.TimeRemaining);
        generator = FindAnyObjectByType<RaidGenerator>();
        // The clock starts once the loading screen lifts and the layout is generated, which may spawn more enemies.
        SceneTransition.WhenReady(BeginRaid);
    }

    private void BeginRaid()
    {
        if (this == null || Session.IsOver)
        {
            return;
        }

        // A character's first time here plays the raid's cinematic first; the raid begins when it ends.
        if (PlayFirstVisit())
        {
            return;
        }

        running = true;
        Creatures.StrengthenAll(true);
        Companions.BringCompanion();
        if (Session.Definition.IsEncounter)
        {
            // A boss encounter: no guards, tolls or elites; the boss waits beyond the gate (EncounterArena).
            GameMessages.Post(Session.Definition.Encounter.ArrivalLine, MessageKind.Warning);
            return;
        }

        var pieceRandom = new System.Random(RaidLayout.SeedFor(Session.Seed, "Set Piece Guards"));
        // Following the host (co-op), their game brings the guards from the same seed.
        foreach (RaidSetPieceSite site in CreatureSource.Remote == null ? FindObjectsByType<RaidSetPieceSite>(FindObjectsInactive.Exclude) : System.Array.Empty<RaidSetPieceSite>())
        {
            site.BringGuards(this, pieceRandom);
        }

        // A valley's bridges ask their toll from the start (RaidBridgeToll): the toll-taker sits there for the whole raid, quietly, until
        // the player comes upon it (RaidEventDirector.Stand). In every game, a friend's following the host too (review 2026-11-17, R2):
        // event sites are each player's own, as random events and rumours are, and what walking through one wakes is asked of the host's
        // game (SpawnCreaturesOutcome). Only the guards above are the host's to bring.
        if (TryGetComponent(out RaidEventDirector events))
        {
            foreach (RaidBridgeToll toll in FindObjectsByType<RaidBridgeToll>(FindObjectsInactive.Exclude))
            {
                if (toll.Event != null)
                {
                    events.Stand(toll.Event, toll.transform.position, toll.transform.rotation);
                }
            }
        }

        Transform start = FindAnyObjectByType<RaidMap>()?.PlayerStart;
        // Following the host, the creatures are the host's (puppets here): the host's game picks its elites.
        if (CreatureSource.Remote == null)
        {
            RaidElites.PromoteAtStart(Session, start != null ? start.position : (Vector3?)null, HealthMultiplier, PowerMultiplier);
            // One of this character's Lost, when the run's seed says one walks it (the briefing told the same).
            LostOnes.Summon(start != null ? start.position : (Vector3?)null);
            // The band's leader with a lost breach's plunder, when one carries it (the death rules).
            PlunderCarrier.Summon(this, start != null ? start.position : player != null ? player.transform.position : Vector3.zero);
        }
        if (NightSettings.IsDark)
        {
            GameMessages.Post(NightSettings.Current.NightRaidText, MessageKind.Warning);
        }

        GameMessages.Post($"{Session.Definition.DisplayName}: grab what you can and reach an extraction within {RaidStats.FormatTime(Session.TimeRemaining)}.");
    }

    /// <summary>
    /// Plays the raid's first-visit cinematic when this character has never been here (once per save; not for a test
    /// raid or in co-op, where pausing doesn't stop the others). The visit is remembered either way. True when the
    /// cinematic is playing and will begin the raid as it ends.
    /// </summary>
    private bool PlayFirstVisit()
    {
        if (firstVisitChecked)
        {
            return false;
        }

        firstVisitChecked = true;
        RaidDefinition raid = Session.Definition;
        bool first = !testRaid && GameSession.Progress.MarkRaidVisited(raid.Id);
        bool together = CoopSession.Current != null && CoopSession.Current.IsActive;
        if (!first || together || raid.FirstVisitCinematic == null)
        {
            return false;
        }

        CinematicScreen.Play(raid.FirstVisitCinematic, () =>
        {
            Arrive(raid);
            BeginRaid();
        });
        return true;
    }

    // The cinematic lifts into the game: fog rolls round the player, something stirs, and nothing notices them yet.
    private void Arrive(RaidDefinition raid)
    {
        if (this == null)
        {
            return;
        }

        calmUntil = Time.time + raid.CalmAfterCinematic;
        if (player == null)
        {
            return;
        }

        if (raid.ArrivalEffect != null)
        {
            Instantiate(raid.ArrivalEffect, player.transform.position, Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f));
        }

        if (raid.ArrivalSound != null)
        {
            AudioManager.Play(raid.ArrivalSound, player.transform.position);
        }
    }

    private void OnDestroy()
    {
        SceneTransition.CancelWhenReady(BeginRaid);
        PlayerGathering.Swung -= OnSwung;
        GameEvents.Raised -= OnGameEvent;
        if (playerHealth != null)
        {
            playerHealth.Died -= OnPlayerDied;
        }

        if (Current == this)
        {
            Current = null;
        }

        // Still active here means the scene was left mid-raid; the menu must be able to load a save again.
        GameSession.AbandonRaid(Session);
    }

    private bool toldSheltered;

    /// <summary>True when the player stands in the circle of an extraction that is open: time running out there doesn't end the raid.</summary>
    private bool InOpenExtraction()
    {
        if (player == null)
        {
            return false;
        }

        Vector3 at = player.transform.position;
        foreach (ExtractionPoint point in ExtractionPoint.Active)
        {
            if (point != null && point.IsOpen && point.Inside(at))
            {
                return true;
            }
        }

        return false;
    }

    private void Update()
    {
        if (!running || turnedBack || Session.IsOver)
        {
            return;
        }

        bool atZero = Session.TimeRemaining <= Time.deltaTime;
        float overtime = OvertimeAllowed ? RaidPacingSettings.Current.OvertimeSeconds : 0f;
        // A boss encounter's hidden limit never ends a fight that is won: once the boss has fallen it holds at zero. Without overtime,
        // an open circle holds the way home too (the old rule).
        bool sheltered = atZero && ((overtime <= 0f && InOpenExtraction()) || (EncounterArena.Current != null && EncounterArena.Current.BossFallen));
        if (Session.Tick(Time.deltaTime, sheltered, overtime))
        {
            Finish();
            return;
        }

        if (InOvertime)
        {
            if (!toldOvertime)
            {
                toldOvertime = true;
                nextOvertimeWarning = overtime > 60f ? 60f : overtime > 30f ? 30f : overtime > 10f ? 10f : 0f;
                GameMessages.Post($"The clock has run out: a portal opens at every stone. Step through, or stay while the mist allows ({RaidStats.FormatTime(overtime)}): " +
                                  "it grips harder the longer you stay, and then it takes you.", MessageKind.Warning);
                OvertimeBegan?.Invoke();
            }

            if (OvertimeLeft <= nextOvertimeWarning && nextOvertimeWarning > 0f)
            {
                GameMessages.Post($"The portals close in {RaidStats.FormatTime(nextOvertimeWarning)}!", MessageKind.Warning);
                nextOvertimeWarning = nextOvertimeWarning > 30f ? 30f : nextOvertimeWarning > 10f ? 10f : 0f;
            }
        }

        UpdateMist();

        if (sheltered && !toldSheltered)
        {
            toldSheltered = true;
            GameMessages.Post("Out of time, but the way home holds while you stand in its circle. Get out now!", MessageKind.Warning);
        }

        while (pendingWarnings.Count > 0 && Session.TimeRemaining <= pendingWarnings[0])
        {
            GameMessages.Post(Session.Definition.IsEncounter ? "The mist is closing in." : $"{RaidStats.FormatTime(pendingWarnings[0])} left! Get to an extraction.",
                MessageKind.Warning);
            pendingWarnings.RemoveAt(0);
        }

        int stage = RaidStages.IndexAt(Session.Definition.Stages, Session.TimeRemaining);
        while (StageIndex < stage)
        {
            EnterStage(StageIndex + 1);
        }
    }

    // Mist-touched (RaidStage › Mist Penalties, RaidPacingSettings › Mist Step Penalties): what the mist takes from the player now, as
    // modifiers on their stats, changed only when the stage or the overtime step does.
    private void UpdateMist()
    {
        int steps = InOvertime ? RaidPacingSettings.Current.MistStepsAt(Session.Overtime) : 0;
        int key = StageIndex * 100 + steps;
        if (key == mistShown || player == null || !player.TryGetComponent(out Combatant combatant))
        {
            return;
        }

        mistShown = key;
        combatant.Stats.RemoveModifiersFrom(mistSource);
        mistNow.Clear();
        RaidStage stage = CurrentStage;
        if (stage != null)
        {
            mistNow.AddRange(stage.MistPenalties);
        }

        for (int i = 0; i < steps; i++)
        {
            mistNow.AddRange(RaidPacingSettings.Current.MistStepPenalties);
        }

        StatBonus.Combine(mistNow);
        foreach (StatBonus bonus in mistNow)
        {
            combatant.Stats.AddModifier(new StatModifier(bonus.stat, bonus.op, bonus.value, mistSource));
        }

        MistChanged?.Invoke();
    }

    // The mist lets go of the player as the raid ends, however it ends.
    private void LiftMist()
    {
        if (player != null && player.TryGetComponent(out Combatant combatant))
        {
            combatant.Stats.RemoveModifiersFrom(mistSource);
        }

        mistNow.Clear();
        mistShown = -1;
    }

    private void EnterStage(int index)
    {
        StageIndex = index;
        RaidStage stage = CurrentStage;
        Creatures.StrengthenAll(false);
        if (stage != null)
        {
            GameMessages.Post(string.IsNullOrWhiteSpace(stage.Message) ? $"{stage.DisplayName}." : stage.Message, MessageKind.Warning);
            // The music follows the danger (T1): the stage's stinger, and a hush of the exploring music after it.
            if (stage.Stinger != null)
            {
                AudioManager.Play(stage.Stinger, transform.position);
            }

            AudioManager.HushExploring(stage.HushSeconds);
        }

        if (generator != null)
        {
            foreach (SpawnGroup group in generator.SpawnStage(index))
            {
                Creatures.PrepareGroup(group);
            }
        }

        StageChanged?.Invoke(index, stage);
        if (player != null && stage != null)
        {
            ExtractionPoint exit = ExtractionPoint.FindNearest(player.transform.position, out float metres);
            PlaytestLogger.Note(PlaytestLog.StageBegan, stage.DisplayName, Mathf.RoundToInt(Session.TimeRemaining), exit != null ? $"exit {Mathf.RoundToInt(metres)} m" : "no exit open");
        }
    }

    /// <summary>Ends the raid as extracted, if it is still running and the user is the player. Called by extraction points.</summary>
    public bool Extract(GameObject user)
    {
        // A downed player can't leave (co-op): helped up first, or the raid is lost.
        if (user == null || user != player || CoopDowned.IsDown || !Session.End(RaidOutcome.Extracted))
        {
            return false;
        }

        Finish();
        return true;
    }

    /// <summary>The raid's end is settled: ended (extracted, lost, out of time) or turned back from (TurnBack). Place asks this.</summary>
    public bool IsDecided => Session.IsOver || turnedBack;

    /// <summary>
    /// Leaves a boss encounter before its gate (its arrival stone): the research's "nothing gained and nothing lost", so this is no
    /// raid's end. No report, results, experience, homecoming or quest count, which an extraction would give for a walk to the gate and
    /// back; the run is let go as a quit is (GameSession.AbandonRaid) and the player goes home as they are. False when it can't.
    /// </summary>
    public bool TurnBack(GameObject user)
    {
        EncounterArena arena = EncounterArena.Current;
        if (user == null || user != player || CoopDowned.IsDown || turnedBack || Session.IsOver || !Session.Definition.IsEncounter || (arena != null && arena.IsSealed))
        {
            return false;
        }

        turnedBack = true;
        running = false;
        LiftMist();
        HavenReadiness.EndRest(player);
        if (player.TryGetComponent(out HandCraftQueue crafts))
        {
            crafts.Stop();
        }

        PlayerSnapshot final = PlayerSnapshot.Capture(player);
        GameSession.AbandonRaid(Session);
        GameSession.CarryPlayerState(final);
        GameSession.SetArrivalPoint(PlayerArrivalPoint.Portal);
        // Co-op: friends can't follow into a run its host has left.
        if (CoopSession.Current != null && CoopSession.Current.Raids != null)
        {
            CoopSession.Current.Raids.CloseFollowing();
        }

        SaveGameService.SaveAtRaidEnd(final);
        SaveGameService.SaveOnceHome();
        StopPlayer();
        GameMessages.Post(EndLine(true), MessageKind.Gain);
        StartCoroutine(TurnBackHome());
        return true;
    }

    private IEnumerator TurnBackHome()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        string scene = CanLoad(Session.ReturnScene) ? Session.ReturnScene : fallbackReturnScene;
        if (CanLoad(scene))
        {
            SceneLoader.Load(scene, "Back through the portal", ifItFails: () => TryAgain(TurnBackHome()));
        }
    }

    // A way out of a raid whose end is settled that failed (refused, or broken off on the way): tried again, or the player stayed held in
    // a finished raid for good, with nothing to press.
    private void TryAgain(IEnumerator leaving)
    {
        if (this == null)
        {
            return;
        }

        GameMessages.Post("The way didn't open. Trying again...", MessageKind.Warning);
        StartCoroutine(leaving);
    }

    private void OnSwung(GatheringSwing swing)
    {
        if (!Session.IsOver)
        {
            Session.Gathering.Record(swing.Result);
        }
    }

    // What this player's bag couldn't take: the summary shows it, with what would have carried it (HavenAnswers).
    private void OnGameEvent(GameEvent gameEvent)
    {
        if (gameEvent.Kind == GameEventKind.LeftBehind && gameEvent.ByPlayer)
        {
            Session.RecordLeftBehind(gameEvent.Subject, gameEvent.Amount);
        }
    }

    private void OnPlayerDied(Damageable victim, DamageInfo killingBlow)
    {
        if (Session.End(RaidOutcome.Died))
        {
            // Co-op: friends still standing on the run are watched first; the raid ends for this player afterwards.
            if (CoopSpectate.TryBegin(Finish))
            {
                return;
            }

            Finish();
        }
    }

    /// <summary>
    /// Quitting the game from the pause menu mid-raid ends it as a fall (Docs/SystemDesignReview_2026-11-17.md K1): what is carried is
    /// lost and the gear wears, as if the player had died, and the result is saved before the game goes. Quitting was cheaper than dying,
    /// and undid a boss encounter's "all in". A crash never comes here: the raid's launch save puts the player back at the portal as they
    /// set out (the grace for a real crash). Returns false when the raid has already ended.
    /// </summary>
    public bool Abandon()
    {
        // Turned back already (saved, on the way home): a quit in those seconds isn't a fall. Before, a guest leaving to the menu then
        // ended the run as a death, worn and emptied, over the turn-back's own save.
        if (turnedBack)
        {
            return false;
        }

        if (!Session.End(RaidOutcome.Died))
        {
            // Fallen and watching friends (co-op): the fall is decided but not yet counted or saved. Quitting then skipped it,
            // and reloading put the player back at the portal with everything.
            return Report == null && CoopSpectate.FinishNow();
        }

        Finish();
        return true;
    }

    private void Finish()
    {
        // The mist lets go first, and the rest that was for this raid ends, so the bag and the body copied for home are the player's own.
        LiftMist();
        HavenReadiness.EndRest(player);
        // Crafts under way give their materials back before the bag is copied for home, as at a launch: one finishing on the way
        // (or refunded when the scene unloads) would land in a bag already copied.
        if (player != null && player.TryGetComponent(out HandCraftQueue crafts))
        {
            crafts.Stop();
        }

        PlayerSnapshot final = player != null ? PlayerSnapshot.Capture(player) : Session.Loadout;
        bool extracted = Session.Outcome == RaidOutcome.Extracted;
        // A failed raid keeps knowledge, the safe pocket and the deployed equipment, worn down (GDD §8.3).
        // An extraction reports what wore on the way (glancing blows chip the axe), so worn gear is seen before it breaks.
        var wear = new List<EquipmentWear>();
        // A boss encounter's failure wears the gear by its own share (the user: 10%), an expedition's by the global one.
        float loss = Session.Definition.IsEncounter ? Session.Definition.Encounter.WearOnFailure : DurabilitySettings.Current.LossOnFailedRaid;
        // Harsh death rules: a fall takes everything, worn gear and the safe pocket too (the user, 2026-11-22).
        PlayerSnapshot keeps = extracted ? final : final?.AfterFailedRaid(Session.Loadout, loss, wear, DeathSettings.Now.raidTakesEverything);
        if (extracted && final != null)
        {
            wear.AddRange(final.WornSince(Session.Loadout));
        }

        Report = RaidReport.Create(Session, Session.Loadout, keeps, final, wear);
        // Those who went home ahead through a stone are among the rescued, however the raid ended.
        foreach (ResidentDefinition early in sentHome)
        {
            if (!Report.Rescued.Contains(early))
            {
                Report.Rescued.Add(early);
            }
        }

        if (PlayerTransport.Current != null)
        {
            PlayerTransport.Current.LeaveRaid(Report, extracted);
        }

        HorseTravel.LeaveRaid(Report, extracted);

        Companions.Settle(Report, extracted);
        // A Lost One that brought the player down takes the most valuable of what the death took; new ones rise at the homecoming.
        LostOnes.Settle(final, keeps);
        ExperienceTracker.SettleRaid(Report, extracted);
        GameSession.EndRaid(Report);
        // Saved again once home; and below, before the results screen (a crash there lost the haul).
        SaveGameService.SaveOnceHome();
        GameEvents.Raise(new GameEvent(GameEventKind.RaidFinished, Session.Definition, 1, null, Session.Outcome, Session.PlayedSeconds));
        GameSession.CarryPlayerState(keeps);
        // Coming home through the portal, or waking at the portal after a failure.
        GameSession.SetArrivalPoint(extracted ? PlayerArrivalPoint.Portal : PlayerArrivalPoint.HomePortal);

        StopPlayer();
        GameMessages.Post(EndLine(extracted), extracted ? MessageKind.Gain : MessageKind.Warning);
        // Started before listeners run, so a faulty one can't leave the player stranded in the raid.
        StartCoroutine(ReturnAfterDelay());
        try
        {
            Ended?.Invoke(Session);
        }
        finally
        {
            // After Ended, which fills in who was rescued (RescuableTraveler), so the journal names them.
            JournalNotices.WriteDown(Report, Report.BroughtDownBy);
        }

        // After Ended: someone rescued has come home and taught what they know, so their workplace answers nothing now.
        // Against one's own Haven (Home): on a raid followed with the host (co-op) the Haven one stands in is still the host's.
        HavenAnswers.Fill(Report, player != null ? player.GetComponent<PlayerCrafting>() : null,
            Resources.Load<ContentDatabase>(ContentDatabase.ResourceName), GameSession.Home);
        // Written now, before the results screen (a crash or a quit there lost the haul), and once all of the raid's end is counted:
        // the raids done (quests that count them, their rewards kept waiting for home), the one rescued made a resident, the
        // homecoming (held quests opened), the journal's record. Saved before those, a crash lost them.
        SaveGameService.SaveAtRaidEnd(keeps);
    }

    private string EndLine(bool extracted)
    {
        if (!Session.Definition.IsEncounter)
        {
            return extracted
                ? "Extracted! Heading back with everything you carry."
                : "The raid is lost. You'll wake at the portal with your gear, but everything else you carried is gone.";
        }

        EncounterArena arena = EncounterArena.Current;
        string boss = Session.Definition.Encounter.BossName;
        if (extracted)
        {
            return arena != null && arena.BossFallen ? $"{boss} has fallen. Heading home with everything you carry." : "You turn back before the mist. Heading home.";
        }

        int worn = Mathf.RoundToInt(Session.Definition.Encounter.WearOnFailure * 100f);
        string where = Session.Outcome == RaidOutcome.TimeExpired ? "The mist closed over you."
            : arena != null && arena.IsSealed ? "You fell beyond the mist." : "You fell before the mist.";
        return $"{where} You'll wake at the portal with your gear, worn {worn}%, but everything else you carried is gone.";
    }

    // The raid is decided; the player only watches the outcome until the scene changes.
    private void StopPlayer()
    {
        if (player == null)
        {
            return;
        }

        if (player.TryGetComponent(out PlayerStateMachine stateMachine))
        {
            stateMachine.EndInteraction();
        }

        PlayerControl.Hold(player, this);

        // The report is written: nothing may change the outcome now. A blow would kill an extracted player under
        // "Extracted!", and an event's choices still open would apply to a raid that has ended.
        if (player.TryGetComponent(out Damageable body))
        {
            body.AddInvulnerability(this);
        }

        if (ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpen)
        {
            ActionMenuWindow.Current.Close();
        }
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSecondsRealtime(returnDelay);

        // The results screen reads the report and sends the player on to the return scene from there.
        if (CanLoad(resultsScene))
        {
            SceneLoader.Load(resultsScene, Report != null && Report.Extracted ? "Counting the haul" : "The raid is lost",
                ifItFails: () => TryAgain(ReturnAfterDelay()));
            yield break;
        }

        // The raid always ends somewhere loadable, so a misconfigured return scene can't strand the player.
        string scene = Session.ReturnScene;
        if (!CanLoad(scene))
        {
            Debug.LogError($"The raid can't return to '{scene}' because it isn't in Build Settings; trying '{fallbackReturnScene}'.", this);
            scene = fallbackReturnScene;
        }

        if (CanLoad(scene))
        {
            SceneLoader.Load(scene, ifItFails: () => TryAgain(ReturnAfterDelay()));
        }
        else if (SceneManager.sceneCountInBuildSettings > 0)
        {
            Debug.LogError($"The fallback return scene '{fallbackReturnScene}' isn't in Build Settings either; loading the first scene in the build.", this);
            SceneLoader.Load(0, ifItFails: () => TryAgain(ReturnAfterDelay()));
        }
        else
        {
            Debug.LogError("The raid has nowhere to return to: Build Settings list no scenes.", this);
        }
    }

    private static bool CanLoad(string scene)
    {
        return !string.IsNullOrEmpty(scene) && Application.CanStreamedLevelBeLoaded(scene);
    }

    // The raid's enemy multipliers times the current stage's bonus and what the hour adds: the raid's rules right now,
    // applied by Creatures and kept up with by Companions.
    /// <summary>The raid's enemy health multiplier right now.</summary>
    public float HealthMultiplier => Parameters.EnemyHealthMultiplier * (1f + (CurrentStage?.EnemyHealthBonus ?? 0f)) * NightSettings.Current.HealthAt(NightSettings.Darkness);
    /// <summary>The raid's enemy damage multiplier right now.</summary>
    public float PowerMultiplier => Parameters.EnemyPowerMultiplier * (1f + (CurrentStage?.EnemyPowerBonus ?? 0f)) * NightSettings.Current.PowerAt(NightSettings.Darkness);

    internal RaidGenerator Generator => generator;
    internal Damageable PlayerHealth => playerHealth;

    private RaidSession StartTestRaid()
    {
        if (fallbackRaid == null)
        {
            return null;
        }

        RaidModifierDefinition[] none = Array.Empty<RaidModifierDefinition>();
        var session = new RaidSession(fallbackRaid, none, RaidParameters.Calculate(fallbackRaid, none),
            player != null ? PlayerSnapshot.Capture(player) : null, fallbackReturnScene);
        GameSession.BeginRaid(session);
        return session;
    }
}
