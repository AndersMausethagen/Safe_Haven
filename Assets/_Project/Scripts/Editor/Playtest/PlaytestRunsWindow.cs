using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Compares playtest runs side by side: one row per run log (newest first) with its sessions, played time, deaths, raids
/// and the played minute each milestone in PlaytestSettings was reached. Green is on target, orange up to 25% late, red
/// later. Runs are the CSVs PlaytestLogger writes; open them in a spreadsheet for the full timeline.
/// </summary>
public sealed class PlaytestRunsWindow : EditorWindow
{
    private const float RunWidth = 120f;
    private const float StatWidth = 64f;
    private const float MilestoneWidth = 92f;

    private readonly List<PlaytestRunSummary> runs = new List<PlaytestRunSummary>();
    // How the abilities were used over the runs without cheats (Docs/SkillChoiceResearch_2026-11-27.md 3.7).
    private PlaytestSkillUse skillUse = new PlaytestSkillUse();
    private int cheatedRuns;
    private Vector2 scroll;
    private string problem;

    [MenuItem("Safe Haven/Playtest Runs", false, 40)]
    public static void Open()
    {
        GetWindow<PlaytestRunsWindow>("Playtest Runs").Refresh();
    }

    private void OnEnable() => Refresh();

    private void Refresh()
    {
        runs.Clear();
        problem = null;
        if (!Directory.Exists(PlaytestLog.Folder))
        {
            return;
        }

        IReadOnlyList<PlaytestMilestone> milestones = PlaytestSettings.Current.Milestones;
        var files = new List<string>(Directory.GetFiles(PlaytestLog.Folder, "*.csv"));
        files.Sort((a, b) => string.CompareOrdinal(b, a));
        var clean = new List<IReadOnlyList<PlaytestEntry>>();
        cheatedRuns = 0;
        foreach (string file in files)
        {
            try
            {
                List<PlaytestEntry> entries = PlaytestLog.Read(file);
                runs.Add(PlaytestLog.Summarise(Path.GetFileNameWithoutExtension(file), entries, milestones));
                if (PlaytestLog.Cheated(entries))
                {
                    cheatedRuns++;
                }
                else
                {
                    clean.Add(entries);
                }
            }
            catch (IOException exception)
            {
                problem = exception.Message;
            }
        }

        skillUse = PlaytestLog.SkillUse(clean);
        // Every ability has a row, so one never slotted shows as hardly ever chosen; the weapon art's entries are left out (slot 1 is
        // theirs on every bar, and an art's casts are counted as swings, not skills).
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition"))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill == null)
            {
                continue;
            }

            if (skill.Kind == SkillKind.Art)
            {
                skillUse.Skills.Remove(skill.Id);
            }
            else
            {
                skillUse.Of(skill.Id);
            }
        }
    }

    // Each ability over the runs without cheats: on how many bars, how often cast in a fight, its share of skill damage, and a flag when
    // it is no real choice (a default, or never picked).
    private void DrawSkills()
    {
        if (skillUse.Skills.Count == 0)
        {
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Abilities: {skillUse.Loadouts} raids begun, {skillUse.FightingMinutes} fighting minutes"
            + (cheatedRuns > 0 ? $" ({cheatedRuns} runs with cheats left out)" : ""), EditorStyles.boldLabel);
        var rows = new List<PlaytestSkillUse.Skill>(skillUse.Skills.Values);
        rows.Sort((a, b) => b.Damage != a.Damage ? b.Damage.CompareTo(a.Damage) : b.Casts.CompareTo(a.Casts));
        foreach (PlaytestSkillUse.Skill skill in rows)
        {
            string flag = skillUse.Flag(skill);
            Color previous = GUI.color;
            if (flag != null)
            {
                GUI.color = new Color(1f, 0.65f, 0.2f);
            }

            EditorGUILayout.LabelField(skill.Id,
                $"on {skillUse.LoadoutShare(skill):P0} of bars, {skillUse.CastsPerFightingMinute(skill):0.0} casts a fighting minute, "
                + $"{skillUse.DamageShare(skill):P0} of skill damage" + (flag != null ? $"  ({flag})" : ""));
            GUI.color = previous;
        }
    }

    // Over all the runs listed: whether each tier is as hard as its numbers say, and whether players come in with the gear it expects.
    private void DrawTiers()
    {
        for (int tier = 1; tier <= Tiers.Max; tier++)
        {
            int played = 0, extracted = 0, margin = 0;
            foreach (PlaytestRunSummary run in runs)
            {
                played += run.TierRaids[tier];
                extracted += run.TierExtractions[tier];
                margin += run.TierScoreMargin[tier];
            }

            if (played > 0)
            {
                EditorGUILayout.LabelField($"Raid tier {Tiers.ToRoman(tier)}",
                    $"{extracted} of {played} extracted ({(float)extracted / played:P0}), gear score {(float)margin / played:+0;-0;0} against recommended on average");
            }
        }
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                Refresh();
            }

            if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                Directory.CreateDirectory(PlaytestLog.Folder);
                EditorUtility.RevealInFinder(PlaytestLog.Folder + Path.DirectorySeparatorChar);
            }

            if (GUILayout.Button("Edit Milestones", EditorStyles.toolbarButton, GUILayout.Width(100f)))
            {
                Selection.activeObject = Resources.Load<PlaytestSettings>(PlaytestSettings.ResourceName);
            }

            GUILayout.FlexibleSpace();
        }

        if (problem != null)
        {
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
        }

        if (runs.Count == 0)
        {
            EditorGUILayout.HelpBox("No runs logged yet. Play the game: every new game is a run, saved as a CSV in " + PlaytestLog.Folder, MessageType.Info);
            return;
        }

        IReadOnlyList<PlaytestMilestone> milestones = PlaytestSettings.Current.Milestones;
        scroll = EditorGUILayout.BeginScrollView(scroll);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Label("Run", EditorStyles.boldLabel, GUILayout.Width(RunWidth));
            GUILayout.Label("Sessions", EditorStyles.boldLabel, GUILayout.Width(StatWidth));
            GUILayout.Label("Played", EditorStyles.boldLabel, GUILayout.Width(StatWidth));
            GUILayout.Label("Deaths", EditorStyles.boldLabel, GUILayout.Width(StatWidth));
            GUILayout.Label("Raids", EditorStyles.boldLabel, GUILayout.Width(StatWidth));
            foreach (PlaytestMilestone milestone in milestones)
            {
                string target = milestone.targetMinutes > 0f ? $"\n≤ {milestone.targetMinutes:0} min" : "\n";
                GUILayout.Label(new GUIContent(milestone.label + target, $"{milestone.eventName} {milestone.subject} {milestone.detail}"),
                    EditorStyles.boldLabel, GUILayout.Width(MilestoneWidth), GUILayout.Height(32f));
            }
        }

        foreach (PlaytestRunSummary run in runs)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(run.RunId, EditorStyles.linkLabel, GUILayout.Width(RunWidth)))
                {
                    EditorUtility.OpenWithDefaultApp(PlaytestLog.PathFor(run.RunId));
                }

                GUILayout.Label(run.Sessions.ToString(), GUILayout.Width(StatWidth));
                GUILayout.Label($"{run.PlayedMinutes:0} min", GUILayout.Width(StatWidth));
                GUILayout.Label(run.Deaths.ToString(), GUILayout.Width(StatWidth));
                GUILayout.Label($"{run.Extractions}/{run.Raids}", GUILayout.Width(StatWidth));
                for (int i = 0; i < milestones.Count && i < run.MilestoneMinutes.Length; i++)
                {
                    DrawMilestone(run.MilestoneMinutes[i], milestones[i].targetMinutes);
                }
            }
        }

        EditorGUILayout.EndScrollView();
        DrawTiers();
        DrawSkills();
        EditorGUILayout.HelpBox("Raids are extractions/total. Click a run to open its log. Times are played minutes, pauses excluded.", MessageType.None);
    }

    private static void DrawMilestone(float? minutes, float target)
    {
        Color previous = GUI.color;
        if (minutes.HasValue && target > 0f)
        {
            GUI.color = minutes.Value <= target ? Color.green : minutes.Value <= target * 1.25f ? new Color(1f, 0.65f, 0.2f) : new Color(1f, 0.4f, 0.4f);
        }

        GUILayout.Label(minutes.HasValue ? $"{minutes.Value:0.0} min" : "—", GUILayout.Width(MilestoneWidth));
        GUI.color = previous;
    }
}
