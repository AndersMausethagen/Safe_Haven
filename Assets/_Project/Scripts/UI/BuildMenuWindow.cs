using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CodeUI;

/// <summary>
/// The build menu (G, GDD §14.1), built in code: a panel docked at the bottom of the screen with a tab per kind of building,
/// a grid of picture cards and, on the right, the pointed-at building's description, what it costs against what the player has,
/// and a Build button. Choosing a building closes the menu and hands it to BuildPlacer to place. The world stays in view above it.
/// </summary>
public sealed class BuildMenuWindow : MonoBehaviour
{
    private const float Width = 1480f;
    private const float Height = 456f;
    private const float Margin = 28f;
    private const float BottomGap = 24f;
    private const float ScreenMargin = 40f;
    private const float TabsLeft = 170f;
    private const float TabWidth = 220f;
    private const float GridTop = 72f;
    private const float GridWidth = 980f;
    private const float GridHeight = 328f;
    private const float CardSize = 152f;
    private const float CardGap = 12f;
    private const float DetailLeft = 1040f;
    private const float DetailWidth = Width - DetailLeft - Margin;
    private const float FadeSeconds = 0.12f;
    private const float RiseUnits = 30f;
    private const float ReadSeconds = 0.5f;

    private static readonly BuildCategory[] Tabs = { BuildCategory.Stations, BuildCategory.Walls, BuildCategory.Structures, BuildCategory.Manage };
    private static BuildCategory lastTab = BuildCategory.Stations;

    private sealed class Tab
    {
        public BuildCategory Category;
        public GameObject Root;
        public TMP_Text Label;
        public Image Underline;
    }

    private sealed class Card
    {
        public BuildingDefinition Building;
        public bool ToUpgrade;
        public string Title;
        public string Line;
        public Image Frame;
        public Outline Rim;
        public Image Icon;
        public TMP_Text Cost;
        public bool Affordable = true;
        public string Missing;
    }

    private UIWindow window;
    private CanvasGroup fade;
    private RectTransform panel;
    private RectTransform grid;
    private ScrollRect scroll;
    private TMP_Text emptyNote;
    private Image detailIcon;
    private TMP_Text detailGlyph;
    private TMP_Text detailName;
    private TMP_Text detailKind;
    private TMP_Text detailTags;
    private TMP_Text detailText;
    private TMP_Text costHeading;
    private TMP_Text costLines;
    private TMP_Text buildLabel;
    private Button buildButton;
    private TMP_Text hint;
    private readonly List<Tab> tabs = new List<Tab>();
    private readonly List<Card> cards = new List<Card>();
    private BuildPlacer builder;
    private BuildCategory shown;
    private int selected = -1;
    private float openedAt;
    private float nextRead;

    public static BuildMenuWindow Current { get; private set; }

    public bool IsOpen => window != null && window.IsOpen;

