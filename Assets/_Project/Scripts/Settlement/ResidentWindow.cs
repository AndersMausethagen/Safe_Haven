using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A resident's page (the user's playtest list, 2026-11-15: "an NPC window like the horse window"): their live picture,
/// name, job and rank, and tabs: Overview (who they are, their jobs), Home (the house they sleep in, what it is like, and
/// every house they could move into: the player's choice wins, "Let them choose" gives it back), Work (where they work, how
/// far from home, what they keep making) and Story (raids along, lessons). Opened from the Haven window's People tab and
/// from talking to them. While it is open the player stands still (PlayerHold).
/// <para>Made of the UI kit (HearthKit, KitParts) like HorsePanelWindow. Built in code the first time it is opened.</para>
/// </summary>
public sealed class ResidentWindow : MonoBehaviour
{
    private const float Width = 1180f;
    private const float Height = 900f;
    private const float Margin = 34f;
    private const float BodyTop = 384f;
    private const float BodyHeight = Height - BodyTop - Margin;
    private const float RefreshSeconds = 0.5f;
    private static readonly string[] TabNames = { "Overview", "Home", "Work", "Story" };

    private enum Tab
    {
        Overview = 0,
        Home = 1,
        Work = 2,
        Story = 3,
    }

    private static ResidentWindow current;

    private UIWindow window;
    private RectTransform panel;
    private ResidentDefinition who;
    private float nextRefresh;
    private Tab tab;

    private KitPortrait portrait;
    private Image still;
    private TMP_Text nameLabel;
    private TMP_Text subtitle;
    private TMP_Text summary;
    private readonly KitStatRow[] headerRows = new KitStatRow[4];
    private KitTabs tabs;
    private readonly RectTransform[] pages = new RectTransform[TabNames.Length];

    private TMP_Text about;
    private KitChips chips;
    private KitActionList jobList;

    private KitSlotCard houseCard;
    private TMP_Text houseLine;
    private TMP_Text qualityTitle;
    private TMP_Text qualityLines;
    private TMP_Text bonusLine;
    private readonly KitButton[] comfortButtons = new KitButton[2];
    private KitActionList houseList;

    private KitSlotCard workCard;
    private TMP_Text workLine;
    private readonly KitFactRow[] orderRows = new KitFactRow[5];
    private KitActionList workActions;

    private readonly KitFactRow[] storyRows = new KitFactRow[7];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>True while it is open.</summary>
    public static bool IsOpen => current != null && current.window != null && current.window.IsOpen;

    /// <summary>Opens it on someone who lives here, at <paramref name="first"/>'s tab.</summary>
    public static void Show(ResidentDefinition resident, string first = null)
    {
        if (resident == null || GameSession.Here.Find(resident) == null)
        {
            return;
        }

        if (current == null)
        {
            var root = new GameObject("Resident Window");
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 205;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            current = root.AddComponent<ResidentWindow>();
            current.Build(root.transform);
            root.SetActive(true);
        }

        current.who = resident;
        // Open first: its labels are measured as they are filled, which they can't be while hidden.
        current.window.Open();
        HearthKit.FitToScreen(current.panel, (RectTransform)current.transform, Width, Height);
        int index = System.Array.IndexOf(TabNames, first);
        current.ShowTab(index >= 0 ? (Tab)index : Tab.Overview);
    }

    // ----- Building it -----

