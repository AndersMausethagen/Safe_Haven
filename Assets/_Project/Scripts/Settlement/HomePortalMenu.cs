using System.Text;
using UnityEngine;

/// <summary>
/// What the portal offers when used: what it says, then going through it (its RaidPortal's actions), the way to friends'
/// Havens (calling friends here, or walking to one whose door is open: co-op, told as the portal reaching other doors),
/// Portal Sparks at its fire, strengthening the ward, the defense against a breach, mending buildings and the Haven page.
/// One menu for everything the portal is the centre of; the portal itself is the ward and the voice. Plain C#, made by
/// <see cref="HomePortal"/>.
/// </summary>
public sealed class HomePortalMenu
{
    private readonly HomePortal portal;
    private readonly RaidPortal raid;
    private readonly CraftingStation station;

    public HomePortalMenu(HomePortal portal, RaidPortal raid, CraftingStation station)
    {
        this.portal = portal;
        this.raid = raid;
        this.station = station;
    }

    public MenuPage Build(GameObject user)
    {
        SettlementSettings settings = SettlementSettings.Current;
        IHavenView settlement = GameSession.Here;
        UIStyle style = UIStyle.Current;
        var body = new StringBuilder();
        // What it says is its own (MenuPage.Story: PortalWindow shows it apart, in italics); the rest is what it tells.
        string line = portal.SpokenLine();
        // The portal's tier first: the one ladder the Haven's progress is shown on (PortalTiers). The whole meadow can be built
        // on (2026-11-12); the ward is the sheltered ground, where the night's prowlers don't come.
        int tier = PortalTiers.Of(settlement);
        body.Append($"<color={UIStyle.Hex(style.Accent)}>Portal {PortalTiers.Title(tier)}.</color> {PortalTiers.Line(tier)}");
        body.Append($"\n<color={UIStyle.Hex(style.Muted)}>The ward shelters {portal.WardRadius:0} m around the portal.</color>");
        WardBreachDirector breach = WardBreachDirector.Current;
        if (breach != null && breach.IsPending)
        {
            body.Append($"\n\n<color={UIStyle.Hex(style.Warning)}>").Append(breach.Definition.PendingText).Append("</color>");
        }

        if (settlement.WardStrain > 0)
        {
            int raids = settlement.WardStrain;
            body.Append($"\n\n<color={UIStyle.Hex(style.Warning)}>The ward is strained. It will mend after {raids} more successful raid{(raids == 1 ? "" : "s")}.</color>");
        }

        bool sleeping = raid != null && !raid.IsRepaired;
        if (sleeping)
        {
            body.Append("\n\n").Append(raid.RuinText);
        }

        var page = new MenuPage { Title = sleeping ? "The Sleeping Portal" : "The Portal", Story = line ?? "", Body = body.ToString() };
        // What most visits are for comes first: going through it.
        if (raid != null)
        {
            raid.AddActions(page, user);
            foreach (MenuAction action in page.Actions)
            {
                if (action.Label.StartsWith("Plan a raid") || action.Label.StartsWith("Follow"))
                {
                    action.Icon = "Lead";
                }
                else if (action.Label.StartsWith("Improve") || action.Label.StartsWith("Repair") || action.Label.StartsWith("Wake"))
                {
                    action.Icon = "Hammer";
                    action.Status = action.Enabled ? "" : "Not enough";
                }
            }
        }

        AddOtherHavens(page, user);
        if (station != null && station.Usable)
        {
            page.Add(new MenuAction("Make Portal Sparks", "Queue them at the portal's own fire", true, () => station.OpenMenu(user), closesMenu: true) { Icon = "Spark" });
        }

        if (portal.HasMoreToSay)
        {
            page.Add(new MenuAction("Listen", "It has more to tell", true, portal.SayNext) { Icon = "Book" });
        }

        int level = settlement.WardLevel;
        SettlementSettings.WardLevel next = sleeping ? null : PortalTiers.Next(settlement);
        // Not offered before the portal has said what raising it means (WardLevel › Introduced By: Steady the Door's first step).
        if (next != null && !PortalTiers.Introduced(next))
        {
            next = null;
        }
        if (next != null)
        {
            bool affordable = MaterialPool.For(user).CanAfford(next.cost, null, out string missing);
            // Beyond its materials a tier asks one or two things (decision 8.4): those show in place of the cost until they stand.
            string needs = PortalTiers.Missing(next, settlement);
            string opens = string.IsNullOrEmpty(next.opens) ? "" : $". {next.opens}";
            // What is missing shows as its state on the right ("Not enough"), so the line keeps to what it costs and gives.
            string detail = needs.Length > 0 ? needs : $"{AmountText.Describe(next.cost)}  →  ward {settings.WardRadiusAt(level + 1):0} m{opens}";
            bool open = needs.Length == 0;
            page.Add(new MenuAction($"Raise the portal to {PortalTiers.Title(tier + 1)}", detail, affordable && open, () => StrengthenWard(user))
                { Icon = "Barding", Status = !open ? "Needs more" : affordable ? "" : "Not enough" });
        }

        if (breach != null && breach.IsPending)
        {
            // A guest's press did nothing (the host's game holds the breach): shown greyed with why. Not on a corrupted night either.
            bool corrupted = CorruptionNights.Current != null && CorruptionNights.Current.IsActive;
            string why = HavenVisit.IsVisiting ? "Your friend begins their own defense" : corrupted ? "Not while the corrupted night is on you" : breach.Hint;
            page.Add(new MenuAction("Begin the defense", why, !HavenVisit.IsVisiting && !corrupted, breach.Begin, closesMenu: true) { Icon = "Sword", Status = "Now" });
        }

        MenuAction mend = BuildingRepairs.MendAction(user);
        if (mend != null)
        {
            page.Add(mend);
        }

        page.Add(new MenuAction("How is Safe Haven doing?", $"Rank, renown and what to do next ({GameInput.KeyLabel(GameInput.Haven)})", true, OpenHaven) { Group = "Haven" });
        return page;
    }

