using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The Purification Altar (GDD §13.5): four pages over the same pair of essences. Purify queues batches; Empower, Enchant and
/// Skills share one flow, a subject on the left, what can be done to it in the middle and the chosen offer with its cost on
/// the right. All the rules are in AltarWork; this only draws them, read again twice a second while open. Built in code the
/// first time an altar is used, and closes when the player walks away. Hearthline 3: framed tabs, the kit's close button,
/// rows as rounded cards with a gold rim on the chosen one, the chosen offer in a card of its own, the queue as an animated
/// bar, and what each essence buys in a card with its sign.
/// </summary>
public sealed class AltarWindow : MonoBehaviour
{
    private enum Page
    {
        Purify = 0,
        Empower = 1,
        Enchant = 2,
        Skills = 3,
    }

    private const float Width = 1240f;
    private const float Height = 730f;
    private const float Margin = 44f;
    private const float BodyTop = 210f;
    private const float BodyBottom = 690f;
    private const float RowHeight = 62f;
    private const float ReadSeconds = 0.5f;
    private const float ScreenMargin = 24f;
    // The chosen offer's column, inside its card.
    private const float OfferX = 864f;
    private const float OfferWidth = Width - Margin - 16f - OfferX;

    private static readonly string[] PageNames = { "Purify", "Empower", "Enchant", "Abilities" };

    private static readonly string[] PageLines =
    {
        "Cursed Essence is strong and dangerous. Purified, it is safe to keep and to build on.",
        "Strengthen the gear you trust. It takes essence and the materials the item was made from.",
        "One enchantment to an item. Purified is safe. Cursed is stronger, and takes its price.",
        "Deepen a skill you know: more power, a shorter cooldown.",
    };

    /// <summary>A scrolling list of rows, kept and reused as the page is read again.</summary>
    private sealed class RowList
    {
        private readonly List<Row> rows = new List<Row>();
        private readonly RectTransform content;
        private readonly float width;
        private readonly Action<int> clicked;

        public RowList(string name, Transform parent, float x, float y, float width, float height, Action<int> clicked)
        {
            this.width = width;
            this.clicked = clicked;
            RectTransform view = At(name, parent, x, y, width, height);
            view.gameObject.AddComponent<Image>().color = Color.clear;
            view.gameObject.AddComponent<RectMask2D>();
            content = At("Content", view, 0f, 0f, width, 0f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = view;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
        }

        public void Show(int count, Action<Row, int> bind)
        {
            while (rows.Count < count)
            {
                rows.Add(new Row(rows.Count, content, width, clicked));
            }

            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < count;
                rows[i].Root.SetActive(used);
                if (used)
                {
                    bind(rows[i], i);
                }
            }

            content.sizeDelta = new Vector2(width, count * RowHeight);
        }
    }

    private sealed class Row
    {
        public readonly GameObject Root;
        private readonly Button button;
        private readonly Image icon;
        private readonly Image fill;
        private readonly Image rim;
        private readonly TMP_Text title;
        private readonly TMP_Text detail;

        public Row(int index, Transform parent, float width, Action<int> clicked)
        {
            UIStyle style = UIStyle.Current;
            RectTransform rect = At($"Row {index}", parent, 0f, index * RowHeight, width, RowHeight - 4f);
            Root = rect.gameObject;
            fill = HearthKit.Sliced(Root.AddComponent<Image>(), style.Card, 1.5f);
            button = Root.AddComponent<Button>();
            button.targetGraphic = fill;
            HearthKit.Tint(button);
            button.onClick.AddListener(() => clicked(index));
            UIHighlight.On(Root);
            rim = HearthKit.Rim(rect, 0.2f, 1.5f);
            icon = At("Icon", rect, 12f, 8f, 42f, 42f).gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            title = Label("Title", rect, 66f, 6f, width - 76f, 28f, 20f, style.Text);
            detail = Label("Detail", rect, 66f, 31f, width - 76f, 24f, 16f, style.Muted);
        }

        public void Bind(string shownTitle, string shownDetail, Sprite picture, bool selected, bool dim) =>
            Bind(shownTitle, shownDetail, picture, selected, dim, null, false);

