using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A resident's page (the user's playtest list, 2026-11-15: "an NPC window like the horse window"): their live picture,
/// name, job and rank, and tabs: Overview (who they are, their traits, their jobs), Talents (their level and passion in every skill:
/// Docs/PeopleResearch_2026-11-23.md), Bonds (their spirits and why, friends and rivals, the marks raids left, the kit they fight with),
/// Home (the house they sleep in, what it is like, and every house they could move into: the player's choice wins, "Let them choose"
/// gives it back), Work (where they work, how far from home, what they keep making) and Story (raids along, lessons). Opened from the Haven window's People tab and
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
    private static readonly string[] TabNames = { "Overview", "Talents", "Bonds", "Home", "Work", "Story" };

    private enum Tab
    {
        Overview = 0,
        Talents = 1,
        Bonds = 2,
        Home = 3,
        Work = 4,
        Story = 5,
    }

    private const int MostTraits = 4;
    private const int MostStandings = 9;
    private const int MostReasons = 6;

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

    private readonly List<KitPictureCard> traitCards = new List<KitPictureCard>();
    private readonly List<(string title, string body, Color colour)> traitTips = new List<(string, string, Color)>();
    private readonly List<KitStatRow> skillRows = new List<KitStatRow>();
    private readonly List<ProficiencyDefinition> skillOrder = new List<ProficiencyDefinition>();
    private TMP_Text talentNote;
    private readonly KitButton[] pickButtons = new KitButton[2];
    private readonly List<(string title, string body, Color colour)> skillTips = new List<(string, string, Color)>();
    private KitStatRow spiritsRow;
    private TMP_Text spiritsMeans;
    private readonly KitFactRow[] reasonRows = new KitFactRow[MostReasons];
    private readonly List<KitPictureCard> standingCards = new List<KitPictureCard>();
    private readonly List<(string title, string body, Color colour)> standingTips = new List<(string, string, Color)>();
    private TMP_Text nobodyClose;
    private readonly List<KitPictureCard> markCards = new List<KitPictureCard>();
    private readonly List<(string title, string body, Color colour)> markTips = new List<(string, string, Color)>();
    private TMP_Text noMarks;
    private readonly KitPictureCard[] kitCards = new KitPictureCard[2];
    private TMP_Text kitNote;
    private KitActionList kitActions;
    private ItemTooltipView tooltip;

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
        tabs = new KitTabs(panel, Margin + 8f, 312f, 168f, 54f, TabNames, i => ShowTab((Tab)i));
        HearthKit.Rule(panel, Margin, 370f, Width - Margin * 2f);
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i] = CodeUI.At(TabNames[i] + " Page", panel, Margin, BodyTop, Width - Margin * 2f, BodyHeight);
        }

        BuildOverview(pages[(int)Tab.Overview]);
        BuildTalents(pages[(int)Tab.Talents]);
        BuildBonds(pages[(int)Tab.Bonds]);
        BuildHome(pages[(int)Tab.Home]);
        BuildWork(pages[(int)Tab.Work]);
        BuildStory(pages[(int)Tab.Story]);

        BuildTooltip(content);
        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.ResidentPanel, content.gameObject);
        window.OpenChanged += open =>
        {
            PlayerHold.Set(this, open);
            if (!open && tooltip != null)
            {
                tooltip.Hide();
            }

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
        about = CodeUI.Paragraph("About", left, 24f, 58f, leftWidth - 48f, 120f, 19f, style.Text);
        CodeUI.Hairline(left, 22f, 186f, leftWidth - 44f, 1f, style);
        // Their traits as pictures with names, what each means on hover (no walls of text: the user, 2026-11-21).
        HearthKit.Section(left, "Who they are", "Star", 22f, 198f);
        for (int i = 0; i < MostTraits; i++)
        {
            int index = i;
            var card = new KitPictureCard(left, "Trait " + i, 118f, 62f, 2, 15f, () => ShowTip(traitTips, index), HideTip);
            card.Place(24f + i * 128f, 238f);
            traitCards.Add(card);
        }

        chips = new KitChips(left, 24f, 420f, leftWidth - 48f);

        float rx = leftWidth + 16f;
        float rightWidth = Width - Margin * 2f - rx;
        RectTransform right = HearthKit.Card(page, "Work Card", rx, 0f, rightWidth, BodyHeight);
        HearthKit.Section(right, "Their work", "Hammer", 22f, 18f);
        jobList = new KitActionList(right, 22f, 58f, rightWidth - 44f, Choose, columns: 2, rowHeight: 62f, nothing: "There is no work here they can do.");

        // The kit they fight with beside the player (CompanionKit): its pictures, what it is on hover, and giving or taking it back here
        // as in talk (the UX pass of 2026-11-23: the card stood half empty and kit could only be handled by talking).
        HearthKit.Section(right, "Their kit", "Sword", 22f, 200f);
        for (int i = 0; i < kitCards.Length; i++)
        {
            bool weapon = i == 0;
            kitCards[i] = new KitPictureCard(right, weapon ? "Kit Weapon" : "Kit Body", 150f, 64f, 2, 15f, () => ShowKitTip(weapon), HideTip);
            kitCards[i].Place(22f + i * 162f, 240f);
        }

        kitNote = CodeUI.Paragraph("Kit Note", right, 350f, 244f, rightWidth - 372f, 96f, 16f, style.Muted);
        kitActions = new KitActionList(right, 22f, 356f, rightWidth - 44f, Choose, columns: 2, rowHeight: 54f, nothing: "");
    }

    // Their level in every skill, best first, with its band's name and their passion's flames; a passion to choose, said at the top.
    private void BuildTalents(RectTransform page)
    {
        UIStyle style = UIStyle.Current;
        float width = Width - Margin * 2f;
        RectTransform card = HearthKit.Card(page, "Talents Card", 0f, 0f, width, BodyHeight);
        HearthKit.Section(card, "Their talents", "Star", 22f, 18f);
        talentNote = CodeUI.Label("Note", card, 300f, 20f, 420f, 30f, 17f, style.Accent, TextAlignmentOptions.Left);
        talentNote.richText = true;
        // A passion to grow (a bond rank reached): the two offers, chosen right here as in talk.
        for (int i = 0; i < pickButtons.Length; i++)
        {
            pickButtons[i] = new KitButton(card, width - 22f - (2 - i) * 236f + 6f, 10f, 230f, 44f, Choose, KitButtonShape.Row);
        }

        int count = 0;
        foreach (ProficiencyDefinition skill in LevelingSettings.Current.Skills)
        {
            count += skill != null ? 1 : 0;
        }

        int perColumn = Mathf.Max(1, Mathf.CeilToInt(count / 2f));
        float column = (width - 44f - 30f) / 2f;
        float step = Mathf.Min(58f, (BodyHeight - 70f) / perColumn);
        for (int i = 0; i < count; i++)
        {
            float x = 22f + (i / perColumn) * (column + 30f);
            float y = 62f + (i % perColumn) * step;
            skillRows.Add(new KitStatRow(card, x, y, column, "Skill", "Star", true, 18f, labelWidth: 220f));
            // What the level does for them, on hover over the whole row.
            int index = i;
            RectTransform hover = CodeUI.At("Skill Hover " + i, card, x, y - 4f, column, step - 4f);
            Image catcher = hover.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            var over = hover.gameObject.AddComponent<UIHover>();
            over.Entered = () => ShowTip(skillTips, index);
            over.Exited = HideTip;
        }
    }

    // Their spirits and why, how they stand with the others (portraits, the standing on hover), and the marks raids left.
    private void BuildBonds(RectTransform page)
    {
        UIStyle style = UIStyle.Current;
        const float leftWidth = 372f;
        const float middleWidth = 372f;
        RectTransform left = HearthKit.Card(page, "Spirits Card", 0f, 0f, leftWidth, BodyHeight);
        HearthKit.Section(left, "Spirits", "Sun", 22f, 18f);
        spiritsRow = new KitStatRow(left, 22f, 58f, leftWidth - 44f, "Now", "Heart", false, 20f);
        spiritsMeans = CodeUI.Paragraph("Means", left, 24f, 94f, leftWidth - 48f, 44f, 15f, style.Muted);
        for (int i = 0; i < reasonRows.Length; i++)
        {
            reasonRows[i] = new KitFactRow(left, 22f, 146f + i * 54f, leftWidth - 44f, 270f);
        }

        RectTransform middle = HearthKit.Card(page, "Standings Card", leftWidth + 16f, 0f, middleWidth, BodyHeight);
        HearthKit.Section(middle, "Friends and rivals", "Bond", 22f, 18f);
        float portrait = (middleWidth - 44f - 16f) / 3f;
        for (int i = 0; i < MostStandings; i++)
        {
            int index = i;
            var card = new KitPictureCard(middle, "Standing " + i, portrait, 84f, 1, 15f, () => ShowTip(standingTips, index), HideTip);
            card.Place(22f + (i % 3) * (portrait + 8f), 58f + (i / 3) * 136f);
            standingCards.Add(card);
        }

        nobodyClose = CodeUI.Paragraph("Nobody", middle, 24f, 62f, middleWidth - 48f, 120f, 16f, style.Muted);
        nobodyClose.text = "Nobody close yet. Scenes at the fire, forays together and work side by side bring people closer.";

        float rx = leftWidth + middleWidth + 32f;
        float rightWidth = Width - Margin * 2f - rx;
        RectTransform right = HearthKit.Card(page, "Marks Card", rx, 0f, rightWidth, BodyHeight);
        HearthKit.Section(right, "Marks of the raids", "Cross", 22f, 18f);
        float markWidth = (rightWidth - 44f - 16f) / 3f;
        for (int i = 0; i < 6; i++)
        {
            int index = i;
            var card = new KitPictureCard(right, "Mark " + i, markWidth, 64f, 2, 14f, () => ShowTip(markTips, index), HideTip);
            card.Place(22f + (i % 3) * (markWidth + 8f), 58f + (i / 3) * 130f);
            markCards.Add(card);
        }

        noMarks = CodeUI.Paragraph("No Marks", right, 24f, 62f, rightWidth - 48f, 120f, 16f, style.Muted);
        noMarks.text = "No marks yet. Raids beside you leave them: a steadier hand, an old wound, a fear to face down.";
    }

    // Laid over the content, as the raid planner's: the words of a trait or mark on hover.
    private void BuildTooltip(RectTransform content)
    {
        UIStyle style = UIStyle.Current;
        var holder = new GameObject("Tooltip Holder", typeof(RectTransform));
        holder.SetActive(false);
        holder.transform.SetParent(content, false);
        RectTransform tip = CodeUI.At("Tooltip", content, 0f, 0f, 380f, 100f);
        tip.pivot = new Vector2(0f, 1f);
        tip.gameObject.AddComponent<Image>().raycastTarget = false;
        tip.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        tip.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
        var layout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 10, 12);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        TMP_Text title = CodeUI.Paragraph("Title", tip, 0f, 0f, 352f, 28f, 19f, style.Text);
        style.Title(title);
        TMP_Text body = CodeUI.Paragraph("Body", tip, 0f, 0f, 352f, 28f, 16f, style.Text);
        tooltip = holder.AddComponent<ItemTooltipView>();
        tooltip.Configure(tip, title, body);
        holder.SetActive(true);
    }

    // Their kit's piece as the bag shows it, with how long it has been with them.
    private void ShowKitTip(bool weapon)
    {
        ResidentRecord record = who != null ? GameSession.Here.Find(who) : null;
        ItemStack piece = record == null ? ItemStack.Empty : weapon ? record.KitWeapon : record.KitBody;
        if (tooltip == null || piece.IsEmpty)
        {
            return;
        }

        int raids = CompanionKit.RaidsWith(record, weapon);
        tooltip.Show(piece, CompanionKit.IsTheirs(record, weapon)
            ? $"{CompanionKit.NameOf(record, weapon)}: {raids} raids with them."
            : $"Theirs by name after {PeopleSettings.Current.KitNamedAfter} raids along ({raids} so far).");
    }

    private void ShowTip(List<(string title, string body, Color colour)> tips, int index)
    {
        if (tooltip != null && index < tips.Count && !string.IsNullOrEmpty(tips[index].title))
        {
            tooltip.Show(tips[index].title, tips[index].colour, tips[index].body);
        }
    }

    private void HideTip()
    {
        if (tooltip != null)
        {
            tooltip.Hide();
        }
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
        subtitle.text = job != null ? $"{ResidentTalents.RankName(record)} {job.DisplayName}" : "No work yet";
        summary.text = Summary(record, npc, house, work);
        SetHeader(record, house, work, style);

        switch (tab)
        {
            case Tab.Overview:
                about.text = string.IsNullOrWhiteSpace(who.Description) ? who.DisplayName : who.Description;
                ShowTraits(record, style);
                chips.Show(Traits(record, style));
                jobList.Show(JobActions(record));
                RefreshKit(record, style);
                break;
            case Tab.Talents:
                RefreshTalents(record, style);
                break;
            case Tab.Bonds:
                RefreshBonds(record, style);
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
        headerRows[2].Set(ResidentTalents.RankName(record), ResidentTalents.LevelShare(record, ResidentTalents.JobSkill(record)), style.Accent, style.Info);
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
            float speed = HomeLife.HomeBonus(record);
            string fed = ResidentMeals.Describe(record);
            bonusLine.text = $"Works {speed * 100f:0}% faster for it" + (liking != null ? content ? $"; content: {liking.likes.ToLowerInvariant()}" : $"; would like: {liking.likes.ToLowerInvariant()}" : "")
                + (fed.Length > 0 ? "\n" + fed : "");
        }
        else
        {
            qualityTitle.text = "Home quality";
            qualityLines.text = "A house of their own is a home: what stands round it (a fire, a well, a light, a bench, their work) makes it better.";
            bonusLine.text = ResidentMeals.Describe(record);
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
                            "build one near their house, or move them nearer, and the walk is short." + CarryLine(record);
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
            actions.Add(new MenuAction(record.PausedByPlayer ? "Back to work" : "Pause their work", record.PausedByPlayer ? "They are resting" : "Stops them using the stash",
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
        ProficiencyDefinition jobSkill = ResidentTalents.JobSkill(record);
        storyRows[0].Set("Rank", record.Job != null && jobSkill != null
            ? $"{ResidentTalents.RankName(record)}, {jobSkill.DisplayName} {ResidentTalents.LevelOf(record, jobSkill)}, {record.WorkDone} made" : "No work yet");
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
        // Their work now and their own trades first, then the rest best first by their level in each one's skill (any known trade with
        // room is theirs to take: Docs/PeopleSystemReview_2026-11-23.md §2.1); the whole picture is Who does what (WorkGridWindow).
        var actions = new List<MenuAction>();
        List<JobDefinition> jobs = GameSession.Here.JobsOpenTo(who);
        jobs.Sort((a, b) =>
        {
            int first = Rank(a).CompareTo(Rank(b));
            return first != 0 ? first : ResidentTalents.LevelOf(record, b.Skill).CompareTo(ResidentTalents.LevelOf(record, a.Skill));
        });
        int Rank(JobDefinition job) => record.Job == job ? 0 : System.Array.IndexOf(who.Jobs, job) >= 0 ? 1 : 2;
        foreach (JobDefinition job in jobs)
        {
            if (job == null || actions.Count >= 3)
            {
                continue;
            }

            JobDefinition offered = job;
            bool holds = record.Job == job;
            ProficiencyDefinition skill = job.Skill;
            string where = job.Workplace != null ? $"At the {job.Workplace.DisplayName}" : "About the Haven";
            string level = skill != null ? $"{skill.DisplayName} {ResidentTalents.LevelOf(record, skill)} {ResidentTalents.Flames(ResidentTalents.PassionOf(record, skill))} · " : "";
            actions.Add(new MenuAction(holds ? $"Working as {job.DisplayName}" : $"Work as {job.DisplayName}", level + where, !holds,
                () => ResidentNpc.GiveJob(who, offered, Player.Current != null ? Player.Current.gameObject : null)) { Icon = "Hammer", Status = holds ? "Now" : "" });
        }

        actions.Add(new MenuAction("Who does what", "Everyone's level in every trade", true, WorkGridWindow.Show) { Icon = "Stats" });
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
        if (record.IsAway)
        {
            shown.Add(("Away on a foray", style.Info));
        }
        else if (record.WorkPaused)
        {
            shown.Add(("Resting", style.Warning));
        }

        if (record.Bruised)
        {
            shown.Add(("Bruised from a foray", style.Warning));
        }

        if (record.RaidsAlong >= 3)
        {
            shown.Add(("Raid-hardened", style.Info));
        }

        return shown;
    }

    // What they carry on a trip for their work (WorkerSettings): by hand, their Strength and their worker gear.
    private static string CarryLine(ResidentRecord record)
    {
        WorkerSettings settings = WorkerSettings.Current;
        CarryLimits hand = WorkerCarry.ByHand(record, settings);
        int strength = WorkerCarry.Strength(record, settings);
        string gear = record.WorkGear != null ? $", with their {record.WorkGear.DisplayName}" : "";
        return $"\nCarries {hand.Weight:0} a trip, {hand.Slots} kind{(hand.Slots == 1 ? "" : "s")} at once (Strength {strength}{gear}). A cart or a working horse carries more.";
    }

    // Their traits as picture cards (a double-edged one in the warm colour), each one's words for its hover.
    private void ShowTraits(ResidentRecord record, UIStyle style)
    {
        IReadOnlyList<ResidentTraitDefinition> traits = ResidentTraits.Of(record);
        traitTips.Clear();
        for (int i = 0; i < traitCards.Count; i++)
        {
            ResidentTraitDefinition trait = i < traits.Count ? traits[i] : null;
            traitCards[i].SetActive(trait != null);
            if (trait == null)
            {
                traitTips.Add((null, null, style.Text));
                continue;
            }

            Color colour = trait.DoubleEdged ? style.Warning : style.Good;
            traitCards[i].ShowPicture(null, trait.Icon, colour);
            traitCards[i].SetName(trait.DisplayName);
            traitTips.Add((trait.DisplayName, trait.Description, colour));
        }
    }

    private void RefreshTalents(ResidentRecord record, UIStyle style)
    {
        skillOrder.Clear();
        skillOrder.AddRange(ResidentTalents.BestFirst(record));
        skillTips.Clear();
        ProficiencyDefinition job = ResidentTalents.JobSkill(record);
        for (int i = 0; i < skillRows.Count; i++)
        {
            ProficiencyDefinition skill = i < skillOrder.Count ? skillOrder[i] : null;
            if (skill == null)
            {
                skillTips.Add((null, null, style.Text));
                continue;
            }

            int level = ResidentTalents.LevelOf(record, skill);
            Passion passion = ResidentTalents.PassionOf(record, skill);
            bool works = skill == job && record.Job != null;
            skillRows[i].SetName(works ? $"{skill.DisplayName} · their work" : skill.DisplayName, IconOf(skill));
            skillRows[i].Set($"{ResidentTalents.Flames(passion)} {level} {ResidentTalents.RankNameAt(level)}", ResidentTalents.LevelShare(record, skill),
                works ? style.Accent : style.Info, style.Info);
            skillTips.Add(($"{skill.DisplayName} {level}", SkillWords(record, skill, level, passion, works), works ? style.Accent : style.Text));
        }

        // A passion to grow: the offers as buttons; else what they are inspired with, if anything.
        List<(ProficiencyDefinition skill, Passion to)> offers = record.Talents.PicksOwed > 0 && !HavenVisit.InHostsHaven
            ? ResidentTalents.PickOffers(record) : new List<(ProficiencyDefinition, Passion)>();
        for (int i = 0; i < pickButtons.Length; i++)
        {
            bool shown = i < offers.Count;
            pickButtons[i].SetActive(shown);
            if (shown)
            {
                (ProficiencyDefinition skill, Passion to) = offers[i];
                pickButtons[i].Bind(new MenuAction(to == Passion.Burning ? $"{skill.DisplayName}: burn" : $"{skill.DisplayName}: love it",
                    null, true, () => ResidentTalents.Pick(record, skill, to)) { Icon = ResidentTalents.PassionIcon });
            }
        }

        string note = offers.Count > 0 ? $"{HearthKit.InlineIcon(ResidentTalents.PassionIcon)} A passion to grow: choose one" : "";
        if (record.Inspiration != Inspiration.None && offers.Count == 0)
        {
            note = $"{HearthKit.InlineIcon("Spark")} {Inspirations.NameOf(record.Inspiration)}";
        }

        talentNote.text = note;
    }

    // What a skill's level does for them, in a few short lines: their work, beside you, how they learn it, and how far to the next level.
    private static string SkillWords(ResidentRecord record, ProficiencyDefinition skill, int level, Passion passion, bool works)
    {
        PeopleSettings people = PeopleSettings.Current;
        var lines = new List<string> { ResidentTalents.RankNameAt(level) + (passion == Passion.Burning ? ", and they burn for it" : passion == Passion.Keen ? ", and they love it" : "") };
        if (works)
        {
            lines.Add($"Their work runs at x{ResidentTalents.StationSpeed(record):0.00}; {ResidentTalents.ExtraChance(record) * 100f:0}% chance of one more.");
        }
        else if (skill == people.BuildingSkill || skill == people.FellingSkill || skill == people.QuarrySkill)
        {
            lines.Add($"At a scaffold or a ring: x{ResidentTalents.SpeedAt(record, skill):0.00}.");
        }

        if (record.Resident != null && record.Resident.RaidCompanion != null && skill == ResidentTalents.FightSkill(record))
        {
            lines.Add($"Beside you: +{ResidentTalents.CompanionDamage(record) * 100f:0}% damage.");
        }

        if (skill == people.MeleeSkill && ResidentTalents.CompanionDodge(record) > 0f)
        {
            lines.Add($"Steps aside from blows {ResidentTalents.CompanionDodge(record) * 100f:0}% more often.");
        }

        float learning = people.LearningFor(passion) * ResidentTraits.LearningOf(record, skill);
        lines.Add($"Learns it at x{learning:0.##}" + (ResidentTalents.Joy(record, skill) > 0f ? $", {ResidentTalents.Joy(record, skill) * 100f:0}% quicker at it for the joy." : "."));
        if (level < LevelingSettings.Current.MaxLevel)
        {
            lines.Add($"{ResidentTalents.LevelShare(record, skill) * 100f:0}% of the way to {level + 1}.");
        }

        return string.Join("\n", lines);
    }

    private void RefreshBonds(ResidentRecord record, UIStyle style)
    {
        Spirits spirits = ResidentSpirits.Of(record);
        spiritsRow.SetName("Now", ResidentSpirits.IconOf(spirits));
        spiritsRow.Set(ResidentSpirits.NameOf(spirits), 0f,
            spirits == Spirits.Bright ? style.Good : spirits == Spirits.Low ? style.Warning : style.Text, style.Warning);
        spiritsMeans.text = spirits == Spirits.Bright ? "Bright: any morning may bring an inspired day."
            : spirits == Spirits.Low ? $"Low: {PeopleSettings.Current.LowWork * 100f:0}% slower at work, and no inspired days."
            : "Steady: one more lift than lower makes them Bright.";
        string good = ColorUtility.ToHtmlStringRGB(style.Good), bad = ColorUtility.ToHtmlStringRGB(style.Warning);
        List<SpiritReason> reasons = ResidentSpirits.ReasonsOf(record);
        for (int i = 0; i < reasonRows.Length; i++)
        {
            bool shown = i < reasons.Count;
            reasonRows[i].Set(shown ? $"{HearthKit.InlineIcon(reasons[i].Icon)} {reasons[i].Words}" : "",
                shown ? $"<color=#{(reasons[i].Steps > 0 ? good : bad)}>{(reasons[i].Steps > 0 ? "+" : "")}{reasons[i].Steps}</color>" : "");
        }

        var standings = new List<(ResidentRecord other, int level)>();
        foreach (ResidentRecord other in GameSession.Here.Residents)
        {
            int level = PeopleStandings.LevelOf(GameSession.Here, record, other);
            if (other != record && level != 0)
            {
                standings.Add((other, level));
            }
        }

        standings.Sort((a, b) => b.level.CompareTo(a.level));
        nobodyClose.gameObject.SetActive(standings.Count == 0);
        standingTips.Clear();
        PeopleSettings people = PeopleSettings.Current;
        for (int i = 0; i < standingCards.Count; i++)
        {
            bool shown = i < standings.Count;
            standingCards[i].SetActive(shown);
            if (!shown)
            {
                standingTips.Add((null, null, style.Text));
                continue;
            }

            (ResidentRecord other, int level) = standings[i];
            Color colour = level > 0 ? style.Good : style.Warning;
            standingCards[i].ShowPicture(other.Resident.Portrait, "Bond", colour);
            standingCards[i].SetName(other.Resident.DisplayName);
            standingCards[i].SetBadge(PeopleStandings.NameOf(level));
            standingCards[i].SetLook(false, colour);
            string means = level > 0
                ? $"Working near each other: {people.FriendNearWork * 100f:0}% quicker. On a foray together: {people.ForayFriends * 100f:0}% stronger. Missed if the forest takes them."
                : level <= -2 ? "As neighbours they sour each other's homes. Now and then they have it out at the fire: it clears the air, or hardens."
                : "Now and then they have it out at the fire: it clears the air, or hardens.";
            standingTips.Add(($"{other.Resident.DisplayName}: {PeopleStandings.NameOf(level)}", means, colour));
        }

        List<MarkDefinition> marks = ResidentMarks.Of(record);
        noMarks.gameObject.SetActive(marks.Count == 0);
        markTips.Clear();
        for (int i = 0; i < markCards.Count; i++)
        {
            MarkDefinition mark = i < marks.Count ? marks[i] : null;
            markCards[i].SetActive(mark != null);
            if (mark == null)
            {
                markTips.Add((null, null, style.Text));
                continue;
            }

            Color colour = mark.Good ? style.Good : style.Warning;
            markCards[i].ShowPicture(null, mark.Icon, colour);
            markCards[i].SetName(mark.DisplayName);
            markCards[i].SetLook(false, colour);
            string way = mark.Good ? "" : mark.HeldByTheForest && record.Forgotten.Count > 0 ? "\nIt stays until every memory is back."
                : mark.HealedBy != null ? $"\nA {mark.HealedBy.DisplayName} would ease it." : mark.FriendsHeal ? "\nAn evening with a friend would ease it."
                : mark.TurnsInto != null ? $"\nFaced down, it becomes {mark.TurnsInto.DisplayName}." : "";
            markTips.Add((mark.DisplayName, mark.Description + way, colour));
        }
    }

    // Their kit: the two pieces as pictures (or empty hands), and giving or taking back here (as in talk; one's own Haven only).
    private void RefreshKit(ResidentRecord record, UIStyle style)
    {
        bool fights = record.Resident.RaidCompanion != null;
        for (int i = 0; i < kitCards.Length; i++)
        {
            bool weapon = i == 0;
            ItemStack piece = weapon ? record.KitWeapon : record.KitBody;
            kitCards[i].SetActive(fights);
            if (!fights)
            {
                continue;
            }

            kitCards[i].ShowPicture(piece.IsEmpty ? null : piece.Item.Icon, weapon ? "Sword" : "Bags");
            kitCards[i].SetName(piece.IsEmpty ? (weapon ? "Their own weapon" : "Their own clothes") : CompanionKit.NameOf(record, weapon));
            kitCards[i].SetBadge(piece.IsEmpty ? "" : $"{Mathf.RoundToInt(piece.Durability * 100f)}%");
            kitCards[i].SetLook(piece.IsEmpty, piece.IsBroken ? style.Warning : (Color?)null);
        }

        kitNote.text = !fights ? "They stay home when you raid." : record.KitWeapon.IsEmpty && record.KitBody.IsEmpty
            ? "Give them a weapon that suits how they fight, and a body piece: theirs by name after enough raids."
            : $"Wears with every raid along. Mend it by taking it back. Theirs by name after {PeopleSettings.Current.KitNamedAfter} raids.";
        kitActions.Show(fights && !HavenVisit.InHostsHaven ? KitActions(record) : new List<MenuAction>());
    }

    // Take back what they carry, and give the best piece in the bag that suits them, for each hand.
    private List<MenuAction> KitActions(ResidentRecord record)
    {
        var actions = new List<MenuAction>();
        Player player = Player.Current;
        ItemContainer bag = player != null && player.Bag != null ? player.Bag.Container : null;
        ItemContainer stash = GameSession.Stash != null ? GameSession.Stash.Items : null;
        for (int hand = 0; hand < 2; hand++)
        {
            bool weapon = hand == 0;
            int best = -1;
            float bestScore = float.MinValue;
            for (int i = 0; bag != null && i < bag.SlotCount; i++)
            {
                ItemStack stack = bag.GetSlot(i);
                if (stack.IsEmpty || stack.IsBroken || !CompanionKit.Fits(record, stack.Item, weapon))
                {
                    continue;
                }

                float score = (weapon ? stack.Item.Weapon.Damage : stack.Item.Tier) + (int)stack.Quality * 0.5f + stack.Durability * 0.1f;
                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }

            if (best >= 0)
            {
                int slot = best;
                ItemStack offered = bag.GetSlot(best);
                actions.Add(new MenuAction(weapon ? "Give a weapon" : "Give armour", offered.DisplayName, true,
                    () => CompanionKit.Give(record, bag, slot, stash, player.transform)) { Icon = weapon ? "Sword" : "Bags" });
            }

            ItemStack had = weapon ? record.KitWeapon : record.KitBody;
            if (!had.IsEmpty && bag != null)
            {
                actions.Add(new MenuAction("Take it back", had.IsBroken ? $"{had.Item.DisplayName}, broken" : $"{had.Item.DisplayName}, {Mathf.RoundToInt(had.Durability * 100f)}%", true,
                    () => CompanionKit.TakeBack(record, weapon, bag, stash, player.transform)) { Icon = "Return" });
            }
        }

        return actions;
    }

    // A kit icon for a skill, by what it trains.
    private static string IconOf(ProficiencyDefinition skill)
    {
        switch (skill.Training)
        {
            case ProficiencyTraining.Melee: return "Sword";
            case ProficiencyTraining.Archery: return "Crit";
            case ProficiencyTraining.Sneaking: return "Eye";
            case ProficiencyTraining.Spells: return "Book";
            case ProficiencyTraining.Fishing: return "Drop";
            case ProficiencyTraining.Construction: return "House";
            case ProficiencyTraining.Horsemanship: return "HorseHead";
            case ProficiencyTraining.Crafting: return skill.Crafts == CraftedGoods.Meals ? "Pot" : "Hammer";
            default: return skill.GatherTool == ToolType.Pickaxe ? "Pickaxe" : skill.GatherTool == ToolType.Axe ? "Axe" : "Sickle";
        }
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
