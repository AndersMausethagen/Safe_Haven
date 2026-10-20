using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds Prefabs/UI/StationWindow.prefab, the grid-based crafting station window (StationWindow), from code (menu:
/// Safe Haven > UI > Build Station Window). Rebuilding overwrites the prefab; scene instances keep working because
/// the prefab's asset GUID stays the same. Add the prefab under Screen Canvas in scenes with crafting stations.
/// </summary>
public static class StationWindowBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/StationWindow.prefab";
    private const string SlotPrefabPath = "Assets/_Project/Prefabs/UI/ItemSlot.prefab";

    private static readonly Color PanelColor = new Color(0.075f, 0.062f, 0.05f, 0.95f);
    private static readonly Color RimColor = new Color(0.64f, 0.49f, 0.29f, 0.9f);
    private static readonly Color ButtonColor = new Color(0.3f, 0.235f, 0.17f, 1f);
    private static readonly Color ButtonText = new Color(0.95f, 0.9f, 0.8f, 1f);
    private static readonly Color TitleColor = new Color(0.91f, 0.77f, 0.42f, 1f);
    private static readonly Color BodyColor = new Color(0.93f, 0.89f, 0.8f, 1f);
    private static readonly Color MutedColor = new Color(0.68f, 0.71f, 0.77f, 1f);
    private static readonly Color WellColor = new Color(0f, 0f, 0f, 0.25f);

    private const float Right = 612f;
    private const float RightWidth = 364f;

    [MenuItem("Safe Haven/UI/Build Station Window")]
    public static void BuildFromMenu()
    {
        Build();
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
    }

    public static GameObject Build()
    {
        var slotPrefab = AssetDatabase.LoadAssetAtPath<ItemSlotView>(SlotPrefabPath);
        if (slotPrefab == null)
        {
            Debug.LogError($"StationWindowBuilder needs the slot prefab at {SlotPrefabPath}.");
            return null;
        }

        var root = new GameObject("StationWindow", typeof(RectTransform));
        try
        {
            Stretch((RectTransform)root.transform);
            var canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 12;
            root.AddComponent<GraphicRaycaster>();
            var uiWindow = root.AddComponent<UIWindow>();
            var view = root.AddComponent<StationWindow>();

            RectTransform panel = Rect("Panel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(1000f, 620f));
            panel.gameObject.AddComponent<Image>().color = PanelColor;
            var outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = RimColor;
            outline.effectDistance = new Vector2(2f, -2f);
            var tooltip = panel.gameObject.AddComponent<ItemTooltipView>();

            TMP_Text title = At(Text("Title", panel, "Station", 26f, TitleColor), 24f, -16f, 500f, 34f);
            Button close = MakeButton("Close Button", panel, "X", 18f);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(40f, 36f));

            RectTransform actionBar = Rect("Action Bar", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-66f, -16f), new Vector2(560f, 36f));
            var actionLayout = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            Configure(actionLayout, 8f, TextAnchor.UpperRight);
            Button actionTemplate = MakeButton("Action Template", actionBar, "Action", 15f);
            FitToLabel(actionTemplate, 14, 36f);

            // Left: tabs and the recipe grid.
            RectTransform tabBar = TopLeft(24f, -62f, 566f, 32f, "Tab Bar", panel);
            var tabLayout = tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            Configure(tabLayout, 6f, TextAnchor.UpperLeft);
            Button tabTemplate = MakeButton("Tab Template", tabBar, "Tab", 14f);
            FitToLabel(tabTemplate, 12, 32f);

            RectTransform scroll = TopLeft(24f, -102f, 566f, 438f, "Recipe Scroll", panel);
            scroll.gameObject.AddComponent<Image>().color = WellColor;
            var scrollRect = scroll.gameObject.AddComponent<ScrollRect>();
            RectTransform viewport = Rect("Viewport", scroll, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform recipeGrid = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Grid(recipeGrid, 64f, 8, 4);
            recipeGrid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.viewport = viewport;
            scrollRect.content = recipeGrid;
            scrollRect.horizontal = false;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.scrollSensitivity = 30f;
            TMP_Text empty = Text("Empty Label", scroll, "You know no recipes for this station yet.\nBlueprints and bosses teach new ones.", 15f, MutedColor);
            Stretch(empty.rectTransform, 20f);
            empty.alignment = TextAlignmentOptions.Center;

            At(Text("Hint", panel,
                "Click a recipe to see its costs  ·  Right-click or double-click to queue it\nHover anything for details  ·  Click the output to collect it", 13f, MutedColor),
                24f, -548f, 566f, 44f);

            // Right: the selected recipe.
            RectTransform detail = TopLeft(Right, -62f, RightWidth, 226f, "Selected", panel);
            detail.gameObject.AddComponent<Image>().color = WellColor;
            RectTransform iconRect = TopLeft(10f, -10f, 64f, 64f, "Icon", detail);
            var detailIcon = iconRect.gameObject.AddComponent<Image>();
            detailIcon.preserveAspect = true;
            detailIcon.raycastTarget = false;
            TMP_Text detailName = At(Text("Name", detail, "Choose a recipe", 18f, BodyColor), 84f, -10f, 270f, 64f);
            detailName.alignment = TextAlignmentOptions.MidlineLeft;
            TMP_Text detailBody = At(Text("Body", detail, "", 14f, BodyColor), 10f, -80f, 344f, 96f);
            detailBody.overflowMode = TextOverflowModes.Ellipsis;
            Button queueButton = MakeButton("Queue Button", detail, "Queue", 16f);
            Place((RectTransform)queueButton.transform, new Vector2(1f, 0f), new Vector2(-10f, 10f), new Vector2(140f, 36f));

            // Right: the queue and its progress bar.
            At(Text("Queue Title", panel, "Queue", 18f, BodyColor), Right, -298f, 200f, 26f);
            Button cancel = MakeButton("Cancel Button", panel, "Cancel last", 14f);
            Place((RectTransform)cancel.transform, new Vector2(0f, 1f), new Vector2(Right + RightWidth - 130f, -296f), new Vector2(130f, 28f), new Vector2(0f, 1f));
            RectTransform queueGrid = TopLeft(Right, -330f, RightWidth, 56f, "Queue Grid", panel);
            Grid(queueGrid, 56f, 6, 0);

            RectTransform bar = TopLeft(Right, -394f, RightWidth, 26f, "Progress Bar", panel);
            bar.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            RectTransform fill = Rect("Fill", bar, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = new Color(0.55f, 0.72f, 0.33f, 1f);
            fillImage.raycastTarget = false;
            TMP_Text progressLabel = Text("Label", bar, "Idle", 13f, BodyColor);
            Stretch(progressLabel.rectTransform, 6f);
            progressLabel.alignment = TextAlignmentOptions.Center;
            progressLabel.enableAutoSizing = true;
            progressLabel.fontSizeMin = 9f;
            progressLabel.fontSizeMax = 13f;
            progressLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // Right: the output slot as a grid.
            TMP_Text outputLabel = At(Text("Output Title", panel, "Output", 16f, BodyColor), Right, -432f, RightWidth, 26f);
            RectTransform outputGrid = TopLeft(Right, -462f, 242f, 118f, "Output Grid", panel);
            Grid(outputGrid, 56f, 4, 0);
            Button collect = MakeButton("Collect Button", panel, "Collect", 16f);
            Place((RectTransform)collect.transform, new Vector2(0f, 1f), new Vector2(Right + RightWidth - 110f, -462f), new Vector2(110f, 40f), new Vector2(0f, 1f));

            // The tooltip goes last so it draws over everything.
            RectTransform tip = Rect("Tooltip", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(320f, 100f));
            var tipImage = tip.gameObject.AddComponent<Image>();
            tipImage.color = new Color(0.03f, 0.03f, 0.04f, 0.97f);
            tipImage.raycastTarget = false;
            var tipLayout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
            tipLayout.padding = new RectOffset(10, 10, 8, 10);
            tipLayout.spacing = 4f;
            tipLayout.childControlWidth = true;
            tipLayout.childControlHeight = true;
            tipLayout.childForceExpandWidth = true;
            tipLayout.childForceExpandHeight = false;
            tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text tipTitle = Text("Title", tip, "", 18f, TitleColor);
            TMP_Text tipBody = Text("Body", tip, "", 14f, BodyColor);

            Wire(tooltip, ("panel", tip), ("titleLabel", tipTitle), ("bodyLabel", tipBody));
            Wire(uiWindow, ("content", panel.gameObject));
            var windowData = new SerializedObject(uiWindow);
            windowData.FindProperty("id").intValue = (int)UIWindowId.Station;
            windowData.ApplyModifiedPropertiesWithoutUndo();
            Wire(view,
                ("window", uiWindow), ("titleLabel", title), ("closeButton", close), ("tooltip", tooltip), ("slotPrefab", slotPrefab),
                ("tabBar", tabBar), ("tabTemplate", tabTemplate), ("recipeGrid", recipeGrid), ("emptyLabel", empty),
                ("detailIcon", detailIcon), ("detailName", detailName), ("detailBody", detailBody), ("queueButton", queueButton),
                ("queueGrid", queueGrid), ("progressFill", fill), ("progressLabel", progressLabel), ("cancelButton", cancel),
                ("outputGrid", outputGrid), ("outputLabel", outputLabel), ("collectButton", collect),
                ("actionBar", actionBar), ("actionTemplate", actionTemplate));

            panel.gameObject.SetActive(false);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"Built {PrefabPath}.", prefab);
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void Wire(Object target, params (string field, Object value)[] fields)
    {
        var data = new SerializedObject(target);
        foreach ((string field, Object value) in fields)
        {
            SerializedProperty property = data.FindProperty(field);
            if (property == null)
            {
                throw new System.InvalidOperationException($"{target.GetType().Name} has no field '{field}'.");
            }

            property.objectReferenceValue = value;
        }

        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static RectTransform TopLeft(float x, float y, float width, float height, string name, Transform parent)
    {
        Vector2 corner = new Vector2(0f, 1f);
        return Rect(name, parent, corner, corner, corner, new Vector2(x, y), new Vector2(width, height));
    }

    // Places a label by its top-left corner, in the parent's top-left space.
    private static TMP_Text At(TMP_Text label, float x, float y, float width, float height)
    {
        Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
        return label;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot ?? anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-2f * inset, -2f * inset);
    }

    private static TMP_Text Text(string name, Transform parent, string text, float size, Color color)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null)
        {
            label.font = TMP_Settings.defaultFontAsset;
        }

        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.raycastTarget = false;
        label.richText = true;
        return label;
    }

    private static Button MakeButton(string name, Transform parent, string text, float size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonColor;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = Text("Label", rect, text, size, ButtonText);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return button;
    }

    // Lets a layout size the button to its label.
    private static void FitToLabel(Button button, int padding, float height)
    {
        var layout = button.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    private static void Configure(HorizontalLayoutGroup layout, float spacing, TextAnchor alignment)
    {
        layout.spacing = spacing;
        layout.childAlignment = alignment;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
    }

    private static void Grid(RectTransform root, float cell, int columns, int padding)
    {
        var grid = root.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(cell, cell);
        grid.spacing = new Vector2(6f, 6f);
        grid.padding = new RectOffset(padding, padding, padding, padding);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
    }
}
