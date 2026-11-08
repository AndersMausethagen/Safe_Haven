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
        SettlementState settlement = GameSession.Settlement;
        UIStyle style = UIStyle.Current;
        var body = new StringBuilder();
        string line = portal.SpokenLine();
        if (line != null)
        {
            body.Append(line).Append("\n\n");
        }

        body.Append($"<color={UIStyle.Hex(style.Accent)}>The ward shelters {portal.WardRadius:0} m around the portal. You can build inside it.</color>");
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

        var page = new MenuPage { Title = sleeping ? "The Sleeping Portal" : "The Portal", Body = body.ToString() };
        // What most visits are for comes first: going through it.
        if (raid != null)
        {
            raid.AddActions(page, user);
        }

        AddOtherHavens(page, user);
        if (station != null && station.Usable)
        {
            page.Add(new MenuAction("Make Portal Sparks", "Queue them at the portal's own fire", true, () => station.OpenMenu(user), closesMenu: true));
        }

        if (portal.HasMoreToSay)
        {
            page.Add(new MenuAction("Listen", null, true, portal.SayNext));
        }

        int level = settlement.WardLevel;
        if (level < settings.WardLevels.Count)
        {
            SettlementSettings.WardLevel next = settings.WardLevels[level];
            bool affordable = MaterialPool.For(user).CanAfford(next.cost, null, out string missing);
            string opens = string.IsNullOrEmpty(next.opens) ? "" : $". {next.opens}";
            string detail = $"{AmountText.Describe(next.cost)}  →  ward {settings.WardRadiusAt(level + 1):0} m{opens}" + (affordable ? "" : $"   <color={UIStyle.Hex(style.Danger)}>{missing}</color>");
            page.Add(new MenuAction("Strengthen the ward", detail, affordable, () => StrengthenWard(user)));
        }

        if (breach != null && breach.IsPending)
        {
            page.Add(new MenuAction("Begin the defense", breach.Hint, true, breach.Begin, closesMenu: true));
        }

        MenuAction mend = BuildingRepairs.MendAction(user);
        if (mend != null)
        {
            page.Add(mend);
        }

        page.Add(new MenuAction("How is Safe Haven doing?", $"Rank, renown and what to do next ({GameInput.KeyLabel(GameInput.Haven)})", true, OpenHaven));
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
                    () => ActionMenuWindow.Current.Open(() => FriendsPage(user, coop), user, portal.transform)));
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
            page.Add(new MenuAction($"{friend.Name}'s Haven", "Walk through to them", true, () => coop.Visit(lobby), closesMenu: true));
        }

        page.Add(new MenuAction("Back", null, true, () => ActionMenuWindow.Current.Open(() => Build(user), user, portal.transform)));
        return page;
    }

    // The ward's next level, paid from what the player has at hand. A friend's ward is theirs to raise.
    private void StrengthenWard(GameObject user)
    {
        if (HavenVisit.LeftToHost())
        {
            return;
        }

        SettlementSettings settings = SettlementSettings.Current;
        int level = GameSession.Settlement.WardLevel;
        if (level >= settings.WardLevels.Count || !MaterialPool.For(user).TrySpend(settings.WardLevels[level].cost))
        {
            return;
        }

        GameSession.Settlement.RaiseWardLevel();
        string opens = settings.WardLevels[level].opens;
        GameMessages.Post($"The portal glows brighter. The ward now reaches {portal.WardRadius:0} m.{(string.IsNullOrEmpty(opens) ? "" : " " + opens)}", MessageKind.Gain);
    }

    private static void OpenHaven()
    {
        if (HavenWindow.Current != null)
        {
            HavenWindow.Current.Open();
        }
    }
}
