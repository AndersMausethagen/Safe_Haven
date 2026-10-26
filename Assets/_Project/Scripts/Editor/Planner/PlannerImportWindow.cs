using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Safe Haven > Planner > Import Planner Data. Shows what the Progression Planner's manifest would create and change and
/// applies it only after the user confirms. New and changed assets are listed with old and new values; nothing is deleted.
/// </summary>
public sealed class PlannerImportWindow : EditorWindow
{
    private const string PathPref = "SafeHaven.Planner.ManifestPath";

    private string manifestPath;
    private List<PlannerPlanEntry> plan;
    private string status;
    private Vector2 scroll;

    [MenuItem("Safe Haven/Planner/Import Planner Data")]
    public static void Open()
    {
        GetWindow<PlannerImportWindow>("Planner Import").Show();
    }

    /// <summary>The planner lives beside the Unity project, outside the repository: ../ProgressionPlanner/export/manifest.json.</summary>
    public static string DefaultManifestPath()
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "../../ProgressionPlanner/export/manifest.json"));
    }

    private void OnEnable()
    {
        manifestPath = EditorPrefs.GetString(PathPref, DefaultManifestPath());
    }

    private void OnGUI()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            manifestPath = EditorGUILayout.TextField("Manifest", manifestPath);
            if (GUILayout.Button("Browse", GUILayout.Width(70f)))
            {
                string picked = EditorUtility.OpenFilePanel("Planner manifest", Path.GetDirectoryName(manifestPath), "json");
                if (!string.IsNullOrEmpty(picked))
                {
                    manifestPath = picked;
                }
            }
        }

        if (GUILayout.Button("Load and compare with project"))
        {
            Load();
        }

        if (!string.IsNullOrEmpty(status))
        {
            EditorGUILayout.HelpBox(status, MessageType.Info);
        }

        if (plan == null)
        {
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawSection(PlannerPlanEntry.Kind.New, "New assets", new Color(0.35f, 0.85f, 0.55f));
        DrawSection(PlannerPlanEntry.Kind.Changed, "Changed assets", new Color(1f, 0.7f, 0.25f));
        DrawSection(PlannerPlanEntry.Kind.Error, "Cannot import", new Color(1f, 0.4f, 0.4f));
        int unchanged = plan.FindAll(e => e.State == PlannerPlanEntry.Kind.Unchanged).Count;
        if (unchanged > 0)
        {
            EditorGUILayout.LabelField($"{unchanged} entries are already up to date.", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndScrollView();

        int selected = plan.FindAll(e => e.Apply && (e.State == PlannerPlanEntry.Kind.New || e.State == PlannerPlanEntry.Kind.Changed)).Count;
        using (new EditorGUI.DisabledScope(selected == 0))
        {
            if (GUILayout.Button($"Import {selected} selected", GUILayout.Height(30f)) && EditorUtility.DisplayDialog(
                    "Import planner data", $"Create new assets and update changed fields on {selected} entries? Nothing will be deleted.", "Import", "Cancel"))
            {
                status = PlannerImporter.Apply(plan);
                Load(keepStatus: true);
            }
        }
    }

    private void Load(bool keepStatus = false)
    {
        try
        {
            EditorPrefs.SetString(PathPref, manifestPath);
            PlannerManifest manifest = PlannerImporter.Parse(File.ReadAllText(manifestPath));
            plan = PlannerImporter.Plan(manifest);
            if (!keepStatus)
            {
                status = $"Manifest from {manifest.generatedAt}: {manifest.entities.Count} entries.";
            }
        }
        catch (Exception exception)
        {
            plan = null;
            status = "Could not load the manifest: " + exception.Message;
        }
    }

    private void DrawSection(PlannerPlanEntry.Kind state, string title, Color color)
    {
        List<PlannerPlanEntry> entries = plan.FindAll(e => e.State == state);
        if (entries.Count == 0)
        {
            return;
        }

        Color previous = GUI.color;
        GUI.color = color;
        EditorGUILayout.LabelField($"{title} ({entries.Count})", EditorStyles.boldLabel);
        GUI.color = previous;
        foreach (PlannerPlanEntry entry in entries)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (state != PlannerPlanEntry.Kind.Error)
                    {
                        entry.Apply = EditorGUILayout.Toggle(entry.Apply, GUILayout.Width(16f));
                    }

                    EditorGUILayout.LabelField(entry.Entity.key, EditorStyles.boldLabel);
                    if (entry.Asset != null && GUILayout.Button("Ping", GUILayout.Width(44f)))
                    {
                        EditorGUIUtility.PingObject(entry.Asset);
                    }
                }

                if (state == PlannerPlanEntry.Kind.New)
                {
                    EditorGUILayout.LabelField("Will be created at " + entry.Entity.path, EditorStyles.miniLabel);
                }

                foreach (string error in entry.Errors)
                {
                    EditorGUILayout.LabelField(error, EditorStyles.wordWrappedMiniLabel);
                }

                foreach (PlannerPlanEntry.FieldChange change in entry.Changes)
                {
                    string text = state == PlannerPlanEntry.Kind.New ? change.New : change.Old + "  →  " + change.New;
                    EditorGUILayout.LabelField(change.Field.name, text, EditorStyles.miniLabel);
                }
            }
        }
    }
}
