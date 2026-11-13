using System.Collections.Generic;
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
    private const float RiseSeconds = 0.12f;
    private const float RiseUnits = 30f;
    private const float ReadSeconds = 0.5f;

    private static readonly BuildCategory[] Tabs = { BuildCategory.Stations, BuildCategory.Walls, BuildCategory.Structures, BuildCategory.Manage };
    private static BuildCategory lastTab = BuildCategory.Stations;

    // Hearthline 3: framed tabs as KitTabs draws them, with the count of what is in each.
    private sealed class Tab
    {
        public BuildCategory Category;
        public GameObject Root;
        public TMP_Text Label;
        public Image Fill;
        public Image Rim;
    }

    // One cost in the details: what it is, its picture, and what the player has of it.
    private sealed class CostRow
    {
        public GameObject Root;
        public Image Icon;
        public TMP_Text Name;
        public TMP_Text Amount;
    }

    private sealed class Card
    {
        public BuildingDefinition Building;
        public bool ToUpgrade;
        public string Title;
        public string Line;
        public Image Frame;
        public Image Rim;
        public Image Icon;
        public TMP_Text Name;
        public TMP_Text Cost;
        public bool Affordable = true;
        public string Missing;
    }

    private UIWindow window;
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
    private TMP_Text costNone;
    private readonly List<CostRow> costRows = new List<CostRow>();
    private RectTransform detailFrame;
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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForNewPlaySession() => Current = null;

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

    private void OnEnable()
    {
        Current = this;
        PadTabs.Register(this, () => IsOpen, StepTab, () => tabs.ConvertAll(tab => (RectTransform)tab.Root.transform));
    }

    // LB and RB on a controller (PadTabs): the categories shown, in order, round from the last to the first.
    private void StepTab(int direction)
    {
        var showing = tabs.FindAll(tab => tab.Root != null && tab.Root.activeSelf);
        if (showing.Count == 0)
        {
            return;
        }

        int at = showing.FindIndex(tab => tab.Category == shown);
        ShowTab(showing[PadTabs.Next(Mathf.Max(0, at), showing.Count, direction)].Category);
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

        if (builder == null || !BuildPlacer.HasBuildArea)
        {
            window.Close();
            return;
        }

        float shownShare = Mathf.Clamp01((Time.unscaledTime - openedAt) / RiseSeconds);
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
        // Hearthline 3: a rounded card with a gold rim (bright on the one chosen), lifting under the pointer.
        card.Frame = HearthKit.Sliced(go.AddComponent<Image>(), style.Card, 1.3f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = card.Frame;
        HearthKit.Tint(button);
        button.onClick.AddListener(() => Choose(index));
        UIHighlight.On(go);
        card.Rim = HearthKit.Rim((RectTransform)go.transform, 0.25f, 1.3f);
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

        card.Name = Label("Name", go.transform, 6f, 106f, CardSize - 12f, 24f, 17f, style.Text, TextAlignmentOptions.Center);
        card.Name.text = card.Title;
        Shrink(card.Name, 12f);
        // Whether it can be built now, and if not the first thing short; every cost is in the details beside.
        card.Cost = Label("State", go.transform, 6f, 128f, CardSize - 12f, 20f, 14f, style.Muted, TextAlignmentOptions.Center);
        card.Cost.text = "";
        Shrink(card.Cost, 11f);
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
            tab.Label.text = count > 0 ? $"{BuildCategories.Name(tab.Category)}  <color=#{ColorUtility.ToHtmlStringRGB(on ? style.Accent : style.Muted)}><size=85%>{count}</size></color>"
                : BuildCategories.Name(tab.Category);
            tab.Label.color = on ? style.AccentBright : count > 0 ? style.Text : style.Inactive;
            tab.Label.fontStyle = on ? FontStyles.Bold : FontStyles.Normal;
            tab.Fill.color = on ? Color.Lerp(style.Inset, style.AccentDeep, 0.55f) : style.Inset;
            tab.Rim.color = on ? style.Accent : HearthKit.Faded(style.Accent, 0.3f);
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

            // What can't be built yet goes grey; what it is short of in red under it.
            card.Name.color = card.Affordable ? style.Text : style.Inactive;
            card.Cost.text = card.Building == null ? "" : card.Affordable ? "Can build" : Short(card.Building, pool);
            card.Cost.color = card.Affordable ? style.Good : style.Danger;
            Paint(card, i == selected);
        }

        DrawDetails(pool);
    }

    private void Paint(Card card, bool on)
    {
        UIStyle style = UIStyle.Current;
        // Only a little lit, the rim carries it: a grey name (what can't be built yet) must still read on it.
        card.Frame.color = on ? Color.Lerp(style.Inset, style.AccentDeep, 0.2f) : style.Inset;
        card.Rim.color = on ? style.Accent : HearthKit.Faded(style.Accent, 0.25f);
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
        if (!any || card.Building == null)
        {
            ShowCosts(null, pool);
        }

        buildButton.gameObject.SetActive(any);
        detailFrame.gameObject.SetActive(any);
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
        ShowCosts(building, pool);
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

    // Each cost as a row with its picture, what the player has against what it takes (green when enough, red when not);
    // past three they go in two columns.
    private void ShowCosts(BuildingDefinition building, MaterialPool pool)
    {
        UIStyle style = UIStyle.Current;
        var shownCosts = new List<(string name, Sprite icon, int need, int have)>();
        if (building != null)
        {
            foreach (ResourceAmount cost in building.ConstructionCost)
            {
                if (cost.Resource != null && cost.Amount > 0)
                {
                    shownCosts.Add((cost.Resource.DisplayName, cost.Resource.Icon, cost.Amount, pool != null ? pool.Have(cost.Resource) : 0));
                }
            }

            foreach (ItemAmount cost in building.ConstructionItems)
            {
                if (cost.Item != null && cost.Amount > 0)
                {
                    shownCosts.Add((cost.Item.DisplayName, cost.Item.Icon, cost.Amount, pool != null ? pool.Have(cost.Item) : 0));
                }
            }
        }

        costNone.gameObject.SetActive(building != null && shownCosts.Count == 0);
        bool twoColumns = shownCosts.Count > 3;
        float columnWidth = twoColumns ? (DetailWidth - 16f) * 0.5f : DetailWidth;
        for (int i = 0; i < Mathf.Max(costRows.Count, shownCosts.Count); i++)
        {
            if (i >= costRows.Count)
            {
                costRows.Add(MakeCostRow(i));
            }

            CostRow row = costRows[i];
            bool used = i < shownCosts.Count && i < CostRowsShown;
            row.Root.SetActive(used);
            if (!used)
            {
                continue;
            }

            var (name, icon, need, have) = shownCosts[i];
            int column = twoColumns ? i / 3 : 0;
            int line = twoColumns ? i % 3 : i;
            var rect = (RectTransform)row.Root.transform;
            rect.anchoredPosition = new Vector2(DetailLeft + column * (columnWidth + 16f), -(CostTop + line * CostRowHeight));
            rect.sizeDelta = new Vector2(columnWidth, CostRowHeight);
            row.Amount.rectTransform.anchoredPosition = new Vector2(columnWidth - 90f, 0f);
            row.Name.rectTransform.sizeDelta = new Vector2(columnWidth - 30f - 92f, CostRowHeight);
            row.Icon.sprite = icon;
            row.Icon.enabled = icon != null;
            row.Name.text = name;
            row.Name.color = have >= need ? style.Text : style.Muted;
            string colour = ColorUtility.ToHtmlStringRGB(have >= need ? style.Good : style.Danger);
            row.Amount.text = $"<color=#{colour}>{have}</color><color=#{ColorUtility.ToHtmlStringRGB(style.Muted)}> / {need}</color>";
        }
    }

    private const float CostRowHeight = 24f;
    private const int CostRowsShown = 6;
    private float CostTop => GridTop + 10f + 120f + 106f;

    private CostRow MakeCostRow(int index)
    {
        UIStyle style = UIStyle.Current;
        RectTransform rect = At("Cost " + index, panel, DetailLeft, CostTop, DetailWidth, CostRowHeight);
        var row = new CostRow { Root = rect.gameObject };
        row.Icon = At("Icon", rect, 0f, 1f, 22f, 22f).gameObject.AddComponent<Image>();
        row.Icon.preserveAspect = true;
        row.Icon.raycastTarget = false;
        row.Name = Label("Name", rect, 30f, 0f, DetailWidth - 122f, CostRowHeight, 17f, style.Text);
        row.Amount = Label("Amount", rect, DetailWidth - 90f, 0f, 90f, CostRowHeight, 17f, style.Text, TextAlignmentOptions.Right);
        return row;
    }

    // "Needs 8 Sticks", and "+2" when two more things are short as well.
    private static string Short(BuildingDefinition building, MaterialPool pool)
    {
        if (pool == null)
        {
            return "";
        }

        string first = null;
        int more = 0;
        foreach (ResourceAmount cost in building.ConstructionCost)
        {
            if (cost.Resource != null && pool.Have(cost.Resource) < cost.Amount)
            {
                if (first == null) first = $"Needs {cost.Amount - pool.Have(cost.Resource)} {cost.Resource.DisplayName}";
                else more++;
            }
        }

        foreach (ItemAmount cost in building.ConstructionItems)
        {
            if (cost.Item != null && pool.Have(cost.Item) < cost.Amount)
            {
                if (first == null) first = $"Needs {cost.Amount - pool.Have(cost.Item)} {cost.Item.DisplayName}";
                else more++;
            }
        }

        return first == null ? "" : more > 0 ? $"{first} +{more}" : first;
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
        RectTransform rect = At(category + " Tab", panel, x, 14f, TabWidth, 40f);
        Image fill = HearthKit.Sliced(rect.gameObject.AddComponent<Image>(), style.Card, 1.4f);
        Image rim = HearthKit.Rim(rect, 0.3f, 1.4f);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        HearthKit.Tint(button);
        UIHighlight.On(rect.gameObject);
        button.onClick.AddListener(() => ShowTab(category));
        TMP_Text label = Label("Label", rect, 0f, 0f, TabWidth, 40f, 19f, style.Text, TextAlignmentOptions.Center);
        style.Title(label);
        tabs.Add(new Tab { Category = category, Root = rect.gameObject, Label = label, Fill = fill, Rim = rim });
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
        // The picture in a gold-framed card, as the tooltip's.
        detailFrame = HearthKit.Card(panel, "Icon Frame", DetailLeft, top, icon, icon, 0.7f);
        detailIcon = At("Icon", panel, DetailLeft + 8f, top + 8f, icon - 16f, icon - 16f).gameObject.AddComponent<Image>();
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
        costNone = Label("Free", panel, DetailLeft, CostTop, DetailWidth, CostRowHeight, 17f, style.Good);
        costNone.text = "Free";
        buildLabel = AddButton("Build", panel, DetailLeft, Height - 66f, DetailWidth, UIRole.Kind.PrimaryButton,
            () => Choose(selected), out buildButton);
    }
}
