using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Reports when a row is pointed at or reached with the keyboard or a gamepad, so its help line can show.</summary>
public sealed class OptionsHover : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public Action Entered;

    public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();

    public void OnSelect(BaseEventData eventData) => Entered?.Invoke();
}

/// <summary>Says when a slider is let go, for settings that move the slider itself if applied while dragging (the UI size).</summary>
public sealed class OptionsSliderGrip : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool IsHeld { get; private set; }
    public Action Released;

    public void OnPointerDown(PointerEventData eventData) => IsHeld = true;

    public void OnPointerUp(PointerEventData eventData)
    {
        IsHeld = false;
        Released?.Invoke();
    }
}

/// <summary>
/// Shrinks a fixed-size, centred window until it fits inside its parent (the screen) with a margin; never enlarges it.
/// The canvas follows the UI size option by shrinking its reference height (1080 units at 1x, 720 at 1.5x), so a
/// 900-unit window fell off the top and bottom at large UI sizes; this keeps it whole, as big as the screen allows.
/// </summary>
public sealed class FitInsideParent : MonoBehaviour
{
    public float Margin = 24f;

    private Vector2 lastRoom = new Vector2(-1f, -1f);
    private Vector2 lastSize;

    private void OnEnable() => Fit(true);

    // The screen or the UI size can change while the window is open; a size compare a frame is all this costs.
    private void LateUpdate() => Fit(false);

    private void Fit(bool force)
    {
        var rect = (RectTransform)transform;
        if (!(rect.parent is RectTransform parent))
        {
            return;
        }

        Vector2 room = parent.rect.size;
        Vector2 size = rect.rect.size;
        if (!force && room == lastRoom && size == lastSize)
        {
            return;
        }

        lastRoom = room;
        lastSize = size;
        if (size.x <= 0f || size.y <= 0f)
        {
            return;
        }

        float fit = Mathf.Min(1f, Mathf.Min((room.x - 2f * Margin) / size.x, (room.y - 2f * Margin) / size.y));
        fit = Mathf.Max(0.1f, fit);
        rect.localScale = new Vector3(fit, fit, 1f);
    }
}

/// <summary>
/// The parts the Options screen is made of, built in code like the game's other code-made windows. Colours follow them.
/// Rows return an action that redraws them from the setting, so the screen can refresh after a preset or a reset.
/// </summary>
public static class OptionsWidgets
{
    public const float RowHeight = 54f;
    private const float LabelWidth = 430f;

    public static Color Panel => UIStyle.Current.Panel;
    public static Color Rim => UIStyle.Current.Rim;
    public static Color WindowRim => UIStyle.Current.RimFor(RaidDirector.Current != null);
    public static Color Title => UIStyle.Current.Text;
    public static Color Body => UIStyle.Current.Text;
    public static Color Muted => UIStyle.Current.Muted;
    public static Color Control => UIStyle.Current.Control;
    public static Color ControlLit => UIStyle.Current.ControlLit;
    public static Color ControlOn => UIStyle.Current.ControlOn;
    public static Color Track => UIStyle.Current.Inset;

