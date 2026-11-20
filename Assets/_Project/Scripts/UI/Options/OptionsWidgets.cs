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
    private const float LabelWidth = 360f;

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

    /// <summary>
    /// A row with a label on the left and a gold bar at its edge while it has an unsaved change; the control goes in the returned rectangle
    /// on the right. <paramref name="markPending"/> lights the bar and the label (the row's refresh calls it).
    /// </summary>
    public static RectTransform LabelledRow(Transform content, OptionsFocus focus, Action<OptionsFocus> show, out Action<bool> markPending)
    {
        RectTransform row = NewRow(content, focus.Name, RowHeight);
        var hover = row.gameObject.AddComponent<OptionsHover>();
        hover.Entered = () => show(focus);
        // Pointer events need something to land on.
        row.gameObject.AddComponent<Image>().color = Color.clear;
        RectTransform bar = Place(Rect("Changed", row), new Vector2(0f, 0.5f), new Vector2(5f, RowHeight - 14f), Vector2.zero);
        var barImage = bar.gameObject.AddComponent<Image>();
        barImage.color = UIStyle.Current.Accent;
        barImage.raycastTarget = false;
        TMP_Text text = Text("Label", row, focus.Name, 23f, Body);
        Place((RectTransform)text.transform, new Vector2(0f, 0.5f), new Vector2(LabelWidth, RowHeight), new Vector2(16f, 0f));
        ((RectTransform)text.transform).pivot = new Vector2(0f, 0.5f);
        RectTransform control = Rect("Control", row);
        control.anchorMin = new Vector2(0f, 0f);
        control.anchorMax = new Vector2(1f, 1f);
        control.offsetMin = new Vector2(LabelWidth + 24f, 6f);
        control.offsetMax = new Vector2(-12f, -6f);
        markPending = pending =>
        {
            bar.gameObject.SetActive(pending);
            text.color = pending ? UIStyle.Current.Accent : Body;
        };
        markPending(false);
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
    public static Action ToggleRow(Transform content, OptionsFocus focus, Func<bool> get, Action<bool> set, Action<OptionsFocus> show)
    {
        RectTransform control = LabelledRow(content, focus, show, out Action<bool> markPending);
        TMP_Text label = null;
        Image image = null;
        Button button = Button("Toggle", control, "", 22f, () =>
        {
            set(!get());
            Refresh();
            show(focus);
        }, out label);
        Stretch((RectTransform)button.transform);
        image = button.GetComponent<Image>();
        void Refresh()
        {
            bool on = get();
            label.text = on ? "On" : "Off";
            image.color = on ? new Color(1.5f, 1.4f, 1f, 1f) : Color.white;
            markPending(focus.IsPending);
        }

        WatchSelection(button, show, focus);
        Refresh();
        return Refresh;
    }

    /// <summary>A value with arrows either side; clicking the middle goes on to the next.</summary>
    public static Action ChoiceRow(Transform content, OptionsFocus focus, Func<string[]> names, Func<int> get, Action<int> set, Action<OptionsFocus> show)
    {
        RectTransform control = LabelledRow(content, focus, show, out Action<bool> markPending);
        TMP_Text label = null;
        void Step(int by)
        {
            string[] all = names();
            if (all.Length > 0)
            {
                set((get() + by + all.Length) % all.Length);
                Refresh();
                show(focus);
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
            label.text = index >= 0 && index < all.Length ? all[index] : "-";
            markPending(focus.IsPending);
        }

        WatchSelection(middle, show, focus);
        Refresh();
        return Refresh;
    }

    public static Action SliderRow(Transform content, OptionsFocus focus, float min, float max, float step, Func<float> get, Action<float> set,
        Func<float, string> describe, Action<OptionsFocus> show)
    {
        RectTransform control = LabelledRow(content, focus, show, out Action<bool> markPending);
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

        float Snap(float raw) => step > 0f ? Mathf.Clamp(min + Mathf.Round((raw - min) / step) * step, min, max) : raw;
        void Refresh()
        {
            float current = get();
            slider.SetValueWithoutNotify(current);
            value.text = describe(current);
            markPending(focus.IsPending);
        }

        // Only the draft changes while dragging (OptionsDraft): nothing is applied until Save, so every step can show at once.
        slider.onValueChanged.AddListener(raw =>
        {
            float snapped = Snap(raw);
            value.text = describe(snapped);
            set(snapped);
            markPending(focus.IsPending);
            show(focus);
        });
        WatchSelection(slider, show, focus);
        Refresh();
        return Refresh;
    }

    /// <summary>
    /// The right row for a setting, editing <paramref name="draft"/>. <paramref name="set"/> replaces the plain draft change (the quality
    /// preset sets its members too).
    /// </summary>
    public static Action Row(Transform content, GameOption option, OptionsDraft draft, Action<OptionsFocus> show, Action<float> set = null)
    {
        OptionsFocus focus = OptionsFocus.For(option, draft);
        Action<float> change = set ?? (value => draft.Set(option, value));
        switch (option.Kind)
        {
            case OptionKind.Toggle:
                return ToggleRow(content, focus, () => draft.On(option), on => change(on ? 1f : 0f), show);
            case OptionKind.Choice:
                return ChoiceRow(content, focus, () => option.Choices, () => draft.Index(option), index => change(index), show);
            default:
                return SliderRow(content, focus, option.Min, option.Max, option.Step, () => draft.Get(option), change, option.TextFor, show);
        }
    }

    private static void WatchSelection(Selectable selectable, Action<OptionsFocus> show, OptionsFocus focus)
    {
        selectable.gameObject.AddComponent<OptionsHover>().Entered = () => show(focus);
    }
}

/// <summary>What the Options screen's details panel says of the row in focus: its name, what it does, its value now, saved and by default.</summary>
public sealed class OptionsFocus
{
    public string Name;
    public string Help;
    /// <summary>The value as it would be saved now (with an unsaved change, that one).</summary>
    public Func<string> Value;
    /// <summary>The value as it is saved.</summary>
    public Func<string> Saved;
    /// <summary>The value it has out of the box; null when there is none to give (a key, the window).</summary>
    public string Default;
    public Func<bool> Pending;

    public bool IsPending => Pending != null && Pending();

    /// <summary>The focus of a GameOption row, read through the draft.</summary>
    public static OptionsFocus For(GameOption option, OptionsDraft draft) => new OptionsFocus
    {
        Name = option.Label,
        Help = option.Help,
        Value = () => option.TextFor(draft.Get(option)),
        Saved = () => option.ValueText,
        Default = option.TextFor(option.Default),
        Pending = () => draft.IsChanged(option),
    };
}
