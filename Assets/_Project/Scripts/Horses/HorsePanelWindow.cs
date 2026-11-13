using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A tamed horse's window, opened by using it (E): everything about it and everything to do with it, in place of a menu
/// of lines. A header with its live picture (a small camera beside it), its name (the pencil renames it), what it is and
/// five numbers with icons; then tabs: Overview (about it, its numbers, its traits; care and the main orders), Care
/// (feeding, grooming, work), Equipment (saddle, barding, bags, cart), Skills (what it is learning, how far along) and
/// History (where it came from, how far it has gone). Its buttons are the horse's own menu (Horse.MenuFor), sorted by
/// MenuAction.Group, so what can be done stays in one place. The player stands still while it is open (PlayerHold), and
/// it closes if the horse goes out of reach.
/// <para>Hearthline, after the user's picture (2026-11-13): a solid panel with gilded scroll corners, rounded cards with a
/// gold rim, the display face for its name, coloured bars and chips. Art: Tools/UI/hearthline_horse_art.py, in
/// Resources/HorsePanelArt (BuildHorses). Built in code the first time it is opened.</para>
/// </summary>
public sealed class HorsePanelWindow : MonoBehaviour
{
    private const float Width = 1180f;
    private const float Height = 930f;
    private const float Margin = 34f;
    private const float BodyTop = 384f;
    private const float BodyHeight = Height - BodyTop - Margin;
    private const float RefreshSeconds = 0.5f;
    private static readonly string[] TabNames = { "Overview", "Care", "Equipment", "Skills", "History" };

    private enum Tab
    {
        Overview = 0,
        Care = 1,
        Equipment = 2,
        Skills = 3,
        History = 4,
    }

    private static HorsePanelWindow current;

    private UIWindow window;
    private RectTransform panelRect;
    private HorsePanelArt art;
    private Horse horse;
    private GameObject user;
    private float reach = 3.5f;
    private float nextRefresh;
    private MenuPage page;
    private Tab tab;

    private RawImage picture;
    private RenderTexture texture;
    private Camera portrait;

    private TMP_Text nameLabel;
    private RectTransform pencil;
    private TMP_Text subtitle;
    private TMP_Text summary;
    private readonly StatRow[] headerRows = new StatRow[5];

    private readonly List<TabButton> tabs = new List<TabButton>();
    private readonly RectTransform[] pages = new RectTransform[TabNames.Length];

    private TMP_Text about;
    private readonly StatRow[] statRows = new StatRow[5];
    private RectTransform chips;
    private readonly List<Chip> chipPool = new List<Chip>();
    private TMP_Text careLine;
    private readonly StatRow[] careRows = new StatRow[3];
    private ActionButton feedButton;
    private ActionButton groomButton;
    private TMP_Text groomedLine;
    private readonly List<ActionButton> mainButtons = new List<ActionButton>();
    private TMP_Text tip;

    private ActionList careList;
    private TMP_Text careSummary;
    private ActionList gearList;
    private BagGrid bagGrid;
    private GearSlot saddleSlot;
    private GearSlot bardingSlot;
    private GearSlot bagsSlot;
    private readonly TraitRow[] traitRows = new TraitRow[4];
    private TMP_Text natureName;
    private TMP_Text natureText;
    private readonly HistoryRow[] historyRows = new HistoryRow[7];

    private static readonly (HorseTrait trait, string name, string learnt, string how)[] Traits =
    {
        (HorseTrait.SureFooted, "Sure-footed", "Rough ground no longer slows it.", "Ride it over rough ground: rock, slopes, the forest floor."),
        (HorseTrait.Swimmer, "Swimmer", "It swims faster and tires less in the water.", "Swim it across rivers and meres."),
        (HorseTrait.Enduring, "Enduring", "Its breath comes back sooner and a gallop costs it less.", "Ride it far, a great many kilometres."),
        (HorseTrait.Brave, "Brave", "It never panics, throws you or shies from monsters.", "Fight from its back: it learns from the blows it takes."),
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => current = null;

    /// <summary>True while it is open.</summary>
    public static bool IsOpen => current != null && current.window != null && current.window.IsOpen;

    /// <summary>Opens it on this horse, for <paramref name="who"/> (whose bag it feeds from), until they are further than
    /// <paramref name="range"/> from it.</summary>
    public static void Show(Horse horse, GameObject who, float range)
    {
        if (horse == null || who == null)
        {
            return;
        }

        if (current == null)
        {
            var root = new GameObject("Horse Window");
            root.SetActive(false);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 205;
            UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
            root.AddComponent<GraphicRaycaster>();
            current = root.AddComponent<HorsePanelWindow>();
            current.Build(root.transform);
            root.SetActive(true);
        }

        if (current.horse != null && current.horse != horse && current.horse.Motion != null)
        {
            current.horse.Motion.SetInspected(current, false);
        }

        current.horse = horse;
        current.user = who;
        if (current.window.IsOpen && horse.Motion != null)
        {
            horse.Motion.SetInspected(current, true);
        }

        current.reach = range;
        // Open first: its labels are measured as they are filled, which they can't be while hidden.
        current.window.Open();
        current.FitToScreen();
        current.ShowTab(Tab.Overview);
    }

    // ----- Building it -----

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        art = HorsePanelArt.Current;
        RectTransform content = CodeUI.Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.4f);
        RectTransform panel = CodeUI.Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panelRect = panel;
        var surface = panel.gameObject.AddComponent<Image>();
        surface.color = style.Panel;
        if (style.PanelMaterial != null)
        {
            surface.material = style.PanelMaterial;
        }

        var rim = panel.gameObject.AddComponent<Outline>();
        rim.effectColor = Faded(style.Accent, 0.55f);
        rim.effectDistance = new Vector2(1.5f, -1.5f);
        Corners(panel, style);

