using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// A development build for measuring (`unity command script run --file AgentScripts/BuildBenchmark.cs --entry BuildBenchmark.Build`):
/// the release build's scenes and full-size textures, as a development player with frame timing stats on (for the CPU and
/// GPU split), into Build/WindowsDev, so the release build in Build/Windows is left alone. Run it with `-sh-benchmark`
/// (FrameBenchmark); the results land in benchmark.txt in the persistent data folder. Frame timing stats are put back.
/// </summary>
public static class BuildBenchmark
{
    public static string Build()
    {
        string[] scenes = Array.ConvertAll(Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled), scene => scene.path);
        string folder = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Build", "WindowsDev"));
        Directory.CreateDirectory(folder);
        string exe = Path.Combine(folder, Application.productName + ".exe");
        // A dirty scene (the World Builder dirties the Haven after every Editor start) makes the build ask to save in a native
        // dialog no command can answer, and it waits there for good: save first, as the test runner does.
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        bool timingWas = PlayerSettings.enableFrameTimingStats;
        PlayerSettings.enableFrameTimingStats = true;
        try
        {
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
            };
            BuildReport report = EditorTextureCap.WithFullTextures(() => BuildPipeline.BuildPlayer(options));
            return $"{report.summary.result} in {report.summary.totalTime.TotalMinutes:0.0} min, {report.summary.totalSize / (1024 * 1024)} MB, " +
                   $"{report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings: {exe}";
        }
        finally
        {
            PlayerSettings.enableFrameTimingStats = timingWas;
            AssetDatabase.SaveAssets(); // the build saved the project settings with it on: write the restore too
        }
    }
}
