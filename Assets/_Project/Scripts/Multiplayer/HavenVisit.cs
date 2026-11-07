using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Visiting a friend's Haven in co-op. A save divides into the <b>world</b> (the persistent scenes' objects, the
/// settlement, the stash: the host's) and the <b>character</b> (bag, resources, recipes, needs, level and talents, look,
/// quests and story moments: yours). A guest plays in the host's world with their own character:
/// <list type="bullet">
/// <item><see cref="Begin"/>: the guest's own game is kept in memory as their home, and the host's world (sent by
/// <see cref="CoopSession"/>) is loaded with the guest's character, beside the host.</item>
/// <item>Saving while visiting (<see cref="ForOwnSave"/>) writes the character into the guest's own journey; their home
/// world is never touched and the host's world is never written.</item>
/// <item><see cref="End"/>: back to their own Haven, with whatever their character gained.</item>
/// </list>
/// Until the host's game hears about changes to its world, guests don't change it: no building, no stash, no raids, no
/// ward breaches (<see cref="Refusal"/>). Gathering, crafting with your own things, fighting and talking are fine.
/// </summary>
public static class HavenVisit
{
    /// <summary>The home scene a host who is away (on a raid) is visited in.</summary>
    public const string HavenScene = "SafeHaven";
    private static readonly Vector3 BesideHost = new Vector3(1.6f, 0f, 1.6f);

    private static SaveData home;

    /// <summary>True while this player is a guest in a friend's Haven.</summary>
    public static bool IsVisiting { get; private set; }

    /// <summary>The host's name, while visiting.</summary>
    public static string HostName { get; private set; } = string.Empty;

    /// <summary>Why a guest can't do this in a friend's Haven (building, the stash, raids, breaches).</summary>
    public static string Refusal => $"This is {HostPossessive} Haven. That will come with a later update to co-op.";

    private static string HostPossessive => string.IsNullOrEmpty(HostName) ? "your friend's" : HostName + "'s";

    // ---- The rules (pure)

    /// <summary>The world part of a host's game, to send to a guest: nothing of the host's character goes with it.</summary>
    public static SaveData WorldOf(SaveData full, bool hostIsHome)
    {
        SaveData copy = Clone(full);
        var world = new SaveData
        {
            version = copy.version,
            savedAtUtc = copy.savedAtUtc,
            scene = hostIsHome ? copy.scene : HavenScene,
            hasPlayerPose = hostIsHome && copy.hasPlayerPose,
            playerPosition = copy.playerPosition,
            playerYaw = copy.playerYaw,
            world = copy.world,
            settlement = copy.settlement,
            stash = copy.stash,
        };
        return world;
    }

    /// <summary>The game a guest plays while visiting: the host's world, the guest's character, standing beside the host.</summary>
    public static SaveData Combine(SaveData hostWorld, SaveData guest)
    {
        SaveData visit = Clone(hostWorld);
        CopyCharacter(Clone(guest), visit);
        visit.raidUnderWay = null;
        if (visit.hasPlayerPose)
        {
            visit.playerPosition += BesideHost;
        }

        return visit;
    }

    /// <summary>The guest's own game with their character as it is now: what saving while visiting writes, and what they go home to.</summary>
    public static SaveData KeepHome(SaveData homeGame, SaveData now)
    {
        SaveData kept = Clone(homeGame);
        CopyCharacter(Clone(now), kept);
        return kept;
    }

    private static void CopyCharacter(SaveData from, SaveData to)
    {
        to.player = from.player;
        to.progress = from.progress;
        to.appearance = from.appearance;
        to.displayName = from.displayName;
        to.quests = from.quests;
        to.happenedEvents = from.happenedEvents;
        to.playtest = from.playtest;
    }

    private static SaveData Clone(SaveData data) => JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data));

    // ---- Going and coming back

    /// <summary>Takes this player into the host's world. False (with the reason) when they can't go now.</summary>
    public static bool Begin(SaveData hostWorld, string hostName, IContentLookup content, out string problem)
    {
        problem = SaveGameService.CheckSave();
        if (problem != null)
        {
            return false;
        }

        if (IsVisiting)
        {
            problem = "Already visiting.";
            return false;
        }

        SaveData ownGame = SaveGameService.Capture();
        HostName = hostName ?? string.Empty;
        if (!SaveGameService.TryPlay(Combine(hostWorld, ownGame), content, $"Visiting {HostPossessive} Haven", out problem))
        {
            HostName = string.Empty;
            return false;
        }

        home = ownGame;
        IsVisiting = true;
        return true;
    }

    /// <summary>What saving while visiting writes: the guest's own journey with their character as it is now.</summary>
    public static SaveData ForOwnSave(SaveData now) => home != null ? KeepHome(home, now) : now;

    /// <summary>Back to this player's own Haven, with their character as it is now (when <paramref name="travel"/>).</summary>
    public static void End(IContentLookup content, bool travel = true)
    {
        if (!IsVisiting)
        {
            return;
        }

        SaveData back = home;
        if (Player.Current != null && Player.Current.Persistence != null)
        {
            back = KeepHome(home, SaveGameService.Capture());
        }

        Forget();
        if (!travel || back == null)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(back.scene))
        {
            back.scene = HavenScene;
            back.hasPlayerPose = false;
        }

        if (!SaveGameService.TryPlay(back, content, "Heading home", out string problem))
        {
            Debug.LogWarning($"Co-op: couldn't head home ({problem}). The journey is saved on disk as it was.");
        }
    }

    /// <summary>Drops the visit without travelling (the game is going to the main menu, say).</summary>
    public static void Forget()
    {
        IsVisiting = false;
        HostName = string.Empty;
        home = null;
    }

    /// <summary>True when the active scene is a home that remembers itself (not a raid): where a host can be visited beside.</summary>
    public static bool HostIsHome() => PersistentScene.For(SceneManager.GetActiveScene()) != null && GameSession.ActiveRaid == null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Forget();
}
