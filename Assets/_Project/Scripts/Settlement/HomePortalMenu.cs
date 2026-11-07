using System.Text;
using UnityEngine;

/// <summary>
/// What the portal offers when used: what it says, then going through it (its RaidPortal's actions), Portal Sparks at
/// its fire, strengthening the ward, the defense against a breach, mending buildings and the Haven page. One menu for
/// everything the portal is the centre of; the portal itself is the ward and the voice. Plain C#, made by
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
