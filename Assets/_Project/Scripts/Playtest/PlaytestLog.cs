using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>One row of a run's log.</summary>
public struct PlaytestEntry
{
    /// <summary>Played time in the run when it happened, excluding pauses.</summary>
    public float PlayedSeconds;
    /// <summary>Real time, UTC, ISO 8601.</summary>
    public string Utc;
    public int Session;
    public string Scene;
    /// <summary>A GameEventKind name, or one of the logger's own: SessionStart, SessionEnd, SceneEntered, PlayerDied.</summary>
    public string Event;
    /// <summary>The asset (quest, monster, building, raid...) or scene name; empty when there is none.</summary>
    public string Subject;
    public int Amount;
    /// <summary>Extra context, such as a raid's outcome.</summary>
    public string Detail;
}

/// <summary>A run's log reduced to what the Run Comparison window shows.</summary>
public sealed class PlaytestRunSummary
{
    public string RunId;
    public int Sessions;
    public float PlayedMinutes;
    public int Deaths;
    public int Raids;
    public int Extractions;
    /// <summary>Played minutes at which each milestone was first reached, in the settings' order; null when it wasn't.</summary>
    public float?[] MilestoneMinutes;
}

/// <summary>
/// The playtest log file: one CSV per run in Playtests next to the saves, readable in any spreadsheet. Writing is done
/// by PlaytestLogger; reading and summarising by the Run Comparison window. Add columns only at the end.
/// </summary>
public static class PlaytestLog
{
    public const string Header = "played_min,utc,session,scene,event,subject,amount,detail";
    public const string SessionStart = "SessionStart";
    public const string SessionEnd = "SessionEnd";
    public const string SceneEntered = "SceneEntered";
    public const string PlayerDied = "PlayerDied";

    public static string Folder => Path.Combine(Application.persistentDataPath, "Playtests");

    public static string PathFor(string runId) => Path.Combine(Folder, runId + ".csv");

    /// <summary>Every event name the log can contain, for checking milestones.</summary>
    public static IEnumerable<string> KnownEvents()
    {
        foreach (string kind in Enum.GetNames(typeof(GameEventKind)))
        {
            yield return kind;
        }

        yield return SessionStart;
        yield return SessionEnd;
        yield return SceneEntered;
        yield return PlayerDied;
    }

    public static string Format(PlaytestEntry entry)
    {
        var line = new StringBuilder();
        line.Append((entry.PlayedSeconds / 60f).ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
        line.Append(Escape(entry.Utc)).Append(',');
        line.Append(entry.Session.ToString(CultureInfo.InvariantCulture)).Append(',');
        line.Append(Escape(entry.Scene)).Append(',');
        line.Append(Escape(entry.Event)).Append(',');
        line.Append(Escape(entry.Subject)).Append(',');
        line.Append(entry.Amount.ToString(CultureInfo.InvariantCulture)).Append(',');
        line.Append(Escape(entry.Detail));
        return line.ToString();
    }

    /// <summary>Reads a row written by Format. False for the header and for lines that aren't rows.</summary>
    public static bool TryParse(string line, out PlaytestEntry entry)
    {
        entry = default;
        List<string> fields = Split(line);
        if (fields.Count < 8 || !float.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float minutes))
        {
            return false;
        }

        int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out entry.Session);
        int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out entry.Amount);
        entry.PlayedSeconds = minutes * 60f;
        entry.Utc = fields[1];
        entry.Scene = fields[3];
        entry.Event = fields[4];
        entry.Subject = fields[5];
        entry.Detail = fields[7];
        return true;
    }

    /// <summary>Reads a run's file; it may still be open for writing.</summary>
    public static List<PlaytestEntry> Read(string path)
    {
        var entries = new List<PlaytestEntry>();
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(stream))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (TryParse(line, out PlaytestEntry entry))
                {
                    entries.Add(entry);
                }
            }
        }

        return entries;
    }

    public static PlaytestRunSummary Summarise(string runId, IReadOnlyList<PlaytestEntry> entries, IReadOnlyList<PlaytestMilestone> milestones)
    {
        var summary = new PlaytestRunSummary { RunId = runId, MilestoneMinutes = new float?[milestones.Count] };
        foreach (PlaytestEntry entry in entries)
        {
            float minutes = entry.PlayedSeconds / 60f;
            summary.Sessions = Mathf.Max(summary.Sessions, entry.Session);
            summary.PlayedMinutes = Mathf.Max(summary.PlayedMinutes, minutes);
            if (entry.Event == PlayerDied)
            {
                summary.Deaths++;
            }
            else if (entry.Event == nameof(GameEventKind.RaidFinished))
            {
                summary.Raids++;
                if (entry.Detail == nameof(RaidOutcome.Extracted))
                {
                    summary.Extractions++;
                }
            }

            for (int i = 0; i < milestones.Count; i++)
            {
                if (summary.MilestoneMinutes[i] == null && milestones[i] != null && milestones[i].Matches(entry))
                {
                    summary.MilestoneMinutes[i] = minutes;
                }
            }
        }

        return summary;
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static List<string> Split(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        fields.Add(field.ToString());
        return fields;
    }
}
