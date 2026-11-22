using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>What a guest does for the host's lands in co-op, sent to the host's game (Territory.ForHost, CoopLands).</summary>
public enum LandWork : byte
{
    Rune = 1,
    Task = 2,
    Find = 3,
    Heart = 4,
}

/// <summary>
/// The Haven's Reach in play (Docs/Territory.md): TerritoryState's rules tied to the game. A turn passes at each dawn at home
/// (Seasons) and whenever a raid ends; what a turn brings is told at once at home, or when the player is next home. Stones are
/// lit and fed Pact Runes here (BoundaryStone), a watch held at one breaks a push (StoneDefence), a rot's heart burned on a raid
/// weakens it (WeakenIncursionOutcome). Held regions ask a task of the Haven (hunting, gathering, fishing there), counted from
/// the game's own events; places found (PlaceDiscovery) give Insight and strengthen the hold. Secure regions send a tithe home at
/// dawn, and the corruption's bands come from the weakest of the Haven's lands (CorruptionNights).
/// In co-op a guest's lands only mirror the host's (they come with the settlement), so what a guest does for them is sent to the
/// host's game (<see cref="ForHost"/>, CoopLands) and applied there (<see cref="FromFriend"/>); lighting stones and standing
/// watch are the host's.
/// </summary>
public static class Territory
{
    public const string HomeScene = "SafeHaven";

    private static readonly List<string> waiting = new List<string>();
    // Runs whose rot's heart has been burned: every player on a run has the heart's rumour, but it weakens a push once.
    private static readonly HashSet<int> burnedOnRuns = new HashSet<int>();
    private static WorldAtlas haven;
    private static Dictionary<string, List<WorldAtlas.Place>> placesByRegion;

    /// <summary>Raised whenever the hold on any region changes.</summary>
    public static event Action Changed;

    /// <summary>
    /// Set by co-op while this game may be a guest: sends what this player did for the host's lands to the host's game (the
    /// work, the region, and the run or task kind). While visiting nothing is changed here: the host's answer comes back with
    /// the settlement.
    /// </summary>
    public static Action<LandWork, string, int> ForHost { get; set; }

