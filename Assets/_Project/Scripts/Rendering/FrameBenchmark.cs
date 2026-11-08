using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A frame-time benchmark for development builds, started with `-sh-benchmark` on the command line (never in a release
/// build or the Editor). It starts a new game in a scratch save folder (the player's saves and playtest logs are never
/// touched), loads the Haven through the normal loading screen (so the shader warm-up runs as it would for a player),
/// stands at four places at day and at dusk, and records each: median, 95th and 99th percentile and worst frame, CPU
/// and GPU time where the platform reports them, and the worst frame of arriving there. Writes benchmark.txt in the
/// persistent data folder and quits. AgentScripts/BuildBenchmark.cs builds it.
/// </summary>
public sealed class FrameBenchmark : MonoBehaviour
{
    private const string Argument = "-sh-benchmark";
    private const int Frames = 300;
    private static readonly (string name, Vector3 at)[] Spots =
    {
        ("portal", new Vector3(0f, 0f, -16f)),
        ("ring", new Vector3(35f, 0f, 25f)),
        ("forest", new Vector3(120f, 0f, -90f)),
        ("far", new Vector3(260f, 0f, 180f)),
    };
    private static readonly float[] Hours = { 10f, 18.5f };

    private readonly StringBuilder log = new StringBuilder();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartIfAsked()
    {
        if (Application.isEditor || !Debug.isDebugBuild || Array.IndexOf(Environment.GetCommandLineArgs(), Argument) < 0)
        {
            return;
        }

        // Before anything saves: a scratch folder, so a benchmark run never writes over a real game.
        SaveGameService.Storage = new SaveStorage(Path.Combine(Application.temporaryCachePath, "Benchmark"));
        var go = new GameObject("Frame Benchmark");
        DontDestroyOnLoad(go);
        go.AddComponent<FrameBenchmark>();
    }

    private IEnumerator Start()
    {
        foreach (PlaytestLogger logger in FindObjectsByType<PlaytestLogger>(FindObjectsInactive.Include))
        {
            Destroy(logger); // a benchmark is not a playtest
        }

        log.AppendLine($"Safe Haven benchmark {DateTime.Now:yyyy-MM-dd HH:mm}, {SystemInfo.graphicsDeviceName}, {Screen.width}x{Screen.height}, " +
                       $"{SystemInfo.graphicsDeviceType}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        yield return new WaitForSecondsRealtime(2f);
        float started = Time.realtimeSinceStartup;
        if (!SaveGameService.TryStartNewGame("SafeHaven", out string problem))
        {
            Finish("could not start: " + problem);
            yield break;
        }

        while (Player.Current == null || SceneTransition.IsBusy || SceneManager.GetActiveScene().name != "SafeHaven")
        {
            yield return null;
        }

        log.AppendLine($"loading the Haven (shader warm-up included): {Time.realtimeSinceStartup - started:0.0} s");
        Player.Current.Health.AddInvulnerability(this);
        var sky = FindAnyObjectByType<DayNightCycle>();
        var persistence = Player.Current.GetComponent<PlayerPersistence>();
        Terrain terrain = Terrain.activeTerrain;
        foreach (float hour in Hours)
        {
            if (sky != null)
            {
                sky.Hour = hour;
            }

            foreach ((string name, Vector3 spot) in Spots)
            {
                Vector3 at = spot;
                at.y = terrain != null ? terrain.SampleHeight(at) + terrain.transform.position.y : 0f;
                persistence.MoveTo(new PlayerPose(at, 0f));
                float worstArriving = 0f;
                float arrived = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - arrived < 3f)
                {
                    yield return null;
                    worstArriving = Mathf.Max(worstArriving, Time.unscaledDeltaTime * 1000f);
                }

                yield return Measure($"{hour:0.#} {name}", worstArriving);
            }
        }

        Finish("done");
    }

    private IEnumerator Measure(string label, float worstArriving)
    {
        var times = new List<float>(Frames);
        double cpu = 0, gpu = 0;
        int timed = 0;
        var timings = new FrameTiming[1];
        for (int i = 0; i < Frames; i++)
        {
            yield return null;
            times.Add(Time.unscaledDeltaTime * 1000f);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
            {
                cpu += timings[0].cpuMainThreadFrameTime;
                gpu += timings[0].gpuFrameTime;
                timed++;
            }
        }

        times.Sort();
        string split = timed > 0 ? $", CPU main {cpu / timed:0.0} ms, GPU {gpu / timed:0.0} ms" : "";
        log.AppendLine($"{label}: median {times[Frames / 2]:0.0} ms, p95 {times[Frames * 95 / 100]:0.0}, p99 {times[Frames * 99 / 100]:0.0}, " +
                       $"worst {times[Frames - 1]:0.0}{split}; worst arriving {worstArriving:0.0} ms");
    }

    private void Finish(string how)
    {
        log.AppendLine(how);
        string path = Path.Combine(Application.persistentDataPath, "benchmark.txt");
        File.WriteAllText(path, log.ToString());
        Debug.Log("[FrameBenchmark]\n" + log);
        Application.Quit();
    }
}
