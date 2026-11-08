using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// A frame-time benchmark for development builds, started with `-sh-benchmark [haven|raid-day|raid-night]` on the command
/// line (never in a release build or the Editor). It starts a new game in a scratch save folder (the player's saves and
/// playtest logs are never touched) and loads the Haven through the normal loading screen (so the shader warm-up runs as
/// it would for a player). `haven` stands at four places at day and dusk, then at night in rain and in a storm; `raid-day`
/// and `raid-night` launch the Misty Woods on a seed that rolls that hour and stand at four places round the arrival, in
/// clear weather and in rain. Each place records median, 95th and 99th percentile and worst frame, CPU and GPU time where
/// the platform reports them, and the worst frame of arriving there. Writes benchmark-[mode].txt in the persistent data
/// folder and quits. AgentScripts/BuildBenchmark.cs builds it.
/// </summary>
public sealed class FrameBenchmark : MonoBehaviour
{
    private const string Argument = "-sh-benchmark";
    private const int Frames = 300;
    private static readonly (string name, Vector3 at)[] HavenSpots =
    {
        ("portal", new Vector3(0f, 0f, -16f)),
        ("ring", new Vector3(35f, 0f, 25f)),
        ("forest", new Vector3(120f, 0f, -90f)),
        ("far", new Vector3(260f, 0f, 180f)),
    };
    private static readonly Vector3[] RaidOffsets =
    {
        Vector3.zero, new Vector3(40f, 0f, 30f), new Vector3(-60f, 0f, 50f), new Vector3(20f, 0f, -70f),
    };

    private readonly StringBuilder log = new StringBuilder();
    private string mode = "haven";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartIfAsked()
    {
        string[] args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, Argument);
        if (Application.isEditor || !Debug.isDebugBuild || at < 0)
        {
            return;
        }

