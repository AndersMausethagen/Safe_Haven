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
/// first time an altar is used, and closes when the player walks away.
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
    private const float Height = 776f;
    private const float Margin = 44f;
    private const float BodyTop = 210f;
    private const float BodyBottom = 690f;
    private const float RowHeight = 62f;
    private const float ReadSeconds = 0.5f;
    private const float FadeSeconds = 0.16f;
    private const float ScreenMargin = 24f;

    private static readonly string[] PageNames = { "Purify", "Empower", "Enchant", "Skills" };

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
        private readonly Image mark;
        private readonly TMP_Text title;
        private readonly TMP_Text detail;

        public Row(int index, Transform parent, float width, Action<int> clicked)
        {
            UIStyle style = UIStyle.Current;
            RectTransform rect = At($"Row {index}", parent, 0f, index * RowHeight, width, RowHeight - 4f);
            Root = rect.gameObject;
            var image = Root.AddComponent<Image>();
            button = Root.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => clicked(index));
            mark = At("Mark", rect, 0f, 0f, 3f, RowHeight - 4f).gameObject.AddComponent<Image>();
            mark.color = style.Accent;
            icon = At("Icon", rect, 12f, 8f, 42f, 42f).gameObject.AddComponent<Image>();
            icon.raycastTarget = false;
            title = Label("Title", rect, 66f, 6f, width - 76f, 28f, 20f, style.Text);
            detail = Label("Detail", rect, 66f, 31f, width - 76f, 24f, 16f, style.Muted);
        }

        public void Bind(string shownTitle, string shownDetail, Sprite picture, bool selected, bool dim)
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
            mark.gameObject.SetActive(selected);
            ColorBlock colors = button.colors;
            colors.normalColor = colors.selectedColor = selected ? style.Filled : style.Panel;
            colors.highlightedColor = style.Filled;
            colors.pressedColor = style.ControlLit;
            colors.colorMultiplier = 1f;
            button.colors = colors;
        }
    }

    private UIWindow window;
    private CanvasGroup fade;
    private RectTransform panel;
    private TMP_Text pageTitle;
    private TMP_Text pageLine;
    private TMP_Text cursedValue;
    private TMP_Text purifiedValue;
    private readonly TMP_Text[] tabs = new TMP_Text[PageNames.Length];
    private readonly GameObject[] tabMarks = new GameObject[PageNames.Length];

    private GameObject purifyPage;
    private TMP_Text rateLine;
    private TMP_Text queueLine;
    private RectTransform queueFill;
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
    private TMP_Text costHeading;
    private TMP_Text offerCost;
    private TMP_Text offerProblem;
    private Button applyButton;
    private TMP_Text applyLabel;

    private GameObject viewer;
    private Transform anchor;
    private float range;
    private float openedAt;
    private float nextRead;
    private Page page;
    private int subjectIndex;
    private int offerIndex;
    private List<AltarSubject> subjects = new List<AltarSubject>();
    private AltarOffer shownOffer;

    public static AltarWindow Current { get; private set; }

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

    private void OnEnable() => Current = this;

    private void OnDisable()
    {
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

        float shown = Mathf.Clamp01((Time.unscaledTime - openedAt) / FadeSeconds);
        fade.alpha = shown;
        if (Time.unscaledTime >= nextRead)
        {
            Read();
        }

        if (page == Page.Purify)
        {
            SettlementState settlement = GameSession.Settlement;
            SettlementSettings settings = SettlementSettings.Current;
            float share = settlement.PurificationQueued > 0 ? settlement.PurificationProgress / settings.SecondsPerBatch : 0f;
            queueFill.anchorMax = new Vector2(Mathf.Clamp01(share), 1f);
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

        openedAt = Time.unscaledTime;
        fade.alpha = 0f;
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
        for (int i = 0; i < tabs.Length; i++)
        {
            bool here = i == (int)page;
            tabs[i].color = here ? style.Accent : style.Muted;
            tabMarks[i].SetActive(here);
        }
    }

    private void DrawPurify()
    {
        UIStyle style = UIStyle.Current;
        SettlementSettings settings = SettlementSettings.Current;
        SettlementState settlement = GameSession.Settlement;
        ResourceDefinition cursed = settings.CursedEssence;
        ResourceDefinition purified = settings.PurifiedEssence;
        if (cursed == null || purified == null)
        {
            rateLine.text = "The altar has no essences set (Systems Editor > Essence).";
            return;
        }

        float seconds = settings.SecondsPerBatch / (1f + settlement.PurificationBonus);
        rateLine.text = $"{settings.CursedPerBatch} Cursed become {settings.PurifiedPerBatch} Purified every {seconds:0.#} seconds.";
        speedLine.text = settlement.PurificationBonus > 0f ? $"Upgrades make this altar work {settlement.PurificationBonus * 100f:0}% faster." : "Upgrading the altar makes it work faster.";
        speedLine.color = settlement.PurificationBonus > 0f ? style.Good : style.Muted;
        int queued = settlement.PurificationQueued;
        queueLine.text = queued > 0 ? $"{queued} batch{(queued == 1 ? "" : "es")} waiting" : "Nothing in the queue";

        bool staffed = settlement.HasWorker(settings.PurifierJob);
        string worker = settings.PurifierJob != null ? settings.PurifierJob.DisplayName : "someone";
        warningLine.gameObject.SetActive(!staffed);
        warningLine.text = $"The altar is cold. It needs a resident working as the {worker}.";

        int have = MaterialPool.For(viewer).Have(cursed);
        int room = settings.MaxQueuedBatches - queued;
        int all = Mathf.Min(have / settings.CursedPerBatch, room);
        purifyOne.interactable = staffed && all >= 1;
        purifyAll.interactable = staffed && all > 1;
        purifyAllLabel.text = all > 1 ? $"Purify all ({all})" : "Purify all";
    }

    private void DrawWork()
    {
        subjects = page == Page.Empower ? AltarWork.Empowering(viewer) : page == Page.Enchant ? AltarWork.Enchanting(viewer) : AltarWork.Deepening(viewer);
        subjectHeading.text = page == Page.Skills ? "SKILLS" : "GEAR";
        offerHeading.text = page == Page.Empower ? "LEVELS" : page == Page.Enchant ? "ENCHANTMENTS" : "RANKS";
        emptyLine.gameObject.SetActive(subjects.Count == 0);
        emptyLine.text = page == Page.Skills ? "No skills to deepen yet." : "Nothing to work on. Carry or wear the gear you want changed.";
        subjectIndex = Mathf.Clamp(subjectIndex, 0, Mathf.Max(0, subjects.Count - 1));
        subjectRows.Show(subjects.Count, (row, i) => row.Bind(subjects[i].Title, subjects[i].Detail, subjects[i].Icon, i == subjectIndex, false));
        List<AltarOffer> offers = subjects.Count > 0 ? subjects[subjectIndex].Offers : new List<AltarOffer>();
        offerIndex = Mathf.Clamp(offerIndex, 0, Mathf.Max(0, offers.Count - 1));
        offerRows.Show(offers.Count, (row, i) => row.Bind(offers[i].Done ? "✓ " + offers[i].Title : offers[i].Title, offers[i].Detail, null, i == offerIndex, offers[i].Done));
        DrawOffer(offers.Count > 0 ? offers[offerIndex] : null);
    }

    private void DrawOffer(AltarOffer offer)
    {
        shownOffer = offer;
        bool any = offer != null;
        offerEffect.gameObject.SetActive(any);
        costHeading.gameObject.SetActive(any);
        offerCost.gameObject.SetActive(any);
        offerProblem.gameObject.SetActive(any);
        applyButton.gameObject.SetActive(any);
        if (!any)
        {
            return;
        }

        offerEffect.text = offer.Effect;
        offerCost.text = CostLines(offer.Cost);
        offerProblem.text = offer.Problem ?? "";
        applyLabel.text = offer.Verb;
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
            offerIndex = FirstOpenOffer();
            Read();
        }
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = Stretch("Content", root);
        content.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
        fade = content.gameObject.AddComponent<CanvasGroup>();
        panel = Box("Panel", content, new Vector2(0.5f, 0.5f), new Vector2(Width, Height), Vector2.zero);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        BuildHeader(style);
        BuildPurifyPage(style);
        BuildWorkPage(style);
        AddButton("Close", panel, Width - Margin - 170f, 708f, 170f, UIRole.Kind.Button, () => window.Close(), out _).text = "Close";

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
        pageLine = Label("Line", panel, Margin, 108f, 860f, 30f, 20f, style.Muted);
        style.Title(pageLine);
        pageLine.color = style.Muted;

        cursedValue = Figure("Cursed", 936f, style.Danger, style);
        purifiedValue = Figure("Purified", 1066f, style.Good, style);

        Hairline(panel, Margin, 194f, Width - Margin * 2f, 1f, style);
        for (int i = 0; i < PageNames.Length; i++)
        {
            int shown = i;
            float x = Margin + i * 136f;
            RectTransform tab = At(PageNames[i], panel, x, 148f, 128f, 46f);
            var image = tab.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            tab.gameObject.AddComponent<Button>().onClick.AddListener(() => Show((Page)shown));
            tabs[i] = Label("Label", tab, 0f, 0f, 128f, 44f, 21f, style.Muted, TextAlignmentOptions.Center);
            tabs[i].text = PageNames[i];
            RectTransform mark = At("Mark", tab, 0f, 44f, 128f, 2f);
            mark.gameObject.AddComponent<Image>().color = style.Accent;
            tabMarks[i] = mark.gameObject;
        }
    }

    private TMP_Text Figure(string label, float x, Color colour, UIStyle style)
    {
        Hairline(panel, x - 20f, 40f, 1f, 76f, style);
        TMP_Text value = Label(label + " Value", panel, x, 34f, 110f, 50f, 36f, colour);
        Label(label + " Label", panel, x, 86f, 110f, 24f, 17f, style.Muted).text = label;
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
        RectTransform bar = At("Progress", page, Margin, BodyTop + 144f, 560f, 3f);
        bar.gameObject.AddComponent<Image>().color = style.ControlLit;
        queueFill = Stretch("Fill", bar);
        queueFill.anchorMax = new Vector2(0f, 1f);
        queueFill.gameObject.AddComponent<Image>().color = style.Accent;
        AddButton("Purify One", page, Margin, BodyTop + 170f, 180f, UIRole.Kind.Button, () => Purify(1), out purifyOne).text = "Purify one";
        purifyAllLabel = AddButton("Purify All", page, Margin + 192f, BodyTop + 170f, 240f, UIRole.Kind.PrimaryButton, () => Purify(SettlementSettings.Current.MaxQueuedBatches), out purifyAll);
        warningLine = Paragraph("Warning", page, Margin, BodyTop + 232f, 560f, 56f, 19f, style.Warning);

        Hairline(page, 624f, BodyTop, 1f, BodyBottom - BodyTop, style);
        Heading("What essence buys", page, 644f, BodyTop, 552f, style);
        Paragraph("Purified", page, 644f, BodyTop + 36f, 552f, 190f, 19f, style.Text).text =
            "<b>Purified Essence</b>\nEmpowers gear and deepens skills. Works enchantments with no drawback, strengthens the Heart Tree's ward, " +
            "raises the greatest building tiers and brews the Purified Draught at a Cauldron. Stored at home, it is safe.";
        Paragraph("Cursed", page, 644f, BodyTop + 236f, 552f, 190f, 19f, style.Text).text =
            "<b>Cursed Essence</b>\nUsed raw, it works stronger enchantments, each with a stated price, and brews the Cursed Draught. " +
            "It works at once, with no queue, and a raid's worth comes home only if you do.";
    }

    private void Purify(int batches)
    {
        if (AltarWork.Purify(viewer, batches))
        {
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
        offerEffect = Paragraph("Effect", page, 856f, BodyTop, 340f, 250f, 19f, style.Text);
        costHeading = Heading("Cost", page, 856f, BodyTop + 258f, 340f, style);
        offerCost = Paragraph("Costs", page, 856f, BodyTop + 288f, 340f, 104f, 19f, style.Text);
        offerProblem = Label("Problem", page, 856f, BodyTop + 394f, 340f, 28f, 18f, style.Warning);
        applyLabel = AddButton("Apply", page, 856f, BodyBottom - 48f, 340f, UIRole.Kind.PrimaryButton, Apply, out applyButton);
    }

    private static float HorizontalDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
