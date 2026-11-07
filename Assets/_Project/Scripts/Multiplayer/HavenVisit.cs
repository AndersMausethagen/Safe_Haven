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
/// While visiting, both games keep the Haven in step (<see cref="HavenSync"/>): what a guest gathers, fells, drops or
/// builds shows in the host's Haven and the other way round. Gathering, crafting with your own things, fighting and
/// talking are free; building and the stash need the host's yes (<see cref="VisitorAccess"/>: <see cref="MayBuild"/>,
/// <see cref="MayUseStash"/>). Raids, ward breaches and loading wait for later phases (<see cref="Refusal"/>).
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

    /// <summary>True while visiting with the host's yes to building.</summary>
    public static bool MayBuild { get; private set; }

    /// <summary>True while visiting with the host's yes to the stash (it may still be on its way: <see cref="MayUseStash"/>).</summary>
    public static bool StashAllowed { get; private set; }

    /// <summary>True while visiting with the host's yes to the stash, and the host's stash has arrived.</summary>
    public static bool MayUseStash => StashAllowed && stashHere;

    private static bool stashHere;

    /// <summary>Why a guest can't do this in a friend's Haven (a ward breach, loading a save).</summary>
    public static string Refusal => $"This is {HostPossessive} Haven: that is theirs to start.";

    /// <summary>Why a guest can't open the portal themselves: they follow the host through it.</summary>
    public static string RaidRefusal => $"This is {HostPossessive} Haven. When they go through the portal, you can follow them.";

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
    public static SaveData ForOwnSave(SaveData now)
    {
        if (home == null)
        {
            return now;
        }

        // Saved on the way into a raid (following the host): if the game ends mid-raid, the journey knows it never came home.
        SaveData kept = KeepHome(home, now);
        kept.raidUnderWay = now.raidUnderWay;
        return kept;
    }

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

    /// <summary>True while visiting without the host's yes to building (placing, moving, upgrading): the host is asked,
    /// and the caller does nothing. False at home, or with the yes.</summary>
    public static bool BuildingNeedsAsking()
    {
        if (!IsVisiting || MayBuild)
        {
            return false;
        }

        if (CoopSession.Current != null)
        {
            CoopSession.Current.Access.Ask(VisitorRight.Build);
        }

        return true;
    }

    /// <summary>True while visiting without the host's yes to their stores (the stash, and their carts' cargo): the host is
    /// asked, and the caller does nothing.</summary>
    public static bool StoresNeedAsking()
    {
        if (!IsVisiting || StashAllowed)
        {
            return false;
        }

        if (CoopSession.Current != null)
        {
            CoopSession.Current.Access.Ask(VisitorRight.Stash);
        }

        return true;
    }

    /// <summary>
    /// True, and says so, when what a guest is about to do is the host's decision: their residents' jobs, the portal and
    /// ward, the altar, trades and the workshops' queues. A guest's copy of the settlement only mirrors the host's
    /// (<see cref="HavenSync"/>), so such a change would be undone at the host's next word.
    /// </summary>
    public static bool LeftToHost()
    {
        if (!IsVisiting)
        {
            return false;
        }

        string host = string.IsNullOrEmpty(HostName) ? "your friend" : HostName;
        GameMessages.Post($"That's for {host} to decide in their own Haven.", MessageKind.Warning);
        return true;
    }

    /// <summary>
    /// A traveller this guest rescued on the host's raid: they join the guest's own Haven (the game kept as home, which
    /// saving while visiting and going home both write), not the mirror of the host's. False when they already live there.
    /// </summary>
    public static bool RescuedHome(ResidentDefinition resident)
    {
        if (home == null || resident == null)
        {
            return false;
        }

        home.settlement ??= new SettlementSaveData();
        if (home.settlement.residents.Exists(entry => entry.id == resident.Id))
        {
            return false;
        }

        home.settlement.residents.Add(new ResidentSaveData { id = resident.Id, job = "" });
        return true;
    }

    /// <summary>Whether this guest's own Haven holds a spirit's gift (what they learnt is theirs, wherever they stand).</summary>
    public static bool HomeHasSpiritGift(string id) =>
        home != null && home.settlement != null && home.settlement.spiritGifts != null && home.settlement.spiritGifts.Contains(id);

    /// <summary>A spirit's gift this guest earnt in the host's Haven (the Nøkk's air) goes home with them.</summary>
    public static void SpiritGiftHome(string id)
    {
        if (home == null || string.IsNullOrEmpty(id))
        {
            return;
        }

        home.settlement ??= new SettlementSaveData();
        home.settlement.spiritGifts ??= new System.Collections.Generic.List<string>();
        if (!home.settlement.spiritGifts.Contains(id))
        {
            home.settlement.spiritGifts.Add(id);
        }
    }

    /// <summary>
    /// A boss this guest helped bring down on the host's raid counts in the guest's own Haven too (what it unlocks there).
    /// True the first time it falls for them, as their own first win would be.
    /// </summary>
    public static bool BossBeatenHome(string bossId)
    {
        if (home == null || string.IsNullOrEmpty(bossId))
        {
            return false;
        }

        home.settlement ??= new SettlementSaveData();
        if (home.settlement.defeatedBosses.Contains(bossId))
        {
            return false;
        }

        home.settlement.defeatedBosses.Add(bossId);
        return true;
    }

    /// <summary>The host's answer about building or the stash.</summary>
    public static void Allow(VisitorRight right, bool yes)
    {
        if (right == VisitorRight.Build)
        {
            MayBuild = yes;
        }
        else if (right == VisitorRight.Stash)
        {
            StashAllowed = yes;
            stashHere &= yes;
        }
    }

    /// <summary>The host's stash has arrived: it can be opened.</summary>
    public static void StashArrived() => stashHere = StashAllowed;

    /// <summary>Drops the visit without travelling (the game is going to the main menu, say).</summary>
    public static void Forget()
    {
        IsVisiting = false;
        HostName = string.Empty;
        home = null;
        MayBuild = false;
        StashAllowed = false;
        stashHere = false;
    }

    /// <summary>True when the active scene is a home that remembers itself (not a raid): where a host can be visited beside.</summary>
    public static bool HostIsHome() => PersistentScene.For(SceneManager.GetActiveScene()) != null && GameSession.ActiveRaid == null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Forget();
}