        // Before anything saves: a scratch folder, so a benchmark run never writes over a real game.
        SaveGameService.Storage = new SaveStorage(Path.Combine(Application.temporaryCachePath, "Benchmark"));
        var go = new GameObject("Frame Benchmark");
        DontDestroyOnLoad(go);
        var benchmark = go.AddComponent<FrameBenchmark>();
        if (at + 1 < args.Length && !args[at + 1].StartsWith("-"))
        {
            benchmark.mode = args[at + 1].ToLowerInvariant();
        }
    }

    private IEnumerator Start()
    {
        foreach (PlaytestLogger logger in FindObjectsByType<PlaytestLogger>(FindObjectsInactive.Include))
        {
            Destroy(logger); // a benchmark is not a playtest
        }

        log.AppendLine($"Safe Haven benchmark ({mode}) {DateTime.Now:yyyy-MM-dd HH:mm}, {SystemInfo.graphicsDeviceName}, " +
                       $"{Screen.width}x{Screen.height}, {SystemInfo.graphicsDeviceType}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
        yield return new WaitForSecondsRealtime(2f);
        float started = Time.realtimeSinceStartup;
        if (!SaveGameService.TryStartNewGame("SafeHaven", out string problem))
        {
            Finish("could not start: " + problem);
            yield break;
        }

        yield return Arrive("SafeHaven");
        log.AppendLine($"loading the Haven (shader warm-up included): {Time.realtimeSinceStartup - started:0.0} s");
        if (mode == "raid-day" || mode == "raid-night")
        {
            yield return Raid(mode == "raid-night" ? "Night" : "Day");
        }
        else
        {
            yield return Haven();
        }

        Finish("done");
    }

    private IEnumerator Haven()
    {
        var sky = FindAnyObjectByType<DayNightCycle>();
        var runs = new (float hour, string weather)[] { (10f, null), (18.5f, null), (23f, "Rain"), (23f, "Storm") };
        foreach ((float hour, string weather) in runs)
        {
            SetWeather(weather ?? "Clear");
            if (sky != null)
            {
                sky.Hour = hour;
            }

            foreach ((string name, Vector3 spot) in HavenSpots)
            {
                yield return Visit(OnTerrain(spot), $"{hour:0.#}{(weather != null ? " " + weather.ToLowerInvariant() : "")} {name}");
            }
        }
    }

    private IEnumerator Raid(string hour)
    {
        RaidPortal portal = FindAnyObjectByType<RaidPortal>();
        RaidDefinition raid = null;
        if (portal != null)
        {
            foreach (RaidDefinition destination in portal.Destinations)
            {
                raid = destination != null && destination.Id == "wildlands" ? destination : raid;
            }
        }

        if (raid == null)
        {
            log.AppendLine("no Misty Woods raid at the portal");
            yield break;
        }

        int seed = 1;
        while (seed < 500 && RaidTimeOfDay.For(raid, seed)?.name != hour)
        {
            seed++;
        }

        SaveGameService.TrySaveAsNew(out _);
        float started = Time.realtimeSinceStartup;
        if (!RaidLauncher.TryLaunch(raid, Player.Current.gameObject, null, out string problem, 0, seed))
        {
            log.AppendLine("could not launch: " + problem);
            yield break;
        }

        while (GameSession.ActiveRaid == null || RaidDirector.Current == null || Player.Current == null || SceneTransition.IsBusy)
        {
            yield return null;
        }

        log.AppendLine($"loading the raid ({hour.ToLowerInvariant()}, seed {seed}): {Time.realtimeSinceStartup - started:0.0} s");
        Player.Current.Health.AddInvulnerability(this);
        RaidDirector.Current.Session.AddTime(1800f);
        Vector3 arrival = Player.Current.transform.position;
        foreach (string weather in new[] { "Clear", "Rain" })
        {
            SetWeather(weather);
            for (int i = 0; i < RaidOffsets.Length; i++)
            {
                Vector3 at = NavMesh.SamplePosition(arrival + RaidOffsets[i], out NavMeshHit hit, 25f, NavMesh.AllAreas) ? hit.position : arrival;
                yield return Visit(at, $"{hour.ToLowerInvariant()} {weather.ToLowerInvariant()} spot {i + 1}");
            }
        }

        log.AppendLine($"creatures awake: {MonsterAI.Active.Count}");
    }

    private IEnumerator Arrive(string scene)
    {
        while (Player.Current == null || SceneTransition.IsBusy || SceneManager.GetActiveScene().name != scene)
        {
            yield return null;
        }

        Player.Current.Health.AddInvulnerability(this);
    }

    private IEnumerator Visit(Vector3 at, string label)
    {
        Player.Current.GetComponent<PlayerPersistence>().MoveTo(new PlayerPose(at, 0f));
        float worstArriving = 0f;
        float arrived = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - arrived < 3f)
        {
            yield return null;
            worstArriving = Mathf.Max(worstArriving, Time.unscaledDeltaTime * 1000f);
        }

        yield return Measure(label, worstArriving);
    }

    private static Vector3 OnTerrain(Vector3 at)
    {
        Terrain terrain = Terrain.activeTerrain;
        at.y = terrain != null ? terrain.SampleHeight(at) + terrain.transform.position.y : 0f;
        return at;
    }

    // The weathers the scene's climate knows are loaded with it; picked by asset name.
    private void SetWeather(string name)
    {
        WeatherSystem weather = WeatherSystem.Active;
        if (weather == null)
        {
            return;
        }

        foreach (WeatherDefinition definition in Resources.FindObjectsOfTypeAll<WeatherDefinition>())
        {
            if (definition.name == name)
            {
                weather.SetWeather(definition, 0f);
                return;
            }
        }

        log.AppendLine($"(no {name} weather loaded)");
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
        string path = Path.Combine(Application.persistentDataPath, $"benchmark-{mode}.txt");
        File.WriteAllText(path, log.ToString());
        Debug.Log("[FrameBenchmark]\n" + log);
        Application.Quit();
    }
}
