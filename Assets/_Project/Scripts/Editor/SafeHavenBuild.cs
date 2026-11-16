using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SafeHavenBuild
{
    /// <summary>
    /// Builds anyway when the audits find something (QuestAudit, ContentAudit): set it, or the environment variable
    /// SAFEHAVEN_SKIP_AUDITS=1, only to get a build out in a hurry. Off by default.
    /// </summary>
    public static bool SkipAudits;

    /// <summary>
    /// What would ship broken: a quest that can never start or be done, a story waiting for nothing, a portal moment no story
    /// plays, a rescue nobody places, a reference to something deleted, a recipe or building nobody can learn, an item asked
    /// for that nothing gives, a monster nothing spawns. Empty when all is well.
    /// </summary>
    public static List<string> Audits()
    {
        QuestSceneIndex.Invalidate();
        ContentAudit.Invalidate();
        return QuestAudit.CantStart().Select(pair => $"quest {pair.Key.name} can't start: {pair.Value}")
            .Concat(QuestAudit.Missing()).Concat(QuestAudit.WaitsForNothing()).Concat(QuestAudit.MissedInAnotherScene())
            .Concat(QuestAudit.SilentPortalMoments()).Concat(QuestAudit.UnplacedRescues()).Concat(ContentAudit.All()).ToList();
    }

    public static void BuildWindows()
    {
        // Loose ends stop the build: they would ship as a quest nobody can finish or an item nobody can find.
        List<string> problems = Audits();
        if (problems.Count > 0 && !SkipAudits && Environment.GetEnvironmentVariable("SAFEHAVEN_SKIP_AUDITS") != "1")
        {
            throw new InvalidOperationException($"The build is stopped: the audits found {problems.Count} loose end(s) (QuestAuditTests, ContentAuditTests):\n- "
                + string.Join("\n- ", problems));
        }

        EditorBuildSettingsScene[] enabledScenes = Array.FindAll(
            EditorBuildSettings.scenes, scene => scene.enabled);
        string[] scenes = Array.ConvertAll(enabledScenes, scene => scene.path);

        if (scenes.Length == 0)
        {
            throw new InvalidOperationException(
                "No enabled scenes are configured in Build Settings.");
        }

        string outputDirectory = Path.GetFullPath(
            Path.Combine(Directory.GetCurrentDirectory(), "Build", "Windows"));
        Directory.CreateDirectory(outputDirectory);

        string outputPath = Path.Combine(
            outputDirectory, Application.productName + ".exe");
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        // A dirty scene (the World Builder dirties the Haven after every Editor start) makes the build ask to save in a native
        // dialog no command can answer, and it waits there for good: save first, as the test runner does.
        // A test run leaves its own unsaved "Untitled" scene open, and saving that asks where, in the same dialog (2026-11-16): the
        // first scene of the build is opened in its place, dropping the test's scratch scene unsaved.
        bool scratchOpen = false;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            scratchOpen |= string.IsNullOrEmpty(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).path);
        }

        if (scratchOpen)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenes[0], UnityEditor.SceneManagement.OpenSceneMode.Single);
        }

        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        // Full-size textures in the build, whatever the Editor works with (EditorTextureCap).
        BuildReport report = EditorTextureCap.WithFullTextures(() => BuildPipeline.BuildPlayer(options));
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Windows build failed with result {report.summary.result}.");
        }

        Debug.Log($"Windows build completed: {outputPath}");
    }
}
