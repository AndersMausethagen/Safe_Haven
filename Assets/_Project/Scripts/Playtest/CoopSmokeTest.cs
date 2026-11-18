using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Two built copies of the game playing co-op on this machine (Docs/BugPatterns.md, second round, fix 8). Started by
/// AgentScripts/CoopTwoCopies.ps1 with "-coopSmoke host|guest -coopSmokeDir &lt;folder&gt;"; without that switch it does nothing.
/// The host starts a new game, lights a boundary stone and opens its Haven on the direct port. The guest starts a game of its
/// own, joins on 127.0.0.1 and checks what one Editor can't (two games there share every static): the host's settlement came as
/// a borrowed copy while its own stayed its own, a Pact Rune laid at the host's stone reaches the host's game, a save made while
/// visiting holds the guest's own Haven, and leaving takes it home. Each copy writes what it saw to &lt;folder&gt;/&lt;role&gt;.txt
/// ("PASS" or "FAIL" first) and quits. Saves go to that folder, Steam stays off and no playtest log is written.
/// </summary>
public sealed class CoopSmokeTest : MonoBehaviour
{
    public const string HostRole = "host";
    public const string GuestRole = "guest";
    private const string Region = "Ravenwood";
    private const string Haven = "SafeHaven";
    private const float LongestRun = 420f;

    private static bool read;
    private static string role;
    private static string folder;

    private readonly List<string> seen = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> exceptions = new List<string>();
    private string step = "starting";
    private bool finished;
    private bool friendArrived;

    /// <summary>"host" or "guest" when this copy runs the two-copy co-op test; null otherwise.</summary>
    public static string Role
    {
        get
        {
            ReadCommandLine();
            return role;
        }
    }

    public static bool IsRunning => Role != null;

