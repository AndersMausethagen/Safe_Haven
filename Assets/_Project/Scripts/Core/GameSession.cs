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
    private static SettlementState settlement;
    private static PlaytestRun playtest;
    private static string pendingArrivalPoint;
    private static PlayerSnapshot pendingPlayerState;
    private static PlayerPose? pendingPose;
    private static RaidReport pendingReport;

    /// <summary>The raid being played, or null outside raids.</summary>
    public static RaidSession ActiveRaid { get; private set; }

    /// <summary>Belongings kept safe in Safe Haven. Created on first use.</summary>
    public static StashStorage Stash => stash ??= new StashStorage();

    /// <summary>What has changed in persistent scenes: dead enemies, damaged buildings, picked nodes. Created on first use.</summary>
    public static WorldState World => world ??= new WorldState();

    /// <summary>The player's quests and their progress. Created on first use.</summary>
    public static QuestLog Quests => quests ??= new QuestLog();

    /// <summary>Safe Haven as a place: portal, ward, residents, breach, purification. Created on first use.</summary>
    public static SettlementState Settlement => settlement ??= new SettlementState();

    /// <summary>The playthrough being logged for playtests. A new game starts a new run; a load continues the saved one.</summary>
    public static PlaytestRun Playtest => playtest ??= PlaytestRun.StartNew();

    /// <summary>
    /// Asks the next scene to place the player at the PlayerArrivalPoint with this id (such as "HeartTree" after a failed
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
    }

    /// <summary>Closes the active raid and keeps its report until Safe Haven shows it.</summary>
    public static void EndRaid(RaidReport report)
    {
        ActiveRaid = null;
        pendingReport = report;
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
    public static void StartNewGame()
    {
        Reset();
    }

    /// <summary>Replaces everything with a loaded game. The player takes it on arriving in the next scene.</summary>
    public static void LoadGame(LoadedGame game)
    {
        Reset();
        stash = game.Stash;
        world = game.World;
        quests = game.Quests;
        settlement = game.Settlement;
        playtest = game.Playtest;
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
        settlement = null;
        playtest = null;
        pendingArrivalPoint = null;
        pendingPlayerState = null;
        pendingPose = null;
        pendingReport = null;
        ActiveRaid = null;
    }
}
