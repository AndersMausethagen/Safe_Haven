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
    /// <summary>Raids played and extracted at each tier, by tier number.</summary>
    public readonly int[] TierRaids = new int[Tiers.Max + 1];
    public readonly int[] TierExtractions = new int[Tiers.Max + 1];
    /// <summary>Sum, per tier, of how far above the recommended gear score the player went in, to read off an average.</summary>
    public readonly int[] TierScoreMargin = new int[Tiers.Max + 1];
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
    /// <summary>The tier a raid was played at and the gear taken in. Written just before its RaidFinished row.</summary>
    public const string RaidTier = "RaidTier";
    public const string SessionStart = "SessionStart";
    public const string Cheat = "Cheat";
    public const string Assist = "Assist";
    public const string SessionEnd = "SessionEnd";
    public const string SceneEntered = "SceneEntered";
    public const string PlayerDied = "PlayerDied";
    /// <summary>A skill reached a level: subject the skill's id, amount the level, detail "insight" when Insight raised it.</summary>
    public const string SkillLevel = "SkillLevel";
    /// <summary>Insight put into a skill: subject the skill's id, amount the Insight.</summary>
    public const string InsightSpent = "InsightSpent";
    /// <summary>Experience a skill gained by practice in the last played minute: subject its id, amount the experience.</summary>
    public const string SkillXp = "SkillXp";
    /// <summary>How the player fought in the last played minute: subject the technique (see <see cref="CombatTally"/>), amount how often,
    /// detail the weapon held. What the parry/aggression balance is measured from (Docs/CombatResearch.md §3.3 #10).</summary>
    public const string Combat = "Combat";
    /// <summary>The player's kit as a raid starts and ends (Docs/PlayerStatsResearch.md #3): health, armour, damage share, toughness, boons.</summary>
    public const string Kit = "Kit";
    /// <summary>A blow that took at least 5% of the player's health: what struck, how hard, with what.</summary>
    public const string Hurt = "Hurt";

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
        int tier = -1;
        int margin = 0;
        foreach (PlaytestEntry entry in entries)
        {
            float minutes = entry.PlayedSeconds / 60f;
            summary.Sessions = Mathf.Max(summary.Sessions, entry.Session);
            summary.PlayedMinutes = Mathf.Max(summary.PlayedMinutes, minutes);
            if (entry.Event == PlayerDied)
            {
                summary.Deaths++;
            }
            else if (entry.Event == RaidTier)
            {
                tier = entry.Amount;
                margin = ReadMargin(entry.Detail);
            }
            else if (entry.Event == nameof(GameEventKind.RaidFinished))
            {
                summary.Raids++;
                bool extracted = entry.Detail == nameof(RaidOutcome.Extracted);
                if (extracted)
                {
                    summary.Extractions++;
                }

                if (tier >= 0 && tier <= Tiers.Max)
                {
                    summary.TierRaids[tier]++;
                    summary.TierExtractions[tier] += extracted ? 1 : 0;
                    summary.TierScoreMargin[tier] += margin;
                }

                tier = -1;
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

    /// <summary>The Detail of a RaidTier row, which goes just before its RaidFinished row.</summary>
    /// <summary>
    /// Played minutes at which each skill first reached each level by practice (levels Insight raised are left out), by
    /// skill id. A run with cheats should not be passed in.
    /// </summary>
    public static Dictionary<string, SortedDictionary<int, float>> SkillPace(IReadOnlyList<PlaytestEntry> entries)
    {
        var pace = new Dictionary<string, SortedDictionary<int, float>>();
        foreach (PlaytestEntry entry in entries)
        {
            if (entry.Event != SkillLevel || entry.Detail == "insight" || string.IsNullOrEmpty(entry.Subject))
            {
                continue;
            }

            if (!pace.TryGetValue(entry.Subject, out SortedDictionary<int, float> levels))
            {
                levels = new SortedDictionary<int, float>();
                pace[entry.Subject] = levels;
            }

            if (!levels.ContainsKey(entry.Amount))
            {
                levels[entry.Amount] = entry.PlayedSeconds / 60f;
            }
        }

        return pace;
    }

    /// <summary>
    /// Experience each skill gained by practice per played hour over a run (the SkillXp rows over the run's played time),
    /// by skill id; skills not practised are left out.
    /// </summary>
    public static Dictionary<string, float> SkillRate(IReadOnlyList<PlaytestEntry> entries)
    {
        var gained = new Dictionary<string, float>();
        float played = 0f;
        foreach (PlaytestEntry entry in entries)
        {
            played = Math.Max(played, entry.PlayedSeconds);
            if (entry.Event == SkillXp && !string.IsNullOrEmpty(entry.Subject) && entry.Amount > 0)
            {
                gained.TryGetValue(entry.Subject, out float sum);
                gained[entry.Subject] = sum + entry.Amount;
            }
        }

        var rate = new Dictionary<string, float>();
        if (played <= 60f)
        {
            return rate;
        }

        foreach (KeyValuePair<string, float> skill in gained)
        {
            rate[skill.Key] = skill.Value / (played / 3600f);
        }

        return rate;
    }

    public static string TierDetail(int gearScore, int recommended, string hour) => $"gear {gearScore}, recommended {recommended}, {hour}";

    private static int ReadMargin(string detail)
    {
        string[] parts = detail != null ? detail.Split(' ', ',') : new string[0];
        int gear = 0, recommended = 0;
        for (int i = 0; i + 1 < parts.Length; i++)
        {
            if (parts[i] == "gear")
            {
                int.TryParse(parts[i + 1], out gear);
            }
            else if (parts[i] == "recommended")
            {
                int.TryParse(parts[i + 1], out recommended);
            }
        }

        return gear - recommended;
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
