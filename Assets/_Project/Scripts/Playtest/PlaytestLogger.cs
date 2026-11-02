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
        }
    }

    public void OnSceneEntered(Scene scene)
    {
        Write(PlaytestLog.SceneEntered, scene.name);
    }

    private void Update()
    {
        if (!logging)
        {
            return;
        }

        PlaytestRun current = EnsureRun();
        if (!GamePause.IsPaused)
        {
            current.PlayedSeconds += Time.unscaledDeltaTime;
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
        EndSession();
    }

    private static string NameOf(UnityEngine.Object subject) => subject != null ? subject.name : "";
}