    public static RectTransform Rect(string name, Transform parent)
    {
        var holder = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)holder.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    /// <summary>A rectangle fixed to a corner or edge of its parent, by its anchor.</summary>
    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    public static void Stretch(RectTransform rect, Vector2 lowerLeft = default, Vector2 upperRight = default)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = lowerLeft;
        rect.offsetMax = upperRight;
    }

    public static TMP_Text Text(string name, Transform parent, string value, float size, Color colour, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        RectTransform rect = Rect(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    public static Button Button(string name, Transform parent, string label, float fontSize, Action clicked, out TMP_Text text)
    {
        RectTransform rect = Rect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Color.white;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colours = button.colors;
        colours.normalColor = Control;
        colours.highlightedColor = ControlLit;
        colours.selectedColor = ControlLit;
        colours.pressedColor = ControlOn;
        colours.disabledColor = new Color(0.14f, 0.12f, 0.1f, 1f);
        colours.colorMultiplier = 1f;
        colours.fadeDuration = 0.06f;
        button.colors = colours;
        button.onClick.AddListener(() => clicked());
        text = Text("Label", rect, label, fontSize, Body, TextAlignmentOptions.Center);
        Stretch((RectTransform)text.transform);
        return button;
    }

    public static void Section(Transform content, string title)
    {
        RectTransform row = NewRow(content, "Section " + title, RowHeight + 6f);
        TMP_Text text = Text("Title", row, title, 26f, Title, TextAlignmentOptions.BottomLeft);
        Stretch((RectTransform)text.transform, new Vector2(4f, 8f));
        text.fontStyle = FontStyles.Bold;
    }

    public static void Note(Transform content, string line)
    {
        RectTransform row = NewRow(content, "Note", 40f);
        Stretch((RectTransform)Text("Text", row, line, 20f, Muted).transform, new Vector2(4f, 0f));
    }

    /// <summary>A row with a label on the left; the control goes in the returned rectangle on the right.</summary>
    public static RectTransform LabelledRow(Transform content, string label, string help, Action<string> showHelp)
    {
        RectTransform row = NewRow(content, label, RowHeight);
        var hover = row.gameObject.AddComponent<OptionsHover>();
        hover.Entered = () => showHelp(help);
        // Pointer events need something to land on.
        row.gameObject.AddComponent<Image>().color = Color.clear;
        TMP_Text text = Text("Label", row, label, 23f, Body);
        Place((RectTransform)text.transform, new Vector2(0f, 0.5f), new Vector2(LabelWidth, RowHeight), new Vector2(4f, 0f));
        ((RectTransform)text.transform).pivot = new Vector2(0f, 0.5f);
        RectTransform control = Rect("Control", row);
        control.anchorMin = new Vector2(0f, 0f);
        control.anchorMax = new Vector2(1f, 1f);
        control.offsetMin = new Vector2(LabelWidth + 20f, 6f);
        control.offsetMax = new Vector2(-12f, -6f);
        return control;
    }

    /// <summary>An empty row of a list, for rows of their own shape.</summary>
    public static RectTransform NewRow(Transform content, string name, float height)
    {
        RectTransform row = Rect(name, content);
        var layout = row.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        return row;
    }

    /// <summary>On/Off, as one button.</summary>
    public static Action ToggleRow(Transform content, GameOption option, Action<string> showHelp)
    {
        RectTransform control = LabelledRow(content, option.Label, option.Help, showHelp);
        TMP_Text label = null;
        Image image = null;
        Button button = Button("Toggle", control, "", 22f, () =>
        {
            option.Value = option.On ? 0f : 1f;
            Refresh();
        }, out label);
        Stretch((RectTransform)button.transform);
        image = button.GetComponent<Image>();
        void Refresh()
        {
            label.text = option.ValueText;
            image.color = option.On ? new Color(1.5f, 1.4f, 1f, 1f) : Color.white;
        }

        WatchSelection(button, showHelp, option.Help);
        Refresh();
        return Refresh;
    }

    /// <summary>A value with arrows either side; clicking the middle goes on to the next.</summary>
    public static Action ChoiceRow(Transform content, string name, string help, Func<string[]> names, Func<int> get, Action<int> set, Action<string> showHelp)
    {
        RectTransform control = LabelledRow(content, name, help, showHelp);
        TMP_Text label = null;
        void Step(int by)
        {
            string[] all = names();
            if (all.Length > 0)
            {
                set((get() + by + all.Length) % all.Length);
                Refresh();
            }
        }

        Button previous = Button("Previous", control, "<", 26f, () => Step(-1), out _);
        Place((RectTransform)previous.transform, new Vector2(0f, 0.5f), new Vector2(52f, 42f), Vector2.zero);
        Button middle = Button("Value", control, "", 22f, () => Step(1), out label);
        var middleRect = (RectTransform)middle.transform;
        Stretch(middleRect, new Vector2(58f, 0f), new Vector2(-58f, 0f));
        Button next = Button("Next", control, ">", 26f, () => Step(1), out _);
        Place((RectTransform)next.transform, new Vector2(1f, 0.5f), new Vector2(52f, 42f), Vector2.zero);
        void Refresh()
        {
            string[] all = names();
            int index = get();
            label.text = index >= 0 && index < all.Length ? all[index] : "–";
        }

        WatchSelection(middle, showHelp, help);
        Refresh();
        return Refresh;
    }

    public static Action OptionChoiceRow(Transform content, GameOption option, Action<string> showHelp)
    {
        return ChoiceRow(content, option.Label, option.Help, () => option.Choices, () => option.Index, index => option.Value = index, showHelp);
    }

    /// <param name="onRelease">Apply only when the slider is let go.</param>
    public static Action SliderRow(Transform content, string name, string help, float min, float max, float step, Func<float> get, Action<float> set, Func<float, string> describe,
        Action<string> showHelp, bool onRelease = false)
    {
        RectTransform control = LabelledRow(content, name, help, showHelp);
        RectTransform area = Rect("Slider", control);
        Stretch(area, Vector2.zero, new Vector2(-96f, 0f));
        TMP_Text value = Text("Value", control, "", 22f, Body, TextAlignmentOptions.MidlineRight);
        Place((RectTransform)value.transform, new Vector2(1f, 0.5f), new Vector2(88f, 40f), Vector2.zero);

        RectTransform track = Rect("Track", area);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(0f, 10f);
        track.gameObject.AddComponent<Image>().color = Track;
        RectTransform fillArea = Rect("Fill Area", track);
        Stretch(fillArea);
        RectTransform fill = Rect("Fill", fillArea);
        fill.sizeDelta = Vector2.zero;
        fill.gameObject.AddComponent<Image>().color = Title;
        RectTransform handleArea = Rect("Handle Area", track);
        Stretch(handleArea, new Vector2(11f, 0f), new Vector2(-11f, 0f));
        RectTransform handle = Rect("Handle", handleArea);
        handle.sizeDelta = new Vector2(22f, 30f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = Color.white;

        var slider = area.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        ColorBlock colours = slider.colors;
        colours.normalColor = Body;
        colours.highlightedColor = Color.white;
        colours.selectedColor = Title;
        colours.pressedColor = Title;
        slider.colors = colours;

        OptionsSliderGrip grip = area.gameObject.AddComponent<OptionsSliderGrip>();
        float Snap(float raw) => step > 0f ? Mathf.Clamp(min + Mathf.Round((raw - min) / step) * step, min, max) : raw;
        void Refresh()
        {
            float current = get();
            slider.SetValueWithoutNotify(current);
            value.text = describe(current);
        }

        slider.onValueChanged.AddListener(raw =>
        {
            float snapped = Snap(raw);
            value.text = describe(snapped);
            if (!onRelease || !grip.IsHeld)
            {
                set(snapped);
            }
        });
        grip.Released = () =>
        {
            if (onRelease)
            {
                set(Snap(slider.value));
            }
        };
        WatchSelection(slider, showHelp, help);
        Refresh();
        return Refresh;
    }

    public static Action OptionSliderRow(Transform content, GameOption option, Action<string> showHelp, bool onRelease = false)
    {
        return SliderRow(content, option.Label, option.Help, option.Min, option.Max, option.Step, () => option.Value, value => option.Value = value, value => Describe(option, value),
            showHelp, onRelease);
    }

    private static string Describe(GameOption option, float value)
    {
        return Mathf.Approximately(option.Min, -option.Max) ? (value > 0f ? "+" : "") + Mathf.RoundToInt(value * 100f) : Mathf.RoundToInt(value * 100f) + "%";
    }

    /// <summary>The right row for the setting.</summary>
    public static Action Row(Transform content, GameOption option, Action<string> showHelp)
    {
        switch (option.Kind)
        {
            case OptionKind.Toggle:
                return ToggleRow(content, option, showHelp);
            case OptionKind.Choice:
                return OptionChoiceRow(content, option, showHelp);
            default:
                return OptionSliderRow(content, option, showHelp, option == GameOptions.UiScale);
        }
    }

    private static void WatchSelection(Selectable selectable, Action<string> showHelp, string help)
    {
        selectable.gameObject.AddComponent<OptionsHover>().Entered = () => showHelp(help);
    }
}
