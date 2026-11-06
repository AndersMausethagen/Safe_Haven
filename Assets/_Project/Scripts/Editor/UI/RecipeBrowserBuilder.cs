using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds the RecipeBrowser the bag and the crafting stations share, in the Hearthline style, and the row prefab it lists
/// (menu: Safe Haven > Setup > UI > Build Recipe Browsers). Safe to run again: the row keeps its GUID, the bag's browser is
/// replaced in ItemHUD, and the station window is rebuilt by StationWindowBuilder.
/// </summary>
public static class RecipeBrowserBuilder
{
    public const string RowPrefabPath = "Assets/_Project/Prefabs/UI/RecipeRow.prefab";
    private const string BagPrefabPath = "Assets/_Project/Prefabs/UI/ItemHUD.prefab";
    private const string BrowserName = "Recipe Browser";
    private const float TabHeight = 30f;
    private const float Gap = 10f;
    private const float FilterWidth = 124f;
    // What the fixed bottom of the selected recipe needs: the Needs heading, four materials, a problem line and the buttons.
    private const float FooterHeight = 214f;

    [MenuItem("Safe Haven/Setup/UI/Build Recipe Browsers", false, 200)]
    public static void BuildAll()
    {
        BuildRow();
        BuildBag();
        StationWindowBuilder.Build();
    }

