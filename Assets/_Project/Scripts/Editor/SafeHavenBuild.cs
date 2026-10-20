using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class SafeHavenBuild
{
    public static void BuildWindows()
    {
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

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Windows build failed with result {report.summary.result}.");
        }

        Debug.Log($"Windows build completed: {outputPath}");
    }
}