    private void Build(Transform root)
    {
        panel = HearthKit.Window(root, Width, Height, () => window.Close(), out RectTransform content);
        BuildHeader();
        tabs = new KitTabs(panel, Margin + 8f, 312f, 178f, 54f, TabNames, i => ShowTab((Tab)i));
        HearthKit.Rule(panel, Margin, 370f, Width - Margin * 2f);
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i] = CodeUI.At(TabNames[i] + " Page", panel, Margin, BodyTop, Width - Margin * 2f, BodyHeight);
        }

        BuildOverview(pages[(int)Tab.Overview]);
        BuildHome(pages[(int)Tab.Home]);
        BuildWork(pages[(int)Tab.Work]);
        BuildStory(pages[(int)Tab.Story]);

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.ResidentPanel, content.gameObject);
        window.OpenChanged += open =>
        {
            PlayerHold.Set(this, open);
            if (!open && portrait != null)
            {
                portrait.Hide();
            }
        };
        PadTabs.Register(this, () => window != null && window.IsOpen, step => ShowTab((Tab)PadTabs.Next((int)tab, TabNames.Length, step)),
            () => tabs.Rects);
    }

    private void BuildHeader()
    {
        UIStyle style = UIStyle.Current;
        portrait = KitPortrait.Make(panel, "Picture", Margin + 8f, Margin + 8f, 330f, 250f, "CharacterBackdrop");
        // Asleep indoors there is nobody to draw: their painted portrait stands in.
        still = CodeUI.At("Still", portrait.Frame, 12f, 12f, 306f, 226f).gameObject.AddComponent<Image>();
        still.preserveAspect = true;
        still.raycastTarget = false;

        float x = Margin + 372f;
        nameLabel = HearthKit.DisplayName(panel, x, Margin - 6f, 420f);
        subtitle = CodeUI.Label("Subtitle", panel, x, Margin + 82f, 400f, 30f, 21f, style.Accent);
        CodeUI.Hairline(panel, x, Margin + 120f, 380f, 1f, style);
        summary = CodeUI.Paragraph("Summary", panel, x, Margin + 134f, 384f, 120f, 20f, style.Muted);
        string[] names = { "Home", "Work", "Rank", "Raids" };
        string[] icons = { "House", "Hammer", "Star", "Sword" };
        for (int i = 0; i < names.Length; i++)
        {
            headerRows[i] = new KitStatRow(panel, Width - Margin - 360f, Margin + 14f + i * 58f, 352f, names[i], icons[i], i == 0 || i == 2, 21f);
        }
    }

    private void BuildOverview(RectTransform page)
    {
        UIStyle style = UIStyle.Current;
        const float leftWidth = 548f;
        RectTransform left = HearthKit.Card(page, "About Card", 0f, 0f, leftWidth, BodyHeight);
        HearthKit.Section(left, "About", "Info", 22f, 18f);
        about = CodeUI.Paragraph("About", left, 24f, 58f, leftWidth - 48f, 250f, 19f, style.Text);
        CodeUI.Hairline(left, 22f, 322f, leftWidth - 44f, 1f, style);
        HearthKit.Section(left, "Traits", "Star", 22f, 336f);
        chips = new KitChips(left, 24f, 376f, leftWidth - 48f);

        float rx = leftWidth + 16f;
        float rightWidth = Width - Margin * 2f - rx;
        RectTransform right = HearthKit.Card(page, "Work Card", rx, 0f, rightWidth, BodyHeight);
        HearthKit.Section(right, "Their work", "Hammer", 22f, 18f);
        jobList = new KitActionList(right, 22f, 58f, rightWidth - 44f, Choose, columns: 1, rowHeight: 62f, nothing: "There is no work here they can do.");
    }

    private void BuildHome(RectTransform page)
    {
        UIStyle style = UIStyle.Current;
        const float leftWidth = 548f;
        RectTransform left = HearthKit.Card(page, "Home Card", 0f, 0f, leftWidth, BodyHeight);
        HearthKit.Section(left, "Their home", "House", 22f, 18f);
        houseCard = new KitSlotCard(left, 22f, 58f, leftWidth - 44f, "Sleeps in", "Bed");
        houseLine = CodeUI.Paragraph("Line", left, 24f, 156f, leftWidth - 48f, 48f, 16f, style.Muted);
        CodeUI.Hairline(left, 22f, 206f, leftWidth - 44f, 1f, style);
        qualityTitle = HearthKit.Section(left, "Home quality", "Star", 22f, 216f, leftWidth - 44f);
        // What makes it a home, one line each with its icon: lit when it has it, with what would give it when not.
        qualityLines = CodeUI.Paragraph("Quality", left, 24f, 254f, leftWidth - 48f, 196f, 16f, style.Text);
        qualityLines.richText = true;
        bonusLine = CodeUI.Paragraph("Bonus", left, 24f, 446f, leftWidth - 48f, 32f, 15f, style.Accent);

        float rx = leftWidth + 16f;
        float rightWidth = Width - Margin * 2f - rx;
        RectTransform right = HearthKit.Card(page, "Houses Card", rx, 0f, rightWidth, BodyHeight);
        HearthKit.Section(right, "Comforts for the house", "Bed", 22f, 18f);
        float half = (rightWidth - 44f - 12f) / 2f;
        for (int i = 0; i < comfortButtons.Length; i++)
        {
            comfortButtons[i] = new KitButton(right, 22f + i * (half + 12f), 58f, half, 62f, Choose, KitButtonShape.Row);
        }

        HearthKit.Section(right, "Move them", "House", 22f, 136f);
        houseList = new KitActionList(right, 22f, 176f, rightWidth - 44f, Choose, columns: 1, rowHeight: 54f,
            nothing: "No house stands yet. A Hut or a Cottage gives beds of their own.");
    }

    private void BuildWork(RectTransform page)
    {
        UIStyle style = UIStyle.Current;
        float width = Width - Margin * 2f;
        RectTransform card = HearthKit.Card(page, "Work Card", 0f, 0f, width, BodyHeight);
        HearthKit.Section(card, "Where they work", "Hammer", 22f, 18f);
        workCard = new KitSlotCard(card, 22f, 58f, 520f, "Works at", "Hammer");
        workLine = CodeUI.Paragraph("Line", card, 566f, 62f, width - 590f, 90f, 17f, style.Muted);
        CodeUI.Hairline(card, 22f, 166f, width - 44f, 1f, style);
        HearthKit.Section(card, "What they keep making", "Gear", 22f, 178f);
        for (int i = 0; i < orderRows.Length; i++)
        {
            orderRows[i] = new KitFactRow(card, 22f, 218f + i * 40f, width - 44f, 300f);
        }

        workActions = new KitActionList(card, 22f, BodyHeight - 84f, 520f, Choose, columns: 2, rowHeight: 62f, nothing: "");
    }

    private void BuildStory(RectTransform page)
    {
        float width = Width - Margin * 2f;
        RectTransform card = HearthKit.Card(page, "Story Card", 0f, 0f, width, BodyHeight);
        HearthKit.Section(card, "Their story", "Hourglass", 22f, 18f);
        for (int i = 0; i < storyRows.Length; i++)
        {
            storyRows[i] = new KitFactRow(card, 22f, 62f + i * 58f, width - 44f);
        }
    }

    // ----- Showing it -----

    private void ShowTab(Tab shown)
    {
        tab = shown;
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].gameObject.SetActive(i == (int)shown);
        }

        tabs.Show((int)shown);
        Refresh();
    }

    private void Update()
    {
        if (window == null || !window.IsOpen)
        {
            return;
        }

        if (who == null || GameSession.Here.Find(who) == null)
        {
            window.Close();
            return;
        }

        if (Time.unscaledTime >= nextRefresh)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        ResidentRecord record = who != null ? GameSession.Here.Find(who) : null;
        if (record == null)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + RefreshSeconds;
        UIStyle style = UIStyle.Current;
        ResidentNpc npc = ResidentNpc.Of(who);
        BuildingObject house = HavenHomes.HomeOf(record);
        BuildingObject work = npc != null ? npc.Workplace : HavenHomes.WorkplaceOf(record, Vector3.zero);
        SettlementSettings settings = SettlementSettings.Current;

        nameLabel.text = who.DisplayName;
        JobDefinition job = record.Job;
        subtitle.text = job != null ? $"{settings.RankFor(record.WorkDone).name} {job.DisplayName}" : "No work yet";
        summary.text = Summary(record, npc, house, work);
        SetHeader(record, house, work, style);

        switch (tab)
        {
            case Tab.Overview:
                about.text = string.IsNullOrWhiteSpace(who.Description) ? who.DisplayName : who.Description;
                chips.Show(Traits(record, style));
                jobList.Show(JobActions(record));
                break;
            case Tab.Home:
                RefreshHome(record, npc, house, work);
                break;
            case Tab.Work:
                RefreshWork(record, house, work);
                break;
            case Tab.Story:
                RefreshStory(record);
                break;
        }
    }

    private void SetHeader(ResidentRecord record, BuildingObject house, BuildingObject work, UIStyle style)
    {
        SettlementSettings settings = SettlementSettings.Current;
        headerRows[0].Set(house != null ? house.Definition.DisplayName : record.Home.Length > 0 || GameSession.Here.HasBed(record) ? "A bed" : "By the fire",
            house != null ? 1f : 0f, style.Accent, style.Warning);
        CraftingStation station = work != null ? work.GetComponent<CraftingStation>() : null;
        headerRows[1].Set(station != null ? $"{station.Speed * 100f:0}% speed" : work != null ? work.Definition.DisplayName : "None", 0f, style.Text, style.Accent);
        headerRows[2].Set(settings.RankFor(record.WorkDone).name, RankShare(record.WorkDone), style.Accent, style.Info);
        headerRows[3].Set($"{record.RaidsAlong} along", 0f, style.Text, style.Accent);
    }

    private void RefreshHome(ResidentRecord record, ResidentNpc npc, BuildingObject house, BuildingObject work)
    {
        UIStyle style = UIStyle.Current;
        HomeQuality.Report report = HomeLife.Of(house);
        if (house != null)
        {
            int beds = house.Beds;
            List<ResidentRecord> living = HavenHomes.LivingIn(house);
            houseCard.Set(HavenHomes.Describe(house), $"{living.Count} of {beds} beds taken", true, beds > 0 ? living.Count / (float)beds : 0f);
            var others = new List<string>();
            foreach (ResidentRecord other in living)
            {
                if (other != record)
                {
                    others.Add(other.Resident.DisplayName);
                }
            }

            string chose = record.HomeChosen == record.Home && record.HomeChosen.Length > 0 ? "You chose it for them" : "They chose it: the free bed nearest their work";
            string with = others.Count > 0 ? $"Lives with {string.Join(", ", others)}" : "A house to themselves";
            string walk = work != null ? $"{HavenHomes.Walk(house.Door(out _), work.transform.position):0} m to the {work.Definition.DisplayName}" : "no work to walk to";
            string window = npc != null && npc.Life != null && npc.Life.IsIndoors ? "Home now: the window is lit." : "";
            houseLine.text = $"{chose}. {with}; {walk}. {window}";
        }
        else
        {
            houseCard.Set("By the fire", "No bed of their own", false);
            houseLine.text = GameSession.Here.Housed >= GameSession.Here.Residents.Count
                ? "" : "Every bed is taken. Another Hut or Cottage gives them one of their own, and their work goes faster for it.";
        }

        if (report != null)
        {
            qualityTitle.text = $"Home quality {report.Quality} ({report.Level.name})";
            var lines = new System.Text.StringBuilder();
            string muted = ColorUtility.ToHtmlStringRGB(style.Muted);
            foreach (HomeQuality.Line line in report.Lines)
            {
                string mark = HearthKit.InlineIcon(line.Icon, line.Met ? style.Accent : style.Inactive);
                lines.Append(mark).Append(' ').Append(line.Met ? $"<b>{line.Name}</b>" : $"<color=#{muted}>{line.Name}</color>")
                    .Append(line.Met ? $"  {line.Detail}" : $"<color=#{muted}>  {line.Detail}</color>").Append('\n');
            }

            qualityLines.text = lines.ToString();
            HomeLifeSettings.Liking liking = HomeLifeSettings.Current.LikingOf(who);
            bool content = HomeLife.IsContent(record, report);
            float speed = HomeLife.WorkBonus(record);
            bonusLine.text = $"Works {speed * 100f:0}% faster for it" + (liking != null ? content ? $"; content: {liking.likes.ToLowerInvariant()}" : $"; would like: {liking.likes.ToLowerInvariant()}" : "");
        }
        else
        {
            qualityTitle.text = "Home quality";
            qualityLines.text = "A house of their own is a home: what stands round it (a fire, a well, a light, a bench, their work) makes it better.";
            bonusLine.text = "";
        }

        IReadOnlyList<HomeLifeSettings.Comfort> comforts = HomeLifeSettings.Current.Comforts;
        for (int i = 0; i < comfortButtons.Length; i++)
        {
            HomeLifeSettings.Comfort comfort = i < comforts.Count ? comforts[i] : null;
            if (comfort == null || house == null)
            {
                comfortButtons[i].Bind(null);
                continue;
            }

            bool has = GameSession.Here.HasComfort(house.Id, comfort.id);
            bool afford = Player.Current != null && MaterialPool.For(Player.Current.gameObject).CanAfford(System.Array.Empty<ResourceAmount>(), comfort.cost);
            HomeLifeSettings.Comfort chosen = comfort;
            BuildingObject inHouse = house;
            comfortButtons[i].Bind(new MenuAction(comfort.name, has ? "In the house" : AmountText.Describe(comfort.cost), !has && afford,
                () => HomeLife.Furnish(inHouse, chosen, Player.Current != null ? Player.Current.gameObject : null))
            { Icon = comfort.icon, Status = has ? "Done" : "" });
        }

        houseList.Show(HouseActions(record));
    }

    private void RefreshWork(ResidentRecord record, BuildingObject house, BuildingObject work)
    {
        JobDefinition job = record.Job;
        if (work != null)
        {
            CraftingStation station = work.GetComponent<CraftingStation>();
            float walk = HavenHomes.Walk(house != null ? house.Door(out _) : (ResidentNpc.Of(who) != null ? ResidentNpc.Of(who).HomeSpot : work.transform.position), work.transform.position);
            workCard.Set(HavenHomes.Describe(work), station != null ? $"Works at {station.Speed * 100f:0}% speed there" : $"{job.DisplayName}'s place", true);
            workLine.text = $"{walk:0} m from {(house != null ? "their house" : "where they sleep")}. They work at the {work.Definition.DisplayName} nearest home: " +
                            "build one near their house, or move them nearer, and the walk is short.";
        }
        else
        {
            workCard.Set(job != null ? "Nowhere yet" : "No work", job != null && job.Workplace != null ? $"Build a {job.Workplace.DisplayName}" : "Give them work in Overview", false);
            workLine.text = "";
        }

        int row = 0;
        StationQueue queue = WorkerProduction.QueueOf(GameSession.Here, record);
        if (job != null && queue != null)
        {
            foreach (WorkOrder order in job.WorkOrders)
            {
                if (order.recipe != null && row < orderRows.Length)
                {
                    string why = WorkerProduction.WhyNot(order, queue, GameSession.Stash);
                    orderRows[row++].Set(order.recipe.DisplayName, why == null ? "Ready" : why);
                }
            }
        }

        while (row < orderRows.Length)
        {
            orderRows[row++].Set("", "");
        }

        var actions = new List<MenuAction>();
        if (job != null && job.WorkOrders.Count > 0)
        {
            actions.Add(new MenuAction(record.WorkPaused ? "Back to work" : "Pause their work", record.WorkPaused ? "They are resting" : "Stops them using the stash",
                true, () => HavenDecisions.Do("resident.pause", who.Id)) { Icon = "Hourglass" });
        }

        ResidentNpc npc = ResidentNpc.Of(who);
        if (npc != null)
        {
            bool open = HavenTravel.IsOpen;
            actions.Add(new MenuAction(open ? "Go there" : "Not during a breach", "Walk over to them", open, () => HavenTravel.GoTo(npc.transform), closesMenu: true) { Icon = "Lead" });
        }

        workActions.Show(actions);
    }

    private void RefreshStory(ResidentRecord record)
    {
        SettlementSettings settings = SettlementSettings.Current;
        storyRows[0].Set("Rank", record.Job != null ? $"{settings.RankFor(record.WorkDone).name}, {record.WorkDone} made" : "No work yet");
        storyRows[1].Set("Raids along", $"{record.RaidsAlong}");
        storyRows[2].Set("Foes felled along", $"{record.KillsAlong}");
        storyRows[3].Set("Helped you up", $"{record.TimesHelpedUp} times");
        storyRows[4].Set("A lesson", record.Job != null && record.Job.Teaches != null
            ? record.TaughtSinceRaid ? $"{record.Job.Teaches.DisplayName}, again after a raid survived" : $"{record.Job.Teaches.DisplayName}: ask them"
            : "Nothing to teach");
        storyRows[5].Set("Remembers", record.Forgotten.Count > 0 ? $"{record.Remembers.Count}, with {record.Forgotten.Count} still lost to the forest" : $"{record.Remembers.Count} things you did together");
        storyRows[6].Set("Came home", string.IsNullOrWhiteSpace(who.ArrivalLine) ? "With the expedition" : $"\"{who.ArrivalLine}\"");
    }

    // ----- What can be done -----

    private List<MenuAction> JobActions(ResidentRecord record)
    {
        var actions = new List<MenuAction>();
        foreach (JobDefinition job in GameSession.Here.JobsOpenTo(who))
        {
            if (job == null)
            {
                continue;
            }

            JobDefinition offered = job;
            bool holds = record.Job == job;
            string where = job.Workplace != null ? $"At the {job.Workplace.DisplayName}" : "About the Haven";
            actions.Add(new MenuAction(holds ? $"Working as {job.DisplayName}" : $"Work as {job.DisplayName}", where, !holds,
                () => ResidentNpc.GiveJob(who, offered, Player.Current != null ? Player.Current.gameObject : null)) { Icon = "Hammer", Status = holds ? "Now" : "" });
        }

        return actions;
    }

    // Every house with beds: move in (the player's choice), and a row to let them choose again.
    private List<MenuAction> HouseActions(ResidentRecord record)
    {
        var actions = new List<MenuAction>();
        var houses = new List<BuildingObject>();
        foreach (BuildingObject building in BuildingObject.Active)
        {
            if (building.Beds > 0 && building.Id != null)
            {
                houses.Add(building);
            }
        }

        houses.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        BuildingObject work = HavenHomes.WorkplaceOf(record, Vector3.zero);
        foreach (BuildingObject house in houses)
        {
            string id = house.Id;
            int chosenBy = 0;
            foreach (ResidentRecord other in GameSession.Here.Residents)
            {
                chosenBy += other != record && other.HomeChosen == id ? 1 : 0;
            }

            bool lives = record.Home == id;
            bool room = chosenBy < house.Beds;
            string walk = work != null ? $" · {HavenHomes.Walk(house.Door(out _), work.transform.position):0} m to the {work.Definition.DisplayName}" : "";
            actions.Add(new MenuAction(HavenHomes.Describe(house), $"{(lives ? "Lives here · " : "")}{HavenHomes.LivingIn(house).Count} of {house.Beds} beds{walk}", !lives && room,
                () => HavenHomes.Choose(who, id)) { Icon = "House", Status = lives || room ? "" : "Promised" });
        }

        if (!string.IsNullOrEmpty(record.HomeChosen))
        {
            actions.Add(new MenuAction("Let them choose", "The free bed nearest their work", true, () => HavenHomes.Choose(who, "")) { Icon = "Bed" });
        }

        return actions;
    }

    private void Choose(MenuAction action)
    {
        if (action == null || !action.Enabled)
        {
            return;
        }

        action.Chosen?.Invoke();
        if (action.ClosesMenu)
        {
            window.Close();
        }
        else
        {
            // Homes are worked out at the end of the frame: show it a moment later.
            nextRefresh = Time.unscaledTime + 0.1f;
        }
    }

    // ----- Words -----

    private string Summary(ResidentRecord record, ResidentNpc npc, BuildingObject house, BuildingObject work)
    {
        string home = house != null ? $"Lives in the {HavenHomes.Describe(house)}" : "Sleeps by the fire near the portal";
        string walk = house != null && work != null ? $", {HavenHomes.Walk(house.Door(out _), work.transform.position):0} m from the {work.Definition.DisplayName}" : "";
        string now = npc != null && npc.Life != null ? Doing(npc.Life) : "";
        return $"{home}{walk}.{(now.Length > 0 ? " " + now : "")}";
    }

    private static string Doing(ResidentLife life) => life.IsIndoors ? "Asleep at home now." : life.Activity switch
    {
        ResidentActivity.Work => "At work now.",
        ResidentActivity.Meal => "Eating now.",
        ResidentActivity.Fire => "By the fire now.",
        ResidentActivity.Sleep => "Off to bed.",
        ResidentActivity.Shelter => "Sheltering at the portal!",
        ResidentActivity.Outing => "Out for a walk.",
        _ => "",
    };

    private List<(string, Color)> Traits(ResidentRecord record, UIStyle style)
    {
        var shown = new List<(string, Color)>();
        if (record.Job != null)
        {
            shown.Add((record.Job.DisplayName, style.Good));
        }

        shown.Add((GameSession.Here.HasBed(record) ? "Has a bed" : "No bed", GameSession.Here.HasBed(record) ? style.Info : style.Warning));
        HomeLifeSettings.Liking liking = HomeLifeSettings.Current.LikingOf(who);
        if (liking != null)
        {
            shown.Add((HomeLife.IsContent(record) ? "Content at home" : $"Likes: {liking.likes.ToLowerInvariant()}", HomeLife.IsContent(record) ? style.Good : style.Muted));
        }
        if (record.WorkPaused)
        {
            shown.Add(("Resting", style.Warning));
        }

        if (record.RaidsAlong >= 3)
        {
            shown.Add(("Raid-hardened", style.Info));
        }

        return shown;
    }

    private static float RankShare(int workDone)
    {
        SettlementSettings settings = SettlementSettings.Current;
        int index = settings.RankIndex(workDone);
        int from = settings.RankFor(workDone).crafts;
        int to = index + 1 < settings.WorkerRanks.Count ? settings.WorkerRanks[index + 1].crafts : from;
        return to > from ? Mathf.Clamp01((workDone - from) / (float)(to - from)) : 1f;
    }

    // ----- The picture: a camera before them at face height; their painted portrait while they sleep indoors -----

    private void LateUpdate()
    {
        if (window == null || !window.IsOpen || who == null)
        {
            return;
        }

        ResidentNpc npc = ResidentNpc.Of(who);
        bool seen = npc != null && (npc.Life == null || !npc.Life.IsIndoors);
        still.sprite = who.Portrait;
        still.enabled = !seen && who.Portrait != null;
        if (seen)
        {
            portrait.Show(npc.transform, Shot);
        }
        else
        {
            portrait.Hide();
        }
    }

    /// <summary>A resident's portrait: from in front and a little to the side, at face height.</summary>
    public static (Vector3 eye, Vector3 look) Shot(Transform body)
    {
        Vector3 look = body.position + Vector3.up * 1.45f;
        return (look + body.forward * 2.2f + body.right * 0.7f + Vector3.up * 0.1f, look);
    }

    private void OnDestroy()
    {
        PlayerHold.Set(this, false);
        PadTabs.Unregister(this);
    }
}