    public static RecipeRowView BuildRow()
    {
        var root = new GameObject("RecipeRow", typeof(RectTransform));
        try
        {
            UIStyle style = UIStyle.Current;
            ((RectTransform)root.transform).sizeDelta = new Vector2(320f, 54f);
            root.AddComponent<LayoutElement>().preferredHeight = 54f;
            var background = root.AddComponent<Image>();
            background.color = style.Inset;
            var row = root.AddComponent<RecipeRowView>();

            RectTransform line = StationWindowBuilder.Rect("Selected Line", root.transform, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(3f, 0f));
            var lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = style.Accent;
            lineImage.raycastTarget = false;

            RectTransform iconRect = StationWindowBuilder.Rect("Icon", root.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(40f, 40f));
            var icon = iconRect.gameObject.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text name = Line("Name", root.transform, 15f, style.Text, -7f, 22f);
            // "Crude Stone Arrows ×5" should still read whole in the bag's narrow list.
            name.fontSizeMax = 15f;
            name.fontSizeMin = 12f;
            name.enableAutoSizing = true;
            TMP_Text status = Line("Status", root.transform, 13f, style.Muted, -29f, 18f);
            status.rectTransform.offsetMax = new Vector2(-8f, status.rectTransform.offsetMax.y);
            TMP_Text tier = StationWindowBuilder.Text("Tier", root.transform, "I", 13f, style.Muted);
            StationWindowBuilder.Place(tier.rectTransform, new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(28f, 20f));
            tier.alignment = TextAlignmentOptions.TopRight;

            StationWindowBuilder.Wire(row, ("background", background), ("selectedLine", lineImage), ("icon", icon),
                ("nameLabel", name), ("statusLabel", status), ("tierLabel", tier));
            return PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath).GetComponent<RecipeRowView>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // A line of a row: stretches between the icon and the tier, and ends in an ellipsis rather than wrapping.
    private static TMP_Text Line(string name, Transform parent, float size, Color color, float y, float height)
    {
        TMP_Text label = StationWindowBuilder.Text(name, parent, "", size, color);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.offsetMin = new Vector2(60f, y - height);
        rect.offsetMax = new Vector2(-34f, y);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }

    /// <summary>Adds a browser at a top-left position: tabs across the top, the list on the left, the selected recipe beside it.</summary>
    public static RecipeBrowser Add(Transform parent, float x, float y, float width, float height, float listWidth)
    {
        UIStyle style = UIStyle.Current;
        RecipeRowView rowPrefab = AssetDatabase.LoadAssetAtPath<RecipeRowView>(RowPrefabPath);
        RectTransform root = StationWindowBuilder.TopLeft(x, y, width, height, BrowserName, parent);
        var browser = root.gameObject.AddComponent<RecipeBrowser>();

        // The tabs, with the filter at the far end.
        RectTransform tabBar = StationWindowBuilder.TopLeft(0f, 0f, width - FilterWidth - Gap, TabHeight, "Tab Bar", root);
        StationWindowBuilder.Configure(tabBar.gameObject.AddComponent<HorizontalLayoutGroup>(), 2f, TextAnchor.UpperLeft);
        Button tabTemplate = Tab("Tab Template", tabBar, "Tab");
        Button canMake = Tab("Can Make", root, "Can make");
        StationWindowBuilder.Place((RectTransform)canMake.transform, new Vector2(1f, 1f), Vector2.zero, new Vector2(FilterWidth, TabHeight));
        canMake.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Center;

        // The list.
        float top = TabHeight + 6f;
        RectTransform scroll = StationWindowBuilder.TopLeft(0f, -top, listWidth, height - top, "Recipe Scroll", root);
        Role(scroll.gameObject.AddComponent<Image>(), UIRole.Kind.Inset);
        var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
        RectTransform viewport = StationWindowBuilder.Rect("Viewport", scroll, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        viewport.offsetMax = new Vector2(-10f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = StationWindowBuilder.Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        var rows = content.gameObject.AddComponent<VerticalLayoutGroup>();
        rows.padding = new RectOffset(4, 0, 4, 4);
        rows.spacing = 2f;
        rows.childControlWidth = true;
        rows.childControlHeight = true;
        rows.childForceExpandWidth = true;
        rows.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.verticalScrollbar = Bar(scroll);
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        TMP_Text empty = StationWindowBuilder.Text("Empty Label", scroll, "You know no recipes here yet.\nBlueprints and bosses teach new ones.", 14f, style.Muted);
        StationWindowBuilder.Stretch(empty.rectTransform, 20f);
        empty.alignment = TextAlignmentOptions.Center;

        // The selected recipe.
        float detailWidth = width - listWidth - Gap;
        float inner = detailWidth - 24f;
        RectTransform selected = StationWindowBuilder.TopLeft(listWidth + Gap, -top, detailWidth, height - top, "Selected", root);
        Role(selected.gameObject.AddComponent<Image>(), UIRole.Kind.Inset);
        RectTransform frame = StationWindowBuilder.TopLeft(12f, -12f, 64f, 64f, "Icon Frame", selected);
        Role(frame.gameObject.AddComponent<Image>(), UIRole.Kind.Slot);
        RectTransform iconRect = StationWindowBuilder.Rect("Icon", frame, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-10f, -10f));
        var icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        TMP_Text nameLabel = StationWindowBuilder.At(StationWindowBuilder.Text("Name", selected, "", 20f, style.Text), 86f, -10f, detailWidth - 98f, 46f);
        style.Title(nameLabel);
        nameLabel.alignment = TextAlignmentOptions.MidlineLeft;
        nameLabel.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text infoLabel = StationWindowBuilder.At(StationWindowBuilder.Text("Info", selected, "", 13f, style.Muted), 86f, -56f, detailWidth - 98f, 22f);
        infoLabel.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform descriptionArea = StationWindowBuilder.Rect("Description Scroll", selected, Vector2.zero, Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
        descriptionArea.offsetMin = new Vector2(12f, FooterHeight);
        descriptionArea.offsetMax = new Vector2(-12f, -86f);
        descriptionArea.gameObject.AddComponent<RectMask2D>();
        // Invisible, but the wheel needs something to land on: text doesn't take the pointer.
        descriptionArea.gameObject.AddComponent<Image>().color = Color.clear;
        TMP_Text description = StationWindowBuilder.Text("Description", descriptionArea, "", 14f, style.Text);
        RectTransform descriptionRect = description.rectTransform;
        descriptionRect.anchorMin = new Vector2(0f, 1f);
        descriptionRect.anchorMax = Vector2.one;
        descriptionRect.pivot = new Vector2(0.5f, 1f);
        descriptionRect.anchoredPosition = Vector2.zero;
        descriptionRect.sizeDelta = Vector2.zero;
        description.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var descriptionScroll = descriptionArea.gameObject.AddComponent<ScrollRect>();
        descriptionScroll.viewport = descriptionArea;
        descriptionScroll.content = descriptionRect;
        descriptionScroll.horizontal = false;
        descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
        descriptionScroll.scrollSensitivity = 30f;

        // The bottom stays put from recipe to recipe, so the buttons never move under the pointer.
        TMP_Text needs = StationWindowBuilder.Text("Needs Title", selected, "Needs", 14f, style.Accent);
        StationWindowBuilder.Place(needs.rectTransform, Vector2.zero, new Vector2(12f, FooterHeight - 22f), new Vector2(inner, 22f));
        RectTransform costList = StationWindowBuilder.Rect("Cost List", selected, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(12f, 86f), new Vector2(inner, 104f));
        var costLayout = costList.gameObject.AddComponent<VerticalLayoutGroup>();
        costLayout.spacing = 2f;
        costLayout.childAlignment = TextAnchor.UpperLeft;
        costLayout.childControlWidth = true;
        costLayout.childControlHeight = true;
        costLayout.childForceExpandWidth = true;
        costLayout.childForceExpandHeight = false;
        RecipeCostRow costTemplate = CostRow(costList);

        TMP_Text problem = StationWindowBuilder.Text("Problem", selected, "", 13f, style.Danger);
        StationWindowBuilder.Place(problem.rectTransform, Vector2.zero, new Vector2(12f, 58f), new Vector2(inner, 24f));
        problem.textWrappingMode = TextWrappingModes.NoWrap;
        problem.overflowMode = TextOverflowModes.Ellipsis;

        float buttonWidth = (inner - 8f) / 2f;
        Button makeAll = StationWindowBuilder.MakeButton("Make All Button", selected, "Craft all", 15f);
        StationWindowBuilder.Place((RectTransform)makeAll.transform, Vector2.zero, new Vector2(12f, 12f), new Vector2(buttonWidth, 40f));
        Button make = StationWindowBuilder.MakeButton("Make Button", selected, "Craft", 16f, UIRole.Kind.PrimaryButton);
        StationWindowBuilder.Place((RectTransform)make.transform, new Vector2(1f, 0f), new Vector2(-12f, 12f), new Vector2(buttonWidth, 40f));

        StationWindowBuilder.Wire(browser,
            ("tabBar", tabBar), ("tabTemplate", tabTemplate), ("canMakeButton", canMake), ("list", content), ("rowPrefab", rowPrefab), ("emptyLabel", empty),
            ("selectedPanel", selected.gameObject), ("icon", icon), ("nameLabel", nameLabel), ("infoLabel", infoLabel), ("descriptionLabel", description),
            ("costList", costList), ("costTemplate", costTemplate), ("problemLabel", problem), ("makeButton", make), ("makeAllButton", makeAll));
        return browser;
    }

    // Tabs are type and a thin line, like the rest of Hearthline, so the label is what takes the pointer.
    private static Button Tab(string name, Transform parent, string text)
    {
        UIStyle style = UIStyle.Current;
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        TMP_Text label = StationWindowBuilder.Text("Label", rect, text, 14f, style.Muted);
        StationWindowBuilder.Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = true;
        button.targetGraphic = label;
        StationWindowBuilder.FitToLabel(button, 8, TabHeight);
        RectTransform line = StationWindowBuilder.Rect("Line", rect, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 2f));
        var lineImage = line.gameObject.AddComponent<Image>();
        lineImage.color = style.Accent;
        lineImage.raycastTarget = false;
        line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        return button;
    }

    private static Scrollbar Bar(Transform parent)
    {
        UIStyle style = UIStyle.Current;
        RectTransform bar = StationWindowBuilder.Rect("Scrollbar", parent, new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0.5f), new Vector2(-2f, 0f), new Vector2(5f, -8f));
        RectTransform area = StationWindowBuilder.Rect("Sliding Area", bar, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        RectTransform handle = StationWindowBuilder.Rect("Handle", area, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = style.ControlOn;
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        return scrollbar;
    }

    private static RecipeCostRow CostRow(Transform parent)
    {
        UIStyle style = UIStyle.Current;
        var rect = new GameObject("Cost Template", typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
        var row = rect.gameObject.AddComponent<RecipeCostRow>();
        RectTransform iconRect = StationWindowBuilder.Rect("Icon", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
        var icon = iconRect.gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        TMP_Text name = StationWindowBuilder.Text("Name", rect, "", 14f, style.Text);
        Stretch(name.rectTransform, 30f, -74f);
        name.alignment = TextAlignmentOptions.MidlineLeft;
        name.textWrappingMode = TextWrappingModes.NoWrap;
        name.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text amount = StationWindowBuilder.Text("Amount", rect, "", 14f, style.Good);
        StationWindowBuilder.Place(amount.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(70f, 22f));
        amount.alignment = TextAlignmentOptions.MidlineRight;
        amount.textWrappingMode = TextWrappingModes.NoWrap;
        StationWindowBuilder.Wire(row, ("icon", icon), ("nameLabel", name), ("amountLabel", amount));
        return row;
    }

    private static void Stretch(RectTransform rect, float left, float right)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(right, 0f);
    }

    private static void Role(Image image, UIRole.Kind kind)
    {
        image.gameObject.AddComponent<UIRole>().Set(kind);
    }

    /// <summary>Swaps the bag's crafting side for a browser, keeping everything else in ItemHUD as it is.</summary>
    public static void BuildBag()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BagPrefabPath);
        try
        {
            var window = root.GetComponentInChildren<ItemInventoryWindow>(true);
            Transform panel = window.transform;
            foreach (string old in new[] { "Recipe Scroll", BrowserName })
            {
                Transform found = panel.Find(old);
                if (found != null)
                {
                    Object.DestroyImmediate(found.gameObject);
                }
            }

            RecipeBrowser browser = Add(panel, 604f, -116f, 532f, 558f, 246f);
            // The tooltip and the dragged icon draw over everything, so they stay last.
            browser.transform.SetSiblingIndex(panel.Find("Tooltip").GetSiblingIndex());
            RectTransform footer = (RectTransform)panel.Find("Recipe Footer");
            footer.anchoredPosition = new Vector2(604f, -678f);
            footer.sizeDelta = new Vector2(532f, 22f);
            var footerLabel = footer.GetComponent<TMP_Text>();
            footerLabel.fontSize = 13f;
            footerLabel.textWrappingMode = TextWrappingModes.NoWrap;
            footerLabel.overflowMode = TextOverflowModes.Ellipsis;
            StationWindowBuilder.Wire(window, ("recipes", browser));
            PrefabUtility.SaveAsPrefabAsset(root, BagPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