        /// <summary>A row with its picture, its frame in <paramref name="kind"/>'s colour (a great, cursed or relic enchantment), and the
        /// picture as a dark shape when <paramref name="unknown"/> (not learned yet: a goal).</summary>
        public void Bind(string shownTitle, string shownDetail, Sprite picture, bool selected, bool dim, Color? kind, bool unknown)
        {
            UIStyle style = UIStyle.Current;
            float left = picture != null ? 66f : 16f;
            title.rectTransform.anchoredPosition = new Vector2(left, -6f);
            detail.rectTransform.anchoredPosition = new Vector2(left, -31f);
            title.text = shownTitle;
            title.color = dim ? style.Muted : style.Text;
            detail.text = shownDetail;
            icon.gameObject.SetActive(picture != null);
            icon.sprite = picture;
            // Darker than a greyed one (review): an unknown inscription is a shape to find, not a choice to make.
            icon.color = unknown ? new Color(0.2f, 0.18f, 0.16f, 0.9f) : Color.white;
            // The chosen row lit and rimmed, the rest dark leather with a faint rim, both in the row's kind's colour.
            Color edge = kind ?? style.Accent;
            fill.color = selected ? Color.Lerp(style.Inset, style.AccentDeep, 0.4f) : style.Inset;
            rim.color = selected ? edge : HearthKit.Faded(edge, kind.HasValue ? 0.5f : 0.2f);
        }
    }

    private UIWindow window;
    private RectTransform panel;
    private TMP_Text pageTitle;
    private TMP_Text pageLine;
    private TMP_Text cursedValue;
    private TMP_Text purifiedValue;
    private KitTabs tabs;

    private GameObject purifyPage;
    private TMP_Text rateLine;
    private TMP_Text queueLine;
    private RectTransform queueFill;
    private Image queueFillImage;
    private TMP_Text warningLine;
    private TMP_Text speedLine;
    private Button purifyOne;
    private Button purifyAll;
    private TMP_Text purifyAllLabel;

    private GameObject workPage;
    private TMP_Text subjectHeading;
    private TMP_Text offerHeading;
    private RowList subjectRows;
    private RowList offerRows;
    private TMP_Text emptyLine;
    private TMP_Text offerEffect;
    private TMP_Text offerName;
    private TMP_Text offerLore;
    private RectTransform offerIconFrame;
    private Image offerIcon;
    private TMP_Text costHeading;
    private TMP_Text offerCost;
    private TMP_Text offerProblem;
    private Button applyButton;
    private TMP_Text applyLabel;

    private GameObject viewer;
    private Transform anchor;
    private float range;
    private float nextRead;
    private Page page;
    private int subjectIndex;
    private int offerIndex;
    private List<AltarSubject> subjects = new List<AltarSubject>();
    private AltarOffer shownOffer;

    public static AltarWindow Current { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

    /// <summary>Builds the window (closed) for this scene, unless it has one.</summary>
    public static AltarWindow Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Altar Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<AltarWindow>();
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    /// <summary>Opens at an altar; it closes again when the user is further than maxDistance metres from it.</summary>
    public void Open(GameObject user, Transform near, float maxDistance)
    {
        viewer = user;
        anchor = near;
        range = maxDistance;
        window.Open();
    }

    public void Close() => window.Close();

    private void OnEnable()
    {
        Current = this;
        PadTabs.Register(this, () => window != null && window.IsOpen, direction => Show((Page)PadTabs.Next((int)page, PageNames.Length, direction)),
            () => tabs != null ? tabs.Rects : new List<RectTransform>());
    }

    private void OnDisable()
    {
        PadTabs.Unregister(this);
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Update()
    {
        if (!window.IsOpen)
        {
            return;
        }

        if (viewer == null || anchor == null || HorizontalDistance(viewer.transform.position, anchor.position) > range)
        {
            window.Close();
            return;
        }

        if (Time.unscaledTime >= nextRead)
        {
            Read();
        }

        if (page == Page.Purify)
        {
            IHavenView settlement = GameSession.Here;
            SettlementSettings settings = SettlementSettings.Current;
            float share = settlement.PurificationQueued > 0 ? settlement.PurificationProgress / settings.SecondsPerBatch : 0f;
            KitBar.Set(queueFill, queueFillImage, Mathf.Clamp01(share), UIStyle.Current.Accent);
            if (purifyStage != null)
            {
                // Red going to green as the batch comes clean; the cursed essence's picture on it.
                UIStyle style = UIStyle.Current;
                purifyStage.Working = settlement.PurificationQueued > 0 ? Mathf.Clamp01(share) : 0f;
                ResourceDefinition shownEssence = share > 0.5f ? settings.PurifiedEssence : settings.CursedEssence;
                purifyStage.Show(shownEssence != null ? shownEssence.Icon : null, settlement.PurificationQueued > 0 ? Color.Lerp(style.Danger, style.Good, share) : style.Danger);
            }
        }
    }

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            viewer = null;
            anchor = null;
            return;
        }

        Show(page);
    }

