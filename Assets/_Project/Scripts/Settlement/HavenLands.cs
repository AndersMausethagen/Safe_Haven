using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// The Haven's lands for the Haven window's Lands tab (Territory): the regions held, how firmly, the one the forest is pushing on,
/// and the wild ones still to claim, each with what it is like, what it gives and what it asks. Only reads.
/// </summary>
public static class HavenLands
{
    public static List<HavenShelf> Read()
    {
        var pushed = new HavenShelf("The forest is pushing");
        var held = new HavenShelf("The Haven's lands");
        var wild = new HavenShelf("Wild");
        TerritorySettings settings = TerritorySettings.Current;
        TerritoryState state = GameSession.Here.Territory;
        foreach (WorldAtlas.Region region in Territory.Claimable)
        {
            HavenEntry entry = EntryOf(region.name, settings, state);
            bool isPushed = state.Incursion != null && state.Incursion.Region == region.name;
            (isPushed ? pushed : state.IsClaimed(region.name) ? held : wild).Entries.Add(entry);
        }

        held.Entries.Sort((a, b) => state.HoldOf(b.Name).CompareTo(state.HoldOf(a.Name)));
        return new[] { ForayShelf(), pushed, held, wild }.Where(s => s.Entries.Count > 0).ToList();
    }

    // ----- Forays through the portal (Forays) -----

    // How many the next foray takes (the strongest who are free), chosen here: kept for the session.
    private static int forayCount = 2;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => forayCount = 2;

    // The foray out, or the places a foray could go: each its odds with the party chosen, its time and what it may bring.
    private static HavenShelf ForayShelf()
    {
        var shelf = new HavenShelf("Forays through the portal");
        SettlementState home = GameSession.Home;
        ForaySettings settings = ForaySettings.Current;
        if (HavenVisit.IsVisiting || PortalTiers.Of(home) < 1 || settings.Destinations.Count == 0)
        {
            return shelf;
        }

        if (home.Foray != null)
        {
            ForayRecord foray = home.Foray;
            ForaySettings.Destination there = settings.Find(foray.Destination);
            List<ResidentRecord> party = Forays.MembersOf(home, foray);
            var entry = new HavenEntry
            {
                Key = "foray-out",
                Name = there != null ? Forays.Where(there) : foray.Destination,
                Line = $"{Forays.Names(party)} · back in {Mathf.CeilToInt(Mathf.Max(0f, foray.SecondsLeft) / 60f)} min · {foray.Chance * 100f:0}% odds",
                State = HavenEntryState.Working,
                Description = "Out through the portal, gathering on their own. Gone wrong, they bring a share home, bruised.",
            };
            if (there != null)
            {
                entry.Details.Add(new HavenDetail("May bring", Forays.Finds(there)));
            }

            entry.Actions.Add(new HavenAction("Call them home", () => Forays.CallHome(GameSession.Home), true, closesWindow: false, primary: false));
            shelf.Entries.Add(entry);
            return shelf;
        }

        TerritorySettings lands = TerritorySettings.Current;
        List<ResidentRecord> free = Forays.Strongest(home, int.MaxValue, settings, lands);
        int count = Mathf.Clamp(forayCount, 1, Mathf.Max(1, Mathf.Min(settings.MostMembers, free.Count)));
        List<ResidentRecord> chosen = free.GetRange(0, Mathf.Min(count, free.Count));
        float power = Forays.PowerOf(chosen, settings);
        foreach (ForaySettings.Destination place in settings.Destinations)
        {
            if (place == null)
            {
                continue;
            }

            bool open = PortalTiers.Of(home) >= place.tier;
            string why = Forays.WhyNot(home, place, chosen, GameSession.Stash != null ? GameSession.Stash.Items : null, false, settings);
            var entry = new HavenEntry
            {
                Key = "foray-" + place.id,
                Name = Forays.Where(place),
                Line = open ? $"{Forays.Chance(power, place.power, settings) * 100f:0}% · {Mathf.CeilToInt(Forays.Seconds(power, place, settings) / 60f)} min · power {power:0} of {place.power:0}"
                    : $"Opens with the portal's Tier {Tiers.ToRoman(place.tier)}",
                State = open ? HavenEntryState.Good : HavenEntryState.Locked,
            };
            if (open)
            {
                entry.Details.Add(new HavenDetail("May bring", Forays.Finds(place)));
                entry.Details.Add(new HavenDetail(chosen.Count == 1 ? "Who goes" : $"Who go ({chosen.Count})",
                    chosen.Count > 0 ? $"{Forays.Names(chosen)}: {power:0} power; each takes food for the road" : "Nobody is free"));
                entry.Actions.Add(new HavenAction($"Send {(chosen.Count == 1 ? "them" : $"the {chosen.Count}")}", () => Send(place, count), why == null,
                    closesWindow: false, primary: false));
                if (count < Mathf.Min(settings.MostMembers, free.Count))
                {
                    entry.Actions.Add(new HavenAction("One more", () => forayCount = count + 1, true, closesWindow: false, primary: false));
                }

                if (count > 1)
                {
                    entry.Actions.Add(new HavenAction("One fewer", () => forayCount = count - 1, true, closesWindow: false, primary: false));
                }

                if (why != null)
                {
                    entry.Details.Add(new HavenDetail("Not now", why));
                }
            }

            shelf.Entries.Add(entry);
        }

        return shelf;
    }