        BuildHeader(panel, style);
        BuildTabs(panel, style);
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i] = CodeUI.At(TabNames[i] + " Page", panel, Margin, BodyTop, Width - Margin * 2f, BodyHeight);
        }

        BuildOverview(pages[(int)Tab.Overview], style);
        BuildCare(pages[(int)Tab.Care], style);
        BuildEquipment(pages[(int)Tab.Equipment], style);
        BuildSkills(pages[(int)Tab.Skills], style);
        BuildHistory(pages[(int)Tab.History], style);

        // Close: a round × in the top right corner, over the scrollwork.
        RectTransform close = CodeUI.At("Close", panel, Width - 66f, 18f, 46f, 46f);
        Image closeFill = Sliced(close.gameObject.AddComponent<Image>(), art?.Card, 1f);
        closeFill.color = style.Inset;
        Image closeRim = Sliced(CodeUI.Stretch("Rim", close).gameObject.AddComponent<Image>(), art?.CardRim, 1f);
        closeRim.color = Faded(style.Accent, 0.8f);
        closeRim.raycastTarget = false;
        var closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeFill;
        Tint(closeButton, style);
        closeButton.onClick.AddListener(() => window.Close());
        TMP_Text cross = CodeUI.Label("X", close, 0f, -2f, 46f, 46f, 30f, style.Text, TextAlignmentOptions.Center);
        cross.text = "×";

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.HorsePanel, content.gameObject);
        window.OpenChanged += open =>
        {
            PlayerHold.Set(this, open);
            if (horse != null && horse.Motion != null)
            {
                horse.Motion.SetInspected(this, open);
            }

            if (!open && portrait != null)
            {
                portrait.enabled = false;
            }
        };
        PadTabs.Register(this, () => window != null && window.IsOpen, step => ShowTab((Tab)PadTabs.Next((int)tab, TabNames.Length, step)),
            () => tabs.ConvertAll(t => t.Rect));
    }

    // Smaller where the screen (or a large UI scale) leaves too little room: the whole window, kept in proportion.
    private void FitToScreen()
    {
        var canvas = (RectTransform)transform;
        Rect room = canvas.rect;
        float scale = Mathf.Min(1f, (room.height - 40f) / Height, (room.width - 40f) / Width);
        if (panelRect != null && scale > 0.1f)
        {
            panelRect.localScale = Vector3.one * scale;
        }
    }

    private void Corners(RectTransform panel, UIStyle style)
    {
        if (art == null || art.Corner == null)
        {
            return;
        }

        const float size = 150f;
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                RectTransform corner = CodeUI.Box("Scroll Corner", panel, new Vector2(x, y), Vector2.one * size,
                    new Vector2(x == 0 ? size * 0.5f + 6f : -size * 0.5f - 6f, y == 0 ? size * 0.5f + 6f : -size * 0.5f - 6f));
                var image = corner.gameObject.AddComponent<Image>();
                image.sprite = art.Corner;
                image.color = Faded(style.Accent, 0.9f);
                image.raycastTarget = false;
                corner.localScale = new Vector3(x == 0 ? 1f : -1f, y == 1 ? 1f : -1f, 1f);
            }
        }
    }

    private void BuildHeader(RectTransform panel, UIStyle style)
    {
        // The picture, in a gold-rimmed frame.
        RectTransform frame = CodeUI.At("Picture", panel, Margin + 8f, Margin + 8f, 330f, 250f);
        Sliced(frame.gameObject.AddComponent<Image>(), art?.Card, 1.5f).color = style.Inset;
        RectTransform inner = CodeUI.Stretch("Image", frame);
        inner.offsetMin = new Vector2(5f, 5f);
        inner.offsetMax = new Vector2(-5f, -5f);
        inner.gameObject.AddComponent<RectMask2D>();
        picture = CodeUI.Stretch("Render", inner).gameObject.AddComponent<RawImage>();
        texture = new RenderTexture(660, 500, 24) { name = "Horse Portrait" };
        picture.texture = texture;
        picture.raycastTarget = false;
        Image frameRim = Sliced(CodeUI.Stretch("Rim", frame).gameObject.AddComponent<Image>(), art?.CardRim, 1.5f);
        frameRim.color = Faded(style.Accent, 0.85f);
        frameRim.raycastTarget = false;

        float x = Margin + 372f;
        nameLabel = CodeUI.Label("Name", panel, x, Margin - 6f, 420f, 92f, 52f, style.Text);
        style.Display(nameLabel);
        // Its display face is taller than its size: with an ellipsis a box shorter than its line would drop the name whole.
        nameLabel.overflowMode = TextOverflowModes.Overflow;
        nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
        pencil = CodeUI.At("Rename", panel, x + 200f, Margin + 22f, 38f, 38f);
        var pencilHit = pencil.gameObject.AddComponent<Image>();
        pencilHit.color = new Color(1f, 1f, 1f, 0f);
        var rename = pencil.gameObject.AddComponent<Button>();
        rename.targetGraphic = pencilHit;
        rename.onClick.AddListener(() => Choose(Find("Name")));
        Icon(pencil, "Pencil", 6f, 6f, 26f, style.Muted);
        subtitle = CodeUI.Label("Subtitle", panel, x, Margin + 82f, 400f, 30f, 21f, style.Accent);
        CodeUI.Hairline(panel, x, Margin + 120f, 380f, 1f, style);
        summary = CodeUI.Paragraph("Summary", panel, x, Margin + 134f, 384f, 120f, 20f, style.Muted);

        string[] names = { "Health", "Stamina", "Carry", "Speed", "Bond" };
        string[] icons = { "Heart", "Bolt", "Weight", "Horseshoe", "Bond" };
        for (int i = 0; i < names.Length; i++)
        {
            bool bar = i == 0 || i == 1 || i == 4;
            headerRows[i] = new StatRow(panel, Width - Margin - 360f, Margin + 14f + i * 50f, 352f, names[i], icons[i], bar, 21f, art, style);
        }
    }

    private void BuildTabs(RectTransform panel, UIStyle style)
    {
        const float tabWidth = 178f;
        for (int i = 0; i < TabNames.Length; i++)
        {
            Tab chosen = (Tab)i;
            tabs.Add(new TabButton(panel, Margin + 8f + i * (tabWidth + 10f), 312f, tabWidth, 54f, TabNames[i], art, style, () => ShowTab(chosen)));
        }

        RectTransform line = CodeUI.At("Tab Line", panel, Margin, 370f, Width - Margin * 2f, 1f);
        line.gameObject.AddComponent<Image>().color = Faded(style.Accent, 0.35f);
    }

    private void BuildOverview(RectTransform page, UIStyle style)
    {
        const float leftWidth = 548f;
        RectTransform left = Card(page, "About Card", 0f, 0f, leftWidth, BodyHeight, style);
        Section(left, "About", "Info", 22f, 18f, style);
        about = CodeUI.Paragraph("About", left, 24f, 58f, leftWidth - 48f, 78f, 19f, style.Text);
        CodeUI.Hairline(left, 22f, 142f, leftWidth - 44f, 1f, style);
        Section(left, "Stats", "Stats", 22f, 156f, style);
        string[] names = { "Health", "Stamina", "Carry capacity", "Speed", "Bond" };
        string[] icons = { "Heart", "Bolt", "Weight", "Horseshoe", "Bond" };
        for (int i = 0; i < names.Length; i++)
        {
            bool bar = i == 0 || i == 1 || i == 4;
            statRows[i] = new StatRow(left, 24f, 196f + i * 38f, leftWidth - 48f, names[i], icons[i], bar, 18f, art, style, inline: true);
        }

        CodeUI.Hairline(left, 22f, 392f, leftWidth - 44f, 1f, style);
        Section(left, "Traits", "Star", 22f, 404f, style);
        chips = CodeUI.At("Chips", left, 24f, 444f, leftWidth - 48f, 40f);

        float rx = leftWidth + 16f;
        float rightWidth = Width - Margin * 2f - rx;
        RectTransform right = Card(page, "Care Card", rx, 0f, rightWidth, BodyHeight, style);
        Section(right, "Care", "Wheat", 22f, 18f, style);
        careLine = CodeUI.Label("Line", right, 24f, 54f, rightWidth - 48f, 26f, 17f, style.Muted);
        string[] care = { "Health", "Stamina", "Bond" };
        string[] careIcons = { "Heart", "Bolt", "Bond" };
        for (int i = 0; i < care.Length; i++)
        {
            careRows[i] = new StatRow(right, 24f, 92f + i * 46f, rightWidth - 190f, care[i], careIcons[i], true, 18f, art, style, inline: true, labelWidth: 100f, valueWidth: 50f);
        }

        feedButton = new ActionButton(right, rightWidth - 150f, 86f, 126f, 38f, art, style, Choose, compact: true);
        groomButton = new ActionButton(right, rightWidth - 150f, 178f, 126f, 38f, art, style, Choose, compact: true);
        groomedLine = CodeUI.Label("Groomed", right, 24f, 226f, rightWidth - 48f, 24f, 17f, style.Muted);
        CodeUI.Hairline(right, 22f, 262f, rightWidth - 44f, 1f, style);
        Section(right, "Actions", "Gear", 22f, 276f, style);
        const float buttonWidth = 122f;
        for (int i = 0; i < 4; i++)
        {
            mainButtons.Add(new ActionButton(right, 24f + i * (buttonWidth + 9f), 316f, buttonWidth, 84f, art, style, Choose));
        }

        RectTransform tipBox = Card(right, "Tip", 22f, 418f, rightWidth - 44f, 72f, style, 0.6f);
        Icon(tipBox, "Info", 16f, 21f, 30f, style.Accent);
        tip = CodeUI.Paragraph("Tip", tipBox, 58f, 10f, rightWidth - 44f - 74f, 56f, 16f, style.Muted);
        tip.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void BuildCare(RectTransform page, UIStyle style)
    {
        RectTransform card = Card(page, "Care Card", 0f, 0f, Width - Margin * 2f, BodyHeight, style);
        Section(card, "Care and work", "Brush", 22f, 18f, style);
        careSummary = CodeUI.Paragraph("Summary", card, 24f, 56f, Width - Margin * 2f - 48f, 54f, 18f, style.Muted);
        careList = new ActionList(card, 22f, 118f, Width - Margin * 2f - 44f, BodyHeight - 140f, art, style, Choose);
    }

    private void BuildEquipment(RectTransform page, UIStyle style)
    {
        float width = Width - Margin * 2f;
        RectTransform card = Card(page, "Gear Card", 0f, 0f, width, BodyHeight, style);
        Section(card, "Equipment", "Saddle", 22f, 18f, style);
        float slot = (width - 44f - 24f) / 3f;
        saddleSlot = new GearSlot(card, 22f, 58f, slot, "Saddle", "Saddle", art, style);
        bardingSlot = new GearSlot(card, 22f + slot + 12f, 58f, slot, "Barding", "Barding", art, style);
        bagsSlot = new GearSlot(card, 22f + (slot + 12f) * 2f, 58f, slot, "Saddle bags", "Bags", art, style);
        const float listWidth = 500f;
        gearList = new ActionList(card, 22f, 166f, listWidth, BodyHeight - 188f, art, style, Choose, columns: 1, rowHeight: 58f);
        // What it carries: its bags' slots as they are; a click opens them.
        float bagsX = 22f + listWidth + 24f;
        Section(card, "In its bags", "Bags", bagsX, 166f, style);
        bagGrid = new BagGrid(card, bagsX, 206f, width - bagsX - 22f, art, style, () => Choose(Find("Gear", "Saddle bags")));
    }

    private void BuildSkills(RectTransform page, UIStyle style)
    {
        float width = Width - Margin * 2f;
        RectTransform card = Card(page, "Skills Card", 0f, 0f, width, BodyHeight, style);
        Section(card, "What it learns", "Star", 22f, 18f, style);
        for (int i = 0; i < traitRows.Length; i++)
        {
            traitRows[i] = new TraitRow(card, 22f, 58f + i * 84f, width - 44f, art, style);
        }

        CodeUI.Hairline(card, 22f, 400f, width - 44f, 1f, style);
        Section(card, "Its nature", "HorseHead", 22f, 412f, style);
        natureName = CodeUI.Label("Nature", card, 24f, 448f, 300f, 26f, 20f, style.Accent);
        natureText = CodeUI.Paragraph("Nature Text", card, 220f, 448f, width - 260f, 48f, 17f, style.Muted);
    }

    private void BuildHistory(RectTransform page, UIStyle style)
    {
        float width = Width - Margin * 2f;
        RectTransform card = Card(page, "History Card", 0f, 0f, width, BodyHeight, style);
        Section(card, "Its story", "Hourglass", 22f, 18f, style);
        for (int i = 0; i < historyRows.Length; i++)
        {
            historyRows[i] = new HistoryRow(card, 22f, 62f + i * 58f, width - 44f, style);
        }
    }

    // ----- Showing it -----

    private void ShowTab(Tab shown)
    {
        tab = shown;
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].gameObject.SetActive(i == (int)shown);
            tabs[i].Set(i == (int)shown);
        }

        Refresh();
    }

    private void Update()
    {
        if (window == null || !window.IsOpen)
        {
            return;
        }

        if (horse == null || !horse.IsAlive || !horse.IsTamed || user == null
            // Attacked while the player looks it over, it is free to move, and the window gives the player back their hands.
            || horse.Motion != null && !horse.Motion.HeldForInspection
            || (user.transform.position - horse.transform.position).sqrMagnitude > (reach + 8f) * (reach + 8f))
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
        if (horse == null || user == null)
        {
            return;
        }

        nextRefresh = Time.unscaledTime + RefreshSeconds;
        page = horse.MenuFor(user);
        UIStyle style = UIStyle.Current;
        HorseDefinition kind = horse.Definition;
        HorseStats stats = horse.Stats;

        // Header.
        nameLabel.text = horse.HorseName;
        nameLabel.ForceMeshUpdate();
        MenuAction naming = Find("Name");
        pencil.gameObject.SetActive(naming != null && naming.Enabled);
        pencil.anchoredPosition = new Vector2(nameLabel.rectTransform.anchoredPosition.x + Mathf.Min(nameLabel.preferredWidth, 400f) + 14f, pencil.anchoredPosition.y);
        subtitle.text = $"{Capital(stats.SexName)}  •  {Capital(Grade(horse))} {kind.DisplayName.ToLowerInvariant()}";
        summary.text = Summary(horse);
        float Share(float value, Vector2 range) => Mathf.InverseLerp(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y), value);
        float health = horse.Health.HealthFraction;
        float breath = horse.MaxStamina > 0f ? horse.Stamina / horse.MaxStamina : 0f;
        float bond = horse.CurrentBond() / HorseBondRules.Most;
        headerRows[0].Set($"{horse.Health.CurrentHealth:0} / {horse.Health.MaxHealth:0}", health, style.Good, style.Danger);
        headerRows[1].Set($"{horse.Stamina:0} / {horse.MaxStamina:0}", breath, style.Water, style.Warning);
        headerRows[2].Set($"{stats.carryWeight:0} kg", 0f, style.Text, style.Accent);
        headerRows[3].Set($"{stats.gallopSpeed:0.0} m/s", 0f, style.Text, style.Accent);
        headerRows[4].Set($"{horse.CurrentBond():0} / 100", bond, style.Accent, style.Danger);

        switch (tab)
        {
            case Tab.Overview:
                RefreshOverview(style, kind, stats, health, breath, bond, Share);
                break;
            case Tab.Care:
                careSummary.text = CareSummary(horse);
                careList.Show(Actions("Care"));
                break;
            case Tab.Equipment:
                saddleSlot.Set(horse.Saddle != null ? horse.Saddle.DisplayName : "No saddle", horse.Saddle != null ? $"{horse.Saddle.SaddleSlots} places in its bags" : "Without one it carries nothing", horse.Saddle != null);
                bardingSlot.Set(horse.Armour != null ? horse.Armour.DisplayName : "No barding", horse.Armour != null ? $"Keeps {horse.Armour.HorseArmour * 100f:0}% of a blow off" : "Iron or hide keeps blows off it", horse.Armour != null);
                bagsSlot.Set(horse.Saddle != null ? $"{horse.Weight:0} / {horse.Capacity:0} weight" : "None yet", horse.Saddle != null ? (horse.IsOverfilled ? "Too heavy: it slows" : "More than six tenths slows it") : "They come with a saddle", horse.Saddle != null,
                    horse.Saddle != null ? horse.Weight / Mathf.Max(1f, horse.Capacity) : -1f);
                gearList.Show(Actions("Gear"));
                bagGrid.Show(horse.Saddle != null ? horse.Cargo : null, horse.Saddle != null ? horse.Saddle.SaddleSlots : 0);
                break;
            case Tab.Skills:
                for (int i = 0; i < Traits.Length; i++)
                {
                    bool learnt = stats.Has(Traits[i].trait);
                    traitRows[i].Set(Traits[i].name, learnt ? Traits[i].learnt : Traits[i].how, learnt, horse.TraitProgress(Traits[i].trait));
                }

                natureName.text = Capital(HorseTemperamentRules.NameOf(stats.temperament));
                natureText.text = string.IsNullOrWhiteSpace(horse.Temperament.description) ? "" : horse.Temperament.description;
                break;
            case Tab.History:
                RefreshHistory(stats);
                break;
        }
    }

    private void RefreshOverview(UIStyle style, HorseDefinition kind, HorseStats stats, float health, float breath, float bond, Func<float, Vector2, float> share)
    {
        about.text = About(horse);
        statRows[0].Set($"{horse.Health.CurrentHealth:0} / {horse.Health.MaxHealth:0}", health, style.Good, style.Danger);
        statRows[1].Set($"{horse.Stamina:0} / {horse.MaxStamina:0}", breath, style.Water, style.Warning);
        statRows[2].Set($"{stats.carryWeight:0} kg", 0f, style.Text, style.Accent);
        statRows[3].Set($"{stats.gallopSpeed:0.0} m/s", 0f, style.Text, style.Accent);
        statRows[4].Set($"{horse.CurrentBond():0} / 100", bond, style.Accent, style.Danger);

        var shown = new List<(string, Color)> { (Capital(HorseTemperamentRules.NameOf(stats.temperament)), style.Good) };
        foreach (var (trait, name, _, _) in Traits)
        {
            if (stats.Has(trait))
            {
                shown.Add((name, style.Info));
            }
        }

        if (share(stats.gallopSpeed, kind.GallopSpeed) >= 0.75f)
        {
            shown.Add(("Fast", style.Info));
        }

        if (share(stats.carryWeight, kind.CarryWeight) >= 0.75f)
        {
            shown.Add(("Strong", style.Info));
        }

        if (!stats.IsGrown)
        {
            shown.Add(("Foal", style.Warning));
        }

        ShowChips(shown, style);

        careLine.text = $"Keep {horse.HorseName} well, and it does more for you.";
        careRows[0].Set($"{horse.Health.CurrentHealth:0}", health, style.Good, style.Danger);
        careRows[1].Set($"{horse.Stamina:0}", breath, style.Water, style.Warning);
        careRows[2].Set($"{horse.CurrentBond():0}", bond, style.Accent, style.Danger);
        feedButton.Bind(Find("Care", "Feed it"), "Feed");
        groomButton.Bind(Find("Care", "Groom it"), "Groom");
        bool groomed = PlantedTrees.Today - horse.LastGroomed < 1f;
        groomedLine.text = groomed ? "Groomed today: it is content." : "Not groomed today: a brush once a day grows its bond.";

        List<MenuAction> main = Actions("Main");
        for (int i = 0; i < mainButtons.Count; i++)
        {
            mainButtons[i].Bind(i < main.Count ? main[i] : null, null);
        }

        tip.text = Tip(horse, groomed);
    }

    private void RefreshHistory(HorseStats stats)
    {
        string owner = horse.IsMine ? "You" : string.IsNullOrWhiteSpace(horse.OwnerName) ? "A friend" : horse.OwnerName;
        string origin = stats.generation > 0 ? $"Born in your stable, generation {stats.generation}" : "Caught wild and broken to the saddle";
        string age = stats.IsGrown ? "Fully grown" : $"A foal, {stats.growth * 100f:0}% grown";
        string doing = horse.IsStabled ? "In the stable" : horse.Order == Horse.Orders.Follow ? "Following you" : horse.Order == Horse.Orders.Stay ? "Staying where it is"
            : horse.Order == Horse.Orders.Work ? "At work" : horse.Rider != null ? "Ridden" : "Grazing";
        historyRows[0].Set("Where it came from", origin);
        historyRows[1].Set("Age", age);
        historyRows[2].Set("Ridden", horse.RiddenMetres >= 1000f ? $"{horse.RiddenMetres / 1000f:0.0} km" : $"{horse.RiddenMetres:0} m");
        historyRows[3].Set("Its rider", owner);
        historyRows[4].Set("Now", doing);
        historyRows[5].Set("Learned", horse.TraitNames().Length > 0 ? horse.TraitNames() : "Nothing yet");
        historyRows[6].Set("Foal", horse.WithFoalSince >= 0f ? "With foal" : "None on the way");
    }

    private void ShowChips(List<(string text, Color colour)> shown, UIStyle style)
    {
        while (chipPool.Count < shown.Count)
        {
            chipPool.Add(new Chip(chips, art, style));
        }

        float x = 0f;
        for (int i = 0; i < chipPool.Count; i++)
        {
            bool used = i < shown.Count;
            chipPool[i].Root.SetActive(used);
            if (used)
            {
                x += chipPool[i].Set(shown[i].text, shown[i].colour, x) + 10f;
            }
        }
    }

    private List<MenuAction> Actions(string group)
    {
        var found = new List<MenuAction>();
        if (page != null)
        {
            foreach (MenuAction action in page.Actions)
            {
                if (action.Group == group)
                {
                    found.Add(action);
                }
            }
        }

        return found;
    }

    private MenuAction Find(string group, string label = null)
    {
        if (page == null)
        {
            return null;
        }

        foreach (MenuAction action in page.Actions)
        {
            if (action.Group == group && (label == null || action.Label == label))
            {
                return action;
            }
        }

        return null;
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
            Refresh();
        }
    }

    // ----- Words -----

    private static string Grade(Horse horse)
    {
        float quality = horse.Definition != null ? horse.Stats.Quality(horse.Definition) : 0f;
        return quality >= 0.8f ? "fine" : quality >= 0.5f ? "good" : quality >= 0.25f ? "ordinary" : "poor";
    }

    private static string Summary(Horse horse)
    {
        HorseStats stats = horse.Stats;
        string nature = HorseTemperamentRules.NameOf(stats.temperament);
        string where = horse.IsStabled ? "resting in the stable" : horse.Order == Horse.Orders.Follow ? "following you"
            : horse.Order == Horse.Orders.Stay ? "waiting where you left it" : horse.Order == Horse.Orders.Work ? "hard at work" : "grazing nearby";
        return $"A {nature} {stats.SexName}, {where}." + (horse.Saddle != null ? $" It wears a {horse.Saddle.DisplayName.ToLowerInvariant()}." : " It has no saddle yet.");
    }

    private static string About(Horse horse)
    {
        HorseStats stats = horse.Stats;
        string nature = horse.Temperament.description;
        string bred = stats.generation > 0 ? $" Born in your stable, generation {stats.generation}." : "";
        string foal = horse.WithFoalSince >= 0f ? " She is with foal." : "";
        return (string.IsNullOrWhiteSpace(nature) ? $"{horse.HorseName} is {HorseTemperamentRules.NameOf(stats.temperament)} by nature." : nature) + bred + foal;
    }

    private static string CareSummary(Horse horse)
    {
        bool groomed = PlantedTrees.Today - horse.LastGroomed < 1f;
        return $"Barley, a turnip or bread from your bag mends it; a brush once a day grows its bond{(groomed ? " (done today)" : "")}. " +
               "In the stable it heals, safe from the night; near a station it can work for you.";
    }

    private static string Tip(Horse horse, bool groomed)
    {
        if (horse.Saddle == null)
        {
            return "A hide saddle, made at the Workshop, gives it bags to carry your load.";
        }

        if (!groomed)
        {
            return "Groom it today: a horse that is fond of you has more breath and fears less.";
        }

        if (horse.CurrentBond() < 40f)
        {
            return "Ride it far and it grows fond of you; at a bond of 40 it strikes out at your command.";
        }

        return "Tap the canter key to cruise, hold it to gallop; Space jumps what is ahead.";
    }

    private static string Capital(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

    // ----- Parts -----

    private static Color Faded(Color colour, float alpha)
    {
        colour.a *= alpha;
        return colour;
    }

    private static Image Sliced(Image image, Sprite sprite, float multiplier)
    {
        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = multiplier;
        }

        return image;
    }

    private static void Tint(Button button, UIStyle style)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.25f, 1.2f, 1.1f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = new Color(1.1f, 1.08f, 1.04f, 1f);
        colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.7f);
        colors.colorMultiplier = 1.4f;
        button.colors = colors;
    }

    // A rounded card: the inset colour, a soft gold rim.
    private RectTransform Card(Transform parent, string name, float x, float y, float width, float height, UIStyle style, float rimAlpha = 0.45f)
    {
        RectTransform card = CodeUI.At(name, parent, x, y, width, height);
        Image fill = Sliced(card.gameObject.AddComponent<Image>(), art?.Card, 1.2f);
        fill.color = style.Inset;
        fill.raycastTarget = false;
        Image rim = Sliced(CodeUI.Stretch("Rim", card).gameObject.AddComponent<Image>(), art?.CardRim, 1.2f);
        rim.color = Faded(style.Accent, rimAlpha);
        rim.raycastTarget = false;
        return card;
    }

    // A section heading: its icon, then its name in the title face.
    private void Section(Transform parent, string text, string icon, float x, float y, UIStyle style)
    {
        Icon(parent, icon, x, y + 3f, 24f, style.Accent);
        TMP_Text heading = CodeUI.Label(text + " Heading", parent, x + 34f, y - 2f, 400f, 32f, 23f, style.Text);
        style.Title(heading);
        heading.text = text;
        heading.fontStyle = FontStyles.Bold;
    }

    private Image Icon(Transform parent, string name, float x, float y, float size, Color colour)
    {
        Image image = CodeUI.At(name + " Icon", parent, x, y, size, size).gameObject.AddComponent<Image>();
        image.sprite = art != null ? art.Icon(name) : null;
        image.color = colour;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = image.sprite != null;
        return image;
    }

    private static Color IconColour(string icon, UIStyle style)
    {
        switch (icon)
        {
            case "Heart": return Color.Lerp(style.Danger, style.Accent, 0.35f);
            case "Bolt": return Color.Lerp(style.Warning, style.Accent, 0.3f);
            case "Bond": return Color.Lerp(style.Danger, style.Accent, 0.5f);
            default: return style.Accent;
        }
    }

    // ----- The window's pieces, each built once and rebound on refresh -----

    // A number with its icon: label, value, and (for some) a bar under or beside it.
    private sealed class StatRow
    {
        private readonly TMP_Text value;
        private readonly RectTransform fill;
        private readonly Image fillImage;
        private readonly bool hasBar;

        public StatRow(Transform parent, float x, float y, float width, string name, string icon, bool bar, float size, HorsePanelArt art, UIStyle style, bool inline = false,
            float labelWidth = 160f, float valueWidth = 110f)
        {
            hasBar = bar;
            Image picture = CodeUI.At(icon + " Icon", parent, x, y + 2f, size + 6f, size + 6f).gameObject.AddComponent<Image>();
            picture.sprite = art != null ? art.Icon(icon) : null;
            picture.enabled = picture.sprite != null;
            picture.color = IconColour(icon, style);
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            float labelX = x + size + 18f;
            TMP_Text label = CodeUI.Label(name, parent, labelX, y, labelWidth, size + 10f, size, style.Text);
            label.text = name;
            float valueX = inline ? labelX + labelWidth : x + width - 120f;
            value = CodeUI.Label("Value", parent, valueX, y, inline ? valueWidth : 120f, size + 10f, size, style.Text, inline ? TextAlignmentOptions.Left : TextAlignmentOptions.Right);
            if (!bar)
            {
                return;
            }

            float barX = inline ? valueX + valueWidth + 2f : labelX;
            float barY = inline ? y + size * 0.5f + 1f : y + size + 12f;
            float barWidth = inline ? x + width - barX : x + width - labelX;
            RectTransform track = CodeUI.At("Track", parent, barX, barY, barWidth, 9f);
            Sliced(track.gameObject.AddComponent<Image>(), art?.Card, 7f).color = style.Track;
            fill = CodeUI.Stretch("Fill", track);
            fillImage = Sliced(fill.gameObject.AddComponent<Image>(), art?.Card, 7f);
            fillImage.raycastTarget = false;
        }

        public void Set(string shown, float fraction, Color colour, Color low)
        {
            value.text = shown;
            if (!hasBar)
            {
                return;
            }

            fill.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            fillImage.color = fraction < 0.25f ? low : colour;
        }
    }

    private sealed class TabButton
    {
        public readonly RectTransform Rect;
        private readonly Image fill;
        private readonly Image rim;
        private readonly TMP_Text label;

        public TabButton(Transform parent, float x, float y, float width, float height, string text, HorsePanelArt art, UIStyle style, Action clicked)
        {
            Rect = CodeUI.At(text + " Tab", parent, x, y, width, height);
            fill = Sliced(Rect.gameObject.AddComponent<Image>(), art?.Card, 1.4f);
            rim = Sliced(CodeUI.Stretch("Rim", Rect).gameObject.AddComponent<Image>(), art?.CardRim, 1.4f);
            rim.raycastTarget = false;
            var button = Rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            Tint(button, style);
            button.onClick.AddListener(() => clicked());
            label = CodeUI.Label("Label", Rect, 0f, 0f, width, height, 21f, style.Text, TextAlignmentOptions.Center);
            label.text = text;
            style.Title(label);
        }

        public void Set(bool on)
        {
            UIStyle style = UIStyle.Current;
            fill.color = on ? Color.Lerp(style.Inset, style.AccentDeep, 0.55f) : style.Inset;
            rim.color = on ? style.Accent : Faded(style.Accent, 0.3f);
            label.color = on ? style.AccentBright : style.Text;
            label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private sealed class Chip
    {
        public readonly GameObject Root;
        private readonly RectTransform rect;
        private readonly Image fill;
        private readonly Image rim;
        private readonly TMP_Text label;

        public Chip(Transform parent, HorsePanelArt art, UIStyle style)
        {
            rect = CodeUI.At("Chip", parent, 0f, 0f, 100f, 36f);
            Root = rect.gameObject;
            fill = Sliced(Root.AddComponent<Image>(), art?.Card, 1.1f);
            fill.raycastTarget = false;
            rim = Sliced(CodeUI.Stretch("Rim", rect).gameObject.AddComponent<Image>(), art?.CardRim, 1.1f);
            rim.raycastTarget = false;
            label = CodeUI.Label("Label", rect, 0f, 0f, 100f, 36f, 17f, style.Text, TextAlignmentOptions.Center);
        }

        // Returns its width.
        public float Set(string text, Color colour, float x)
        {
            label.text = text;
            label.ForceMeshUpdate();
            float width = label.preferredWidth + 36f;
            rect.sizeDelta = new Vector2(width, 36f);
            rect.anchoredPosition = new Vector2(x, 0f);
            label.rectTransform.sizeDelta = new Vector2(width, 36f);
            fill.color = Color.Lerp(UIStyle.Current.Inset, colour, 0.28f);
            rim.color = Faded(colour, 0.85f);
            label.color = Color.Lerp(colour, Color.white, 0.55f);
            return width;
        }
    }

    // A button for one of the horse's actions: its icon and name (and, in a list, its detail).
    private sealed class ActionButton
    {
        private readonly GameObject root;
        private readonly Button button;
        private readonly Image fill;
        private readonly Image rim;
        private readonly Image icon;
        private readonly TMP_Text label;
        private readonly TMP_Text detail;
        private readonly HorsePanelArt art;
        private readonly bool compact;
        private readonly bool row;
        private MenuAction action;

        public ActionButton(Transform parent, float x, float y, float width, float height, HorsePanelArt art, UIStyle style, Action<MenuAction> chosen,
            bool compact = false, bool row = false)
        {
            this.art = art;
            this.compact = compact;
            this.row = row;
            RectTransform rect = CodeUI.At("Action", parent, x, y, width, height);
            root = rect.gameObject;
            fill = Sliced(root.AddComponent<Image>(), art?.Card, compact ? 1.6f : 1.3f);
            rim = Sliced(CodeUI.Stretch("Rim", rect).gameObject.AddComponent<Image>(), art?.CardRim, compact ? 1.6f : 1.3f);
            rim.raycastTarget = false;
            button = root.AddComponent<Button>();
            button.targetGraphic = fill;
            Tint(button, style);
            button.onClick.AddListener(() => chosen(action));
            if (row)
            {
                icon = CodeUI.At("Icon", rect, 18f, (height - 34f) * 0.5f, 34f, 34f).gameObject.AddComponent<Image>();
                label = CodeUI.Label("Label", rect, 68f, 8f, width - 90f, 28f, 20f, style.Text);
                detail = CodeUI.Label("Detail", rect, 68f, 36f, width - 90f, 24f, 16f, style.Muted);
            }
            else if (compact)
            {
                icon = CodeUI.At("Icon", rect, 12f, (height - 22f) * 0.5f, 22f, 22f).gameObject.AddComponent<Image>();
                label = CodeUI.Label("Label", rect, 40f, 0f, width - 48f, height, 18f, style.Text, TextAlignmentOptions.Center);
            }
            else
            {
                icon = CodeUI.At("Icon", rect, (width - 34f) * 0.5f, 12f, 34f, 34f).gameObject.AddComponent<Image>();
                label = CodeUI.Label("Label", rect, 4f, height - 34f, width - 8f, 26f, 17f, style.Text, TextAlignmentOptions.Center);
            }

            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        public void Bind(MenuAction shown, string name)
        {
            action = shown;
            root.SetActive(shown != null);
            if (shown == null)
            {
                return;
            }

            UIStyle style = UIStyle.Current;
            button.interactable = shown.Enabled;
            label.text = name ?? shown.Label;
            label.color = shown.Enabled ? style.Text : style.Muted;
            if (detail != null)
            {
                detail.text = shown.Detail ?? "";
            }

            icon.sprite = art != null ? art.Icon(shown.Icon) : null;
            icon.enabled = icon.sprite != null;
            icon.color = shown.Enabled ? style.Accent : Faded(style.Muted, 0.7f);
            fill.color = row ? style.Panel : Color.Lerp(style.Inset, style.Control, 0.6f);
            rim.color = Faded(style.Accent, shown.Enabled ? (row ? 0.3f : 0.6f) : 0.18f);
        }
    }

    private sealed class ActionList
    {
        private readonly float rowHeight;
        private readonly int columns;
        private readonly List<ActionButton> rows = new List<ActionButton>();
        private readonly Transform parent;
        private readonly float x;
        private readonly float y;
        private readonly float width;
        private readonly HorsePanelArt art;
        private readonly UIStyle style;
        private readonly Action<MenuAction> chosen;
        private readonly TMP_Text empty;

        public ActionList(Transform parent, float x, float y, float width, float height, HorsePanelArt art, UIStyle style, Action<MenuAction> chosen,
            int columns = 2, float rowHeight = 68f)
        {
            this.columns = Mathf.Max(1, columns);
            this.rowHeight = rowHeight;
            this.parent = parent;
            this.x = x;
            this.y = y;
            this.width = width;
            this.art = art;
            this.style = style;
            this.chosen = chosen;
            empty = CodeUI.Label("Nothing", parent, x + 4f, y + 8f, width, 26f, 18f, style.Muted);
            empty.text = "Nothing to do here just now.";
        }

        public void Show(List<MenuAction> actions)
        {
            // In columns, so a long list stays in the card.
            float column = (width - 12f * (columns - 1)) / columns;
            while (rows.Count < actions.Count)
            {
                int i = rows.Count;
                rows.Add(new ActionButton(parent, x + (i % columns) * (column + 12f), y + (i / columns) * (rowHeight + 10f), column, rowHeight, art, style, chosen, row: true));
            }

            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].Bind(i < actions.Count ? actions[i] : null, null);
            }

            empty.gameObject.SetActive(actions.Count == 0);
        }
    }

    // Its saddle bags' slots: each item's icon and how many, empty slots dim; none without a saddle. A click opens the bags.
    private sealed class BagGrid
    {
        private const float Cell = 58f;
        private const float Gap = 8f;
        private readonly Transform parent;
        private readonly float x;
        private readonly float y;
        private readonly int perRow;
        private readonly HorsePanelArt art;
        private readonly UIStyle style;
        private readonly Action open;
        private readonly List<(GameObject root, Image icon, TMP_Text count, Image rim)> cells = new List<(GameObject, Image, TMP_Text, Image)>();
        private readonly TMP_Text note;

        public BagGrid(Transform parent, float x, float y, float width, HorsePanelArt art, UIStyle style, Action open)
        {
            this.parent = parent;
            this.x = x;
            this.y = y;
            this.art = art;
            this.style = style;
            this.open = open;
            perRow = Mathf.Max(1, Mathf.FloorToInt((width + Gap) / (Cell + Gap)));
            note = CodeUI.Paragraph("Note", parent, x + 2f, y, width, 60f, 17f, style.Muted);
        }

        public void Show(ItemContainer bags, int slots)
        {
            int count = bags != null ? Mathf.Min(slots, bags.SlotCount) : 0;
            while (cells.Count < count)
            {
                int i = cells.Count;
                RectTransform rect = CodeUI.At("Bag Slot", parent, x + (i % perRow) * (Cell + Gap), y + (i / perRow) * (Cell + Gap), Cell, Cell);
                Image fill = Sliced(rect.gameObject.AddComponent<Image>(), art?.Card, 1.8f);
                fill.color = style.Panel;
                Image rim = Sliced(CodeUI.Stretch("Rim", rect).gameObject.AddComponent<Image>(), art?.CardRim, 1.8f);
                rim.raycastTarget = false;
                var button = rect.gameObject.AddComponent<Button>();
                button.targetGraphic = fill;
                Tint(button, style);
                button.onClick.AddListener(() => open());
                Image icon = CodeUI.At("Icon", rect, 7f, 7f, Cell - 14f, Cell - 14f).gameObject.AddComponent<Image>();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                TMP_Text amount = CodeUI.Label("Count", rect, 0f, Cell - 22f, Cell - 6f, 20f, 15f, style.Text, TextAlignmentOptions.Right);
                cells.Add((rect.gameObject, icon, amount, rim));
            }

            int used = 0;
            for (int i = 0; i < cells.Count; i++)
            {
                bool shown = i < count;
                cells[i].root.SetActive(shown);
                if (!shown)
                {
                    continue;
                }

                ItemStack stack = bags.GetSlot(i);
                bool full = !stack.IsEmpty;
                used += full ? 1 : 0;
                cells[i].icon.sprite = full ? stack.Item.Icon : null;
                cells[i].icon.enabled = full && stack.Item.Icon != null;
                cells[i].count.text = full && stack.Count > 1 ? stack.Count.ToString() : "";
                cells[i].rim.color = Faded(style.Accent, full ? 0.55f : 0.18f);
            }

            float below = y + Mathf.CeilToInt(count / (float)perRow) * (Cell + Gap) + 6f;
            note.rectTransform.anchoredPosition = new Vector2(x + 2f, -(count > 0 ? below : y));
            note.text = count == 0 ? "No saddle, no bags: a hide saddle gives it eight places to carry your load."
                : used == 0 ? "Empty. Click to open the bags and load them." : $"{used} of {count} places used. Click to open the bags.";
        }
    }

    // What it wears in one place: an icon, what it is, a line, and for the bags how full they are.
    private sealed class GearSlot
    {
        private readonly Image icon;
        private readonly TMP_Text value;
        private readonly TMP_Text line;
        private readonly RectTransform fill;
        private readonly GameObject track;

        public GearSlot(Transform parent, float x, float y, float width, string name, string iconName, HorsePanelArt art, UIStyle style)
        {
            RectTransform card = CodeUI.At(name + " Slot", parent, x, y, width, 92f);
            Sliced(card.gameObject.AddComponent<Image>(), art?.Card, 1.3f).color = style.Panel;
            Image rim = Sliced(CodeUI.Stretch("Rim", card).gameObject.AddComponent<Image>(), art?.CardRim, 1.3f);
            rim.color = Faded(style.Accent, 0.35f);
            rim.raycastTarget = false;
            icon = CodeUI.At("Icon", card, 16f, 22f, 46f, 46f).gameObject.AddComponent<Image>();
            icon.sprite = art != null ? art.Icon(iconName) : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            TMP_Text label = CodeUI.Label("Name", card, 76f, 10f, width - 90f, 22f, 15f, style.Accent);
            label.text = name.ToUpperInvariant();
            label.characterSpacing = 6f;
            value = CodeUI.Label("Value", card, 76f, 32f, width - 90f, 28f, 20f, style.Text);
            line = CodeUI.Label("Line", card, 76f, 58f, width - 90f, 20f, 15f, style.Muted);
            RectTransform bar = CodeUI.At("Track", card, 76f, 81f, width - 96f, 5f);
            track = bar.gameObject;
            Sliced(track.AddComponent<Image>(), art?.Card, 8f).color = style.Track;
            fill = CodeUI.Stretch("Fill", bar);
            Sliced(fill.gameObject.AddComponent<Image>(), art?.Card, 8f).color = style.Accent;
        }

        public void Set(string shown, string detail, bool worn, float full = -1f)
        {
            UIStyle style = UIStyle.Current;
            value.text = shown;
            value.color = worn ? style.Text : style.Muted;
            line.text = detail;
            icon.color = worn ? style.Accent : Faded(style.Muted, 0.6f);
            track.SetActive(full >= 0f);
            if (full >= 0f)
            {
                fill.anchorMax = new Vector2(Mathf.Clamp01(full), 1f);
                fill.GetComponent<Image>().color = full > 1f ? style.Danger : full > 0.6f ? style.Warning : style.Accent;
            }
        }
    }

    // One trait it can learn: its name, what it does (or how to teach it), and how far along it is.
    private sealed class TraitRow
    {
        private readonly Image icon;
        private readonly TMP_Text name;
        private readonly TMP_Text line;
        private readonly TMP_Text state;
        private readonly RectTransform fill;
        private readonly Image fillImage;

        public TraitRow(Transform parent, float x, float y, float width, HorsePanelArt art, UIStyle style)
        {
            RectTransform card = CodeUI.At("Trait", parent, x, y, width, 74f);
            Sliced(card.gameObject.AddComponent<Image>(), art?.Card, 1.3f).color = style.Panel;
            Image rim = Sliced(CodeUI.Stretch("Rim", card).gameObject.AddComponent<Image>(), art?.CardRim, 1.3f);
            rim.color = Faded(style.Accent, 0.25f);
            rim.raycastTarget = false;
            icon = CodeUI.At("Icon", card, 18f, 20f, 34f, 34f).gameObject.AddComponent<Image>();
            icon.sprite = art != null ? art.Icon("Star") : null;
            icon.enabled = icon.sprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            name = CodeUI.Label("Name", card, 68f, 10f, 260f, 28f, 21f, style.Text);
            style.Title(name);
            line = CodeUI.Label("Line", card, 68f, 40f, width - 420f, 24f, 16f, style.Muted);
            state = CodeUI.Label("State", card, width - 320f, 10f, 300f, 26f, 16f, style.Muted, TextAlignmentOptions.Right);
            RectTransform track = CodeUI.At("Track", card, width - 320f, 44f, 300f, 9f);
            Sliced(track.gameObject.AddComponent<Image>(), art?.Card, 7f).color = style.Track;
            fill = CodeUI.Stretch("Fill", track);
            fillImage = Sliced(fill.gameObject.AddComponent<Image>(), art?.Card, 7f);
            fillImage.raycastTarget = false;
        }

        public void Set(string trait, string what, bool learnt, float progress)
        {
            UIStyle style = UIStyle.Current;
            name.text = trait;
            name.color = learnt ? style.AccentBright : style.Text;
            line.text = what;
            state.text = learnt ? "Learned" : $"{progress * 100f:0}% of the way";
            state.color = learnt ? style.Good : style.Muted;
            icon.color = learnt ? style.Accent : Faded(style.Muted, 0.6f);
            fill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            fillImage.color = learnt ? style.Good : style.Info;
        }
    }

    private sealed class HistoryRow
    {
        private readonly TMP_Text label;
        private readonly TMP_Text value;

        public HistoryRow(Transform parent, float x, float y, float width, UIStyle style)
        {
            label = CodeUI.Label("Label", parent, x + 4f, y, 300f, 30f, 19f, style.Accent);
            value = CodeUI.Label("Value", parent, x + 300f, y, width - 310f, 30f, 20f, style.Text);
            CodeUI.Hairline(parent, x, y + 42f, width, 1f, style);
        }

        public void Set(string name, string shown)
        {
            label.text = name;
            value.text = shown;
        }
    }

    // ----- The picture: a camera beside it, three-quarters on, looking at its head and shoulder; only while open -----

    private void LateUpdate()
    {
        bool open = window != null && window.IsOpen && horse != null;
        if (!open)
        {
            return;
        }

        if (portrait == null)
        {
            var holder = new GameObject("Horse Portrait Camera");
            holder.transform.SetParent(transform, false);
            portrait = holder.AddComponent<Camera>();
            portrait.targetTexture = texture;
            portrait.fieldOfView = 30f;
            portrait.nearClipPlane = 0.2f;
            portrait.farClipPlane = 150f;
        }

        portrait.enabled = true;
        Transform body = horse.transform;
        Vector3 look = body.position + Vector3.up * 1.45f + body.forward * 0.35f;
        portrait.transform.position = look + body.right * 3.4f + body.forward * 2.6f + Vector3.up * 0.25f;
        portrait.transform.LookAt(look);
    }

    private void OnDestroy()
    {
        PlayerHold.Set(this, false);
        PadTabs.Unregister(this);
        if (texture != null)
        {
            texture.Release();
            Destroy(texture);
        }
    }
}