    private static void ReadCommandLine()
    {
        if (read)
        {
            return;
        }

        read = true;
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
        {
            if (args[i] == "-coopSmoke")
            {
                role = args[i + 1];
            }
            else if (args[i] == "-coopSmokeDir")
            {
                folder = args[i + 1];
            }
        }

        if ((role != HostRole && role != GuestRole) || string.IsNullOrEmpty(folder))
        {
            role = null;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BeginIfAsked()
    {
        if (IsRunning)
        {
            DontDestroyOnLoad(new GameObject("Co-op smoke test").AddComponent<CoopSmokeTest>().gameObject);
        }
    }

    private IEnumerator Start()
    {
        Application.runInBackground = true;
        Application.logMessageReceived += Heard;
        NetworkPlayerAvatar.FriendArrived += OnFriendArrived;
        Directory.CreateDirectory(folder);
        SaveGameService.Storage = new SaveStorage(Path.Combine(folder, role + " saves"));
        yield return null;
        yield return role == HostRole ? AsHost() : AsGuest();
        Finish();
    }

    private void Update()
    {
        if (!finished && Time.realtimeSinceStartup > LongestRun)
        {
            failures.Add($"timed out at: {step}");
            Finish();
        }
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= Heard;
        NetworkPlayerAvatar.FriendArrived -= OnFriendArrived;
    }

    private IEnumerator AsHost()
    {
        yield return NewGame();
        TerritorySettings settings = TerritorySettings.Current;
        TerritoryState lands = GameSession.Here.Territory;
        lands.Claim(Region, settings);
        float hold = lands.HoldOf(Region);
        Saw($"lit {Region}: hold {hold}");

        step = "hosting";
        string problem = "there is no co-op session";
        bool hosting = CoopSession.Current != null && CoopSession.Current.Host(out problem);
        if (!Check(hosting, "host: " + problem))
        {
            yield break;
        }

        File.WriteAllText(Path.Combine(folder, "host.ready"), "hosting");
        step = "waiting for the friend";
        yield return Until(() => friendArrived, 240f);
        if (!Check(friendArrived, "the friend never arrived"))
        {
            yield break;
        }

        Saw("the friend arrived");
        step = "waiting for the friend's rune";
        float wanted = hold + settings.RuneHold;
        yield return Until(() => lands.HoldOf(Region) >= wanted - 0.01f, 120f);
        Check(lands.HoldOf(Region) >= wanted - 0.01f, $"the friend's rune never reached the host: hold {lands.HoldOf(Region)}, wanted {wanted}");
        Saw($"the friend's rune: hold {lands.HoldOf(Region)}");

        // Open until the friend has finished, so its leaving is its own and not the host closing.
        step = "waiting for the friend to finish";
        yield return Until(() => File.Exists(ResultPath(GuestRole)), 180f);
        Check(File.Exists(ResultPath(GuestRole)), "the friend never finished");
        CoopSession.Current.Leave(false);
    }

    private IEnumerator AsGuest()
    {
        step = "waiting for the host to open";
        yield return Until(() => File.Exists(Path.Combine(folder, "host.ready")), 300f);
        if (!Check(File.Exists(Path.Combine(folder, "host.ready")), "the host never opened"))
        {
            yield break;
        }

        yield return NewGame();
        Check(!GameSession.Home.Territory.IsClaimed(Region), $"a new game hasn't lit {Region}");

        step = "joining";
        string problem = "there is no co-op session";
        bool joined = CoopSession.Current != null && CoopSession.Current.Join(CoopSession.LocalAddress, out problem);
        if (!Check(joined, "join: " + problem))
        {
            yield break;
        }

        step = "visiting";
        yield return Until(() => HavenVisit.IsVisiting && AtHaven(), 180f);
        if (!Check(HavenVisit.IsVisiting && AtHaven(), "never arrived in the host's Haven"))
        {
            yield break;
        }

        step = "the host's settlement";
        yield return Until(() => GameSession.Here.Territory.IsClaimed(Region), 60f);
        Check(GameSession.Here.Territory.IsClaimed(Region), "the host's settlement never arrived (its lit stone)");
        Check(!ReferenceEquals(GameSession.Here, GameSession.Home), "the host's settlement is a copy beside this player's own");
        Check(!GameSession.Home.Territory.IsClaimed(Region), "this player's own Haven stays its own");
        Saw($"in the host's Haven: {Region} lit there, not at home");

        step = "laying a rune";
        TerritorySettings settings = TerritorySettings.Current;
        Player.Current.Bag.Give(settings.PactRune, 1);
        Territory.Offer(Region, Player.Current.gameObject);
        Check(Player.Current.Bag.CountCarried(settings.PactRune) == 0, "the rune was laid (spent)");
        yield return Wait(5f);

        step = "saving while visiting";
        Check(SaveGameService.TrySave(out problem), "save while visiting: " + problem);
        if (Check(SaveGameService.TryReadSave(out SaveData saved, out problem), "read the save: " + problem))
        {
            Check(saved.settlement != null && saved.settlement.territory.holds.All(held => held.region != Region || !held.claimed),
                "the save made while visiting holds this player's own Haven, not the host's");
        }

        step = "leaving";
        CoopSession.Current.Leave();
        yield return Until(() => !HavenVisit.IsVisiting && AtHaven(), 120f);
        Check(!HavenVisit.IsVisiting && AtHaven(), "never home again");
        Check(!GameSession.Here.Territory.IsClaimed(Region), "home is this player's own Haven again");
        Saw("home again");
    }

    private IEnumerator NewGame()
    {
        step = "new game";
        if (!Check(SaveGameService.TryStartNewGame(Haven, out string problem), "new game: " + problem))
        {
            yield break;
        }

        yield return Until(AtHaven, 180f);
        Check(AtHaven(), "the Haven never loaded with a player");
        Check(SaveGameService.TrySaveAsNew(out problem), "the first save: " + problem);
        Saw($"a new game in the Haven ({Time.realtimeSinceStartup:0} s)");
    }

    private static bool AtHaven() => SceneManager.GetActiveScene().name == Haven && Player.Current != null && !SceneTransition.IsBusy;

    private bool Check(bool holds, string failure)
    {
        if (!holds)
        {
            failures.Add($"{step}: {failure}");
        }

        return holds;
    }

    private void Saw(string what) => seen.Add($"{Time.realtimeSinceStartup:0} s: {what}");

    private void OnFriendArrived(NetworkPlayerAvatar friend) => friendArrived = true;

    private void Heard(string message, string stack, LogType type)
    {
        if (type == LogType.Exception)
        {
            exceptions.Add(message + " | " + stack.Split('\n').FirstOrDefault());
        }
    }

    private static IEnumerator Until(Func<bool> done, float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (!done() && Time.realtimeSinceStartup < until)
        {
            yield return null;
        }
    }

    private static IEnumerator Wait(float seconds)
    {
        float until = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < until)
        {
            yield return null;
        }
    }

    private static string ResultPath(string whose) => Path.Combine(folder, whose + ".txt");

    private void Finish()
    {
        if (finished)
        {
            return;
        }

        finished = true;
        bool passed = failures.Count == 0 && exceptions.Count == 0;
        var report = new List<string> { passed ? "PASS" : "FAIL" };
        report.AddRange(seen);
        report.AddRange(failures.Select(failure => "failed: " + failure));
        report.AddRange(exceptions.Select(exception => "exception: " + exception));
        File.WriteAllLines(ResultPath(role), report);
        Debug.Log($"Co-op smoke test ({role}): {string.Join(" / ", report)}");
        Application.Quit(passed ? 0 : 1);
    }

    // Entering Play doesn't reload scripts in this project: what a session left in the statics goes (EngineHazardTests).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        folder = null;
        read = false;
        role = null;
    }
}
