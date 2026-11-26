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
    /// <summary>Where the player stands in a raid, every few seconds (subject: x;z in metres; detail: the raid): for heatmaps of where
    /// players really go (Docs/WorldDesignReview_2026-11-17.md).</summary>
    public const string Position = "Position";
    /// <summary>A named place found (subject its id, amount metres from the portal, detail its region).</summary>
    public const string PlaceFound = "PlaceFound";
    /// <summary>A region of the Haven's land walked into (subject its name).</summary>
    public const string RegionEntered = "RegionEntered";
    /// <summary>A lookout looked out from (subject its place, amount the places it marked, detail first or again).</summary>
    public const string LookoutUsed = "LookoutUsed";
    /// <summary>A thing left at a place taken (subject the place, amount how many, detail the item).</summary>
    public const string PlaceFindTaken = "PlaceFindTaken";
    /// <summary>A stage revealed a cache (subject Glint or Hoard, amount metres from the player, detail the stage).</summary>
    public const string StageReveal = "StageReveal";
    /// <summary>A creature's group behaviour (subject what: BreakAway, Rout, Flank, Alarm, SearchPoint, Intensity.Peak, Intensity.Relax;
    /// detail the creature or the stage).</summary>
    public const string EnemyAI = "EnemyAI";
    public const string SceneEntered = "SceneEntered";
    public const string PlayerDied = "PlayerDied";
    /// <summary>Right after PlayerDied: the last DeathRecap.Seconds before it (subject the killer, amount the health lost in them, detail the
    /// blows, small ones and ticks among them, who dealt what, the health they started at, and the crowd at the end).</summary>
    public const string DeathRecap = "DeathRecap";
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
    /// <summary>A creature died (KillLedger): subject the monster, amount the player's blows on it, detail who made the kill, the player's
    /// damage, everyone else's, its health and how long the fight took.</summary>
    public const string Kill = "Kill";
    /// <summary>A playtester wrote a report (F8): subject its kind (bug, idea, confusing, liked), detail the report's id (Reports/&lt;id&gt;.json).</summary>
    public const string Report = "Report";
    // The economy in numbers (Docs/EconomyAndPhases_2026-11-22.md §4.5, measured before anything early is changed):
    /// <summary>Goods that came home from a source not the player's own hands: subject the source (Tithe, Party:Hunt, Gift, FellAway,
    /// QuarryAway, Farm, Dock, Beehive), amount how many, detail the item's id.</summary>
    public const string CameHome = "CameHome";
    /// <summary>The Haven's breakfast at dawn (ResidentMeals): subject the food's id, amount how many were eaten, detail "hungry N".</summary>
    public const string Eaten = "Eaten";
    /// <summary>A stay at home ended (a raid began): subject what the player was doing (build, craft, gather, walk, idle, window, keeper),
    /// amount the seconds spent at it.</summary>
    public const string HomeStay = "HomeStay";
    /// <summary>The chests on leaving for a raid: subject an item's id (the Keeper's view's stores), amount how many.</summary>
    public const string Stock = "Stock";
    /// <summary>The gathering nodes standing a few seconds into a raid: subject the node's id, amount how many.</summary>
    public const string RaidNodes = "RaidNodes";
    // People (Docs/PeopleSystemReview_2026-11-23.md §2.9), for tuning their pace and spirits:
    /// <summary>A resident rose a level: subject their id, amount the new level, detail the skill's id.</summary>
    public const string PersonLevel = "PersonLevel";
    /// <summary>A resident's job skill reached a new rank: subject their id, amount the level, detail the rank's name.</summary>
    public const string PersonRank = "PersonRank";
    /// <summary>A resident woke inspired: subject their id, detail the kind.</summary>
    public const string PersonInspired = "PersonInspired";
    /// <summary>A mark came (amount 1) or was healed (-1): subject their id, detail the mark's id.</summary>
    public const string PersonMark = "PersonMark";
    /// <summary>Two residents' standing changed level: subject "idA|idB", amount the new level (-2..2), detail why.</summary>
    public const string Standing = "Standing";
    /// <summary>The player grew a passion: subject their id, amount the passion (1 Keen, 2 Burning), detail the skill's id.</summary>
    public const string PassionPick = "PassionPick";
    /// <summary>A people's morning: subject "haven", amount how many were Bright, detail "low N steady N".</summary>
    public const string Spirits = "Spirits";
    /// <summary>A resident's bond with the player grew: subject their id, amount the bond in hundredths, detail why.</summary>
    public const string Bond = "Bond";
    // Why players leave a raid (Docs/RaidLoopResearch_2026-11-22.md #1):
    /// <summary>A danger stage began: subject the stage, amount the seconds left on the clock, detail "exit N m" (to the nearest open one).</summary>
    public const string StageBegan = "StageBegan";
    /// <summary>An exit was lit, or a portal stepped through in overtime: subject the exit, amount the seconds left (overtime: minus the
    /// seconds past the clock), detail the stage then ("portal" in overtime).</summary>
    public const string ExitLit = "ExitLit";
    /// <summary>A stage's revealed cache opened: subject the cache, amount the seconds left.</summary>
    public const string RevealOpened = "RevealOpened";
    /// <summary>A raid event's choice: subject the event, amount which choice (1 the first), detail its label.</summary>
    public const string EventChoice = "EventChoice";
    /// <summary>The bag as a raid ended: subject "bag", amount its weight in percent of the most, detail "slots N%".</summary>
    public const string BagAtEnd = "BagAtEnd";
    /// <summary>A raid's nodes, as it ended: subject the node's id, amount how many the player gathered, detail "standing N".</summary>
    public const string NodesMined = "NodesMined";
    /// <summary>The player came upon a place of a generated raid map for the first time (Docs/RaidLayoutAnalysis_2026-11-26.md R9):
    /// subject the place (its kind, "Kind: Group" where groups share the kind, a set piece's or landmark's name), amount the raid's
    /// seconds so far, detail "N m from the start".</summary>
    public const string CameUpon = "CameUpon";
    /// <summary>The player first came within 10 m of a raid event's site (Docs/RaidEventsPlan_2026-11-26.md E1): subject the event,
    /// amount the raid's seconds.</summary>
    public const string EventReached = "EventReached";
    /// <summary>A raid event's site still open as the raid ended: subject the event, amount 1 if the player reached it (and chose
    /// nothing) or 0 if never, detail "began N s, M m off".</summary>
    public const string EventSkipped = "EventSkipped";
    // Boss fights (Docs/HeartrootPlaytest_2026-11-23.md P0: the first hand try left no trace of the boss's side):
    /// <summary>A boss fight ended: subject the boss's id, amount its health left in percent, detail the outcome (won, died, left), the
    /// seconds from its waking, the stage reached, topples by cause, heart strikes, interrupts, calls and bound summons killed, the
    /// player's damage to it by kind of blow, draughts drunk and what struck the last blow on the player.</summary>
    public const string BossTry = "BossTry";
    /// <summary>A moment in a boss fight: subject what (wake, stage, topple), amount its health in percent, detail the seconds since it
    /// woke and, for a topple, its cause and length.</summary>
    public const string Boss = "Boss";
    /// <summary>Someone stranded: subject the resident's id, amount the seconds left, detail freed, sent home, extracted or fell.</summary>
    public const string Rescue = "Rescue";
    /// <summary>The playtester's one tap on the results screen (RaidResultsScreen, playtests only): subject the reason.</summary>
    public const string WhyHome = "WhyHome";
    // The death rules (Docs/DeathPenaltyResearch_2026-11-22.md):
    /// <summary>A fall at home: subject the rules (Gentle, Normal, Harsh), amount the stacks left in the cairn, detail lessons and meals.</summary>
    public const string HomeDeath = "HomeDeath";
    /// <summary>A cairn emptied: subject how many stacks it held, amount the seconds since the fall, detail the lessons given back.</summary>
    public const string CairnRecovered = "CairnRecovered";
    /// <summary>Lessons a fall took: subject "skill=amount;...", amount the total.</summary>
    public const string LessonsLost = "LessonsLost";
    /// <summary>A fall in a ward breach: subject the breach, amount which fall in it, detail the ward's strength after.</summary>
    public const string BreachFall = "BreachFall";
    /// <summary>A lost breach's plunder: subject Taken or Recovered, amount how many, detail what.</summary>
    public const string Plundered = "Plundered";
    /// <summary>The death rules changed (the options): subject the new ones.</summary>
    public const string DeathRules = "DeathRules";
    // How the game ran (Docs/QualityLevers_2026-11-24.md §2):
    /// <summary>A played minute's frames (fewer at a change of place; paused, unfocused and loading frames left out): subject the place (a
    /// raid's id, home, or the scene), amount the 95th percentile frame in ms, detail percentiles, worst, hitches over 50 ms, frames and
    /// the CPU main thread / GPU averages where reported (FrameTally).</summary>
    public const string Perf = "Perf";
    /// <summary>What the game runs on, once a session and when the display settings change: subject the GPU, amount its memory in MB,
    /// detail the screen, quality, vsync, frame cap, graphics API, CPU, RAM and OS.</summary>
    public const string Machine = "Machine";
    /// <summary>Loot left on a creature's body (BodyLootSettings): subject its name, amount the stacks and resources it still held, detail
    /// "faded" (its time ran out) or "left" (the scene ended first).</summary>
    public const string LootLeft = "LootLeft";

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