    /// <summary>Builds the menu (closed) for this scene, unless it has one.</summary>
    public static BuildMenuWindow Create()
    {
        if (Current != null)
        {
            return Current;
        }

        var root = new GameObject("Build Menu Window");
        root.SetActive(false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        UIRoot.ConfigureScaler(root.AddComponent<CanvasScaler>());
        root.AddComponent<GraphicRaycaster>();
        var view = root.AddComponent<BuildMenuWindow>();
        view.Build(root.transform);
        root.SetActive(true);
        return view;
    }

    public void Open(BuildPlacer placer)
    {
        builder = placer;
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

        if (builder == null || !BuildPlacer.HasBuildArea)
        {
            window.Close();
            return;
        }

        float shownShare = Mathf.Clamp01((Time.unscaledTime - openedAt) / FadeSeconds);
        fade.alpha = shownShare;
        panel.anchoredPosition = new Vector2(0f, BottomGap - (1f - shownShare) * (1f - shownShare) * RiseUnits);
        if (Time.unscaledTime >= nextRead)
        {
            Read();
        }
    }

    private void OnOpenChanged(bool open)
    {
        if (!open)
        {
            return;
        }

        if (ActionMenuWindow.Current != null && ActionMenuWindow.Current.IsOpen)
        {
            ActionMenuWindow.Current.Close();
        }

        openedAt = Time.unscaledTime;
        fade.alpha = 0f;
        FitTheScreen();
        hint.text = $"Click a building to place it  \u2022  Mouse wheel turns it, Shift for small steps  \u2022  Hold Alt to place freely  \u2022  "
            + $"Right-click cancels  \u2022  {GameInput.KeyLabel(GameInput.Build)} or Esc closes";
        ShowTab(Count(lastTab) > 0 ? lastTab : FirstFilledTab());
    }

    private void FitTheScreen()
    {
        Rect screen = ((RectTransform)transform).rect;
        float fit = Mathf.Min((screen.width - ScreenMargin) / Width, (screen.height - ScreenMargin) / Height);
        panel.localScale = Vector3.one * Mathf.Min(1f, fit);
    }

    private BuildCategory FirstFilledTab()
    {
        foreach (BuildCategory category in Tabs)
        {
            if (Count(category) > 0)
            {
                return category;
            }
        }

        return BuildCategory.Stations;
    }

    private int Count(BuildCategory category)
    {
        if (builder == null)
        {
            return 0;
        }

        if (category == BuildCategory.Manage)
        {
            return builder.AllowsChanges ? 2 : 0;
        }

        int count = 0;
        foreach (BuildingDefinition building in builder.Buildable())
        {
            if (BuildCategories.Of(building) == category)
            {
                count++;
            }
        }

        return count;
    }

    private void ShowTab(BuildCategory category)
    {
        shown = category;
        lastTab = category;
        foreach (Transform child in grid)
        {
            Destroy(child.gameObject);
        }

        cards.Clear();
        if (category == BuildCategory.Manage)
        {
            if (builder.AllowsChanges)
            {
                AddCard(new Card { ToUpgrade = true, Title = "Upgrade or repair", Line = "Make something better, or bring a ruin back." });
                AddCard(new Card { ToUpgrade = false, Title = "Move", Line = "Pick up something you built and place it again, for free." });
            }
        }
        else
        {
            foreach (BuildingDefinition building in builder.Buildable())
            {
                if (BuildCategories.Of(building) == category)
                {
                    AddCard(new Card { Building = building, Title = building.DisplayName });
                }
            }
        }

        emptyNote.gameObject.SetActive(cards.Count == 0);
        emptyNote.text = category == BuildCategory.Manage
            ? "Nothing here can be moved or upgraded yet."
            : $"You don't know how to build any {BuildCategories.Name(category).ToLowerInvariant()} yet.\n\n{builder.Guidance}";
        scroll.verticalNormalizedPosition = 1f;
        selected = -1;
        Read();
        Select(cards.Count > 0 ? 0 : -1);
    }

    private void AddCard(Card card)
    {
        UIStyle style = UIStyle.Current;
        int index = cards.Count;
        var go = new GameObject(card.Title, typeof(RectTransform));
        go.transform.SetParent(grid, false);
        card.Frame = go.AddComponent<Image>();
        card.Rim = go.AddComponent<Outline>();
        card.Rim.effectDistance = new Vector2(1f, -1f);
        go.AddComponent<UIRole>().Set(UIRole.Kind.Slot);
        var button = go.AddComponent<Button>();
        button.targetGraphic = card.Frame;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Choose(index));
        var focus = go.AddComponent<BuildMenuCard>();
        focus.Index = index;
        focus.Focused += c => Select(c.Index);

        if (card.Building != null && card.Building.Icon != null)
        {
            card.Icon = At("Icon", go.transform, (CardSize - 96f) * 0.5f, 10f, 96f, 96f).gameObject.AddComponent<Image>();
            card.Icon.sprite = card.Building.Icon;
            card.Icon.preserveAspect = true;
            card.Icon.raycastTarget = false;
        }
        else
        {
            TMP_Text glyph = Label("Glyph", go.transform, 0f, 10f, CardSize, 96f, 54f, style.Accent, TextAlignmentOptions.Center);
            glyph.text = card.Building != null ? Initials(card.Title) : card.ToUpgrade ? "\u25B2" : "\u21C4";
        }

        TMP_Text name = Label("Name", go.transform, 6f, 108f, CardSize - 12f, 22f, 17f, style.Text, TextAlignmentOptions.Center);
        name.text = card.Title;
        Shrink(name, 12f);
        card.Cost = Label("Cost", go.transform, 6f, 128f, CardSize - 12f, 18f, 13f, style.Muted, TextAlignmentOptions.Center);
        card.Cost.text = card.Building != null ? ShortCost(card.Building) : "";
        Shrink(card.Cost, 10f);
        cards.Add(card);
    }