    private static void Send(ForaySettings.Destination place, int count)
    {
        SettlementState home = GameSession.Home;
        List<ResidentRecord> party = Forays.Strongest(home, count, ForaySettings.Current, TerritorySettings.Current);
        Forays.Send(home, place, party, GameSession.Stash != null ? GameSession.Stash.Items : null, ForaySettings.Current);
    }

    /// <summary>"3 of 11 held · the forest pushes on Ravenwood in 2 turns".</summary>
    public static string Summary()
    {
        TerritoryState state = GameSession.Here.Territory;
        int all = Territory.Claimable.Count();
        if (!Territory.Unlocked)
        {
            QuestDefinition opener = TerritorySettings.Current.UnlockedBy;
            bool called = opener != null && GameSession.Quests.StatusOf(opener) != QuestStatus.NotStarted;
            return $"{all} regions beyond the meadows, each with a cold boundary stone · " +
                (called ? "the portal wants a word with you about them" : "the portal will speak of them once you have been through it a few times");
        }

        string summary = $"{state.ClaimedCount} of {all} regions held · {Territory.Renown} renown from them";
        if (state.Incursion != null)
        {
            int turns = System.Math.Max(0, state.Incursion.DueTurn - state.Turn);
            summary += $" · the forest pushes on {state.Incursion.Region} in {turns} turn{(turns == 1 ? "" : "s")}";
        }

        return summary;
    }

    private static HavenEntry EntryOf(string region, TerritorySettings settings, TerritoryState state)
    {
        TerritorySettings.RegionInfo info = settings.Info(region);
        RegionStanding standing = Territory.StandingOf(region);
        bool isPushed = state.Incursion != null && state.Incursion.Region == region;
        var entry = new HavenEntry
        {
            Key = region,
            Name = region,
            Line = Territory.Describe(region),
            Status = Territory.Describe(region),
            Description = info != null ? info.description : "",
            State = isPushed || standing == RegionStanding.Slipping ? HavenEntryState.Attention
                : standing == RegionStanding.Wild ? HavenEntryState.Locked : HavenEntryState.Good,
        };

        if (info != null)
        {
            entry.Details.Add(new HavenDetail("Danger", info.danger.ToString()));
            entry.Details.Add(new HavenDetail("What it gives", info.gives));
        }

        entry.Details.Add(new HavenDetail("Places found", $"{Territory.PlacesFound(region)} of {Territory.PlacesIn(region).Count}"));
        if (isPushed)
        {
            TerritoryState.Push push = state.Incursion;
            entry.Details.Add(new HavenDetail("The forest's push",
                $"Comes in {System.Math.Max(0, push.DueTurn - state.Turn)} turns and would take {push.Remaining:0} hold. Stand watch at its boundary stone to break it, " +
                "or burn the rot's heart on a raid (the portal's briefing shows where): each one halves it."));
        }

        string task = Territory.TaskText(region);
        if (task != null)
        {
            entry.Details.Add(new HavenDetail("What it asks", task));
        }

        if (info != null && info.tithe.Count > 0)
        {
            string tithe = string.Join(", ", info.tithe.Where(t => t.Item != null).Select(t => $"{t.Amount} {t.Item.DisplayName}"));
            entry.Details.Add(new HavenDetail(standing == RegionStanding.Secure ? "Sends home each dawn" : "Would send home each dawn, once Secure", tithe));
        }

        if (standing == RegionStanding.Wild)
        {
            entry.Details.Add(new HavenDetail("To claim it", !Territory.Unlocked
                ? "Not yet: its boundary stone is cold. The portal knows what the stones are."
                : $"Find one of its places, then light its boundary stone{(state.ClaimNeedsRune(settings) ? " with a Pact Rune" : " (the first is free)")}."));
        }
        else
        {
            AddParties(entry, region, settings);
            AddOutpost(entry, region, settings);
        }

        return entry;
    }

