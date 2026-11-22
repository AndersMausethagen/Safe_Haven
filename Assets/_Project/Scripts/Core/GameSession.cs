using UnityEngine;

/// <summary>
/// The in-memory game state that outlives scene loads: the player's state handed from the scene being left to the next
/// one (and where they should stand), the stash kept in Safe Haven, the world state of persistent scenes, the quest log,
/// the settlement (portal, ward, residents), the raid in progress and the report of the last raid. It is plain data with no scene objects, so scenes come and go
/// around it. Raids write to it (RaidLauncher, RaidDirector), the player and Safe Haven read from it, and the save system
/// fills it when a game is started or loaded. It is never written to disk directly: SaveGameService copies what is worth
/// keeping into a save file through SaveMapper.
/// <para>
/// Extending: add state here only if it must survive a scene change and belongs to the current game (it is wiped by New
/// Game and replaced by Load). Keep the state itself in its own class (like QuestLog or StashStorage) and only hold it
/// here, so this class stays a container. Player preferences such as volume belong in PlayerPrefs instead.
/// </para>
/// </summary>
public static class GameSession
{
    private static StashStorage stash;
    private static WorldState world;
    private static QuestLog quests;
    private static CharacterProgress progress;
    private static SettlementState settlement;
    private static PlaytestRun playtest;
    private static CharacterAppearance appearance;
    private static string pendingArrivalPoint;
    private static PlayerSnapshot pendingPlayerState;
    private static PlayerPose? pendingPose;
    private static RaidReport pendingReport;
    private static CarriedTransport? pendingTransport;

    /// <summary>The raid being played, or null outside raids.</summary>
    public static RaidSession ActiveRaid { get; private set; }

    /// <summary>Belongings kept safe in Safe Haven. Created on first use.</summary>
    public static StashStorage Stash => stash ??= new StashStorage();

    /// <summary>What has changed in persistent scenes: dead enemies, damaged buildings, picked nodes. Created on first use.</summary>
    public static WorldState World => world ??= new WorldState();

    /// <summary>The player's quests and their progress. Created on first use.</summary>
    public static QuestLog Quests => quests ??= new QuestLog();

    /// <summary>The player's level, experience and talents: permanent, kept across scenes and saved.</summary>
    public static CharacterProgress Progress => progress ??= new CharacterProgress();

    /// <summary>Safe Haven as a place: portal, ward, residents, breach, purification. Created on first use.</summary>
    /// <summary>The settlement by its old name, which meant two Havens in a friend's: choose <see cref="Here"/> or <see cref="Home"/>.</summary>
    [System.Obsolete("Choose GameSession.Here (the Haven one stands in, to read) or GameSession.Home (one's own, to change): Docs/BugPatterns.md, guard 9.")]
    public static IHavenView Settlement => Here;

    /// <summary>
    /// The Haven the player stands in, to read: their own, or in a friend's (co-op) the copy of the host's, which the host's word
    /// keeps (SettlementState.TakeOn) and a guest never changes (Docs/BugPatterns.md, guard 9). Read-only by type: change one's
    /// own through <see cref="Home"/>.
    /// </summary>
    public static IHavenView Here => HereItself;

    /// <summary>
    /// The settlement object behind <see cref="Here"/>, for the few that must hold it: the co-op mirror (HavenSync), saving, and
    /// what this game sees standing (HavenProgress, a station's queue, the territory's state). Never to change the host's world
    /// in a friend's Haven; ConventionTests allows it only in the files it lists.
    /// </summary>
    public static SettlementState HereItself => settlement ??= new SettlementState();

    /// <summary>
    /// The player's own Haven, always theirs to change: the one they stand in at home, and in a friend's their own, kept alive for
    /// the visit (HavenVisit.OwnHaven) and saved with them. Their extraction, the person they rescued, the boss they beat, their
    /// companion lost: here, in one call, wherever they are.
    /// </summary>
    public static SettlementState Home => HavenVisit.OwnHaven ?? HereItself;

    /// <summary>The playthrough being logged for playtests. A new game starts a new run; a load continues the saved one.</summary>
    public static PlaytestRun Playtest => playtest ??= PlaytestRun.StartNew();

    /// <summary>How the player looks and what they are called, chosen when the game was made. Saved with the game.</summary>
    public static CharacterAppearance Appearance
    {
        get => appearance ??= CharacterCatalog.Current.StartingLook;
        set => appearance = value != null ? value.Clone() : null;
    }

    /// <summary>
    /// Asks the next scene to place the player at the PlayerArrivalPoint with this id (such as "HomePortal" after a failed
    /// raid, "Portal" after an extraction). A saved pose, when loading, takes precedence.
    /// </summary>
    public static void SetArrivalPoint(string id)
    {
        pendingArrivalPoint = id;
    }

    /// <summary>Returns the requested arrival point once, or null.</summary>
    public static string TakeArrivalPoint()
    {
        string id = pendingArrivalPoint;
        pendingArrivalPoint = null;
        return id;
    }

    /// <summary>A cart on its way between scenes: what it is and its saved cargo.</summary>
    public readonly struct CarriedTransport
    {
        public readonly TransportDefinition Definition;
        public readonly string State;

        public CarriedTransport(TransportDefinition definition, string state)
        {
            Definition = definition;
            State = state;
        }
    }