    private static void Shrink(TMP_Text text, float smallest)
    {
        text.fontSizeMax = text.fontSize;
        text.fontSizeMin = smallest;
        text.enableAutoSizing = true;
    }

    private static string Initials(string title) => string.IsNullOrEmpty(title) ? "?" : title.Substring(0, 1).ToUpperInvariant();

    // Affordability changes as the player gathers, so cards and details are re-read while open.
    private void Read()
    {
        nextRead = Time.unscaledTime + ReadSeconds;
        UIStyle style = UIStyle.Current;
        MaterialPool pool = builder != null ? MaterialPool.For(builder.gameObject) : null;
        foreach (Tab tab in tabs)
        {
            int count = Count(tab.Category);
            bool on = tab.Category == shown;
            tab.Root.SetActive(tab.Category != BuildCategory.Manage || builder.AllowsChanges);
            tab.Label.text = count > 0 ? $"{BuildCategories.Name(tab.Category)}  <color=#{ColorUtility.ToHtmlStringRGB(style.Muted)}>{count}</color>"
                : BuildCategories.Name(tab.Category);
            tab.Label.color = on ? style.Accent : count > 0 ? style.Text : style.Muted;
            tab.Underline.enabled = on;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            Card card = cards[i];
            if (card.Building != null && pool != null)
            {
                card.Affordable = pool.CanAfford(card.Building.ConstructionCost, card.Building.ConstructionItems, out card.Missing);
            }

            if (card.Icon != null)
            {
                card.Icon.color = card.Affordable ? Color.white : new Color(1f, 1f, 1f, 0.4f);
            }

            card.Cost.color = card.Affordable ? style.Muted : style.Danger;
            Paint(card, i == selected);
        }

        DrawDetails(pool);
    }

    private void Paint(Card card, bool on)
    {
        UIStyle style = UIStyle.Current;
        card.Frame.color = on ? style.ControlLit : style.Inset;
        card.Rim.effectColor = on ? style.Accent : style.Hairline;
        card.Rim.effectDistance = on ? new Vector2(2f, -2f) : new Vector2(1f, -1f);
    }

    private void Select(int index)
    {
        if (index == selected && index >= 0)
        {
            return;
        }

        selected = index;
        for (int i = 0; i < cards.Count; i++)
        {
            Paint(cards[i], i == selected);
        }

        DrawDetails(builder != null ? MaterialPool.For(builder.gameObject) : null);
    }