    private void Show(Page shown)
    {
        page = shown;
        subjectIndex = 0;
        offerIndex = 0;
        Read();
    }

    private void Read()
    {
        nextRead = Time.unscaledTime + ReadSeconds;
        Rect screen = ((RectTransform)transform).rect;
        panel.localScale = Vector3.one * Mathf.Min(1f, (screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / Height);
        DrawHeader();
        purifyPage.SetActive(page == Page.Purify);
        workPage.SetActive(page != Page.Purify);
        if (page == Page.Purify)
        {
            DrawPurify();
        }
        else
        {
            DrawWork();
        }
    }

    private void DrawHeader()
    {
        UIStyle style = UIStyle.Current;
        SettlementSettings settings = SettlementSettings.Current;
        MaterialPool pool = MaterialPool.For(viewer);
        cursedValue.text = (settings.CursedEssence != null ? pool.Have(settings.CursedEssence) : 0).ToString();
        purifiedValue.text = (settings.PurifiedEssence != null ? pool.Have(settings.PurifiedEssence) : 0).ToString();
        pageTitle.text = PageNames[(int)page];
        pageLine.text = PageLines[(int)page];
        tabs.Show((int)page);
    }

    private void DrawPurify()
    {
        UIStyle style = UIStyle.Current;
        SettlementSettings settings = SettlementSettings.Current;
        IHavenView settlement = GameSession.Here;
        ResourceDefinition cursed = settings.CursedEssence;
        ResourceDefinition purified = settings.PurifiedEssence;
        if (cursed == null || purified == null)
        {
            rateLine.text = "The altar has no essences set (Systems Editor > Essence).";
            return;
        }

        float seconds = settings.SecondsPerBatch / (1f + settlement.PurificationBonus);
        rateLine.text = HearthKit.ToneNumbers($"{settings.CursedPerBatch} Cursed become {settings.PurifiedPerBatch} Purified every {seconds:0.#} seconds.");
        speedLine.text = settlement.PurificationYield > 0f ? $"Upgrades give {settlement.PurificationYield * 100f:0}% more Purified, {settlement.PurificationBonus * 100f:0}% faster." : "Upgrading the altar gives more Purified for the same Cursed.";
        speedLine.color = settlement.PurificationYield > 0f ? style.Good : style.Muted;
        int queued = settlement.PurificationQueued;
        queueLine.text = queued > 0 ? $"{queued} batch{(queued == 1 ? "" : "es")} waiting" : "Nothing in the queue";

        // The altar's own answer (AltarWork.Queueable): who is at work, the room, the essence to pay.
        int all = AltarWork.Queueable(viewer, out string cold);
        warningLine.gameObject.SetActive(cold != null);
        warningLine.text = cold ?? "";
        purifyOne.interactable = all >= 1;
        purifyAll.interactable = all > 1;
        purifyAllLabel.text = all > 1 ? $"Purify all ({all})" : "Purify all";
    }

    private void DrawWork()
    {
        subjects = page == Page.Empower ? AltarWork.Empowering(viewer) : page == Page.Enchant ? AltarWork.Enchanting(viewer) : AltarWork.Deepening(viewer);
        subjectHeading.text = page == Page.Skills ? "SKILLS" : "GEAR";
        offerHeading.text = page == Page.Empower ? "LEVELS" : page == Page.Enchant ? "ENCHANTMENTS" : "RANKS AND CHOICES";
        emptyLine.gameObject.SetActive(subjects.Count == 0);
        emptyLine.text = page == Page.Skills ? "No skills to deepen yet." : "Nothing to work on. Carry or wear the gear you want changed.";
        subjectIndex = Mathf.Clamp(subjectIndex, 0, Mathf.Max(0, subjects.Count - 1));
        subjectRows.Show(subjects.Count, (row, i) => row.Bind(subjects[i].Title, subjects[i].Detail, subjects[i].Icon, i == subjectIndex, false));
        List<AltarOffer> offers = subjects.Count > 0 ? subjects[subjectIndex].Offers : new List<AltarOffer>();
        offerIndex = Mathf.Clamp(offerIndex, 0, Mathf.Max(0, offers.Count - 1));
        offerRows.Show(offers.Count, (row, i) => row.Bind(offers[i].Done ? $"{offers[i].Title}  <size=75%>Done</size>" : offers[i].Title, offers[i].Detail,
            offers[i].Picture, i == offerIndex, offers[i].Done, KindColour(offers[i].Kind), offers[i].Kind == AltarOfferKind.Unknown));
        DrawOffer(offers.Count > 0 ? offers[offerIndex] : null, subjects.Count > 0 ? subjects[subjectIndex].Icon : null);
    }

    // An offer's kind as a colour: a great one bright gold, a cursed one red, a relic's old bronze, an unknown one grey; null for plain.
    private static Color? KindColour(AltarOfferKind kind)
    {
        UIStyle style = UIStyle.Current;
        switch (kind)
        {
            case AltarOfferKind.Great: return style.AccentBright;
            case AltarOfferKind.Cursed: return style.Danger;
            case AltarOfferKind.Relic: return new Color(0.55f, 0.72f, 0.52f);
            case AltarOfferKind.Unknown: return style.Inactive;
            default: return null;
        }
    }

    // The chosen offer: on the stage (its own picture, else what it is worked on) in its kind's colour, its words, cost and button below.
    private void DrawOffer(AltarOffer offer, Sprite picture)
    {
        UIStyle style = UIStyle.Current;
        if (offer != shownOffer && offer != null && shownOffer != null)
        {
            stage.Selected();
        }

        shownOffer = offer;
        bool any = offer != null;
        Sprite shown = any && offer.Picture != null && offer.Kind != AltarOfferKind.Unknown ? offer.Picture : picture;
        Color colour = any && page == Page.Enchant ? KindColour(offer.Kind) ?? style.Accent : page == Page.Skills ? style.AccentBright : style.Accent;
        stage.Show(any ? shown : null, colour);
        // Empowering and deepening are steps: how many are held, of how many, as pips under the ring.
        List<AltarOffer> steps = subjects.Count > subjectIndex ? subjects[subjectIndex].Offers : null;
        int held = 0, total = 0;
        if (steps != null && page != Page.Enchant)
        {
            foreach (AltarOffer step in steps)
            {
                // A skill's choices are not ranks.
                if (page == Page.Skills && step.Kind == AltarOfferKind.Great)
                {
                    continue;
                }

                total++;
                held += step.Done ? 1 : 0;
            }
        }

        stage.SetPips(held, total);
        offerIconFrame.gameObject.SetActive(false);
        offerEffect.gameObject.SetActive(any);
        offerName.gameObject.SetActive(any);
        offerLore.gameObject.SetActive(any);
        costHeading.gameObject.SetActive(any);
        offerCost.gameObject.SetActive(any);
        offerProblem.gameObject.SetActive(any);
        applyButton.gameObject.SetActive(any);
        if (!any)
        {
            return;
        }

        bool split = !string.IsNullOrEmpty(offer.Name);
        offerName.text = split ? offer.Name : "";
        offerEffect.text = split ? offer.Summary : offer.Effect;
        offerLore.text = split ? offer.Lore : "";
        offerCost.text = CostLines(offer.Cost);
        offerProblem.text = offer.Problem ?? "";
        applyLabel.richText = true;
        applyLabel.text = offer.Problem == null ? $"{offer.Verb}  <size=70%>hold</size>" : offer.Verb;
        applyButton.interactable = offer.Problem == null;
    }

    // Each cost on a line against what the player has, so nothing has to be looked up: a value goes with the colour.
    private string CostLines(RepairCost cost)
    {
        UIStyle style = UIStyle.Current;
        MaterialPool pool = MaterialPool.For(viewer);
        var lines = new StringBuilder();
        foreach (ResourceAmount part in cost.Resources)
        {
            AppendCost(lines, part.Resource.DisplayName, pool.Have(part.Resource), part.Amount, style);
        }

        foreach (ItemAmount part in cost.Items)
        {
            AppendCost(lines, part.Item.DisplayName, pool.Have(part.Item), part.Amount, style);
        }

        return lines.Length > 0 ? lines.ToString() : "Nothing";
    }

    private static void AppendCost(StringBuilder lines, string name, int have, int need, UIStyle style)
    {
        string colour = ColorUtility.ToHtmlStringRGB(have >= need ? style.Good : style.Danger);
        lines.Append(lines.Length > 0 ? "\n" : "").Append($"<color=#{colour}>{have} / {need}</color>  {name}");
    }

    private void Choose(int index)
    {
        subjectIndex = index;
        offerIndex = FirstOpenOffer();
        Read();
    }

    // Straight to the next step, not to a level already bought.
    private int FirstOpenOffer()
    {
        if (subjectIndex >= subjects.Count)
        {
            return 0;
        }

        List<AltarOffer> offers = subjects[subjectIndex].Offers;
        int first = offers.FindIndex(offer => !offer.Done);
        return Mathf.Max(0, first);
    }

    private void Apply()
    {
        if (shownOffer != null && shownOffer.Problem == null)
        {
            shownOffer.Apply?.Invoke();
            Read();
            // A level or a rank goes on to the next step; an enchantment stays chosen, so what was carved is what is seen.
            if (page != Page.Enchant)
            {
                offerIndex = FirstOpenOffer();
            }

            Read();
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        panel = Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        BuildHeader(style);
        BuildPurifyPage(style);
        BuildWorkPage(style);
        HearthKit.CloseButton(panel, Width - 66f, 18f, () => window.Close());

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Altar, content.gameObject);
        window.OpenChanged += OnOpenChanged;
    }

    private void BuildHeader(UIStyle style)
    {
        TMP_Text kicker = Label("Kicker", panel, Margin, 30f, 500f, 24f, 17f, style.Accent);
        kicker.text = "PURIFICATION ALTAR";
        kicker.characterSpacing = 12f;
        pageTitle = Label("Title", panel, Margin, 52f, 620f, 56f, 42f, style.Text);
        style.Title(pageTitle);
        // Under the carved title, clear of the rule beneath its letters.
        pageLine = Label("Line", panel, Margin, 116f, 860f, 30f, 20f, style.Muted);
        style.Title(pageLine);
        pageLine.color = style.Muted;

        // Left of the close button.
        // LocalSpawn art behind the header (title, essences), the band no taller than it, so nothing below moves.
        // Ending above the page line, which would otherwise sit on its gold rule; veiled so the essences read over candles.
        KitBanner.Show(panel, "Altar", 112f, 112f).Darken(0.45f);
        // A dark plate under the essences (review: the numbers sat on the banner's candles and read poorly).
        RectTransform plate = CodeUI.At("Essence Plate", panel, 846f, 26f, 300f, 90f);
        plate.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.035f, 0.03f, 0.72f);
        plate.GetComponent<Image>().raycastTarget = false;
        cursedValue = Figure("Cursed", "Skull", 880f, style.Danger, style);
        purifiedValue = Figure("Purified", "Spark", 1020f, style.Good, style);

        Hairline(panel, Margin, 198f, Width - Margin * 2f, 1f, style);
        tabs = new KitTabs(panel, Margin, 148f, 150f, 42f, PageNames, shown => Show((Page)shown));
    }