    /// <summary>Forgets what belonged to the game being played (news waiting to be told, hearts burned on its runs): a new game
    /// or a loaded one starts clean (GameSession).</summary>
    public static void ForgetGame()
    {
        waiting.Clear();
        burnedOnRuns.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession()
    {
        waiting.Clear();
        burnedOnRuns.Clear();
        haven = null;
        placesByRegion = null;
        Changed = null;
        ForHost = null;
    }

    // After the reset phase: GameEvents clears its own listeners in SubsystemRegistration, and in a build the order of hooks
    // in one phase isn't fixed, so subscribing there could be wiped (the bug audit's B10).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Listen()
    {
        GameEvents.Raised -= OnGameEvent;
        GameEvents.Raised += OnGameEvent;
        ExperienceTracker.Practised -= OnPractised;
        ExperienceTracker.Practised += OnPractised;
    }

    /// <summary>The host's lands have arrived in a guest's game (HavenSync): the stones show them.</summary>
    public static void MirrorArrived() => Changed?.Invoke();

    private static string Host => string.IsNullOrEmpty(HavenVisit.HostName) ? "your friend" : HavenVisit.HostName;

    private static void SendToHost(LandWork work, string region, int detail) => ForHost?.Invoke(work, region, detail);

    /// <summary>
    /// False until the portal has spoken of the stones (TerritorySettings › Unlocked By, after the third raid, and Unlocked When,
    /// the conversation's end): every stone is cold. A Haven that has lit one already stays open.
    /// </summary>
    public static bool Unlocked
    {
        get
        {
            QuestDefinition quest = Settings.UnlockedBy;
            if (quest == null || State.FreeClaimUsed || State.ClaimedCount > 0)
            {
                return true;
            }

            QuestStatus status = GameSession.Quests.StatusOf(quest);
            return status == QuestStatus.Completed
                || status != QuestStatus.NotStarted && (Settings.UnlockedWhen == null || GameSession.Quests.HasHappened(Settings.UnlockedWhen));
        }
    }

    // What the quests hear of the land (TerritorySettings › The story).
    private static void Tell(GameEventDefinition happened)
    {
        if (happened != null)
        {
            GameEvents.Raise(happened, Player.Current != null ? Player.Current.gameObject : null);
        }
    }

    // A firmer hold, and word to the quests when it has made the region Secure.
    private static void RaiseHold(string region, float amount)
    {
        bool wasSecure = StandingOf(region) == RegionStanding.Secure;
        State.Raise(region, amount);
        NoteSecure(region, wasSecure);
    }

    private static void NoteSecure(string region, bool wasSecure)
    {
        if (!wasSecure && StandingOf(region) == RegionStanding.Secure)
        {
            Tell(Settings.LandSecure);
        }
    }

    // "the nearest lies about 120 m to the north-west": where to look for a place the region's stone remembers.
    private static string NearestUnfound(string region, Vector3 from)
    {
        var here = new Vector2(from.x, from.z);
        WorldAtlas.Place best = null;
        float bestDistance = float.MaxValue;
        foreach (WorldAtlas.Place place in PlacesIn(region))
        {
            float distance = (place.at - here).magnitude;
            if (distance < bestDistance && !GameSession.Progress.Journal.HasDiscovered(place.id))
            {
                bestDistance = distance;
                best = place;
            }
        }

        if (best == null)
        {
            return null;
        }

        string[] ways = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
        float bearing = Mathf.Repeat(HavenWorldPlan.BearingOf(best.at - here), 360f);
        return $"the nearest lies about {Mathf.Max(10, Mathf.RoundToInt(bestDistance / 10f) * 10)} m to the {ways[Mathf.RoundToInt(bearing / 45f) % 8]}";
    }

    // The lands of the Haven one stands in: read here, changed only off a visit (a guest's work goes to the host: ForHost).
    private static TerritoryState State => GameSession.HereItself.Territory;
    private static TerritorySettings Settings => TerritorySettings.Current;

    /// <summary>The Haven's atlas, wherever the player is (the Lands tab reads it on raids too).</summary>
    public static WorldAtlas Atlas => haven != null ? haven : haven = WorldAtlas.For(HomeScene);

    /// <summary>The region the portal stands in: home, never claimed or lost.</summary>
    public static string HomeRegion => Atlas != null ? Atlas.RegionAt(Vector3.zero)?.name : null;

    /// <summary>The regions that can be claimed: every region but home, in the atlas's order.</summary>
    public static IEnumerable<WorldAtlas.Region> Claimable => Atlas != null ? Atlas.Regions.Where(r => r.name != HomeRegion) : Enumerable.Empty<WorldAtlas.Region>();

    /// <summary>The region at a point of the scene being played, or null away from home.</summary>
    public static WorldAtlas.Region RegionAt(Vector3 at)
    {
        WorldAtlas atlas = PlaceDiscovery.Current != null ? PlaceDiscovery.Current.Atlas : null;
        return atlas != null && atlas == Atlas ? atlas.RegionAt(at) : null;
    }

    public static IReadOnlyList<WorldAtlas.Place> PlacesIn(string region)
    {
        if (placesByRegion == null)
        {
            placesByRegion = new Dictionary<string, List<WorldAtlas.Place>>();
            foreach (WorldAtlas.Place place in Atlas != null ? Atlas.Places : new List<WorldAtlas.Place>())
            {
                string name = Atlas.RegionAt(new Vector3(place.at.x, 0f, place.at.y))?.name;
                if (name != null)
                {
                    if (!placesByRegion.TryGetValue(name, out List<WorldAtlas.Place> list))
                    {
                        placesByRegion[name] = list = new List<WorldAtlas.Place>();
                    }

                    list.Add(place);
                }
            }
        }

        return region != null && placesByRegion.TryGetValue(region, out List<WorldAtlas.Place> found) ? found : new List<WorldAtlas.Place>();
    }

    public static int PlacesFound(string region) => PlacesIn(region).Count(p => GameSession.Progress.Journal.HasDiscovered(p.id));

    public static RegionStanding StandingOf(string region) => State.StandingOf(region, Settings);

    /// <summary>"Held · hold 64", "Wild", "Under threat: 2 turns"...: how a region stands, in a few words.</summary>
    public static string Describe(string region)
    {
        if (region == HomeRegion)
        {
            return "Home";
        }

        RegionStanding standing = StandingOf(region);
        string words = standing == RegionStanding.Wild
            ? (State.Of(region).EverClaimed ? "Wild again" : "Wild")
            : $"{standing} · hold {State.HoldOf(region):0}";
        TerritoryState.Push push = State.Incursion;
        if (push != null && push.Region == region)
        {
            int turns = Math.Max(0, push.DueTurn - State.Turn);
            words += turns <= 1 ? " · the forest pushes in next turn" : $" · the forest pushes in {turns} turns";
        }

        return words;
    }

    // ----- At a stone -----

    public static bool CanClaim(string region, GameObject user, out string reason)
    {
        TerritorySettings settings = Settings;
        reason = null;
        if (region == HomeRegion || State.IsClaimed(region))
        {
            reason = "It is the Haven's already.";
            return false;
        }

        if (HavenVisit.IsVisiting)
        {
            reason = $"These are {Host}'s lands: lighting their stones is for them to decide. A Pact Rune offered at a lit stone helps them hold it.";
            return false;
        }

        if (!Unlocked)
        {
            bool called = Settings.UnlockedBy != null && GameSession.Quests.StatusOf(Settings.UnlockedBy) != QuestStatus.NotStarted;
            reason = called
                ? "The stone is cold under your hand, and doesn't know you. The portal wants a word with you about these stones first."
                : "The stone is cold under your hand, and doesn't know you. The portal knows what these stones are: it will speak of them once you have been through it a few times.";
            return false;
        }

        int needed = Math.Min(settings.PlacesToClaim, PlacesIn(region).Count);
        if (PlacesFound(region) < needed)
        {
            string hint = user != null ? NearestUnfound(region, user.transform.position) : null;
            reason = $"The stone doesn't know you. Find {needed} of {region}'s places first ({PlacesFound(region)} found){(hint != null ? ": " + hint : "")}.";
            return false;
        }

        if (State.ClaimNeedsRune(settings) && !HasRune(user))
        {
            reason = "It wants a Pact Rune: they turn up in raids, in caches and at the rot's heart.";
            return false;
        }

        return true;
    }

    public static void Claim(string region, GameObject user)
    {
        if (!CanClaim(region, user, out string reason))
        {
            GameMessages.Post(reason, MessageKind.Warning);
            return;
        }

        if (State.ClaimNeedsRune(Settings) && !SpendRune(user))
        {
            return;
        }

        State.Claim(region, Settings);
        GameMessages.Post($"The boundary stone of {region} burns again. {region} is the Haven's: hold it, and it will give back.", MessageKind.Gain);
        Tell(Settings.StoneLit);
        Changed?.Invoke();
    }

    public static bool CanOffer(string region, GameObject user, out string reason)
    {
        reason = null;
        if (!State.IsClaimed(region))
        {
            reason = "Light the stone first.";
            return false;
        }

        if (State.HoldOf(region) >= 99.5f)
        {
            reason = "Its hold is as strong as it gets.";
            return false;
        }

        if (!HasRune(user))
        {
            reason = "You have no Pact Rune.";
            return false;
        }

        return true;
    }

    public static void Offer(string region, GameObject user)
    {
        if (!CanOffer(region, user, out string reason))
        {
            GameMessages.Post(reason, MessageKind.Warning);
            return;
        }

        if (!SpendRune(user))
        {
            return;
        }

        // Home Only: a rune laid in a friend's stone moves the friend's quests (FromFriend), not this player's own.
        Tell(Settings.RuneOffered);
        if (HavenVisit.IsVisiting)
        {
            SendToHost(LandWork.Rune, region, 0);
            GameMessages.Post($"The rune sinks into the stone. {Host}'s hold on {region} grows.", MessageKind.Gain);
            return;
        }

        RaiseHold(region, Settings.RuneHold);
        GameMessages.Post($"The rune sinks into the stone. {region}: {Describe(region)}.", MessageKind.Gain);
        Changed?.Invoke();
    }

    /// <summary>Whether this player can stand watch at a stone: in a friend's Haven, the host calls the forest, and friends stand with them.</summary>
    public static bool CanStandWatch(out string reason)
    {
        reason = HavenVisit.IsVisiting ? $"Only {Host} can call the forest to their stone. Stand with them when they do." : null;
        return reason == null;
    }

    public static int RunesHeld(GameObject user) =>
        Settings.PactRune != null && user != null ? MaterialPool.For(user).Have(Settings.PactRune) : 0;

    private static bool HasRune(GameObject user) => RunesHeld(user) > 0;

    private static bool SpendRune(GameObject user) =>
        MaterialPool.For(user).TrySpend(new List<ResourceAmount>(), new[] { new ItemAmount(Settings.PactRune, 1) });

    /// <summary>Whether a push on <paramref name="region"/> is still to come (a stone's watch is for it).</summary>
    public static bool PushOn(string region) => State != null && State.Incursion != null && State.Incursion.Region == region;

    /// <summary>A watch held at a stone (StoneDefence).</summary>
    public static void Defended(string region)
    {
        bool wasSecure = StandingOf(region) == RegionStanding.Secure;
        if (!HavenVisit.IsVisiting && State.Defended(region, Settings))
        {
            ExperienceTracker.GrantInsight(Settings.DefenceInsight);
            GameMessages.Post($"The push on {region} is broken. {region}: {Describe(region)}.", MessageKind.Gain);
            Tell(Settings.PushAnswered);
            NoteSecure(region, wasSecure);
            Changed?.Invoke();
        }
    }

    /// <summary>A rot's heart burned on a raid (WeakenIncursionOutcome).</summary>
    public static void HeartBurned()
    {
        int run = GameSession.ActiveRaid != null ? GameSession.ActiveRaid.Seed : 0;
        if (HavenVisit.IsVisiting)
        {
            TerritoryState.Push pushed = State.Incursion;
            if (pushed != null)
            {
                SendToHost(LandWork.Heart, pushed.Region, run);
                GameMessages.Post($"The rot's heart burns. The forest's push on {Host}'s {pushed.Region} falters.", MessageKind.Gain);
            }

            return;
        }

        Weaken(run, null);
    }

    // Once a run (every player on it has the heart's rumour). Run 0: not on a raid, always counts.
    private static void Weaken(int run, string friend)
    {
        if (State.Incursion == null || (run != 0 && burnedOnRuns.Contains(run)) || !State.Weaken(Settings))
        {
            return;
        }

        if (run != 0)
        {
            burnedOnRuns.Add(run);
        }

        TerritoryState.Push push = State.Incursion;
        string who = friend != null ? $"{friend} burned the rot's heart." : "The rot's heart burns.";
        GameMessages.Post(push.Weakened >= 0.999f
            ? $"{who} The forest's push on {push.Region} has nothing left behind it."
            : $"{who} The forest's push on {push.Region} falters.", MessageKind.Gain);
        Tell(Settings.PushAnswered);
        Changed?.Invoke();
    }

    /// <summary>A named place found (PlaceDiscovery): Insight, and a firmer hold on its region if it is the Haven's.</summary>
    public static void PlaceFound(WorldAtlas.Place place)
    {
        // The portal stands at the world's origin (the Haven rework centred the terrain on it).
        ExperienceTracker.GrantInsight(Settings.FindInsightAt(place.at.magnitude));
        WorldAtlas atlas = Atlas;
        string region = atlas != null ? atlas.RegionAt(new Vector3(place.at.x, 0f, place.at.y))?.name : null;
        if (!State.IsClaimed(region))
        {
            return;
        }

        if (HavenVisit.IsVisiting)
        {
            SendToHost(LandWork.Find, region, 0);
            return;
        }

        RaiseHold(region, Settings.FindHold);
        Changed?.Invoke();
    }

    /// <summary>
    /// The host's game applies what a friend did for its lands (CoopLands): a rune offered, a task's work, a place found, a heart
    /// burned on the run (<paramref name="detail"/>: the task's kind, or the run).
    /// </summary>
    public static void FromFriend(LandWork work, string region, int detail, string friend)
    {
        if (HavenVisit.IsVisiting || string.IsNullOrEmpty(region))
        {
            return;
        }

        friend = string.IsNullOrEmpty(friend) ? "A friend" : friend;
        switch (work)
        {
            case LandWork.Rune:
                if (!State.IsClaimed(region))
                {
                    GameMessages.Post($"{friend}'s Pact Rune found no lit stone in {region}.", MessageKind.Warning);
                    return;
                }

                RaiseHold(region, Settings.RuneHold);
                GameMessages.Post($"{friend} offered a Pact Rune at the stone of {region}. {region}: {Describe(region)}.", MessageKind.Gain);
                // As a friend's task work does, it counts for the host's quests about the land.
                Tell(Settings.RuneOffered);
                Changed?.Invoke();
                break;
            case LandWork.Task:
                Count(region, (RegionTaskKind)detail);
                break;
            case LandWork.Find:
                if (State.IsClaimed(region))
                {
                    RaiseHold(region, Settings.FindHold);
                    Changed?.Invoke();
                }

                break;
            case LandWork.Heart:
                Weaken(detail, friend);
                break;
        }
    }

    // ----- Turns -----

    /// <summary>A turn has passed: a dawn at home, or a raid ended (<paramref name="raidSeconds"/> long: the parties out to the held lands
    /// meanwhile, LandParties). What it brings is told now at home, or on coming home.</summary>
    public static void Turn(bool atDawn, float raidSeconds = 0f)
    {
        if (HavenVisit.IsVisiting || Atlas == null)
        {
            return;
        }

        // Only off a visit (above), where one's own Haven is the one here.
        SettlementState settlement = GameSession.Home;
        bool pushComing = State.Incursion != null;
        RaidReport report = GameSession.PendingReport;
        List<(LandPartyKind kind, string region, ResidentRecord who)> parties = atDawn
            ? new List<(LandPartyKind, string, ResidentRecord)>()
            : LandParties.Going(settlement, raidSeconds, report != null ? report.Companion : null, Settings);
        var tended = new HashSet<string>();
        var scouted = new HashSet<string>();
        foreach ((LandPartyKind kind, string region, ResidentRecord _) in parties)
        {
            (kind == LandPartyKind.Scout ? scouted : tended).Add(region);
        }

        List<string> news = State.AdvanceTurn(Settings, settlement.Blight, () => UnityEngine.Random.value, out float spread, tended, scouted);
        // A quick raid gives those out less time: they bring a share of a full trip (RaidPacingSettings › Time Away, the user 2026-11-22:
        // "reduce reward for quick raids. less than 4 min, then gradually increase up to 9 min").
        float share = atDawn ? 1f : RaidPacingSettings.Current.TimeAwayShare(raidSeconds);
        int before = news.Count;
        news.AddRange(PartiesHome(parties, share));
        if (!atDawn)
        {
            news.AddRange(OutpostsHome(settlement, raidSeconds, report != null ? report.Companion : null, share));
        }

        if (share < 0.999f && news.Count > before)
        {
            news.Add($"A short trip ({RaidStats.FormatTime(raidSeconds)}): those out had time for {Mathf.RoundToInt(share * 100f)}% of a full one. A longer raid gives them all of it.");
        }

        // An outpost on a land the forest has taken back is given up (Outposts).
        foreach (string region in new List<string>(settlement.Outposts.Keys))
        {
            if (!State.IsClaimed(region))
            {
                settlement.SetOutpost(region, null);
                news.Add($"The outpost on {region} is given up: the land is no longer the Haven's.");
            }
        }

        // Every land Secure at once: the ring holds, and the safe pocket has a slot more from then on (TerritorySettings › Ring Pocket Slots).
        if (!settlement.RingHeld && AllSecure())
        {
            settlement.HoldRing();
            news.Add("Every land round the Haven is Secure at once: the boundary stones hold a ring. Your safe pocket has room for one more.");
            if (Player.Current != null && Player.Current.TryGetComponent(out ItemInventory inventory))
            {
                inventory.RefreshPocket();
            }
        }
        // A party on a land the forest has taken back comes home for good.
        foreach (LandPartyKind kind in LandParties.All)
        {
            string region = settlement.PartyOn(kind);
            if (region != null && !State.IsClaimed(region))
            {
                settlement.SetParty(kind, null);
                news.Add($"The {LandParties.Doing(kind)} party is home from {region}: the land is no longer the Haven's.");
            }
        }
        if (!pushComing && State.Incursion != null)
        {
            Tell(Settings.ForestGathers);
        }

        if (spread > 0f)
        {
            settlement.ChangeBlight(spread);
        }

        if (atDawn)
        {
            string tithe = Tithe();
            if (tithe != null)
            {
                news.Add(tithe);
            }
        }

        waiting.AddRange(news);
        if (PlaceDiscovery.Current != null)
        {
            PostWaiting();
        }

        Changed?.Invoke();
    }

    /// <summary>True when every land in the atlas is held Secure.</summary>
    public static bool AllSecure()
    {
        if (Settings == null || Settings.Regions.Count == 0)
        {
            return false;
        }

        foreach (TerritorySettings.RegionInfo region in Settings.Regions)
        {
            if (region == null || State.StandingOf(region.region, Settings) != RegionStanding.Secure)
            {
                return false;
            }
        }

        return true;
    }

    // What the outposts send home for a raid, into the chests (Outposts); in words, one line an outpost.
    private static List<string> OutpostsHome(SettlementState settlement, float raidSeconds, ResidentDefinition along, float share)
    {
        var told = new List<string>();
        foreach ((string region, ResidentRecord keeper) in Outposts.Working(settlement, raidSeconds, along, Settings))
        {
            var sent = new List<string>();
            foreach (ItemAmount brought in Outposts.Roll(region, Settings, (least, most) => UnityEngine.Random.Range(least, most + 1)))
            {
                int amount = RaidParameters.ScaleAmount(brought.Amount, share, UnityEngine.Random.value);
                int added = amount > 0 ? GameSession.Stash.Items.Add(brought.Item, amount) : 0;
                PlaytestLogger.Note(PlaytestLog.CameHome, "Outpost", added, brought.Item.Id);
                if (added > 0)
                {
                    sent.Add($"{added} {brought.Item.DisplayName}");
                }
            }

            if (sent.Count > 0)
            {
                told.Add($"From the outpost on {region}, {keeper.Resident.DisplayName} sent {string.Join(", ", sent)} home.");
            }
        }

        return told;
    }

    // What the hunting and fishing parties bring home, into the chests, and what the scouts saw; in words, one line a party.
    private static List<string> PartiesHome(List<(LandPartyKind kind, string region, ResidentRecord who)> parties, float share)
    {
        var told = new List<string>();
        foreach ((LandPartyKind kind, string region, ResidentRecord who) in parties)
        {
            string name = who.Resident.DisplayName;
            if (kind == LandPartyKind.Scout)
            {
                told.Add($"{name} scouted {region} while you were away" + (PushOn(region) ? ", and has seen the forest gathering there." : ": all quiet."));
                continue;
            }

            var sent = new List<string>();
            foreach (ItemAmount brought in LandParties.Roll(kind, who, Settings, (least, most) => UnityEngine.Random.Range(least, most + 1)))
            {
                int amount = RaidParameters.ScaleAmount(brought.Amount, share, UnityEngine.Random.value);
                int added = amount > 0 ? GameSession.Stash.Items.Add(brought.Item, amount) : 0;
                PlaytestLogger.Note(PlaytestLog.CameHome, $"Party:{kind}", added, brought.Item.Id);
                if (added > 0)
                {
                    sent.Add($"{added} {brought.Item.DisplayName}");
                }
            }

            bool worn = Settings.ErosionAt(GameSession.Home.Blight) > 0f && State.IsClaimed(region);
            told.Add((sent.Count > 0
                ? $"{name}'s {LandParties.Doing(kind)} party in {region} brought {string.Join(", ", sent)} to the chests"
                : $"{name}'s {LandParties.Doing(kind)} party in {region} came back with nothing the chests had room for")
                + (worn ? ", and the land wore less for being tended." : "."));
        }

        return told;
    }

    /// <summary>Tells what happened to the land while the player was away (on arriving home).</summary>
    public static void PostWaiting()
    {
        foreach (string line in waiting)
        {
            GameMessages.Post(line, line.Contains("taken") || line.Contains("gathering") ? MessageKind.Warning : MessageKind.Info);
        }

        waiting.Clear();
    }

    // What the Secure regions send home at dawn, into the stash.
    private static string Tithe()
    {
        var sent = new List<string>();
        bool full = false;
        foreach (WorldAtlas.Region region in Claimable)
        {
            TerritorySettings.RegionInfo info = Settings.Info(region.name);
            if (info == null || StandingOf(region.name) != RegionStanding.Secure)
            {
                continue;
            }

            foreach (ItemAmount gift in info.tithe)
            {
                if (gift.Item == null || gift.Amount <= 0)
                {
                    continue;
                }

                // What fitted: a full stash took fewer (or none), and the line named the whole gift.
                int added = GameSession.Stash.Items.Add(gift.Item, gift.Amount);
                PlaytestLogger.Note(PlaytestLog.CameHome, "Tithe", added, gift.Item.Id);
                if (added > 0)
                {
                    sent.Add($"{added} {gift.Item.DisplayName}");
                }

                if (added < gift.Amount)
                {
                    full = true;
                }
            }
        }

        string told = sent.Count > 0 ? "From the Haven's secure lands, to the stash: " + string.Join(", ", sent) + "." : null;
        if (full)
        {
            told = (told != null ? told + " " : "") + "The stash had no room for the rest of the lands' gifts.";
        }

        return told;
    }

    // ----- Tasks -----

    /// <summary>What a held region asks now ("Hunt 6 creatures in it: 2 so far"), or null.</summary>
    public static string TaskText(string region)
    {
        TerritorySettings.RegionInfo info = Settings.Info(region);
        if (info == null || !State.IsClaimed(region))
        {
            return null;
        }

        if (!State.TaskOpen(region))
        {
            return "Nothing asked for now: it will ask again after the next turn.";
        }

        string what = info.task == RegionTaskKind.Hunt ? "Bring down" : info.task == RegionTaskKind.Fish ? "Land" : "Gather";
        string things = info.task == RegionTaskKind.Hunt ? "creatures or game" : info.task == RegionTaskKind.Fish ? "fish" : "times (fell, mine or forage)";
        return $"{what} {info.taskCount} {things} in {region}: {State.Of(region).TaskProgress} so far. Hold +{Settings.TaskHold:0}, {ExperienceTracker.InsightWorth(Settings.TaskInsight)} Insight.";
    }

    private static void OnGameEvent(GameEvent happened)
    {
        if (happened.Kind == GameEventKind.RaidFinished)
        {
            Turn(false, happened.Seconds);
            return;
        }

        if (happened.Kind == GameEventKind.Killed && happened.VictimFaction != Faction.Player && happened.VictimFaction != Faction.Ally
            && Player.Current != null && happened.Actor == Player.Current.gameObject)
        {
            Vector3 at = happened.Origin != null ? happened.Origin.transform.position : Player.Current.transform.position;
            Count(RegionAt(at)?.name, RegionTaskKind.Hunt);
        }
    }

    private static void OnPractised(ProficiencyDefinition skill, float amount)
    {
        if (skill == null || Player.Current == null)
        {
            return;
        }

        RegionTaskKind kind = skill.Training == ProficiencyTraining.Fishing ? RegionTaskKind.Fish : RegionTaskKind.Gather;
        if (skill.Training == ProficiencyTraining.Gathering || skill.Training == ProficiencyTraining.Fishing)
        {
            Count(RegionAt(Player.Current.transform.position)?.name, kind);
        }
    }

    private static void Count(string region, RegionTaskKind kind)
    {
        TerritorySettings.RegionInfo info = region != null ? Settings.Info(region) : null;
        if (info == null || info.task != kind || !State.TaskOpen(region))
        {
            return;
        }

        if (HavenVisit.IsVisiting)
        {
            SendToHost(LandWork.Task, region, (int)kind);
            return;
        }

        bool wasSecure = StandingOf(region) == RegionStanding.Secure;
        if (State.CountTask(region, info.taskCount, Settings))
        {
            ExperienceTracker.GrantInsight(Settings.TaskInsight);
            GameMessages.Post($"{region} has what it needed. {region}: {Describe(region)}.", MessageKind.Gain);
            Tell(Settings.TaskDone);
            NoteSecure(region, wasSecure);
        }

        Changed?.Invoke();
    }

    // ----- The corruption's way in -----

    /// <summary>
    /// Which way a corrupted night's band comes from (degrees, 0 north): the region the forest is pushing on, else, half the time,
    /// the weakest of the Haven's lands that isn't Secure; else anywhere that isn't Secure. Secure lands keep the corruption out.
    /// </summary>
    public static float NightBearing(Vector3 centre)
    {
        WorldAtlas atlas = Atlas;
        if (atlas == null)
        {
            return UnityEngine.Random.Range(0f, 360f);
        }

        TerritoryState.Push push = State.Incursion;
        string toward = push != null ? push.Region : null;
        if (toward == null && UnityEngine.Random.value < 0.5f)
        {
            toward = Claimable.Where(r => State.IsClaimed(r.name) && StandingOf(r.name) != RegionStanding.Secure)
                .OrderBy(r => State.HoldOf(r.name)).Select(r => r.name).FirstOrDefault();
        }

        WorldAtlas.Region region = toward != null ? atlas.Regions.FirstOrDefault(r => r.name == toward) : null;
        if (region != null)
        {
            Vector2 at = region.stoneAt != Vector2.zero ? region.stoneAt : region.label;
            return HavenWorldPlan.BearingOf(at - new Vector2(centre.x, centre.z));
        }

        for (int attempt = 0; attempt < 12; attempt++)
        {
            float bearing = UnityEngine.Random.Range(0f, 360f);
            Vector3 out250 = centre + new Vector3(Mathf.Sin(bearing * Mathf.Deg2Rad), 0f, Mathf.Cos(bearing * Mathf.Deg2Rad)) * 250f;
            string there = atlas.RegionAt(out250)?.name;
            if (there == null || StandingOf(there) != RegionStanding.Secure)
            {
                return bearing;
            }
        }

        return UnityEngine.Random.Range(0f, 360f);
    }

    /// <summary>Renown the Haven's lands bring (HavenRenown).</summary>
    public static int Renown => State.Renown(Settings);

    /// <summary>The territory's turn (a dawn or a raid's end moves it on): what pins a raid's briefing (RaidPlan).</summary>
    public static int TurnNow => State.Turn;
}