    private void DrawDetails(MaterialPool pool)
    {
        UIStyle style = UIStyle.Current;
        Card card = selected >= 0 && selected < cards.Count ? cards[selected] : null;
        bool any = card != null;
        detailName.gameObject.SetActive(any);
        detailKind.gameObject.SetActive(any);
        detailTags.gameObject.SetActive(any);
        detailText.gameObject.SetActive(any);
        costHeading.gameObject.SetActive(any && card.Building != null);
        costLines.gameObject.SetActive(any && card.Building != null);
        buildButton.gameObject.SetActive(any);
        detailIcon.gameObject.SetActive(any && card.Building != null && card.Building.Icon != null);
        detailGlyph.gameObject.SetActive(any && !detailIcon.gameObject.activeSelf);
        if (!any)
        {
            return;
        }

        detailName.text = card.Title;
        if (card.Building == null)
        {
            detailGlyph.text = card.ToUpgrade ? "\u25B2" : "\u21C4";
            detailKind.text = "Manage";
            detailTags.text = "";
            detailText.text = card.ToUpgrade
                ? "Click a building you have built to see what its next level costs and brings, or walk up to a ruin to repair it."
                : "Click a building you have built to pick it up, then place it somewhere else. Moving costs nothing.";
            buildLabel.text = "Choose a building";
            buildButton.interactable = true;
            return;
        }

        BuildingDefinition building = card.Building;
        detailIcon.sprite = building.Icon;
        detailGlyph.text = Initials(building.DisplayName);
        detailKind.text = BuildCategories.Name(BuildCategories.Of(building));
        detailTags.text = Tags(building, style);
        detailText.text = string.IsNullOrWhiteSpace(building.Description) ? "" : building.Description;
        costHeading.text = building.PlaceInLines ? "COST, EACH PIECE" : "COST";
        costLines.text = CostLines(building, pool, style);
        buildButton.interactable = card.Affordable;
        buildLabel.text = card.Affordable ? "Build" : "Not enough materials";
    }

    private static string Tags(BuildingDefinition building, UIStyle style)
    {
        var tags = new List<string>();
        if (building.Renown > 0) tags.Add($"+{building.Renown} renown");
        if (building.PlaceInLines) tags.Add("drag to build a line");
        if (building.YardDepth > 0f) tags.Add("needs clear ground in front");
        if (building.MaxLevel > 1) tags.Add($"{building.MaxLevel} levels");
        return string.Join("   \u2022   ", tags);
    }

    private static string CostLines(BuildingDefinition building, MaterialPool pool, UIStyle style)
    {
        var text = new StringBuilder();
        foreach (ResourceAmount cost in building.ConstructionCost)
        {
            if (cost.Resource != null && cost.Amount > 0)
            {
                AppendCost(text, cost.Resource.DisplayName, cost.Amount, pool != null ? pool.Have(cost.Resource) : 0, style);
            }
        }

        foreach (ItemAmount cost in building.ConstructionItems)
        {
            if (cost.Item != null && cost.Amount > 0)
            {
                AppendCost(text, cost.Item.DisplayName, cost.Amount, pool != null ? pool.Have(cost.Item) : 0, style);
            }
        }

        return text.Length > 0 ? text.ToString() : "Free";
    }

