using System;

/// <summary>
/// One playthrough being measured, from New Game (or entering Play without loading) onwards. Its id names the run's log
/// file and is kept in the save file, so loading a save continues the same run. Each launch, Play-mode entry or load
/// within the run is a session. Held by GameSession, advanced by PlaytestLogger.
/// </summary>
public sealed class PlaytestRun
{
    public PlaytestRun(string id, float playedSeconds = 0f, int sessions = 0)
    {
        Id = id;
        PlayedSeconds = playedSeconds;
        Sessions = sessions;
    }

    /// <summary>Unique and sortable: the local start time, such as 20260514-193012.</summary>
    public string Id { get; }

    /// <summary>Unpaused time played in this run, carried across saves and loads.</summary>
    public float PlayedSeconds { get; set; }

    /// <summary>How many sessions the run has had so far.</summary>
    public int Sessions { get; set; }

    public static PlaytestRun StartNew() => new PlaytestRun(DateTime.Now.ToString("yyyyMMdd-HHmmss"));
}