    /// <summary>Hands a cart to the next scene; the player there takes it up on arrival (PlayerTransport).</summary>
    public static void CarryTransport(CarriedTransport carried) => pendingTransport = carried;

    /// <summary>The cart on its way to the next scene, without taking it (a save written before it is set down). False with none.</summary>
    public static bool PeekTransport(out CarriedTransport carried)
    {
        carried = pendingTransport ?? default;
        return pendingTransport.HasValue;
    }

    /// <summary>Returns the cart handed over by the previous scene, once.</summary>
    public static bool TakeCarriedTransport(out CarriedTransport carried)
    {
        carried = pendingTransport ?? default;
        bool any = pendingTransport.HasValue;
        pendingTransport = null;
        return any;
    }

    /// <summary>True while a report of the last raid is waiting to be shown.</summary>
    public static bool HasPendingReport => pendingReport != null;

    /// <summary>Hands the player's state to the next scene; the player there takes it on arrival (see PlayerPersistence).</summary>
    public static void CarryPlayerState(PlayerSnapshot state)
    {
        pendingPlayerState = state;
    }

    /// <summary>Returns the state handed over by the previous scene, once. Null when there is none.</summary>
    public static PlayerSnapshot TakePlayerState()
    {
        PlayerSnapshot state = pendingPlayerState;
        pendingPlayerState = null;
        return state;
    }

    public static void BeginRaid(RaidSession raid)
    {
        ActiveRaid = raid;
        pendingReport = null;
        RecentBlows.Clear();
    }

    /// <summary>Closes the active raid and keeps its report until Safe Haven shows it.</summary>
    public static void EndRaid(RaidReport report)
    {
        ActiveRaid = null;
        string advice = null;
        report.BroughtDownBy = report.Outcome == RaidOutcome.Died ? CreatureRecords.TakeKillerName(out advice) : null;
        report.KillerAdvice = advice;
        report.LastBlows = report.Outcome == RaidOutcome.Died ? RecentBlows.Take() : new System.Collections.Generic.List<string>();
        RecentBlows.Clear();
        if (report.Outcome == RaidOutcome.Died && Progress != null)
        {
            // Counted for Damage Taken › Grows With Falls.
            Progress.NoteFall();
        }
        pendingReport = report;
    }

    /// <summary>
    /// A raid left without an ending (quit to the menu): it is simply over, with no report. What the player carried in it
    /// is gone with the scene, and the game goes on from its last save.
    /// </summary>
    public static void AbandonRaid(RaidSession raid)
    {
        if (raid != null && ActiveRaid == raid)
        {
            ActiveRaid = null;
        }
    }

    /// <summary>Returns the last raid's report, once. Null when there is none.</summary>
    public static RaidReport TakeReport()
    {
        RaidReport report = pendingReport;
        pendingReport = null;
        return report;
    }

    /// <summary>Returns where the player should appear in the next scene, once. False when they keep the scene's spawn.</summary>
    public static bool TakeSpawnPose(out PlayerPose pose)
    {
        pose = pendingPose.GetValueOrDefault();
        bool has = pendingPose.HasValue;
        pendingPose = null;
        return has;
    }

    /// <summary>Forgets everything: the next scene starts with the player's authored starting contents and an empty stash.</summary>
    public static void StartNewGame(CharacterAppearance look = null)
    {
        Reset();
        Tutorials.Clear();
        Appearance = look;
    }

    /// <summary>Replaces everything with a loaded game. The player takes it on arriving in the next scene.</summary>
    public static void LoadGame(LoadedGame game)
    {
        Reset();
        // Tutorials waiting belong to the game they opened in.
        Tutorials.Clear();
        stash = game.Stash;
        world = game.World;
        quests = game.Quests;
        progress = game.Progress;
        settlement = game.Settlement;
        playtest = game.Playtest;
        Appearance = game.Appearance;
        pendingPlayerState = game.Player;
        pendingPose = game.Pose;
    }

    // Entering play mode doesn't reload scripts in this project (Enter Play Mode Options), so statics are reset here.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        stash = null;
        world = null;
        quests = null;
        progress = null;
        settlement = null;
        playtest = null;
        appearance = null;
        pendingArrivalPoint = null;
        pendingPlayerState = null;
        pendingPose = null;
        pendingReport = null;
        pendingTransport = null;
        ActiveRaid = null;
        // What other systems hold of the game being played goes with it: quitting to the menu just after a raid's end and
        // loading another save posted the old run's territory news in the loaded Haven.
        // Every ForgetGame in the game is called here (SharedStateTests): a system holding something of the game being played,
        // kept in a static that outlives a scene, writes one and adds it below. A static is not reset by quitting to the menu: only
        // starting the program again (or Play in the Editor) does that. These run on every load, co-op's TryPlay too (into a friend's
        // Haven and home again): what must outlive a visit doesn't belong here (the rowing hint came back after every visit).
        global::Territory.ForgetGame();
        CreatureRecords.ForgetGame();
        CorruptionNights.ForgetGame();
        QuestTimers.ForgetGame();
        ResidentBuilders.ForgetGame();
        Homecoming.ForgetGame();
        WorkerProduction.ForgetGame();
        RingWork.ForgetGame();
    }
}