    private TMP_Text Figure(string label, string icon, float x, Color colour, UIStyle style)
    {
        Hairline(panel, x - 20f, 40f, 1f, 76f, style);
        TMP_Text value = Label(label + " Value", panel, x, 34f, 110f, 50f, 36f, colour);
        Label(label + " Label", panel, x, 86f, 120f, 24f, 17f, style.Muted).text = HearthKit.InlineIcon(icon, colour) + " " + label;
        return value;
    }

    private void BuildPurifyPage(UIStyle style)
    {
        purifyPage = Stretch("Purify Page", panel).gameObject;
        Transform page = purifyPage.transform;
        Heading("The queue", page, Margin, BodyTop, 560f, style);
        rateLine = Label("Rate", page, Margin, BodyTop + 36f, 560f, 30f, 21f, style.Text);
        speedLine = Label("Speed", page, Margin, BodyTop + 66f, 560f, 26f, 18f, style.Muted);
        queueLine = Label("Queue", page, Margin, BodyTop + 112f, 560f, 28f, 19f, style.Text);
        queueFill = KitBar.Make(page, Margin, BodyTop + 144f, 560f, 10f, out queueFillImage);
        KitBar.Set(queueFill, queueFillImage, 0f, style.Accent);
        AddButton("Purify One", page, Margin, BodyTop + 170f, 180f, UIRole.Kind.Button, () => Purify(1), out purifyOne).text = "Purify one";
        purifyAllLabel = AddButton("Purify All", page, Margin + 192f, BodyTop + 170f, 240f, UIRole.Kind.PrimaryButton, () => Purify(SettlementSettings.Current.MaxQueuedBatches), out purifyAll);
        warningLine = Paragraph("Warning", page, Margin, BodyTop + 232f, 560f, 56f, 19f, style.Warning);
        // The essence on its own stage: cursed red at rest, its ring turning toward purified green while the queue works.
        purifyStage = AltarStage.Build((RectTransform)page, Margin + 190f, BodyTop + 292f, 180f);

        Hairline(page, 624f, BodyTop, 1f, BodyBottom - BodyTop, style);
        Heading("What essence buys", page, 644f, BodyTop, 552f, style);
        EssenceCard(page, "Purified Essence", "Spark", style.Good, BodyTop + 36f, 176f,
            "Empowers gear and deepens skills. Works enchantments with no drawback, strengthens the portal's ward, " +
            "raises the greatest building tiers and brews the Purified Draught at a Cauldron. Stored at home, it is safe.");
        EssenceCard(page, "Cursed Essence", "Skull", style.Danger, BodyTop + 228f, 152f,
            "Used raw, it works stronger enchantments, each with a stated price, and brews the Cursed Draught. " +
            "It works at once, with no queue, and a raid's worth comes home only if you do.");
    }

