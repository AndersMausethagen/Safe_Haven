using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds Prefabs/UI/StationWindow.prefab, the crafting station window (StationWindow), from code (menu: Safe Haven > UI >
/// Build Recipe Browsers), in the Hearthline style. Rebuilding overwrites the prefab; scene instances keep working because
/// the prefab's asset GUID stays the same. Add the prefab under Screen Canvas in scenes with crafting stations.
/// </summary>
public static class StationWindowBuilder
{
    public const string PrefabPath = "Assets/_Project/Prefabs/UI/StationWindow.prefab";
    private const string SlotPrefabPath = "Assets/_Project/Prefabs/UI/ItemSlot.prefab";

    private const float Right = 764f;
    private const float RightWidth = 312f;

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
            UIStyle style = UIStyle.Current;
            Stretch((RectTransform)root.transform);
            var canvas = root.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 12;
            root.AddComponent<GraphicRaycaster>();
            var uiWindow = root.AddComponent<UIWindow>();
            var view = root.AddComponent<StationWindow>();

            RectTransform panel = Rect("Panel", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 20f), new Vector2(1100f, 640f));
            panel.gameObject.AddComponent<Image>();
            panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1f, -1f);
            panel.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
            var tooltip = panel.gameObject.AddComponent<ItemTooltipView>();

            TMP_Text title = At(Text("Title", panel, "Station", 26f, style.Text), 24f, -16f, 500f, 34f);
            style.Title(title);
            Button close = MakeButton("Close Button", panel, "X", 18f);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(40f, 36f));

            RectTransform actionBar = Rect("Action Bar", panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-66f, -16f), new Vector2(560f, 36f));
            var actionLayout = actionBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            Configure(actionLayout, 8f, TextAnchor.UpperRight);
            Button actionTemplate = MakeButton("Action Template", actionBar, "Action", 15f);
            FitToLabel(actionTemplate, 14, 36f);

            // Left: choosing recipes.
            RecipeBrowser browser = RecipeBrowserBuilder.Add(panel, 24f, -62f, 724f, 554f, 378f);

            // Right: the queue and its progress bar.
            At(Text("Queue Title", panel, "Queue", 16f, style.Accent), Right, -62f, 200f, 26f);
            Button cancel = MakeButton("Cancel Button", panel, "Cancel last", 14f);
            Place((RectTransform)cancel.transform, new Vector2(0f, 1f), new Vector2(Right + RightWidth - 120f, -60f), new Vector2(120f, 28f), new Vector2(0f, 1f));
            RectTransform bar = TopLeft(Right, -96f, RightWidth, 26f, "Progress Bar", panel);
            bar.gameObject.AddComponent<Image>().color = style.Inset;
            RectTransform fill = Rect("Fill", bar, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = style.Good;
            fillImage.raycastTarget = false;
            TMP_Text progressLabel = Text("Label", bar, "Idle", 13f, style.Text);
            Stretch(progressLabel.rectTransform, 6f);
            progressLabel.alignment = TextAlignmentOptions.Center;
            progressLabel.enableAutoSizing = true;
            progressLabel.fontSizeMin = 9f;
            progressLabel.fontSizeMax = 13f;
            progressLabel.textWrappingMode = TextWrappingModes.NoWrap;

            RectTransform queueGrid = TopLeft(Right, -130f, RightWidth, 118f, "Queue Grid", panel);
            Grid(queueGrid, 56f, 5, 0);

            // Right: what is waiting to be collected. One kind at a time, so four cells show any amount.
            TMP_Text outputLabel = At(Text("Output Title", panel, "Output", 16f, style.Accent), Right, -262f, RightWidth, 26f);
            RectTransform outputGrid = TopLeft(Right, -294f, RightWidth, 56f, "Output Grid", panel);
            Grid(outputGrid, 56f, 4, 0);
            Button collect = MakeButton("Collect Button", panel, "Collect", 16f);
            Place((RectTransform)collect.transform, new Vector2(0f, 1f), new Vector2(Right, -358f), new Vector2(RightWidth, 40f), new Vector2(0f, 1f));
            At(Text("Hint", panel, "Right-click or double-click a recipe to queue one.\nStations keep working while you are away.", 13f, style.Muted),
                Right, -566f, RightWidth, 50f);

            // The tooltip goes last so it draws over everything.
            RectTransform tip = Rect("Tooltip", panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 1f), Vector2.zero, new Vector2(320f, 100f));
            var tipImage = tip.gameObject.AddComponent<Image>();
            tipImage.raycastTarget = false;
            tip.gameObject.AddComponent<UIRole>().Set(UIRole.Kind.Window);
            var tipLayout = tip.gameObject.AddComponent<VerticalLayoutGroup>();
            tipLayout.padding = new RectOffset(10, 10, 8, 10);
            tipLayout.spacing = 4f;
            tipLayout.childControlWidth = true;
            tipLayout.childControlHeight = true;
            tipLayout.childForceExpandWidth = true;
            tipLayout.childForceExpandHeight = false;
            tip.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text tipTitle = Text("Title", tip, "", 18f, style.Text);
            style.Title(tipTitle);
            TMP_Text tipBody = Text("Body", tip, "", 14f, style.Text);

            Wire(tooltip, ("panel", tip), ("titleLabel", tipTitle), ("bodyLabel", tipBody));
            Wire(uiWindow, ("content", panel.gameObject));
            var windowData = new SerializedObject(uiWindow);
            windowData.FindProperty("id").intValue = (int)UIWindowId.Station;
            windowData.ApplyModifiedPropertiesWithoutUndo();
            Wire(view,
                ("window", uiWindow), ("titleLabel", title), ("closeButton", close), ("tooltip", tooltip), ("slotPrefab", slotPrefab), ("recipes", browser),
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

    internal static void Wire(Object target, params (string field, Object value)[] fields)
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

    internal static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
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

    internal static RectTransform TopLeft(float x, float y, float width, float height, string name, Transform parent)
    {
        Vector2 corner = new Vector2(0f, 1f);
        return Rect(name, parent, corner, corner, corner, new Vector2(x, y), new Vector2(width, height));
    }

    // Places a label by its top-left corner, in the parent's top-left space.
    internal static TMP_Text At(TMP_Text label, float x, float y, float width, float height)
    {
        Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, height));
        return label;
    }

    internal static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot ?? anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    internal static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(-2f * inset, -2f * inset);
    }

    internal static TMP_Text Text(string name, Transform parent, string text, float size, Color color)
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
        label.textWrappingMode = TextWrappingModes.Normal;
        return label;
    }

    internal static Button MakeButton(string name, Transform parent, string text, float size, UIRole.Kind role = UIRole.Kind.Button)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        var image = rect.gameObject.AddComponent<Image>();
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = Text("Label", rect, text, size, UIStyle.Current.Text);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        rect.gameObject.AddComponent<UIRole>().Set(role);
        return button;
    }

    // Lets a layout size the button to its label.
    internal static void FitToLabel(Button button, int padding, float height)
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

    internal static void Configure(HorizontalLayoutGroup layout, float spacing, TextAnchor alignment)
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