    // Co-op through the portal: it hears other portals in other clearings, and can call friends here or carry the player to
    // a friend whose way stands open (both through Steam).
    private void AddOtherHavens(MenuPage page, GameObject user)
    {
        CoopSession coop = CoopSession.Current;
        if (coop == null || raid != null && !raid.IsRepaired)
        {
            return;
        }

        string noSteam = $"<color={UIStyle.Hex(UIStyle.Current.Danger)}>I can't reach the other doors: Steam isn't running.</color>";
        switch (coop.State)
        {
            case CoopState.Offline:
                page.Add(new MenuAction("Call friends to Safe Haven", coop.SteamReady ? "I'll open my way and call to them" : noSteam, coop.SteamReady,
                    () => CallFriends(coop), closesMenu: true));
                page.Add(new MenuAction("Walk to a friend's Haven", coop.SteamReady ? "I hear other doors. Name one, and I'll carry you" : noSteam, coop.SteamReady,
                    () => ActionMenuWindow.Current.Open(() => FriendsPage(user, coop), user, portal.transform), closesMenu: true) { Icon = "Lead" });
                break;
            case CoopState.Hosting:
                page.Add(new MenuAction("Call another friend", $"My way stands open ({coop.PlayerCount} here)", coop.ViaSteam, () => coop.InviteFriends()));
                page.Add(new MenuAction("Close the way", "Friends here go home", true, () => coop.Leave(), closesMenu: true));
                break;
            case CoopState.Joined:
                page.Add(new MenuAction("Walk home", "Back through to your own Haven", true, () => coop.Leave(), closesMenu: true));
                break;
        }
    }

    private static void CallFriends(CoopSession coop)
    {
        if (!coop.Host(out string problem))
        {
            GameMessages.Post(string.IsNullOrEmpty(problem) ? "The portal can't open its way just now." : problem, MessageKind.Warning);
            return;
        }

        GameMessages.Post("The portal hums, and its way stands open. Your friends can walk through to you.", MessageKind.Gain);
        coop.InviteFriends();
    }

    // The friends whose door is open now, rebuilt while it shows, so one who opens theirs appears.
    private MenuPage FriendsPage(GameObject user, CoopSession coop)
    {
        var page = new MenuPage { Title = "Other Havens", Body = "I hear other doors, faint, like bells under water. These ones stand open." };
        var friends = coop.FriendsHosting();
        if (friends.Count == 0)
        {
            page.Body = "I hear other doors, faint, like bells under water. None of your friends' stands open just now. " +
                        "When one of them opens their way, it will ring here.";
        }

        foreach (SteamLobby.FriendHaven friend in friends)
        {
            ulong lobby = friend.Lobby;
            string title = friend.IsHost ? $"{friend.Name}'s Haven" : $"The Haven {friend.Name} is in";
            string with = friend.IsHost && friend.Friends != friend.Name ? $"Walk through to them ({friend.Friends} there)" : "Walk through to them";
            page.Add(new MenuAction(title, with, true, () => coop.Visit(lobby), closesMenu: true));
        }

        page.Add(new MenuAction("Back", null, true, () => { ActionMenuWindow.Current.Close(); PortalWindow.Show(() => Build(user), user, portal.transform, 6f); }));
        return page;
    }

    // The ward's next level, paid from what the player has at hand: a visitor helps raise their friend's (HavenDecisions).
    private void StrengthenWard(GameObject user)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int level = GameSession.Here.WardLevel;
        if (level >= settings.WardLevels.Count)
        {
            return;
        }

        string needs = PortalTiers.Missing(settings.WardLevels[level], GameSession.Here);
        if (needs.Length > 0)
        {
            GameMessages.Post(needs, MessageKind.Warning);
            return;
        }

        if (!MaterialPool.For(user).TrySpend(settings.WardLevels[level].cost))
        {
            return;
        }

        HavenDecisions.Do("ward.raise", level.ToString(System.Globalization.CultureInfo.InvariantCulture), settings.WardLevels[level].cost);
    }

    // The ward raised from the level the player saw (another may have raised it meanwhile).
    [HavenDecision("ward.raise")]
    private static HavenDecisions.Outcome RaiseWard(string fromLevel)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int level = GameSession.Here.WardLevel;
        if (!int.TryParse(fromLevel, out int seen) || seen != level || level >= settings.WardLevels.Count)
        {
            return HavenDecisions.Outcome.Refused("The ward was raised meanwhile: what you gave is back in your bag.");
        }

        string needs = PortalTiers.Missing(settings.WardLevels[level], GameSession.Here);
        if (needs.Length > 0)
        {
            return HavenDecisions.Outcome.Refused(needs + " What you gave is back in your bag.");
        }

        GameSession.Home.RaiseWardLevel();
        GameEvents.Raise(new GameEvent(GameEventKind.PortalRaised, null, PortalTiers.Of(GameSession.Home)));
        string opens = settings.WardLevels[level].opens;
        float reach = settings.WardRadiusAt(GameSession.Here.WardLevel);
        string tier = PortalTiers.Title(PortalTiers.Current);
        return HavenDecisions.Outcome.Made($"The portal glows brighter: it stands at {tier}. The ward now reaches {reach:0} m.{(string.IsNullOrEmpty(opens) ? "" : " " + opens)}");
    }

    private static void OpenHaven()
    {
        if (HavenWindow.Current != null)
        {
            HavenWindow.Current.Open();
        }
    }
}