    // The outpost (Outposts): who keeps it and what it sends home, or what raising one would bring and cost. Only on a land that has
    // something worth one.
    private static void AddOutpost(HavenEntry entry, string region, TerritorySettings settings)
    {
        TerritorySettings.RegionInfo info = settings.Info(region);
        if (info == null || info.outpost.Count == 0)
        {
            return;
        }

        IHavenView home = GameSession.Here;
        bool visiting = HavenVisit.IsVisiting;
        string odds = Outposts.Odds(region, settings);
        if (!string.IsNullOrEmpty(home.OutpostKeeper(region)))
        {
            ResidentRecord keeper = Outposts.KeeperOf(home, region);
            string paused = Outposts.Paused(home, region, settings);
            string name = keeper != null ? keeper.Resident.DisplayName : null;
            entry.Details.Add(new HavenDetail("The outpost", name == null ? $"Nobody keeps it now. Kept, it sends {odds} a raid."
                : $"{name} keeps it. {EachRaid(settings)}: {odds}." + (paused != null ? " " + paused : $" {name}'s own work waits meanwhile.")));
            ResidentRecord next = Outposts.Candidate(home, settings, keeper);
            if (next != null)
            {
                entry.Actions.Add(new HavenAction($"Send {next.Resident.DisplayName} to keep the outpost", () => GameSession.Home.SetOutpost(region, next.Resident.Id),
                    !visiting, closesWindow: false, primary: false));
            }

            entry.Actions.Add(new HavenAction("Give the outpost up", () => GameSession.Home.SetOutpost(region, null), !visiting, closesWindow: false, primary: false));
            return;
        }

        string why = Outposts.WhyNot(region, visiting, home, settings);
        ResidentRecord who = Outposts.Candidate(home, settings);
        entry.Details.Add(new HavenDetail("An outpost", $"{EachRaid(settings)}: {odds}, kept by {(who != null ? who.Resident.DisplayName : "someone free")}. " +
            $"Costs {AmountText.Describe(settings.OutpostCost)}." + (why != null ? $" {why}" : "")));
        entry.Actions.Add(new HavenAction("Raise an outpost", () => RaiseOutpost(region, settings), why == null, closesWindow: false, primary: false));
    }

    private static void RaiseOutpost(string region, TerritorySettings settings)
    {
        IHavenView home = GameSession.Here;
        string why = Outposts.WhyNot(region, HavenVisit.IsVisiting, home, settings);
        ResidentRecord who = Outposts.Candidate(home, settings);
        Player player = Player.Current;
        if (why != null || who == null || player == null)
        {
            GameMessages.Post(why ?? "Nobody is free to keep it.", MessageKind.Warning);
            return;
        }

        MaterialPool pool = MaterialPool.For(player.gameObject);
        var none = new List<ResourceAmount>();
        if (!pool.CanAfford(none, settings.OutpostCost, out string missing))
        {
            GameMessages.Post($"An outpost takes {AmountText.Describe(settings.OutpostCost)}. Missing {missing}", MessageKind.Warning);
            return;
        }

        if (pool.TrySpend(none, settings.OutpostCost))
        {
            GameSession.Home.SetOutpost(region, who.Resident.Id);
            GameMessages.Post($"An outpost stands on {region}. {who.Resident.DisplayName} goes out to keep it on your raids.", MessageKind.Gain);
        }
    }

    // "Each raid of 3 minutes or more": shorter ones are no day's hunting.
    private static string EachRaid(TerritorySettings settings) =>
        $"Each raid of {Mathf.Max(1, Mathf.RoundToInt(settings.PartyMinSeconds / 60f))} minutes or more";

    // The parties (LandParties): who goes out to this land on raids, what they bring, and the orders to send or call them home. Only the
    // kinds someone here can lead are offered.
    private static void AddParties(HavenEntry entry, string region, TerritorySettings settings)
    {
        IHavenView home = GameSession.Here;
        bool visiting = HavenVisit.IsVisiting;
        foreach (LandPartyKind kind in LandParties.All)
        {
            TerritorySettings.PartyInfo info = settings.PartyOf(kind);
            ResidentRecord who = LandParties.WhoGoes(home, kind, null, settings);
            if (info == null || info.job == null || who == null)
            {
                continue;
            }

            string doing = LandParties.Doing(kind);
            string name = who.Resident.DisplayName;
            string there = home.PartyOn(kind);
            if (there == region)
            {
                entry.Details.Add(new HavenDetail($"{name}, {doing} here", $"{EachRaid(settings)}: {LandParties.Odds(kind, who, settings)}. {name}'s own work waits meanwhile."));
                entry.Actions.Add(new HavenAction($"Call the {doing} party home", () => GameSession.Home.SetParty(kind, null), !visiting,
                    closesWindow: false, primary: false));
                continue;
            }

            string why = LandParties.WhyNot(kind, region, visiting, home, settings);
            string label = there != null ? $"Send the {doing} party here" : $"Send {name} {doing}";
            entry.Actions.Add(new HavenAction(label, () => GameSession.Home.SetParty(kind, region), why == null, closesWindow: false, primary: false));
            if (why == null)
            {
                entry.Details.Add(new HavenDetail($"If {name} went {doing}", $"{EachRaid(settings)}: {LandParties.Odds(kind, who, settings)}" +
                    (there != null ? $" (now in {there})." : ".")));
            }
        }
    }
}