    // One essence's card: its sign and name in its colour, what it does under them.
    private static void EssenceCard(Transform page, string name, string icon, Color colour, float y, float height, string text)
    {
        UIStyle style = UIStyle.Current;
        RectTransform card = HearthKit.Card(page, name + " Card", 644f, y, 552f, height, 0.4f);
        HearthKit.Icon(card, icon, 18f, 16f, 28f, colour);
        TMP_Text title = Label("Name", card, 56f, 14f, 470f, 32f, 21f, colour);
        style.Title(title);
        title.color = colour;
        title.fontStyle = FontStyles.Bold;
        title.text = name;
        Paragraph("Text", card, 18f, 54f, 516f, height - 64f, 18f, style.Text).text = text;
    }

    private void Purify(int batches)
    {
        if (AltarWork.Purify(viewer, batches))
        {
            // Purifying is done often: the sound carries it (Path of Exile's lesson), no ceremony.
            if (AltarFeel.Current.Purify != null)
            {
                AudioManager.Play(AltarFeel.Current.Purify, viewer != null ? viewer.transform.position : Vector3.zero);
            }

            purifyStage?.Pulse(batches > 1 ? AltarMoment.Large : AltarMoment.Small, batches > 1 ? $"{batches} batches to the queue" : "One batch to the queue");

            Read();
        }
    }

