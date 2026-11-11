using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The Haven's Reach in play (Docs/Territory.md): TerritoryState's rules tied to the game. A turn passes at each dawn at home
/// (Seasons) and whenever a raid ends; what a turn brings is told at once at home, or when the player is next home. Stones are
/// lit and fed Pact Runes here (BoundaryStone), a watch held at one breaks a push (StoneDefence), a rot's heart burned on a raid
/// weakens it (WeakenIncursionOutcome). Held regions ask a task of the Haven (hunting, gathering, fishing there), counted from
/// the game's own events; places found (PlaceDiscovery) give Insight and strengthen the hold. Secure regions send a tithe home at
/// dawn, and the corruption's bands come from the weakest of the Haven's lands (CorruptionNights).
/// </summary>
public static class Territory
{
    public const string HomeScene = "SafeHaven";

    private static readonly List<string> waiting = new List<string>();
    private static WorldAtlas haven;
    private static Dictionary<string, List<WorldAtlas.Place>> placesByRegion;

    /// <summary>Raised whenever the hold on any region changes.</summary>
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Listen()
    {
        GameEvents.Raised -= OnGameEvent;
        GameEvents.Raised += OnGameEvent;
        ExperienceTracker.Practised -= OnPractised;
        ExperienceTracker.Practised += OnPractised;
        waiting.Clear();
        haven = null;
        placesByRegion = null;
        Changed = null;
    }

    private static TerritoryState State => GameSession.Settlement.Territory;
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

        int needed = Math.Min(settings.PlacesToClaim, PlacesIn(region).Count);
        if (PlacesFound(region) < needed)
        {
            reason = $"The stone doesn't know you. Find {needed} of {region}'s places first ({PlacesFound(region)} found).";
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

        if (SpendRune(user))
        {
            State.Raise(region, Settings.RuneHold);
            GameMessages.Post($"The rune sinks into the stone. {region}: {Describe(region)}.", MessageKind.Gain);
            Changed?.Invoke();
        }
    }

    public static int RunesHeld(GameObject user) =>
        Settings.PactRune != null && user != null ? MaterialPool.For(user).Have(Settings.PactRune) : 0;

    private static bool HasRune(GameObject user) => RunesHeld(user) > 0;

    private static bool SpendRune(GameObject user) =>
        MaterialPool.For(user).TrySpend(new List<ResourceAmount>(), new[] { new ItemAmount(Settings.PactRune, 1) });

    /// <summary>A watch held at a stone (StoneDefence).</summary>
    public static void Defended(string region)
    {
        if (State.Defended(region, Settings))
        {
            ExperienceTracker.GrantInsight(Settings.DefenceInsight);
            GameMessages.Post($"The push on {region} is broken. {region}: {Describe(region)}.", MessageKind.Gain);
            Changed?.Invoke();
        }
    }

    /// <summary>A rot's heart burned on a raid (WeakenIncursionOutcome).</summary>
    public static void HeartBurned()
    {
        if (State.Weaken(Settings))
        {
            TerritoryState.Push push = State.Incursion;
            GameMessages.Post(push.Weakened >= 0.999f
                ? $"The rot's heart burns. The forest's push on {push.Region} has nothing left behind it."
                : $"The rot's heart burns. The forest's push on {push.Region} falters.", MessageKind.Gain);
            Changed?.Invoke();
        }
    }

    /// <summary>A named place found (PlaceDiscovery): Insight, and a firmer hold on its region if it is the Haven's.</summary>
    public static void PlaceFound(WorldAtlas.Place place)
    {
        ExperienceTracker.GrantInsight(Settings.FindInsight);
        string region = Atlas?.RegionAt(new Vector3(place.at.x, 0f, place.at.y))?.name;
        if (State.IsClaimed(region))
        {
            State.Raise(region, Settings.FindHold);
        }

        Changed?.Invoke();
    }

    // ----- Turns -----

    /// <summary>A turn has passed: a dawn at home, or a raid ended. What it brings is told now at home, or on coming home.</summary>
    public static void Turn(bool atDawn)
    {
        if (HavenVisit.IsVisiting || Atlas == null)
        {
            return;
        }

        SettlementState settlement = GameSession.Settlement;
        List<string> news = State.AdvanceTurn(Settings, settlement.Blight, () => UnityEngine.Random.value, out float spread);
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
        foreach (WorldAtlas.Region region in Claimable)
        {
            TerritorySettings.RegionInfo info = Settings.Info(region.name);
            if (info == null || StandingOf(region.name) != RegionStanding.Secure)
            {
                continue;
            }

            foreach (ItemAmount gift in info.tithe)
            {
                if (gift.Item != null && gift.Amount > 0 && GameSession.Stash.Items.Add(gift.Item, gift.Amount) > 0)
                {
                    sent.Add($"{gift.Amount} {gift.Item.DisplayName}");
                }
            }
        }

        return sent.Count > 0 ? "From the Haven's secure lands, to the stash: " + string.Join(", ", sent) + "." : null;
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
        return $"{what} {info.taskCount} {things} in {region}: {State.Of(region).TaskProgress} so far. Hold +{Settings.TaskHold:0}, {Settings.TaskInsight} Insight.";
    }

    private static void OnGameEvent(GameEvent happened)
    {
        if (happened.Kind == GameEventKind.RaidFinished)
        {
            Turn(false);
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

        if (State.CountTask(region, info.taskCount, Settings))
        {
            ExperienceTracker.GrantInsight(Settings.TaskInsight);
            GameMessages.Post($"{region} has what it needed. {region}: {Describe(region)}.", MessageKind.Gain);
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
}
