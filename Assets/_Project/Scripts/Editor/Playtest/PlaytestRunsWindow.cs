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
    private Vector2 scroll;
    private string problem;

    [MenuItem("Safe Haven/Playtest/Run Comparison")]
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
        foreach (string file in files)
        {
            try
            {
                runs.Add(PlaytestLog.Summarise(Path.GetFileNameWithoutExtension(file), PlaytestLog.Read(file), milestones));
            }
            catch (IOException exception)
            {
                problem = exception.Message;
            }
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