    private void BuildWorkPage(UIStyle style)
    {
        workPage = Stretch("Work Page", panel).gameObject;
        Transform page = workPage.transform;
        float listTop = BodyTop + 36f;
        float listHeight = BodyBottom - listTop;
        subjectHeading = Heading("Gear", page, Margin, BodyTop, 350f, style);
        subjectRows = new RowList("Subjects", page, Margin, listTop, 350f, listHeight, Choose);
        emptyLine = Paragraph("Empty", page, Margin, listTop + 8f, 350f, 80f, 19f, style.Muted);
        Hairline(page, 408f, BodyTop, 1f, BodyBottom - BodyTop, style);
        offerHeading = Heading("Levels", page, 424f, BodyTop, 400f, style);
        offerRows = new RowList("Offers", page, 424f, listTop, 400f, listHeight, index =>
        {
            offerIndex = index;
            Read();
        });
        Hairline(page, 838f, BodyTop, 1f, BodyBottom - BodyTop, style);
        HearthKit.Card(page, "Offer Card", 848f, BodyTop - 6f, Width - Margin - 848f, BodyBottom - 60f - (BodyTop - 6f), 0.4f);
        offerIconFrame = HearthKit.Card(page, "Offer Icon", OfferX, BodyTop + 8f, 54f, 54f, 0.7f);
        RectTransform inner = Stretch("Icon", offerIconFrame);
        inner.offsetMin = new Vector2(6f, 6f);
        inner.offsetMax = new Vector2(-6f, -6f);
        offerIcon = inner.gameObject.AddComponent<Image>();
        offerIcon.preserveAspect = true;
        offerIcon.raycastTarget = false;
        // The stage (Docs/AltarUXResearch_2026-11-28.md): what is worked on, large in its ring of runes, over its words and cost.
        const float stageSize = 170f;
        float cardX = 848f, cardWidth = Width - Margin - 848f;
        stage = AltarStage.Build((RectTransform)page, cardX + (cardWidth - stageSize) * 0.5f, BodyTop + 4f, stageSize);
        // Its name in the carved title face, what it does (before → after where it raises something), then its lore, muted
        // (Docs/AltarUXResearch_2026-11-28.md, review: one block of mixed text read as a wall).
        offerName = Label("Name", page, OfferX, BodyTop + 178f, OfferWidth, 30f, 22f, style.AccentBright);
        style.Title(offerName);
        offerName.color = style.AccentBright;
        offerName.alignment = TextAlignmentOptions.Center;
        offerName.enableAutoSizing = true;
        offerName.fontSizeMin = 15f;
        offerName.fontSizeMax = 22f;
        offerEffect = Paragraph("Effect", page, OfferX, BodyTop + 210f, OfferWidth, 46f, 17f, style.Text);
        offerEffect.alignment = TextAlignmentOptions.Top;
        offerEffect.enableAutoSizing = true;
        offerEffect.fontSizeMin = 12f;
        offerEffect.fontSizeMax = 17f;
        offerLore = Paragraph("Lore", page, OfferX, BodyTop + 258f, OfferWidth, 48f, 15f, style.Muted);
        offerLore.alignment = TextAlignmentOptions.Top;
        offerLore.enableAutoSizing = true;
        offerLore.fontSizeMin = 11f;
        offerLore.fontSizeMax = 15f;
        costHeading = Heading("Cost", page, 864f, BodyTop + 310f, 316f, style);
        offerCost = Paragraph("Costs", page, 864f, BodyTop + 338f, 316f, 62f, 16f, style.Text);
        offerCost.enableAutoSizing = true;
        offerCost.fontSizeMin = 12f;
        offerCost.fontSizeMax = 16f;
        offerProblem = Paragraph("Problem", page, 864f, BodyTop + 398f, 316f, 22f, 15f, style.Warning);
        offerProblem.enableAutoSizing = true;
        offerProblem.fontSizeMin = 11f;
        offerProblem.fontSizeMax = 15f;
        applyLabel = AddButton("Apply", page, 848f, BodyBottom - 48f, Width - Margin - 848f, UIRole.Kind.PrimaryButton, Apply, out applyButton);
        // Held, not clicked: the stage charges while it is held and answers when it is done.
        stage.Attach(applyButton, () => shownOffer != null && shownOffer.Problem == null, Apply,
            () => (shownOffer != null ? shownOffer.Moment : AltarMoment.Small, ResultLine()));
    }

    // What the stage says when a working is done.
    private string ResultLine()
    {
        if (shownOffer == null || subjectIndex >= subjects.Count)
        {
            return "";
        }

        // Plain names: the row's title carries its kind's tags ("Great", "Cursed"), which read as part of the name here.
        string what = StripTags(string.IsNullOrEmpty(shownOffer.Name) ? shownOffer.Title : shownOffer.Name);
        string subject = StripTags(subjects[subjectIndex].Title);
        return page == Page.Empower ? $"{what}" : page == Page.Enchant ? $"{what} carved into {subject}"
            : shownOffer.Kind == AltarOfferKind.Great ? $"{subject}: {what}" : $"{what}";
    }

    private static string StripTags(string text) => System.Text.RegularExpressions.Regex.Replace(text ?? "", "<[^>]+>", "").Trim();

    private AltarStage stage;
    private AltarStage purifyStage;

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