    private static void AppendCost(StringBuilder text, string name, int need, int have, UIStyle style)
    {
        string colour = ColorUtility.ToHtmlStringRGB(have >= need ? style.Good : style.Danger);
        text.Append(name).Append("<pos=62%><color=#").Append(colour).Append('>').Append(have).Append("</color>")
            .Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(style.Muted)).Append("> / ").Append(need).Append("</color>\n");
    }

    private static string ShortCost(BuildingDefinition building)
    {
        var text = new StringBuilder(AmountText.Describe(building.ConstructionCost));
        if (building.ConstructionItems.Count > 0)
        {
            text.Append(text.Length > 0 ? ", " : "").Append(AmountText.Describe(building.ConstructionItems));
        }

        return text.Length > 0 ? text.ToString() : "Free";
    }

    private void Choose(int index)
    {
        if (index < 0 || index >= cards.Count || builder == null)
        {
            return;
        }

        Card card = cards[index];
        if (card.Building == null)
        {
            window.Close();
            builder.PickBuilding(card.ToUpgrade);
            return;
        }

        if (!card.Affordable)
        {
            Select(index);
            GameMessages.Post($"Not enough for the {card.Building.DisplayName}: {card.Missing}", MessageKind.Warning);
            return;
        }

        window.Close();
        builder.Place(card.Building);
    }

    private void Build(Transform root)
    {
        UIStyle style = UIStyle.Current;
        RectTransform content = Stretch("Content", root);
        fade = content.gameObject.AddComponent<CanvasGroup>();

        panel = Box("Panel", content, new Vector2(0.5f, 0f), new Vector2(Width, Height), new Vector2(0f, BottomGap));
        panel.pivot = new Vector2(0.5f, 0f);
        panel.gameObject.AddComponent<Image>();
        panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
        panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);

        TMP_Text kicker = Label("Kicker", panel, Margin, 20f, 130f, 30f, 22f, style.Accent);
        kicker.text = "BUILD";
        kicker.characterSpacing = 12f;
        for (int i = 0; i < Tabs.Length; i++)
        {
            BuildTab(Tabs[i], TabsLeft + i * (TabWidth + 8f), style);
        }

        Hairline(panel, Margin, 62f, Width - Margin * 2f, 1f, style);
        BuildGrid(style);
        Hairline(panel, DetailLeft - 20f, GridTop + 4f, 1f, Height - GridTop - 24f, style);
        BuildDetails(style);
        hint = Label("Hint", panel, Margin, GridTop + GridHeight + 18f, GridWidth, 24f, 15f, style.Muted);

        window = root.gameObject.AddComponent<UIWindow>();
        window.Configure(UIWindowId.Build, content.gameObject);
        window.OpenChanged += OnOpenChanged;
    }

    private void BuildTab(BuildCategory category, float x, UIStyle style)
    {
        Button button = AddTab(category.ToString(), panel, x, 12f, TabWidth, 44f, style, out TMP_Text label, out Image underline);
        button.onClick.AddListener(() => ShowTab(category));
        tabs.Add(new Tab { Category = category, Root = button.gameObject, Label = label, Underline = underline });
    }

    private void BuildGrid(UIStyle style)
    {
        RectTransform area = At("Grid", panel, Margin, GridTop + 6f, GridWidth, GridHeight);
        scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        area.gameObject.AddComponent<RectMask2D>();
        // A transparent graphic lets the wheel scroll over the gaps between cards.
        area.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        grid = At("Cards", area, 0f, 0f, GridWidth, GridHeight);
        var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(CardSize, CardSize);
        layout.spacing = new Vector2(CardGap, CardGap);
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 6;
        var fitter = grid.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = grid;
        scroll.viewport = area;

        emptyNote = Paragraph("Empty", area, 20f, 30f, GridWidth - 40f, GridHeight - 60f, 19f, style.Muted);
    }

    private void BuildDetails(UIStyle style)
    {
        const float icon = 120f;
        float top = GridTop + 10f;
        detailIcon = At("Icon", panel, DetailLeft, top, icon, icon).gameObject.AddComponent<Image>();
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;
        detailGlyph = Label("Glyph", panel, DetailLeft, top, icon, icon, 64f, style.Accent, TextAlignmentOptions.Center);
        float textLeft = DetailLeft + icon + 18f;
        float textWidth = DetailWidth - icon - 18f;
        detailName = Label("Name", panel, textLeft, top + 6f, textWidth, 44f, 32f, style.Text);
        style.Title(detailName);
        detailKind = Label("Kind", panel, textLeft, top + 52f, textWidth, 24f, 17f, style.Muted);
        detailTags = Paragraph("Tags", panel, textLeft, top + 78f, textWidth, 44f, 15f, style.Accent);
        detailText = Paragraph("Description", panel, DetailLeft, top + icon + 12f, DetailWidth, 70f, 17f, style.Text);
        costHeading = Label("Cost Heading", panel, DetailLeft, top + icon + 86f, DetailWidth, 22f, 15f, style.Accent);
        costHeading.characterSpacing = 6f;
        costLines = Paragraph("Cost", panel, DetailLeft, top + icon + 108f, DetailWidth, 64f, 17f, style.Text);
        buildLabel = AddButton("Build", panel, DetailLeft, Height - 66f, DetailWidth, UIRole.Kind.PrimaryButton,
            () => Choose(selected), out buildButton);
    }
}
